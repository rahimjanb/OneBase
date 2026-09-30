using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OneBase.AI.Consultant;
using OneBase.AI.Gateway;
using OneBase.Api.Auth;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

/// <summary>Раздел «Консультант»: чат с главным AI-консультантом и история чатов пользователя.</summary>
[ApiController]
[Route("api/ai")]
[HasPermission(Permissions.AgentsRun)]
public sealed class ConsultantController(
    ConsultantChatService chats,
    IAiGateway gateway,
    IHostApplicationLifetime lifetime,
    ILogger<ConsultantController> logger) : ControllerBase
{
    /// <summary>Сколько консультант может думать над одним вопросом.</summary>
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromMinutes(6);

    /// <summary>Как часто слать «пульс», пока нет событий: меньше таймаутов nginx (300 с) и Cloudflare (100 с).</summary>
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);

    public sealed record ChatRequest(Guid? ConversationId, string Message);

    public sealed record RenameRequest(string Title);

    /// <summary>Готов ли AI отвечать — для экрана чата.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var readiness = await gateway.GetReadinessAsync(ct);
        return Ok(new { readiness.Ready, readiness.Message });
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> Conversations(CancellationToken ct) => Ok(await chats.ListAsync(User.GetUserId(), ct));

    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> Conversation(Guid id, CancellationToken ct) =>
        await chats.GetAsync(User.GetUserId(), id, ct) is { } view ? Ok(view) : NotFound();

    [HttpPatch("conversations/{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, RenameRequest request, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(request.Title) ? BadRequest(new { error = "Название не может быть пустым." })
        : await chats.RenameAsync(User.GetUserId(), id, request.Title, ct) ? NoContent() : NotFound();

    /// <summary>Убрать чат из истории (сообщения остаются в журнале AI).</summary>
    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        await chats.ArchiveAsync(User.GetUserId(), id, ct) ? NoContent() : NotFound();

    /// <summary>
    /// Вопрос консультанту. Ответ — поток text/event-stream: start, progress…, done | error.
    /// Если браузер закрыли, ответ всё равно дорабатывается и сохраняется в чат.
    /// </summary>
    [HttpPost("chat")]
    [EnableRateLimiting("ai")]
    public async Task Chat(ChatRequest request)
    {
        var message = request.Message?.Trim() ?? string.Empty;
        if (message.Length == 0 || message.Length > ConsultantChatService.MaxQuestionLength)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = $"Вопрос — от 1 до {ConsultantChatService.MaxQuestionLength} символов." });
            return;
        }

        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";

        var clientGone = HttpContext.RequestAborted;
        using var work = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
        work.CancelAfter(AnswerTimeout);

        // Запись в поток — по одной: события и «пульс» идут из разных задач.
        using var writeLock = new SemaphoreSlim(1, 1);
        async Task Write(string text)
        {
            if (clientGone.IsCancellationRequested)
            {
                return;
            }

            await writeLock.WaitAsync(clientGone);
            try
            {
                await Response.WriteAsync(text, clientGone);
                await Response.Body.FlushAsync(clientGone);
            }
            finally
            {
                writeLock.Release();
            }
        }

        async Task Emit(ChatEvent e)
        {
            try
            {
                await Write($"event: {e.Type}\ndata: {JsonSerializer.Serialize(e.Data, ConsultantChatService.Json)}\n\n");
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException)
            {
                // браузер ушёл — ответ всё равно сохранится в чат
            }
        }

        // «Пульс» — комментарий SSE, браузер его пропускает. Пока модель думает, событий может не быть больше минуты,
        // а nginx и Cloudflare закрывают молчащее соединение (Cloudflare — через 100 с).
        using var stopPing = new CancellationTokenSource();
        var ping = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(PingInterval);
            try
            {
                while (await timer.WaitForNextTickAsync(stopPing.Token))
                {
                    await Write(": ping\n\n");
                }
            }
            catch (Exception)
            {
                // ответ готов, браузер ушёл или запись не удалась — «пульс» не должен влиять на ответ
            }
        });

        try
        {
            await chats.SendAsync(User.GetUserId(), request.ConversationId, message, Emit, work.Token);
        }
        catch (KeyNotFoundException ex)
        {
            await Emit(new ChatEvent("error", new { error = ex.Message }));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Консультант: ошибка чата");
            await Emit(new ChatEvent("error", new { error = "Внутренняя ошибка OneBase. Подробности — в журнале сервера." }));
        }
        finally
        {
            await stopPing.CancelAsync();
            await ping;
        }
    }
}
