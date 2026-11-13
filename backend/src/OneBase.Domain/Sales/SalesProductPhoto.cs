using OneBase.Domain.Common;

namespace OneBase.Domain.Sales;

/// <summary>
/// Фото товара Linko в «Продажах» (ассортимент, категория, артикул). У товара может быть несколько фото: SortOrder 0 — главное (упаковка),
/// дальше — штучная обёртка и другие ракурсы. Файлы — в хранилище, готовыми WebP: товар вписан целиком в квадрат 160, 400 и 800 px
/// (ключи — sales/product-photos/{FileId}-{размер}.webp). FileId новый при каждой загрузке — по нему адрес картинки, браузер хранит её
/// навсегда. Similar — фото другого товара той же линейки (другой вес или упаковка), поставленное автоматически, пока своего нет;
/// SimilarFrom — код товара, чьё это фото. Source — seed (из набора фото при сервере) или upload (загрузил сотрудник).
/// </summary>
public class SalesProductPhoto : Entity
{
    /// <summary>Товар Linko (linko.Products.Id).</summary>
    public long ProductId { get; set; }

    public int SortOrder { get; set; }

    public Guid FileId { get; set; }

    /// <summary>Размер самого крупного варианта (до 800 px по большей стороне).</summary>
    public int Width { get; set; }

    public int Height { get; set; }

    /// <summary>Сколько весят все варианты вместе.</summary>
    public long SizeBytes { get; set; }

    public bool Similar { get; set; }

    public string? SimilarFrom { get; set; }

    public required string Source { get; set; }

    public Guid? UploadedById { get; set; }
}

/// <summary>
/// Код товара, которому фото из набора при сервере уже поставлены (один раз): удалённое вручную фото при следующем запуске не возвращается,
/// а товар, которого в базе ещё не было (Linko не загрузил), получит фото, когда появится.
/// </summary>
public class SalesProductPhotoSeed
{
    public required string Code { get; set; }

    public DateTimeOffset AppliedAt { get; set; }
}
