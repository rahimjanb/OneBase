namespace OneBase.Domain.Sales;

/// <summary>
/// Строка расходной накладной дилера в Таджикистане (1С Таджикистан, GET /sales): точка, ТП, товар, количество, цена, сумма в сомони.
/// Идентификаторов строк в API нет: накладная — номер и дата, строка — порядок в ответе. Изменённая или пропавшая в 1С накладная не
/// удаляется: прежние строки получают RemovedAt (в отчётах не участвуют), новые добавляются.
/// </summary>
public class TajikSaleLine
{
    public long Id { get; set; }

    public required string DocNumber { get; set; }

    public DateOnly DocDate { get; set; }

    /// <summary>Порядок строки в накладной (с 1) — как в ответе 1С.</summary>
    public int LineNo { get; set; }

    /// <summary>Покупатель — торговая точка, как в 1С (название, часто с городом или районом в конце).</summary>
    public required string Customer { get; set; }

    /// <summary>Торговый представитель.</summary>
    public string? Manager { get; set; }

    /// <summary>Код номенклатуры 1С Таджикистана (НФ-00000358).</summary>
    public required string ProductCode { get; set; }

    public required string ProductName { get; set; }

    /// <summary>Количество в единицах 1С Таджикистана (коробка или пачка — у товаров по-разному).</summary>
    public decimal Qty { get; set; }

    public decimal Price { get; set; }

    /// <summary>Сумма строки, сомони (после скидки).</summary>
    public decimal Amount { get; set; }

    public decimal Discount { get; set; }

    public decimal? DiscountPercent { get; set; }

    /// <summary>Когда строка загружена (или подтверждена) последней синхронизацией.</summary>
    public DateTimeOffset SyncedAt { get; set; }

    /// <summary>Накладная изменилась или пропала в 1С: строка больше не действует (история не удаляется).</summary>
    public DateTimeOffset? RemovedAt { get; set; }
}

/// <summary>Остаток склада дилера на дату (GET /sklad): снимок дня обновляется каждой синхронизацией, прошлые дни остаются историей.</summary>
public class TajikStockItem
{
    /// <summary>День снимка (Душанбе, UTC+5).</summary>
    public DateOnly Date { get; set; }

    public required string ProductCode { get; set; }

    public required string Warehouse { get; set; }

    public required string ProductName { get; set; }

    public decimal Qty { get; set; }

    public DateTimeOffset SyncedAt { get; set; }
}

/// <summary>
/// Регион Таджикистана для отчёта: название и слова, по которым точка относится к региону (город, район, рынок в названии точки).
/// Регион точки: закреплённый вручную → по словам в названии → регион ТП по умолчанию → «не определён».
/// </summary>
public class TajikRegion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public required string Name { get; set; }

    /// <summary>Слова в названии точки (без учёта регистра и таджикских букв: ҷ = ч, ӯ = у, ҳ = х, қ = к, ғ = г, ӣ = и, ё = е).</summary>
    public string[] Keywords { get; set; } = [];

    public int SortOrder { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }
}

/// <summary>Закрепление за регионом: ТП (регион по умолчанию для его точек) или отдельная точка (главнее слов в названии).</summary>
public class TajikRegionAssignment
{
    public const string AgentKind = "agent";
    public const string StoreKind = "store";

    /// <summary>agent | store.</summary>
    public required string Kind { get; set; }

    /// <summary>Имя ТП или название точки — как в 1С.</summary>
    public required string Name { get; set; }

    public Guid RegionId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }
}

/// <summary>Запуск синхронизации 1С Таджикистан → OneBase: окно, что изменилось, ошибка (без ключа доступа).</summary>
public class TajikSyncRun
{
    public long Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>schedule | manual | full.</summary>
    public required string Trigger { get; set; }

    /// <summary>running | ok | failed.</summary>
    public required string Status { get; set; }

    /// <summary>С какого дня запрошены продажи.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Строк продаж в ответе (в окне).</summary>
    public int Lines { get; set; }

    public int Documents { get; set; }

    public int Added { get; set; }

    public int Changed { get; set; }

    public int Removed { get; set; }

    /// <summary>Строк, которые не удалось прочитать (без номера, даты или товара) — их накладные не трогаются.</summary>
    public int Rejected { get; set; }

    public int StockRows { get; set; }

    public string? Error { get; set; }
}
