using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Domain.Audit;
using OneBase.Domain.Telegram;

namespace OneBase.Application.Telegram.Routing;

/// <summary>Подразделение в настройках аккаунта: включено ли, по умолчанию ли, есть ли у него AI-обработчик сообщений.</summary>
public sealed record TelegramDepartmentOption(string Code, string Name, bool Enabled, bool IsDefault, bool HasHandler);

/// <summary>Configured = false — подразделения не заданы: аккаунт целиком у продаж (как до маршрутизации).</summary>
public sealed record TelegramDepartmentsView(bool Configured, IReadOnlyList<TelegramDepartmentOption> Departments);

/// <summary>Departments — коды включённых подразделений; Default — кому новые диалоги без признака (null — «не распределён»).</summary>
public sealed record TelegramDepartmentsInput(IReadOnlyList<string> Departments, string? Default);

/// <summary>
/// Подразделения аккаунта Telegram: кто работает с этим номером (продажи, HR, …) и кому новые диалоги. Сам аккаунт, его вход и сессия
/// не меняются. Каждое изменение — в журнал аудита.
/// </summary>
public sealed class TelegramDepartmentService(IAppDbContext db, IEnumerable<ITelegramDepartmentHandler> handlers, IAuditLogger audit, TimeProvider clock)
{
    public async Task<TelegramDepartmentsView> GetAsync(Guid accountId, CancellationToken ct = default)
    {
        await EnsureAccountAsync(accountId, ct);
        var rows = await db.TelegramAccountDepartments.AsNoTracking().Where(d => d.AccountId == accountId).ToListAsync(ct);
        var configured = rows.Count > 0;
        var known = handlers.Select(h => h.Department).ToHashSet(StringComparer.Ordinal);
        if (known.Contains(TelegramDepartments.Any))
        {
            // Отделы с включённым ИИ-агентом («Настройки → ИИ-агенты») — тоже с AI.
            known.UnionWith(await db.DialogAgents.AsNoTracking().Where(a => a.Enabled && a.Kind == OneBase.Domain.AI.DialogAgentKinds.Assistant).Select(a => a.Department).ToListAsync(ct));
        }
        var departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name).Select(d => new { d.Code, d.Name }).ToListAsync(ct);
        return new TelegramDepartmentsView(configured, departments.Select(d =>
        {
            var row = rows.FirstOrDefault(r => r.Department == d.Code);
            var enabled = configured ? row is not null : d.Code == TelegramDepartments.Sales;
            var isDefault = configured ? row?.IsDefault == true : d.Code == TelegramDepartments.Sales;
            return new TelegramDepartmentOption(d.Code, d.Name, enabled, isDefault, known.Contains(d.Code));
        }).ToList());
    }

    public async Task<TelegramDepartmentsView> SaveAsync(Guid accountId, TelegramDepartmentsInput input, Guid userId, CancellationToken ct = default)
    {
        await EnsureAccountAsync(accountId, ct);
        var codes = (input.Departments ?? []).Select(c => c?.Trim() ?? string.Empty).Where(c => c.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (codes.Count == 0)
        {
            throw new TelegramException("Выберите хотя бы одно подразделение.", TelegramErrorKind.Config);
        }

        var existing = await db.Departments.AsNoTracking().Where(d => codes.Contains(d.Code)).Select(d => d.Code).ToListAsync(ct);
        if (codes.FirstOrDefault(c => !existing.Contains(c)) is { } unknown)
        {
            throw new TelegramException($"Подразделение «{unknown}» не найдено.", TelegramErrorKind.Config);
        }

        var fallback = string.IsNullOrWhiteSpace(input.Default) ? null : input.Default.Trim();
        if (fallback is not null && !codes.Contains(fallback))
        {
            throw new TelegramException("Подразделение по умолчанию должно быть среди выбранных.", TelegramErrorKind.Config);
        }

        var rows = await db.TelegramAccountDepartments.Where(d => d.AccountId == accountId).ToListAsync(ct);
        var before = rows.Select(d => d.Department).OrderBy(c => c, StringComparer.Ordinal).ToList();
        var beforeDefault = rows.FirstOrDefault(d => d.IsDefault)?.Department;
        var now = clock.GetUtcNow();
        db.TelegramAccountDepartments.RemoveRange(rows.Where(r => !codes.Contains(r.Department)));
        foreach (var code in codes)
        {
            var row = rows.FirstOrDefault(r => r.Department == code);
            if (row is null)
            {
                row = new TelegramAccountDepartment { AccountId = accountId, Department = code };
                db.TelegramAccountDepartments.Add(row);
            }

            row.IsDefault = code == fallback;
            row.UpdatedAt = now;
            row.UpdatedById = userId;
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), "telegram.account.departments", nameof(TelegramAccount), accountId.ToString(),
            new
            {
                before,
                beforeDefault,
                after = codes.OrderBy(c => c).ToList(),
                afterDefault = fallback,
            }, ct);
        return await GetAsync(accountId, ct);
    }

    private async Task EnsureAccountAsync(Guid accountId, CancellationToken ct)
    {
        if (!await db.TelegramAccounts.AnyAsync(a => a.Id == accountId, ct))
        {
            throw new KeyNotFoundException("Аккаунт Telegram не найден.");
        }
    }
}
