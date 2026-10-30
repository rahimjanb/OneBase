using OneBase.Application.Field;
using OneBase.Application.Hr;

namespace OneBase.AI.Tools.Data;

/// <summary>
/// HR за период длиннее квартала. Страницы и API HR считают не больше 62 дней, AI — до квартала (92 дня, как «Отчёты») за один вызов
/// HrService; период до года режется на части по 92 дня подряд (PeriodRanges.Chunks — без пересечений и пропусков, EnsureContiguous это
/// проверяет), каждая часть считается сервисом, результаты объединяются: итоги и дни по документам, опозданиям, отсутствиям — складываются
/// (это счётчики дней и минут непересекающихся частей), буквы табеля — подряд.
/// </summary>
internal static class HrPeriods
{
    /// <summary>Предел одного вызова HrService для AI — квартал.</summary>
    public const int ChunkDays = HrService.MaxReportDays;

    /// <summary>Самый длинный период HR для AI — год, частями по ChunkDays.</summary>
    public const int MaxDays = PeriodRanges.MaxDays;

    /// <summary>
    /// Части периода from–to (from не задан — null: один вызов, умолчание периода — у HrService; to не задан — сегодня по Ташкенту): до
    /// квартала — одна часть, длиннее — по 92 дня. Длиннее года — ошибка аргументов.
    /// </summary>
    public static IReadOnlyList<DateRange>? Split(DateOnly? from, DateOnly? to)
    {
        if (from is not { } f)
        {
            return null;
        }

        var t = to ?? FieldClock.Today;
        var range = f <= t ? new DateRange(f, t) : new DateRange(t, f);
        if (range.Days > MaxDays)
        {
            throw new ToolArgumentException($"Период — не больше {MaxDays} дней (год): {range.Label} — {range.Days} дн. Разбейте запрос на части.");
        }

        var parts = PeriodRanges.Chunks(range, ChunkDays);
        PeriodRanges.EnsureContiguous(parts, range);
        return parts;
    }

    /// <summary>Каждая часть по очереди (не параллельно: один DbContext на запрос).</summary>
    public static async Task<IReadOnlyList<T>> EachAsync<T>(IReadOnlyList<DateRange> parts, Func<DateOnly, DateOnly, Task<T>> load)
    {
        var results = new List<T>(parts.Count);
        foreach (var part in parts)
        {
            results.Add(await load(part.From, part.To));
        }

        return results;
    }

    /// <summary>
    /// Табели частей → один табель периода: даты подряд, строка сотрудника — сумма итогов частей и буквы дней подряд (в части, где его нет —
    /// не работал или ещё не принят, — пустые буквы, как «не работал» у сервиса); итог — сумма итогов частей; синхронизация — последней.
    /// </summary>
    public static HrTimesheet Merge(IReadOnlyList<HrTimesheet> parts)
    {
        if (parts.Count == 0)
        {
            throw new ArgumentException("Нет частей табеля.", nameof(parts));
        }

        if (parts.Count == 1)
        {
            return parts[0];
        }

        var whole = new DateRange(parts[0].From, parts[^1].To);
        PeriodRanges.EnsureContiguous(parts.Select(p => new DateRange(p.From, p.To)).ToList(), whole);
        var dates = parts.SelectMany(p => p.Dates).ToList();
        if (dates.Count != whole.Days || dates.Distinct().Count() != dates.Count)
        {
            throw new InvalidOperationException($"Дни табеля {whole.Label} пересекаются или с пропуском.");
        }

        var rows = new Dictionary<Guid, (HrTimesheetRow Last, HrSummary Summary, List<string> Codes)>();
        var offset = 0;
        foreach (var part in parts)
        {
            foreach (var row in part.Rows)
            {
                if (row.Codes.Count != part.Dates.Count)
                {
                    throw new InvalidOperationException($"Табель {row.Employee}: букв дней {row.Codes.Count}, дней {part.Dates.Count}.");
                }

                var (_, summary, codes) = rows.TryGetValue(row.EmployeeId, out var known) ? known : (row, Zero, new List<string>());
                codes.AddRange(Enumerable.Repeat("", offset - codes.Count));
                codes.AddRange(row.Codes);
                rows[row.EmployeeId] = (row, Add(summary, row.Summary), codes);
            }

            offset += part.Dates.Count;
        }

        var merged = rows.Select(p =>
            {
                var (last, summary, codes) = p.Value;
                codes.AddRange(Enumerable.Repeat("", dates.Count - codes.Count));
                // Имя, подразделение и должность — последней части, где сотрудник есть: так он сейчас в Verifix.
                return new HrTimesheetRow(p.Key, last.Employee, last.Division, last.Job, summary, codes);
            })
            .OrderBy(r => r.Division).ThenBy(r => r.Employee)
            .ToList();
        return new HrTimesheet(whole.From, whole.To, dates, merged, Sum(parts.Select(p => p.Total)), parts[^1].Sync);
    }

    /// <summary>Отклонения частей → одним списком, как у сервиса: сначала новые дни, внутри дня — по ФИО. Случай вне своей части — ошибка.</summary>
    public static IReadOnlyList<HrDeviation> Merge(IReadOnlyList<DateRange> parts, IReadOnlyList<IReadOnlyList<HrDeviation>> lists)
    {
        if (parts.Count != lists.Count)
        {
            throw new ArgumentException("Частей и списков отклонений — разное число.", nameof(lists));
        }

        for (var i = 0; i < parts.Count; i++)
        {
            if (lists[i].FirstOrDefault(d => d.Date < parts[i].From || d.Date > parts[i].To) is { } outside)
            {
                throw new InvalidOperationException($"Отклонение за {outside.Date:yyyy-MM-dd} вне части {parts[i].Label}.");
            }
        }

        return lists.SelectMany(l => l).OrderByDescending(d => d.Date).ThenBy(d => d.Employee).ToList();
    }

    public static readonly HrSummary Zero = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public static HrSummary Sum(IEnumerable<HrSummary> all) => all.Aggregate(Zero, Add);

    public static HrSummary Add(HrSummary a, HrSummary s) => new(
        a.PlanDays + s.PlanDays, a.PresentDays + s.PresentDays, a.LateDays + s.LateDays, a.LateMinutes + s.LateMinutes, a.EarlyDays + s.EarlyDays,
        a.EarlyMinutes + s.EarlyMinutes, a.AbsentDays + s.AbsentDays, a.LeaveDays + s.LeaveDays, a.UnknownDays + s.UnknownDays, a.NoExitDays + s.NoExitDays,
        a.WorkedMinutes + s.WorkedMinutes, a.PlanMinutes + s.PlanMinutes);
}
