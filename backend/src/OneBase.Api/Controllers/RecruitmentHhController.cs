using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Recruitment;
using OneBase.Application.Recruitment.Hh;
using OneBase.Infrastructure.Recruitment;

namespace OneBase.Api.Controllers;

/// <summary>
/// HR → «Подбор» → HH.uz: настройки приложения, вход работодателя (OAuth: адрес входа и завершение по code и state), вакансии HH и связь с
/// вакансиями OneBase, синхронизация откликов, действия по отклику. Ошибки HH — 400 с понятным текстом.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/hr/recruitment/hh")]
public sealed class RecruitmentHhController(RecruitmentService recruitment, HhService hh) : ControllerBase
{
    private Task<RecruitmentContext> ContextAsync(CancellationToken ct) =>
        recruitment.ContextAsync(User.GetUserId(), User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet(), ct);

    public sealed record CompleteInput(string Code, string State);
    public sealed record LinkInput(Guid VacancyId, string? HhVacancyId);
    public sealed record ActionInput(string ActionId, string? Message);

    [HttpGet]
    public async Task<HhStatus> Status(CancellationToken ct) => await hh.StatusAsync(await ContextAsync(ct), ct);

    [HttpPut("settings")]
    public async Task<HhStatus> Save(HhSettingsInput input, CancellationToken ct) => await hh.SaveSettingsAsync(await ContextAsync(ct), input, ct);

    [HttpPost("connect")]
    public Task<IActionResult> Connect(CancellationToken ct) => Run(async () => Ok(new { url = await hh.StartConnectAsync(await ContextAsync(ct), ct) }));

    /// <summary>Завершение входа: HH вернул пользователя на redirect_uri (страница OneBase) с code и state.</summary>
    [HttpPost("complete")]
    public Task<IActionResult> Complete(CompleteInput input, CancellationToken ct) =>
        Run(async () => Ok(await hh.CompleteConnectAsync(await ContextAsync(ct), input.Code, input.State, ct)));

    [HttpPost("disconnect")]
    public async Task<HhStatus> Disconnect(CancellationToken ct) => await hh.DisconnectAsync(await ContextAsync(ct), ct);

    [HttpGet("vacancies")]
    public Task<IActionResult> Vacancies(CancellationToken ct) => Run(async () => Ok(await hh.VacanciesAsync(await ContextAsync(ct), ct)));

    [HttpPost("link")]
    public async Task<IActionResult> Link(LinkInput input, CancellationToken ct)
    {
        await hh.LinkAsync(await ContextAsync(ct), input.VacancyId, input.HhVacancyId, ct);
        return NoContent();
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        var ctx = await ContextAsync(ct);
        if (!ctx.CanManage)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Синхронизацию HH запускает HR." });
        }

        return await Run(async () => Ok(await hh.SyncAsync(ctx.UserId, ct)));
    }

    /// <summary>Quota Guard: каталог операций HH (что бесплатно, что заблокировано и почему), счётчики за 30 дней, предохранитель, журнал запросов.</summary>
    [HttpGet("guard")]
    public async Task<HhGuardView> Guard(CancellationToken ct) => await hh.GuardAsync(await ContextAsync(ct), ct);

    /// <summary>«Проверить снова» — снять предохранитель расхода.</summary>
    [HttpPost("guard/reset")]
    public async Task<HhGuardView> ResetGuard(CancellationToken ct) => await hh.ResetGuardAsync(await ContextAsync(ct), ct);

    [HttpGet("candidates/{candidateId:guid}/negotiations")]
    public async Task<IReadOnlyList<HhNegotiationView>> Negotiations(Guid candidateId, CancellationToken ct) => await hh.NegotiationsAsync(await ContextAsync(ct), candidateId, ct);

    [HttpPost("negotiations/{id:guid}/action")]
    public Task<IActionResult> Action(Guid id, ActionInput input, CancellationToken ct) =>
        Run(async () => Ok(await hh.ActionAsync(await ContextAsync(ct), id, input.ActionId, input.Message, ct)));

    private static async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (HhApiException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message, hh = ex.Type });
        }
        catch (HhBlockedException ex)
        {
            return new BadRequestObjectResult(new { error = ex.Message, hh = "blocked", operation = ex.Operation });
        }
    }
}
