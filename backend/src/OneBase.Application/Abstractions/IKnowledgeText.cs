namespace OneBase.Application.Abstractions;

/// <summary>Извлечение текста из загруженного документа для базы знаний AI.</summary>
public interface IDocumentTextExtractor
{
    /// <summary>Расширения, которые умеет читать: ".txt", ".xlsx"…</summary>
    IReadOnlyCollection<string> Extensions { get; }

    /// <summary>Текст документа. NotSupportedException — формат не поддерживается (сообщение — для пользователя).</summary>
    Task<string> ExtractAsync(Stream content, string fileName, CancellationToken cancellationToken = default);
}

public sealed record FullTextMatch(Guid ChunkId, double Rank);

/// <summary>Полнотекстовый поиск по фрагментам документов базы знаний (PostgreSQL).</summary>
public interface IKnowledgeFullTextSearch
{
    Task<IReadOnlyList<FullTextMatch>> SearchAsync(string query, IReadOnlyCollection<Guid> documentIds, int limit, CancellationToken cancellationToken = default);
}
