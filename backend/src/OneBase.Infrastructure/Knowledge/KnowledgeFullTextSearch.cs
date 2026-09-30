using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Knowledge;

/// <summary>
/// Полнотекстовый поиск PostgreSQL по фрагментам базы знаний: конфигурация russian (со стеммингом),
/// websearch-синтаксис запроса; если по всем словам ничего нет — хотя бы по одному из них.
/// </summary>
internal sealed class KnowledgeFullTextSearch(OneBaseDbContext db) : IKnowledgeFullTextSearch
{
    public async Task<IReadOnlyList<FullTextMatch>> SearchAsync(string query, IReadOnlyCollection<Guid> documentIds, int limit, CancellationToken ct = default)
    {
        if (documentIds.Count == 0 || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var ids = documentIds.ToArray();
        var all = await RunAsync("websearch_to_tsquery('russian', {0})", query, ids, limit, ct);
        if (all.Count > 0)
        {
            return all;
        }

        // Ни одного фрагмента со всеми словами — ищем по любому слову (длиннее двух букв).
        var words = query.Split((char[])[' ', ',', '.', '?', '!', ';', ':', '"', '«', '»', '(', ')', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .Select(w => w.Replace("'", string.Empty).Replace("\\", string.Empty))
            .Distinct()
            .Take(12)
            .ToList();
        return words.Count == 0 ? [] : await RunAsync("to_tsquery('russian', {0})", string.Join(" | ", words.Select(w => $"'{w}'")), ids, limit, ct);
    }

    private async Task<IReadOnlyList<FullTextMatch>> RunAsync(string tsQuery, string query, Guid[] ids, int limit, CancellationToken ct)
    {
        // {0} — текст запроса, {1} — документы, {2} — лимит (параметры SqlQueryRaw); tsQuery — одна из двух функций выше.
        var sql = $$"""
            select c."Id" "ChunkId", ts_rank_cd(to_tsvector('russian', c."Content"), q)::float8 "Rank"
            from ai."KnowledgeChunks" c, {{tsQuery}} q
            where c."DocumentId" = any({1}) and to_tsvector('russian', c."Content") @@ q
            order by 2 desc
            limit {2}
            """;
        var rows = await db.Database.SqlQueryRaw<Row>(sql, query, ids, limit).ToListAsync(ct);
        return rows.Select(r => new FullTextMatch(r.ChunkId, r.Rank)).ToList();
    }

    private sealed class Row
    {
        public Guid ChunkId { get; set; }
        public double Rank { get; set; }
    }
}
