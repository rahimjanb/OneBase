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
public sealed class TelegramReportsController(TelegramReportService reports, IncentiveSettingsService incentives, IncentiveSleepingService sleeping,
    IncentivePayoutsService payouts) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Overview(CancellationToken ct) => Run(async () => Ok(await reports.OverviewAsync(CanUseSales, ct)));

    [HttpPut("settings")]
    public Task<IActionResult> SaveSettings(ReportSettingsInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.SaveSettingsAsync(input, User.GetUserId(), ct)));

    /// <summary>«Отчёты PDF»: только при полных данных, карточки ТП для СВ, формулы целей недели.</summary>
    [HttpPut("weekly-pdf")]
    public Task<IActionResult> SaveWeeklyPdf(WeeklyPdfSettingsInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.SaveWeeklyPdfAsync(input, User.GetUserId(), ct)));

    /// <summary>«Акции»: накопительные акции (Twizos и будущие), типы товаров Linko для выбора, имена оптовых ТП.</summary>
    [HttpGet("incentives")]
    public Task<IActionResult> Incentives(CancellationToken ct) => Run(async () => Ok(await incentives.OverviewAsync(ct)));

    /// <summary>Новая акция по образцу (создаётся выключенной).</summary>
    [HttpPost("incentives")]
    public Task<IActionResult> CreateIncentive(IncentiveProgramInput input, CancellationToken ct) =>
        Run(async () => Ok(await incentives.CreateAsync(input, User.GetUserId(), ct)));

    /// <summary>Настройки акции: период, товары, ступени и призы, ориентиры, цели магазинов, оптовые ТП, СВ и РМ, несвежие данные, включение.</summary>
    [HttpPut("incentives/{code}")]
    public Task<IActionResult> SaveIncentive(string code, IncentiveProgramInput input, CancellationToken ct) =>
        Run(async () => Ok(await incentives.SaveAsync(code, input, User.GetUserId(), ct)));

    /// <summary>Накоплено сейчас по оргструктуре «Продаж» (только с sales.read): розница и опт своими местами.</summary>
    [HttpGet("incentives/{code}/standings")]
    public Task<IActionResult> IncentiveStandings(string code, [FromQuery] DateOnly? date, CancellationToken ct) =>
        Run(async () => Ok(await incentives.StandingsAsync(code, date, CanUseSales, ct)));

    /// <summary>Акции ноября акции (YM-01…03, TW-01…03, бюджеты): параметры, которые меняются без программиста (ТЗ, раздел 8).</summary>
    [HttpPut("incentives/{code}/promos")]
    public Task<IActionResult> SavePromos(string code, OneBase.Application.Telegram.Incentive.IncentivePromos input, CancellationToken ct) =>
        Run(async () => Ok(await incentives.SavePromosAsync(code, input, User.GetUserId(), ct)));

    /// <summary>Кэшбек YM-02 и призы TW-01 по ТП и неделям (только с sales.read).</summary>
    [HttpGet("incentives/{code}/payouts")]
    public Task<IActionResult> Payouts(string code, [FromQuery] DateOnly? date, CancellationToken ct) =>
        Run(async () => Ok(await payouts.PayoutsAsync(code, date, CanUseSales, ct)));

    /// <summary>Отметка бухгалтерии «выплачено» за неделю (сумма 0 — снять отметку).</summary>
    [HttpPut("incentives/{code}/payouts")]
    public Task<IActionResult> SetPayout(string code, IncentivePayoutInput input, CancellationToken ct) =>
        Run(async () => Ok(await payouts.SetPayoutAsync(code, input, User.GetUserId(), CanUseSales, ct)));

    /// <summary>Список спящих Yamelly акции (по Linko): сколько точек, с какого дня действует, когда меняли, правило.</summary>
    [HttpGet("incentives/{code}/sleeping")]
    public Task<IActionResult> Sleeping(string code, CancellationToken ct) => Run(async () => Ok(await sleeping.SummaryAsync(code, ct)));

    /// <summary>Пересчитать список спящих по Linko на сегодня: новые спящие — в список, больше не спящие и не бравшие Yamelly — исключить.</summary>
    [HttpPost("incentives/{code}/sleeping/refresh")]
    public Task<IActionResult> RefreshSleeping(string code, CancellationToken ct) =>
        Run(async () => Ok(await sleeping.RefreshAsync(code, User.GetUserId(), ct)));

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

    /// <summary>Массовое действие над получателями (включить отчёт, время, чат, отключить, отмена): все строки — одним сохранением.</summary>
    [HttpPut("recipients")]
    public Task<IActionResult> UpdateRecipients(ReportRecipientsBulkInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.SaveRecipientsAsync(input, CanUseSales, User.GetUserId(), ct)));

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

    /// <summary>
    /// Еженедельный отчёт — данные без PDF и без отправки (проверка цифр): роль (rm без направления и пользователя — РОП, rm, sv, tp), область, по какой
    /// день (пусто — по отчётный день «Продаж»). Нужно право sales.read; деньги КПУ и DSO — только с finance.read.
    /// </summary>
    [HttpPost("weekly/data")]
    public Task<IActionResult> WeeklyData(WeeklyDataInput input, [FromServices] OneBase.Application.Telegram.Weekly.WeeklyReportBuilder builder, CancellationToken ct) =>
        Run(async () =>
        {
            if (!CanUseSales)
            {
                throw new ReportForbiddenException("Нет права на данные продаж (sales.read).");
            }

            var role = input.Role ?? OneBase.Domain.Telegram.TelegramReportRoles.Sv;
            var scope = new OneBase.Application.Telegram.Reports.ReportScope(role, DirectionId: input.DirectionId, RegionId: input.RegionId, AgentId: input.AgentId,
                UserId: role == OneBase.Domain.Telegram.TelegramReportRoles.Rm ? input.UserId : null);
            return Ok(await builder.BuildAsync(scope, input.Date, User.FindAll(OneBaseClaims.Permission).Any(c => c.Value == Permissions.FinanceRead), ct));
        });

    /// <summary>
    /// Еженедельный отчёт PDF — предпросмотр и скачивание без отправки: та же область и день, язык (ru/uz). kind=cards — карточки ТП (у ТП —
    /// всегда карточка, у СВ — карточки команды); иначе — страницы РОП / РМ / СВ.
    /// </summary>
    [HttpPost("weekly/pdf")]
    public Task<IActionResult> WeeklyPdf(WeeklyDataInput input, [FromQuery] string? language, [FromQuery] string? kind,
        [FromServices] OneBase.Application.Telegram.Weekly.WeeklyReportBuilder builder, [FromServices] OneBase.Application.Telegram.Weekly.IWeeklyPdfRenderer renderer,
        CancellationToken ct) =>
        Run(async () =>
        {
            if (!CanUseSales)
            {
                throw new ReportForbiddenException("Нет права на данные продаж (sales.read).");
            }

            var role = input.Role ?? OneBase.Domain.Telegram.TelegramReportRoles.Sv;
            if (role == OneBase.Domain.Telegram.TelegramReportRoles.Tp && input.AgentId is null)
            {
                throw new ReportValidationException("Выберите ТП.");
            }

            var lang = OneBase.Application.Abstractions.UiLanguage.Normalize(language) ?? "ru";
            var scope = new OneBase.Application.Telegram.Reports.ReportScope(role, DirectionId: input.DirectionId, RegionId: input.RegionId, AgentId: input.AgentId,
                UserId: role == OneBase.Domain.Telegram.TelegramReportRoles.Rm ? input.UserId : null);
            var report = await builder.BuildAsync(scope, input.Date, User.FindAll(OneBaseClaims.Permission).Any(c => c.Value == Permissions.FinanceRead), ct);
            var cards = role == OneBase.Domain.Telegram.TelegramReportRoles.Tp || kind == OneBase.Application.Telegram.Weekly.WeeklyPdfKinds.Cards;
            var fileName = string.Concat($"weekly-{(cards ? "cards" : report.Role)}-{report.ScopeName}-{report.PeriodStart:yyyy-MM-dd}.pdf"
                .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch));
            if (Request.Query.ContainsKey("images"))
            {
                // Предпросмотр: страницы картинками (data URI), как у предпросмотра автоотчётов, и что с данными.
                return Ok(new
                {
                    pages = (cards ? renderer.RenderCardImages(report, lang) : renderer.RenderImages(report, lang)).Select(p => "data:image/png;base64," + Convert.ToBase64String(p)).ToList(),
                    report.Role,
                    report.ScopeName,
                    report.SupervisorName,
                    report.PeriodStart,
                    report.PeriodEnd,
                    report.DataThrough,
                    report.DataAsOf,
                    report.SourceStatuses,
                    report.Incomplete,
                    warnings = report.DataWarnings.Select(w => OneBase.Application.Telegram.Weekly.WeeklyTexts.Note(w, lang)).Where(w => w is not null).ToList(),
                    fileName,
                });
            }

            return File(cards ? renderer.RenderCards(report, lang) : renderer.Render(report, lang), "application/pdf", fileName);
        });

    /// <summary>Тест еженедельного PDF — только в выбранный чат, с пометкой «ТЕСТОВАЯ ОТПРАВКА».</summary>
    [HttpPost("weekly/test")]
    public Task<IActionResult> WeeklyTest(WeeklyPdfTestInput input, CancellationToken ct) =>
        Run(async () => Ok(await reports.WeeklyTestAsync(input, CanUseSales, User.GetUserId(), ct)));

    /// <summary>PDF отправки из хранилища — «скачать» в истории.</summary>
    [HttpGet("deliveries/{id:guid}/files/{index:int}")]
    public Task<IActionResult> DeliveryFile(Guid id, int index, [FromServices] WeeklyPdfService weekly, CancellationToken ct) =>
        Run(async () =>
        {
            if (!CanUseSales)
            {
                throw new ReportForbiddenException("Нет права на данные продаж (sales.read).");
            }

            var (content, name) = await weekly.DownloadAsync(id, index, ct);
            return File(content, "application/pdf", name);
        });

    /// <summary>«Сформировать заново»: те же область, неделя и язык — новые PDF в хранилище, без отправки.</summary>
    [HttpPost("deliveries/{id:guid}/rebuild")]
    public Task<IActionResult> Rebuild(Guid id, [FromServices] WeeklyPdfService weekly, CancellationToken ct) =>
        Run(async () =>
        {
            if (!CanUseSales)
            {
                throw new ReportForbiddenException("Нет права на данные продаж (sales.read).");
            }

            var files = await weekly.RebuildAsync(id, ct);
            return Ok(files.Select((f, i) => new WeeklyPdfFileView(i, f.Kind, f.Name, f.Pages, f.Bytes)).ToList());
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

/// <summary>Запрос данных еженедельного отчёта: роль (rm/sv/tp), пользователь-РМ или направление (rm; оба пусто — вся страна), регион (sv), ТП (tp), по какой день.</summary>
public sealed record WeeklyDataInput(string? Role, Guid? DirectionId, Guid? RegionId, long? AgentId, DateOnly? Date, Guid? UserId = null);
