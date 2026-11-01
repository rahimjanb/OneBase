using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>
/// Складской учёт снабжения — общий для заявок (резерв, выдача) и склада (приход, списание, инвентаризация): остатки (отслеживаются, с
/// версией xmin), складские документы с номерами, строки журнала движений. Все изменения остатка проверяются здесь: на складе не бывает
/// меньше нуля и меньше зарезервированного. Сохраняет вызывающий — одной записью вместе с документом, журналом и заявкой; если остаток
/// успели изменить одновременно, RetryAsync повторяет всю операцию заново по свежим данным.
/// </summary>
public sealed class ProcurementStockLedger(IAppDbContext db, IProcurementNumbers numbers)
{
    public const int Attempts = 3;

    /// <summary>
    /// Операция с остатками целиком (прочитать, проверить, изменить, сохранить): при одновременном изменении — заново по свежим данным, до
    /// трёх раз; дальше — 409 «изменили одновременно». Одновременное изменение — версия строки (xmin) или уникальный индекс, который успела
    /// занять параллельная запись (резерв под ту же позицию, тот же ключ формы).
    /// </summary>
    public async Task<T> RetryAsync<T>(Func<Task<T>> operation)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (DbUpdateException e) when (attempt < Attempts && (e is DbUpdateConcurrencyException || IsUniqueViolation(e)))
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException e) => e.InnerException is System.Data.Common.DbException { SqlState: "23505" };

    /// <summary>Центральный склад (засевается при запуске).</summary>
    public async Task<Guid> DefaultWarehouseIdAsync(CancellationToken ct)
    {
        var id = await db.ProcurementWarehouses.AsNoTracking().OrderByDescending(w => w.IsDefault).ThenBy(w => w.CreatedAt).Select(w => w.Id).FirstOrDefaultAsync(ct);
        return id == Guid.Empty ? throw new FieldConflictException("Склад снабжения не настроен — перезапустите OneBase, чтобы создать «Центральный склад».") : id;
    }

    /// <summary>Остатки позиций на складе (отслеживаются); строки, которой ещё нет, — новая с нулями.</summary>
    public async Task<Dictionary<Guid, ProcurementStockBalance>> BalancesAsync(Guid warehouseId, IEnumerable<Guid> itemIds, CancellationToken ct)
    {
        var ids = itemIds.Distinct().ToList();
        var balances = await db.ProcurementStockBalances.Where(b => b.WarehouseId == warehouseId && ids.Contains(b.ItemId)).ToDictionaryAsync(b => b.ItemId, ct);
        foreach (var id in ids.Where(id => !balances.ContainsKey(id)))
        {
            var balance = new ProcurementStockBalance { WarehouseId = warehouseId, ItemId = id };
            db.ProcurementStockBalances.Add(balance);
            balances[id] = balance;
        }

        return balances;
    }

    /// <summary>Новый складской документ с номером (RC-, ISS-, WO-, INV-год-NNNNN).</summary>
    public async Task<ProcurementStockDocument> DocumentAsync(ProcurementStockDocumentKind kind, Guid warehouseId, Guid actorId, CancellationToken ct)
    {
        var doc = new ProcurementStockDocument
        {
            Number = await numbers.NextAsync(ProcurementRules.Prefix(kind), ct),
            Kind = kind,
            WarehouseId = warehouseId,
            ActorId = actorId,
        };
        db.ProcurementStockDocuments.Add(doc);
        return doc;
    }

    /// <summary>
    /// Движение: меняет «на складе» на quantity (+ приход, − расход) и пишет строку журнала. fromReserve — выдача из резерва заявки:
    /// резерв уменьшается вместе с остатком. Нельзя уйти в минус и ниже зарезервированного — ошибка с понятным текстом.
    /// </summary>
    public ProcurementStockMovement Move(ProcurementStockDocument doc, ProcurementStockBalance balance, ProcurementMovementKind kind, decimal quantity, string itemLabel,
        decimal? unitCost = null, Guid? requestItemId = null, bool fromReserve = false)
    {
        var before = balance.OnHand;
        var after = before + quantity;
        var reserved = fromReserve ? balance.Reserved + quantity : balance.Reserved;
        if (after < 0 || reserved < 0)
        {
            throw new FieldConflictException($"«{itemLabel}»: на складе {ProcurementRules.Quantity(before)} — столько не списать и не выдать.");
        }

        if (after < reserved)
        {
            throw new FieldConflictException(
                $"«{itemLabel}»: на складе {ProcurementRules.Quantity(before)}, из них в резерве {ProcurementRules.Quantity(balance.Reserved)} — свободно {ProcurementRules.Quantity(before - balance.Reserved)}.");
        }

        balance.OnHand = after;
        balance.Reserved = reserved;
        balance.UpdatedAt = DateTimeOffset.UtcNow;
        var movement = new ProcurementStockMovement
        {
            DocumentId = doc.Id,
            WarehouseId = doc.WarehouseId,
            ItemId = balance.ItemId,
            Kind = kind,
            Quantity = quantity,
            BalanceBefore = before,
            BalanceAfter = after,
            UnitCost = unitCost,
            RequestItemId = requestItemId,
            ActorId = doc.ActorId,
        };
        db.ProcurementStockMovements.Add(movement);
        return movement;
    }
}
