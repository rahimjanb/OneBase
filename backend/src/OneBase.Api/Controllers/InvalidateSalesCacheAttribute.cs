using Microsoft.AspNetCore.Mvc.Filters;
using OneBase.Application.Sales;

namespace OneBase.Api.Controllers;

/// <summary>После успешного изменения (не GET) сбрасывает кэш расчётов продаж.</summary>
public sealed class InvalidateSalesCacheAttribute : ActionFilterAttribute
{
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (!HttpMethods.IsGet(context.HttpContext.Request.Method) && context.Exception is null)
        {
            context.HttpContext.RequestServices.GetRequiredService<SalesCacheSignal>().Invalidate();
        }
    }
}
