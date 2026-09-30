using OneBase.Domain.Common;

namespace OneBase.Domain.AI;

public enum KnowledgeDocumentStatus
{
    Indexing,
    Indexed,
    Failed,
}

/// <summary>
/// Документ базы знаний AI: оригинал — в файловом хранилище, текст — во фрагментах (AiKnowledgeChunk).
/// Кто видит: отдел документа (пусто — вся компания) и, если задано, право OneBase.
/// </summary>
public class AiKnowledgeDocument : Entity
{
    public required string Title { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string ObjectKey { get; set; }
    public required string Sha256 { get; set; }

    /// <summary>Отдел, чьи сотрудники видят документ; пусто — вся компания.</summary>
    public string? DepartmentCode { get; set; }

    /// <summary>Право OneBase, без которого документ не виден (например finance.read).</summary>
    public string? RequiredPermission { get; set; }

    public KnowledgeDocumentStatus Status { get; set; } = KnowledgeDocumentStatus.Indexing;
    public string? Error { get; set; }
    public int Chunks { get; set; }
    public int Characters { get; set; }

    /// <summary>Модель, которой построены эмбеддинги фрагментов; пусто — только полнотекстовый поиск.</summary>
    public string? EmbeddingModel { get; set; }

    public Guid UploadedById { get; set; }

    /// <summary>Документ убран из базы знаний: в поиске не участвует, файл и текст сохраняются.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }
}

/// <summary>Фрагмент текста документа — единица поиска и цитирования.</summary>
public class AiKnowledgeChunk : Entity
{
    public Guid DocumentId { get; set; }
    public int Index { get; set; }
    public required string Content { get; set; }
}
