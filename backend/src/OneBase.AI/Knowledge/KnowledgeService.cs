using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneBase.AI.Gateway;
using OneBase.AI.Llm;
using OneBase.AI.Providers;
using OneBase.AI.Security;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;
using OneBase.Domain.AI;

namespace OneBase.AI.Knowledge;

/// <summary>Ошибка загрузки или поиска — текст для пользователя.</summary>
public sealed class KnowledgeException(string message) : Exception(message);

public sealed record KnowledgeHit(Guid DocumentId, string DocumentTitle, int ChunkIndex, string Text, double Score);

public sealed record KnowledgeDocumentContent(AiKnowledgeDocument Document, IReadOnlyList<string> Chunks);

/// <summary>
/// База знаний AI: документы разбиваются на фрагменты; поиск — полнотекстовый (PostgreSQL) и, если выбрана модель эмбеддингов,
/// семантический (Qdrant), результаты объединяются. Пользователь находит только документы, которые ему доступны.
/// </summary>
public sealed partial class KnowledgeService(
    IAppDbContext db,
    IFileStorage files,
    IDocumentTextExtractor extractor,
    IKnowledgeFullTextSearch fullText,
    IVectorStore vectors,
    IAiGateway gateway,
    IAiSettingsSource settings,
    IUserPermissions permissions,
    ILogger<KnowledgeService> logger)
{
    public const long MaxBytes = 20 * 1024 * 1024;
    private const string Collection = "onebase_knowledge";
    private const int ChunkSize = 1200;
    private const int EmbeddingBatch = 64;

    public IReadOnlyCollection<string> Extensions => extractor.Extensions;

    public async Task<AiKnowledgeDocument> UploadAsync(
        Stream content, string fileName, string contentType, string? title, string? departmentCode, string? requiredPermission, Guid userId, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        if (buffer.Length == 0)
        {
            throw new KnowledgeException("Файл пустой.");
        }

        if (buffer.Length > MaxBytes)
        {
            throw new KnowledgeException($"Файл больше {MaxBytes / 1024 / 1024} МБ.");
        }

        var bytes = buffer.ToArray();
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var duplicate = await db.AiKnowledgeDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Sha256 == sha && d.ArchivedAt == null, ct);
        if (duplicate is not null)
        {
            throw new KnowledgeException($"Этот файл уже есть в базе знаний: «{duplicate.Title}».");
        }

        string text;
        try
        {
            text = await extractor.ExtractAsync(new MemoryStream(bytes), fileName, ct);
        }
        catch (NotSupportedException ex)
        {
            throw new KnowledgeException(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "База знаний: не удалось прочитать {FileName}", fileName);
            throw new KnowledgeException("Не удалось прочитать файл — возможно, он повреждён или защищён паролем.");
        }

        var chunks = Chunk(text);
        if (chunks.Count == 0)
        {
            throw new KnowledgeException("В файле нет текста.");
        }

        var document = new AiKnowledgeDocument
        {
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(fileName) : title.Trim(),
            FileName = Path.GetFileName(fileName),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = bytes.Length,
            ObjectKey = string.Empty,
            Sha256 = sha,
            DepartmentCode = string.IsNullOrWhiteSpace(departmentCode) ? null : departmentCode,
            RequiredPermission = string.IsNullOrWhiteSpace(requiredPermission) ? null : requiredPermission,
            UploadedById = userId,
            Chunks = chunks.Count,
            Characters = chunks.Sum(c => c.Length),
        };
        document.ObjectKey = $"ai-knowledge/{document.Id:N}/{SafeName().Replace(document.FileName, "_")}";

        await files.PutAsync(document.ObjectKey, new MemoryStream(bytes), bytes.Length, document.ContentType, ct);

        var rows = chunks.Select((c, i) => new AiKnowledgeChunk { DocumentId = document.Id, Index = i, Content = c }).ToList();
        document.Status = KnowledgeDocumentStatus.Indexed;
        db.AiKnowledgeDocuments.Add(document);
        db.AiKnowledgeChunks.AddRange(rows);
        await db.SaveChangesAsync(ct);

        await TryEmbedAsync(document, rows, userId, ct);
        return document;
    }

    /// <summary>Строит эмбеддинги для документов, у которых их нет или они построены другой моделью. Возвращает число документов.</summary>
    public async Task<int> EmbedPendingAsync(Guid userId, CancellationToken ct)
    {
        var model = (await settings.GetSettingsAsync(ct)).Embedding
            ?? throw new KnowledgeException("Модель эмбеддингов не выбрана в «Настройки → AI → Общие».");
        var key = model.ToString();
        var pending = await db.AiKnowledgeDocuments
            .Where(d => d.ArchivedAt == null && d.Status == KnowledgeDocumentStatus.Indexed && d.EmbeddingModel != key)
            .ToListAsync(ct);
        var done = 0;
        foreach (var document in pending)
        {
            var rows = await db.AiKnowledgeChunks.AsNoTracking().Where(c => c.DocumentId == document.Id).OrderBy(c => c.Index).ToListAsync(ct);
            if (await TryEmbedAsync(document, rows, userId, ct))
            {
                done++;
            }
        }

        return done;
    }

    /// <summary>Эмбеддинги документа в Qdrant. Ошибка не мешает полнотекстовому поиску — она записывается в документ.</summary>
    private async Task<bool> TryEmbedAsync(AiKnowledgeDocument document, IReadOnlyList<AiKnowledgeChunk> rows, Guid userId, CancellationToken ct)
    {
        var model = (await settings.GetSettingsAsync(ct)).Embedding;
        if (model is null)
        {
            return false;
        }

        try
        {
            for (var i = 0; i < rows.Count; i += EmbeddingBatch)
            {
                var batch = rows.Skip(i).Take(EmbeddingBatch).ToList();
                var (embedded, _) = await gateway.EmbedAsync(
                    batch.Select(c => $"{document.Title}\n{c.Content}").ToList(),
                    new AiCallContext(userId, null, null, "knowledge.index"), ct);
                var collection = CollectionFor(embedded[0].Length);
                await vectors.EnsureCollectionAsync(collection, embedded[0].Length, ct);
                await vectors.UpsertAsync(collection, batch.Select((c, j) => new VectorPoint(c.Id, embedded[j], new Dictionary<string, string>
                {
                    ["documentId"] = document.Id.ToString(),
                    ["chunk"] = c.Index.ToString(),
                })).ToList(), ct);
            }

            document.EmbeddingModel = model.ToString();
            document.Error = null;
        }
        catch (Exception ex) when (ex is AiProviderException or LlmNotConfiguredException or KnowledgeException)
        {
            document.Error = $"Семантический поиск недоступен: {ex.Message}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "База знаний: эмбеддинги документа {DocumentId} не построены", document.Id);
            document.Error = "Семантический поиск недоступен: векторное хранилище не ответило. Работает полнотекстовый поиск.";
        }

        db.AiKnowledgeDocuments.Update(document);
        await db.SaveChangesAsync(ct);
        return document.EmbeddingModel == model.ToString();
    }

    /// <summary>Документы, которые пользователь может видеть: отдел документа, право документа, «Настройки AI» видят всё.</summary>
    public async Task<IReadOnlySet<Guid>> AllowedDocumentsAsync(Guid userId, CancellationToken ct)
    {
        var own = await permissions.GetAsync(userId, ct);
        if (!own.Contains(Permissions.FilesRead) && !own.Contains(Permissions.AiSettingsManage))
        {
            return new HashSet<Guid>();
        }

        var department = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Department != null ? u.Department.Code : null).FirstOrDefaultAsync(ct);
        var everything = own.Contains(Permissions.AiSettingsManage);
        var docs = await db.AiKnowledgeDocuments.AsNoTracking()
            .Where(d => d.ArchivedAt == null && d.Status == KnowledgeDocumentStatus.Indexed)
            .Where(d => everything || d.DepartmentCode == null || d.DepartmentCode == department)
            .Select(d => new { d.Id, d.RequiredPermission })
            .ToListAsync(ct);
        return docs.Where(d => d.RequiredPermission is null || own.Contains(d.RequiredPermission)).Select(d => d.Id).ToHashSet();
    }

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(Guid userId, string query, int limit, CancellationToken ct)
    {
        var allowed = await AllowedDocumentsAsync(userId, ct);
        if (allowed.Count == 0 || string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        // Reciprocal Rank Fusion: место в каждом списке даёт 1 / (60 + место).
        var scores = new Dictionary<Guid, double>();
        void Add(IEnumerable<Guid> ranked)
        {
            var place = 0;
            foreach (var id in ranked)
            {
                scores[id] = scores.GetValueOrDefault(id) + 1.0 / (60 + ++place);
            }
        }

        Add((await fullText.SearchAsync(query, allowed, 20, ct)).Select(m => m.ChunkId));
        Add(await VectorSearchAsync(userId, query, allowed, ct));

        var top = scores.OrderByDescending(s => s.Value).Take(limit).ToList();
        if (top.Count == 0)
        {
            return [];
        }

        var ids = top.Select(t => t.Key).ToList();
        var chunks = await db.AiKnowledgeChunks.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        var docIds = chunks.Select(c => c.DocumentId).Distinct().ToList();
        var titles = await db.AiKnowledgeDocuments.AsNoTracking().Where(d => docIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Title, ct);
        return top
            .Select(t => chunks.FirstOrDefault(c => c.Id == t.Key) is { } c && allowed.Contains(c.DocumentId)
                ? new KnowledgeHit(c.DocumentId, titles.GetValueOrDefault(c.DocumentId) ?? "Документ", c.Index, c.Content, Math.Round(t.Value, 4))
                : null)
            .OfType<KnowledgeHit>()
            .ToList();
    }

    private async Task<IReadOnlyList<Guid>> VectorSearchAsync(Guid userId, string query, IReadOnlySet<Guid> allowed, CancellationToken ct)
    {
        var model = (await settings.GetSettingsAsync(ct)).Embedding;
        if (model is null || !await db.AiKnowledgeDocuments.AnyAsync(d => d.EmbeddingModel == model.ToString() && d.ArchivedAt == null, ct))
        {
            return [];
        }

        try
        {
            var (vector, _) = await gateway.EmbedAsync([query], new AiCallContext(userId, null, null, "knowledge.search"), ct);
            var matches = await vectors.SearchAsync(CollectionFor(vector[0].Length), vector[0], 60, ct);
            return matches
                .Where(m => m.Payload.TryGetValue("documentId", out var d) && Guid.TryParse(d, out var id) && allowed.Contains(id))
                .Select(m => m.Id)
                .Take(20)
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("База знаний: семантический поиск недоступен ({Error}), используется полнотекстовый", ex.Message);
            return [];
        }
    }

    public async Task<KnowledgeDocumentContent?> GetForUserAsync(Guid userId, Guid id, CancellationToken ct)
    {
        if (!(await AllowedDocumentsAsync(userId, ct)).Contains(id))
        {
            return null;
        }

        var document = await db.AiKnowledgeDocuments.AsNoTracking().FirstAsync(d => d.Id == id, ct);
        var chunks = await db.AiKnowledgeChunks.AsNoTracking().Where(c => c.DocumentId == id).OrderBy(c => c.Index).Select(c => c.Content).ToListAsync(ct);
        return new KnowledgeDocumentContent(document, chunks);
    }

    private static string CollectionFor(int dimensions) => $"{Collection}_{dimensions}";

    /// <summary>
    /// Фрагменты ~1200 символов по границам абзацев и строк; длинная строка режется по предложениям.
    /// Короткий последний кусок фрагмента повторяется в начале следующего, чтобы не терять контекст на стыке.
    /// </summary>
    internal static IReadOnlyList<string> Chunk(string text, int size = ChunkSize)
    {
        var normalized = ManyBlankLines().Replace(text.Replace("\r\n", "\n").Replace('\r', '\n'), "\n\n").Trim();
        var pieces = new List<string>();
        foreach (var paragraph in normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (paragraph.Length <= size)
            {
                pieces.Add(paragraph);
                continue;
            }

            foreach (var line in paragraph.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.Length <= size)
                {
                    pieces.Add(line);
                    continue;
                }

                var sentence = new StringBuilder();
                foreach (var part in SentenceEnd().Split(line))
                {
                    if (sentence.Length + part.Length > size && sentence.Length > 0)
                    {
                        pieces.Add(sentence.ToString().Trim());
                        sentence.Clear();
                    }

                    for (var start = 0; start < part.Length; start += size)
                    {
                        var slice = part.Substring(start, Math.Min(size, part.Length - start));
                        if (slice.Length == size)
                        {
                            pieces.Add(slice.Trim());
                        }
                        else
                        {
                            sentence.Append(slice).Append(' ');
                        }
                    }
                }

                if (sentence.Length > 0)
                {
                    pieces.Add(sentence.ToString().Trim());
                }
            }
        }

        var chunks = new List<string>();
        var current = new StringBuilder();
        string? last = null;
        foreach (var piece in pieces.Where(p => p.Length > 0))
        {
            if (current.Length > 0 && current.Length + piece.Length + 1 > size)
            {
                chunks.Add(current.ToString());
                current.Clear();
                if (last is { Length: <= 200 } && last.Length + piece.Length + 1 <= size)
                {
                    current.Append(last).Append('\n');
                }
            }

            current.Append(piece).Append('\n');
            last = piece;
        }

        if (current.Length > 0)
        {
            chunks.Add(current.ToString().TrimEnd());
        }

        return chunks.Select(c => c.TrimEnd()).Where(c => c.Length > 0).ToList();
    }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyBlankLines();

    [GeneratedRegex(@"(?<=[.!?…])\s+")]
    private static partial Regex SentenceEnd();

    [GeneratedRegex(@"[^\w.\-]+")]
    private static partial Regex SafeName();
}
