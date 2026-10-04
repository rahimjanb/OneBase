using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OneBase.Api.Auth;
using OneBase.Application.Field;
using OneBase.Application.Security;

namespace OneBase.Api.Controllers;

/// <summary>
/// Основа контроллеров Sales Base: область пользователя (FieldScope) строится из токена и состава Sales Base — никогда из
/// параметров запроса. Ошибки области и проверки превращаются в 403/404/400/409 с текстом для интерфейса.
/// </summary>
[ApiController]
[Authorize]
[FieldErrors]
public abstract class FieldControllerBase(FieldAccess access) : ControllerBase
{
    private FieldScope? _scope;

    protected async Task<FieldScope> ScopeAsync(CancellationToken ct)
    {
        if (_scope is not null)
        {
            return _scope;
        }

        var permissions = User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet();
        _scope = await access.ResolveAsync(User.GetUserId(), permissions.Contains(Permissions.FieldUse), permissions.Contains(Permissions.FieldManage), ct,
            permissions.Contains(Permissions.FieldPlan));
        return _scope;
    }

    protected HashSet<string> MyPermissions() => User.FindAll(OneBaseClaims.Permission).Select(c => c.Value).ToHashSet();
}

/// <summary>Исключения Sales Base → ответы с { error } (и details для конфликта).</summary>
public sealed class FieldErrorsAttribute : ExceptionFilterAttribute
{
    private const string Concurrent = "Это уже изменили одновременно с вами (например, повторное нажатие) — обновите страницу.";

    public override void OnException(ExceptionContext context)
    {
        IActionResult? result = context.Exception switch
        {
            FieldForbiddenException e => new ObjectResult(new { error = e.Message }) { StatusCode = StatusCodes.Status403Forbidden },
            FieldNotFoundException e => new NotFoundObjectResult(new { error = e.Message }),
            FieldValidationException e => new BadRequestObjectResult(new { error = e.Message }),
            FieldConflictException e => new ConflictObjectResult(new { error = e.Message, details = e.Details }),
            // Два одновременных запроса (двойное нажатие, две вкладки): уникальный индекс или изменённая строка — 409, а не 500.
            DbUpdateConcurrencyException => new ConflictObjectResult(new { error = Concurrent }),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => new ConflictObjectResult(new { error = Concurrent }),
            _ => null,
        };

        if (result is not null)
        {
            context.Result = result;
            context.ExceptionHandled = true;
        }
    }
}
