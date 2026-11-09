namespace OneBase.Infrastructure.Recovery;

/// <summary>Запрос во внешнюю систему отклонён режимом изоляции: копия для проверки наружу не ходит.</summary>
public sealed class RecoveryIsolatedException(string host)
    : HttpRequestException($"Копия для проверки (Recovery:Isolated): внешние запросы выключены — {host} не вызывался.")
{
    public string Host { get; } = host;
}

/// <summary>
/// Первый обработчик каждого HttpClient из IHttpClientFactory в режиме изоляции (Linko, Google, Verifix, провайдеры моделей, голос,
/// Web Push, проверка подключения папок): запрос не уходит в сеть, вызывающий код получает HttpRequestException — как при недоступном
/// сервере, с понятным текстом. MinIO и Qdrant — свои клиенты внутри сети Docker, их это не касается.
/// </summary>
internal sealed class IsolationHttpHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new RecoveryIsolatedException(request.RequestUri?.Host ?? "адрес не задан");

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new RecoveryIsolatedException(request.RequestUri?.Host ?? "адрес не задан");
}
