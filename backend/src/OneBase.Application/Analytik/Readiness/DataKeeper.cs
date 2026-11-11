using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using OneBase.Application.Field;

namespace OneBase.Application.Analytik.Readiness;

/// <summary>Последний запуск обновления Аналитиком по источнику: когда, кем вызван (фон, вопрос, панель, утро), результат, номер подряд.</summary>
public sealed record KeeperAttempt(DateTimeOffset At, string Trigger, SyncStartResult Result, int Streak);

/// <summary>Память контроля данных между вызовами (одна на процесс): последние запуски обновлений — для паузы между повторами и текста «что сделано».</summary>
public sealed class DataKeeperState
{
    private readonly ConcurrentDictionary<string, KeeperAttempt> _attempts = new(StringComparer.Ordinal);

    public KeeperAttempt? Last(string code) => _attempts.TryGetValue(code, out var a) ? a : null;

    public void Record(string code, KeeperAttempt attempt) => _attempts[code] = attempt;

    public void Clear(string code) => _attempts.TryRemove(code, out _);
}

/// <summary>
/// Контроль данных Аналитика — чтобы данные были загружены заранее, а не «данных нет» в ответ на вопрос:
/// <list type="bullet">
/// <item>данные внешней системы отстали (дольше допустимого без обновления) — Аналитик сам запускает обновление; не помогло — повторяет с паузой
/// 10, 20, 40… минут (не чаще раза в 2 часа), пока данные не станут свежими;</item>
/// <item>утром перед ежедневным отчётом (за 45 минут до Analytik:DailyAt) — догружает вчерашний день, если после полуночи обмена ещё не было;</item>
/// <item>исправить сам не может (интеграция не подключена или автоматический обмен выключен) — один раз уведомляет администраторов интеграций,
/// а когда данные снова свежие — сообщает о восстановлении;</item>
/// <item>перед ответом AI-аналитика — то же самое и ждёт идущего обновления до Analytik:FreshWaitSeconds секунд.</item>
/// </list>
/// Автоматический обмен, выключенный администратором, Аналитик не включает: только предупреждает.
/// </summary>
public sealed class DataKeeper(DataReadiness readiness, IAnalytikSourceSync sources, DataKeeperState state, AnalytikOptions options, TimeProvider clock,
    ILogger<DataKeeper> logger)
{
    /// <summary>Перед ежедневным отчётом: за сколько до Analytik:DailyAt догружать вчерашний день и сколько после ждать данных.</summary>
    public static readonly TimeSpan MorningLead = TimeSpan.FromMinutes(45);

    public static readonly TimeSpan MorningHold = TimeSpan.FromHours(2);

    /// <summary>Пауза перед повтором запуска: 10, 20, 40, 80 минут, дальше — раз в 2 часа.</summary>
    public static TimeSpan Backoff(int streak) =>
        streak <= 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(Math.Min(120, 10 * Math.Pow(2, Math.Min(streak, 8) - 1)));

    /// <summary>Окно утренней догрузки: от DailyAt − 45 минут до DailyAt + 2 часа по Ташкенту.</summary>
    public static bool InMorning(DateTimeOffset utc, string dailyAt)
    {
        var at = DailyAt(dailyAt);
        var local = TimeOnly.FromDateTime(utc.ToOffset(FieldClock.Offset).DateTime);
        var from = at.Add(-MorningLead);
        return from <= at ? local >= from && local <= at.Add(MorningHold) : local >= from || local <= at.Add(MorningHold);
    }

    public static TimeOnly DailyAt(string dailyAt) =>
        TimeOnly.TryParse(dailyAt, CultureInfo.InvariantCulture, out var t) ? t : new TimeOnly(7, 30);

    /// <summary>
    /// Проход контроля (фон — каждые 5 минут, панель, вопрос): запускает обновления отстающих источников, сообщает о том, что исправить нельзя.
    /// Возвращает готовность после запусков.
    /// </summary>
    public async Task<ReadinessReport> RunAsync(string trigger, CancellationToken ct)
    {
        var report = await readiness.CheckAsync(ct);
        var now = report.CheckedAt;
        var attempted = false;
        foreach (var link in report.Links)
        {
            var sets = report.Datasets.Where(d => d.Source == link.Code && d.Status != ReadinessStatus.Missing).ToList();
            if (sets.Count == 0)
            {
                continue;
            }

            if (!link.Connected || !link.AutoSync)
            {
                state.Clear(link.Code);
                // Источник уже использовался (данные были) — сказать тем, кто может подключить; ни разу не подключённый — не повод.
                if (sets.Any(d => d.UpdatedAt is not null))
                {
                    await AlertAsync(link, ct);
                }

                continue;
            }

            var morning = link.Code == SourceCodes.Linko && InMorning(now, options.DailyAt)
                          && sets.Any(d => d.Code == "sales" && (d.UpdatedAt is null || d.UpdatedAt < FieldClock.DayStartUtc(FieldClock.LocalDate(now))));
            if (!sets.Any(d => d.Lagging) && !morning)
            {
                state.Clear(link.Code);
                continue;
            }

            if (link.Running)
            {
                continue;
            }

            var last = state.Last(link.Code);
            if (last is not null && now - last.At < Backoff(last.Streak))
            {
                continue;
            }

            SyncStartResult result;
            try
            {
                result = await sources.StartAsync(link.Code, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Аналитик: обновление {Source} не запущено", link.Title);
                result = SyncStartResult.Failed;
            }

            state.Record(link.Code, new KeeperAttempt(now, trigger, result, (last?.Streak ?? 0) + 1));
            attempted = true;
            logger.LogInformation("Аналитик: данные {Source} отстают ({Trigger}{Morning}) — обновление: {Result}, попытка {Streak}", link.Title, trigger,
                morning ? ", утренняя догрузка" : "", result, (last?.Streak ?? 0) + 1);
        }

        // После запуска — заново: статус «обновляется» и «что сделано» уже с этой попыткой.
        return attempted ? await readiness.CheckAsync(ct) : report;
    }

    /// <summary>
    /// Перед ответом AI-аналитика: проход контроля и ожидание идущего обновления отстающих данных (до <paramref name="maxWait"/>); <paramref name="waiting"/>
    /// — сказать пользователю, что идёт обновление (один раз). Waited — ждали обновления.
    /// </summary>
    public async Task<(ReadinessReport Report, bool Waited)> EnsureFreshAsync(TimeSpan maxWait, Func<string, Task>? waiting, CancellationToken ct)
    {
        var report = await RunAsync("question", ct);
        var deadline = clock.GetUtcNow() + maxWait;
        var waited = false;
        while (true)
        {
            var running = report.Links
                .Where(l => l.Running && report.Datasets.Any(d => d.Source == l.Code && d.Lagging && d.Status != ReadinessStatus.Missing)).ToList();
            if (running.Count == 0 || clock.GetUtcNow() >= deadline)
            {
                return (report, waited);
            }

            if (!waited && waiting is not null)
            {
                await waiting(string.Join(", ", running.Select(l => l.Title)));
            }

            waited = true;
            await Task.Delay(TimeSpan.FromSeconds(2), clock, ct);
            report = await readiness.CheckAsync(ct);
        }
    }

    /// <summary>
    /// Ежедневный отчёт ждёт вчерашних продаж: до DailyAt + 2 часа, пока после полуночи не было обмена с Linko, который может пройти сам
    /// (подключён, обмен по расписанию). true — отложить отчёт до следующей проверки.
    /// </summary>
    public async Task<bool> HoldDailyAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (!InMorning(now, options.DailyAt))
        {
            return false;
        }

        var report = await RunAsync("daily", ct);
        if (report.Link(SourceCodes.Linko) is not { Connected: true, AutoSync: true })
        {
            return false;
        }

        var sales = report.Datasets.FirstOrDefault(d => d.Code == "sales");
        var hold = sales is not null && (sales.UpdatedAt is null || sales.UpdatedAt < FieldClock.DayStartUtc(FieldClock.LocalDate(now)));
        if (hold)
        {
            logger.LogInformation("Аналитик: ежедневный отчёт ждёт вчерашних данных Linko (последнее обновление — {At})", sales!.UpdatedAt);
        }

        return hold;
    }

    private async Task AlertAsync(SourceLink link, CancellationToken ct)
    {
        var cause = !link.Connected
            ? link.Problem ?? $"{link.Title} не подключён."
            : $"Автоматический обмен с {link.Title} выключен — данные обновляются только кнопкой «Обновить».";
        try
        {
            await sources.AlertAsync(link.Code, cause, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Аналитик: уведомление об отставании {Source} не отправлено", link.Title);
        }
    }
}
