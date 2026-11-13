namespace OneBase.Application.Abstractions;

/// <summary>
/// Подготовка фото к хранению: прочитать (JPEG, PNG, WebP; HEIC — где платформа умеет), повернуть по EXIF, вырезать центр в заданной
/// пропорции и сохранить в JPEG нужных ширин (меньше исходника — без увеличения). EXIF в результат не попадает (в нём бывают координаты).
/// Не картинка или формат не читается — null.
/// </summary>
public interface IImageProcessor
{
    ProcessedImage? CropToJpeg(byte[] data, int ratioWidth, int ratioHeight, IReadOnlyList<int> widths, int quality);

    /// <summary>
    /// Фото товара: поворот по EXIF, прозрачные поля обрезаются, товар вписывается целиком в квадраты нужных размеров (без увеличения),
    /// прозрачность сохраняется — WebP. Не картинка, пустая (вся прозрачная) или слишком большая по пикселям — null.
    /// </summary>
    FittedImage? FitToWebp(byte[] data, IReadOnlyList<int> boxes, int quality);
}

/// <summary>Вписанное фото: размер самого крупного варианта и варианты в порядке запрошенных квадратов.</summary>
public sealed record FittedImage(int Width, int Height, IReadOnlyList<FittedVariant> Variants);

/// <summary>Вариант: сторона квадрата, в который вписан товар, итоговый размер и WebP.</summary>
public sealed record FittedVariant(int Box, int Width, int Height, byte[] Webp);

/// <summary>Результат: размер первого (самого большого) варианта, средний цвет кадра («#RRGGBB») и варианты в порядке запрошенных ширин.</summary>
public sealed record ProcessedImage(int Width, int Height, string Tint, IReadOnlyList<ImageVariant> Variants);

public sealed record ImageVariant(int Width, int Height, byte[] Jpeg);
