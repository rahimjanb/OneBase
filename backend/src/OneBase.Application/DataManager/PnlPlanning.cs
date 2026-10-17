using OneBase.Application.Abstractions;
using OneBase.Application.Sales.Pnl;
using OneBase.Domain.AI;

namespace OneBase.Application.DataManager;

/// <summary>Столбец значений: столбец отчёта (буква или заголовок) → строка P&L (ключ «sales», подпись или код статьи).</summary>
public sealed record PnlColumn(string Column, string Line);

/// <summary>
/// Как читать P&L из листа. Значения OneBase берёт сам по этому сопоставлению — модель чисел не передаёт.
/// <list type="bullet">
/// <item>строки — контрагенты (<see cref="DealerColumn"/>): широкая таблица — столбцы строк P&L (<see cref="ValueColumns"/>);
/// длинная (реестр расходов, база продаж, журнал логистики) — столбец статьи или строки P&L (<see cref="LineColumn"/>) и суммы
/// (<see cref="ValueColumn"/>), либо строки P&L заданы столбцами ValueColumns и складываются по строкам отчёта (<see cref="SumRows"/>);</item>
/// <item>строки — статьи P&L, столбцы — контрагенты (выгрузка «P&L таблицей»): <see cref="LineColumn"/> и диапазон столбцов
/// <see cref="FirstDealerColumn"/>…<see cref="LastDealerColumn"/>, имена контрагентов — в строке заголовка.</item>
/// </list>
/// </summary>
public sealed record PnlSheetMapping(
    string? Sheet,
    int HeaderRow,
    string? DealerColumn,
    IReadOnlyList<PnlColumn>? ValueColumns,
    string? LineColumn,
    string? ValueColumn,
    string? YearColumn,
    string? MonthColumn,
    int? Year,
    int? Month,
    decimal Multiplier = 1m,
    int? FirstRow = null,
    int? LastRow = null,
    string? FirstDealerColumn = null,
    string? LastDealerColumn = null,
    string? ReturnColumn = null,
    IReadOnlyList<string>? ReturnValues = null,
    bool SumRows = false);

/// <summary>
/// Значение из отчёта или сообщения до сопоставления. Line — текст строки P&L (ключ, подпись или статья реестра);
/// у строки возврата значение уже со знаком минус.
/// </summary>
public sealed record PnlRawValue(string Dealer, string Line, int Year, int Month, decimal? Value, string SourceRef, string? InvalidRaw = null);

public sealed record ExistingPnlRow(int Year, int Month, string Dealer, string DealerKey, string Line, decimal Amount);

/// <summary>Месяц P&L до и после применения — по формулам OneBase (итог всех дилеров).</summary>
public sealed record PnlMonthTotal(int Year, int Month, IReadOnlyDictionary<string, decimal?> Before, IReadOnlyDictionary<string, decimal?> After);

/// <summary>Расчётная строка из отчёта (валовая, маржа, EBIT…) против расчёта OneBase по исходным строкам после применения.</summary>
public sealed record PnlCheck(string Dealer, int Year, int Month, string Line, string Label, decimal Report, decimal? OneBase, decimal? Diff, string SourceRef);

public sealed record PnlSummary(IReadOnlyList<PnlMonthTotal> Months, IReadOnlyList<PnlCheck> Checks, int CheckedValues, IReadOnlyList<string> NewDealers);

public sealed record PnlPlanningResult(IReadOnlyList<PlannedItem> Items, IReadOnlyList<string> Warnings, PnlSummary Summary, int SkippedEmpty);

public sealed record PnlRequest(
    bool Replace,
    Guid? FileId,
    PnlSheetMapping? Mapping,
    IReadOnlyList<PnlRawValue>? Values,
    IReadOnlyDictionary<string, string>? LineMap,
    IReadOnlyDictionary<string, string>? DealerMap,
    string? Note,
    string? FromMonth = null,
    string? ToMonth = null);

public static class PnlExtraction
{
    /// <summary>Значения P&L из листа. Строки «Итого» и строки без контрагента пропускаются.</summary>
    public static IReadOnlyList<PnlRawValue> Values(ReportDocument document, PnlSheetMapping map)
    {
        var sheet = ReportExtraction.Sheet(document, map.Sheet);
        if (map.HeaderRow < 1 || map.HeaderRow > sheet.Rows.Count)
        {
            throw new DataManagerException($"Строка заголовка {map.HeaderRow} вне листа (строк: {sheet.Rows.Count}).");
        }

        if (map.Multiplier <= 0)
        {
            throw new DataManagerException("Множитель единиц должен быть больше нуля (сум — 1, тыс. сум — 1000).");
        }

        int? Col(string? column) => string.IsNullOrWhiteSpace(column) ? null : ReportExtraction.Column(sheet, map.HeaderRow, column);
        var header = sheet.Rows[map.HeaderRow - 1];
        var first = Math.Max(map.FirstRow ?? map.HeaderRow + 1, map.HeaderRow + 1);
        var last = Math.Min(map.LastRow ?? sheet.Rows.Count, sheet.Rows.Count);
        var values = new List<PnlRawValue>();

        // Строки — статьи P&L, столбцы — контрагенты.
        if (!string.IsNullOrWhiteSpace(map.FirstDealerColumn))
        {
            var lineCol = Col(map.LineColumn) ?? throw new DataManagerException("Для таблицы «статьи в строках» нужен столбец статей (lineColumn).");
            if (map.Year is not { } year || map.Month is not { } month)
            {
                throw new DataManagerException("Для таблицы «статьи в строках» укажите год и месяц (year, month).");
            }

            var from = Col(map.FirstDealerColumn)!.Value;
            var to = Col(map.LastDealerColumn) ?? Math.Max(sheet.TotalColumns - 1, from);
            for (var r = first; r <= last; r++)
            {
                var row = sheet.Rows[r - 1];
                var line = ReportExtraction.Cell(row, lineCol);
                if (line.Length == 0)
                {
                    continue;
                }

                for (var c = from; c <= to; c++)
                {
                    var dealer = ReportExtraction.Cell(header, c);
                    if (dealer.Length == 0 || ReportExtraction.IsSummary(dealer))
                    {
                        continue;
                    }

                    values.Add(Value(dealer, line, year, month, ReportExtraction.Cell(row, c), map.Multiplier, false,
                        $"{sheet.Name}!{DataValues.ColumnLetter(c)}{r}"));
                }
            }

            return values;
        }

        var dealerCol = Col(map.DealerColumn) ?? throw new DataManagerException("Укажите столбец контрагента (dealerColumn).");
        var wide = map.ValueColumns is { Count: > 0 };
        if (!wide && (string.IsNullOrWhiteSpace(map.LineColumn) || string.IsNullOrWhiteSpace(map.ValueColumn)))
        {
            throw new DataManagerException("Укажите столбцы строк P&L (valueColumns) или столбец статьи (lineColumn) со столбцом суммы (valueColumn).");
        }

        var columns = wide ? map.ValueColumns!.Select(c => (Index: Col(c.Column)!.Value, c.Line)).ToList() : [];
        var lineColumn = wide ? null : Col(map.LineColumn);
        var valueColumn = wide ? null : Col(map.ValueColumn);
        var yearCol = Col(map.YearColumn);
        var monthCol = Col(map.MonthColumn);
        var returnCol = Col(map.ReturnColumn);
        var returnValues = (map.ReturnValues ?? []).Select(DataValues.Normalize).Where(v => v.Length > 0).ToHashSet();
        if (returnCol is not null && returnValues.Count == 0)
        {
            throw new DataManagerException("Для столбца возврата (returnColumn) укажите значения, которые означают возврат (returnValues).");
        }

        if (monthCol is null && map.Month is null)
        {
            throw new DataManagerException("Не указан месяц: ни столбцом (monthColumn), ни значением (month).");
        }

        for (var r = first; r <= last; r++)
        {
            var row = sheet.Rows[r - 1];
            var dealer = ReportExtraction.Cell(row, dealerCol);
            if (dealer.Length == 0 || ReportExtraction.IsSummary(dealer))
            {
                continue;
            }

            var (rowYear, rowMonth) = monthCol is { } mc ? DataValues.ParseMonth(ReportExtraction.Cell(row, mc)) : (null, null);
            var year = yearCol is { } yc && int.TryParse(ReportExtraction.Cell(row, yc), out var y) ? y : rowYear ?? map.Year ?? 0;
            var month = rowMonth ?? map.Month ?? 0;
            var isReturn = returnCol is { } rc && returnValues.Contains(DataValues.Normalize(ReportExtraction.Cell(row, rc)));
            if (wide)
            {
                foreach (var (index, line) in columns)
                {
                    values.Add(Value(dealer, line, year, month, ReportExtraction.Cell(row, index), map.Multiplier, isReturn,
                        $"{sheet.Name}!{DataValues.ColumnLetter(index)}{r}"));
                }
            }
            else
            {
                values.Add(Value(dealer, ReportExtraction.Cell(row, lineColumn!.Value), year, month, ReportExtraction.Cell(row, valueColumn!.Value),
                    map.Multiplier, isReturn, $"{sheet.Name}!{DataValues.ColumnLetter(valueColumn.Value)}{r}"));
            }
        }

        return values;
    }

    private static PnlRawValue Value(string dealer, string line, int year, int month, string raw, decimal multiplier, bool isReturn, string source)
    {
        var text = raw.Trim();
        var percent = text.EndsWith('%');
        var number = DataValues.ParseNumber(percent ? text[..^1] : text, out var ok);
        if (!ok)
        {
            return new PnlRawValue(dealer, line, year, month, null, source, raw);
        }

        if (number is null)
        {
            return new PnlRawValue(dealer, line, year, month, null, source);
        }

        // проценты (маржа, ROS) — только для сверки: единицы денег к ним не применяются
        var value = percent ? number.Value / 100m
            : PnlMath.FindLine(line) is { } key && PnlMath.Line(key)!.Format == PnlFormat.Percent ? number.Value
            : number.Value * multiplier;
        return new PnlRawValue(dealer, line, year, month, isReturn ? -Math.Abs(value) : value, source + (isReturn ? " (возврат)" : ""));
    }
}

/// <summary>
/// План изменений P&L дилеров (sales.PnlValues) — чистая функция: значения отчёта против того, что сейчас в базе.
/// Исходные строки (продажи, себестоимость, расходы) пишутся; расчётные (валовая, маржа, EBIT…) не пишутся, а сверяются
/// с расчётом OneBase — расхождения показываются администратору.
/// </summary>
public static class PnlPlanner
{
    private const decimal MoneyTolerance = 1m;
    private const decimal PercentTolerance = 0.001m;
    public const int MaxDealerLength = 160;

    public static string Key(int year, int month, string line, string dealer) => $"{DataEntityTypes.PnlValue}:{year:D4}-{month:D2}:{line}:{dealer}";

    public static (int Year, int Month, string Line, string Dealer) ParseKey(string key)
    {
        var p = key.Split(':', 4);
        if (p.Length != 4 || p[0] != DataEntityTypes.PnlValue)
        {
            throw new FormatException($"Неверный ключ P&L: {key}");
        }

        var period = p[1].Split('-');
        return (int.Parse(period[0]), int.Parse(period[1]), p[2], p[3]);
    }

    /// <param name="replace">Отчёт полный за свои месяцы и строки P&L: значения этих строк, которых нет в отчёте, удаляются.</param>
    /// <param name="sumRows">Строки отчёта с одним контрагентом, месяцем и строкой P&L складываются (реестр, база продаж, журнал);
    /// иначе разные значения — конфликт.</param>
    /// <param name="existing">Текущие значения за месяцы отчёта.</param>
    /// <param name="knownDealers">Контрагенты, которые уже есть в P&L (любой месяц): имя для показа по ключу.</param>
    public static PnlPlanningResult Plan(
        bool replace,
        bool sumRows,
        IReadOnlyList<PnlRawValue> values,
        IReadOnlyDictionary<string, string>? lineMap,
        IReadOnlyDictionary<string, string>? dealerMap,
        IReadOnlyList<ExistingPnlRow> existing,
        IReadOnlyDictionary<string, string> knownDealers)
    {
        var items = new List<PlannedItem>();
        var warnings = new List<string>();
        var skippedEmpty = 0;
        var lines = (lineMap ?? new Dictionary<string, string>())
            .ToDictionary(p => DataValues.Normalize(p.Key), p => p.Value.Trim().ToLowerInvariant());
        var dealers = (dealerMap ?? new Dictionary<string, string>())
            .ToDictionary(p => PnlMath.DealerKey(p.Key), p => PnlMath.DealerName(p.Value));
        var names = new Dictionary<string, string>(knownDealers);
        var resolved = new List<(string DealerKey, string Line, int Year, int Month, decimal Value, string Source)>();
        var checks = new List<(string DealerKey, string Line, int Year, int Month, decimal Value, string Source)>();

        if (lines.Values.FirstOrDefault(l => PnlMath.Line(l) is null) is { } unknown)
        {
            throw new DataManagerException($"lineMap: «{unknown}» — нет такой строки P&L. Исходные строки: {string.Join(", ", PnlMath.Inputs)}.");
        }

        string? MapLine(string text) =>
            lines.GetValueOrDefault(DataValues.Normalize(text))
            ?? (PnlMath.ArticleOf(text) is { } code ? lines.GetValueOrDefault(code) : null)
            ?? PnlMath.FindLine(text);

        for (var i = 0; i < values.Count; i++)
        {
            var v = values[i];
            if (v.Value is null && v.InvalidRaw is null)
            {
                skippedEmpty++;
                continue;
            }

            var reportDealer = PnlMath.DealerName(v.Dealer);
            var dealerName = dealers.GetValueOrDefault(PnlMath.DealerKey(reportDealer)) ?? reportDealer;
            var dealerKey = PnlMath.DealerKey(dealerName);
            var line = MapLine(v.Line);
            var label = $"{reportDealer} · {(line is not null ? PnlMath.Line(line)!.Label : v.Line)} · {DataEntityTypes.MonthLabel(v.Year, v.Month)}";
            var error = v.InvalidRaw is not null ? $"не число: «{v.InvalidRaw}»"
                : v.Month is < 1 or > 12 || v.Year is < 2020 or > 2100 ? $"неверный период: {v.Month:D2}.{v.Year}"
                : dealerKey.Length == 0 ? "контрагент не указан"
                : dealerName.Length > MaxDealerLength ? $"имя контрагента длиннее {MaxDealerLength} символов"
                : line is null ? $"строка P&L «{v.Line}» не распознана — укажите её в lineMap"
                : null;
            if (error is not null)
            {
                items.Add(new PlannedItem(DataEntityTypes.PnlValue, $"conflict:{i}", label, DataChangeAction.Conflict, null, null, v.SourceRef, error));
                continue;
            }

            if (PnlMath.Line(line!)!.Format == PnlFormat.NotAvailable)
            {
                continue; // ROA — данных об активах нет, строка отчёта только для вида
            }

            names.TryAdd(dealerKey, dealerName);
            if (PnlMath.IsInput(line!))
            {
                resolved.Add((dealerKey, line!, v.Year, v.Month, v.Value!.Value, v.SourceRef));
            }
            else
            {
                // процент в отчёте может быть записан числом процентов (5,7) — расчёт OneBase хранит долю (0,057)
                var value = v.Value!.Value;
                checks.Add((dealerKey, line!, v.Year, v.Month,
                    PnlMath.Line(line!)!.Format == PnlFormat.Percent && Math.Abs(value) > 1.5m ? value / 100m : value, v.SourceRef));
            }
        }

        var current = existing.ToDictionary(e => Key(e.Year, e.Month, e.Line, e.DealerKey), e => e.Amount);
        string Label(string dealerKey, string line, int year, int month) =>
            $"{names.GetValueOrDefault(dealerKey, dealerKey)} · {PnlMath.Line(line)?.Label ?? line} · {DataEntityTypes.MonthLabel(year, month)}";
        string ItemKey(string dealerKey, string line, int year, int month) => Key(year, month, line, names.GetValueOrDefault(dealerKey, dealerKey));

        var projected = new Dictionary<string, decimal>(current);
        var touched = new HashSet<string>();
        foreach (var group in resolved.GroupBy(r => Key(r.Year, r.Month, r.Line, r.DealerKey)))
        {
            var first = group.First();
            var count = group.Count();
            var sources = string.Join("; ", group.Select(g => g.Source).Distinct().Take(8)) + (count > 8 ? $" … всего строк: {count}" : "");
            var label = Label(first.DealerKey, first.Line, first.Year, first.Month);
            var key = ItemKey(first.DealerKey, first.Line, first.Year, first.Month);
            touched.Add(group.Key);
            decimal value;
            string? note = null;
            if (count > 1 && sumRows)
            {
                value = group.Sum(g => g.Value);
                note = $"сумма {count} строк отчёта";
            }
            else if (count > 1 && group.Select(g => g.Value).Distinct().Count() > 1)
            {
                items.Add(new PlannedItem(DataEntityTypes.PnlValue, key, label, DataChangeAction.Conflict,
                    current.TryGetValue(group.Key, out var was) ? DataValues.Format(was) : null, null, sources,
                    $"в отчёте несколько разных значений: {string.Join(" / ", group.Select(g => DataValues.Format(g.Value)).Take(6))} — "
                    + "если их нужно сложить (реестр, база продаж), подготовьте план с sumRows = true"));
                continue;
            }
            else
            {
                value = first.Value;
                note = count > 1 ? "строка повторяется в отчёте с тем же значением" : null;
            }

            value = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            if (current.TryGetValue(group.Key, out var before))
            {
                if (value == 0)
                {
                    items.Add(new PlannedItem(DataEntityTypes.PnlValue, key, label, DataChangeAction.Delete, DataValues.Format(before), null, sources,
                        "в отчёте 0 — значение удаляется"));
                    projected.Remove(group.Key);
                }
                else if (Math.Abs(before - value) < 0.005m)
                {
                    items.Add(new PlannedItem(DataEntityTypes.PnlValue, key, label, DataChangeAction.Unchanged, DataValues.Format(before),
                        DataValues.Format(value), sources, note ?? "значение уже такое"));
                }
                else
                {
                    items.Add(new PlannedItem(DataEntityTypes.PnlValue, key, label, DataChangeAction.Update, DataValues.Format(before),
                        DataValues.Format(value), sources, note));
                    projected[group.Key] = value;
                }
            }
            else if (value == 0)
            {
                skippedEmpty++;
            }
            else
            {
                items.Add(new PlannedItem(DataEntityTypes.PnlValue, key, label, DataChangeAction.Create, null, DataValues.Format(value), sources, note));
                projected[group.Key] = value;
            }
        }

        var periods = resolved.Select(r => (r.Year, r.Month)).Concat(checks.Select(c => (c.Year, c.Month)))
            .Distinct().OrderBy(p => p.Year).ThenBy(p => p.Month).ToList();
        if (replace)
        {
            // Полный отчёт: удаляются значения тех же месяцев и тех же строк P&L, которых в отчёте нет.
            var scope = resolved.Select(r => (r.Year, r.Month, r.Line)).ToHashSet();
            var removed = 0;
            foreach (var row in existing.Where(e => scope.Contains((e.Year, e.Month, e.Line))))
            {
                var key = Key(row.Year, row.Month, row.Line, row.DealerKey);
                if (!touched.Contains(key))
                {
                    items.Add(new PlannedItem(DataEntityTypes.PnlValue, Key(row.Year, row.Month, row.Line, row.Dealer),
                        Label(row.DealerKey, row.Line, row.Year, row.Month), DataChangeAction.Delete, DataValues.Format(row.Amount), null, null,
                        "значения нет в отчёте, а отчёт отмечен как полный за месяц — удаляется"));
                    projected.Remove(key);
                    removed++;
                }
            }

            if (scope.Count > 0)
            {
                warnings.Add("Полная замена: затронуты только строки P&L из отчёта ("
                             + string.Join(", ", scope.Select(s => s.Line).Distinct().Select(l => PnlMath.Line(l)!.Label))
                             + $") за его месяцы; удаляется значений: {removed}.");
            }
        }

        // Итоги месяцев до и после и сверка расчётных строк отчёта — по формулам OneBase.
        var months = new List<PnlMonthTotal>();
        var checkResults = new List<PnlCheck>();
        foreach (var (year, month) in periods)
        {
            var before = Month(current, year, month);
            var after = Month(projected, year, month);
            months.Add(new PnlMonthTotal(year, month, PnlMath.Sum(before.Values), PnlMath.Sum(after.Values)));
            foreach (var check in checks.Where(c => c.Year == year && c.Month == month))
            {
                var line = PnlMath.Line(check.Line)!;
                var computed = after.GetValueOrDefault(check.DealerKey)?.GetValueOrDefault(check.Line);
                var diff = computed is { } c ? check.Value - c : (decimal?)null;
                var ok = diff is { } d && Math.Abs(d) <= (line.Format == PnlFormat.Percent ? PercentTolerance : MoneyTolerance);
                if (!ok)
                {
                    checkResults.Add(new PnlCheck(names.GetValueOrDefault(check.DealerKey, check.DealerKey), year, month, check.Line, line.Label,
                        check.Value, computed, diff, check.Source));
                }
            }
        }

        if (checks.Count > 0)
        {
            warnings.Add(checkResults.Count == 0
                ? $"Расчётные строки отчёта ({checks.Count} знач.) совпали с расчётом OneBase."
                : $"Расчётные строки отчёта не совпадают с расчётом OneBase: {checkResults.Count} из {checks.Count} — смотрите сверку. "
                  + "Расчётные строки не загружаются: их считает OneBase по исходным.");
        }

        var newDealers = knownDealers.Count == 0
            ? []
            : names.Keys.Where(k => !knownDealers.ContainsKey(k) && resolved.Any(r => r.DealerKey == k))
                .Select(k => Similar(k, knownDealers) is { } similar ? $"{names[k]} (похож на «{similar}»)" : names[k])
                .ToList();
        if (newDealers.Count > 0)
        {
            warnings.Add($"Новые контрагенты, которых ещё не было в P&L: {string.Join(", ", newDealers.Take(20))}"
                         + (newDealers.Count > 20 ? $" и ещё {newDealers.Count - 20}" : "")
                         + ". Если это тот же дилер под другим именем — сопоставьте его через dealerMap.");
        }

        var conflicts = items.Count(i => i.Action == DataChangeAction.Conflict);
        if (conflicts > 0)
        {
            warnings.Insert(0, $"Конфликтов: {conflicts}. Эти строки не будут применены — уточните их и подготовьте план заново.");
        }

        return new PnlPlanningResult(items, warnings, new PnlSummary(months, checkResults.Take(200).ToList(), checks.Count, newDealers), skippedEmpty);
    }

    /// <summary>Значения дилеров за месяц по формулам OneBase из набора исходных строк (ключ → сумма).</summary>
    private static Dictionary<string, Dictionary<string, decimal?>> Month(IReadOnlyDictionary<string, decimal> rows, int year, int month)
    {
        var inputs = new Dictionary<string, Dictionary<string, decimal>>();
        var prefix = $"{DataEntityTypes.PnlValue}:{year:D4}-{month:D2}:";
        foreach (var (key, value) in rows)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var p = ParseKey(key);
            if (!inputs.TryGetValue(p.Dealer, out var dealer))
            {
                inputs[p.Dealer] = dealer = [];
            }

            dealer[p.Line] = value;
        }

        var scope = inputs.Values.SelectMany(d => d.Keys).ToHashSet();
        return inputs.ToDictionary(d => d.Key, d => PnlMath.Compute(d.Value, scope));
    }

    /// <summary>Похожее имя среди известных: те же слова без скобок и знаков, или одно имя содержит все слова другого.</summary>
    private static string? Similar(string key, IReadOnlyDictionary<string, string> known)
    {
        static HashSet<string> Words(string s) =>
            s.Split([' ', '(', ')', ',', '.', '-', '/'], StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        var words = Words(key);
        return known.Where(k => k.Key != key)
            .Select(k => (k.Value, Other: Words(k.Key)))
            .Where(k => words.SetEquals(k.Other) || (words.Count > 0 && k.Other.Count > 0 && (words.IsSubsetOf(k.Other) || k.Other.IsSubsetOf(words))))
            .Select(k => k.Value)
            .FirstOrDefault();
    }
}
