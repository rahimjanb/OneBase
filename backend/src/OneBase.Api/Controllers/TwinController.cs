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
public sealed class TwinController(TwinService twin, OneBase.Application.Sales.Access.SalesAccess sales) : ControllerBase
{
    /// <summary>
    /// Права из токена; охват продаж РМ / СВР / ТП — без «Продаж» и «Финансов»: в двойнике цифры всей страны (выручка, первичка, промо,
    /// оповещения), а свои цифры такой пользователь видит в разделе «Продажи».
    /// </summary>
    private async Task<IReadOnlySet<string>> PermissionsAsync(CancellationToken ct)
    {
        var own = User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet();
        if ((own.Contains(Application.Security.Permissions.SalesRead) || own.Contains(Application.Security.Permissions.FinanceRead))
            && !(await sales.ScopeAsync(User.GetUserId(), ct)).IsOrganization)
        {
            own.ExceptWith([Application.Security.Permissions.SalesRead, Application.Security.Permissions.FinanceRead, Application.Security.Permissions.SalesExport]);
        }

        return own;
    }

    [HttpGet]
    public async Task<TwinSnapshot> Snapshot(CancellationToken ct) => await twin.SnapshotAsync(User.GetUserId(), await PermissionsAsync(ct), ct);

    [HttpGet("options")]
    public async Task<TwinOptions> Options(CancellationToken ct) => await twin.OptionsAsync(await PermissionsAsync(ct), ct);

    [HttpPut("objects/{key}")]
    public async Task<TwinObjectView> Update(string key, TwinObjectInput input, CancellationToken ct) =>
        await twin.UpdateAsync(User.GetUserId(), await PermissionsAsync(ct), key, input, ct);
}
