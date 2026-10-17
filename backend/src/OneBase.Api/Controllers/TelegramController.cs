using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Security;
using OneBase.Application.Telegram;
using OneBase.Infrastructure.Telegram;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → Telegram»: api_id/api_hash приложения, аккаунты (вход по номеру → код → пароль 2FA, проверка, отключение), AI-агент,
/// пользователи Telegram с режимами доступа, чаты и журнал агента. Сессии, номера, коды и пароли наружу не отдаются.
/// </summary>
[ApiController]
[Route("api/settings/telegram")]
[HasPermission(Permissions.TelegramManage)]
public sealed class TelegramController(TelegramAccountService telegram, OneBase.AI.Telegram.TelegramAgentService agent) : ControllerBase
{
    public sealed record CredentialsRequest(int ApiId, string? ApiHash);
    public sealed record NameRequest(string Name);
    public sealed record PhoneRequest(string Phone);
    public sealed record CodeRequest(string Code);
    public sealed record PasswordRequest(string Password);
    public sealed record ChatAgentRequest(bool Enabled);

    [HttpGet]
    public async Task<IActionResult> Overview(CancellationToken ct) => Ok(await telegram.OverviewAsync(ct));

    /// <summary>api_hash пустой — оставить прежний (в ответах он не возвращается, только последние символы).</summary>
    [HttpPut("credentials")]
    public Task<IActionResult> SaveCredentials(CredentialsRequest request, CancellationToken ct) =>
        Run(async () =>
        {
            await telegram.SaveCredentialsAsync(request.ApiId, request.ApiHash, User.GetUserId(), ct);
            return Ok(await telegram.OverviewAsync(ct));
        });

    [HttpPost("accounts")]
    public Task<IActionResult> CreateAccount(NameRequest request, CancellationToken ct) =>
        Run(async () => Ok(await telegram.CreateAccountAsync(request.Name, User.GetUserId(), ct)));

    [HttpPut("accounts/{id:guid}")]
    public Task<IActionResult> RenameAccount(Guid id, NameRequest request, CancellationToken ct) =>
        Run(async () => Ok(await telegram.RenameAccountAsync(id, request.Name, User.GetUserId(), ct)));

    [HttpDelete("accounts/{id:guid}")]
    public Task<IActionResult> DeleteAccount(Guid id, CancellationToken ct) =>
        Run(async () =>
        {
            await telegram.DeleteAccountAsync(id, User.GetUserId(), ct);
            return NoContent();
        });

    /// <summary>Вход, шаг 1: номер → Telegram присылает код. Ответ — следующий шаг (CodeRequired, PasswordRequired, Connected).</summary>
    [HttpPost("accounts/{id:guid}/login")]
    public Task<IActionResult> StartLogin(Guid id, PhoneRequest request, CancellationToken ct) =>
        Run(async () => Ok(await telegram.StartLoginAsync(id, request.Phone, User.GetUserId(), ct)));

    [HttpPost("accounts/{id:guid}/login/code")]
    public Task<IActionResult> SubmitCode(Guid id, CodeRequest request, CancellationToken ct) =>
        Run(async () => Ok(await telegram.SubmitCodeAsync(id, request.Code, User.GetUserId(), ct)));

    [HttpPost("accounts/{id:guid}/login/password")]
    public Task<IActionResult> SubmitPassword(Guid id, PasswordRequest request, CancellationToken ct) =>
        Run(async () => Ok(await telegram.SubmitPasswordAsync(id, request.Password, User.GetUserId(), ct)));

    [HttpPost("accounts/{id:guid}/check")]
    public Task<IActionResult> Check(Guid id, CancellationToken ct) => Run(async () => Ok(await telegram.CheckAsync(id, ct)));

    [HttpPost("accounts/{id:guid}/disconnect")]
    public Task<IActionResult> Disconnect(Guid id, CancellationToken ct) =>
        Run(async () => Ok(await telegram.DisconnectAsync(id, User.GetUserId(), ct)));

    [HttpGet("accounts/{id:guid}/agent")]
    public Task<IActionResult> Settings(Guid id, CancellationToken ct) => Run(async () => Ok(await telegram.SettingsAsync(id, ct)));

    [HttpPut("accounts/{id:guid}/agent")]
    public Task<IActionResult> SaveSettings(Guid id, TelegramSettingsInput input, CancellationToken ct) =>
        Run(async () => Ok(await telegram.SaveSettingsAsync(id, input, User.GetUserId(), ct)));

    /// <summary>Проверка агента: ответ «как будто» на сообщение выбранного пользователя в выбранном чате; в Telegram ничего не уходит.</summary>
    [HttpPost("accounts/{id:guid}/agent/test")]
    public Task<IActionResult> TestAgent(Guid id, OneBase.AI.Telegram.AgentTestInput input, CancellationToken ct) =>
        Run(async () => Ok(await agent.SimulateAsync(id, input, ct)));

    /// <summary>Кому отвечать (всем по отправленным данным или только списку) и с чьими правами OneBase — полный доступ.</summary>
    [HttpGet("accounts/{id:guid}/access")]
    public Task<IActionResult> Access(Guid id, CancellationToken ct) => Run(async () => Ok(await telegram.AccessAsync(id, ct)));

    [HttpPut("accounts/{id:guid}/access")]
    public Task<IActionResult> SaveAccess(Guid id, TelegramAccessInput input, CancellationToken ct) =>
        Run(async () => Ok(await telegram.SaveAccessAsync(id, input, User.GetUserId(), ct)));

    [HttpGet("accounts/{id:guid}/users")]
    public Task<IActionResult> Users(Guid id, [FromQuery] string? search, [FromQuery] string? mode, CancellationToken ct) =>
        Run(async () => Ok(await telegram.PermissionsAsync(id, search, mode, ct)));

    [HttpPost("accounts/{id:guid}/users")]
    public Task<IActionResult> CreatePermission(Guid id, TelegramPermissionInput input, CancellationToken ct) =>
        Run(async () => Ok(await telegram.SavePermissionAsync(id, null, input, User.GetUserId(), ct)));

    [HttpPut("accounts/{id:guid}/users/{permissionId:guid}")]
    public Task<IActionResult> UpdatePermission(Guid id, Guid permissionId, TelegramPermissionInput input, CancellationToken ct) =>
        Run(async () => Ok(await telegram.SavePermissionAsync(id, permissionId, input, User.GetUserId(), ct)));

    [HttpDelete("accounts/{id:guid}/users/{permissionId:guid}")]
    public Task<IActionResult> DeletePermission(Guid id, Guid permissionId, CancellationToken ct) =>
        Run(async () =>
        {
            await telegram.DeletePermissionAsync(id, permissionId, User.GetUserId(), ct);
            return NoContent();
        });

    /// <summary>Массово: enable, disable, sent_only, delete.</summary>
    [HttpPost("accounts/{id:guid}/users/bulk")]
    public Task<IActionResult> Bulk(Guid id, TelegramBulkInput input, CancellationToken ct) =>
        Run(async () => Ok(new { changed = await telegram.BulkAsync(id, input, User.GetUserId(), ct) }));

    /// <summary>Пользователи OneBase для полного доступа (поиск по имени и логину).</summary>
    [HttpGet("onebase-users")]
    public async Task<IActionResult> OneBaseUsers([FromQuery] string? search, CancellationToken ct) => Ok(await telegram.OneBaseUsersAsync(search, ct));

    [HttpGet("accounts/{id:guid}/chats")]
    public Task<IActionResult> Chats(Guid id, [FromQuery] string? search, [FromQuery] string? type, CancellationToken ct) =>
        Run(async () => Ok(await telegram.ChatsAsync(id, search, type, ct)));

    /// <summary>Загрузить чаты аккаунта из Telegram.</summary>
    [HttpPost("accounts/{id:guid}/chats/sync")]
    public Task<IActionResult> SyncChats(Guid id, CancellationToken ct) =>
        Run(async () => Ok(new { chats = await telegram.SyncChatsAsync(id, ct) }));

    [HttpPut("accounts/{id:guid}/chats/{chatId:guid}/agent")]
    public Task<IActionResult> ChatAgent(Guid id, Guid chatId, ChatAgentRequest request, CancellationToken ct) =>
        Run(async () => Ok(await telegram.SetChatAgentAsync(id, chatId, request.Enabled, User.GetUserId(), ct)));

    /// <summary>Журнал агента: последние 200 ответов (status — answered, no_info, refused, rate_limited, error).</summary>
    [HttpGet("accounts/{id:guid}/executions")]
    public Task<IActionResult> Executions(Guid id, [FromQuery] string? status, CancellationToken ct) =>
        Run(async () => Ok(await telegram.ExecutionsAsync(id, status, ct)));

    internal static async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (TelegramException ex) when (ex.Kind == TelegramErrorKind.Flood)
        {
            return new ObjectResult(new { error = ex.Message, waitSeconds = ex.WaitSeconds }) { StatusCode = StatusCodes.Status429TooManyRequests };
        }
        catch (TelegramException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message, kind = ex.Kind.ToString() });
        }
        catch (KeyNotFoundException)
        {
            return new NotFoundObjectResult(new { error = "Не найдено — обновите страницу." });
        }
    }
}

/// <summary>Привязка своего Telegram к профилю OneBase (любой вошедший пользователь): одноразовый код, список привязок, отвязка.</summary>
[ApiController]
[Authorize]
[Route("api/me/telegram")]
public sealed class MyTelegramController(TelegramAccountService telegram) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Links(CancellationToken ct) => Ok(await telegram.LinksAsync(User.GetUserId(), ct));

    /// <summary>Новый код (действует 15 минут, показывается один раз) и кому его отправить.</summary>
    [HttpPost("code")]
    public Task<IActionResult> Code(CancellationToken ct) =>
        TelegramController.Run(async () => Ok(await telegram.CreateLinkCodeAsync(User.GetUserId(), ct)));

    [HttpDelete("{permissionId:guid}")]
    public Task<IActionResult> Unlink(Guid permissionId, CancellationToken ct) =>
        TelegramController.Run(async () =>
        {
            await telegram.UnlinkAsync(User.GetUserId(), permissionId, ct);
            return NoContent();
        });
}
