namespace OneBase.Application.Abstractions;

public sealed record VectorPoint(Guid Id, float[] Vector, IReadOnlyDictionary<string, string> Payload);

public sealed record VectorMatch(Guid Id, float Score, IReadOnlyDictionary<string, string> Payload);

/// <summary>Векторное хранилище (Qdrant) для RAG и семантической памяти агентов.</summary>
public interface IVectorStore
{
    Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken cancellationToken = default);
    Task UpsertAsync(string collection, IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VectorMatch>> SearchAsync(string collection, float[] vector, int limit, CancellationToken cancellationToken = default);
}
