using Minio;
using Minio.DataModel.Args;
using OneBase.Application.Abstractions;

namespace OneBase.Infrastructure.Storage;

public sealed class MinioOptions
{
    public const string Section = "Minio";

    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "onebase-files";
    public bool UseSsl { get; set; }
}

internal sealed class MinioFileStorage(IMinioClient client, MinioOptions options) : IFileStorage
{
    private volatile bool _bucketReady;

    public async Task PutAsync(string objectKey, Stream content, long sizeBytes, string contentType, CancellationToken cancellationToken = default)
    {
        await EnsureBucketAsync(cancellationToken);
        await client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(options.Bucket)
            .WithObject(objectKey)
            .WithStreamData(content)
            .WithObjectSize(sizeBytes)
            .WithContentType(contentType), cancellationToken);
    }

    public async Task DownloadAsync(string objectKey, Stream destination, CancellationToken cancellationToken = default)
    {
        await client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(options.Bucket)
            .WithObject(objectKey)
            .WithCallbackStream((stream, ct) => stream.CopyToAsync(destination, ct)), cancellationToken);
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default) =>
        client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(options.Bucket).WithObject(objectKey), cancellationToken);

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (_bucketReady)
        {
            return;
        }

        var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(options.Bucket), cancellationToken);
        if (!exists)
        {
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(options.Bucket), cancellationToken);
        }

        _bucketReady = true;
    }
}
