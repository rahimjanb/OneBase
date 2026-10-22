using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Security;
using OneBase.Application.Telegram;
using OneBase.Domain.Telegram;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → Telegram → Автоматические отчёты» (telegram.manage): аккаунт и язык, расписания утро/обед/вечер, автоотправки
/// «регион → супервайзер → чаты», предпросмотр, тестовая и ручная отправка, журнал. Реальные данные продаж — только с правом sales.read
/// (проверяется на сервере, из токена).
/// </summary>
[ApiController]
[Route("api/settings/telegram/reports")]
[HasPermission(Permissions.TelegramManage)]
public sealed class TelegramReportsController(TelegramReportService reports) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Overview(CancellationToken ct) => Run(async () => Ok(await reports.OverviewAsync(CanUseSales, ct)));

    [HttpPut("settings")]
    public Task<IActionResult> SaveSettings(ReportSettingsInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.SaveSettingsAsync(input, User.GetUserId(), ct)));

    [HttpPut("schedules/{kind}")]
    public Task<IActionResult> SaveSchedule(string kind, ReportScheduleInput input, CancellationToken ct) =>
        Run(async () => Enum.TryParse<TelegramReportKind>(kind, true, out var k)
            ? Ok(await reports.SaveScheduleAsync(k, input, User.GetUserId(), ct))
            : BadRequest(new { error = "Вид отчёта — Morning, Midday или Evening." }));

    [HttpPost("subscriptions")]
    public Task<IActionResult> CreateSubscription(ReportSubscriptionInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.SaveSubscriptionAsync(null, input, CanUseSales, User.GetUserId(), ct)));

    [HttpPut("subscriptions/{id:guid}")]
    public Task<IActionResult> UpdateSubscription(Guid id, ReportSubscriptionInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.SaveSubscriptionAsync(id, input, CanUseSales, User.GetUserId(), ct)));

    [HttpDelete("subscriptions/{id:guid}")]
    public Task<IActionResult> DeleteSubscription(Guid id, CancellationToken ct) =>
        Run(async () =>
        {
            await reports.DeleteSubscriptionAsync(id, User.GetUserId(), ct);
            return NoContent();
        });

    /// <summary>Отправить отчёт автоотправки сейчас во все её чаты (реальные данные); уже отправлен сегодня — 409, повтор — с force.</summary>
    [HttpPost("subscriptions/{id:guid}/send")]
    public Task<IActionResult> Send(Guid id, ReportSendInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.SendNowAsync(id, input, CanUseSales, User.GetUserId(), ct)));

    /// <summary>Картинки отчёта без отправки: демонстрационные или реальные данные региона и супервайзера (или автоотправки).</summary>
    [HttpPost("preview")]
    public Task<IActionResult> Preview(ReportPreviewInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.PreviewAsync(input, CanUseSales, ct)));

    /// <summary>Тестовая отправка в выбранный чат: по умолчанию демонстрационные данные с пометкой «ТЕСТОВЫЕ ДАННЫЕ».</summary>
    [HttpPost("test")]
    public Task<IActionResult> Test(ReportTestInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.TestAsync(input, CanUseSales, User.GetUserId(), ct)));

    /// <summary>Журнал: последние 300 отправок (kind, status — pending/sending/sent/failed, mode — real/demo).</summary>
    [HttpGet("deliveries")]
    public Task<IActionResult> Deliveries([FromQuery] string? kind, [FromQuery] string? status, [FromQuery] string? mode, CancellationToken ct) =>
        Run(async () => Ok(await reports.DeliveriesAsync(kind, status, mode, ct)));

    /// <summary>Право на данные продаж — из токена: без него — только демонстрационные данные.</summary>
    private bool CanUseSales => User.FindAll(OneBaseClaims.Permission).Any(c => c.Value == Permissions.SalesRead);

    private static async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ReportValidationException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message });
        }
        catch (ReportForbiddenException ex)
        {
            // Не 403: интерфейс на 403 показывает только «Недостаточно прав», а здесь важна причина.
            return new BadRequestObjectResult(new { error = ex.Message, forbidden = true });
        }
        catch (ReportConflictException ex)
        {
            return new ConflictObjectResult(new { error = ex.Message, conflict = true });
        }
        catch (TelegramException ex) when (ex.Kind == TelegramErrorKind.Flood)
        {
            return new ObjectResult(new { error = ex.Message, waitSeconds = ex.WaitSeconds }) { StatusCode = StatusCodes.Status429TooManyRequests };
        }
        catch (TelegramException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message, kind = ex.Kind.ToString() });
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(new { error = ex.Message is { Length: > 0 } m && !m.StartsWith("The given key", StringComparison.Ordinal) ? m : "Не найдено — обновите страницу." });
        }
    }
}
