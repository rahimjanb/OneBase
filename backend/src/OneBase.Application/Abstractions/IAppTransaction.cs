namespace OneBase.Application.Abstractions;

/// <summary>Транзакция базы: CommitAsync — зафиксировать; закрытие без него — откат.</summary>
public interface IAppTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
}
