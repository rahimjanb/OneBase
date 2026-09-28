using OneBase.Application.Abstractions;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace OneBase.Infrastructure.Vector;

public sealed class QdrantOptions
{
    public const string Section = "Qdrant";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6334;
    public bool UseHttps { get; set; }
    public string? ApiKey { get; set; }
}

internal sealed class QdrantVectorStore(QdrantClient client) : IVectorStore
{
    public async Task EnsureCollectionAsync(string collection, int dimensions, CancellationToken cancellationToken = default)
    {
        if (await client.CollectionExistsAsync(collection, cancellationToken))
        {
            return;
        }

        await client.CreateCollectionAsync(
            collection,
            new VectorParams { Size = (ulong)dimensions, Distance = Distance.Cosine },
            cancellationToken: cancellationToken);
    }

    public async Task UpsertAsync(string collection, IReadOnlyList<VectorPoint> points, CancellationToken cancellationToken = default)
    {
        var structs = points.Select(p =>
        {
            var point = new PointStruct { Id = p.Id, Vectors = p.Vector };
            foreach (var (key, value) in p.Payload)
            {
                point.Payload[key] = value;
            }
            return point;
        }).ToList();

        await client.UpsertAsync(collection, structs, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<VectorMatch>> SearchAsync(string collection, float[] vector, int limit, CancellationToken cancellationToken = default)
    {
        var results = await client.QueryAsync(
            collection,
            query: vector,
            limit: (ulong)limit,
            payloadSelector: true,
            cancellationToken: cancellationToken);

        return results
            .Select(r => new VectorMatch(
                Guid.Parse(r.Id.Uuid),
                r.Score,
                r.Payload.ToDictionary(kv => kv.Key, kv => kv.Value.StringValue)))
            .ToList();
    }
}
