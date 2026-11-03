using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

/// <summary>Позиция тендерной закупки из формы: наименование, единица, количество, цена за единицу (сум).</summary>
public sealed record ProcurementTenderLineInput(string? Name, string? UnitCode, decimal Quantity, decimal Price);

/// <summary>
/// Тендерная закупка из формы: предмет, номер и дата тендера, победитель (поставщик из справочника или название — новый добавится сам),
/// категория, договор, участники, срок поставки, комментарий, позиции. ClientKey — ключ повтора формы; Version — версия при правке.
/// </summary>
public sealed record ProcurementTenderInput(
    string? Title,
    string? TenderNo,
    DateOnly? TenderDate,
    Guid? SupplierId,
    string? SupplierName,
    Guid? CategoryId,
    string? ContractNo,
    int? Participants,
    DateOnly? DeliveryDate,
    string? Comment,
    IReadOnlyList<ProcurementTenderLineInput>? Items,
    Guid? ClientKey = null,
    uint? Version = null);

/// <summary>Строка журнала: Overdue — договор, срок поставки прошёл, а поставки нет.</summary>
public sealed record ProcurementTenderRow(
    Guid Id,
    string Number,
    string Title,
    string? TenderNo,
    DateOnly TenderDate,
    Guid SupplierId,
    string SupplierName,
    string CategoryName,
    ProcurementTenderStatus Status,
    DateOnly? DeliveryDate,
    DateOnly? DeliveredOn,
    bool Overdue,
    decimal Total,
    int Lines,
    string? FirstItem,
    string ResponsibleName);

/// <summary>
/// Фильтр журнала: поиск (номер, предмет, номер тендера и договора, поставщик, позиция), статус (waiting — ждут поставки, overdue —
/// просрочены, delivered, cancelled), период по дате тендера, поставщик.
/// </summary>
public sealed record ProcurementTenderSearch(string? Search = null, string? Status = null, DateOnly? From = null, DateOnly? To = null, Guid? SupplierId = null,
    int Page = 1, int PageSize = 50);

/// <summary>Журнал: страница записей и итоги по фильтру (без учёта отменённых — сумма), ждут поставки и просрочены — по всему журналу.</summary>
public sealed record ProcurementTenderList(
    IReadOnlyList<ProcurementTenderRow> Items,
    int Total,
    int Page,
    int PageSize,
    decimal Amount,
    int Waiting,
    int Overdue,
    bool CanCreate);

public sealed record ProcurementTenderItemView(Guid Id, int LineNo, string Name, string UnitCode, decimal Quantity, decimal Price, decimal Sum);

/// <summary>Что можно с записью: изменить, отметить поставку, отменить, прикрепить документы — снабжение; отменённую — ничего.</summary>
public sealed record ProcurementTenderAbilities(bool Edit, bool Deliver, bool Cancel, bool Attach);

public sealed record ProcurementTenderDetail(
    Guid Id,
    string Number,
    string Title,
    string? TenderNo,
    DateOnly TenderDate,
    Guid SupplierId,
    string SupplierName,
    Guid CategoryId,
    string CategoryName,
    string? ContractNo,
    int? Participants,
    DateOnly? DeliveryDate,
    ProcurementTenderStatus Status,
    DateOnly? DeliveredOn,
    DateTimeOffset? CancelledAt,
    bool Overdue,
    decimal Total,
    string? Comment,
    string ResponsibleName,
    DateTimeOffset CreatedAt,
    uint Version,
    IReadOnlyList<ProcurementTenderItemView> Items,
    IReadOnlyList<ProcurementDocumentView> Documents,
    IReadOnlyList<ProcurementEventView> History,
    ProcurementTenderAbilities Can);

/// <summary>
/// Журнал тендерных закупок снабжения: сырьё и материалы для производства, закупленные через тендер. Записи добавляет и правит снабжение
/// (каждое изменение — в истории: было и стало), отмечает поставку; ошибочную запись отменяет с причиной — не удаляет. Видят снабжение и
/// директор. На склад снабжения записи не приходуются.
/// </summary>
public sealed class ProcurementTenderService(IAppDbContext db, IProcurementNumbers numbers, ProcurementDocumentService documents)
{
    public const string Doc = "tender";
    public const string Prefix = "TN";
    public const int MaxItems = 50;

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    private static void RequireView(ProcurementContext ctx)
    {
        if (!ctx.ReadsAll && !ctx.IsDirector)
        {
            throw new FieldForbiddenException("Тендерные закупки видят снабжение и директор.");
        }
    }

    private static void RequireManage(ProcurementContext ctx)
    {
        if (!ctx.Manages)
        {
            throw new FieldForbiddenException("Тендерные закупки ведёт снабжение.");
        }
    }

    // ---------- Журнал ----------

    public async Task<ProcurementTenderList> SearchAsync(ProcurementContext ctx, ProcurementTenderSearch s, CancellationToken ct)
    {
        RequireView(ctx);
        var today = FieldClock.Today;
        var q = db.ProcurementTenders.AsNoTracking();
        if (s.Search?.Trim().ToLower() is { Length: > 0 } text)
        {
            q = q.Where(t => t.Number.ToLower().Contains(text) || t.Title.ToLower().Contains(text) || t.Supplier!.Name.ToLower().Contains(text)
                             || (t.TenderNo != null && t.TenderNo.ToLower().Contains(text)) || (t.ContractNo != null && t.ContractNo.ToLower().Contains(text))
                             || t.Items.Any(i => i.Name.ToLower().Contains(text)));
        }

        q = s.Status switch
        {
            "waiting" => q.Where(t => t.Status == ProcurementTenderStatus.Contracted),
            "overdue" => q.Where(t => t.Status == ProcurementTenderStatus.Contracted && t.DeliveryDate != null && t.DeliveryDate < today),
            "delivered" => q.Where(t => t.Status == ProcurementTenderStatus.Delivered),
            "cancelled" => q.Where(t => t.Status == ProcurementTenderStatus.Cancelled),
            _ => q,
        };
        if (s.From is { } from)
        {
            q = q.Where(t => t.TenderDate >= from);
        }

        if (s.To is { } to)
        {
            q = q.Where(t => t.TenderDate <= to);
        }

        if (s.SupplierId is { } supplier)
        {
            q = q.Where(t => t.SupplierId == supplier);
        }

        var total = await q.CountAsync(ct);
        var amount = await q.Where(t => t.Status != ProcurementTenderStatus.Cancelled).SumAsync(t => (decimal?)t.Total, ct) ?? 0m;
        var page = Math.Max(1, s.Page);
        var size = Math.Clamp(s.PageSize, 1, 200);
        var rows = await q.OrderByDescending(t => t.TenderDate).ThenByDescending(t => t.CreatedAt).Skip((page - 1) * size).Take(size)
            .Select(t => new ProcurementTenderRow(t.Id, t.Number, t.Title, t.TenderNo, t.TenderDate, t.SupplierId, t.Supplier!.Name, t.Category!.Name, t.Status,
                t.DeliveryDate, t.DeliveredOn, t.Status == ProcurementTenderStatus.Contracted && t.DeliveryDate != null && t.DeliveryDate < today, t.Total,
                t.Items.Count, t.Items.OrderBy(i => i.LineNo).Select(i => i.Name).FirstOrDefault(), t.Responsible!.FullName))
            .ToListAsync(ct);
        var waiting = await db.ProcurementTenders.CountAsync(t => t.Status == ProcurementTenderStatus.Contracted, ct);
        var overdue = await db.ProcurementTenders.CountAsync(t => t.Status == ProcurementTenderStatus.Contracted && t.DeliveryDate != null && t.DeliveryDate < today, ct);
        return new ProcurementTenderList(rows, total, page, size, Math.Round(amount, 2), waiting, overdue, ctx.Manages);
    }

    public async Task<ProcurementTenderDetail> GetAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireView(ctx);
        var t = await db.ProcurementTenders.AsNoTracking().Include(x => x.Items).Include(x => x.Supplier).Include(x => x.Category).Include(x => x.Responsible)
                    .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new FieldNotFoundException("Тендерная закупка не найдена.");
        var history = await db.ProcurementAuditLogs.AsNoTracking().Where(e => e.DocType == Doc && e.DocId == id).OrderBy(e => e.CreatedAt)
            .Select(e => new ProcurementEventView(e.Id, e.Action, e.Text, e.Actor == null ? "Система" : e.Actor.FullName, e.FromStatus, e.ToStatus, e.Comment, e.CreatedAt))
            .ToListAsync(ct);
        var open = t.Status != ProcurementTenderStatus.Cancelled;
        var can = new ProcurementTenderAbilities(ctx.Manages && open, ctx.Manages && t.Status == ProcurementTenderStatus.Contracted, ctx.Manages && open, ctx.Manages);
        return new ProcurementTenderDetail(t.Id, t.Number, t.Title, t.TenderNo, t.TenderDate, t.SupplierId, t.Supplier!.Name, t.CategoryId, t.Category!.Name, t.ContractNo,
            t.Participants, t.DeliveryDate, t.Status, t.DeliveredOn, t.CancelledAt, Overdue(t), t.Total, t.Comment, t.Responsible!.FullName, t.CreatedAt, t.Version,
            t.Items.OrderBy(i => i.LineNo).Select(i => new ProcurementTenderItemView(i.Id, i.LineNo, i.Name, i.UnitCode, i.Quantity, i.Price, Math.Round(i.Quantity * i.Price, 2)))
                .ToList(),
            await documents.ListAsync(ctx, Doc, id, ct), history, can);
    }

    private static bool Overdue(ProcurementTender t) =>
        t.Status == ProcurementTenderStatus.Contracted && t.DeliveryDate is { } d && d < FieldClock.Today;

    // ---------- Запись ----------

    public async Task<Guid> CreateAsync(ProcurementContext ctx, ProcurementTenderInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        if (input.ClientKey is { } key)
        {
            var done = await db.ProcurementTenders.AsNoTracking().Where(t => t.ResponsibleId == ctx.UserId && t.ClientKey == key).Select(t => t.Id).FirstOrDefaultAsync(ct);
            if (done != Guid.Empty)
            {
                return done;
            }
        }

        var tender = new ProcurementTender { Number = "", Title = "", ResponsibleId = ctx.UserId, ClientKey = input.ClientKey };
        var (supplier, created) = await ApplyAsync(ctx, tender, input, ct);
        tender.Number = await numbers.NextAsync(Prefix, ct);
        db.ProcurementTenders.Add(tender);
        Log(ctx, tender.Id, "created", null, tender.Status,
            $"Добавлена тендерная закупка: {tender.Title} — {supplier.Name}, {ProcurementRules.Money(tender.Total)} сум", null);
        if (created)
        {
            SupplierLog(ctx, supplier.Id, $"Поставщик добавлен из тендерной закупки {tender.Number}");
        }

        await db.SaveChangesAsync(ct);
        return tender.Id;
    }

    /// <summary>Правка записи (кроме отменённой): в истории — что изменилось, было и стало.</summary>
    public async Task UpdateAsync(ProcurementContext ctx, Guid id, ProcurementTenderInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var tender = await TrackedAsync(id, ct);
        if (tender.Status == ProcurementTenderStatus.Cancelled)
        {
            throw new FieldConflictException("Тендерная закупка отменена — изменить её нельзя.");
        }

        if (input.Version is { } version && version != tender.Version)
        {
            throw new FieldConflictException("Запись уже изменили в другом окне — обновите страницу.");
        }

        var before = Describe(tender, tender.Supplier!.Name, tender.Category!.Name);
        var (supplier, created) = await ApplyAsync(ctx, tender, input, ct);
        var category = await db.ProcurementCategories.AsNoTracking().Where(c => c.Id == tender.CategoryId).Select(c => c.Name).FirstAsync(ct);
        var after = Describe(tender, supplier.Name, category);
        var changes = before.Where(b => after.GetValueOrDefault(b.Key) != b.Value).Select(b => $"{b.Key}: {b.Value} → {after[b.Key]}").ToList();
        if (changes.Count == 0)
        {
            return;
        }

        tender.UpdatedAt = Now;
        Log(ctx, tender.Id, "updated", null, null, $"Изменено: {string.Join("; ", changes)}", null);
        if (created)
        {
            SupplierLog(ctx, supplier.Id, $"Поставщик добавлен из тендерной закупки {tender.Number}");
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Поставлено (по договору): дата — сегодня или указанная, не позже сегодня и не раньше даты тендера.</summary>
    public async Task DeliveredAsync(ProcurementContext ctx, Guid id, DateOnly? date, CancellationToken ct)
    {
        RequireManage(ctx);
        var tender = await TrackedAsync(id, ct);
        if (tender.Status == ProcurementTenderStatus.Delivered)
        {
            return;
        }

        if (tender.Status != ProcurementTenderStatus.Contracted)
        {
            throw new FieldConflictException("Тендерная закупка отменена — отметить поставку нельзя.");
        }

        var day = date ?? FieldClock.Today;
        if (day > FieldClock.Today)
        {
            throw new FieldValidationException("Дата поставки — не позже сегодняшней.");
        }

        if (day < tender.TenderDate)
        {
            throw new FieldValidationException("Дата поставки — не раньше даты тендера.");
        }

        tender.Status = ProcurementTenderStatus.Delivered;
        tender.DeliveredOn = day;
        tender.UpdatedAt = Now;
        Log(ctx, tender.Id, "delivered", ProcurementTenderStatus.Contracted, tender.Status, $"Поставлено {day:dd.MM.yyyy}", null);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Отменить ошибочную или сорванную запись — с причиной; запись остаётся в журнале.</summary>
    public async Task CancelAsync(ProcurementContext ctx, Guid id, string? comment, CancellationToken ct)
    {
        RequireManage(ctx);
        var tender = await TrackedAsync(id, ct);
        if (tender.Status == ProcurementTenderStatus.Cancelled)
        {
            return;
        }

        var reason = Text(comment, 2000, "Причина") ?? throw new FieldValidationException("Напишите, почему запись отменяется.");
        var from = tender.Status;
        tender.Status = ProcurementTenderStatus.Cancelled;
        tender.CancelledAt = Now;
        tender.UpdatedAt = Now;
        Log(ctx, tender.Id, "cancelled", from, tender.Status, "Отменена", reason);
        await db.SaveChangesAsync(ct);
    }

    private async Task<ProcurementTender> TrackedAsync(Guid id, CancellationToken ct) =>
        await db.ProcurementTenders.Include(t => t.Items).Include(t => t.Supplier).Include(t => t.Category).FirstOrDefaultAsync(t => t.Id == id, ct)
        ?? throw new FieldNotFoundException("Тендерная закупка не найдена.");

    /// <summary>Поля формы → запись: проверки и позиции (заменяются целиком); итог — сумма позиций.</summary>
    private async Task<(ProcurementSupplier Supplier, bool Created)> ApplyAsync(ProcurementContext ctx, ProcurementTender tender, ProcurementTenderInput input,
        CancellationToken ct)
    {
        var title = Text(input.Title, 300, "Предмет тендера") ?? throw new FieldValidationException("Укажите предмет тендера: что закупали.");
        var date = input.TenderDate ?? throw new FieldValidationException("Укажите дату тендера.");
        if (date > FieldClock.Today)
        {
            throw new FieldValidationException("Дата тендера — не позже сегодняшней: в журнал записывают проведённые тендеры.");
        }

        if (input.DeliveryDate is { } delivery && delivery < date)
        {
            throw new FieldValidationException("Срок поставки — не раньше даты тендера.");
        }

        if (input.Participants is < 1 or > 1000)
        {
            throw new FieldValidationException("Участников — от 1 до 1000.");
        }

        if (input.CategoryId is not { } categoryId || !await db.ProcurementCategories.AnyAsync(c => c.Id == categoryId && (c.IsActive || c.Id == tender.CategoryId), ct))
        {
            throw new FieldValidationException("Выберите категорию.");
        }

        var lines = (input.Items ?? []).ToList();
        if (lines.Count == 0)
        {
            throw new FieldValidationException("Добавьте хотя бы одну позицию: что закупили, сколько и почём.");
        }

        if (lines.Count > MaxItems)
        {
            throw new FieldValidationException($"Позиций — до {MaxItems}.");
        }

        var units = await db.ProcurementUnits.AsNoTracking().Select(u => u.Code).ToListAsync(ct);
        var items = new List<ProcurementTenderItem>();
        foreach (var (line, no) in lines.Select((l, i) => (l, i + 1)))
        {
            var name = Text(line.Name, 300, $"Позиция {no}") ?? throw new FieldValidationException($"Позиция {no}: укажите наименование.");
            var unit = line.UnitCode?.Trim();
            if (string.IsNullOrEmpty(unit) || !units.Contains(unit))
            {
                throw new FieldValidationException($"«{name}»: выберите единицу измерения.");
            }

            if (line.Quantity <= 0 || line.Quantity > ProcurementRules.MaxQuantity || decimal.Round(line.Quantity, 3) != line.Quantity)
            {
                throw new FieldValidationException($"«{name}»: количество — больше нуля, не больше трёх знаков после запятой.");
            }

            if (line.Price <= 0 || line.Price > ProcurementRules.MaxPrice || decimal.Round(line.Price, 2) != line.Price)
            {
                throw new FieldValidationException($"«{name}»: цена за {unit} — больше нуля, не больше двух знаков после запятой.");
            }

            items.Add(new ProcurementTenderItem { TenderId = tender.Id, LineNo = no, Name = name, UnitCode = unit, Quantity = line.Quantity, Price = line.Price });
        }

        var supplier = await ProcurementSupplierService.ResolveAsync(db, ctx, input.SupplierId, input.SupplierName, "Укажите победителя тендера — поставщика.", ct);
        tender.Title = title;
        tender.TenderNo = Text(input.TenderNo, 100, "Номер тендера");
        tender.TenderDate = date;
        tender.SupplierId = supplier.Supplier.Id;
        tender.Supplier = supplier.Supplier;
        tender.CategoryId = categoryId;
        tender.ContractNo = Text(input.ContractNo, 100, "Номер договора");
        tender.Participants = input.Participants;
        tender.DeliveryDate = input.DeliveryDate;
        tender.Comment = Text(input.Comment, 2000, "Комментарий");
        foreach (var old in tender.Items.ToList())
        {
            db.ProcurementTenderItems.Remove(old);
        }

        tender.Items.Clear();
        foreach (var item in items)
        {
            tender.Items.Add(item);
            db.ProcurementTenderItems.Add(item);
        }

        tender.Total = items.Sum(i => Math.Round(i.Quantity * i.Price, 2));
        return supplier;
    }

    /// <summary>Поля записи для истории правок: подпись → значение.</summary>
    private static Dictionary<string, string> Describe(ProcurementTender t, string supplier, string category) => new()
    {
        ["предмет"] = t.Title,
        ["номер тендера"] = t.TenderNo ?? "—",
        ["дата тендера"] = t.TenderDate.ToString("dd.MM.yyyy"),
        ["поставщик"] = supplier,
        ["категория"] = category,
        ["договор"] = t.ContractNo ?? "—",
        ["участников"] = t.Participants?.ToString() ?? "—",
        ["срок поставки"] = t.DeliveryDate?.ToString("dd.MM.yyyy") ?? "—",
        ["комментарий"] = t.Comment ?? "—",
        ["позиции"] = string.Join(", ", t.Items.OrderBy(i => i.LineNo)
            .Select(i => $"{i.Name} {ProcurementRules.Quantity(i.Quantity)} {i.UnitCode} × {ProcurementRules.Money(i.Price)}")),
        ["сумма"] = $"{ProcurementRules.Money(t.Total)} сум",
    };

    private void Log(ProcurementContext ctx, Guid id, string action, ProcurementTenderStatus? from, ProcurementTenderStatus? to, string text, string? comment) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog
        {
            DocType = Doc, DocId = id, ActorId = ctx.UserId, Action = action, FromStatus = from?.ToString(), ToStatus = to?.ToString(), Text = Cut(text, 1000),
            Comment = comment,
        });

    private void SupplierLog(ProcurementContext ctx, Guid supplierId, string text) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog { DocType = ProcurementOrderRules.SupplierDoc, DocId = supplierId, ActorId = ctx.UserId, Action = "created", Text = text });

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

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
