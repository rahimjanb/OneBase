using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Security;
using OneBase.Application.Telegram;
using OneBase.Infrastructure.Telegram.Reports;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → Telegram → Автоматические отчёты» (telegram.manage): аккаунт, язык и параметры; расписание 13 запусков шести отчётов;
/// получатели «роль (РМ/СВ/ТП) → область → чаты → отчёты»; предпросмотр, тест в выбранный чат, ручная отправка и повтор; история.
/// Реальные данные продаж — только с правом sales.read (проверяется на сервере, из токена).
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

    /// <summary>Расписание запуска (code — «daily.midday», «weekly.saturday» …): включение, время, дни, пояс, догон пропущенного.</summary>
    [HttpPut("jobs/{code}")]
    public Task<IActionResult> SaveJob(string code, ReportJobInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.SaveJobAsync(code, input, User.GetUserId(), ct)));

    [HttpPost("recipients")]
    public Task<IActionResult> CreateRecipient(ReportRecipientInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.SaveRecipientAsync(null, input, CanUseSales, User.GetUserId(), ct)));

    [HttpPut("recipients/{id:guid}")]
    public Task<IActionResult> UpdateRecipient(Guid id, ReportRecipientInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.SaveRecipientAsync(id, input, CanUseSales, User.GetUserId(), ct)));

    [HttpDelete("recipients/{id:guid}")]
    public Task<IActionResult> DeleteRecipient(Guid id, CancellationToken ct) =>
        Run(async () =>
        {
            await reports.DeleteRecipientAsync(id, User.GetUserId(), ct);
            return NoContent();
        });

    /// <summary>Отправить отчёт получателю сейчас во все его чаты (реальные данные); уже отправлен сегодня — 409, повтор — с force.</summary>
    [HttpPost("recipients/{id:guid}/send")]
    public Task<IActionResult> Send(Guid id, ReportSendInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.SendNowAsync(id, input, CanUseSales, User.GetUserId(), ct)));

    /// <summary>Картинки отчёта без отправки: запуск, роль и область (или получатель), день и время формирования, язык.</summary>
    [HttpPost("preview")]
    public Task<IActionResult> Preview(ReportPreviewInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.PreviewAsync(input, CanUseSales, ct)));

    /// <summary>Тестовая отправка — только в выбранный чат, с пометкой «ТЕСТОВАЯ ОТПРАВКА»; получатели её не получают.</summary>
    [HttpPost("test")]
    public Task<IActionResult> Test(ReportTestInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.TestAsync(input, CanUseSales, User.GetUserId(), ct)));

    /// <summary>Повтор неудачной отправки; «неизвестно, дошёл ли» — только с force после проверки чата.</summary>
    [HttpPost("deliveries/{id:guid}/retry")]
    public Task<IActionResult> Retry(Guid id, [FromQuery] bool force, CancellationToken ct) =>
        Run(async () => Ok(await reports.RetryAsync(id, force, CanUseSales, User.GetUserId(), ct)));

    /// <summary>История: отправки (фильтры — период, отчёт или запуск, регион, статус; по 50) и последние запуски расписания.</summary>
    [HttpGet("history")]
    public Task<IActionResult> History([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? report, [FromQuery] Guid? regionId,
        [FromQuery] string? status, [FromQuery] int page = 1, CancellationToken ct = default) =>
        Run(async () => Ok(await reports.HistoryAsync(new ReportHistoryQuery(from, to, report, regionId, status, page), ct)));

    /// <summary>Дневные планы ТП на день для «ТП без заказов» (зафиксированы утром): действующие строки и история пересчётов.</summary>
    [HttpGet("daily-plans")]
    public Task<IActionResult> DailyPlans([FromQuery] DateOnly? date, CancellationToken ct) => Run(async () => Ok(await reports.DailyPlansAsync(date, CanUseSales, ct)));

    /// <summary>Пересчёт дневного плана администратором (день, один ТП или все) — причина обязательна; прежние строки остаются историей.</summary>
    [HttpPost("daily-plans/recalculate")]
    public Task<IActionResult> RecalculateDailyPlans(DailyPlanRecalcInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.RecalculateDailyPlansAsync(input, CanUseSales, User.GetUserId(), ct)));

    /// <summary>Справочник праздников OneBase (нерабочие дни помимо воскресений) — для дневного плана.</summary>
    [HttpGet("holidays")]
    public Task<IActionResult> Holidays([FromQuery] int? year, CancellationToken ct) => Run(async () => Ok(await reports.HolidaysAsync(year, ct)));

    [HttpPost("holidays")]
    public Task<IActionResult> AddHoliday(HolidayInput input, CancellationToken ct) => Run(async () => Ok(await reports.AddHolidayAsync(input, User.GetUserId(), ct)));

    [HttpDelete("holidays/{id:guid}")]
    public Task<IActionResult> DeleteHoliday(Guid id, CancellationToken ct) =>
        Run(async () =>
        {
            await reports.DeleteHolidayAsync(id, User.GetUserId(), ct);
            return NoContent();
        });

    /// <summary>Право на данные продаж — из токена.</summary>
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
