using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
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

        async Task Emit(ChatEvent e)
        {
            if (clientGone.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var json = JsonSerializer.Serialize(e.Data, ConsultantChatService.Json);
                await Response.WriteAsync($"event: {e.Type}\ndata: {json}\n\n", clientGone);
                await Response.Body.FlushAsync(clientGone);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException)
            {
                // браузер ушёл — ответ всё равно сохранится в чат
            }
        }

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
    }
}
