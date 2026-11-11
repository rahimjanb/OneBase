using OneBase.Infrastructure.Storage;
using SkiaSharp;

namespace OneBase.AI.Tests;

/// <summary>Подготовка фото продукта: поворот по EXIF (снимок телефона «лёжа»), центральный кроп 4:3, без увеличения, прозрачность — на белом.</summary>
public class ImageProcessorTests
{
    private static readonly SkiaImageProcessor Images = new();

    /// <summary>JPEG: верхняя половина красная, нижняя синяя; orientation — EXIF-тег поворота (1 — как есть).</summary>
    private static byte[] Jpeg(int width, int height, ushort orientation)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.Red);
        using var paint = new SKPaint { Color = SKColors.Blue };
        surface.Canvas.DrawRect(0, height / 2f, width, height / 2f, paint);
        using var image = surface.Snapshot();
        var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 95).ToArray();
        // APP1 Exif сразу после SOI: TIFF big-endian, один тег 0x0112 (Orientation, SHORT).
        byte[] tiff = [(byte)'M', (byte)'M', 0, 42, 0, 0, 0, 8, 0, 1, 0x01, 0x12, 0, 3, 0, 0, 0, 1, (byte)(orientation >> 8), (byte)orientation, 0, 0, 0, 0, 0, 0];
        byte[] header = [(byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0];
        var length = 2 + header.Length + tiff.Length;
        return [0xFF, 0xD8, 0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. header, .. tiff, .. jpeg[2..]];
    }

    private static SKColor Pixel(byte[] jpeg, float x, float y)
    {
        using var bitmap = SKBitmap.Decode(jpeg);
        return bitmap.GetPixel((int)(x * (bitmap.Width - 1)), (int)(y * (bitmap.Height - 1)));
    }

    private static bool Reddish(SKColor c) => c.Red > 200 && c.Blue < 60;
    private static bool Bluish(SKColor c) => c.Blue > 200 && c.Red < 60;

    [Fact]
    public void Landscape_photo_is_cropped_to_the_centre_four_by_three_without_upscaling()
    {
        var result = Images.CropToJpeg(Jpeg(1000, 400, 1), 4, 3, [1600, 960, 480], 85)!;
        Assert.Equal((533, 400), (result.Width, result.Height));
        Assert.Equal([(533, 400), (533, 400), (480, 360)], result.Variants.Select(v => (v.Width, v.Height)));
        Assert.True(Reddish(Pixel(result.Variants[2].Jpeg, 0.5f, 0.1f)));
        Assert.True(Bluish(Pixel(result.Variants[2].Jpeg, 0.5f, 0.9f)));
    }

    [Fact]
    public void Exif_rotation_is_applied_before_cropping()
    {
        // Снято «лёжа» (6 — повернуть на 90° по часовой): красная верхняя половина кадра становится правой.
        var result = Images.CropToJpeg(Jpeg(800, 600, 6), 4, 3, [480], 85)!;
        Assert.Equal((480, 360), (result.Width, result.Height));
        Assert.True(Reddish(Pixel(result.Variants[0].Jpeg, 0.9f, 0.5f)));
        Assert.True(Bluish(Pixel(result.Variants[0].Jpeg, 0.1f, 0.5f)));

        // 8 — на 90° против часовой: красная половина — слева; 3 — на 180°: красная — снизу.
        var left = Images.CropToJpeg(Jpeg(800, 600, 8), 4, 3, [480], 85)!;
        Assert.True(Reddish(Pixel(left.Variants[0].Jpeg, 0.1f, 0.5f)));
        var flipped = Images.CropToJpeg(Jpeg(800, 600, 3), 4, 3, [480], 85)!;
        Assert.True(Reddish(Pixel(flipped.Variants[0].Jpeg, 0.5f, 0.9f)));
    }

    [Fact]
    public void Transparent_png_goes_on_white_and_garbage_is_rejected()
    {
        using var surface = SKSurface.Create(new SKImageInfo(400, 300, SKColorType.Rgba8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        using var image = surface.Snapshot();
        var png = image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
        var result = Images.CropToJpeg(png, 4, 3, [480], 85)!;
        Assert.Equal("#FFFFFF", result.Tint);
        Assert.Null(Images.CropToJpeg("hello"u8.ToArray(), 4, 3, [480], 85));
        Assert.Null(Images.CropToJpeg([], 4, 3, [480], 85));
    }
}
