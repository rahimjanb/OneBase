using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Sales;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Sales;

/// <summary>
/// Импорт планов. Первая строка — заголовки (порядок колонок не важен):
/// регион: «Регион | Категория | Год | Месяц | План, кг»;
/// ТП: «ID агента | Агент | Категория | Год | Месяц | План, кг».
/// Регион и категорию можно указать названием или id; пустая категория — план без разбивки.
/// Месяц — числом (9) или периодом (2026-09, 09.2026) — тогда колонка «Год» не нужна.
/// </summary>
internal sealed class SalesPlanImporter(OneBaseDbContext db) : ISalesPlanImporter
{
    private const int MaxErrors = 50;

    public async Task<PlanImportResult> ImportAsync(
        Stream file,
        string fileName,
        PlanImportTarget target,
        PlanKind kind,
        CancellationToken ct = default)
    {
        var rows = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            ? ReadCsv(file)
            : ReadXlsx(file);

        if (rows.Count < 2)
        {
            return new PlanImportResult(0, ["Файл пустой или без строк после заголовка."]);
        }

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToArray();
        var col = new Columns(header, target);
        if (col.Missing() is { } missing)
        {
            return new PlanImportResult(0, [missing]);
        }

        var categories = await db.LinkoProductTypes.AsNoTracking().ToListAsync(ct);
        var regions = await db.SalesRegions.AsNoTracking().ToListAsync(ct);
        var agents = await db.LinkoUsers.AsNoTracking().ToListAsync(ct);

        var errors = new List<string>();
        var imported = 0;

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var line = i + 1;
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            try
            {
                var (year, month) = ParsePeriod(Cell(row, col.Year), Cell(row, col.Month));
                var plan = ParseDecimal(Cell(row, col.Plan)) ?? throw new FormatException("не указан план");
                var category = ResolveCategory(Cell(row, col.Category), categories);

                if (target == PlanImportTarget.Region)
                {
                    var region = ResolveRegion(Cell(row, col.Region), regions);
                    await UpsertRegionPlanAsync(region.Id, kind, year, month, category, plan, ct);
                }
                else
                {
                    var agentId = ResolveAgent(Cell(row, col.AgentId), Cell(row, col.AgentName), agents);
                    await UpsertAgentPlanAsync(agentId, kind, year, month, category, plan, ct);
                }

                imported++;
            }
            catch (FormatException ex)
            {
                if (errors.Count < MaxErrors)
                {
                    errors.Add($"Строка {line}: {ex.Message}");
                }
            }
        }

        await db.SaveChangesAsync(ct);
        return new PlanImportResult(imported, errors);
    }

    public async Task<byte[]> BuildTemplateAsync(PlanImportTarget target, CancellationToken ct = default)
    {
        var today = DateTime.Today;
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("План");

        string[] headers = target == PlanImportTarget.Region
            ? ["Регион", "Категория", "Год", "Месяц", "План, кг"]
            : ["ID агента", "Агент", "Категория", "Год", "Месяц", "План, кг"];
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        sheet.Row(1).Style.Font.Bold = true;

        // Справочники, чтобы было из чего выбирать.
        var reference = book.AddWorksheet("Справочник");
        var r = 1;
        if (target == PlanImportTarget.Region)
        {
            reference.Cell(r++, 1).Value = "Регионы";
            foreach (var region in await db.SalesRegions.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct))
            {
                reference.Cell(r, 1).Value = region.LinkoBranchId;
                reference.Cell(r++, 2).Value = region.Name;
            }
        }
        else
        {
            reference.Cell(r++, 1).Value = "Агенты";
            foreach (var agent in await db.LinkoUsers.AsNoTracking().Where(u => u.IsActive).OrderBy(x => x.FirstName).ToListAsync(ct))
            {
                reference.Cell(r, 1).Value = agent.Id;
                reference.Cell(r++, 2).Value = agent.DisplayName;
            }
        }

        r++;
        reference.Cell(r++, 1).Value = "Категории";
        foreach (var type in await db.LinkoProductTypes.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct))
        {
            reference.Cell(r, 1).Value = type.Id;
            reference.Cell(r++, 2).Value = type.Name;
        }

        // Строка-пример.
        var example = target == PlanImportTarget.Region
            ? new object[] { "Название региона", "", today.Year, today.Month, 10000 }
            : new object[] { 0, "Имя агента", "", today.Year, today.Month, 2000 };
        for (var c = 0; c < example.Length; c++)
        {
            sheet.Cell(2, c + 1).Value = XLCellValue.FromObject(example[c]);
        }

        sheet.Columns().AdjustToContents();
        reference.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task UpsertRegionPlanAsync(Guid regionId, PlanKind kind, int year, int month, long? category, decimal plan, CancellationToken ct)
    {
        var entity = db.SalesRegionPlans.Local.FirstOrDefault(p => Same(p, regionId, kind, year, month, category))
            ?? await db.SalesRegionPlans.FirstOrDefaultAsync(p => p.RegionId == regionId && p.Kind == kind && p.Year == year && p.Month == month && p.CategoryId == category, ct);
        if (entity is null)
        {
            entity = new SalesRegionPlan { RegionId = regionId, Kind = kind, Year = year, Month = month, CategoryId = category };
            db.SalesRegionPlans.Add(entity);
        }

        entity.PlanKg = plan;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task UpsertAgentPlanAsync(long agentId, PlanKind kind, int year, int month, long? category, decimal plan, CancellationToken ct)
    {
        var entity = db.SalesAgentPlans.Local.FirstOrDefault(p => p.LinkoUserId == agentId && p.Kind == kind && p.Year == year && p.Month == month && p.CategoryId == category)
            ?? await db.SalesAgentPlans.FirstOrDefaultAsync(p => p.LinkoUserId == agentId && p.Kind == kind && p.Year == year && p.Month == month && p.CategoryId == category, ct);
        if (entity is null)
        {
            entity = new SalesAgentPlan { LinkoUserId = agentId, Kind = kind, Year = year, Month = month, CategoryId = category };
            db.SalesAgentPlans.Add(entity);
        }

        entity.PlanKg = plan;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static bool Same(SalesRegionPlan p, Guid regionId, PlanKind kind, int year, int month, long? category) =>
        p.RegionId == regionId && p.Kind == kind && p.Year == year && p.Month == month && p.CategoryId == category;

    // ---------- Разбор значений ----------

    private static SalesRegion ResolveRegion(string value, List<SalesRegion> regions)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("не указан регион");
        }

        return (long.TryParse(value, out var id) ? regions.FirstOrDefault(r => r.LinkoBranchId == id) : null)
            ?? regions.FirstOrDefault(r => string.Equals(r.Name.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new FormatException($"регион «{value}» не найден");
    }

    private static long ResolveAgent(string idValue, string nameValue, List<Domain.Sales.LinkoUser> agents)
    {
        if (long.TryParse(idValue, out var id) && id > 0)
        {
            return agents.Any(a => a.Id == id) ? id : throw new FormatException($"агент с ID {id} не найден");
        }

        var matches = agents.Where(a => string.Equals(a.DisplayName.Trim(), nameValue.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return matches.Count switch
        {
            1 => matches[0].Id,
            0 => throw new FormatException($"агент «{nameValue}» не найден — укажите ID"),
            _ => throw new FormatException($"агентов с именем «{nameValue}» несколько — укажите ID"),
        };
    }

    private static long? ResolveCategory(string value, List<Domain.Sales.LinkoProductType> categories)
    {
        var v = value.Trim();
        if (v.Length == 0 || v.Equals("все", StringComparison.OrdinalIgnoreCase) || v.Equals("итого", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (long.TryParse(v, out var id) && categories.Any(c => c.Id == id))
        {
            return id;
        }

        return categories.FirstOrDefault(c => string.Equals(c.Name.Trim(), v, StringComparison.OrdinalIgnoreCase))?.Id
            ?? throw new FormatException($"категория «{value}» не найдена");
    }

    internal static (int Year, int Month) ParsePeriod(string year, string month)
    {
        month = month.Trim();
        foreach (var format in new[] { "yyyy-MM", "yyyy.MM", "MM.yyyy", "MM/yyyy", "yyyy-MM-dd", "dd.MM.yyyy" })
        {
            if (DateTime.TryParseExact(month, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return (date.Year, date.Month);
            }
        }

        // Excel может отдать дату как DateTime в текстовом виде.
        if (DateTime.TryParse(month, CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.None, out var parsed) && month.Length > 2)
        {
            return (parsed.Year, parsed.Month);
        }

        if (!int.TryParse(month, out var m) || m is < 1 or > 12)
        {
            throw new FormatException($"неверный месяц «{month}»");
        }

        if (!int.TryParse(year.Trim(), out var y) || y is < 2000 or > 2100)
        {
            throw new FormatException($"неверный год «{year}»");
        }

        return (y, m);
    }

    internal static decimal? ParseDecimal(string value)
    {
        var v = value.Replace(" ", "").Replace(" ", "").Replace(',', '.').Trim();
        return decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static string Cell(string[] row, int index) => index >= 0 && index < row.Length ? row[index] ?? "" : "";

    // ---------- Чтение файлов ----------

    private static List<string[]> ReadXlsx(Stream file)
    {
        using var book = new XLWorkbook(file);
        var sheet = book.Worksheets.First();
        var range = sheet.RangeUsed();
        if (range is null)
        {
            return [];
        }

        return range.Rows()
            .Select(r => r.Cells().Select(c => c.Value.IsDateTime
                ? c.Value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : c.Value.IsNumber
                    ? c.Value.GetNumber().ToString(CultureInfo.InvariantCulture)
                    : c.GetString()).ToArray())
            .ToList();
    }

    private static List<string[]> ReadCsv(Stream file)
    {
        using var reader = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        if (lines.Count == 0)
        {
            return [];
        }

        var delimiter = new[] { ';', '\t', ',' }.MaxBy(d => lines[0].Count(ch => ch == d));
        return lines.Select(l => l.Split(delimiter).Select(v => v.Trim().Trim('"')).ToArray()).ToList();
    }

    private sealed class Columns
    {
        public Columns(string[] header, PlanImportTarget target)
        {
            Target = target;
            Region = Find(header, "регион", "branch");
            AgentId = Find(header, "id агента", "id");
            AgentName = Find(header, "агент", "тп");
            Category = Find(header, "категор");
            Year = Find(header, "год");
            Month = Find(header, "месяц", "период");
            Plan = Find(header, "план");
        }

        private PlanImportTarget Target { get; }
        public int Region { get; }
        public int AgentId { get; }
        public int AgentName { get; }
        public int Category { get; }
        public int Year { get; }
        public int Month { get; }
        public int Plan { get; }

        public string? Missing()
        {
            if (Plan < 0) return "Нет колонки «План, кг».";
            if (Month < 0) return "Нет колонки «Месяц».";
            if (Target == PlanImportTarget.Region && Region < 0) return "Нет колонки «Регион».";
            if (Target == PlanImportTarget.Agent && AgentId < 0 && AgentName < 0) return "Нет колонки «ID агента» или «Агент».";
            return null;
        }

        private static int Find(string[] header, params string[] names)
        {
            foreach (var name in names)
            {
                var exact = Array.FindIndex(header, h => h == name);
                if (exact >= 0) return exact;
            }

            foreach (var name in names)
            {
                var partial = Array.FindIndex(header, h => h.StartsWith(name, StringComparison.Ordinal));
                if (partial >= 0) return partial;
            }

            return -1;
        }
    }
}
