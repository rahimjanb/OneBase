using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Recruitment;
using OneBase.Application.Recruitment.Telegram;
using OneBase.Domain.Recruitment;

namespace OneBase.Api.Controllers;

/// <summary>
/// HR → «Подбор» → Telegram: номера HR, диалоги с кандидатами (история, ответ через очередь, AI или человек), публикации вакансий, источники
/// и лиды. Права — как у подбора (просмотр / управление); подключение номеров — только «Настройки → Telegram».
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/hr/recruitment/telegram")]
public sealed class RecruitmentTelegramController(RecruitmentService recruitment, RecruitmentTelegramService telegram) : ControllerBase
{
    private Task<RecruitmentContext> ContextAsync(CancellationToken ct) =>
        recruitment.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    [HttpGet("accounts")]
    public async Task<IReadOnlyList<HrTelegramAccount>> Accounts(CancellationToken ct) => await telegram.AccountsAsync(await ContextAsync(ct), ct);

    [HttpGet("conversations")]
    public async Task<IReadOnlyList<HrConversationRow>> Conversations([FromQuery] Guid? accountId, [FromQuery] string? search, CancellationToken ct) =>
        await telegram.ConversationsAsync(await ContextAsync(ct), accountId, search, ct);

    [HttpGet("conversations/{id:guid}")]
    public async Task<HrConversation> Conversation(Guid id, CancellationToken ct) => await telegram.ConversationAsync(await ContextAsync(ct), id, ct);

    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<HrConversation> Send(Guid id, SendInput input, CancellationToken ct) => await telegram.SendAsync(await ContextAsync(ct), id, input, ct);

    [HttpPut("conversations/{id:guid}/mode")]
    public async Task<HrConversation> Mode(Guid id, ModeInput input, CancellationToken ct) => await telegram.SetModeAsync(await ContextAsync(ct), id, input.Mode, ct);

    [HttpGet("accounts/{accountId:guid}/chats")]
    public async Task<IReadOnlyList<PublicationChat>> Chats(Guid accountId, CancellationToken ct) => await telegram.PublicationChatsAsync(await ContextAsync(ct), accountId, ct);

    [HttpGet("vacancies/{vacancyId:guid}/template")]
    public async Task<IActionResult> Template(Guid vacancyId, [FromQuery] Guid accountId, CancellationToken ct) =>
        Ok(new { text = await telegram.TemplateAsync(await ContextAsync(ct), vacancyId, accountId, ct) });

    [HttpGet("publications")]
    public async Task<IReadOnlyList<PublicationView>> Publications([FromQuery] Guid? vacancyId, CancellationToken ct) =>
        await telegram.PublicationsAsync(await ContextAsync(ct), vacancyId, ct);

    [HttpPost("publications")]
    public async Task<IReadOnlyList<PublicationView>> Publish(PublicationInput input, CancellationToken ct) => await telegram.PublishAsync(await ContextAsync(ct), input, ct);

    [HttpPost("publications/{id:guid}/cancel")]
    public async Task<IReadOnlyList<PublicationView>> CancelPublication(Guid id, CancellationToken ct) => await telegram.CancelPublicationAsync(await ContextAsync(ct), id, ct);

    [HttpGet("sources")]
    public async Task<IReadOnlyList<TelegramSourceView>> Sources(CancellationToken ct) => await telegram.SourcesAsync(await ContextAsync(ct), ct);

    [HttpPost("sources")]
    public async Task<IReadOnlyList<TelegramSourceView>> AddSource(SourceInput input, CancellationToken ct) => await telegram.AddSourceAsync(await ContextAsync(ct), input, ct);

    [HttpDelete("sources/{id:guid}")]
    public async Task<IReadOnlyList<TelegramSourceView>> RemoveSource(Guid id, CancellationToken ct) => await telegram.RemoveSourceAsync(await ContextAsync(ct), id, ct);

    [HttpGet("leads")]
    public async Task<IReadOnlyList<LeadView>> Leads([FromQuery] LeadStatus? status, CancellationToken ct) => await telegram.LeadsAsync(await ContextAsync(ct), status, ct);

    [HttpPost("leads/{id:guid}/convert")]
    public async Task<IActionResult> ConvertLead(Guid id, LeadConvertInput input, CancellationToken ct) =>
        Ok(new { candidateId = await telegram.ConvertLeadAsync(await ContextAsync(ct), id, input, recruitment, ct) });

    [HttpPost("leads/{id:guid}/ignore")]
    public async Task<IReadOnlyList<LeadView>> IgnoreLead(Guid id, CancellationToken ct) => await telegram.IgnoreLeadAsync(await ContextAsync(ct), id, ct);
}
