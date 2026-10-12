using System.Text.RegularExpressions;
using OneBase.Domain.AI;
using OneBase.Domain.Sales;

namespace OneBase.Application.DataManager;

/// <summary>Регион продаж для сопоставления строк отчёта (без «Завода», «К К Мерч» и старых филиалов).</summary>
public sealed record DataRegionRef(Guid Id, string Name, string? Dealer, long BranchId, string? Supervisor = null);

/// <summary>Категория отчёта: тип товара Linko, под которым хранится план, и слова, по которым её узнать.</summary>
public sealed record DataCategoryRef(long TypeId, string Name, IReadOnlyList<string> Aliases);

public sealed record ExistingPlanRow(Guid RegionId, int Year, int Month, long? CategoryId, decimal PlanKg);

/// <summary>Значение плана из отчёта или сообщения — до сопоставления с регионами и категориями.</summary>
public sealed record PlanValue(string Region, string? Category, int Year, int Month, decimal? Value, string SourceRef, string? InvalidRaw = null);

/// <summary>Строка будущего плана изменений.</summary>
public sealed record PlannedItem(
    string EntityType,
    string EntityKey,
    string Label,
    DataChangeAction Action,
    string? Before,
    string? After,
    string? SourceRef,
    string? Message);

/// <summary>План региона на месяц целиком — до и после (по правилу расчёта: строка без категории — итог, иначе сумма категорий).</summary>
public sealed record RegionPlanTotal(Guid RegionId, string Region, int Year, int Month, decimal? Before, decimal? After);

public sealed record PlanningResult(
    IReadOnlyList<PlannedItem> Items,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<RegionPlanTotal> Totals,
    int SkippedEmpty);

public static class DataEntityTypes
{
    public const string RegionPlan = "region_plan";
    public const string SalesTarget = "sales_target";
    public const string RegionProfile = "region_profile";
    public const string PnlValue = "pnl_value";

    public static string PlanKindLabel(PlanKind kind) => kind switch
    {
        PlanKind.Rop => "план РОП",
        PlanKind.Factory => "план «Завод»",
        _ => "план первички",
    };

    public static string MonthLabel(int year, int month) =>
        month is >= 1 and <= 12 ? $"{DataValues.MonthNames[month - 1]} {year}" : $"{month:D2}.{year}";
}

/// <summary>Сопоставление названий из отчёта с регионами и категориями OneBase. Неоднозначное — не угадывается.</summary>
public sealed partial class DataMatcher
{
    private readonly IReadOnlyList<DataRegionRef> _regions;
    private readonly IReadOnlyList<DataCategoryRef> _categories;

    public DataMatcher(IReadOnlyList<DataRegionRef> regions, IReadOnlyList<DataCategoryRef> categories)
    {
        _regions = regions;
        _categories = categories;
    }

    /// <summary>Слова, означающие «план без разбивки по категориям».</summary>
    private static readonly HashSet<string> TotalWords =
        ["", "итого", "итог", "всего", "total", "план", "план кг", "план, кг", "итого кг", "без разбивки", "все", "жами"];

    /// <summary>Регион по названию, дилеру или id филиала Linko. Ошибка — текст для администратора.</summary>
    public (DataRegionRef? Region, string? Error) Region(string text)
    {
        var name = OldBranch().Replace(DataValues.Normalize(text), "").Trim();
        if (name.Length == 0)
        {
            return (null, "регион не указан");
        }

        if (long.TryParse(name, out var branchId) && _regions.FirstOrDefault(r => r.BranchId == branchId) is { } byId)
        {
            return (byId, null);
        }

        var exact = _regions.Where(r => DataValues.Normalize(r.Name) == name || (r.Dealer is { } d && DataValues.Normalize(d) == name)).ToList();
        if (exact.Count == 1)
        {
            return (exact[0], null);
        }

        if (exact.Count > 1)
        {
            return (null, $"«{text}» подходит к нескольким регионам: {string.Join(", ", exact.Select(r => r.Name))}");
        }

        // «Азизбек Хоразм» — дилер, в названии которого есть регион; берётся, только если регион один.
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var partial = _regions.Where(r => words.Contains(DataValues.Normalize(r.Name))).ToList();
        if (partial.Count == 1)
        {
            return (partial[0], null);
        }

        return partial.Count > 1
            ? (null, $"«{text}» подходит к нескольким регионам: {string.Join(", ", partial.Select(r => r.Name))}")
            : (null, $"регион «{text}» не найден в OneBase");
    }

    /// <summary>Категория: null — итог без разбивки. Ошибка — текст для администратора.</summary>
    public (DataCategoryRef? Category, bool Total, string? Error) Category(string? text)
    {
        var name = DataValues.Normalize(text);
        if (TotalWords.Contains(name))
        {
            return (null, true, null);
        }

        var exact = _categories.Where(c => DataValues.Normalize(c.Name) == name || c.Aliases.Any(a => DataValues.Normalize(a) == name)).ToList();
        if (exact.Count == 1)
        {
            return (exact[0], false, null);
        }

        var partial = _categories
            .Where(c => name.Contains(DataValues.Normalize(c.Name), StringComparison.Ordinal)
                        || c.Aliases.Any(a => a.Length >= 4 && name.Contains(DataValues.Normalize(a), StringComparison.Ordinal)))
            .ToList();
        if (partial.Count == 1)
        {
            return (partial[0], false, null);
        }

        return partial.Count > 1
            ? (null, false, $"«{text}» подходит к нескольким категориям: {string.Join(", ", partial.Select(c => c.Name))}")
            : (null, false, $"категория «{text}» не найдена (категории отчёта: {string.Join(", ", _categories.Select(c => c.Name))})");
    }

    [GeneratedRegex(@"\s*\(?эски\)?\s*\d*$")]
    private static partial Regex OldBranch();
}

/// <summary>
/// План изменений «планов регионов» (sales.RegionPlans) — чистая функция: значения отчёта против того, что сейчас в базе.
/// Ничего не пишет; применяет план только сервис применения после подтверждения администратора.
/// </summary>
public static class RegionPlanPlanner
{
    private const decimal Tolerance = 0.0005m;

    public static string Key(PlanKind kind, Guid regionId, int year, int month, long? categoryId) =>
        $"{DataEntityTypes.RegionPlan}:{kind}:{regionId:N}:{year:D4}-{month:D2}:{(categoryId?.ToString() ?? "total")}";

    public static (PlanKind Kind, Guid RegionId, int Year, int Month, long? CategoryId) ParseKey(string key)
    {
        var p = key.Split(':');
        if (p.Length != 5 || p[0] != DataEntityTypes.RegionPlan)
        {
            throw new FormatException($"Неверный ключ плана: {key}");
        }

        var period = p[3].Split('-');
        return (Enum.Parse<PlanKind>(p[1]), Guid.ParseExact(p[2], "N"), int.Parse(period[0]), int.Parse(period[1]),
            p[4] == "total" ? null : long.Parse(p[4]));
    }

    /// <param name="replace">Отчёт полный за свои месяцы: строки базы этих месяцев, которых нет в отчёте, удаляются.</param>
    /// <param name="regions">Регионы вторички (для сопоставления и для предупреждения «регион останется без плана»).</param>
    /// <param name="existing">Текущие строки плана этого вида за месяцы отчёта.</param>
    public static PlanningResult Plan(
        PlanKind kind,
        bool replace,
        IReadOnlyList<PlanValue> values,
        IReadOnlyList<DataRegionRef> regions,
        IReadOnlyList<DataCategoryRef> categories,
        IReadOnlyList<ExistingPlanRow> existing)
    {
        var matcher = new DataMatcher(regions, categories);
        var items = new List<PlannedItem>();
        var warnings = new List<string>();
        var skippedEmpty = 0;
        var resolved = new List<(string Key, DataRegionRef Region, DataCategoryRef? Category, int Year, int Month, decimal Value, string Source)>();

        for (var i = 0; i < values.Count; i++)
        {
            var v = values[i];
            if (v.Value is null && v.InvalidRaw is null)
            {
                skippedEmpty++; // пустая ячейка — значения нет, строка не меняется
                continue;
            }

            var label = $"{v.Region} · {(string.IsNullOrWhiteSpace(v.Category) ? "итог" : v.Category)} · {DataEntityTypes.MonthLabel(v.Year, v.Month)}";
            string? error = null;
            if (v.InvalidRaw is not null)
            {
                error = $"не число: «{v.InvalidRaw}»";
            }
            else if (v.Month is < 1 or > 12 || v.Year is < 2020 or > 2100)
            {
                error = $"неверный период: {v.Month:D2}.{v.Year}";
            }
            else if (v.Value is < 0)
            {
                error = "план не может быть меньше нуля";
            }

            var (region, regionError) = matcher.Region(v.Region);
            var (category, _, categoryError) = matcher.Category(v.Category);
            error ??= regionError ?? categoryError;
            if (error is not null)
            {
                items.Add(new PlannedItem(DataEntityTypes.RegionPlan, $"conflict:{i}", label, DataChangeAction.Conflict, null, null, v.SourceRef, error));
                continue;
            }

            if (v.Value is null)
            {
                skippedEmpty++;
                continue;
            }

            resolved.Add((Key(kind, region!.Id, v.Year, v.Month, category?.TypeId), region, category, v.Year, v.Month, v.Value.Value, v.SourceRef));
        }

        var current = existing.ToDictionary(e => Key(kind, e.RegionId, e.Year, e.Month, e.CategoryId), e => e.PlanKg);
        var regionNames = regions.ToDictionary(r => r.Id, r => r.Name);
        var categoryNames = categories.ToDictionary(c => c.TypeId, c => c.Name);
        string Label(Guid regionId, long? categoryId, int year, int month) =>
            $"{regionNames.GetValueOrDefault(regionId, regionId.ToString("N")[..8])} · "
            + $"{(categoryId is { } c ? categoryNames.GetValueOrDefault(c, $"тип {c}") : "итог без разбивки")} · "
            + $"{DataEntityTypes.MonthLabel(year, month)} · {DataEntityTypes.PlanKindLabel(kind)}";

        var projected = new Dictionary<string, decimal>(current);
        var touched = new HashSet<string>();
        foreach (var group in resolved.GroupBy(r => r.Key))
        {
            var first = group.First();
            var sources = string.Join("; ", group.Select(g => g.Source).Distinct());
            var label = Label(first.Region.Id, first.Category?.TypeId, first.Year, first.Month);
            touched.Add(group.Key);
            if (group.Select(g => g.Value).Distinct().Count() > 1)
            {
                items.Add(new PlannedItem(DataEntityTypes.RegionPlan, group.Key, label, DataChangeAction.Conflict,
                    current.TryGetValue(group.Key, out var was) ? DataValues.Format(was) : null, null, sources,
                    $"в отчёте несколько разных значений: {string.Join(" / ", group.Select(g => DataValues.Format(g.Value)))}"));
                continue;
            }

            var value = first.Value;
            var duplicate = group.Count() > 1 ? "строка повторяется в отчёте с тем же значением" : null;
            if (current.TryGetValue(group.Key, out var before))
            {
                if (value == 0)
                {
                    items.Add(new PlannedItem(DataEntityTypes.RegionPlan, group.Key, label, DataChangeAction.Delete,
                        DataValues.Format(before), null, sources, "в отчёте 0 — строка плана удаляется"));
                    projected.Remove(group.Key);
                }
                else if (Math.Abs(before - value) < Tolerance)
                {
                    items.Add(new PlannedItem(DataEntityTypes.RegionPlan, group.Key, label, DataChangeAction.Unchanged,
                        DataValues.Format(before), DataValues.Format(value), sources, duplicate ?? "значение уже такое"));
                }
                else
                {
                    items.Add(new PlannedItem(DataEntityTypes.RegionPlan, group.Key, label, DataChangeAction.Update,
                        DataValues.Format(before), DataValues.Format(value), sources, duplicate));
                    projected[group.Key] = value;
                }
            }
            else if (value == 0)
            {
                skippedEmpty++;
            }
            else
            {
                items.Add(new PlannedItem(DataEntityTypes.RegionPlan, group.Key, label, DataChangeAction.Create,
                    null, DataValues.Format(value), sources, duplicate));
                projected[group.Key] = value;
            }
        }

        var periods = resolved.Select(r => (r.Year, r.Month)).Distinct().OrderBy(p => p.Year).ThenBy(p => p.Month).ToList();
        if (replace)
        {
            foreach (var row in existing.Where(e => periods.Contains((e.Year, e.Month))))
            {
                var key = Key(kind, row.RegionId, row.Year, row.Month, row.CategoryId);
                if (!touched.Contains(key))
                {
                    items.Add(new PlannedItem(DataEntityTypes.RegionPlan, key, Label(row.RegionId, row.CategoryId, row.Year, row.Month),
                        DataChangeAction.Delete, DataValues.Format(row.PlanKg), null, null,
                        "строки нет в отчёте, а отчёт отмечен как полный за месяц — удаляется"));
                    projected.Remove(key);
                }
            }
        }

        // Итог региона за месяц до и после — по правилу расчёта OneBase (SalesMath.PlanTotal).
        var totals = new List<RegionPlanTotal>();
        foreach (var (year, month) in periods)
        {
            var beforeRows = Rows(current, kind, year, month);
            var afterRows = Rows(projected, kind, year, month);
            foreach (var regionId in beforeRows.Keys.Union(afterRows.Keys).Distinct())
            {
                var before = Total(beforeRows.GetValueOrDefault(regionId));
                var after = Total(afterRows.GetValueOrDefault(regionId));
                if (before != after || items.Any(it => it.EntityKey.Contains(regionId.ToString("N"), StringComparison.Ordinal)))
                {
                    totals.Add(new RegionPlanTotal(regionId, regionNames.GetValueOrDefault(regionId, regionId.ToString()), year, month, before, after));
                }

                if (afterRows.GetValueOrDefault(regionId) is { } rows && rows.ContainsKey("total") && rows.Count > 1)
                {
                    var sum = rows.Where(r => r.Key != "total").Sum(r => r.Value);
                    warnings.Add($"{regionNames.GetValueOrDefault(regionId)} · {DataEntityTypes.MonthLabel(year, month)}: есть и итог без разбивки "
                                 + $"({DataValues.Format(rows["total"])} кг), и категории (сумма {DataValues.Format(sum)} кг) — "
                                 + "в выполнении OneBase берёт итог, категории остаются только в плане по категориям.");
                }
            }

            if (kind != PlanKind.Primary && afterRows.Count > 0)
            {
                if (beforeRows.Count == 0)
                {
                    warnings.Add($"{DataEntityTypes.MonthLabel(year, month)}: сейчас планов регионов вида «{DataEntityTypes.PlanKindLabel(kind)}» нет — "
                                 + "план месяца считается из планов ТП Linko. После применения весь месяц перейдёт на планы регионов.");
                }

                var without = regions.Where(r => !afterRows.ContainsKey(r.Id)).Select(r => r.Name).ToList();
                if (without.Count > 0)
                {
                    warnings.Add($"{DataEntityTypes.MonthLabel(year, month)}: без плана останутся {without.Count} рег.: {string.Join(", ", without)} "
                                 + "(их факт войдёт в выполнение РМ и республики, план — нет).");
                }
            }
        }

        var conflicts = items.Count(i => i.Action == DataChangeAction.Conflict);
        if (conflicts > 0)
        {
            warnings.Insert(0, $"Конфликтов: {conflicts}. Эти строки не будут применены — уточните их и подготовьте план заново.");
        }

        return new PlanningResult(items, warnings, totals, skippedEmpty);
    }

    private static Dictionary<Guid, Dictionary<string, decimal>> Rows(Dictionary<string, decimal> rows, PlanKind kind, int year, int month)
    {
        var result = new Dictionary<Guid, Dictionary<string, decimal>>();
        var prefix = $"{DataEntityTypes.RegionPlan}:{kind}:";
        var period = $"{year:D4}-{month:D2}";
        foreach (var (key, value) in rows)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var p = ParseKey(key);
            if (p.Year != year || p.Month != month || !key.Contains(period, StringComparison.Ordinal))
            {
                continue;
            }

            if (!result.TryGetValue(p.RegionId, out var region))
            {
                result[p.RegionId] = region = [];
            }

            region[p.CategoryId?.ToString() ?? "total"] = value;
        }

        return result;
    }

    /// <summary>Итог плана региона: строка без категории, иначе сумма категорий (как SalesMath.PlanTotal).</summary>
    public static decimal? Total(IReadOnlyDictionary<string, decimal>? rows) =>
        rows is null || rows.Count == 0 ? null : rows.TryGetValue("total", out var total) ? total : rows.Values.Sum();

}

/// <summary>Цели KPI (sales.Targets): проверка допустимых значений и план изменений.</summary>
public static class TargetPlanner
{
    public static readonly IReadOnlyDictionary<string, (string Label, decimal Min, decimal Max)> Rules =
        new Dictionary<string, (string, decimal, decimal)>
        {
            [SalesTargetKeys.VisitConversion] = ("Цель: конверсия визита (доля 0–1)", 0.01m, 1m),
            [SalesTargetKeys.RevenuePerOutlet] = ("Цель: выручка на торговую точку, сум", 1m, 1_000_000_000m),
            [SalesTargetKeys.AkbPerAgent] = ("Цель: АКБ на агента, точек", 1m, 10_000m),
            [SalesTargetKeys.CategoriesPerOutlet] = ("Цель: категорий на торговую точку", 1m, 20m),
        };

    public static string Key(string target) => $"{DataEntityTypes.SalesTarget}:{target}";

    public static IReadOnlyList<PlannedItem> Plan(IReadOnlyDictionary<string, decimal?> values, IReadOnlyDictionary<string, decimal> current, string source)
    {
        var items = new List<PlannedItem>();
        foreach (var (target, value) in values)
        {
            if (!Rules.TryGetValue(target, out var rule))
            {
                items.Add(new PlannedItem(DataEntityTypes.SalesTarget, $"conflict:{target}", target, DataChangeAction.Conflict, null, null, source,
                    $"такой цели нет; есть: {string.Join(", ", Rules.Keys)}"));
                continue;
            }

            var before = current.TryGetValue(target, out var b) ? DataValues.Format(b) : null;
            if (value is not { } v || v < rule.Min || v > rule.Max)
            {
                items.Add(new PlannedItem(DataEntityTypes.SalesTarget, Key(target), rule.Label, DataChangeAction.Conflict, before, null, source,
                    $"значение должно быть от {DataValues.Format(rule.Min)} до {DataValues.Format(rule.Max)}"));
                continue;
            }

            var action = before is null ? DataChangeAction.Create : b == v ? DataChangeAction.Unchanged : DataChangeAction.Update;
            items.Add(new PlannedItem(DataEntityTypes.SalesTarget, Key(target), rule.Label, action, before, DataValues.Format(v), source, null));
        }

        return items;
    }
}

/// <summary>Профиль региона (дилер, СВР) — данные OneBase, которых нет в Linko.</summary>
public sealed record RegionProfileValue(string Region, string Field, string? Value, string SourceRef);

public static class RegionProfilePlanner
{
    public const string Dealer = "dealer";
    public const string Supervisor = "supervisor";
    public const int MaxLength = 200;

    public static string Key(Guid regionId, string field) => $"{DataEntityTypes.RegionProfile}:{regionId:N}:{field}";

    public static (Guid RegionId, string Field) ParseKey(string key)
    {
        var p = key.Split(':');
        return p.Length == 3 && p[0] == DataEntityTypes.RegionProfile
            ? (Guid.ParseExact(p[1], "N"), p[2])
            : throw new FormatException($"Неверный ключ профиля региона: {key}");
    }

    public static IReadOnlyList<PlannedItem> Plan(IReadOnlyList<RegionProfileValue> values, IReadOnlyList<DataRegionRef> regions)
    {
        var matcher = new DataMatcher(regions, []);
        var items = new List<PlannedItem>();
        foreach (var (v, i) in values.Select((v, i) => (v, i)))
        {
            var field = v.Field.Trim().ToLowerInvariant();
            var fieldLabel = field == Dealer ? "дилер" : field == Supervisor ? "СВР" : field;
            var (region, error) = matcher.Region(v.Region);
            var value = string.IsNullOrWhiteSpace(v.Value) ? null : string.Join(' ', v.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            error ??= field is not (Dealer or Supervisor) ? "поле — dealer или supervisor"
                : value?.Length > MaxLength ? $"не длиннее {MaxLength} символов" : null;
            if (error is not null || region is null)
            {
                items.Add(new PlannedItem(DataEntityTypes.RegionProfile, $"conflict:{i}", $"{v.Region} · {fieldLabel}", DataChangeAction.Conflict,
                    null, value, v.SourceRef, error));
                continue;
            }

            var before = field == Dealer ? region.Dealer : region.Supervisor;
            var action = before == value ? DataChangeAction.Unchanged : before is null ? DataChangeAction.Create
                : value is null ? DataChangeAction.Delete : DataChangeAction.Update;
            items.Add(new PlannedItem(DataEntityTypes.RegionProfile, Key(region.Id, field), $"{region.Name} · {fieldLabel}", action,
                before, value, v.SourceRef, null));
        }

        return items;
    }
}
