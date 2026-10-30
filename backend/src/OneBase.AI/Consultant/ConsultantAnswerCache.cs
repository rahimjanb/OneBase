using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.AI.Grounding;
using OneBase.AI.Knowledge;
using OneBase.AI.Reports;
using OneBase.AI.Security;
using OneBase.AI.Tools;
using OneBase.Application.Abstractions;
using OneBase.Application.Sales;

namespace OneBase.AI.Consultant;

/// <summary>Кэш ответов консультанта: сколько минут хранится ответ; 0 — выключен. Настройка Ai:AnswerCache:Minutes.</summary>
public sealed class AnswerCacheOptions
{
    public const string Section = "Ai:AnswerCache";

    public int Minutes { get; init; } = 30;
}

/// <summary>Ответ из кэша: текст и подробности (сотрудники, источники, проверка чисел) — как у исходного ответа.</summary>
public sealed record CachedConsultantAnswer(string Content, string Details, DateTimeOffset AnsweredAt);

/// <summary>
/// Повторный такой же вопрос того же пользователя — ответ без вызовов модели, пока данные не обновились. Кэшируется только то, что
/// однозначно определяется вопросом и данными: первый вопрос чата (без истории), без отчёта Excel и перепроверки, ответ по данным продаж и
/// поля, которые обновляются синхронизацией Linko, Google Sheets и правками данных. Ключ — пользователь, его права и отдел, вопрос, канал,
/// дата по Ташкенту, настройки AI и сотрудников и версия данных (последняя успешная синхронизация Linko, запуск источников Google Sheets,
/// версия кэша продаж). Обновились данные — ключ другой, старый ответ не используется. Пользователи и их права не смешиваются.
/// </summary>
public sealed partial class ConsultantAnswerCache(
    IAppDbContext db,
    ISharedCache cache,
    IUserPermissions permissions,
    IToolRegistry tools,
    SalesCacheSignal salesSignal,
    AnswerCacheOptions options,
    TimeProvider time,
    ILogger<ConsultantAnswerCache> logger)
{
    /// <summary>Источники, данные которых обновляются только синхронизацией и правками, меняющими версию данных: ответ по ним можно повторить.</summary>
    private static readonly string[] VersionedSources = ["sales.", "field.", "finance.", "supply."];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool Enabled => options.Minutes > 0;

    /// <summary>Ключ кэша для вопроса; null — вопрос не кэшируется (продолжение разговора, отчёт Excel, кэш выключен).</summary>
    public async Task<string?> KeyAsync(Guid userId, string question, int historyCount, ConsultantChannel? channel, CancellationToken ct)
    {
        if (!Enabled || historyCount > 0 || ReportIntent.Requested(question) || Normalize(question) is not { Length: > 0 } normalized)
        {
            return null;
        }

        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.DepartmentId, u.UpdatedAt }).FirstOrDefaultAsync(ct);
        if (user is null)
        {
            return null;
        }

        var rights = string.Join(",", (await permissions.GetAsync(userId, ct)).Order(StringComparer.Ordinal));
        var linko = await db.LinkoSyncStates.AsNoTracking().MaxAsync(s => s.LastSuccessAt, ct);
        var sheets = await db.DataSourceBindings.AsNoTracking().MaxAsync(b => (DateTimeOffset?)(b.LastRunAt ?? b.UpdatedAt ?? b.CreatedAt), ct);
        var settings = await db.AiSettings.AsNoTracking().Select(s => s.UpdatedAt).FirstOrDefaultAsync(ct);
        var agents = await db.AiAgents.AsNoTracking().MaxAsync(a => a.UpdatedAt, ct);
        var today = time.GetUtcNow().ToOffset(TimeSpan.FromHours(5)).ToString("yyyy-MM-dd");
        var parts = new[]
        {
            userId.ToString("N"), user.DepartmentId?.ToString("N") ?? "-", user.UpdatedAt?.UtcTicks.ToString() ?? "-", Hash(rights),
            channel is null ? "web" : $"{channel.Name}|{channel.Model}|{channel.AgentModel}|{channel.MaxOutputTokens}|{channel.AllowReports}|{Hash(channel.Instructions ?? "")}",
            today, linko?.UtcTicks.ToString() ?? "-", sheets?.UtcTicks.ToString() ?? "-", salesSignal.Version, settings?.UtcTicks.ToString() ?? "-",
            agents?.UtcTicks.ToString() ?? "-", normalized,
        };
        return "ai:answer:" + Hash(string.Join("\n", parts));
    }

    public async Task<CachedConsultantAnswer?> GetAsync(string key, CancellationToken ct)
    {
        try
        {
            return await cache.GetAsync(key, ct) is { } json ? JsonSerializer.Deserialize<CachedConsultantAnswer>(json, Json) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Консультант: не удалось прочитать кэш ответа");
            return null;
        }
    }

    /// <summary>Сохраняет ответ, если его можно повторить: сотрудники ответили по данным с версией, без ошибок, отчётов и неподтверждённых чисел.</summary>
    public async Task StoreAsync(string key, string content, ConsultantDetails details, string detailsJson, CancellationToken ct)
    {
        if (!Cacheable(details, tools))
        {
            return;
        }

        try
        {
            var value = JsonSerializer.Serialize(new CachedConsultantAnswer(content, detailsJson, time.GetUtcNow()), Json);
            await cache.SetAsync(key, value, TimeSpan.FromMinutes(options.Minutes), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Консультант: не удалось сохранить ответ в кэш");
        }
    }

    /// <summary>
    /// Можно ли повторить ответ: были сотрудники, все завершили работу, ничего не помечено как неподтверждённое, нет отчётов и перепроверки,
    /// и каждый использованный инструмент с данными берёт их из источника, который обновляется синхронизацией (продажи, поле, финансы, снабжение).
    /// Задачи отделов, Telegram и документы меняются без синхронизации — такие ответы не кэшируются.
    /// </summary>
    internal static bool Cacheable(ConsultantDetails details, IToolRegistry registry)
    {
        if (details.Agents.Count == 0 || details.Reports is { Count: > 0 } || details.Correction is not null || details.Grounding is { Warned: true })
        {
            return false;
        }

        foreach (var agent in details.Agents)
        {
            if (agent.Status is not ("completed" or "partial") || agent.Unverified is { Count: > 0 } || agent.Error is not null)
            {
                return false;
            }

            foreach (var name in agent.ToolsUsed)
            {
                var tool = registry.Find(name);
                if (tool is null)
                {
                    // Calculator, источник истины, страницы результата и отчёт — не инструменты реестра: новых данных они не читают.
                    if (name is Calculator.ToolName or SourceOfTruthTool.ToolName or ToolResultPager.ToolName)
                    {
                        continue;
                    }

                    return false;
                }

                if (tool.Source is not { } source || !VersionedSources.Any(p => source.StartsWith(p, StringComparison.Ordinal)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Вопрос без различий в регистре, пробелах и знаках в конце: «Как продажи сегодня?» и «как продажи  сегодня» — один вопрос.</summary>
    internal static string Normalize(string question) => Spaces().Replace(question.Trim().TrimEnd('?', '!', '.', ' ').ToLowerInvariant().Replace('ё', 'е'), " ");

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..32];

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
