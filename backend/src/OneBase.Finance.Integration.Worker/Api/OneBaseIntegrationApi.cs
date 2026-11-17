using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OneBase.Finance.Integration.Contracts;

namespace OneBase.Finance.Integration.Worker.Api;

/// <summary>Внутренний API OneBase (/api/internal/onec) — всё, что служба пишет и читает в OneBase, идёт только через него.</summary>
public interface IOneBaseIntegrationApi
{
    Task<HeartbeatResponse> HeartbeatAsync(WorkerHeartbeat beat, CancellationToken ct);
    Task CompleteCommandAsync(Guid id, CommandResult result, CancellationToken ct);
    Task<StartRunResponse> StartRunAsync(StartRunRequest request, CancellationToken ct);
    Task<RunProgressResponse> ProgressAsync(Guid runId, RunProgress progress, CancellationToken ct);
    Task FinishRunAsync(Guid runId, FinishRunRequest request, CancellationToken ct);
    Task<ImportBatchResult> ImportAsync(ImportBatchRequest request, bool dryRun, CancellationToken ct);
    Task<ImportedTotals> TotalsAsync(string stream, string organizationId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<Guid> ReportFailureAsync(FailureReport report, CancellationToken ct);
    Task<List<FailureItem>> DueFailuresAsync(int limit, CancellationToken ct);
    Task FailureOutcomeAsync(Guid id, FailureOutcome outcome, CancellationToken ct);
    Task<HistoricalImportState> HistoricalAsync(Guid id, CancellationToken ct);
    Task HistoricalProgressAsync(Guid id, HistoricalProgressRequest request, CancellationToken ct);
}

/// <summary>Ошибка внутреннего API OneBase. 409 — запись невозможна (запуск закрыт, пауза); 401 — неверный ключ службы.</summary>
public sealed class OneBaseApiException(HttpStatusCode? status, string message, bool transient, TimeSpan? retryAfter = null, Exception? inner = null)
    : Exception(message, inner)
{
    public HttpStatusCode? Status { get; } = status;
    public bool Transient { get; } = transient;
    public TimeSpan? RetryAfter { get; } = retryAfter;
    public bool Conflict => Status == HttpStatusCode.Conflict;
    public bool Unauthorized => Status == HttpStatusCode.Unauthorized;
}

public sealed class OneBaseIntegrationApi(HttpClient http) : IOneBaseIntegrationApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Root = "api/internal/onec/";

    public Task<HeartbeatResponse> HeartbeatAsync(WorkerHeartbeat beat, CancellationToken ct) => PostAsync<WorkerHeartbeat, HeartbeatResponse>("heartbeat", beat, ct);

    public Task CompleteCommandAsync(Guid id, CommandResult result, CancellationToken ct) => PostAsync<CommandResult, object?>($"commands/{id}/complete", result, ct);

    public Task<StartRunResponse> StartRunAsync(StartRunRequest request, CancellationToken ct) => PostAsync<StartRunRequest, StartRunResponse>("runs", request, ct);

    public Task<RunProgressResponse> ProgressAsync(Guid runId, RunProgress progress, CancellationToken ct) =>
        PostAsync<RunProgress, RunProgressResponse>($"runs/{runId}/progress", progress, ct);

    public Task FinishRunAsync(Guid runId, FinishRunRequest request, CancellationToken ct) => PostAsync<FinishRunRequest, object?>($"runs/{runId}/finish", request, ct);

    public Task<ImportBatchResult> ImportAsync(ImportBatchRequest request, bool dryRun, CancellationToken ct) =>
        PostAsync<ImportBatchRequest, ImportBatchResult>(dryRun ? "batches?dryRun=true" : "batches", request, ct);

    public Task<ImportedTotals> TotalsAsync(string stream, string organizationId, DateOnly from, DateOnly to, CancellationToken ct) =>
        GetAsync<ImportedTotals>($"totals?stream={Uri.EscapeDataString(stream)}&organizationId={Uri.EscapeDataString(organizationId)}&from={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&to={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}", ct);

    public async Task<Guid> ReportFailureAsync(FailureReport report, CancellationToken ct) =>
        (await PostAsync<FailureReport, JsonElement>("failures", report, ct)).GetProperty("id").GetGuid();

    public Task<List<FailureItem>> DueFailuresAsync(int limit, CancellationToken ct) => GetAsync<List<FailureItem>>($"failures/due?limit={limit}", ct);

    public Task FailureOutcomeAsync(Guid id, FailureOutcome outcome, CancellationToken ct) => PostAsync<FailureOutcome, object?>($"failures/{id}/outcome", outcome, ct);

    public Task<HistoricalImportState> HistoricalAsync(Guid id, CancellationToken ct) => GetAsync<HistoricalImportState>($"historical/{id}", ct);

    public Task HistoricalProgressAsync(Guid id, HistoricalProgressRequest request, CancellationToken ct) =>
        PostAsync<HistoricalProgressRequest, object?>($"historical/{id}/progress", request, ct);

    private async Task<TOut> PostAsync<TIn, TOut>(string path, TIn body, CancellationToken ct)
    {
        using var response = await SendAsync(() => http.PostAsJsonAsync(Root + path, body, Json, ct), path, ct);
        return await ReadAsync<TOut>(response, path, ct);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken ct)
    {
        using var response = await SendAsync(() => http.GetAsync(Root + path, ct), path, ct);
        return await ReadAsync<T>(response, path, ct);
    }

    private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send, string path, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (TaskCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new OneBaseApiException(null, $"OneBase /{Clean(path)}: нет ответа (тайм-аут)", true, inner: e);
        }
        catch (HttpRequestException e)
        {
            throw new OneBaseApiException(null, $"OneBase /{Clean(path)}: API OneBase недоступен ({e.HttpRequestError})", true, inner: e);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            string? error = null;
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                error = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
            }
            catch (Exception)
            {
                // не JSON — без текста
            }

            var status = response.StatusCode;
            var transient = status is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
                or HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout;
            var message = status == HttpStatusCode.Unauthorized
                ? "OneBase отклонил ключ службы (401): создайте ключ в «Настройки → Интеграции → Финансы → 1С» и укажите его в ONEC_INTEGRATION_TOKEN"
                : $"OneBase /{Clean(path)}: {(int)status} {error}".Trim();
            throw new OneBaseApiException(status, message, transient, response.Headers.RetryAfter?.Delta);
        }
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, string path, CancellationToken ct)
    {
        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Accepted || response.Content.Headers.ContentLength == 0)
        {
            return default!;
        }

        try
        {
            return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
        }
        catch (JsonException e)
        {
            throw new OneBaseApiException(response.StatusCode, $"OneBase /{Clean(path)}: неожиданный ответ", false, inner: e);
        }
    }

    private static string Clean(string path) => path.Split('?')[0];
}
