using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Compute;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Semantic;
using OneBase.Domain.Analytik;

namespace OneBase.Application.Analytik.Decisions;

/// <summary>Показатель решения: код показателя движка срезов, фильтры (регион, группа, агент…), сегмент продаж.</summary>
public sealed record DecisionMetric(string Metric, IReadOnlyList<AnalyticsFilter>? Filters = null, string? Segment = null);

/// <summary>Связи решения: исследования, находки, сценарии.</summary>
public sealed record DecisionLinks(IReadOnlyList<Guid>? Investigations = null, IReadOnlyList<Guid>? Insights = null, IReadOnlyList<Guid>? Scenarios = null);

/// <summary>Новое решение: что решили, почему, когда, какой показатель и как должен измениться к какой дате.</summary>
public sealed record DecisionInput(
    string Title, string? Description, string? Reason, DateOnly DecidedOn, DecisionMetric Metric, string ExpectedDirection, decimal? ExpectedChangePct,
    DateOnly ReviewOn, DateOnly? BaselineFrom = null, DateOnly? BaselineTo = null, string? ExpectedText = null, DecisionLinks? Links = null, string? Notes = null);

/// <summary>Решение для показа: всё из analytik.Decisions, автор, пора ли проверять.</summary>
public sealed record DecisionView(
    Guid Id, string Title, string? Description, string? Reason, string Module, DateOnly DecidedOn, Guid AuthorId, string? AuthorName, DecisionMetric Metric,
    string? MetricTitle, DateOnly BaselineFrom, DateOnly BaselineTo, decimal? BaselineValue, JsonElement? Baseline, string ExpectedDirection,
    decimal? ExpectedChangePct, string? ExpectedText, DateOnly ReviewOn, string Status, string? Outcome, decimal? ActualValue, JsonElement? Actual,
    DateTimeOffset? EvaluatedAt, DecisionLinks? Links, string? Notes, DateTimeOffset CreatedAt, bool Due);

/// <summary>
/// Business Decision Memory: управленческие решения с исходными показателями и проверкой результата в срок. Исходное значение — срез движка за
/// период до решения (по умолчанию 30 дней), факт — тот же срез за период после решения; сравниваются средние в день, значимость — t-тест по
/// дням (CausalMath.BeforeAfter). Вердикт: достигнуто, частично, не достигнуто, не ясно (мало данных). Это сравнение «до / после», а не
/// доказательство, что изменение дало решение, — так и пишется. Неподтверждённые предположения фактами не сохраняются: ожидание — отдельно
/// от факта. Видят решение автор и управляющий Аналитиком; данные — по правам автора.
/// </summary>
public sealed class DecisionService(SemanticEngine engine, AnalytikAccess access, IAnalytikDb adb, IAppDbContext db, AnalytikOptions options, TimeProvider clock)
{
    public static readonly string[] Directions = ["up", "down", "keep"];
    public const int DefaultBaselineDays = 30;
    public const int MinPostDays = 7;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public DateOnly Today => engine.Today;

    public async Task<DecisionView> CreateAsync(AnalytikPrincipal p, DecisionInput input, CancellationToken ct)
    {
        StatMath.Require(p.UserId is not null, "Решение записывает пользователь.");
        StatMath.Require(input.Title.Trim().Length is > 0 and <= 300, "Название решения — от 1 до 300 символов.");
        StatMath.Require(Directions.Contains(input.ExpectedDirection), "Ожидание: up (рост), down (снижение) или keep (без изменений).");
        StatMath.Require(input.ReviewOn > input.DecidedOn, "Дата проверки должна быть позже даты решения.");
        var metric = SemanticCatalog.Metric(input.Metric.Metric) ?? throw new ComputeException($"Показателя «{input.Metric.Metric}» нет.");
        StatMath.Require(!metric.Calculated || metric.Code is "avg_check" or "avg_price_kg", "Для решения нужен показатель-сумма (выручка, кг, заказы, АКБ, оплаты…).");
        var to = input.BaselineTo ?? input.DecidedOn.AddDays(-1);
        var from = input.BaselineFrom ?? to.AddDays(-(DefaultBaselineDays - 1));
        StatMath.Require(from <= to && to < input.DecidedOn, "Исходный период должен закончиться до даты решения.");

        var baseline = await TotalAsync(p, input.Metric, from, to, ct);
        var row = new AnalytikDecision
        {
            Tenant = options.Tenant, AuthorId = p.UserId!.Value, Title = input.Title.Trim(), Description = Cut(input.Description, 4000), Reason = Cut(input.Reason, 4000),
            Module = metric.Fact == SemanticCatalog.Payments ? "finance" : "sales", DecidedOn = input.DecidedOn,
            Metric = JsonSerializer.Serialize(input.Metric, Json), MetricTitle = metric.Title + Filters(input.Metric),
            BaselineFrom = from, BaselineTo = to, BaselineValue = baseline.Value,
            Baseline = JsonSerializer.Serialize(new { baseline.Value, perDay = PerDay(baseline.Value, from, to), baseline.QueryId, baseline.Complete, baseline.Sources }, Json),
            ExpectedDirection = input.ExpectedDirection, ExpectedChangePct = input.ExpectedChangePct, ExpectedText = Cut(input.ExpectedText, 2000),
            ReviewOn = input.ReviewOn, Links = input.Links is null ? null : JsonSerializer.Serialize(input.Links, Json), Notes = Cut(input.Notes, 4000),
        };
        adb.Decisions.Add(row);
        await adb.SaveChangesAsync(ct);
        return await ViewAsync(row, ct);
    }

    public async Task<IReadOnlyList<DecisionView>> ListAsync(AnalytikPrincipal p, string? status, string? text, CancellationToken ct)
    {
        var q = adb.Decisions.AsNoTracking().Where(d => d.Tenant == options.Tenant);
        if (!p.Manager)
        {
            q = q.Where(d => d.AuthorId == p.UserId);
        }

        if (status is { Length: > 0 })
        {
            q = q.Where(d => d.Status == status);
        }

        if (text is { Length: > 0 } t)
        {
            var like = $"%{t.Trim().ToLowerInvariant()}%";
            q = q.Where(d => EF.Functions.Like(d.Title.ToLower(), like) || (d.Reason != null && EF.Functions.Like(d.Reason.ToLower(), like)));
        }

        var rows = await q.OrderByDescending(d => d.DecidedOn).ThenByDescending(d => d.CreatedAt).Take(200).ToListAsync(ct);
        var views = new List<DecisionView>();
        foreach (var r in rows)
        {
            views.Add(await ViewAsync(r, ct));
        }

        return views;
    }

    public async Task<DecisionView> GetAsync(AnalytikPrincipal p, Guid id, CancellationToken ct) => await ViewAsync(await RowAsync(p, id, ct), ct);

    public async Task<DecisionView> CancelAsync(AnalytikPrincipal p, Guid id, string? note, CancellationToken ct)
    {
        var row = await RowAsync(p, id, ct);
        StatMath.Require(row.AuthorId == p.UserId, "Отменить решение может только автор.");
        row.Status = AnalytikDecision.Cancelled;
        row.Notes = Cut(string.Join("\n", new[] { row.Notes, note }.Where(x => x is { Length: > 0 })), 4000);
        row.UpdatedAt = clock.GetUtcNow();
        await adb.SaveChangesAsync(ct);
        return await ViewAsync(row, ct);
    }

    /// <summary>Оценить решение сейчас (кнопка) — по правам того, кто нажал; срок мог ещё не наступить — тогда факт по сегодняшний день.</summary>
    public async Task<DecisionView> EvaluateAsync(AnalytikPrincipal p, Guid id, CancellationToken ct)
    {
        var row = await RowAsync(p, id, ct);
        await EvaluateRowAsync(row, p, ct);
        await adb.SaveChangesAsync(ct);
        return await ViewAsync(row, ct);
    }

    /// <summary>Фоновая проверка: решения, у которых наступил срок, оцениваются по правам автора. Возвращает, сколько оценено.</summary>
    public async Task<int> EvaluateDueAsync(CancellationToken ct)
    {
        var today = Today;
        var due = await adb.Decisions.Where(d => d.Tenant == options.Tenant && d.Status == AnalytikDecision.Active && d.ReviewOn <= today).Take(50).ToListAsync(ct);
        foreach (var row in due)
        {
            AnalytikPrincipal author;
            try
            {
                author = await access.ResolveAsync(row.AuthorId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                continue;
            }

            await EvaluateRowAsync(row, author, ct);
        }

        if (due.Count > 0)
        {
            await adb.SaveChangesAsync(ct);
        }

        return due.Count;
    }

    private async Task EvaluateRowAsync(AnalytikDecision row, AnalytikPrincipal p, CancellationToken ct)
    {
        var metric = JsonSerializer.Deserialize<DecisionMetric>(row.Metric, Json)!;
        var postFrom = row.DecidedOn;
        var postTo = row.ReviewOn < Today ? row.ReviewOn : Today;
        var notes = new List<string>();
        try
        {
            var actual = await TotalAsync(p, metric, postFrom, postTo, ct);
            var through = actual.Sources.Select(s => s.DataThrough).Where(d => d is not null).Min() ?? postTo;
            var end = through < postTo ? through : postTo;
            var days = end.DayNumber - postFrom.DayNumber + 1;
            var baseDays = row.BaselineTo.DayNumber - row.BaselineFrom.DayNumber + 1;
            var basePerDay = PerDay(row.BaselineValue, row.BaselineFrom, row.BaselineTo);
            var actualValue = end < postTo ? (await TotalAsync(p, metric, postFrom, end, ct)).Value : actual.Value;
            var perDay = PerDay(actualValue, postFrom, end);
            decimal? change = basePerDay is { } b && b != 0 && perDay is { } a ? Math.Round((a - b) / Math.Abs(b) * 100, 2) : null;

            BeforeAfterResult? test = null;
            if (days >= 2)
            {
                var series = await DailyAsync(p, metric, row.BaselineFrom, end, ct);
                var before = series.Where(x => x.Date <= row.BaselineTo).Select(x => x.Value).ToList();
                var after = series.Where(x => x.Date >= postFrom).Select(x => x.Value).ToList();
                if (before.Count >= 2 && after.Count >= 2)
                {
                    test = CausalMath.BeforeAfter(before, after);
                }
            }

            if (end < postTo)
            {
                notes.Add($"Данные источника — по {end:dd.MM.yyyy}: факт посчитан по эту дату.");
            }

            notes.Add("Сравнение «до / после» — без контрольной группы оно не доказывает, что изменение дало именно это решение.");
            var outcome = Outcome(row.ExpectedDirection, row.ExpectedChangePct, change, days, test);
            row.ActualValue = actualValue;
            row.Actual = JsonSerializer.Serialize(new
            {
                postFrom, postTo = end, days, baselineDays = baseDays, value = actualValue, perDay, baselinePerDay = basePerDay, changePct = change,
                test = test is null ? null : new { test.Test.P, test.Test.Diff, test.Test.DiffPct, test.EvidenceClass, test.TrendAdjustedPct },
                evidenceClass = test?.EvidenceClass ?? EvidenceClasses.Observed, actual.QueryId, notes,
                text = Text(row, change, outcome, test),
            }, Json);
            row.Outcome = outcome;
        }
        catch (Exception ex) when (ex is AnalyticsQueryException or ComputeException or UnauthorizedAccessException)
        {
            row.Outcome = "inconclusive";
            row.Actual = JsonSerializer.Serialize(new { error = ex.Message, notes }, Json);
        }

        row.EvaluatedAt = clock.GetUtcNow();
        if (row.ReviewOn <= Today)
        {
            row.Status = AnalytikDecision.Evaluated;
        }
    }

    /// <summary>Вердикт: мало дней после решения — не ясно; без изменений — в пределах ожидаемого; рост / снижение — по знаку и величине.</summary>
    public static string Outcome(string direction, decimal? expectedPct, decimal? changePct, int days, BeforeAfterResult? test)
    {
        if (changePct is not { } c || days < MinPostDays)
        {
            return "inconclusive";
        }

        if (direction == "keep")
        {
            return Math.Abs(c) <= Math.Abs(expectedPct ?? 5) ? "achieved" : "missed";
        }

        var sign = direction == "up" ? 1 : -1;
        if (Math.Sign(c) != sign || Math.Abs(c) < 1)
        {
            return "missed";
        }

        return expectedPct is null || Math.Abs(c) >= Math.Abs(expectedPct.Value) ? "achieved" : "partial";
    }

    private static string Text(AnalytikDecision row, decimal? change, string outcome, BeforeAfterResult? test) =>
        (outcome switch
        {
            "achieved" => "Ожидание выполнено",
            "partial" => "Ожидание выполнено частично",
            "missed" => "Ожидание не выполнено",
            _ => "Пока не ясно",
        }) + (change is { } c ? $": «{row.MetricTitle}» в среднем за день {(c >= 0 ? "+" : "")}{c.ToString("N1", Ru)} % к периоду до решения" : "") +
        (row.ExpectedChangePct is { } e ? $" (ожидалось {(row.ExpectedDirection == "down" ? "−" : row.ExpectedDirection == "up" ? "+" : "±")}{Math.Abs(e).ToString("N1", Ru)} %)" : "") +
        (test is null ? "." : $"; {EvidenceClasses.Title(test.EvidenceClass)} (p = {test.Test.P.ToString("0.000", Ru)}).");

    private async Task<(decimal? Value, Guid QueryId, bool Complete, IReadOnlyList<AnalyticsSource> Sources)> TotalAsync(AnalytikPrincipal p, DecisionMetric m,
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        var r = await engine.RunAsync(new AnalyticsQuery
        {
            Metrics = [m.Metric], DateFrom = from, DateTo = to, Filters = m.Filters, Segment = m.Segment, IncludeTotals = true,
        }, new QueryCaller(p.UserId, "decide"), ct, p);
        var value = r.Totals.GetValueOrDefault(m.Metric) ?? (r.Rows.Count == 1 && r.Rows[0].Length > 0 && r.Rows[0][^1] is { } v ? Convert.ToDecimal(v, CultureInfo.InvariantCulture) : null);
        return (value, r.QueryId, r.Complete, r.Sources);
    }

    private async Task<List<(DateOnly Date, double Value)>> DailyAsync(AnalytikPrincipal p, DecisionMetric m, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var r = await engine.RunAsync(new AnalyticsQuery
        {
            Metrics = [m.Metric], Dimensions = ["date"], Grain = "day", DateFrom = from, DateTo = to, Filters = m.Filters, Segment = m.Segment, FillZeros = true,
            IncludeTotals = false, Limit = SemanticEngine.MaxLimit,
        }, new QueryCaller(p.UserId, "decide"), ct, p);
        return r.Rows.Select(row => (DateOnly.ParseExact(row[0]!.ToString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            row[1] is { } v ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : 0)).ToList();
    }

    private static decimal? PerDay(decimal? value, DateOnly from, DateOnly to) => value is { } v ? Math.Round(v / (to.DayNumber - from.DayNumber + 1), 4) : null;

    private static string Filters(DecisionMetric m) =>
        m.Filters is { Count: > 0 } f ? " (" + string.Join("; ", f.Select(x => string.Join(", ", x.Values ?? []))) + ")" : "";

    private async Task<AnalytikDecision> RowAsync(AnalytikPrincipal p, Guid id, CancellationToken ct)
    {
        var row = await adb.Decisions.FirstOrDefaultAsync(d => d.Id == id && d.Tenant == options.Tenant, ct);
        if (row is null || (row.AuthorId != p.UserId && !p.Manager))
        {
            throw new KeyNotFoundException("Решение не найдено.");
        }

        return row;
    }

    private async Task<DecisionView> ViewAsync(AnalytikDecision d, CancellationToken ct)
    {
        var author = await db.Users.AsNoTracking().Where(u => u.Id == d.AuthorId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return new DecisionView(d.Id, d.Title, d.Description, d.Reason, d.Module, d.DecidedOn, d.AuthorId, author, JsonSerializer.Deserialize<DecisionMetric>(d.Metric, Json)!,
            d.MetricTitle, d.BaselineFrom, d.BaselineTo, d.BaselineValue, Element(d.Baseline), d.ExpectedDirection, d.ExpectedChangePct, d.ExpectedText, d.ReviewOn,
            d.Status, d.Outcome, d.ActualValue, Element(d.Actual), d.EvaluatedAt, d.Links is null ? null : JsonSerializer.Deserialize<DecisionLinks>(d.Links, Json), d.Notes,
            d.CreatedAt, d.Status == AnalytikDecision.Active && d.ReviewOn <= Today);
    }

    private static JsonElement? Element(string? json) => json is null ? null : JsonDocument.Parse(json).RootElement.Clone();

    private static string? Cut(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}
