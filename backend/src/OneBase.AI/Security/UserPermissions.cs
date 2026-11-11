using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Security;

namespace OneBase.AI.Security;

/// <summary>
/// Права пользователя, от имени которого работает AI. AI видит только то, что видит сам пользователь:
/// инструмент или AI-сотрудник с правом, которого у пользователя нет, данных не получает. Права — роли с учётом отмеченных пользователю
/// разделов (UserSections): раздел снят — AI-сотрудник и инструменты этого раздела ему не отвечают.
/// </summary>
public interface IUserPermissions
{
    /// <summary>Права активного пользователя; отключённый или несуществующий — пустой набор.</summary>
    Task<IReadOnlySet<string>> GetAsync(Guid userId, CancellationToken ct = default);
}

internal sealed class UserPermissions(IAppDbContext db, OneBase.Application.Sales.Access.SalesAccess sales) : IUserPermissions
{
    private readonly Dictionary<Guid, IReadOnlySet<string>> _cache = [];

    public async Task<IReadOnlySet<string>> GetAsync(Guid userId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        IReadOnlySet<string> set = await db.EffectivePermissionsAsync(userId, ct) ?? new HashSet<string>(StringComparer.Ordinal);
        set = await LimitSalesAsync(userId, set, ct);
        _cache[userId] = set;
        return set;
    }

    /// <summary>
    /// Охват продаж РМ / СВР / ТП (Sales.Access): инструменты AI, оповещения и сводки считают по всей стране и охват не применяют — от имени
    /// такого пользователя AI данных продаж и финансов не получает. Свои цифры он видит на страницах раздела «Продажи», где охват проверяет
    /// сервер. Ключ кэша ответов консультанта строится по этим же правам — смена охвата не отдаст прежний ответ.
    /// </summary>
    private async Task<IReadOnlySet<string>> LimitSalesAsync(Guid userId, IReadOnlySet<string> set, CancellationToken ct)
    {
        if (!set.Contains(Permissions.SalesRead) && !set.Contains(Permissions.FinanceRead))
        {
            return set;
        }

        if ((await sales.ScopeAsync(userId, ct)).IsOrganization)
        {
            return set;
        }

        return set.Where(p => p is not (Permissions.SalesRead or Permissions.FinanceRead or Permissions.SalesExport)).ToHashSet(StringComparer.Ordinal);
    }
}
