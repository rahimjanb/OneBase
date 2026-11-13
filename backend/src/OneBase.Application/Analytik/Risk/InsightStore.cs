using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Analytik;
using OneBase.Domain.Work;

namespace OneBase.Application.Analytik.Risk;

public static class InsightKinds
{
    public const string Risk = "risk";
    public const string Anomaly = "anomaly";
    public const string Quality = "quality";
    public const string Opportunity = "opportunity";
}

public static class Severities
{
    public const string Critical = "critical";
    public const string Warning = "warning";
    public const string Info = "info";

    public static int Rank(string? severity) => severity switch
    {
        Critical => 3,
        Warning => 2,
        Info => 1,
        _ => 0,
    };
}

/// <summary>
/// Находка до записи: вид, модуль, правило, ключ (одна активная на правило и объект), критичность, суть, причина — только подтверждённая данными
/// (CauseConfirmed), гипотезы, масштаб, последствия, рекомендации, доказательства (цифры, период, источники), ссылка и право просмотра.
/// </summary>
public sealed record InsightCandidate(
    string Kind, string Module, string Rule, string Key, string Severity, string Title, string Description,
    string? Cause = null, bool CauseConfirmed = false, IReadOnlyList<string>? Hypotheses = null, decimal? Impact = null, string? ImpactUnit = null,
    string? Consequences = null, IReadOnlyList<string>? Recommendations = null, object? Evidence = null, string? Href = null, string? RequiredPermission = null);

public sealed record InsightRunResult(int Active, int Created, int Updated, int Resolved, int Notified);

/// <summary>Находка для показа: всё из analytik.Insights, массивы разобраны.</summary>
public sealed record InsightView(
    Guid Id, string Kind, string Module, string Rule, string Severity, string Title, string Description, string? Cause, bool CauseConfirmed,
    IReadOnlyList<string> Hypotheses, decimal? Impact, string? ImpactUnit, string? Consequences, IReadOnlyList<string> Recommendations, JsonElement? Evidence,
    string? Href, DateTimeOffset DetectedAt, DateTimeOffset LastSeenAt, DateTimeOffset? ResolvedAt, int Occurrences, string Source = "analytik");

/// <summary>
/// Хранилище находок Аналитика с защитой от повторов: одна активная находка на ключ (уникальный индекс), повтор — обновление и счётчик,
/// исчезнувшее условие проверенного правила — закрытие. Уведомление (колокольчик) — только при новой находке или росте критичности и только
/// для critical/warning; получатели — у кого есть право находки и раздел «Аналитик»; находки продаж и финансов — только тем, кто видит всю
/// организацию (иначе через итоги компании утекли бы чужие регионы).
/// </summary>
public sealed class InsightStore(IAnalytikDb adb, IAppDbContext db, UserNotifier notifier, TimeProvider clock, InsightInvestigator? investigator = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<InsightRunResult> ApplyAsync(IReadOnlyList<InsightCandidate> found, IReadOnlySet<string> evaluatedRules, bool notify, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var active = await adb.Insights.Where(i => i.ResolvedAt == null).ToListAsync(ct);
        var byKey = active.ToDictionary(i => i.Key, StringComparer.Ordinal);
        int created = 0, updated = 0, resolved = 0;
        var toNotify = new List<AnalytikInsight>();
        // История находки (analytik.InsightEvents) и что расследовать: новые и выросшие по важности.
        var events = new List<AnalytikInsightEvent>();
        var investigate = new List<AnalytikInsight>();
        foreach (var c in found.GroupBy(c => c.Key).Select(g => g.OrderByDescending(x => Severities.Rank(x.Severity)).First()))
        {
            if (!byKey.TryGetValue(c.Key, out var row))
            {
                row = new AnalytikInsight
                {
                    Kind = c.Kind, Module = c.Module, Rule = c.Rule, Key = c.Key, Severity = c.Severity, Title = c.Title, Description = c.Description,
                    DetectedAt = now,
                };
                adb.Insights.Add(row);
                byKey[c.Key] = row;
                created++;
                var reopened = await adb.Insights.AsNoTracking().AnyAsync(i => i.Key == c.Key && i.ResolvedAt != null, ct);
                events.Add(Event(row.Id, now, reopened ? "reopened" : "created", c.Severity, c.Impact, reopened ? "Условие вернулось после закрытия." : null));
                investigate.Add(row);
            }
            else
            {
                row.Occurrences++;
                row.UpdatedAt = now;
                updated++;
                if (Severities.Rank(c.Severity) != Severities.Rank(row.Severity))
                {
                    events.Add(Event(row.Id, now, "severity", c.Severity, c.Impact, $"Важность: {row.Severity} → {c.Severity}."));
                    if (Severities.Rank(c.Severity) > Severities.Rank(row.Severity))
                    {
                        investigate.Add(row);
                    }
                }
                else if (c.Impact is { } ni && row.Impact is { } oi && oi != 0 && Math.Abs((ni - oi) / oi) > 0.1m)
                {
                    events.Add(Event(row.Id, now, "impact", c.Severity, c.Impact, $"Масштаб: {oi:N0} → {ni:N0} {c.ImpactUnit}."));
                }
            }

            row.Severity = c.Severity;
            row.Title = Trim(c.Title, 300)!;
            row.Description = Trim(c.Description, 4000)!;
            row.Cause = Trim(c.Cause, 2000);
            row.CauseConfirmed = c.CauseConfirmed && c.Cause is not null;
            row.Hypotheses = c.Hypotheses is { Count: > 0 } h ? JsonSerializer.Serialize(h, Json) : null;
            row.Impact = c.Impact is { } impact ? Math.Round(impact, 3) : null;
            row.ImpactUnit = c.ImpactUnit;
            row.Consequences = Trim(c.Consequences, 2000);
            row.Recommendations = c.Recommendations is { Count: > 0 } r ? JsonSerializer.Serialize(r, Json) : null;
            row.Evidence = c.Evidence is null ? null : JsonSerializer.Serialize(c.Evidence, Json);
            row.Href = c.Href;
            row.RequiredPermission = c.RequiredPermission;
            row.LastSeenAt = now;
            if (notify && Severities.Rank(c.Severity) >= Severities.Rank(NotifyFrom(c.Kind)) && Severities.Rank(c.Severity) > Severities.Rank(row.NotifiedSeverity))
            {
                toNotify.Add(row);
            }
        }

        var keys = found.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var row in active.Where(i => evaluatedRules.Contains(i.Rule) && !keys.Contains(i.Key)))
        {
            row.ResolvedAt = now;
            resolved++;
            events.Add(Event(row.Id, now, "resolved", row.Severity, row.Impact, "Условие больше не выполняется."));
        }

        // Авторасследование — до уведомления: открыв находку из колокольчика, человек видит и вклад, и уверенность. Сбой расследования не мешает находкам.
        if (investigator is not null)
        {
            foreach (var row in investigate.Where(r => Severities.Rank(r.Severity) >= Severities.Rank(Severities.Warning)).Take(MaxInvestigations))
            {
                try
                {
                    if (await investigator.InvestigateAsync(row, now, ct) is { } conclusion)
                    {
                        events.Add(Event(row.Id, now, "investigated", row.Severity, row.Impact, conclusion));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    row.Investigation = JsonSerializer.Serialize(new { error = "Авторасследование не выполнено." }, Json);
                }
            }
        }

        adb.InsightEvents.AddRange(events);

        var notified = 0;
        if (toNotify.Count > 0)
        {
            notified = await NotifyAsync(toNotify, now, ct);
        }

        await adb.SaveChangesAsync(ct);
        return new InsightRunResult(byKey.Count - resolved, created, updated, resolved, notified);
    }

    /// <summary>
    /// С какой важности находка уведомляет: риски и отклонения — с «внимание», качество данных — только критичное (предупреждения качества
    /// видны на экране «Риски», но не должны засыпать колокольчик при каждом обнаружении).
    /// </summary>
    internal static string NotifyFrom(string kind) => kind == InsightKinds.Quality ? Severities.Critical : Severities.Warning;

    /// <summary>Сколько находок за проход расследуется (каждая — несколько срезов движка).</summary>
    internal const int MaxInvestigations = 5;

    private static AnalytikInsightEvent Event(Guid insight, DateTimeOffset at, string kind, string? severity, decimal? impact, string? note) =>
        new() { InsightId = insight, At = at, Event = kind, Severity = severity, Impact = impact is { } i ? Math.Round(i, 3) : null, Note = Trim(note, 2000) };

    /// <summary>Больше стольких новых находок пользователю за один проход — одно сводное уведомление вместо отдельных.</summary>
    internal const int DigestAfter = 3;

    private async Task<int> NotifyAsync(IReadOnlyList<AnalytikInsight> insights, DateTimeOffset now, CancellationToken ct)
    {
        var analysts = (await db.ActiveUsersWithAsync(Permissions.AnalytikUse, ct, withoutAdmin: true)).ToHashSet();
        var scoped = (await db.SalesUserScopes.AsNoTracking().Select(s => s.UserId).ToListAsync(ct)).ToHashSet();
        var cache = new Dictionary<string, HashSet<Guid>>();
        var perUser = new Dictionary<Guid, List<AnalytikInsight>>();
        foreach (var insight in insights)
        {
            var recipients = analysts.AsEnumerable();
            // Право находки: любое из перечисленных через «|».
            if (insight.RequiredPermission is { Length: > 0 } required)
            {
                if (!cache.TryGetValue(required, out var holders))
                {
                    holders = [];
                    foreach (var p in required.Split('|', StringSplitOptions.RemoveEmptyEntries))
                    {
                        holders.UnionWith(await db.ActiveUsersWithAsync(p, ct, withoutAdmin: true));
                    }

                    cache[required] = holders;
                }

                recipients = recipients.Where(holders.Contains);
            }

            if (insight.Module is "sales" or "finance")
            {
                recipients = recipients.Where(u => !scoped.Contains(u));
            }

            foreach (var user in recipients.Take(50))
            {
                (perUser.TryGetValue(user, out var list) ? list : perUser[user] = []).Add(insight);
            }

            insight.NotifiedAt = now;
            insight.NotifiedSeverity = insight.Severity;
        }

        var count = 0;
        foreach (var (user, list) in perUser)
        {
            if (list.Count > DigestAfter)
            {
                var critical = list.Count(i => i.Severity == Severities.Critical);
                var top = list.OrderByDescending(i => Severities.Rank(i.Severity)).Take(3).Select(i => i.Title);
                notifier.Notify(user, UserNotificationKind.AgentAlert,
                    $"Аналитик: новых рисков — {list.Count}" + (critical > 0 ? $", критичных — {critical}" : ""),
                    Trim(string.Join("; ", top) + (list.Count > 3 ? $" и ещё {list.Count - 3}." : "."), 400), "/analytik/risks");
            }
            else
            {
                foreach (var insight in list)
                {
                    notifier.Notify(user, UserNotificationKind.AgentAlert, $"Аналитик: {insight.Title}", Trim(insight.Description, 400), insight.Href ?? "/analytik/risks");
                }
            }

            count += list.Count > DigestAfter ? 1 : list.Count;
        }

        return count;
    }

    /// <summary>Активные находки, которые пользователь может видеть (право находки; продажи и финансы — только при доступе ко всей организации).</summary>
    public async Task<IReadOnlyList<InsightView>> ListAsync(AnalytikPrincipal principal, string? module, bool resolved, int take, CancellationToken ct)
    {
        var since = clock.GetUtcNow().AddDays(-30);
        var rows = await adb.Insights.AsNoTracking()
            .Where(i => resolved ? i.ResolvedAt != null && i.ResolvedAt >= since : i.ResolvedAt == null)
            .Where(i => module == null || i.Module == module)
            .OrderByDescending(i => i.LastSeenAt)
            .Take(500)
            .ToListAsync(ct);
        return rows.Where(i => Visible(i.RequiredPermission, i.Module, principal))
            .OrderByDescending(i => Severities.Rank(i.Severity)).ThenByDescending(i => i.DetectedAt)
            .Take(Math.Clamp(take, 1, 500))
            .Select(View)
            .ToList();
    }

    public static bool Visible(string? permission, string module, AnalytikPrincipal p) =>
        (permission is null || p.HasAny(permission.Split('|', StringSplitOptions.RemoveEmptyEntries)))
        && !(module is "sales" or "finance" && p.Sales.Restricted);

    public static InsightView View(AnalytikInsight i) => new(i.Id, i.Kind, i.Module, i.Rule, i.Severity, i.Title, i.Description, i.Cause, i.CauseConfirmed,
        Strings(i.Hypotheses), i.Impact, i.ImpactUnit, i.Consequences, Strings(i.Recommendations),
        i.Evidence is null ? null : JsonSerializer.Deserialize<JsonElement>(i.Evidence), i.Href, i.DetectedAt, i.LastSeenAt, i.ResolvedAt, i.Occurrences);

    private static IReadOnlyList<string> Strings(string? json) => json is null ? [] : JsonSerializer.Deserialize<List<string>>(json) ?? [];

    private static string? Trim(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}
