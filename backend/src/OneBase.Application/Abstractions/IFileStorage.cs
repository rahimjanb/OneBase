namespace OneBase.Application.Abstractions;

/// <summary>Объектное хранилище содержимого файлов (MinIO). Метаданные — в PostgreSQL.</summary>
public interface IFileStorage
{
    Task PutAsync(string objectKey, Stream content, long sizeBytes, string contentType, CancellationToken cancellationToken = default);
    Task DownloadAsync(string objectKey, Stream destination, CancellationToken cancellationToken = default);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default);
}
