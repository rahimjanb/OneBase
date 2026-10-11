using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OneBase.AI.Consultant;
using OneBase.AI.DataManager;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.Security;
using OneBase.Domain.Audit;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → AI Data Manager»: чат администратора с Claude, загрузка отчётов, планы изменений данных OneBase,
/// их подтверждение, отмена, откат и журнал операций. Права проверяются здесь и ещё раз при применении (sales.manage).
/// </summary>
[ApiController]
[Route("api/data-manager")]
[HasPermission(Permissions.DataManage)]
public sealed class DataManagerController(
    DataManagerChatService chats,
    DataFileService files,
    DataChangeService changes,
    DataManagerOptions options,
    IAuditLogger audit,
    IHostApplicationLifetime lifetime,
    ILogger<DataManagerController> logger) : ControllerBase
{
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);

    public sealed record ChatRequest(Guid? ConversationId, string Message, IReadOnlyList<Guid>? FileIds);

    public sealed record RenameRequest(string Title);

    public sealed record CreateRequest(string? Title);

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(await chats.StatusAsync(ct));

    [HttpGet("conversations")]
    public async Task<IActionResult> Conversations(CancellationToken ct) => Ok(await chats.ListAsync(User.GetUserId(), ct));

    [HttpPost("conversations")]
    public async Task<IActionResult> Create(CreateRequest request, CancellationToken ct) =>
        Ok(await chats.CreateAsync(User.GetUserId(), request.Title, ct));

    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> Conversation(Guid id, CancellationToken ct) =>
        await chats.GetAsync(User.GetUserId(), id, ct) is { } view ? Ok(view) : NotFound(new { error = "Чат не найден." });

    [HttpPatch("conversations/{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, RenameRequest request, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(request.Title) ? BadRequest(new { error = "Название не может быть пустым." })
        : await chats.RenameAsync(User.GetUserId(), id, request.Title, ct) ? NoContent() : NotFound(new { error = "Чат не найден." });

    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        await chats.ArchiveAsync(User.GetUserId(), id, ct) ? NoContent() : NotFound(new { error = "Чат не найден." });

    /// <summary>Загрузить отчёт (.xlsx, .csv): проверка формата и размера, SHA-256, разбор таблиц. Файл видит только загрузивший.</summary>
    [HttpPost("files")]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, [FromForm] Guid? conversationId, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "Файл не выбран или пустой." });
        }

        if (file.Length > options.MaxFileBytes)
        {
            return BadRequest(new { error = $"Файл больше {options.MaxFileBytes / 1024 / 1024} МБ." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var view = await files.UploadAsync(User.GetUserId(), conversationId, file.FileName, stream, ct);
            await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "ai.data.file_uploaded", "data_file", view.Id.ToString(),
                new { view.FileName, view.SizeBytes, view.Sha256, duplicates = view.Duplicates.Count }, ct);
            return Ok(view);
        }
        catch (DataManagerException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Сообщение в чат. Ответ — поток text/event-stream: start, step…, changeset…, done | error.</summary>
    [HttpPost("chat")]
    [EnableRateLimiting("ai")]
    public async Task Chat(ChatRequest request)
    {
        var message = request.Message?.Trim() ?? string.Empty;
        if (message.Length == 0 || message.Length > DataManagerChatService.MaxMessageLength)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = $"Сообщение — от 1 до {DataManagerChatService.MaxMessageLength} символов." });
            return;
        }

        await StreamAsync((emit, ct) => chats.SendAsync(User.GetUserId(), request.ConversationId, message, request.FileIds ?? [], emit, ct));
    }

    [HttpGet("change-sets")]
    public async Task<IActionResult> ChangeSets([FromQuery] Guid? conversationId, [FromQuery] int take = 50, CancellationToken ct = default) =>
        Ok(await changes.ListAsync(conversationId, take, ct));

    [HttpGet("change-sets/{id:guid}")]
    public async Task<IActionResult> ChangeSet(Guid id, CancellationToken ct) =>
        await changes.GetAsync(id, ct) is { } view ? Ok(view) : NotFound(new { error = "План изменений не найден." });

    /// <summary>«Подтвердить»: применить план. Поток событий: changeset, event, затем итог модели (step…, done | error).</summary>
    [HttpPost("change-sets/{id:guid}/apply")]
    [EnableRateLimiting("ai")]
    public Task Apply(Guid id) => StreamAsync(async (emit, ct) =>
    {
        var outcome = await chats.ApplyAsync(User.GetUserId(), id, emit, ct);
        await emit(new ChatEvent("outcome", outcome));
    });

    /// <summary>Откат применённого плана: только строки, которые с тех пор никто не менял.</summary>
    [HttpPost("change-sets/{id:guid}/rollback")]
    [EnableRateLimiting("ai")]
    public Task Rollback(Guid id) => StreamAsync(async (emit, ct) =>
    {
        var outcome = await chats.RollbackAsync(User.GetUserId(), id, emit, ct);
        await emit(new ChatEvent("outcome", outcome));
    });

    [HttpPost("change-sets/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await chats.CancelAsync(User.GetUserId(), id, _ => Task.CompletedTask, ct));
        }
        catch (DataManagerException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("change-sets/{id:guid}/verify")]
    public async Task<IActionResult> Verify(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await changes.VerifyAsync(id, ct));
        }
        catch (DataManagerException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Поток событий SSE с «пульсом» каждые 15 с. Работа продолжается и сохраняется, даже если браузер закрыли
    /// (отменяется только остановкой сервера или таймаутом хода).
    /// </summary>
    private async Task StreamAsync(Func<Func<ChatEvent, Task>, CancellationToken, Task> work)
    {
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";

        var clientGone = HttpContext.RequestAborted;
        using var token = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
        token.CancelAfter(TimeSpan.FromMinutes(options.TurnTimeoutMinutes + 2));

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
                await Write($"event: {e.Type}\ndata: {JsonSerializer.Serialize(e.Data, DataManagerChatService.Json)}\n\n");
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException)
            {
                // браузер ушёл — результат всё равно сохранится в чат
            }
        }

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
                // «пульс» не влияет на результат
            }
        });

        try
        {
            await work(Emit, token.Token);
        }
        catch (Exception ex) when (ex is DataManagerException or KeyNotFoundException)
        {
            await Emit(new ChatEvent("error", new { error = ex.Message }));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "AI Data Manager: ошибка запроса");
            await Emit(new ChatEvent("error", new { error = "Внутренняя ошибка OneBase. Подробности — в журнале сервера." }));
        }
        finally
        {
            await stopPing.CancelAsync();
            await ping;
        }
    }
}
