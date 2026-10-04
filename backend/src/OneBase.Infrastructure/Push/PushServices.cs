using System.Buffers.Text;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneBase.Domain.Field;
using OneBase.Domain.Work;
using OneBase.Infrastructure.Persistence;
using WebPushSubscription = Lib.Net.Http.WebPush.PushSubscription;

namespace OneBase.Infrastructure.Push;

/// <summary>Что отправить: получатель — пользователь OneBase или участник Sales Base (push уйдёт его пользователю).</summary>
public sealed record PushNotice(Guid? UserId, Guid? FieldMemberId, string Title, string? Body, string? Link, Guid Tag);

/// <summary>Очередь push-уведомлений: заполняется после сохранения, разбирается фоновой отправкой.</summary>
public sealed class PushQueue
{
    private readonly Channel<PushNotice> _channel = Channel.CreateBounded<PushNotice>(new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(PushNotice notice) => _channel.Writer.TryWrite(notice);

    public IAsyncEnumerable<PushNotice> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>Ключи VAPID: открытый (для подписки в браузере) и закрытый (подпись запросов к push-сервисам).</summary>
public sealed record VapidKeys(string PublicKey, string PrivateKey, string Subject);

/// <summary>
/// Ключи VAPID из базы (work.PushKeys): создаются один раз, закрытый ключ хранится зашифрованным (Data Protection).
/// Если строка есть, но расшифровать её нельзя, — push отключается с ошибкой в журнале, ключи НЕ пересоздаются:
/// новые ключи сломали бы все подписки браузеров.
/// </summary>
public sealed class PushKeyProvider(IServiceScopeFactory scopes, IDataProtectionProvider dataProtection, IConfiguration config, ILogger<PushKeyProvider> logger)
{
    private readonly IDataProtector _protector = dataProtection.CreateProtector("OneBase.Push.Vapid");
    private readonly SemaphoreSlim _lock = new(1, 1);
    private VapidKeys? _keys;
    private bool _broken;

    public async Task<VapidKeys?> GetAsync(CancellationToken ct)
    {
        if (_keys is not null || _broken)
        {
            return _keys;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_keys is not null || _broken)
            {
                return _keys;
            }

            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
            var row = await db.PushKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Id == 1, ct);
            if (row is null)
            {
                using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                var p = ec.ExportParameters(true);
                byte[] publicKey = [0x04, .. p.Q.X!, .. p.Q.Y!];
                var privateKey = Base64Url.EncodeToString(p.D!);
                row = new PushKeys
                {
                    PublicKey = Base64Url.EncodeToString(publicKey),
                    ProtectedPrivateKey = _protector.Protect(privateKey),
                    // Apple отклоняет subject не вида mailto:/https: (и localhost) — берём настройку или основной домен.
                    Subject = config["Push:Subject"] is { Length: > 0 } subject ? subject : "https://1base.uz",
                };
                db.PushKeys.Add(row);
                try
                {
                    await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException)
                {
                    // Ключи одновременно создал другой экземпляр API — берём его.
                    db.ChangeTracker.Clear();
                    row = await db.PushKeys.AsNoTracking().FirstAsync(k => k.Id == 1, ct);
                }

                logger.LogInformation("Push: созданы ключи VAPID");
            }

            try
            {
                _keys = new VapidKeys(row.PublicKey, _protector.Unprotect(row.ProtectedPrivateKey), row.Subject);
            }
            catch (CryptographicException ex)
            {
                _broken = true;
                logger.LogError(ex, "Push: не удалось расшифровать ключ VAPID (ключи Data Protection изменились). Push-уведомления отключены; ключи не пересоздаются, чтобы не сломать подписки.");
            }

            return _keys;
        }
        finally
        {
            _lock.Release();
        }
    }
}

/// <summary>
/// После сохранения ставит в очередь push для каждого нового уведомления (field.Notifications и work.Notifications).
/// Отправка — в фоне: задержки и ошибки push-сервисов не замедляют и не ломают запрос, который создал уведомление.
/// </summary>
public sealed class PushNotificationInterceptor(PushQueue queue) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, List<PushNotice>> _pending = new();

    private void Collect(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var notices = context.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity switch
            {
                FieldNotification f => new PushNotice(null, f.RecipientId, f.Title, f.Body, f.Link, f.Id),
                UserNotification u => new PushNotice(u.RecipientId, null, u.Title, u.Body, u.Link, u.Id),
                _ => null,
            })
            .Where(n => n is not null)
            .Select(n => n!)
            .ToList();
        if (notices.Count > 0)
        {
            _pending.AddOrUpdate(context, notices);
        }
    }

    private void Flush(DbContext? context)
    {
        if (context is not null && _pending.TryGetValue(context, out var notices))
        {
            _pending.Remove(context);
            foreach (var n in notices)
            {
                queue.Enqueue(n);
            }
        }
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Flush(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Flush(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null)
        {
            _pending.Remove(eventData.Context);
        }
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        SaveChangesFailed(eventData);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Фоновая отправка Web Push всем подпискам получателя (телефон, компьютер, оба сайта). Push — «по возможности»:
/// источник правды — уведомление в базе (колокольчик). Политика ошибок: 404/410 (подписки нет) и 403 с другим ключом —
/// подписка удаляется; 429/5xx — счётчик неудач; остальные 4xx — ошибка настройки в журнал.
/// </summary>
public sealed class PushSender(
    PushQueue queue,
    PushKeyProvider keys,
    IServiceScopeFactory scopes,
    IVapidTokenCache tokenCache,
    ILogger<PushSender> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private VapidAuthentication? _vapid;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var notice in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await SendAsync(notice, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Push: ошибка отправки уведомления {Tag}", notice.Tag);
            }
        }
    }

    private async Task SendAsync(PushNotice notice, CancellationToken ct)
    {
        if (await keys.GetAsync(ct) is not { } vapidKeys)
        {
            return;
        }

        _vapid ??= new VapidAuthentication(vapidKeys.PublicKey, vapidKeys.PrivateKey) { Subject = vapidKeys.Subject, TokenCache = tokenCache };

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OneBaseDbContext>();
        var userId = notice.UserId ?? (notice.FieldMemberId is { } member
            ? await db.FieldMembers.AsNoTracking().Where(m => m.Id == member && m.IsActive).Select(m => m.UserId).FirstOrDefaultAsync(ct)
            : null);
        // У участника ещё нет входа — уведомление ждёт в колокольчике. Отключённому пользователю push не шлём.
        if (userId is null || !await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive, ct))
        {
            return;
        }

        var subscriptions = await db.PushSubscriptions.Where(s => s.UserId == userId).ToListAsync(ct);
        if (subscriptions.Count == 0)
        {
            return;
        }

        var client = scope.ServiceProvider.GetRequiredService<PushServiceClient>();
        // Повтор по Retry-After ждал бы сколько скажет сервер (хоть сутки) и останавливал рассылку всем — повторов нет.
        client.AutoRetryAfter = false;
        foreach (var row in subscriptions)
        {
            var subscription = new WebPushSubscription { Endpoint = row.Endpoint };
            subscription.SetKey(PushEncryptionKeyName.P256DH, row.P256dh);
            subscription.SetKey(PushEncryptionKeyName.Auth, row.Auth);
            var message = new PushMessage(Payload(notice, row.Origin))
            {
                Urgency = PushMessageUrgency.High,
                TimeToLive = 86400, // Apple требует положительный TTL
                Topic = notice.Tag.ToString("N"), // до 32 символов base64url
            };

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                await client.RequestPushMessageDeliveryAsync(subscription, message, _vapid, timeout.Token);
                row.LastSuccessAt = DateTimeOffset.UtcNow;
                row.Failures = 0;
            }
            catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone
                                                        || (ex.StatusCode == HttpStatusCode.Forbidden && ex.Body?.Contains("VapidPkHashMismatch") == true))
            {
                db.PushSubscriptions.Remove(row);
            }
            catch (PushServiceClientException ex) when ((int)ex.StatusCode == 429 || (int)ex.StatusCode >= 500)
            {
                row.Failures++;
                logger.LogWarning("Push: push-сервис временно не принял уведомление ({Status})", (int)ex.StatusCode);
            }
            catch (PushServiceClientException ex)
            {
                row.Failures++;
                logger.LogWarning("Push: push-сервис отклонил уведомление ({Status}): {Body}", (int)ex.StatusCode, ex.Body);
            }
            catch (HttpRequestException ex)
            {
                row.Failures++;
                logger.LogWarning(ex, "Push: нет связи с push-сервисом");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                row.Failures++;
                logger.LogWarning("Push: push-сервис не ответил за 15 секунд");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Битые ключи подписки и прочее — проблема одного устройства, остальные получают уведомление.
                row.Failures++;
                logger.LogWarning(ex, "Push: не удалось отправить на устройство");
            }

            if (row.Failures >= 10 && db.Entry(row).State != EntityState.Deleted)
            {
                db.PushSubscriptions.Remove(row);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Формат Declarative Web Push (iOS 18.4+ показывает его и без service worker); остальные браузеры передают его в sw.js,
    /// который читает data.notification. Ссылка — абсолютная на сайте, где сделана подписка.
    /// </summary>
    private static string Payload(PushNotice notice, string? origin)
    {
        var link = notice.Link ?? "/";
        var navigate = link.StartsWith('/') && Uri.TryCreate(origin, UriKind.Absolute, out var site) ? SiteFor(site, link) + link : link;
        return JsonSerializer.Serialize(new
        {
            web_push = 8030,
            notification = new
            {
                title = Trim(notice.Title, 120),
                body = Trim(notice.Body, 240),
                navigate,
                lang = "ru",
                tag = notice.Tag.ToString("N"),
            },
        }, Json);
    }

    private static string? Trim(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..(max - 1)] + "…";

    /// <summary>
    /// Сайт для ссылки: страницы OneBase (не /field) на домене Sales Base не открываются (там всё переписывается в /field),
    /// поэтому для подписки с sales.* такие ссылки ведут на основной домен (sales.1base.uz → 1base.uz). Корень «/» — главная
    /// того сайта, где сделана подписка (уведомление без ссылки, например проверочное).
    /// </summary>
    private static string SiteFor(Uri site, string link)
    {
        var host = site.Host;
        if (link != "/" && !link.StartsWith("/field", StringComparison.Ordinal) && host.StartsWith("sales.", StringComparison.OrdinalIgnoreCase))
        {
            host = host["sales.".Length..];
        }

        return site.IsDefaultPort ? $"{site.Scheme}://{host}" : $"{site.Scheme}://{host}:{site.Port}";
    }
}
