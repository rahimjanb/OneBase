using Microsoft.AspNetCore.Mvc;
using OneBase.AI.Telegram;
using OneBase.Api.Auth;
using OneBase.Application.Security;
using OneBase.Application.Telegram;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Рассылки»: мастер (аккаунт, текст, файлы и отчёты, получатели из чатов аккаунта), предпросмотр и тест, запуск сразу или по расписанию,
/// пауза, продолжение, отмена, повтор ошибок; статус по каждому получателю.
/// </summary>
[ApiController]
[Route("api/telegram/campaigns")]
[HasPermission(Permissions.TelegramCampaigns)]
public sealed class TelegramCampaignsController(TelegramCampaignService campaigns) : ControllerBase
{
    public sealed record RecipientsRequest(IReadOnlyList<Guid> ChatIds);
    public sealed record ReportRequest(Guid ReportId);
    public sealed record TestRequest(Guid ChatId);

    [HttpGet]
    public Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) => Run(async () => Ok(await campaigns.ListAsync(status, ct)));

    [HttpGet("accounts")]
    public Task<IActionResult> Accounts(CancellationToken ct) => Run(async () => Ok(await campaigns.AccountsAsync(ct)));

    /// <summary>Отчёты текущего пользователя — приложить к рассылке файлом Excel.</summary>
    [HttpGet("reports")]
    public Task<IActionResult> Reports(CancellationToken ct) => Run(async () => Ok(await campaigns.ReportsAsync(User.GetUserId(), ct)));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => Run(async () => Ok(await campaigns.GetAsync(id, ct)));

    [HttpPost]
    public Task<IActionResult> Create(CampaignInput input, CancellationToken ct) => Run(async () => Ok(await campaigns.CreateAsync(input, User.GetUserId(), ct)));

    [HttpPut("{id:guid}")]
    public Task<IActionResult> Update(Guid id, CampaignInput input, CancellationToken ct) =>
        Run(async () => Ok(await campaigns.UpdateAsync(id, input, User.GetUserId(), ct)));

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        Run(async () =>
        {
            await campaigns.DeleteAsync(id, User.GetUserId(), ct);
            return NoContent();
        });

    /// <summary>Чаты аккаунта рассылки (поиск, тип); Added — уже среди получателей, CanSend — аккаунт может туда писать.</summary>
    [HttpGet("{id:guid}/chats")]
    public Task<IActionResult> Chats(Guid id, [FromQuery] string? search, [FromQuery] string? type, CancellationToken ct) =>
        Run(async () => Ok(await campaigns.ChatsAsync(id, search, type, ct)));

    [HttpPost("{id:guid}/chats/sync")]
    public Task<IActionResult> SyncChats(Guid id, CancellationToken ct) => Run(async () => Ok(new { chats = await campaigns.SyncChatsAsync(id, ct) }));

    [HttpPost("{id:guid}/recipients")]
    public Task<IActionResult> AddRecipients(Guid id, RecipientsRequest request, CancellationToken ct) =>
        Run(async () => Ok(await campaigns.AddRecipientsAsync(id, request.ChatIds, ct)));

    [HttpDelete("{id:guid}/recipients/{recipientId:guid}")]
    public Task<IActionResult> RemoveRecipient(Guid id, Guid recipientId, CancellationToken ct) =>
        Run(async () =>
        {
            await campaigns.RemoveRecipientAsync(id, recipientId, ct);
            return NoContent();
        });

    [HttpDelete("{id:guid}/recipients")]
    public Task<IActionResult> ClearRecipients(Guid id, CancellationToken ct) =>
        Run(async () =>
        {
            await campaigns.ClearRecipientsAsync(id, ct);
            return NoContent();
        });

    [HttpPost("{id:guid}/files")]
    [RequestSizeLimit(TelegramRules.MaxAttachmentBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = TelegramRules.MaxAttachmentBytes + 1024 * 1024)]
    public Task<IActionResult> AddFile(Guid id, IFormFile file, CancellationToken ct) =>
        Run(async () =>
        {
            await using var stream = file.OpenReadStream();
            return Ok(await campaigns.AddFileAsync(id, file.FileName, file.ContentType, stream, file.Length, User.GetUserId(), ct));
        });

    [HttpPost("{id:guid}/reports")]
    public Task<IActionResult> AddReport(Guid id, ReportRequest request, CancellationToken ct) =>
        Run(async () => Ok(await campaigns.AddReportAsync(id, request.ReportId, User.GetUserId(), ct)));

    /// <summary>Скачать файл рассылки — ровно тот, что ушёл получателям.</summary>
    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public Task<IActionResult> DownloadAttachment(Guid id, Guid attachmentId, CancellationToken ct) =>
        Run(async () =>
        {
            var file = await campaigns.AttachmentAsync(id, attachmentId, ct);
            return File(file.Content, file.ContentType, file.FileName);
        });

    /// <summary>Повторить как новую: черновик с тем же текстом, файлами и получателями.</summary>
    [HttpPost("{id:guid}/duplicate")]
    public Task<IActionResult> Duplicate(Guid id, CancellationToken ct) => Run(async () => Ok(await campaigns.DuplicateAsync(id, User.GetUserId(), ct)));

    [HttpDelete("{id:guid}/attachments/{attachmentId:guid}")]
    public Task<IActionResult> RemoveAttachment(Guid id, Guid attachmentId, CancellationToken ct) =>
        Run(async () =>
        {
            await campaigns.RemoveAttachmentAsync(id, attachmentId, ct);
            return NoContent();
        });

    [HttpGet("{id:guid}/preview")]
    public Task<IActionResult> Preview(Guid id, CancellationToken ct) => Run(async () => Ok(await campaigns.PreviewAsync(id, ct)));

    /// <summary>Тестовая отправка в один чат аккаунта; статусы рассылки не меняются.</summary>
    [HttpPost("{id:guid}/test")]
    public Task<IActionResult> Test(Guid id, TestRequest request, CancellationToken ct) =>
        Run(async () => Ok(new { sentTo = await campaigns.TestAsync(id, request.ChatId, User.GetUserId(), ct) }));

    [HttpPost("{id:guid}/launch")]
    public Task<IActionResult> Launch(Guid id, CancellationToken ct) => Run(async () => Ok(await campaigns.LaunchAsync(id, User.GetUserId(), ct)));

    [HttpPost("{id:guid}/pause")]
    public Task<IActionResult> Pause(Guid id, CancellationToken ct) => Run(async () => Ok(await campaigns.PauseAsync(id, User.GetUserId(), ct)));

    [HttpPost("{id:guid}/resume")]
    public Task<IActionResult> Resume(Guid id, CancellationToken ct) => Run(async () => Ok(await campaigns.ResumeAsync(id, User.GetUserId(), ct)));

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) => Run(async () => Ok(await campaigns.CancelAsync(id, User.GetUserId(), ct)));

    [HttpPost("{id:guid}/retry")]
    public Task<IActionResult> Retry(Guid id, CancellationToken ct) => Run(async () => Ok(await campaigns.RetryFailedAsync(id, User.GetUserId(), ct)));

    private static Task<IActionResult> Run(Func<Task<IActionResult>> action) => TelegramController.Run(action);
}
