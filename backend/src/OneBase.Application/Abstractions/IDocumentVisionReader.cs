namespace OneBase.Application.Abstractions;

/// <summary>
/// Что модель прочитала в документе. DocumentType — resume, certificate (диплом, сертификат, справка), id_document (паспорт, ID-карта — текст
/// не переписывается), other. Error — почему не прочитано (для пользователя); тогда Text — null.
/// </summary>
public sealed record VisionReading(string? Text, string DocumentType, string? Error, string? Model = null)
{
    public static VisionReading Failed(string error) => new(null, "other", error);
}

/// <summary>
/// Чтение фото и сканов (PDF без текстового слоя) моделью AI — фото резюме, скан диплома. Реализация — OneBase.AI (Claude через шлюз; платный
/// вызов, учитывается в расходах AI). Без регистрации фото не распознаются.
/// </summary>
public interface IDocumentVisionReader
{
    /// <summary>Расширения, которые модель читает: .jpg, .jpeg, .png, .webp, .pdf.</summary>
    bool Supports(string extension);

    Task<VisionReading> ReadAsync(byte[] content, string fileName, string purpose, CancellationToken ct = default);
}
