using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneBase.AI.Gateway;
using OneBase.Api.Auth;
using OneBase.Application.Recovery;
using OneBase.Application.Security;
using OneBase.Infrastructure.Recovery;

namespace OneBase.Api.Controllers;

/// <summary>
/// Восстановление из резервной копии. mode — открыт всем: плашку «Копия для проверки» видно и на странице входа. check — проверка
/// восстановленной копии изнутри приложения (миграции, ключи Data Protection, расшифровка каждого вида секрета без вывода значений,
/// ссылки на файлы): движку восстановления — по заголовку X-Recovery-Token, равному Recovery:CheckToken (не короче 32 символов; без
/// настройки вход по токену выключен), администратору — по праву «Журнал ошибок».
/// </summary>
[ApiController]
[Route("api/recovery")]
public sealed class RecoveryController(RecoveryIsolation isolation, IConfiguration config) : ControllerBase
{
    public const string TokenHeader = "X-Recovery-Token";
    public const string TokenKey = "Recovery:CheckToken";
    public const int MinTokenLength = 32;

    [HttpGet("mode")]
    [AllowAnonymous]
    public IActionResult Mode() => Ok(new { isolation.Isolated, Banner = isolation.Isolated ? RecoveryIsolation.Banner : null });

    [HttpGet("check")]
    [AllowAnonymous]
    public async Task<IActionResult> Check([FromServices] RecoveryCheckService checks, [FromServices] AiSafetyOptions ai, CancellationToken ct)
    {
        if (!User.HasClaim(OneBaseClaims.Permission, Permissions.LogsRead) && !TokenMatches())
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        }

        var result = await checks.RunAsync(ct);
        return Ok(new
        {
            result.Ok,
            result.CheckedAt,
            result.ElapsedMs,
            result.Problems,
            Mode = new { result.Isolated, result.TelegramEnabled, AiPaidCallsBlocked = ai.BlockPaidCalls, result.DisabledWorkers },
            result.Database,
            result.Counts,
            result.DataProtection,
            result.Secrets,
            result.Files,
        });
    }

    private bool TokenMatches()
    {
        var expected = config[TokenKey];
        var given = Request.Headers[TokenHeader].ToString();
        return expected is { Length: >= MinTokenLength } && given.Length > 0
               && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(expected));
    }
}
