using System.Text.Json;
using OneBase.AI.Consultant;
using OneBase.AI.Knowledge;
using OneBase.Application.Security;

namespace OneBase.AI.Tools.Data;

internal sealed class SearchKnowledgeTool(KnowledgeService knowledge) : DataTool
{
    public override string Name => "search_knowledge";
    public override string Title => "Поиск по базе знаний";
    public override string Source => KnowledgeSources.Documents;
    public override string RequiredPermission => Permissions.FilesRead;

    public override string Description =>
        "Поиск по документам базы знаний компании (регламенты, отчёты, выгрузки Excel и CSV), доступным пользователю: " +
        "возвращает подходящие фрагменты с названием документа. Цитируй фрагменты и называй документ как источник.";

    public override JsonElement InputSchema { get; } = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            query = new { type = "string", description = "Что искать — ключевые слова или вопрос." },
            limit = new { type = "integer", description = "Сколько фрагментов вернуть (по умолчанию 6, до 12)." },
        },
        required = new[] { "query" },
    });

    protected override async Task<ToolResult> RunAsync(ToolContext context, ToolArgs args, CancellationToken ct)
    {
        var query = args.Str("query") ?? throw new ToolArgumentException("Нужен аргумент query — что искать.");
        if (context.UserId is not { } userId)
        {
            return ToolResult.Error("Поиск по базе знаний выполняется только от имени пользователя.");
        }

        var hits = await knowledge.SearchAsync(userId, query, args.Int("limit", 1, 12) ?? 6, ct);
        if (hits.Count == 0)
        {
            return Data(new { Query = query, Found = 0, Note = "В доступных пользователю документах базы знаний ничего не найдено." });
        }

        var sources = hits
            .GroupBy(h => h.DocumentId)
            .Select(g => new DataSource($"База знаний → {g.First().DocumentTitle}", null, $"/knowledge/{g.Key}"))
            .ToArray();
        return Data(new
        {
            Query = query,
            Found = hits.Count,
            Fragments = hits.Select(h => new { Document = h.DocumentTitle, Fragment = h.ChunkIndex + 1, h.Text }),
        }, sources);
    }
}
