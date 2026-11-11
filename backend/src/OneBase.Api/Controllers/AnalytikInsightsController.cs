using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OneBase.AI.Analytik;
using OneBase.AI.Consultant;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Analytik;
using OneBase.Application.Analytik.Business;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Analytik.Readiness;
using OneBase.Application.Analytik.Risk;
using OneBase.Application.Analytik.Supply;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Domain.Audit;

namespace OneBase.Api.Controllers;

public sealed record AnalytikReportInput(string Kind, string? Period, DateOnly? From, DateOnly? To);

/// <summary>Свежесть данных, видимых пользователю, и нехватка данных выбранного периода (баннер экранов отделов).</summary>
public sealed record ReadinessView(IReadOnlyList<FreshnessRow> Rows, IReadOnlyList<DataGap> Gaps);

public sealed record JobRunRow(Guid Id, string Job, string PeriodKey, string Status, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string? Summary, string? Error);

/// <summary>
/// «Аналитик»: панель компании, аналитика отделов, прогнозы, риски и отклонения, управленческие отчёты, фоновые задачи. Всё — по правам
/// пользователя и его области продаж (решают сервисы Аналитика); запуск задач — analytik.manage.
/// </summary>
[ApiController]
[Authorize]
[HasPermission(Permissions.AnalytikUse)]
[AnalytikErrors]
[Route("api/analytik")]
public sealed class AnalytikInsightsController(
    AnalytikAccess access,
    DashboardService dashboard,
    SalesAnalytik sales,
    FinanceAnalytik finance,
    OperationsAnalytik operations,
    SupplyPlanner supply,
    ForecastService forecasts,
    RiskEngine risks,
    AnalytikReportService reports,
    AnalytikJobs jobs,
    DataKeeper keeper,
    DataReadiness readiness,
    IAnalytikSourceSync sources,
    IAuditLogger audit,
    IAnalytikDb adb) : ControllerBase
{
    private Task<AnalytikPrincipal> PrincipalAsync(CancellationToken ct) => access.ResolveAsync(User.GetUserId(), ct);

    private static AnalytikPeriod Period(string? period, DateOnly? from, DateOnly? to) => AnalytikPeriod.Of(period, from, to, FieldClock.Today);

    [HttpGet("dashboard")]
    public async Task<DashboardView> Dashboard([FromQuery] string? period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] bool forecast,
        CancellationToken ct) =>
        await dashboard.BuildAsync(Period(period, from, to), await PrincipalAsync(ct), ct, forecast);

    [HttpGet("sales")]
    public async Task<ActionResult<SalesBlock>> Sales([FromQuery] string? period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        await sales.BlockAsync(Period(period, from, to), await PrincipalAsync(ct), ct) is { } block ? block : Forbidden("продажам");

    [HttpGet("production")]
    public async Task<ActionResult<ProductionBlock>> Production([FromQuery] string? period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        await operations.ProductionAsync(Period(period, from, to), await PrincipalAsync(ct), ct) is { } block ? block : Forbidden("производству");

    /// <summary>Склад сырья и план закупок — один расчёт (остатки, расход, поставки, потребность).</summary>
    [HttpGet("supply")]
    public async Task<SupplyPlanView> Supply([FromQuery] string? search, CancellationToken ct) => await supply.PlanAsync(await PrincipalAsync(ct), ct, search);

    [HttpGet("finance")]
    public async Task<ActionResult<FinanceBlock>> Finance([FromQuery] string? period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var principal = await PrincipalAsync(ct);
        var p = Period(period, from, to);
        return await finance.BlockAsync(p, principal, await sales.BlockAsync(p, principal, ct), ct) is { } block ? block : Forbidden("финансам");
    }

    [HttpGet("hr")]
    public async Task<IActionResult> Hr([FromQuery] string? period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var principal = await PrincipalAsync(ct);
        var p = Period(period, from, to);
        var hr = await operations.HrAsync(p, principal, ct);
        var work = await operations.WorkAsync(p, principal, ct);
        return hr is null && work is null ? Forbidden("данным HR и задач") : Ok(new { Period = p, Hr = hr, Work = work });
    }

    [HttpGet("forecast")]
    public async Task<ForecastView> Forecast([FromQuery] string metric, [FromQuery] string? granularity, [FromQuery] int? horizon, [FromQuery] Guid? item,
        [FromQuery] int? scenario, CancellationToken ct) =>
        await forecasts.ForecastAsync(metric, granularity ?? "day", horizon ?? 30, await PrincipalAsync(ct), ct, item, scenario is { } s ? s / 100m : null);

    [HttpGet("risks")]
    public async Task<IReadOnlyList<InsightView>> Risks([FromQuery] string? module, [FromQuery] bool resolved, CancellationToken ct) =>
        await risks.ListAsync(await PrincipalAsync(ct), string.IsNullOrWhiteSpace(module) ? null : module, resolved, 300, ct);

    [HttpGet("reports")]
    public async Task<IReadOnlyList<ReportListRow>> Reports(CancellationToken ct) => await reports.ListAsync(await PrincipalAsync(ct), 100, ct);

    [HttpPost("reports")]
    [EnableRateLimiting("ai")]
    public async Task<ReportListRow> CreateReport(AnalytikReportInput input, CancellationToken ct)
    {
        var principal = await PrincipalAsync(ct);
        var row = await reports.CreateAsync(input.Kind, Period(input.Period, input.From, input.To), principal, principal.UserId, null, ct);
        return new ReportListRow(row.Id, row.Kind, row.Title, row.PeriodFrom, row.PeriodTo, row.CreatedAt, false);
    }

    [HttpGet("reports/{id:guid}")]
    public async Task<ActionResult<ReportDoc>> Report(Guid id, CancellationToken ct) =>
        await reports.GetAsync(id, await PrincipalAsync(ct), ct) is { } doc ? doc : NotFound(new { error = "Отчёт не найден." });

    /// <summary>Отчёт файлом: xlsx или pdf. Имя файла — в пути (так его берут iOS и браузеры без Content-Disposition).</summary>
    [HttpGet("reports/{id:guid}/{format}/{*fileName}")]
    public async Task<IActionResult> ReportFile(Guid id, string format, CancellationToken ct)
    {
        if (await reports.GetAsync(id, await PrincipalAsync(ct), ct) is not { } doc)
        {
            return NotFound(new { error = "Отчёт не найден." });
        }

        var name = string.Concat(doc.Title.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return format switch
        {
            "xlsx" => File(reports.Excel(doc), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{name}.xlsx"),
            "pdf" => File(reports.Pdf(doc), "application/pdf", $"{name}.pdf"),
            _ => BadRequest(new { error = "Формат — xlsx или pdf." }),
        };
    }

    [HttpGet("jobs")]
    [HasPermission(Permissions.AnalytikManage)]
    public async Task<IReadOnlyList<JobRunRow>> Jobs(CancellationToken ct) =>
        await adb.JobRuns.AsNoTracking().OrderByDescending(j => j.StartedAt).Take(60)
            .Select(j => new JobRunRow(j.Id, j.Job, j.PeriodKey, j.Status, j.StartedAt, j.FinishedAt, j.Summary, j.Error))
            .ToListAsync(ct);

    /// <summary>Запустить фоновую задачу сейчас (ключ периода — «manual-…»: не мешает плановому запуску).</summary>
    [HttpPost("jobs/{name}/run")]
    [HasPermission(Permissions.AnalytikManage)]
    public async Task<IActionResult> RunJob(string name, CancellationToken ct)
    {
        if (!AnalytikJobs.Names.Contains(name))
        {
            return BadRequest(new { error = $"Задача: {string.Join(", ", AnalytikJobs.Names)}." });
        }

        var ok = await jobs.RunAsync(new AnalytikJob(name, $"manual-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}"), ct, force: true);
        return Ok(new { ok });
    }

    /// <summary>
    /// Свежесть данных и нехватка данных периода — для экранов отделов (module: sales, finance, hr). Отстающий источник при этом начинает
    /// обновляться (как при открытии обзора).
    /// </summary>
    [HttpGet("readiness")]
    public async Task<ReadinessView> Readiness([FromQuery] string? period, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? module,
        CancellationToken ct)
    {
        var principal = await PrincipalAsync(ct);
        var report = await keeper.RunAsync("dashboard", ct);
        var modules = string.IsNullOrWhiteSpace(module) ? null : module.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        bool In(string m) => modules is null || modules.Contains(m);
        var codes = report.Visible(principal).Where(d => In(d.Module)).Select(d => d.Code).ToHashSet();
        return new ReadinessView(DashboardService.Freshness(report, principal).Where(r => r.Code is not null && codes.Contains(r.Code)).ToList(),
            DataReadiness.Gaps(report.Visible(principal).Where(d => In(d.Module)), Period(period, from, to), report.CheckedAt,
                code => report.CanManage(code, principal)));
    }

    /// <summary>
    /// «Обновить» у источника данных (linko, verifix): запуск обновления в фоне. Право — как у подключения: Linko — управление продажами по всей
    /// компании или интеграциями, Verifix — интеграциями.
    /// </summary>
    [HttpPost("readiness/{source}/refresh")]
    public async Task<IActionResult> Refresh(string source, CancellationToken ct)
    {
        var principal = await PrincipalAsync(ct);
        if ((await readiness.CheckAsync(ct)).Link(source) is not { } link)
        {
            return NotFound(new { error = "Неизвестный источник данных." });
        }

        if (!DataReadiness.CanManage(link, principal))
        {
            return Forbidden($"обновлению данных {link.Title}");
        }

        if (!link.Connected)
        {
            return Problem(link.Problem ?? $"{link.Title} не подключён.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        switch (await sources.StartAsync(link.Code, ct))
        {
            case SyncStartResult.AlreadyRunning:
                return Conflict(new { error = "Обновление уже идёт." });
            case SyncStartResult.NotConnected:
                return Problem(link.Problem ?? $"{link.Title} не подключён.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        await audit.LogAsync(ActorType.User, User.GetUserId().ToString(), "analytik.data.refresh", data: new { source = link.Code }, cancellationToken: ct);
        return Accepted();
    }

    private ObjectResult Forbidden(string what) => StatusCode(StatusCodes.Status403Forbidden, new { error = $"Нет доступа к {what}." });
}

/// <summary>
/// AI-аналитик раздела «Аналитик»: чат с командой аналитиков (свои чаты, отдельно от консультанта), поток ответа — как у консультанта
/// (text/event-stream: start, progress, draft, done, error).
/// </summary>
[ApiController]
[Authorize]
[HasPermission(Permissions.AnalytikUse)]
[Route("api/analytik")]
public sealed class AnalytikChatController(
    [FromKeyedServices(AnalytikAiRegistration.ChatKey)] ConsultantChatService chats,
    OneBase.AI.Gateway.IAiGateway gateway,
    IHostApplicationLifetime lifetime,
    ILogger<AnalytikChatController> logger) : ControllerBase
{
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromMinutes(8);
    private static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(15);

    public sealed record ChatRequest(Guid? ConversationId, string Message, ChatContext? Context = null);

    public sealed record FeedbackRequest(string Rating, string? Comment);

    public sealed record RenameRequest(string Title);

    [HttpGet("conversations")]
    public async Task<IActionResult> Conversations([FromQuery] string? q, CancellationToken ct) => Ok(await chats.ListAsync(User.GetUserId(), ct, q));

    [HttpGet("conversations/{id:guid}")]
    public async Task<IActionResult> Conversation(Guid id, CancellationToken ct) =>
        await chats.GetAsync(User.GetUserId(), id, ct) is { } view ? Ok(view) : NotFound();

    /// <summary>Готов ли AI отвечать (ключ, модель, бюджет) — для экрана AI-аналитика.</summary>
    [HttpGet("chat/status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var readiness = await gateway.GetReadinessAsync(ct);
        return Ok(new { readiness.Ready, readiness.Message });
    }

    [HttpPatch("conversations/{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, RenameRequest request, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(request.Title) ? BadRequest(new { error = "Название не может быть пустым." })
        : await chats.RenameAsync(User.GetUserId(), id, request.Title, ct) ? NoContent() : NotFound();

    [HttpDelete("conversations/{id:guid}")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct) =>
        await chats.ArchiveAsync(User.GetUserId(), id, ct) ? NoContent() : NotFound();

    [HttpPost("messages/{id:guid}/feedback")]
    public async Task<IActionResult> Feedback(Guid id, FeedbackRequest request, CancellationToken ct) =>
        request.Rating is not ("up" or "down") ? BadRequest(new { error = "Оценка — up или down." })
        : await chats.FeedbackAsync(User.GetUserId(), id, request.Rating, request.Comment, ct) ? NoContent() : NotFound(new { error = "Ответ не найден." });

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
                // «пульс» не влияет на ответ
            }
        });

        try
        {
            await chats.SendAsync(User.GetUserId(), request.ConversationId, message, Emit, work.Token, request.Context);
        }
        catch (KeyNotFoundException ex)
        {
            await Emit(new ChatEvent("error", new { error = ex.Message }));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Аналитик: ошибка чата");
            await Emit(new ChatEvent("error", new { error = "Внутренняя ошибка OneBase. Подробности — в журнале сервера." }));
        }
        finally
        {
            await stopPing.CancelAsync();
            await ping;
        }
    }
}
