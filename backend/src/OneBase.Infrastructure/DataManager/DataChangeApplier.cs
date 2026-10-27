using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using OneBase.Application.Abstractions;
using OneBase.Application.DataManager;
using OneBase.Application.DataSources;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Pnl;
using OneBase.Application.Security;
using OneBase.Domain.AI;
using OneBase.Domain.Audit;
using OneBase.Domain.Sales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.DataManager;

/// <summary>
/// Применение и откат плана изменений AI Data Manager. Одна транзакция (Serializable) на весь план:
/// <list type="bullet">
/// <item>план «захватывается» условным UPDATE по статусу — второе нажатие или повторный запрос его не применит ещё раз;</item>
/// <item>перед записью каждое значение сверяется со значением «до» из плана: если данные изменились, ничего не пишется,
/// план помечается Stale и готовится заново;</item>
/// <item>откат возвращает «до» только там, где сейчас всё ещё «после» — чужие более новые изменения не затираются.</item>
/// </list>
/// Пишутся только данные OneBase (sales.RegionPlans, sales.Targets, профиль региона, sales.PnlValues) — копия Linko не трогается.
/// Поле, которое ведёт привязка Google Sheets, меняет только план её синхронизации (FieldOwnership) — проверяется и при применении.
/// </summary>
internal sealed class DataChangeApplier(
    OneBaseDbContext db,
    SalesCacheSignal cacheSignal,
    IAuditLogger audit,
    FieldOwnership ownership,
    ILogger<DataChangeApplier> logger) : IDataChangeApplier
{
    public async Task<ApplyOutcome> ApplyAsync(Guid changeSetId, Guid userId, CancellationToken ct = default)
    {
        await RequireAsync(userId, ct);
        db.ChangeTracker.Clear(); // свежие значения из базы: статус и версия плана меняются условным UPDATE в обход трекера
        await EnsureOwnerAsync(changeSetId, userId, ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var claimed = await db.DataChangeSets
                .Where(s => s.Id == changeSetId && s.Status == DataChangeSetStatus.Draft)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, DataChangeSetStatus.Applying), ct);
            if (claimed == 0)
            {
                await tx.RollbackAsync(ct);
                return await AlreadyHandledAsync(changeSetId, applying: true, ct);
            }

            var set = await db.DataChangeSets.Include(s => s.Items).FirstAsync(s => s.Id == changeSetId, ct);
            var work = set.Items.Where(i => i.Action is DataChangeAction.Create or DataChangeAction.Update or DataChangeAction.Delete)
                .OrderBy(i => i.Position)
                .ToList();
            if (work.Count == 0)
            {
                await tx.RollbackAsync(ct);
                throw new DataManagerException("В плане нет строк для применения.");
            }

            // Данные изменились после подготовки — план устарел целиком.
            var stale = new List<string>();
            foreach (var item in work)
            {
                var current = await CurrentAsync(item.EntityKey, ct);
                if (!Same(item.EntityType, current, item.Before))
                {
                    stale.Add($"{item.Label}: в плане «было» {item.Before ?? "нет"}, сейчас {current ?? "нет"}");
                }
            }

            if (stale.Count > 0)
            {
                set.Status = DataChangeSetStatus.Stale;
                set.Error = $"Данные изменились после подготовки плана ({stale.Count} стр.). Подготовьте план заново.";
                set.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                await audit.LogAsync(ActorType.User, userId.ToString(), "ai.data.stale", "data_change_set", changeSetId.ToString(),
                    new { set.Target, stale = stale.Take(20) }, ct);
                return new ApplyOutcome(DataChangeSetStatus.Stale, 0, 0, set.Error, stale);
            }

            foreach (var item in work)
            {
                await WriteAsync(item.EntityType, item.EntityKey, item.Action == DataChangeAction.Delete ? null : item.After, ct);
                item.Status = DataChangeItemStatus.Applied;
            }

            set.Status = DataChangeSetStatus.Applied;
            set.AppliedAt = DateTimeOffset.UtcNow;
            set.AppliedById = userId;
            set.Error = null;
            set.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            cacheSignal.Invalidate();
            var skipped = set.Items.Count - work.Count;
            await audit.LogAsync(ActorType.User, userId.ToString(), "ai.data.applied", "data_change_set", changeSetId.ToString(), new
            {
                set.Target, set.PlanKind, set.Period, set.FileSha256, applied = work.Count, skipped,
                created = work.Count(i => i.Action == DataChangeAction.Create),
                updated = work.Count(i => i.Action == DataChangeAction.Update),
                deleted = work.Count(i => i.Action == DataChangeAction.Delete),
            }, ct);
            return new ApplyOutcome(DataChangeSetStatus.Applied, work.Count, skipped, $"Применено строк: {work.Count}.", []);
        }
        catch (DataManagerException)
        {
            db.ChangeTracker.Clear(); // транзакция откатилась — несохранённые строки не должны попасть в следующее сохранение
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FailedAsync(changeSetId, userId, ex, "ai.data.apply_failed", applying: true, ct);
        }
    }

    public async Task<ApplyOutcome> RollbackAsync(Guid changeSetId, Guid userId, CancellationToken ct = default)
    {
        await RequireAsync(userId, ct);
        db.ChangeTracker.Clear(); // свежие значения из базы: статус и версия плана меняются условным UPDATE в обход трекера
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var claimed = await db.DataChangeSets
                .Where(s => s.Id == changeSetId && s.Status == DataChangeSetStatus.Applied)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, DataChangeSetStatus.Applying), ct);
            if (claimed == 0)
            {
                await tx.RollbackAsync(ct);
                return await AlreadyHandledAsync(changeSetId, applying: false, ct);
            }

            var set = await db.DataChangeSets.Include(s => s.Items).FirstAsync(s => s.Id == changeSetId, ct);
            var reverted = 0;
            var conflicts = new List<string>();
            foreach (var item in set.Items.Where(i => i.Status == DataChangeItemStatus.Applied).OrderByDescending(i => i.Position))
            {
                var expectedNow = item.Action == DataChangeAction.Delete ? null : item.After;
                var current = await CurrentAsync(item.EntityKey, ct);
                if (!Same(item.EntityType, current, expectedNow))
                {
                    // С тех пор значение изменил кто-то другой — его более новое изменение сохраняется.
                    item.Status = DataChangeItemStatus.RollbackConflict;
                    conflicts.Add($"{item.Label}: ожидалось {expectedNow ?? "нет"}, сейчас {current ?? "нет"} — оставлено как есть");
                    continue;
                }

                await WriteAsync(item.EntityType, item.EntityKey, item.Before, ct);
                item.Status = DataChangeItemStatus.RolledBack;
                reverted++;
            }

            set.Status = conflicts.Count == 0 ? DataChangeSetStatus.RolledBack : DataChangeSetStatus.PartiallyRolledBack;
            set.RolledBackAt = DateTimeOffset.UtcNow;
            set.RolledBackById = userId;
            set.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            cacheSignal.Invalidate();
            await audit.LogAsync(ActorType.User, userId.ToString(), "ai.data.rolled_back", "data_change_set", changeSetId.ToString(),
                new { set.Target, set.PlanKind, set.Period, reverted, conflicts = conflicts.Take(20) }, ct);
            return new ApplyOutcome(set.Status, reverted, conflicts.Count,
                conflicts.Count == 0 ? $"Откачено строк: {reverted}." : $"Откачено строк: {reverted}; оставлено из-за более новых изменений: {conflicts.Count}.",
                conflicts);
        }
        catch (DataManagerException)
        {
            db.ChangeTracker.Clear();
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await FailedAsync(changeSetId, userId, ex, "ai.data.rollback_failed", applying: false, ct);
        }
    }

    /// <summary>Применить и откатить план может только активный пользователь с ai.data.manage — проверяется по базе, а не по токену.</summary>
    /// <summary>
    /// Владелец полей на момент применения: план из чата или файла не меняет поле, которое с тех пор стала вести привязка Google Sheets,
    /// а план синхронизации — поле чужой привязки. Отказ — в журнал конфликтов; план остаётся черновиком, данные не меняются.
    /// </summary>
    private async Task EnsureOwnerAsync(Guid changeSetId, Guid userId, CancellationToken ct)
    {
        var set = await db.DataChangeSets.AsNoTracking()
            .Where(s => s.Id == changeSetId)
            .Select(s => new { s.Target, s.PlanKind, s.Period, s.Title, s.SourceBindingId, s.Status })
            .FirstOrDefaultAsync(ct);
        if (set is not { Status: DataChangeSetStatus.Draft })
        {
            return; // не найден или уже обработан — это скажет захват плана
        }

        var profileFields = set.Target == DataTargets.RegionProfiles
            ? (await db.DataChangeItems.AsNoTracking()
                .Where(i => i.ChangeSetId == changeSetId && i.EntityKey.StartsWith(DataEntityTypes.RegionProfile + ":"))
                .Select(i => i.EntityKey)
                .ToListAsync(ct)).Select(k => k[(k.LastIndexOf(':') + 1)..]).ToList()
            : [];
        var foreign = await ownership.ForeignAsync(FieldOwnership.Fields(set.Target, set.PlanKind, profileFields), set.SourceBindingId, ct);
        if (foreign.Count > 0)
        {
            await ownership.RecordRefusalAsync(foreign, set.SourceBindingId is null ? ConflictSources.DataManager : ConflictSources.GoogleSheets, userId,
                set.Title, set.Period, ct);
            throw new DataManagerException(FieldOwnership.Refusal(foreign));
        }
    }

    private async Task RequireAsync(Guid userId, CancellationToken ct)
    {
        var allowed = (await db.EffectivePermissionsAsync(userId, ct))?.Contains(Permissions.DataManage) == true;
        if (!allowed)
        {
            throw new DataManagerException("Недостаточно прав: нужно право «AI Data Manager».");
        }
    }

    /// <summary>Повторное нажатие или параллельный запрос: план уже применён/откачен — результат без повторной записи.</summary>
    private async Task<ApplyOutcome> AlreadyHandledAsync(Guid id, bool applying, CancellationToken ct)
    {
        var set = await db.DataChangeSets.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct)
                  ?? throw new DataManagerException("План изменений не найден.");
        return set.Status switch
        {
            DataChangeSetStatus.Applied when applying => new ApplyOutcome(set.Status, 0, 0, "План уже применён — повторно не применяется.", []),
            DataChangeSetStatus.Applying => throw new DataManagerException("План сейчас применяется или откатывается — дождитесь окончания."),
            DataChangeSetStatus.RolledBack or DataChangeSetStatus.PartiallyRolledBack when !applying =>
                new ApplyOutcome(set.Status, 0, 0, "План уже откачен.", []),
            _ => throw new DataManagerException(applying
                ? $"План в статусе {set.Status} применить нельзя."
                : $"Откатить можно только применённый план (сейчас {set.Status})."),
        };
    }

    private async Task<ApplyOutcome> FailedAsync(Guid id, Guid userId, Exception ex, string action, bool applying, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); // транзакция откатилась целиком — план остаётся в прежнем статусе
        var serialization = ex is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
                            || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure };
        if (serialization)
        {
            // Параллельный запрос успел применить (откатить) этот же план — это не ошибка, повторно ничего не делается.
            var status = await db.DataChangeSets.AsNoTracking().Where(s => s.Id == id).Select(s => (DataChangeSetStatus?)s.Status).FirstOrDefaultAsync(ct);
            if (applying ? status is DataChangeSetStatus.Applied : status is DataChangeSetStatus.RolledBack or DataChangeSetStatus.PartiallyRolledBack)
            {
                return await AlreadyHandledAsync(id, applying, ct);
            }
        }

        var message = serialization
            ? "Эти данные одновременно меняли в другом месте. Ничего не записано — повторите действие."
            : "Ошибка базы данных — ничего не записано. Подробности в журнале сервера.";
        logger.LogError(ex, "AI Data Manager: {Action} для плана {ChangeSetId}", action, id);
        await db.DataChangeSets.Where(s => s.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Error, message).SetProperty(s => s.UpdatedAt, DateTimeOffset.UtcNow), ct);
        await audit.LogAsync(ActorType.User, userId.ToString(), action, "data_change_set", id.ToString(), new { error = ex.GetType().Name }, ct);
        return new ApplyOutcome(DataChangeSetStatus.Failed, 0, 0, message, [message]);
    }

    private static bool Same(string entityType, string? current, string? expected) =>
        entityType == DataEntityTypes.RegionProfile
            ? current == expected
            : DataValues.ParseStored(current) == DataValues.ParseStored(expected);

    private async Task<string?> CurrentAsync(string key, CancellationToken ct)
    {
        if (key.StartsWith(DataEntityTypes.RegionPlan + ":", StringComparison.Ordinal))
        {
            var row = await PlanRowAsync(key, ct);
            return row is null ? null : DataValues.Format(row.PlanKg);
        }

        if (key.StartsWith(DataEntityTypes.SalesTarget + ":", StringComparison.Ordinal))
        {
            var target = await db.SalesTargets.FirstOrDefaultAsync(t => t.Key == TargetKey(key), ct);
            return target is null ? null : DataValues.Format(target.Value);
        }

        if (key.StartsWith(DataEntityTypes.RegionProfile + ":", StringComparison.Ordinal))
        {
            var (regionId, field) = RegionProfilePlanner.ParseKey(key);
            var region = await db.SalesRegions.FirstOrDefaultAsync(r => r.Id == regionId, ct);
            return field == RegionProfilePlanner.Dealer ? region?.DealerName : region?.SupervisorName;
        }

        if (key.StartsWith(DataEntityTypes.PnlValue + ":", StringComparison.Ordinal))
        {
            var row = await PnlRowAsync(key, ct);
            return row is null ? null : DataValues.Format(row.Amount);
        }

        throw new DataManagerException($"Неизвестный вид записи: {key}");
    }

    /// <summary>Записать значение (null — удалить запись) только в таблицы OneBase.</summary>
    private async Task WriteAsync(string entityType, string key, string? value, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        switch (entityType)
        {
            case DataEntityTypes.RegionPlan:
            {
                var row = await PlanRowAsync(key, ct);
                if (value is null)
                {
                    if (row is not null)
                    {
                        db.SalesRegionPlans.Remove(row);
                    }

                    return;
                }

                var kg = DataValues.ParseStored(value)!.Value;
                if (row is null)
                {
                    var k = RegionPlanPlanner.ParseKey(key);
                    if (!await db.SalesRegions.AnyAsync(r => r.Id == k.RegionId, ct))
                    {
                        throw new DataManagerException("Регион плана удалён — подготовьте план заново.");
                    }

                    db.SalesRegionPlans.Add(new SalesRegionPlan
                    {
                        RegionId = k.RegionId, Kind = k.Kind, Year = k.Year, Month = k.Month, CategoryId = k.CategoryId, PlanKg = kg,
                    });
                }
                else
                {
                    row.PlanKg = kg;
                    row.UpdatedAt = now;
                }

                return;
            }

            case DataEntityTypes.SalesTarget:
            {
                var target = await db.SalesTargets.FirstOrDefaultAsync(t => t.Key == TargetKey(key), ct);
                if (value is null)
                {
                    if (target is not null)
                    {
                        db.SalesTargets.Remove(target);
                    }

                    return;
                }

                var number = DataValues.ParseStored(value)!.Value;
                if (target is null)
                {
                    db.SalesTargets.Add(new SalesTarget { Key = TargetKey(key), Value = number });
                }
                else
                {
                    target.Value = number;
                }

                return;
            }

            case DataEntityTypes.RegionProfile:
            {
                var (regionId, field) = RegionProfilePlanner.ParseKey(key);
                var region = await db.SalesRegions.FirstOrDefaultAsync(r => r.Id == regionId, ct)
                             ?? throw new DataManagerException("Регион удалён — подготовьте план заново.");
                if (field == RegionProfilePlanner.Dealer)
                {
                    region.DealerName = value;
                }
                else
                {
                    region.SupervisorName = value;
                }

                region.UpdatedAt = now;
                return;
            }

            case DataEntityTypes.PnlValue:
            {
                var row = await PnlRowAsync(key, ct);
                if (value is null)
                {
                    if (row is not null)
                    {
                        db.SalesPnlValues.Remove(row);
                    }

                    return;
                }

                var amount = DataValues.ParseStored(value)!.Value;
                if (row is null)
                {
                    var k = PnlPlanner.ParseKey(key);
                    db.SalesPnlValues.Add(new SalesPnlValue
                    {
                        Year = k.Year, Month = k.Month, Line = k.Line, Dealer = PnlMath.DealerName(k.Dealer), DealerKey = PnlMath.DealerKey(k.Dealer), Amount = amount,
                    });
                }
                else
                {
                    row.Amount = amount;
                    row.UpdatedAt = now;
                }

                return;
            }

            default:
                throw new DataManagerException($"Неизвестный вид записи: {entityType}");
        }
    }

    private async Task<SalesRegionPlan?> PlanRowAsync(string key, CancellationToken ct)
    {
        var k = RegionPlanPlanner.ParseKey(key);
        var local = db.SalesRegionPlans.Local.FirstOrDefault(p =>
            p.RegionId == k.RegionId && p.Kind == k.Kind && p.Year == k.Year && p.Month == k.Month && p.CategoryId == k.CategoryId
            && db.Entry(p).State != EntityState.Deleted);
        return local ?? await db.SalesRegionPlans.FirstOrDefaultAsync(p =>
            p.RegionId == k.RegionId && p.Kind == k.Kind && p.Year == k.Year && p.Month == k.Month && p.CategoryId == k.CategoryId, ct);
    }

    private async Task<SalesPnlValue?> PnlRowAsync(string key, CancellationToken ct)
    {
        var k = PnlPlanner.ParseKey(key);
        var dealerKey = PnlMath.DealerKey(k.Dealer);
        var local = db.SalesPnlValues.Local.FirstOrDefault(v =>
            v.Year == k.Year && v.Month == k.Month && v.Line == k.Line && v.DealerKey == dealerKey && db.Entry(v).State != EntityState.Deleted);
        return local ?? await db.SalesPnlValues.FirstOrDefaultAsync(v =>
            v.Year == k.Year && v.Month == k.Month && v.Line == k.Line && v.DealerKey == dealerKey, ct);
    }

    private static string TargetKey(string key) => key[(DataEntityTypes.SalesTarget.Length + 1)..];
}
