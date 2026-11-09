using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Security;
using OneBase.Application.Work;
using OneBase.Domain.Audit;
using OneBase.Domain.Procurement;
using OneBase.Domain.Work;

namespace OneBase.Application.Procurement;

/// <summary>Вкладка «Качество»: видна ли, вносит ли пользователь проверки, решает ли, настраивает ли; счётчики открытых проверок по шагам.</summary>
public sealed record ProcurementQualityNav(bool Sees, bool Quality, bool Decides, bool Configures, int Open = 0, int Documents = 0, int Lab = 0, int Decision = 0);

/// <summary>
/// Проверка в очереди: шаг, контроль, партия (номер, номер производителя, срок), позиция, поставщик, склад прихода, пришло и остаток партии,
/// дней ждёт (до решения), итоги документов и лаборатории, решение. Проверка поставки (приезд машины) до приёмки — без партии: пришло — по
/// накладной, остатка нет; поставка — номер AR-.
/// </summary>
public sealed record ProcurementQualityCheckRow(
    Guid Id,
    string Number,
    ProcurementQualityCheckStatus Status,
    ProcurementQualityControl Control,
    Guid? BatchId,
    string? BatchNumber,
    string? SupplierBatchNo,
    DateOnly? ExpiresOn,
    Guid ItemId,
    string Sku,
    string ItemName,
    string UnitCode,
    string? SupplierName,
    Guid WarehouseId,
    string WarehouseName,
    decimal Received,
    decimal OnHand,
    DateTimeOffset CreatedAt,
    int DaysWaiting,
    bool? DocumentsOk,
    bool? LabOk,
    ProcurementBatchQuality? Decision,
    string? DecidedByName,
    DateTimeOffset? DecidedAt,
    Guid? ArrivalId = null,
    string? ArrivalNumber = null);

/// <summary>Очередь и архив: status — open (по умолчанию), documents, lab, decision, decided, cancelled, all; склад, позиция, партия, поиск.</summary>
public sealed record ProcurementQualitySearch(string? Status = "open", Guid? WarehouseId = null, Guid? ItemId = null, Guid? BatchId = null, string? Search = null, int Page = 1,
    int PageSize = 50);

public sealed record ProcurementQualitySpecView(Guid Id, int LineNo, ProcurementQualitySpecKind Kind, string Name, string? Unit, decimal? Min, decimal? Max, string? Expected,
    string? Method, bool Required);

public sealed record ProcurementQualityResultView(Guid? SpecId, ProcurementQualitySpecKind Kind, int LineNo, string Name, string? Unit, decimal? Min, decimal? Max, string? Expected,
    decimal? Value, string? Text, bool? Passed);

/// <summary>Что пользователь может сделать с проверкой (сервер проверяет то же при каждом действии).</summary>
public sealed record ProcurementQualityAbilities(bool Documents, bool Lab, bool Approve, bool Reject, bool Attach);

/// <summary>Карточка проверки: строка очереди, шаги (кто и когда), пробы, спецификация, результаты, документы проверки и партии, история, где лежит партия.</summary>
public sealed record ProcurementQualityCheckCard(
    ProcurementQualityCheckRow Check,
    uint Version,
    string? StorageConditions,
    string? DocumentsNote,
    string? DocumentsByName,
    DateTimeOffset? DocumentsAt,
    decimal? SampleQty,
    string? SampleDocumentNumber,
    string? LabNote,
    string? LabByName,
    DateTimeOffset? LabAt,
    string? DecisionComment,
    IReadOnlyList<ProcurementQualitySpecView> Specs,
    IReadOnlyList<ProcurementQualityResultView> Results,
    IReadOnlyList<ProcurementDocumentView> Documents,
    IReadOnlyList<ProcurementDocumentView> BatchDocuments,
    IReadOnlyList<ProcurementEventView> History,
    IReadOnlyList<ProcurementBatchWarehouse> Warehouses,
    ProcurementQualityAbilities Can);

/// <summary>Отметка по строке спецификации: документ (Passed — есть и в порядке) или показатель (Value — по норме, Passed — у качественного).</summary>
public sealed record ProcurementQualityItemInput(Guid SpecId, bool? Passed = null, decimal? Value = null, string? Text = null);

/// <summary>Проверка документов: отметки по документам спецификации (без них — Ok целиком), что не так, версия проверки.</summary>
public sealed record ProcurementQualityDocumentsInput(IReadOnlyList<ProcurementQualityItemInput>? Items, bool? Ok = null, string? Note = null, uint? Version = null);

/// <summary>Лаборатория: показатели спецификации (без них — Ok целиком), заключение, проба (сколько и с какого склада — списывается один раз), версия.</summary>
public sealed record ProcurementQualityLabInput(IReadOnlyList<ProcurementQualityItemInput>? Items, bool? Ok = null, string? Note = null, decimal? SampleQty = null,
    Guid? SampleWarehouseId = null, uint? Version = null);

/// <summary>Решение: approve или reject, комментарий (при браке и допуске с отклонениями — обязателен), версия проверки.</summary>
public sealed record ProcurementQualityDecisionInput(string? Decision, string? Comment, uint? Version = null);

public sealed record ProcurementQualitySpecInput(Guid? Id, ProcurementQualitySpecKind Kind, string? Name, string? Unit = null, decimal? Min = null, decimal? Max = null,
    string? Expected = null, string? Method = null, bool Required = true);

public sealed record ProcurementQualitySpecsInput(IReadOnlyList<ProcurementQualitySpecInput>? Lines);

/// <summary>Спецификация материала: позиция, её контроль и учёт партий, действующие строки, можно ли править (лаборатория).</summary>
public sealed record ProcurementQualitySpecsView(Guid ItemId, string Sku, string Name, string UnitCode, ProcurementQualityControl Control, bool TracksBatches,
    IReadOnlyList<ProcurementQualitySpecView> Lines, bool CanEdit);

/// <summary>Блокировка или снятие: причина (обязательна), срок годности (при снятии у партии «срок не указан»), версия партии.</summary>
public sealed record ProcurementBlockInput(string? Reason, DateOnly? ExpiresOn = null, uint? Version = null);

/// <summary>Настройки качества: кто решает по партиям, из кого выбрать (право «Качество сырья»), можно ли править (настройки снабжения).</summary>
public sealed record ProcurementQualitySettingsView(IReadOnlyList<ProcurementPersonOption> Approvers, IReadOnlyList<ProcurementPersonOption> Candidates, bool CanEdit);

public sealed record ProcurementQualitySettingsInput(IReadOnlyList<Guid>? ApproverIds);

/// <summary>
/// Контроль качества сырья: очередь проверок партий, проверка документов и лаборатория (лаборатория — право «Качество сырья»), отбор проб
/// (списание «Пробы лаборатории» тем же документом), решение «допустить / забраковать», блокировка и снятие блокировки — только назначенные в
/// настройках качества (администратор со всеми правами не решает), спецификации материалов. Каждое действие — в историю проверки и партии
/// той же записью; решение неизменяемо, двойное нажатие не принимает второе (версия xmin).
/// </summary>
public sealed class ProcurementQualityService(IAppDbContext db, ProcurementStockLedger ledger, UserNotifier notifier, ProcurementDocumentService documents, IAuditLogger audit)
{
    public const string CheckDoc = "qc";
    public const string BatchDoc = "batch";

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private static void RequireSees(ProcurementContext ctx)
    {
        if (!ctx.SeesQuality)
        {
            throw new FieldForbiddenException("Контроль качества видят лаборатория, назначенные по качеству, кладовщик, снабжение и директор.");
        }
    }

    private static void RequireLab(ProcurementContext ctx)
    {
        if (!ctx.Quality)
        {
            throw new FieldForbiddenException("Проверку документов и показатели вносит лаборатория (право «Качество сырья»).");
        }
    }

    private static void RequireDecides(ProcurementContext ctx)
    {
        if (!ctx.DecidesQuality)
        {
            throw new FieldForbiddenException("Решение по партии принимает сотрудник, назначенный в настройках качества («Настройки → Отделы → Снабжение»).");
        }
    }

    // ---------- Очередь ----------

    public async Task<ProcurementQualityNav> NavAsync(ProcurementContext ctx, CancellationToken ct)
    {
        if (!ctx.SeesQuality)
        {
            return new ProcurementQualityNav(false, false, false, ctx.Configures);
        }

        var open = ProcurementQualityRules.Open;
        var counts = await db.ProcurementQualityChecks.AsNoTracking().Where(c => open.Contains(c.Status)).GroupBy(c => c.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        int Count(ProcurementQualityCheckStatus s) => counts.GetValueOrDefault(s);
        return new ProcurementQualityNav(true, ctx.Quality, ctx.DecidesQuality, ctx.Configures, counts.Values.Sum(), Count(ProcurementQualityCheckStatus.Documents),
            Count(ProcurementQualityCheckStatus.Lab), Count(ProcurementQualityCheckStatus.Decision));
    }

    private sealed class CheckData
    {
        public Guid Id { get; init; }
        public string Number { get; init; } = "";
        public ProcurementQualityCheckStatus Status { get; init; }
        public ProcurementQualityControl Control { get; init; }
        public Guid? BatchId { get; init; }
        public string? BatchNumber { get; init; }
        public Guid? ArrivalId { get; init; }
        public string? ArrivalNumber { get; init; }
        public string? SupplierBatchNo { get; init; }
        public DateOnly? ExpiresOn { get; init; }
        public Guid ItemId { get; init; }
        public string Sku { get; init; } = "";
        public string ItemName { get; init; } = "";
        public string UnitCode { get; init; } = "";
        public string? SupplierName { get; init; }
        public Guid WarehouseId { get; init; }
        public string WarehouseName { get; init; } = "";
        public decimal Received { get; init; }
        public decimal OnHand { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public bool? DocumentsOk { get; init; }
        public bool? LabOk { get; init; }
        public ProcurementBatchQuality? Decision { get; init; }
        public string? DecidedByName { get; init; }
        public DateTimeOffset? DecidedAt { get; init; }
    }

    private IQueryable<CheckData> Rows() =>
        from c in db.ProcurementQualityChecks.AsNoTracking()
        select new CheckData
        {
            Id = c.Id,
            Number = c.Number,
            Status = c.Status,
            Control = c.Control,
            BatchId = c.BatchId,
            BatchNumber = c.Batch == null ? null : c.Batch.Number,
            // Партии ещё нет (поставка до приёмки) — номер и срок из строки поставки.
            SupplierBatchNo = c.Batch != null ? c.Batch.SupplierBatchNo : db.ProcurementArrivalLines.Where(l => l.QualityCheckId == c.Id).Select(l => l.SupplierBatchNo).FirstOrDefault(),
            ExpiresOn = c.Batch != null ? c.Batch.ExpiresOn : db.ProcurementArrivalLines.Where(l => l.QualityCheckId == c.Id).Select(l => l.ExpiresOn).FirstOrDefault(),
            ItemId = c.ItemId,
            Sku = c.Item!.Sku,
            ItemName = c.Item.Name,
            UnitCode = c.Item.UnitCode,
            SupplierName = c.Supplier == null ? null : c.Supplier.Name,
            WarehouseId = c.WarehouseId,
            WarehouseName = db.ProcurementWarehouses.Where(w => w.Id == c.WarehouseId).Select(w => w.Name).FirstOrDefault() ?? "",
            // Пришло — приход партии её документом появления (до приёмки — по накладной); остаток — по всем складам.
            Received = c.Batch != null
                ? db.ProcurementStockMovements.Where(m => m.BatchId == c.BatchId && m.DocumentId == c.Batch.SourceDocumentId).Sum(m => (decimal?)m.Quantity) ?? 0m
                : db.ProcurementArrivalLines.Where(l => l.QualityCheckId == c.Id).Select(l => (decimal?)l.DeclaredBase).FirstOrDefault() ?? 0m,
            OnHand = db.ProcurementBatchBalances.Where(b => b.BatchId == c.BatchId).Sum(b => (decimal?)b.OnHand) ?? 0m,
            ArrivalId = c.ArrivalId,
            ArrivalNumber = c.Arrival == null ? null : c.Arrival.Number,
            CreatedAt = c.CreatedAt,
            DocumentsOk = c.DocumentsOk,
            LabOk = c.LabOk,
            Decision = c.Decision,
            DecidedByName = c.DecidedBy == null ? null : c.DecidedBy.FullName,
            DecidedAt = c.DecidedAt,
        };

    private ProcurementQualityCheckRow Row(CheckData c, DateOnly today) => new(c.Id, c.Number, c.Status, c.Control, c.BatchId, c.BatchNumber, c.SupplierBatchNo, c.ExpiresOn,
        c.ItemId, c.Sku, c.ItemName, c.UnitCode, c.SupplierName, c.WarehouseId, c.WarehouseName, c.Received, c.OnHand, c.CreatedAt,
        Math.Max(0, (c.DecidedAt is { } at ? FieldClock.LocalDate(at) : today).DayNumber - FieldClock.LocalDate(c.CreatedAt).DayNumber),
        c.DocumentsOk, c.LabOk, c.Decision, c.DecidedByName, c.DecidedAt, c.ArrivalId, c.ArrivalNumber);

    public async Task<PageResult<ProcurementQualityCheckRow>> ListAsync(ProcurementContext ctx, ProcurementQualitySearch s, CancellationToken ct)
    {
        RequireSees(ctx);
        var open = ProcurementQualityRules.Open;
        var q = Rows();
        q = s.Status switch
        {
            "documents" => q.Where(c => c.Status == ProcurementQualityCheckStatus.Documents),
            "lab" => q.Where(c => c.Status == ProcurementQualityCheckStatus.Lab),
            "decision" => q.Where(c => c.Status == ProcurementQualityCheckStatus.Decision),
            "decided" => q.Where(c => c.Status == ProcurementQualityCheckStatus.Decided),
            "rejected" => q.Where(c => c.Decision == ProcurementBatchQuality.Rejected),
            "cancelled" => q.Where(c => c.Status == ProcurementQualityCheckStatus.Cancelled),
            "all" => q,
            _ => q.Where(c => open.Contains(c.Status)),
        };
        if (s.WarehouseId is { } warehouse)
        {
            q = q.Where(c => c.WarehouseId == warehouse);
        }

        if (s.ItemId is { } item)
        {
            q = q.Where(c => c.ItemId == item);
        }

        if (s.BatchId is { } batch)
        {
            q = q.Where(c => c.BatchId == batch);
        }

        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var text = s.Search.Trim().ToLower();
            q = q.Where(c => c.Number.ToLower().Contains(text) || (c.BatchNumber != null && c.BatchNumber.ToLower().Contains(text))
                             || (c.ArrivalNumber != null && c.ArrivalNumber.ToLower().Contains(text)) || (c.SupplierBatchNo != null && c.SupplierBatchNo.ToLower().Contains(text))
                             || c.Sku.ToLower().Contains(text) || c.ItemName.ToLower().Contains(text) || (c.SupplierName != null && c.SupplierName.ToLower().Contains(text)));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(s.PageSize, 5, 200);
        var page = Math.Max(s.Page, 1);
        // Очередь — сначала самые давние; архив — сначала последние.
        q = s.Status is null or "" or "open" or "documents" or "lab" or "decision" ? q.OrderBy(c => c.CreatedAt).ThenBy(c => c.Number) : q.OrderByDescending(c => c.CreatedAt);
        var today = ledger.Today;
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PageResult<ProcurementQualityCheckRow>(rows.Select(c => Row(c, today)).ToList(), total, page, pageSize);
    }

    public async Task<ProcurementQualityCheckCard> CardAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireSees(ctx);
        var data = await Rows().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new FieldNotFoundException("Проверка качества не найдена.");
        var check = await db.ProcurementQualityChecks.AsNoTracking().Include(c => c.Results).FirstAsync(c => c.Id == id, ct);
        var people = new[] { check.DocumentsById, check.LabById }.OfType<Guid>().ToList();
        var names = await db.Users.AsNoTracking().Where(u => people.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        string? Name(Guid? who) => who is { } x ? names.GetValueOrDefault(x) : null;
        var storage = await db.ProcurementItems.AsNoTracking().Where(i => i.Id == data.ItemId).Select(i => i.StorageConditions).FirstOrDefaultAsync(ct);
        var sample = await db.ProcurementStockDocuments.AsNoTracking().Where(d => d.QualityCheckId == id).Select(d => d.Number).FirstOrDefaultAsync(ct);
        var specs = await SpecLinesAsync(data.ItemId, ct);
        var history = await db.ProcurementAuditLogs.AsNoTracking().Where(e => e.DocType == CheckDoc && e.DocId == id).OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
            .Select(e => new ProcurementEventView(e.Id, e.Action, e.Text, e.Actor == null ? "Система" : e.Actor.FullName, e.FromStatus, e.ToStatus, e.Comment, e.CreatedAt))
            .ToListAsync(ct);
        var warehouses = data.BatchId is { } batchId
            ? await db.ProcurementBatchBalances.AsNoTracking().Where(b => b.BatchId == batchId && b.OnHand > 0)
                .Join(db.ProcurementWarehouses.AsNoTracking(), b => b.WarehouseId, w => w.Id, (b, w) => new ProcurementBatchWarehouse(w.Id, w.Name, b.OnHand))
                .ToListAsync(ct)
            : [];
        var open = ProcurementQualityRules.IsOpen(check.Status);
        var can = new ProcurementQualityAbilities(
            ctx.Quality && open,
            ctx.Quality && ProcurementQualityRules.LabAllowed(check.Control, check.Status, check.DocumentsOk),
            ctx.DecidesQuality && ProcurementQualityRules.CanApprove(check.Status),
            ctx.DecidesQuality && open,
            ctx.Quality || ctx.DecidesQuality || ctx.Warehouse);
        return new ProcurementQualityCheckCard(Row(data, ledger.Today), check.Version, storage, check.DocumentsNote, Name(check.DocumentsById), check.DocumentsAt, check.SampleQty,
            sample, check.LabNote, Name(check.LabById), check.LabAt, check.DecisionComment, specs,
            check.Results.OrderBy(r => r.Kind).ThenBy(r => r.LineNo)
                .Select(r => new ProcurementQualityResultView(r.SpecId, r.Kind, r.LineNo, r.Name, r.Unit, r.Min, r.Max, r.Expected, r.Value, r.Text, r.Passed)).ToList(),
            await documents.ListAsync(ctx, CheckDoc, id, ct), data.BatchId is { } batch ? await documents.ListAsync(ctx, BatchDoc, batch, ct) : [], history, warehouses, can);
    }

    // ---------- Шаги проверки ----------

    /// <summary>Открытая проверка для изменения (отслеживается, с партией и результатами); закрыта — 409; правили одновременно — 409.</summary>
    private async Task<ProcurementQualityCheck> OpenCheckAsync(Guid id, uint? version, CancellationToken ct)
    {
        var check = await db.ProcurementQualityChecks.Include(c => c.Batch).Include(c => c.Item).Include(c => c.Arrival).Include(c => c.Results)
                        .FirstOrDefaultAsync(c => c.Id == id, ct)
                    ?? throw new FieldNotFoundException("Проверка качества не найдена.");
        if (!ProcurementQualityRules.IsOpen(check.Status))
        {
            throw new FieldConflictException($"Проверка {check.Number} закрыта ({ProcurementQualityRules.Label(check.Status)}) — её не изменить.");
        }

        if (version is { } v && v != check.Version)
        {
            throw new FieldConflictException($"Проверку {check.Number} уже изменили — обновите страницу.");
        }

        return check;
    }

    /// <summary>
    /// Проверка документов (лаборатория): по каждому документу спецификации — есть и в порядке; без документов в спецификации — итог целиком.
    /// Не в порядке — что именно (обязательно); проверка уходит на решение (обычно — забраковать). В порядке — дальше лаборатория или решение.
    /// Пока решения нет, документы можно проверить заново.
    /// </summary>
    public async Task DocumentsAsync(ProcurementContext ctx, Guid id, ProcurementQualityDocumentsInput input, CancellationToken ct)
    {
        RequireLab(ctx);
        var check = await OpenCheckAsync(id, input.Version, ct);
        var specs = (await ActiveSpecsAsync(check.ItemId, ct)).Where(s => s.Kind == ProcurementQualitySpecKind.Document).ToList();
        var marks = Marks(input.Items);
        var results = new List<ProcurementQualityResult>();
        foreach (var spec in specs)
        {
            var passed = marks.GetValueOrDefault(spec.Id)?.Passed;
            if (spec.Required && passed is null)
            {
                throw new FieldValidationException($"«{spec.Name}»: отметьте, есть ли документ и в порядке ли он.");
            }

            results.Add(Result(check, spec, null, null, passed));
        }

        var ok = specs.Count > 0
            ? results.All(r => r.Passed != false)
            : input.Ok ?? throw new FieldValidationException("Отметьте, в порядке ли документы партии.");
        var note = Text(input.Note, 2000, "Замечание");
        if (!ok && note is null)
        {
            throw new FieldValidationException("Документы не в порядке — напишите, что именно (нет сертификата, не совпадает номер партии или срок).");
        }

        Replace(check, ProcurementQualitySpecKind.Document, results);
        var from = check.Status;
        check.DocumentsOk = ok;
        check.DocumentsNote = note;
        check.DocumentsById = ctx.UserId;
        check.DocumentsAt = Now;
        check.Status = ProcurementQualityRules.Next(check.Control, check.DocumentsOk, check.LabOk);
        check.UpdatedAt = Now;
        Log(check, ctx.UserId, "documents", ok ? "Документы в порядке" : "Документы не в порядке", note, from);
        await NotifyDecisionAsync(check, from, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Лаборатория (после документов в порядке, только при контроле «документы и лаборатория»): показатели спецификации — количественный по норме
    /// считается, качественный отмечает лаборант; без показателей в спецификации — итог целиком. Проба — списание «Пробы лаборатории» с этой
    /// партии одним документом WO-… (один раз; склад — указанный или склад прихода). Пока решения нет, результаты можно поправить.
    /// </summary>
    public Task LabAsync(ProcurementContext ctx, Guid id, ProcurementQualityLabInput input, CancellationToken ct) => ledger.RetryAsync(async () =>
    {
        RequireLab(ctx);
        var check = await OpenCheckAsync(id, input.Version, ct);
        if (!ProcurementQualityRules.LabAllowed(check.Control, check.Status, check.DocumentsOk))
        {
            throw new FieldConflictException(check.Control != ProcurementQualityControl.Lab
                ? $"У «{check.Item!.Name}» контроль — по документам: лаборатория для этой партии не нужна."
                : "Лаборатория — после проверки документов: сначала отметьте, что документы в порядке.");
        }

        var item = check.Item!;
        var label = $"{item.Sku} {item.Name}";
        var specs = (await ActiveSpecsAsync(check.ItemId, ct)).Where(s => s.Kind == ProcurementQualitySpecKind.Parameter).ToList();
        var marks = Marks(input.Items);
        var results = new List<ProcurementQualityResult>();
        foreach (var spec in specs)
        {
            var mark = marks.GetValueOrDefault(spec.Id);
            var text = Text(mark?.Text, 500, $"«{spec.Name}»");
            bool? passed;
            decimal? value = null;
            if (spec.Min is not null || spec.Max is not null)
            {
                // Количественный показатель: «соответствует» — по норме, не по отметке.
                value = mark?.Value;
                if (value is { } v && (Math.Abs(v) >= 100_000_000_000_000m || decimal.Round(v, 4) != v))
                {
                    throw new FieldValidationException($"«{spec.Name}»: значение — до четырёх знаков после запятой.");
                }

                passed = value is { } x ? ProcurementQualityRules.Passed(spec.Min, spec.Max, x) : null;
                if (spec.Required && passed is null)
                {
                    throw new FieldValidationException($"«{spec.Name}»: укажите значение показателя.");
                }
            }
            else
            {
                passed = mark?.Passed;
                if (spec.Required && passed is null)
                {
                    throw new FieldValidationException($"«{spec.Name}»: отметьте, соответствует ли показатель.");
                }
            }

            results.Add(Result(check, spec, value, text, passed));
        }

        var ok = specs.Count > 0
            ? results.All(r => r.Passed != false)
            : input.Ok ?? throw new FieldValidationException("Отметьте итог лаборатории: соответствует или нет.");
        var note = Text(input.Note, 2000, "Заключение");

        // Проба — один раз: списывается с партии тем же сохранением; у поставки до приёмки — взята из машины, только записывается.
        if (input.SampleQty is { } sample && sample != 0)
        {
            if (sample < 0 || sample > ProcurementRules.MaxQuantity || decimal.Round(sample, 3) != sample)
            {
                throw new FieldValidationException("Проба — больше нуля, не больше трёх знаков после запятой.");
            }

            if (check.SampleQty is { } taken)
            {
                if (taken != sample)
                {
                    throw new FieldConflictException($"Проба уже {(check.BatchId is null ? "записана" : "списана")} ({ProcurementRules.Quantity(taken)} {item.UnitCode}) — количество не изменить.");
                }
            }
            else if (check.Batch is not { } batch)
            {
                // Товара на складе ещё нет — пробу отобрали из машины; кладовщик примет то, что осталось.
                check.SampleQty = sample;
            }
            else
            {
                var warehouseId = input.SampleWarehouseId ?? check.WarehouseId;
                var store = await db.ProcurementWarehouses.AsNoTracking().FirstOrDefaultAsync(w => w.Id == warehouseId, ct) ?? throw new FieldNotFoundException("Склад не найден.");
                if (!store.IsActive)
                {
                    throw new FieldConflictException($"Склад «{store.Name}» отключён — пробу с него не списать.");
                }

                var balance = (await ledger.BalancesAsync(store.Id, [batch.ItemId], ct))[batch.ItemId];
                var stock = await ledger.BatchBalanceAsync(store.Id, batch, ct);
                var doc = await ledger.DocumentAsync(ProcurementStockDocumentKind.WriteOff, store.Id, ctx.UserId, ct);
                doc.Comment = $"Пробы лаборатории: {check.Number}";
                doc.QualityCheckId = check.Id;
                ledger.MoveBatch(doc, balance, stock, ProcurementMovementKind.WriteOff, -sample, label);
                check.SampleQty = sample;
            }
        }

        Replace(check, ProcurementQualitySpecKind.Parameter, results);
        var from = check.Status;
        check.LabOk = ok;
        check.LabNote = note;
        check.LabById = ctx.UserId;
        check.LabAt = Now;
        check.Status = ProcurementQualityRules.Next(check.Control, check.DocumentsOk, check.LabOk);
        check.UpdatedAt = Now;
        var outside = results.Where(r => r.Passed == false).Select(r => r.Name).ToList();
        Log(check, ctx.UserId, "lab", ok ? "Лаборатория: соответствует" : $"Лаборатория: не соответствует{(outside.Count > 0 ? $" ({string.Join(", ", outside)})" : "")}",
            note, from);
        await NotifyDecisionAsync(check, from, ct);
        await db.SaveChangesAsync(ct);
        return true;
    });

    /// <summary>
    /// Решение по партии (назначенный в настройках качества): «допустить» — только на шаге «решение», с отклонениями (документы не в порядке,
    /// показатель вне нормы) — с комментарием; «забраковать» — на любом шаге, комментарий обязателен. Партия становится допущенной или
    /// забракованной, проверка закрывается; кладовщикам и снабжению — уведомление.
    /// </summary>
    public async Task DecideAsync(ProcurementContext ctx, Guid id, ProcurementQualityDecisionInput input, CancellationToken ct)
    {
        RequireDecides(ctx);
        var check = await OpenCheckAsync(id, input.Version, ct);
        var decision = input.Decision?.Trim().ToLowerInvariant() switch
        {
            "approve" or "approved" => ProcurementBatchQuality.Approved,
            "reject" or "rejected" => ProcurementBatchQuality.Rejected,
            _ => throw new FieldValidationException("Решение — допустить или забраковать."),
        };
        var comment = Text(input.Comment, 2000, "Комментарий");
        if (decision == ProcurementBatchQuality.Approved)
        {
            if (!ProcurementQualityRules.CanApprove(check.Status))
            {
                throw new FieldConflictException(
                    $"Допустить партию можно после проверки документов{(check.Control == ProcurementQualityControl.Lab ? " и лаборатории" : "")} — сейчас: {ProcurementQualityRules.Label(check.Status)}.");
            }

            if (ProcurementQualityRules.Deviates(check.DocumentsOk, check.LabOk) && comment is null)
            {
                throw new FieldValidationException("Партия с отклонениями (документы или показатели) — чтобы допустить, напишите почему.");
            }
        }
        else if (comment is null)
        {
            throw new FieldValidationException("Напишите, почему партия забракована.");
        }

        var from = check.Status;
        check.Decision = decision;
        check.DecisionComment = comment;
        check.DecidedById = ctx.UserId;
        check.DecidedAt = Now;
        check.Status = ProcurementQualityCheckStatus.Decided;
        check.UpdatedAt = Now;
        var approved = decision == ProcurementBatchQuality.Approved;
        var item = check.Item!;
        if (check.Batch is { } batch)
        {
            var batchFrom = batch.QualityStatus;
            batch.QualityStatus = decision;
            batch.QualityDecidedAt = Now;
            batch.QualityDecidedById = ctx.UserId;
            batch.BlockReason = null;
            batch.UpdatedAt = Now;
            Log(check, ctx.UserId, "decided", approved ? "Партия допущена" : "Партия забракована", comment, from);
            LogBatch(batch, ctx.UserId, approved ? "approved" : "rejected", $"{(approved ? "Допущена" : "Забракована")} по проверке {check.Number}", comment, batchFrom);
            await RequestEffectsAsync(batch, approved, ctx.UserId, ct);
            await NotifyWarehouseAsync(ctx, check.WarehouseId,
                approved ? $"Партия допущена: {item.Name} — {batch.Number}" : $"Партия забракована: {item.Name} — {batch.Number}",
                approved ? "Можно выдавать, пока не истёк срок годности." : $"{comment} Верните поставщику или спишите.", $"/procurement/batches/{batch.Id}", ct);
        }
        else
        {
            // Поставка до склада: решение — по строке поставки; решено по всем — кладовщик принимает (или возврат поставщику).
            Log(check, ctx.UserId, "decided", approved ? "Допущено к приёмке" : "Забраковано — вернуть поставщику", comment, from);
            await ProcurementArrivalEffects.CheckDecidedAsync(db, notifier, check, ctx.UserId, ct);
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.quality.decided", "procurement_quality_check", check.Id.ToString(),
            new { check.Number, batch = check.Batch?.Number, arrival = check.Arrival?.Number, decision = decision.ToString(), comment }, ct);
    }

    /// <summary>
    /// Партия, купленная под заявку («Куплено»): допущена — резервируется под позицию заявки (теперь её можно выдать инициатору), забракована —
    /// её количество снова «к закупке» (снабжение докупит), в истории заявки — запись, снабжению — уведомление.
    /// </summary>
    private async Task RequestEffectsAsync(ProcurementBatch batch, bool approved, Guid actor, CancellationToken ct)
    {
        var receipts = await db.ProcurementStockMovements.AsNoTracking()
            .Where(m => m.BatchId == batch.Id && m.Kind == ProcurementMovementKind.Receipt && m.RequestItemId != null)
            .Select(m => new { m.WarehouseId, RequestItemId = m.RequestItemId!.Value, m.Quantity })
            .ToListAsync(ct);
        foreach (var receipt in receipts)
        {
            var requestItem = await db.ProcurementRequestItems.Include(i => i.Request).FirstOrDefaultAsync(i => i.Id == receipt.RequestItemId, ct);
            if (requestItem is null)
            {
                continue;
            }

            var request = requestItem.Request!;
            var unit = requestItem.UnitCode;
            string text;
            if (approved)
            {
                // Резерв — не больше того, что осталось от партии на складе (пробы списаны) и свободно в остатке позиции.
                var balance = (await ledger.BalancesAsync(receipt.WarehouseId, [batch.ItemId], ct))[batch.ItemId];
                var left = await db.ProcurementBatchBalances.Where(b => b.BatchId == batch.Id && b.WarehouseId == receipt.WarehouseId).Select(b => b.OnHand).FirstOrDefaultAsync(ct);
                var active = await db.ProcurementStockReservations.Where(x => x.RequestItemId == requestItem.Id && x.Status == ProcurementReservationStatus.Active)
                    .ToDictionaryAsync(x => x.RequestItemId, ct);
                var reserved = ledger.ReserveForRequest(balance, requestItem, Math.Min(receipt.Quantity, Math.Min(left, balance.OnHand - balance.Reserved)), requestItem.Name, active);
                if (reserved <= 0)
                {
                    continue;
                }

                text = $"Партия {batch.Number} допущена по качеству — {ProcurementRules.Quantity(reserved)} {unit} «{requestItem.Name}» зарезервировано, можно выдавать";
            }
            else
            {
                // Забракованное снова «к закупке»: снабжение купит заново.
                var back = Math.Min(receipt.Quantity, Math.Min(requestItem.ReceivedQty, requestItem.OrderedQty));
                requestItem.ReceivedQty -= back;
                requestItem.OrderedQty -= back;
                text = $"Партия {batch.Number} забракована — {ProcurementRules.Quantity(back)} {unit} «{requestItem.Name}» снова к закупке";
                foreach (var user in (await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementManage, ct)).Where(u => u != actor))
                {
                    notifier.Notify(user, UserNotificationKind.ProcurementRequest, $"Снова к закупке: {request.Number} — {requestItem.Name}", text, $"/requests/{request.Id}");
                }
            }

            if (requestItem.Status != ProcurementItemStatus.Cancelled)
            {
                requestItem.Status = ProcurementRules.ItemStatus(requestItem, submitted: true);
            }

            requestItem.UpdatedAt = Now;
            request.UpdatedAt = Now;
            db.ProcurementAuditLogs.Add(new ProcurementAuditLog
            {
                DocType = ProcurementRules.RequestDoc, DocId = request.Id, ActorId = actor, Action = approved ? "quality_approved" : "quality_rejected", Text = text,
            });
        }
    }

    // ---------- Блокировка ----------

    /// <summary>Заблокировать допущенную партию (жалоба, отзыв, сомнение в качестве) — назначенный по качеству, причина обязательна. Не выдаётся до снятия.</summary>
    public async Task BlockAsync(ProcurementContext ctx, Guid batchId, ProcurementBlockInput input, CancellationToken ct)
    {
        RequireDecides(ctx);
        var batch = await BatchAsync(batchId, input.Version, ct);
        var reason = Text(input.Reason, 1000, "Причина") ?? throw new FieldValidationException("Укажите причину блокировки.");
        if (batch.QualityStatus != ProcurementBatchQuality.Approved)
        {
            throw new FieldConflictException($"Партия {batch.Number} {ProcurementQualityRules.Label(batch.QualityStatus)} — заблокировать можно допущенную.");
        }

        var from = batch.QualityStatus;
        batch.QualityStatus = ProcurementBatchQuality.Blocked;
        batch.BlockReason = reason;
        batch.QualityDecidedAt = Now;
        batch.QualityDecidedById = ctx.UserId;
        batch.UpdatedAt = Now;
        LogBatch(batch, ctx.UserId, "blocked", "Партия заблокирована", reason, from);
        // Партия на одном складе — его кладовщикам; на нескольких — всем кладовщикам.
        var warehouses = await db.ProcurementBatchBalances.AsNoTracking().Where(b => b.BatchId == batch.Id && b.OnHand > 0).Select(b => b.WarehouseId).ToListAsync(ct);
        await NotifyWarehouseAsync(ctx, warehouses.Count == 1 ? warehouses[0] : null, $"Партия заблокирована: {batch.Item!.Name} — {batch.Number}",
            $"{reason} Не выдавать до снятия блокировки.", $"/procurement/batches/{batch.Id}", ct);

        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.batch.blocked", "procurement_batch", batch.Id.ToString(), new { batch.Number, reason }, ct);
    }

    /// <summary>
    /// Снять блокировку (назначенный по качеству, причина обязательна): партия снова допущена. У партии «срок не указан» — сначала срок годности
    /// (не раньше даты производства); указанный при приходе срок не меняется.
    /// </summary>
    public async Task UnblockAsync(ProcurementContext ctx, Guid batchId, ProcurementBlockInput input, CancellationToken ct)
    {
        RequireDecides(ctx);
        var batch = await BatchAsync(batchId, input.Version, ct);
        var reason = Text(input.Reason, 1000, "Причина") ?? throw new FieldValidationException("Укажите, почему блокировка снята.");
        if (batch.QualityStatus != ProcurementBatchQuality.Blocked)
        {
            throw new FieldConflictException($"Партия {batch.Number} {ProcurementQualityRules.Label(batch.QualityStatus)} — снять можно только блокировку.");
        }

        var text = "Блокировка снята";
        if (batch.ExpiresOn is null)
        {
            if (input.ExpiresOn is not { } expires)
            {
                throw new FieldValidationException("У партии не указан срок годности — укажите его, иначе партию не выдать.");
            }

            if (batch.ManufacturedOn is { } made && expires < made)
            {
                throw new FieldValidationException($"Срок годности {expires:dd.MM.yyyy} — раньше даты производства {made:dd.MM.yyyy}.");
            }

            batch.ExpiresOn = expires;
            text = $"Блокировка снята, срок годности — {expires:dd.MM.yyyy}";
        }
        else if (input.ExpiresOn is { } other && other != batch.ExpiresOn)
        {
            throw new FieldValidationException("Срок годности партии указан при приходе и не меняется.");
        }

        var from = batch.QualityStatus;
        batch.QualityStatus = ProcurementBatchQuality.Approved;
        batch.BlockReason = null;
        batch.QualityDecidedAt = Now;
        batch.QualityDecidedById = ctx.UserId;
        batch.UpdatedAt = Now;
        LogBatch(batch, ctx.UserId, "unblocked", text, reason, from);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.batch.unblocked", "procurement_batch", batch.Id.ToString(),
            new { batch.Number, reason, expiresOn = batch.ExpiresOn }, ct);
    }

    private async Task<ProcurementBatch> BatchAsync(Guid id, uint? version, CancellationToken ct)
    {
        var batch = await db.ProcurementBatches.Include(b => b.Item).FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new FieldNotFoundException("Партия не найдена.");
        if (version is { } v && v != batch.Version)
        {
            throw new FieldConflictException($"Партию {batch.Number} уже изменили — обновите страницу.");
        }

        return batch;
    }

    // ---------- Спецификации ----------

    public async Task<ProcurementQualitySpecsView> SpecsAsync(ProcurementContext ctx, Guid itemId, CancellationToken ct)
    {
        RequireSees(ctx);
        var item = await db.ProcurementItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, ct) ?? throw new FieldNotFoundException("Позиция каталога не найдена.");
        return new ProcurementQualitySpecsView(item.Id, item.Sku, item.Name, item.UnitCode, item.QualityControl, item.TracksBatches, await SpecLinesAsync(itemId, ct), ctx.Quality);
    }

    /// <summary>
    /// Спецификация материала (лаборатория): документы и показатели по порядку строк. Показатель — с нормой (от и до) или ожидаемым (качественный).
    /// Убранные строки отключаются, не удаляются: проверки хранят снимок нормы. До 50 строк, названия внутри вида не повторяются.
    /// </summary>
    public async Task UpdateSpecsAsync(ProcurementContext ctx, Guid itemId, ProcurementQualitySpecsInput input, CancellationToken ct)
    {
        RequireLab(ctx);
        var item = await db.ProcurementItems.FirstOrDefaultAsync(i => i.Id == itemId, ct) ?? throw new FieldNotFoundException("Позиция каталога не найдена.");
        var lines = (input.Lines ?? []).ToList();
        if (lines.Count > ProcurementQualityRules.MaxSpecLines)
        {
            throw new FieldValidationException($"В спецификации — до {ProcurementQualityRules.MaxSpecLines} строк.");
        }

        var existing = await db.ProcurementQualitySpecs.Where(s => s.ItemId == itemId && s.IsActive).ToListAsync(ct);
        var before = existing.OrderBy(s => s.LineNo).Select(Snapshot).ToList();
        var names = new HashSet<(ProcurementQualitySpecKind, string)>();
        var kept = new HashSet<Guid>();
        var lineNo = 0;
        foreach (var line in lines)
        {
            var name = Text(line.Name, 200, "Название") ?? throw new FieldValidationException("У строки спецификации нет названия.");
            if (!names.Add((line.Kind, name.ToLowerInvariant())))
            {
                throw new FieldValidationException($"«{name}» повторяется в спецификации.");
            }

            var parameter = line.Kind == ProcurementQualitySpecKind.Parameter;
            var min = parameter ? Norm(line.Min, name) : null;
            var max = parameter ? Norm(line.Max, name) : null;
            var expected = parameter ? Text(line.Expected, 300, $"«{name}»: ожидаемое") : null;
            if (min is { } lo && max is { } hi && lo > hi)
            {
                throw new FieldValidationException($"«{name}»: норма «от» больше нормы «до».");
            }

            if (parameter && min is null && max is null && expected is null)
            {
                throw new FieldValidationException($"«{name}»: укажите норму — от и до или каким должен быть показатель.");
            }

            var spec = line.Id is { } sid ? existing.FirstOrDefault(s => s.Id == sid) : null;
            if (spec is null)
            {
                spec = new ProcurementQualitySpec { ItemId = itemId, Name = name };
                db.ProcurementQualitySpecs.Add(spec);
            }
            else
            {
                kept.Add(spec.Id);
                spec.UpdatedAt = Now;
            }

            spec.LineNo = ++lineNo;
            spec.Kind = line.Kind;
            spec.Name = name;
            spec.Unit = parameter ? Text(line.Unit, 32, $"«{name}»: единица") : null;
            spec.Min = min;
            spec.Max = max;
            spec.Expected = expected;
            spec.Method = Text(line.Method, 300, $"«{name}»: метод");
            spec.Required = line.Required;
        }

        foreach (var gone in existing.Where(s => !kept.Contains(s.Id)))
        {
            gone.IsActive = false;
            gone.UpdatedAt = Now;
        }

        item.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
        var after = (await db.ProcurementQualitySpecs.AsNoTracking().Where(s => s.ItemId == itemId && s.IsActive).OrderBy(s => s.LineNo).ToListAsync(ct)).Select(Snapshot).ToList();
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.quality.spec_updated", "procurement_item", itemId.ToString(), new { before, after }, ct);
    }

    private static object Snapshot(ProcurementQualitySpec s) => new { Kind = s.Kind.ToString(), s.Name, s.Unit, s.Min, s.Max, s.Expected, s.Required };

    private static decimal? Norm(decimal? value, string name) => value is { } v && (Math.Abs(v) >= 100_000_000_000_000m || decimal.Round(v, 4) != v)
        ? throw new FieldValidationException($"«{name}»: норма — до четырёх знаков после запятой.")
        : value;

    private Task<List<ProcurementQualitySpec>> ActiveSpecsAsync(Guid itemId, CancellationToken ct) =>
        db.ProcurementQualitySpecs.AsNoTracking().Where(s => s.ItemId == itemId && s.IsActive).OrderBy(s => s.LineNo).ToListAsync(ct);

    private async Task<List<ProcurementQualitySpecView>> SpecLinesAsync(Guid itemId, CancellationToken ct) =>
        (await ActiveSpecsAsync(itemId, ct)).Select(s => new ProcurementQualitySpecView(s.Id, s.LineNo, s.Kind, s.Name, s.Unit, s.Min, s.Max, s.Expected, s.Method, s.Required)).ToList();

    // ---------- Настройки ----------

    public async Task<ProcurementQualitySettingsView> SettingsAsync(ProcurementContext ctx, CancellationToken ct)
    {
        if (!ctx.Configures && !ctx.SeesQuality)
        {
            throw new FieldForbiddenException("Настройки качества видят снабжение, лаборатория и те, кто настраивает снабжение.");
        }

        var approvers = await db.ProcurementQualityApprovers.AsNoTracking().OrderBy(a => a.User!.FullName)
            .Select(a => new ProcurementPersonOption(a.UserId, a.User!.FullName)).ToListAsync(ct);
        var lab = await ProcurementPeople.WithPermissionAsync(db, Permissions.QualityCheck, ct);
        var candidates = await db.Users.AsNoTracking().Where(u => lab.Contains(u.Id)).OrderBy(u => u.FullName).Select(u => new ProcurementPersonOption(u.Id, u.FullName))
            .ToListAsync(ct);
        return new ProcurementQualitySettingsView(approvers, candidates, ctx.Configures);
    }

    /// <summary>
    /// Кто решает по качеству (право «Настройки снабжения»): действующие пользователи с правом «Качество сырья» по роли — технический администратор
    /// сюда не попадает (уже назначенного можно оставить, даже если права сняли). Можно никого: тогда партии на контроле ждут назначения.
    /// </summary>
    public async Task UpdateSettingsAsync(ProcurementContext ctx, ProcurementQualitySettingsInput input, CancellationToken ct)
    {
        if (!ctx.Configures)
        {
            throw new FieldForbiddenException("Назначает решающих по качеству тот, кто настраивает снабжение.");
        }

        var ids = (input.ApproverIds ?? []).Distinct().ToList();
        var current = await db.ProcurementQualityApprovers.ToListAsync(ct);
        var allowed = (await ProcurementPeople.WithPermissionAsync(db, Permissions.QualityCheck, ct)).ToHashSet();
        if (ids.Any(id => !allowed.Contains(id) && current.All(a => a.UserId != id)))
        {
            throw new FieldValidationException("Решать по качеству можно назначить только действующего сотрудника с правом «Качество сырья» (роль «Лаборатория»).");
        }

        var before = current.Select(a => a.UserId).ToList();
        db.ProcurementQualityApprovers.RemoveRange(current.Where(a => !ids.Contains(a.UserId)));
        db.ProcurementQualityApprovers.AddRange(ids.Where(id => current.All(a => a.UserId != id)).Select(id => new ProcurementQualityApprover { UserId = id }));
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(ActorType.User, ctx.UserId.ToString(), "procurement.quality.settings_updated", "procurement_settings", "1", new { before, after = ids }, ct);
    }

    // ---------- Вспомогательное ----------

    private static Dictionary<Guid, ProcurementQualityItemInput> Marks(IReadOnlyList<ProcurementQualityItemInput>? items) =>
        (items ?? []).GroupBy(i => i.SpecId).ToDictionary(g => g.Key, g => g.Last());

    private static ProcurementQualityResult Result(ProcurementQualityCheck check, ProcurementQualitySpec spec, decimal? value, string? text, bool? passed) => new()
    {
        CheckId = check.Id, SpecId = spec.Id, Kind = spec.Kind, LineNo = spec.LineNo, Name = spec.Name, Unit = spec.Unit, Min = spec.Min, Max = spec.Max, Expected = spec.Expected,
        Value = value, Text = text, Passed = passed,
    };

    /// <summary>Результаты вида kind заменяются новыми (до решения проверку можно поправить).</summary>
    private void Replace(ProcurementQualityCheck check, ProcurementQualitySpecKind kind, List<ProcurementQualityResult> results)
    {
        foreach (var old in check.Results.Where(r => r.Kind == kind).ToList())
        {
            check.Results.Remove(old);
            db.ProcurementQualityResults.Remove(old);
        }

        foreach (var result in results)
        {
            db.ProcurementQualityResults.Add(result);
        }
    }

    private void Log(ProcurementQualityCheck check, Guid actor, string action, string text, string? comment, ProcurementQualityCheckStatus from) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = CheckDoc, DocId = check.Id, ActorId = actor, Action = action, Text = text, Comment = comment,
            FromStatus = from == check.Status ? null : from.ToString(), ToStatus = from == check.Status ? null : check.Status.ToString(),
        });

    private void LogBatch(ProcurementBatch batch, Guid actor, string action, string text, string? comment, ProcurementBatchQuality from) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = BatchDoc, DocId = batch.Id, ActorId = actor, Action = action, Text = text, Comment = comment, FromStatus = from.ToString(),
            ToStatus = batch.QualityStatus.ToString(),
        });

    /// <summary>Проверка дошла до решения — назначенным по качеству.</summary>
    private async Task NotifyDecisionAsync(ProcurementQualityCheck check, ProcurementQualityCheckStatus from, CancellationToken ct)
    {
        if (from == check.Status || check.Status != ProcurementQualityCheckStatus.Decision)
        {
            return;
        }

        var what = check.Batch?.Number ?? (check.Arrival is { } arrival ? $"поставка {arrival.Number}" : check.Number);
        foreach (var user in await ProcurementQualityNotices.DecidersAsync(db, ct))
        {
            notifier.Notify(user, UserNotificationKind.ProcurementRequest, $"Ждёт решения по качеству: {check.Item!.Name} — {what}",
                check.DocumentsOk == false || check.LabOk == false ? "Есть отклонения — посмотрите проверку." : "Проверки пройдены.", $"/production/quality/{check.Id}");
        }
    }

    /// <summary>Кладовщикам склада (закреплённым, иначе всем кладовщикам) и руководителю снабжения — кроме того, кто решил.</summary>
    private async Task NotifyWarehouseAsync(ProcurementContext ctx, Guid? warehouseId, string title, string body, string link, CancellationToken ct)
    {
        var keepers = warehouseId is { } w ? await db.ProcurementWarehouseKeepers.AsNoTracking().Where(k => k.WarehouseId == w).Select(k => k.UserId).ToListAsync(ct) : [];
        if (keepers.Count == 0)
        {
            keepers = await ProcurementPeople.WithPermissionAsync(db, Permissions.ProcurementWarehouse, ct);
        }

        foreach (var user in keepers.Concat(await ProcurementPeople.SupplyHeadsAsync(db, ct)).Distinct().Where(u => u != ctx.UserId))
        {
            notifier.Notify(user, UserNotificationKind.ProcurementStatus, title, body, link);
        }
    }

    private static string? Text(string? value, int max, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.Length <= max ? text : throw new FieldValidationException($"{field} — до {max} символов.");
    }
}

/// <summary>Уведомления контроля качества, общие для прихода на складе и загрузки журнала: новая партия на контроле — лаборатории и назначенным.</summary>
internal static class ProcurementQualityNotices
{
    /// <summary>Назначенные решать по качеству (действующие).</summary>
    public static Task<List<Guid>> DecidersAsync(IAppDbContext db, CancellationToken ct) =>
        db.ProcurementQualityApprovers.AsNoTracking().Where(a => a.User!.IsActive).Select(a => a.UserId).ToListAsync(ct);

    /// <summary>Новые проверки операции — одним уведомлением на получателя (лаборатория и назначенные); одна проверка — ссылка на неё.</summary>
    public static async Task NewChecksAsync(IAppDbContext db, UserNotifier notifier, IReadOnlyList<ProcurementQualityCheck> checks, CancellationToken ct)
    {
        if (checks.Count == 0)
        {
            return;
        }

        var itemIds = checks.Select(c => c.ItemId).Distinct().ToList();
        var names = await db.ProcurementItems.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Name, ct);
        var labels = checks.Select(c => $"{names.GetValueOrDefault(c.ItemId, "Позиция")} — {c.Batch?.Number ?? c.Number}").ToList();
        var people = (await ProcurementPeople.WithPermissionAsync(db, Permissions.QualityCheck, ct)).Concat(await DecidersAsync(db, ct)).Distinct().ToList();
        var title = checks.Count == 1 ? $"Партия на контроле качества: {labels[0]}" : $"На контроле качества: {checks.Count} парт.";
        var body = string.Join("; ", labels.Take(5)) + (labels.Count > 5 ? $" и ещё {labels.Count - 5}" : "");
        var link = checks.Count == 1 ? $"/production/quality/{checks[0].Id}" : "/production/quality";
        foreach (var user in people)
        {
            notifier.Notify(user, UserNotificationKind.ProcurementRequest, title, body, link);
        }
    }
}
