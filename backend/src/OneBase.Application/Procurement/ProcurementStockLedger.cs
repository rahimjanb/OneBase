using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>Вид операции для проверки владельца данных склада: ручная (кладовщик, снабжение) или загрузка журнала (файл, Google-таблица).</summary>
public enum ProcurementWarehouseUse
{
    Manual,
    Import,
}

/// <summary>
/// Складской учёт снабжения — общий для заявок (резерв, выдача) и склада (приход, списание, инвентаризация, перемещение): остатки
/// (отслеживаются, с версией xmin), складские документы с номерами, строки журнала движений. Все изменения остатка проверяются здесь: на
/// складе не бывает меньше нуля и меньше зарезервированного. У позиции с партиями остаток меняется только вместе с остатком партии
/// (MoveBatch): остаток позиции на складе = сумма её партий; обычное движение (Move) по такой позиции — ошибка. Выдаётся только годное:
/// допущенная по качеству и не просроченная партия (ProcurementBatchRules.Usable). Приход позиции с контролем качества ставит новую партию
/// «на контроль» и заводит проверку (NewChecks — кому сообщить). Сохраняет вызывающий —
/// одной записью вместе с документом, журналом и заявкой; если остаток успели изменить одновременно, RetryAsync повторяет всю операцию
/// заново по свежим данным.
/// </summary>
public sealed class ProcurementStockLedger(IAppDbContext db, IProcurementNumbers numbers, TimeProvider clock)
{
    public const int Attempts = 3;

    /// <summary>Позиции с партиями среди загруженных остатков — по ним Move запрещён.</summary>
    private readonly HashSet<Guid> _tracked = [];

    private readonly List<ProcurementQualityCheck> _checks = [];

    /// <summary>Проверки качества, заведённые этой операцией (приход позиций с контролем), — кому сообщить.</summary>
    public IReadOnlyList<ProcurementQualityCheck> NewChecks => _checks;

    /// <summary>Сегодня по Ташкенту (часы приложения): по нему партия годна или просрочена.</summary>
    public DateOnly Today => ProcurementBatchRules.Today(clock);

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
                _tracked.Clear();
                _checks.Clear();
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

    /// <summary>
    /// Склад операции: указанный или (не указан) склад по умолчанию. Его нет — 404; отключён — 409; кладовщик закреплён за другими складами —
    /// 403 (закреплённый проводит операции только по своим складам). Владелец данных (D13): ручная операция по складу «в таблице» и загрузка
    /// журнала на склад «в OneBase» — 409.
    /// </summary>
    public async Task<ProcurementWarehouse> WarehouseAsync(ProcurementContext ctx, Guid? id, CancellationToken ct, ProcurementWarehouseUse use = ProcurementWarehouseUse.Manual)
    {
        var warehouseId = id ?? await DefaultWarehouseIdAsync(ct);
        var warehouse = await db.ProcurementWarehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId, ct)
                        ?? throw new FieldNotFoundException("Склад не найден.");
        if (!warehouse.IsActive)
        {
            throw new FieldConflictException($"Склад «{warehouse.Name}» отключён — операции по нему не проводятся.");
        }

        if (!ctx.Posts(warehouse.Id))
        {
            throw new FieldForbiddenException(ctx.Warehouse
                ? $"Вы закреплены за другими складами — операции по складу «{warehouse.Name}» проводит его кладовщик."
                : "Приход, выдачу, списание и инвентаризацию проводит кладовщик.");
        }

        CheckOwner(warehouse, use);
        return warehouse;
    }

    /// <summary>Склад принимает операцию этого вида: «в таблице» — только загрузку журнала, «в OneBase» — только ручные операции; не выбран — всё.</summary>
    public static void CheckOwner(ProcurementWarehouse warehouse, ProcurementWarehouseUse use)
    {
        var since = warehouse.DataOwnerSince is { } d ? $" с {d:dd.MM.yyyy}" : "";
        if (use == ProcurementWarehouseUse.Manual && warehouse.DataOwner == ProcurementDataOwner.Sheet)
        {
            throw new FieldConflictException(
                $"Склад «{warehouse.Name}» ведётся в таблице{since} — остатки приходят загрузкой журнала, ручные операции по нему закрыты (кроме контроля качества).");
        }

        if (use == ProcurementWarehouseUse.Import && warehouse.DataOwner == ProcurementDataOwner.OneBase)
        {
            throw new FieldConflictException($"Склад «{warehouse.Name}» ведётся в OneBase{since} — загрузка журнала на него закрыта.");
        }
    }

    /// <summary>Ручная операция без выбора склада кладовщиком («Куплено», получатель перемещения): склад «в таблице» её не принимает.</summary>
    public async Task RequireManualAsync(Guid warehouseId, CancellationToken ct)
    {
        var warehouse = await db.ProcurementWarehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId, ct) ?? throw new FieldNotFoundException("Склад не найден.");
        CheckOwner(warehouse, ProcurementWarehouseUse.Manual);
    }

    /// <summary>Остатки позиций на складе (отслеживаются); строки, которой ещё нет, — новая с нулями. Запоминает, какие из позиций — с партиями.</summary>
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

        _tracked.UnionWith(await db.ProcurementItems.AsNoTracking().Where(i => ids.Contains(i.Id) && i.TracksBatches).Select(i => i.Id).ToListAsync(ct));
        return balances;
    }

    /// <summary>Позиция ведётся по партиям (среди загруженных BalancesAsync).</summary>
    public bool Tracks(Guid itemId) => _tracked.Contains(itemId);

    /// <summary>Новый складской документ с номером (RC-, ISS-, WO-, INV-, TRF-, BE-, STN-год-NNNNN).</summary>
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
    /// Движение позиции без партий: меняет «на складе» на quantity (+ приход, − расход) и пишет строку журнала — на склад строки остатка
    /// (у перемещения обе стороны в одном документе). fromReserve — выдача из резерва заявки: резерв уменьшается вместе с остатком. Нельзя
    /// уйти в минус и ниже зарезервированного — ошибка с понятным текстом. Позицию с партиями так провести нельзя — только MoveBatch.
    /// </summary>
    public ProcurementStockMovement Move(ProcurementStockDocument doc, ProcurementStockBalance balance, ProcurementMovementKind kind, decimal quantity, string itemLabel,
        decimal? unitCost = null, Guid? requestItemId = null, bool fromReserve = false)
    {
        if (_tracked.Contains(balance.ItemId))
        {
            throw new FieldConflictException(
                $"«{itemLabel}» ведётся по партиям — её проводят с партией и сроком годности: приход — на складе («Приход»), выдача и списание — по партиям.");
        }

        return MoveCore(doc, balance, kind, quantity, itemLabel, unitCost, requestItemId, fromReserve);
    }

    private ProcurementStockMovement MoveCore(ProcurementStockDocument doc, ProcurementStockBalance balance, ProcurementMovementKind kind, decimal quantity, string itemLabel,
        decimal? unitCost, Guid? requestItemId, bool fromReserve)
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
            WarehouseId = balance.WarehouseId,
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

    /// <summary>
    /// Движение по партии: остаток партии и позиции меняются на quantity вместе, строка журнала — с партией и её остатком до и после. Партия
    /// не уходит в минус; правила остатка позиции (минус, резерв) — те же, что у Move.
    /// </summary>
    public ProcurementStockMovement MoveBatch(ProcurementStockDocument doc, ProcurementStockBalance balance, ProcurementBatchBalance batch, ProcurementMovementKind kind,
        decimal quantity, string itemLabel, decimal? unitCost = null, Guid? requestItemId = null, bool fromReserve = false)
    {
        var before = batch.OnHand;
        var after = before + quantity;
        if (after < 0)
        {
            throw new FieldConflictException($"«{itemLabel}»: партия {batch.Batch?.Number} — на складе {ProcurementRules.Quantity(before)}, столько не провести.");
        }

        var movement = MoveCore(doc, balance, kind, quantity, itemLabel, unitCost, requestItemId, fromReserve);
        batch.OnHand = after;
        batch.UpdatedAt = DateTimeOffset.UtcNow;
        movement.BatchId = batch.BatchId;
        movement.BatchBefore = before;
        movement.BatchAfter = after;
        return movement;
    }

    /// <summary>Ввод партии в имеющийся остаток: остаток позиции не меняется (движение 0), партия получает quantity.</summary>
    public ProcurementStockMovement EnterBatch(ProcurementStockDocument doc, ProcurementStockBalance balance, ProcurementBatchBalance batch, decimal quantity, string itemLabel)
    {
        var movement = MoveCore(doc, balance, ProcurementMovementKind.BatchEntry, 0, itemLabel, null, null, false);
        movement.BatchId = batch.BatchId;
        movement.BatchBefore = batch.OnHand;
        batch.OnHand += quantity;
        batch.UpdatedAt = DateTimeOffset.UtcNow;
        movement.BatchAfter = batch.OnHand;
        return movement;
    }

    /// <summary>
    /// Новая партия позиции с номером LOT-год-NNNNN и её строка остатка на складе (с нулём); приходует вызывающий (MoveBatch). control —
    /// контроль качества позиции: не None — партия «на контроле» и проверка QC-… (документы, затем лаборатория). Срок не указан (ввод
    /// партий) — партия заблокирована, пока назначенный по качеству не укажет срок.
    /// </summary>
    public async Task<ProcurementBatchBalance> NewBatchAsync(ProcurementStockDocument doc, Guid warehouseId, Guid itemId, string? supplierBatchNo, DateOnly? manufacturedOn,
        DateOnly? expiresOn, decimal? unitCost, Guid? supplierId, CancellationToken ct, ProcurementQualityControl control = ProcurementQualityControl.None)
    {
        var batch = new ProcurementBatch
        {
            Number = await numbers.NextAsync(ProcurementBatchRules.Prefix, ct),
            ItemId = itemId,
            SupplierBatchNo = string.IsNullOrWhiteSpace(supplierBatchNo) ? null : supplierBatchNo.Trim(),
            SupplierId = supplierId,
            ManufacturedOn = manufacturedOn,
            ExpiresOn = expiresOn,
            ReceivedOn = Today,
            SourceDocumentId = doc.Id,
            UnitCost = unitCost,
        };
        if (expiresOn is null)
        {
            batch.QualityStatus = ProcurementBatchQuality.Blocked;
            batch.BlockReason = ProcurementQualityRules.UnknownExpiry;
        }
        else if (control != ProcurementQualityControl.None)
        {
            batch.QualityStatus = ProcurementBatchQuality.Pending;
            var check = new ProcurementQualityCheck
            {
                Number = await numbers.NextAsync(ProcurementQualityRules.Prefix, ct), BatchId = batch.Id, Batch = batch, WarehouseId = warehouseId, Control = control,
            };
            db.ProcurementQualityChecks.Add(check);
            _checks.Add(check);
        }

        db.ProcurementBatches.Add(batch);
        var balance = new ProcurementBatchBalance { WarehouseId = warehouseId, BatchId = batch.Id, Batch = batch, ItemId = itemId };
        db.ProcurementBatchBalances.Add(balance);
        return balance;
    }

    /// <summary>Остатки партий позиции на складе (отслеживаются, с партией): с остатком больше нуля или все (withEmpty).</summary>
    public Task<List<ProcurementBatchBalance>> BatchStockAsync(Guid warehouseId, Guid itemId, CancellationToken ct, bool withEmpty = false) =>
        db.ProcurementBatchBalances.Include(b => b.Batch).Where(b => b.WarehouseId == warehouseId && b.ItemId == itemId && (withEmpty || b.OnHand > 0)).ToListAsync(ct);

    /// <summary>Строка остатка партии на складе (отслеживается); на этом складе её ещё нет — новая с нулём (перемещение, возврат, инвентаризация).</summary>
    public async Task<ProcurementBatchBalance> BatchBalanceAsync(Guid warehouseId, ProcurementBatch batch, CancellationToken ct)
    {
        var balance = db.ProcurementBatchBalances.Local.FirstOrDefault(b => b.WarehouseId == warehouseId && b.BatchId == batch.Id)
                      ?? await db.ProcurementBatchBalances.Include(b => b.Batch).FirstOrDefaultAsync(b => b.WarehouseId == warehouseId && b.BatchId == batch.Id, ct);
        if (balance is null)
        {
            balance = new ProcurementBatchBalance { WarehouseId = warehouseId, BatchId = batch.Id, Batch = batch, ItemId = batch.ItemId };
            db.ProcurementBatchBalances.Add(balance);
        }

        return balance;
    }

    /// <summary>
    /// Из каких партий взять quantity позиции на складе: указанная партия (batchId) или FEFO. usableOnly — только годные (выдача, перемещение
    /// без выбора партии); keepReserve — после отбора годного остаётся не меньше резерва заявок (выдача без заявки, списание и перемещение
    /// годного). Не хватает — 409 с понятным текстом.
    /// </summary>
    public async Task<List<(ProcurementBatchBalance Batch, decimal Quantity)>> PickAsync(ProcurementStockBalance balance, string warehouseName, string label, decimal quantity,
        Guid? batchId, bool usableOnly, bool keepReserve, CancellationToken ct)
    {
        var today = Today;
        var stock = await BatchStockAsync(balance.WarehouseId, balance.ItemId, ct);
        var usable = stock.Where(b => Usable(b.Batch!, today)).Sum(b => b.OnHand);
        List<(ProcurementBatchBalance, decimal)> picks;
        if (batchId is { } id)
        {
            var chosen = stock.FirstOrDefault(b => b.BatchId == id) ?? throw new FieldConflictException($"«{label}»: этой партии нет на складе «{warehouseName}».");
            var b = chosen.Batch!;
            if (usableOnly && ProcurementBatchRules.Unusable(b.QualityStatus, b.ExpiresOn, today) is { } why)
            {
                throw new FieldConflictException(why switch
                {
                    "control" => $"«{label}»: партия {b.Number} на контроле качества — до решения её не выдать (переместить и списать можно).",
                    "blocked" => b.QualityStatus == ProcurementBatchQuality.Rejected
                        ? $"«{label}»: партия {b.Number} забракована — её можно вернуть поставщику или списать."
                        : $"«{label}»: партия {b.Number} заблокирована{(b.BlockReason is { } reason ? $" ({reason})" : "")} — до снятия блокировки её не выдать.",
                    _ => $"«{label}»: партия {b.Number} просрочена ({b.ExpiresOn:dd.MM.yyyy}) — её можно только списать или переместить.",
                });
            }

            if (chosen.OnHand < quantity)
            {
                throw new FieldConflictException($"«{label}»: партия {b.Number} — на складе «{warehouseName}» {ProcurementRules.Quantity(chosen.OnHand)}, столько не провести.");
            }

            picks = [(chosen, quantity)];
        }
        else
        {
            var found = ProcurementBatchRules.Pick(stock.Select(Stock), quantity, today, usableOnly);
            if (found is null)
            {
                // Чего не хватает: годного столько-то, остальное — на контроле, забраковано или заблокировано, просрочено.
                var held = stock.Where(b => !Usable(b.Batch!, today))
                    .GroupBy(b => ProcurementBatchRules.Unusable(b.Batch!.QualityStatus, b.Batch.ExpiresOn, today)!)
                    .ToDictionary(g => g.Key, g => g.Sum(b => b.OnHand));
                var parts = new[] { ("control", "на контроле"), ("blocked", "забраковано и заблокировано"), ("expired", "просрочено") }
                    .Where(x => held.GetValueOrDefault(x.Item1) > 0).Select(x => $"{x.Item2} {ProcurementRules.Quantity(held[x.Item1])}").ToList();
                throw new FieldConflictException(usableOnly && parts.Count > 0
                    ? $"«{label}»: на складе «{warehouseName}» годного {ProcurementRules.Quantity(usable)}, {string.Join(", ", parts)} — столько не выдать."
                    : $"«{label}»: на складе «{warehouseName}» {ProcurementRules.Quantity(stock.Sum(b => b.OnHand))} — столько не провести.");
            }

            picks = found.Select(p => (stock.First(b => b.BatchId == p.BatchId), p.Quantity)).ToList();
        }

        if (keepReserve && balance.Reserved > 0)
        {
            var taken = picks.Where(p => Usable(p.Item1.Batch!, today)).Sum(p => p.Item2);
            if (usable - taken < balance.Reserved)
            {
                throw new FieldConflictException(
                    $"«{label}»: годного на складе «{warehouseName}» {ProcurementRules.Quantity(usable)}, из них в резерве заявок {ProcurementRules.Quantity(balance.Reserved)} — свободно {ProcurementRules.Quantity(Math.Max(0, usable - balance.Reserved))}.");
            }
        }

        return picks;
    }

    private static ProcurementBatchStock Stock(ProcurementBatchBalance b) =>
        new(b.BatchId, b.Batch!.Number, b.Batch.ExpiresOn, b.Batch.ReceivedOn, b.OnHand, b.Batch.QualityStatus);

    private static bool Usable(ProcurementBatch b, DateOnly today) => ProcurementBatchRules.Usable(b.QualityStatus, b.ExpiresOn, today);

    /// <summary>
    /// Остаток позиций с партиями на складе, который не выдать: просроченный, на контроле качества, забракованный, заблокированный (по нему
    /// «доступно» меньше: такое не выдаётся и не резервируется).
    /// </summary>
    public async Task<Dictionary<Guid, decimal>> UnusableAsync(Guid warehouseId, IEnumerable<Guid> itemIds, CancellationToken ct)
    {
        var ids = itemIds.Distinct().ToList();
        var today = Today;
        return await db.ProcurementBatchBalances.AsNoTracking()
            .Where(b => b.WarehouseId == warehouseId && ids.Contains(b.ItemId) && b.OnHand > 0
                        && !(b.Batch!.QualityStatus == ProcurementBatchQuality.Approved && b.Batch.ExpiresOn != null && b.Batch.ExpiresOn >= today))
            .GroupBy(b => b.ItemId).Select(g => new { g.Key, OnHand = g.Sum(b => b.OnHand) })
            .ToDictionaryAsync(x => x.Key, x => x.OnHand, ct);
    }

    /// <summary>
    /// Приход под позицию заявки (приёмка по заказу, «Куплено»): движение «приход» по цене, «получено» в позиции, карточка склада — у позиции
    /// без неё. Пока по заявке выдают, поступившее резервируется под неё — на складе, но занято до выдачи инициатору (к выдаче). active —
    /// действующие резервы позиций заявок (дополняется новыми).
    /// </summary>
    public void ReceiveForRequest(ProcurementStockDocument doc, ProcurementStockBalance balance, ProcurementRequestItem requestItem, decimal quantity, decimal? unitCost,
        string itemLabel, Dictionary<Guid, ProcurementStockReservation> active)
    {
        Move(doc, balance, ProcurementMovementKind.Receipt, quantity, itemLabel, unitCost, requestItem.Id);
        Received(balance, requestItem, quantity, quantity, itemLabel, active);
    }

    /// <summary>
    /// Приход позиции с партиями под позицию заявки («Куплено»): новые партии (у позиции с контролем — «на контроле» с проверкой), движение
    /// каждой — с позицией заявки, «получено» в позиции. Резервируется под заявку только годное сразу (без контроля); партия на контроле
    /// резервируется под неё решением «допустить» (ReserveForRequest), а забракованная возвращает количество «к закупке».
    /// </summary>
    public async Task ReceiveBatchesForRequestAsync(ProcurementStockDocument doc, ProcurementStockBalance balance, ProcurementRequestItem requestItem, ProcurementItem item,
        IReadOnlyList<(ProcurementBatchInput Batch, DateOnly Expires)> batches, decimal? unitCost, Guid? supplierId, string itemLabel,
        Dictionary<Guid, ProcurementStockReservation> active, CancellationToken ct)
    {
        decimal usable = 0;
        foreach (var (b, expires) in batches)
        {
            var batch = await NewBatchAsync(doc, balance.WarehouseId, item.Id, b.SupplierBatchNo, b.ManufacturedOn, expires, unitCost, supplierId, ct, item.QualityControl);
            MoveBatch(doc, balance, batch, ProcurementMovementKind.Receipt, b.Quantity, itemLabel, unitCost, requestItem.Id);
            usable += batch.Batch!.QualityStatus == ProcurementBatchQuality.Approved ? b.Quantity : 0;
        }

        Received(balance, requestItem, batches.Sum(b => b.Batch.Quantity), usable, itemLabel, active);
    }

    /// <summary>«Получено» в позиции заявки и резерв под неё поступившего (reserve из quantity), карточка склада — у позиции без неё, статусы.</summary>
    private void Received(ProcurementStockBalance balance, ProcurementRequestItem requestItem, decimal quantity, decimal reserve, string itemLabel,
        Dictionary<Guid, ProcurementStockReservation> active)
    {
        var now = DateTimeOffset.UtcNow;
        requestItem.ReceivedQty += quantity;
        requestItem.ItemId ??= balance.ItemId;
        requestItem.UpdatedAt = now;
        if (reserve > 0)
        {
            ReserveForRequest(balance, requestItem, reserve, itemLabel, active);
        }

        if (requestItem.Status != ProcurementItemStatus.Cancelled)
        {
            requestItem.Status = ProcurementRules.ItemStatus(requestItem, submitted: true);
        }

        requestItem.Request!.UpdatedAt = now;
    }

    /// <summary>
    /// Резерв поступившего под позицию заявки (пока по ней выдают): в действующий резерв позиции или новый — на складе строки остатка; заявка
    /// начинает выдаваться со склада. Заявка не выдаётся или позиция отменена — ничего. Возвращает, сколько зарезервировано.
    /// </summary>
    public decimal ReserveForRequest(ProcurementStockBalance balance, ProcurementRequestItem requestItem, decimal quantity, string itemLabel,
        Dictionary<Guid, ProcurementStockReservation> active)
    {
        var request = requestItem.Request!;
        if (!ProcurementRules.Issuable(request.Status) || requestItem.Status == ProcurementItemStatus.Cancelled || quantity <= 0)
        {
            return 0;
        }

        if (active.GetValueOrDefault(requestItem.Id) is { } reservation)
        {
            if (reservation.ItemId != balance.ItemId)
            {
                throw new FieldConflictException($"«{itemLabel}»: под заявку уже зарезервирована другая карточка склада — примите на неё.");
            }

            reservation.Quantity += quantity;
        }
        else
        {
            var created = new ProcurementStockReservation
            {
                WarehouseId = balance.WarehouseId, ItemId = balance.ItemId, RequestId = request.Id, RequestItemId = requestItem.Id, Quantity = quantity,
            };
            db.ProcurementStockReservations.Add(created);
            active[requestItem.Id] = created;
        }

        balance.Reserved += quantity;
        request.UsesStock = true;
        return quantity;
    }
}
