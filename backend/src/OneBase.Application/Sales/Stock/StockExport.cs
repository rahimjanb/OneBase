using System.Globalization;
using System.Text.Json;
using OneBase.Application.Abstractions;
using OneBase.Application.Reports;

namespace OneBase.Application.Sales.Stock;

/// <summary>
/// Колонка выгрузки «Рек. остатка» в Excel: ключ (в адресе запроса), заголовок на русском и узбекском, формат, значение строки товара и —
/// если у колонки есть разрез — клетки склада (лист «По складам»). Sum=false — в итоговой строке не складывается (дни, цена, вес).
/// </summary>
public sealed record StockExportColumn(
    string Key,
    string Ru,
    string Uz,
    string Format,
    Func<StockItem, object?> Item,
    Func<StockItem, StockCell, object?>? Cell = null,
    bool Sum = true)
{
    public string Header => UiLanguage.IsUzbek ? Uz : Ru;
}

/// <summary>
/// «Собрать в Excel» на странице «Рек. остаток»: те же строки, что на странице (охват и фильтры), в выбранных колонках — лист «Сводка»;
/// по желанию — лист «По складам»: каждый товар по заводу и складам дилеров охвата, где есть остаток или продажи. Числа — с сервера
/// (StockService), как на странице; кг, коробки и сумы — в своих колонках, без переключателя единиц.
/// </summary>
public static class StockExport
{
    private static string F(decimal? v) => v?.ToString(CultureInfo.InvariantCulture) ?? "";

    /// <summary>Скорость, дни и коробки — до десятых, веса — до граммов: в файле те же цифры, что видны на странице.</summary>
    private static decimal? R1(decimal? v) => v is { } d ? Math.Round(d, 1) : null;

    private static decimal? R3(decimal? v) => v is { } d ? Math.Round(d, 3) : null;

    public static readonly IReadOnlyList<StockExportColumn> Columns =
    [
        new("name", "Наименование", "Nomi", ReportFormats.Text, i => i.Name),
        new("code", "Код", "Kod", ReportFormats.Text, i => i.Code),
        new("top", "ТОП", "TOP", ReportFormats.Text, i => i.Top ? "ТОП" : null),
        new("cat", "Категория", "Kategoriya", ReportFormats.Text, i => i.Category),
        new("pack", "Фасовка, кг", "Qadoq, kg", ReportFormats.Number, i => R3(i.PackKg), Sum: false),
        new("unitKg", "Вес штуки, кг", "Dona og'irligi, kg", ReportFormats.Number, i => R3(i.UnitKg), Sum: false),
        new("boxKg", "Вес коробки, кг", "Quti og'irligi, kg", ReportFormats.Number, i => R3(i.BoxKg), Sum: false),
        new("stockKg", "Остаток, кг", "Qoldiq, kg", ReportFormats.Kg, i => i.Kg, (_, c) => c.Kg),
        new("stockBoxes", "Остаток, коробки", "Qoldiq, quti", ReportFormats.Number, i => R1(i.Boxes), (_, c) => R1(c.Boxes)),
        new("stockPieces", "Остаток, штуки", "Qoldiq, dona", ReportFormats.Number, i => R3(i.Pieces), (_, c) => R3(c.Pieces)),
        new("perDay", "Продажи в день, кг", "Kunlik savdo, kg", ReportFormats.Number, i => R1(i.KgPerDay), (_, c) => R1(c.KgPerDay)),
        new("rawPerDay", "Продажи в день без поправки на аутсток, кг", "Autstoksiz kunlik savdo, kg", ReportFormats.Number, i => R1(i.RawKgPerDay), (_, c) => R1(c.RawKgPerDay)),
        new("zeroDays", "Дней без товара в прошлом месяце", "O'tgan oyda tovarsiz kunlar", ReportFormats.Integer, _ => null, (_, c) => c.ZeroDays, Sum: false),
        new("days", "Хватит, дн.", "Yetadi, kun", ReportFormats.Number, i => R1(i.DaysOfCover), (_, c) => R1(c.DaysOfCover), Sum: false),
        new("need15", "Запас на 15 дн., кг", "15 kunlik zaxira, kg", ReportFormats.Kg, i => i.Need15Kg, (_, c) => c.Need15Kg),
        new("need30", "Запас на 30 дн., кг", "30 kunlik zaxira, kg", ReportFormats.Kg, i => i.Need30Kg, (_, c) => c.KgPerDay * 30),
        new("price", "Цена входа, сум", "Kirim narxi, so'm", ReportFormats.Money, i => i.Price, (i, _) => i.Price, Sum: false),
        new("value", "Сумма запаса, сум", "Zaxira summasi, so'm", ReportFormats.Money, i => i.ValueSum, (_, c) => c.ValueSum),
        new("orderKg", "Рек. заказ, кг", "Tavsiya buyurtma, kg", ReportFormats.Kg, i => i.OrderKg, (_, c) => c.OrderKg),
        new("orderBoxes", "Рек. заказ, коробки", "Tavsiya buyurtma, quti", ReportFormats.Number, i => R1(i.OrderBoxes), (_, c) => R1(c.OrderBoxes)),
        new("orderSum", "Рек. заказ, сум", "Tavsiya buyurtma, so'm", ReportFormats.Money, i => i.OrderSum, (_, c) => c.OrderSum),
        new("status", "Статус", "Holat", ReportFormats.Text, i => StatusLabel(i.Status), (_, c) => StatusLabel(c.Status)),
        new("factoryKg", "Завод, кг", "Zavod, kg", ReportFormats.Kg, i => i.Factory?.Kg),
        new("factoryOrder", "Не хватает на заводе, кг", "Zavodda yetishmaydi, kg", ReportFormats.Kg, i => i.Factory?.OrderKg),
    ];

    /// <summary>Колонки по умолчанию — как в таблице страницы (в кг).</summary>
    public static readonly IReadOnlyList<string> DefaultKeys =
        ["name", "code", "cat", "pack", "unitKg", "stockKg", "perDay", "days", "need15", "need30", "price", "value", "orderKg", "status", "factoryKg"];

    /// <summary>Ключи из адреса «name,code,stockKg» — в порядке каталога, неизвестные отброшены; пусто — колонки по умолчанию.</summary>
    public static IReadOnlyList<StockExportColumn> Parse(string? keys)
    {
        var wanted = (keys ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0)
        {
            wanted = DefaultKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var picked = Columns.Where(c => wanted.Contains(c.Key)).ToList();
        return picked.Count > 0 ? picked : Columns.Where(c => DefaultKeys.Contains(c.Key)).ToList();
    }

    public static string StatusLabel(string status) => (status, UiLanguage.IsUzbek) switch
    {
        (StockStatuses.Deficit, false) => "дефицит",
        (StockStatuses.Overstock, false) => "затоварка",
        (StockStatuses.Dead, false) => "не продаётся",
        (StockStatuses.Ok, false) => "норма",
        (StockStatuses.Empty, false) => "нет остатка",
        (StockStatuses.Unknown, false) => "нет веса",
        (StockStatuses.Deficit, true) => "taqchil",
        (StockStatuses.Overstock, true) => "ortiqcha",
        (StockStatuses.Dead, true) => "sotilmaydi",
        (StockStatuses.Ok, true) => "me'yor",
        (StockStatuses.Empty, true) => "qoldiq yo'q",
        (StockStatuses.Unknown, true) => "vazn yo'q",
        _ => "",
    };

    /// <summary>Книга: «Сводка» в выбранных колонках, по желанию — «По складам» (только у охватов складов дилеров).</summary>
    public static AiReportContent Build(StockView view, IReadOnlyList<StockExportColumn> columns, bool warehouses)
    {
        var uz = UiLanguage.IsUzbek;
        // Дни без товара есть только у склада — в «Сводке» колонка была бы пустой.
        var summary = columns.Where(c => c.Key != "zeroDays").ToList();
        var sheets = new List<AiReportSheet>
        {
            new(uz ? "Jamlama" : "Сводка",
                summary.Select(c => new AiReportColumn(c.Header, c.Format, c.Sum)).ToList(),
                view.Items.Select(i => (IReadOnlyList<JsonElement>)summary.Select(c => Json(c.Item(i))).ToList()).ToList(),
                Total: true,
                Note: Note(view)),
        };

        var dealers = view.Scope is not (StockScopes.Plant or StockScopes.Export);
        if (warehouses && dealers)
        {
            // Товар — колонки без разреза (наименование, код, категория, вес…), склад, затем выбранные величины склада. Колонки завода
            // здесь не нужны: у завода своя строка.
            var identity = columns.Where(c => c.Cell is null && c.Key is not ("factoryKg" or "factoryOrder")).ToList();
            var metrics = columns.Where(c => c.Cell is not null).ToList();
            var header = identity.Select(c => new AiReportColumn(c.Header, c.Format, c.Sum))
                .Append(new AiReportColumn(uz ? "Ombor" : "Склад", ReportFormats.Text))
                .Concat(metrics.Select(c => new AiReportColumn(c.Header, c.Format, c.Sum)))
                .ToList();
            var rows = new List<IReadOnlyList<JsonElement>>();
            foreach (var item in view.Items)
            {
                var cells = new List<(string Name, StockCell Cell)>();
                if (view.Factory is { } factory && item.Factory is { } fc)
                {
                    cells.Add((factory.Name, fc));
                }

                cells.AddRange(view.ScopeRegions
                    .Select(r => (r.Name, Cell: item.Regions.GetValueOrDefault(r.Id)))
                    .Where(x => x.Cell is { } c && ((c.Kg ?? 0) > 0 || (c.KgPerDay ?? 0) > 0))
                    .OrderByDescending(x => x.Cell!.Kg ?? 0)
                    .Select(x => (x.Name, x.Cell!)));
                rows.AddRange(cells.Select(x => (IReadOnlyList<JsonElement>)identity.Select(c => Json(c.Item(item)))
                    .Append(Json(x.Name))
                    .Concat(metrics.Select(c => Json(c.Cell!(item, x.Cell))))
                    .ToList()));
            }

            sheets.Add(new AiReportSheet(uz ? "Omborlar bo'yicha" : "По складам", header, rows, Total: false,
                Note: uz ? "Har bir tovar — zavod va qamrovdagi diler omborlari bo'yicha (qoldig'i yoki savdosi bor joylar). Zavod qatorida: tezlik — butun mamlakat savdosi, buyurtma — barcha dilerlarga jo'natish uchun yetishmaydigan miqdor."
                         : "Каждый товар — по заводу и складам дилеров охвата, где есть остаток или продажи. В строке завода скорость — продажи всей страны, заказ — сколько не хватает, чтобы отгрузить всем дилерам."));
        }

        var scopeName = uz ? view.ScopeName switch { "Вся страна" => "Butun mamlakat", "Завод" => "Zavod", "Экспорт" => "Eksport", var n => n } : view.ScopeName;
        var title = uz ? $"Tavsiya etilgan qoldiq — {scopeName}" : $"Рекомендуемый остаток — {view.ScopeName}";
        var sources = new List<string>
        {
            uz ? $"Linko qoldiqlari {view.SnapshotDate:dd.MM.yyyy}" : $"остатки Linko на {view.SnapshotDate:dd.MM.yyyy}",
            uz ? $"savdo tezligi — ikkilamchi {view.VelocityFrom:dd.MM}–{view.VelocityTo:dd.MM.yyyy} ({view.VelocityDays} kun), autstokka tuzatilgan"
               : $"скорость — вторичка {view.VelocityFrom:dd.MM}–{view.VelocityTo:dd.MM.yyyy} ({view.VelocityDays} дн.) с поправкой на аутсток",
        };
        if (view.PriceList is { Length: > 0 } priceList)
        {
            sources.Add(uz ? $"narx — «{priceList}» {view.PriceAsOf:dd.MM.yyyy}" : $"цена — прайс «{priceList}» на {view.PriceAsOf:dd.MM.yyyy}");
        }

        return new AiReportContent(title, sheets, sources);
    }

    /// <summary>Какие фильтры страницы попали в файл — чтобы отчёт не путали с полным.</summary>
    private static string? Note(StockView view)
    {
        var uz = UiLanguage.IsUzbek;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(view.Query))
        {
            parts.Add(uz ? $"qidiruv «{view.Query}»" : $"поиск «{view.Query}»");
        }

        if (view.SelectedCategories.Count > 0)
        {
            parts.Add((uz ? "kategoriyalar: " : "категории: ") + string.Join(", ", view.SelectedCategories));
        }

        if (view.SelectedPacks.Count > 0)
        {
            parts.Add((uz ? "qadoq: " : "фасовка: ") + string.Join(", ", view.SelectedPacks.Select(p => F(p) + (uz ? " kg" : " кг"))));
        }

        if (view.Top is "only" or "not")
        {
            parts.Add(view.Top == "only" ? (uz ? "faqat TOP" : "только ТОП") : (uz ? "TOPdan tashqari" : "кроме ТОПа"));
        }

        if (view.Status != "all")
        {
            parts.Add((uz ? "holat: " : "статус: ") + StatusLabel(view.Status));
        }

        return parts.Count == 0 ? null : (uz ? "Filtrlar: " : "Фильтры: ") + string.Join("; ", parts) + ".";
    }

    private static JsonElement Json(object? value) => JsonSerializer.SerializeToElement(value);
}
