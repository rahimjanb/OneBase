using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Twin;

namespace OneBase.Api.Controllers;

/// <summary>
/// Цифровой двойник (Operational Twin): снимок предприятия для 3D-модели территории и привязки зданий к отделам и складам. Что видно в
/// снимке, решают сервисы разделов по правам из токена (TwinService их только вызывает); привязки меняет администратор пользователей.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
[Route("api/twin")]
public sealed class TwinController(TwinService twin) : ControllerBase
{
    private IReadOnlySet<string> Permissions => User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet();

    [HttpGet]
    public async Task<TwinSnapshot> Snapshot(CancellationToken ct) => await twin.SnapshotAsync(User.GetUserId(), Permissions, ct);

    [HttpGet("options")]
    public async Task<TwinOptions> Options(CancellationToken ct) => await twin.OptionsAsync(Permissions, ct);

    [HttpPut("objects/{key}")]
    public async Task<TwinObjectView> Update(string key, TwinObjectInput input, CancellationToken ct) =>
        await twin.UpdateAsync(User.GetUserId(), Permissions, key, input, ct);
}
