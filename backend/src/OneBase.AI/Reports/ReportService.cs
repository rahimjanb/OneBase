using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.AI.Tools;
using OneBase.Application.Abstractions;
using OneBase.Application.Reports;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;

namespace OneBase.AI.Reports;

/// <summary>Отчёт для чата: ссылка в ответе консультанта (без данных — они загружаются для предпросмотра отдельно).</summary>
public sealed record ReportRef(Guid Id, string Title, int SheetCount, int RowCount);

/// <summary>Отчёт для предпросмотра: листы целиком и когда сформирован.</summary>
public sealed record ReportView(Guid Id, string Title, DateTimeOffset CreatedAt, IReadOnlyList<AiReportSheet> Sheets, IReadOnlyList<string> Sources);

/// <summary>
/// Отчёты AI-консультанта: создание по описанию AI-сотрудника (данные — через ToolExecutor с правами пользователя), предпросмотр
/// и файл Excel. Отчёт виден только автору.
/// </summary>
public sealed class ReportService(IAppDbContext db, ToolExecutor executor, IReportWorkbookWriter writer, IAuditLogger audit, ILogger<ReportService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Вызов create_report от AI-сотрудника: строит отчёт (инструменты данных — с флагом Report: полные списки) и сохраняет его.
    /// Ошибка описания — текст для модели, чтобы она исправила вызов.
    /// </summary>
    public async Task<(ToolResult Result, Guid? Id)> CreateAsync(ToolContext context, JsonElement args, CancellationToken ct)
    {
        if (context.UserId is not { } userId)
        {
            return (ToolResult.Error("Отчёт формируется только от имени пользователя."), null);
        }

        AiReportContent report;
        try
        {
            var dataContext = context with { Report = true };
            report = await ReportSpec.BuildAsync(args, (tool, input, token) => executor.ExecuteAsync(dataContext, tool, input, token), ct);
        }
        catch (ReportSpecException ex)
        {
            return (ToolResult.Error(ex.Message), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Сбой отчёта не должен отнимать у пользователя ответ сотрудника: модель скажет, что файл не получился.
            logger.LogError(ex, "Отчёт консультанта: не удалось собрать отчёт для {Agent}", context.AgentCode);
            return (ToolResult.Error("Отчёт не сформирован из-за внутренней ошибки OneBase — ответь текстом и скажи, что файл не получился."), null);
        }

        var entity = new AiReport
        {
            UserId = userId,
            ConversationId = context.ConversationId,
            AgentCode = context.AgentCode,
            Title = report.Title,
            Content = JsonSerializer.Serialize(report, Json),
            SheetCount = report.Sheets.Count,
            RowCount = report.RowCount,
        };
        db.AiReports.Add(entity);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.Agent, context.AgentCode, "ai.report.created", nameof(AiReport), entity.Id.ToString(),
            new { context.UserId, context.ConversationId, entity.Title, entity.SheetCount, entity.RowCount }, ct);
        return (new ToolResult(true, ReportSpec.Summary(entity.Id, report)), entity.Id);
    }

    /// <summary>Отчёты, сформированные в чате с момента since (за этот вопрос) — для ответа консультанта.</summary>
    public async Task<IReadOnlyList<ReportRef>> CreatedAsync(Guid userId, Guid conversationId, DateTimeOffset since, CancellationToken ct) =>
        await db.AiReports.AsNoTracking()
            .Where(r => r.UserId == userId && r.ConversationId == conversationId && r.CreatedAt >= since)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new ReportRef(r.Id, r.Title, r.SheetCount, r.RowCount))
            .ToListAsync(ct);

    /// <summary>Отчёт для предпросмотра; null — нет такого или он чужой.</summary>
    public async Task<ReportView?> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var entity = await db.AiReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (entity is null)
        {
            return null;
        }

        var report = Read(entity);
        return new ReportView(entity.Id, entity.Title, entity.CreatedAt, report.Sheets, report.Sources);
    }

    /// <summary>Файл Excel отчёта и имя файла; null — нет такого или он чужой.</summary>
    public async Task<(byte[] Content, string FileName)?> WorkbookAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var entity = await db.AiReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        return entity is null ? null : (writer.Write(Read(entity), entity.CreatedAt), FileName(entity.Title));
    }

    private static AiReportContent Read(AiReport entity) =>
        JsonSerializer.Deserialize<AiReportContent>(entity.Content, Json) ?? new AiReportContent(entity.Title, [], []);

    /// <summary>Имя файла: название отчёта без кавычек и недопустимых знаков (они — пробел), .xlsx.</summary>
    public static string FileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var name = new string(title.Where(c => c is not ('"' or '«' or '»' or '\'')).Select(c => invalid.Contains(c) ? ' ' : c).ToArray());
        name = string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        name = name.Length == 0 ? "Отчёт OneBase" : name.Length > 100 ? name[..100].TrimEnd() : name;
        return name + ".xlsx";
    }
}
