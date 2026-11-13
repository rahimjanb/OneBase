using OneBase.Application.Abstractions;
using SkiaSharp;

namespace OneBase.Infrastructure.Storage;

/// <summary>
/// Фото через SkiaSharp: JPEG читается сразу уменьшенным (масштаб декодера), поворот по EXIF. Техкарты — центральный кроп в пропорции,
/// JPEG нужных ширин на белом фоне (прозрачность PNG — не чёрная), средний цвет кадра. Товары — вписаны целиком без прозрачных полей, WebP с
/// прозрачностью. Огромные по пикселям картинки (не JPEG) не читаются целиком — null.
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor
{
    private const long MaxPixels = 60_000_000;

    public ProcessedImage? CropToJpeg(byte[] data, int ratioWidth, int ratioHeight, IReadOnlyList<int> widths, int quality)
    {
        if (data.Length == 0 || widths.Count == 0 || ratioWidth <= 0 || ratioHeight <= 0)
        {
            return null;
        }

        using var encoded = SKData.CreateCopy(data);
        using var codec = SKCodec.Create(encoded);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
        {
            return null;
        }

        var origin = codec.EncodedOrigin;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        // Кадр после поворота: сколько из него нужно для самой большой ширины — во столько и уменьшаем при чтении. Декодер умеет не любой
        // масштаб (JPEG — восьмыми) и может уменьшить сильнее: тогда берём масштаб крупнее, пока кадра хватает на нужную ширину.
        var needed = widths.Max();
        var size = codec.Info.Size;
        for (var scale = Math.Min(1f, (float)needed / Math.Max(1, CropWidth(codec.Info.Size, swap, ratioWidth, ratioHeight))); scale < 1f; scale += 0.125f)
        {
            var scaled = codec.GetScaledDimensions(scale);
            if (CropWidth(scaled, swap, ratioWidth, ratioHeight) >= needed)
            {
                size = scaled;
                break;
            }
        }

        if ((long)size.Width * size.Height > MaxPixels)
        {
            return null;
        }

        using var decoded = SKBitmap.Decode(codec, new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (decoded is null)
        {
            return null;
        }

        using var upright = Orient(decoded, origin);
        var w = upright.Width;
        var h = upright.Height;
        var cw = Math.Min(w, (int)Math.Round((double)h * ratioWidth / ratioHeight));
        var ch = Math.Min(h, (int)Math.Round((double)cw * ratioHeight / ratioWidth));
        if (cw <= 0 || ch <= 0)
        {
            return null;
        }

        var source = SKRect.Create((w - cw) / 2f, (h - ch) / 2f, cw, ch);
        using var image = SKImage.FromBitmap(upright);
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        var variants = new List<ImageVariant>(widths.Count);
        foreach (var wanted in widths)
        {
            var vw = Math.Min(wanted, cw);
            var vh = Math.Max(1, (int)Math.Round((double)vw * ch / cw));
            using var surface = SKSurface.Create(new SKImageInfo(vw, vh, SKColorType.Rgba8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.White);
            surface.Canvas.DrawImage(image, source, SKRect.Create(vw, vh), sampling);
            using var snapshot = surface.Snapshot();
            using var jpeg = snapshot.Encode(SKEncodedImageFormat.Jpeg, quality);
            variants.Add(new ImageVariant(vw, vh, jpeg.ToArray()));
        }

        return new ProcessedImage(variants[0].Width, variants[0].Height, Tint(image, source, sampling), variants);
    }

    public FittedImage? FitToWebp(byte[] data, IReadOnlyList<int> boxes, int quality)
    {
        if (data.Length == 0 || boxes.Count == 0)
        {
            return null;
        }

        using var encoded = SKData.CreateCopy(data);
        using var codec = SKCodec.Create(encoded);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
        {
            return null;
        }

        // Большой JPEG читаем уменьшенным, но с запасом на обрезку полей: большая сторона — не меньше полуторного самого крупного квадрата.
        var needed = boxes.Max();
        var size = codec.Info.Size;
        for (var scale = Math.Min(1f, needed * 1.5f / Math.Max(size.Width, size.Height)); scale < 1f; scale += 0.125f)
        {
            var scaled = codec.GetScaledDimensions(scale);
            if (Math.Max(scaled.Width, scaled.Height) >= needed * 1.5f)
            {
                size = scaled;
                break;
            }
        }

        if ((long)size.Width * size.Height > MaxPixels)
        {
            return null;
        }

        using var decoded = SKBitmap.Decode(codec, new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (decoded is null)
        {
            return null;
        }

        using var upright = Orient(decoded, codec.EncodedOrigin);
        var bounds = codec.Info.AlphaType == SKAlphaType.Opaque ? new SKRectI(0, 0, upright.Width, upright.Height) : VisibleBounds(upright);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        using var image = SKImage.FromBitmap(upright);
        var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        var variants = new List<FittedVariant>(boxes.Count);
        foreach (var box in boxes)
        {
            var scale = Math.Min(1d, (double)box / Math.Max(bounds.Width, bounds.Height));
            var vw = Math.Max(1, (int)Math.Round(bounds.Width * scale));
            var vh = Math.Max(1, (int)Math.Round(bounds.Height * scale));
            using var surface = SKSurface.Create(new SKImageInfo(vw, vh, SKColorType.Rgba8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawImage(image, SKRect.Create(bounds.Left, bounds.Top, bounds.Width, bounds.Height), SKRect.Create(vw, vh), sampling);
            using var snapshot = surface.Snapshot();
            using var webp = snapshot.Encode(SKEncodedImageFormat.Webp, quality);
            if (webp is null)
            {
                return null;
            }

            variants.Add(new FittedVariant(box, vw, vh, webp.ToArray()));
        }

        var largest = variants.MaxBy(v => v.Width * v.Height)!;
        return new FittedImage(largest.Width, largest.Height, variants);
    }

    /// <summary>Рамка видимой части: пиксели заметнее почти прозрачных (альфа больше 8) — по ним обрезаются прозрачные поля вырезки.</summary>
    private static SKRectI VisibleBounds(SKBitmap bitmap)
    {
        var pixels = bitmap.GetPixelSpan();
        int w = bitmap.Width, h = bitmap.Height, stride = bitmap.RowBytes;
        int left = w, top = h, right = -1, bottom = -1;
        for (var y = 0; y < h; y++)
        {
            var row = pixels.Slice(y * stride, w * 4);
            for (var x = 0; x < w; x++)
            {
                if (row[x * 4 + 3] <= 8)
                {
                    continue;
                }

                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                bottom = y;
            }
        }

        return right < 0 ? SKRectI.Empty : new SKRectI(left, top, right + 1, bottom + 1);
    }

    /// <summary>Ширина кадра в пропорции у картинки такого размера (с учётом поворота).</summary>
    private static int CropWidth(SKSizeI size, bool swap, int ratioWidth, int ratioHeight)
    {
        var (w, h) = swap ? (size.Height, size.Width) : (size.Width, size.Height);
        return Math.Min(w, (int)Math.Round((double)h * ratioWidth / ratioHeight));
    }

    /// <summary>Средний цвет кадра: кадр, уменьшенный до 1×1 (с мипмапами — честное среднее), «#RRGGBB».</summary>
    private static string Tint(SKImage image, SKRect source, SKSamplingOptions sampling)
    {
        using var surface = SKSurface.Create(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.White);
        surface.Canvas.DrawImage(image, source, SKRect.Create(1, 1), sampling);
        using var pixel = new SKBitmap(1, 1, SKColorType.Rgba8888, SKAlphaType.Premul);
        surface.ReadPixels(pixel.Info, pixel.GetPixels(), pixel.RowBytes, 0, 0);
        var c = pixel.GetPixel(0, 0);
        return $"#{c.Red:X2}{c.Green:X2}{c.Blue:X2}";
    }

    /// <summary>Поворот и отражение по EXIF: картинка «как снята» → как её видит человек.</summary>
    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        int w = source.Width, h = source.Height;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var result = new SKBitmap(new SKImageInfo(swap ? h : w, swap ? w : h, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(result);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(w, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(w, h);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, h);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(h, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(h, w);
                canvas.Scale(-1, -1);
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, w);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(source, 0, 0);
        return result;
    }
}
