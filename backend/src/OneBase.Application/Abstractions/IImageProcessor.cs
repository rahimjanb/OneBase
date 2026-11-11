namespace OneBase.Application.Abstractions;

/// <summary>
/// Подготовка фото к хранению: прочитать (JPEG, PNG, WebP; HEIC — где платформа умеет), повернуть по EXIF, вырезать центр в заданной
/// пропорции и сохранить в JPEG нужных ширин (меньше исходника — без увеличения). EXIF в результат не попадает (в нём бывают координаты).
/// Не картинка или формат не читается — null.
/// </summary>
public interface IImageProcessor
{
    ProcessedImage? CropToJpeg(byte[] data, int ratioWidth, int ratioHeight, IReadOnlyList<int> widths, int quality);
}

/// <summary>Результат: размер первого (самого большого) варианта, средний цвет кадра («#RRGGBB») и варианты в порядке запрошенных ширин.</summary>
public sealed record ProcessedImage(int Width, int Height, string Tint, IReadOnlyList<ImageVariant> Variants);

public sealed record ImageVariant(int Width, int Height, byte[] Jpeg);
