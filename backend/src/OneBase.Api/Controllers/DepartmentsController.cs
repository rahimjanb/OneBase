using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Work;

namespace OneBase.Api.Controllers;

/// <summary>
/// «Настройки → Отделы»: отделы компании, руководители, пользователи для выбора. Маршрут согласования заявок снабжения — в
/// api/procurement/settings (право «Настройки снабжения»). Права решает DepartmentSettingsService по правам из токена.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/settings/departments")]
public sealed class DepartmentsController(DepartmentSettingsService departments) : ControllerBase
{
    private IReadOnlySet<string> Permissions => User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet();

    [HttpGet]
    public async Task<DepartmentsSettingsView> List(CancellationToken ct) => await departments.ListAsync(Permissions, ct);

    [HttpGet("users")]
    public async Task<List<DepartmentPerson>> Users(CancellationToken ct) => await departments.UsersAsync(Permissions, ct);

    public sealed record HeadsInput(IReadOnlyList<Guid>? UserIds);

    [HttpPut("{code}/heads")]
    public async Task<IActionResult> SetHeads(string code, HeadsInput input, CancellationToken ct)
    {
        await departments.SetHeadsAsync(User.GetUserId(), Permissions, code, input.UserIds ?? [], ct);
        return NoContent();
    }
}
