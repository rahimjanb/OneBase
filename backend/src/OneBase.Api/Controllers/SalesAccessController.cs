using Microsoft.AspNetCore.Mvc;
using OneBase.Api.Auth;
using OneBase.Application.Sales.Access;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

/// <summary>
/// Охват продаж текущего пользователя (что ему открыто в разделе «Продажи») и назначение охвата пользователям. Охват считает сервер из
/// назначения и оргструктуры — меню и кнопки строятся по нему, но проверяет каждый запрос всё равно API.
/// </summary>
[ApiController]
[FieldErrors]
public sealed class SalesAccessController(SalesAccess access, SalesScopeAdmin admin) : ControllerBase
{
    /// <summary>Мой охват: уровень, направления, регионы, ТП, подпись; что можно (выгрузки, настройки, финансы).</summary>
    public sealed record AccessView(string Level, string Title, IReadOnlyList<Guid> DirectionIds, IReadOnlyList<Guid> RegionIds, long? AgentId, bool CanExport,
        bool CanManage, bool CanSeeFinance, string Home);

    [HttpGet("api/sales/access")]
    [HasPermission(Permissions.SalesRead)]
    public async Task<AccessView> Mine(CancellationToken ct)
    {
        var scope = await access.ScopeAsync(User.GetUserId(), ct);
        var title = await admin.TitleAsync(scope.Level, scope.DirectionIds, scope.RegionIds, scope.AgentId, ct);
        bool Has(string p) => User.HasClaim(OneBaseClaims.Permission, p);
        return new AccessView(scope.Level?.ToString() ?? "Organization", title, scope.DirectionIds.Order().ToList(), scope.RegionIds.Order().ToList(), scope.AgentId,
            Has(Permissions.SalesExport), Has(Permissions.SalesManage), Has(Permissions.FinanceRead), await admin.HomeAsync(scope, ct));
    }

    [HttpGet("api/users/sales-scope/options")]
    [HasPermission(Permissions.UsersManage)]
    public Task<SalesScopeOptions> Options(CancellationToken ct) => admin.OptionsAsync(ct);

    [HttpGet("api/users/{id:guid}/sales-scope")]
    [HasPermission(Permissions.UsersManage)]
    public Task<SalesScopeView> Get(Guid id, CancellationToken ct) => admin.GetAsync(id, ct);

    /// <summary>Назначить охват: level = null — вся организация; Rm — directionIds; Supervisor — regionIds; Agent — agentId. В журнал аудита.</summary>
    [HttpPut("api/users/{id:guid}/sales-scope")]
    [HasPermission(Permissions.UsersManage)]
    public Task<SalesScopeView> Put(Guid id, SalesScopeInput input, CancellationToken ct) => admin.SetAsync(User.GetUserId(), id, input, ct);
}
