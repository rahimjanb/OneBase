using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using OneBase.Api.Auth;
using OneBase.Application.Sales.Access;

namespace OneBase.Api.Controllers;

/// <summary>
/// Страница или действие «Продаж» только для охвата «вся организация»: страна целиком (обзор, республика, первичка, экспорт «Завода»,
/// остатки, P&L, акт сверки…) и настройки. Пользователь с охватом РМ / СВР / ТП получает 403 — на клиенте это «Нет доступа»; страницы
/// своего охвата (направление, регион, ТП, магазин, списки по охвату) проверяются в самих действиях.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SalesOrganizationOnlyAttribute : Attribute, IAsyncActionFilter
{
    public const string Message = "Эта страница продаж открыта только сотрудникам с охватом «вся организация». Ваш охват — своя часть оргструктуры.";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var access = context.HttpContext.RequestServices.GetRequiredService<SalesAccess>();
        var scope = await access.ScopeAsync(context.HttpContext.User.GetUserId(), context.HttpContext.RequestAborted);
        if (!scope.IsOrganization)
        {
            context.Result = new ObjectResult(new { error = Message }) { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }
}

/// <summary>
/// Действие проверяет охват само (направление, регион, ТП, магазин — чужой id → 404; списки — только видимые люди). Метка для теста,
/// который требует, чтобы каждое действие «Продаж» было либо «только вся организация», либо с проверкой охвата, либо без данных.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SalesScopedAttribute : Attribute;

/// <summary>Действие без данных продаж (месяцы, ход синхронизации) — открыто любому с разделом «Продажи». Метка для того же теста.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SalesNoDataAttribute : Attribute;
