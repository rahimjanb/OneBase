using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.DialogAgents;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Agents;
using OneBase.Application.Field;
using OneBase.Domain.Telegram;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → ИИ-агенты»: агенты отделов (создать, настроить, удалить), готовые ответы, журнал ответов (проверить черновик, исправить,
/// оценить), диалоги отдела и «Проверка» (что ответил бы агент — без отправки). Права — по отделу: администратор AI, HR (агенты HR),
/// управление Telegram (агенты продаж), руководитель отдела; чужой отдел — 403.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/agents")]
public sealed class DialogAgentsController(DialogAgentService agents, DialogAgentTester tester, IAppDbContext db) : ControllerBase
{
    public sealed record ModeInput(TelegramConversationMode Mode);

    private Task<AgentAccess> AccessAsync(CancellationToken ct) =>
        agents.AccessAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    [HttpGet("departments")]
    public async Task<IReadOnlyList<AgentDepartmentView>> Departments(CancellationToken ct) => await agents.DepartmentsAsync(await AccessAsync(ct), ct);

    [HttpPost]
    public async Task<AgentView> Create(AgentCreateInput input, CancellationToken ct) => await agents.CreateAsync(await AccessAsync(ct), input, ct);

    [HttpGet("{id:guid}")]
    public async Task<AgentView> Get(Guid id, CancellationToken ct) => await agents.GetAsync(await AccessAsync(ct), id, ct);

    [HttpPut("{id:guid}")]
    public async Task<AgentView> Save(Guid id, AgentInput input, CancellationToken ct) => await agents.SaveAsync(await AccessAsync(ct), id, input, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await agents.DeleteAsync(await AccessAsync(ct), id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/default")]
    public async Task<AgentView> MakeDefault(Guid id, CancellationToken ct) => await agents.MakeDefaultAsync(await AccessAsync(ct), id, ct);

    // ---------- Готовые ответы ----------

    [HttpPost("{id:guid}/answers")]
    public async Task<AgentView> AddAnswer(Guid id, AgentAnswerInput input, CancellationToken ct) => await agents.AddAnswerAsync(await AccessAsync(ct), id, input, ct);

    [HttpPut("{id:guid}/answers/{answerId:guid}")]
    public async Task<AgentView> UpdateAnswer(Guid id, Guid answerId, AgentAnswerInput input, CancellationToken ct) =>
        await agents.UpdateAnswerAsync(await AccessAsync(ct), id, answerId, input, ct);

    [HttpDelete("{id:guid}/answers/{answerId:guid}")]
    public async Task<AgentView> DeleteAnswer(Guid id, Guid answerId, CancellationToken ct) => await agents.DeleteAnswerAsync(await AccessAsync(ct), id, answerId, ct);

    // ---------- Журнал ответов ----------

    [HttpGet("{id:guid}/replies")]
    public async Task<AgentRepliesPage> Replies(Guid id, [FromQuery] string? filter, CancellationToken ct) => await agents.RepliesAsync(await AccessAsync(ct), id, filter, ct);

    [HttpPost("replies/{replyId:guid}/approve")]
    public async Task<AgentRepliesPage> Approve(Guid replyId, ReplyReviewInput input, CancellationToken ct) =>
        await agents.ApproveAsync(await AccessAsync(ct), replyId, input, ct);

    [HttpPost("replies/{replyId:guid}/reject")]
    public async Task<AgentRepliesPage> Reject(Guid replyId, CancellationToken ct) => await agents.RejectAsync(await AccessAsync(ct), replyId, ct);

    [HttpPost("replies/{replyId:guid}/rate")]
    public async Task<AgentRepliesPage> Rate(Guid replyId, ReplyRateInput input, CancellationToken ct) => await agents.RateAsync(await AccessAsync(ct), replyId, input, ct);

    // ---------- Диалоги отдела ----------

    [HttpGet("{id:guid}/conversations")]
    public async Task<IReadOnlyList<AgentConversationRow>> Conversations(Guid id, CancellationToken ct) => await agents.ConversationsAsync(await AccessAsync(ct), id, ct);

    [HttpGet("{id:guid}/conversations/{conversationId:guid}")]
    public async Task<AgentConversationView> Conversation(Guid id, Guid conversationId, CancellationToken ct) =>
        await agents.ConversationAsync(await AccessAsync(ct), id, conversationId, ct);

    [HttpPost("{id:guid}/conversations/{conversationId:guid}/messages")]
    public async Task<AgentConversationView> Send(Guid id, Guid conversationId, AgentSendInput input, CancellationToken ct) =>
        await agents.SendAsync(await AccessAsync(ct), id, conversationId, input, ct);

    [HttpPut("{id:guid}/conversations/{conversationId:guid}/mode")]
    public async Task<AgentConversationView> Mode(Guid id, Guid conversationId, ModeInput input, CancellationToken ct) =>
        await agents.SetModeAsync(await AccessAsync(ct), id, conversationId, input.Mode, ct);

    // ---------- Проверка ----------

    /// <summary>Что ответил бы агент на переписку — без отправки (платный вызов модели, только по кнопке).</summary>
    [HttpPost("{id:guid}/test")]
    [EnableRateLimiting("ai")]
    public async Task<AgentTestResult> Test(Guid id, AgentTestInput input, CancellationToken ct)
    {
        var access = await AccessAsync(ct);
        await agents.GetAsync(access, id, ct); // права на отдел
        var agent = await db.DialogAgents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new FieldNotFoundException("Агент не найден.");
        return await tester.TestAsync(agent, input, access.UserId, ct);
    }
}
