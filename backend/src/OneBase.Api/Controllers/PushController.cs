using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneBase.Api.Auth;
using OneBase.Application.Abstractions;
using OneBase.Application.Work;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Push;

namespace OneBase.Api.Controllers;

/// <summary>
/// Web Push: открытый ключ VAPID для подписки, сохранение и удаление подписки устройства, проверочное уведомление.
/// Подписка привязывается к вошедшему пользователю; тот же браузер после входа другого пользователя переходит к нему.
/// </summary>
[ApiController]
[Authorize]
[Route("api/push")]
public sealed class PushController(IAppDbContext db, PushKeyProvider keys, UserNotifier notifier, IMemoryCache cache) : ControllerBase
{
    /// <summary>Подписок на пользователя (устройства × сайты); лишние — самые давние.</summary>
    private const int MaxSubscriptionsPerUser = 10;

    /// <summary>
    /// Адреса push-сервисов браузеров. Чужой адрес не принимаем: иначе сервер слал бы запросы куда укажет пользователь
    /// (в том числе во внутреннюю сеть), а «тормозящий» адрес задерживал бы доставку всем.
    /// </summary>
    private static readonly string[] PushHosts = ["fcm.googleapis.com", "android.googleapis.com", "updates.push.services.mozilla.com", "web.push.apple.com"];

    private static readonly string[] PushHostSuffixes = [".push.services.mozilla.com", ".push.apple.com", ".notify.windows.com"];

    private static bool IsPushService(Uri uri) =>
        uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort
        && (PushHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) || PushHostSuffixes.Any(s => uri.Host.EndsWith(s, StringComparison.OrdinalIgnoreCase)));

    private static bool IsBase64Url(string s) => s.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_' || c == '=');

    [HttpGet("key")]
    public async Task<IActionResult> Key(CancellationToken ct) =>
        await keys.GetAsync(ct) is { } k ? Ok(new { publicKey = k.PublicKey }) : StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Push-уведомления сейчас недоступны." });

    public sealed record SubscriptionKeys(string? P256dh, string? Auth);

    /// <summary>origin и userAgent присылает браузер: BFF не пересылает эти заголовки.</summary>
    public sealed record SubscriptionInput(string? Endpoint, SubscriptionKeys? Keys, string? Origin, string? UserAgent);

    [HttpPost("subscriptions")]
    public async Task<IActionResult> Subscribe(SubscriptionInput input, CancellationToken ct)
    {
        var endpoint = input.Endpoint?.Trim();
        if (endpoint is not { Length: > 0 and <= 1000 } || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || !IsPushService(uri)
            || input.Keys?.P256dh is not { Length: > 0 and <= 200 } p256dh || input.Keys.Auth is not { Length: > 0 and <= 100 } auth
            || !IsBase64Url(p256dh) || !IsBase64Url(auth))
        {
            return BadRequest(new { error = "Неверная подписка браузера." });
        }

        var origin = Uri.TryCreate(input.Origin, UriKind.Absolute, out var o) && o.Scheme is "https" or "http" ? o.GetLeftPart(UriPartial.Authority) : null;
        var me = User.GetUserId();
        var row = await db.PushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);
        if (row is null)
        {
            db.PushSubscriptions.Add(new PushSubscription
            {
                UserId = me,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                Origin = origin,
                UserAgent = input.UserAgent is { Length: > 500 } ua ? ua[..500] : input.UserAgent,
            });
        }
        else
        {
            // Тот же браузер: обновляем ключи и владельца (на общем телефоне вошёл другой сотрудник).
            row.UserId = me;
            row.P256dh = p256dh;
            row.Auth = auth;
            row.Origin = origin ?? row.Origin;
            row.UserAgent = input.UserAgent is { Length: > 500 } ua ? ua[..500] : input.UserAgent ?? row.UserAgent;
            row.Failures = 0;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Тот же endpoint одновременно сохранил второй запрос (две вкладки) — подписка уже есть.
        }

        // Не больше MaxSubscriptionsPerUser: самые давно работавшие удаляем.
        var extra = await db.PushSubscriptions.Where(s => s.UserId == me)
            .OrderByDescending(s => s.LastSuccessAt ?? s.UpdatedAt ?? s.CreatedAt)
            .Skip(MaxSubscriptionsPerUser).Select(s => s.Id).ToListAsync(ct);
        if (extra.Count > 0)
        {
            await db.PushSubscriptions.Where(s => extra.Contains(s.Id)).ExecuteDeleteAsync(ct);
        }

        return NoContent();
    }

    /// <summary>Отписка устройства (выход, «Отключить уведомления»): удаляется только своя подписка.</summary>
    [HttpDelete("subscriptions")]
    public async Task<IActionResult> Unsubscribe([FromQuery] string endpoint, CancellationToken ct)
    {
        var me = User.GetUserId();
        await db.PushSubscriptions.Where(s => s.Endpoint == endpoint && s.UserId == me).ExecuteDeleteAsync(ct);
        return NoContent();
    }

    /// <summary>Проверка: уведомление себе — в колокольчик и push на все свои устройства.</summary>
    [HttpPost("test")]
    public async Task<IActionResult> Test(CancellationToken ct)
    {
        var me = User.GetUserId();
        // Проверка — не чаще раза в 30 секунд, чтобы ею нельзя было забить очередь уведомлений.
        if (cache.TryGetValue(("push-test", me), out _))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "Проверку можно повторить через полминуты." });
        }

        cache.Set(("push-test", me), true, TimeSpan.FromSeconds(30));
        var devices = await db.PushSubscriptions.CountAsync(s => s.UserId == me, ct);
        notifier.Notify(me, UserNotificationKind.TaskChanged, "Проверка уведомлений", "Если вы видите это на телефоне — уведомления работают.", null);
        await db.SaveChangesAsync(ct);
        return Ok(new { devices });
    }
}
