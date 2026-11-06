using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

public sealed record ProcurementSupplierRow(
    Guid Id,
    string Name,
    string? Inn,
    string? ContactPerson,
    string? Phone,
    IReadOnlyList<string> Categories,
    bool IsActive,
    int Orders,
    decimal OrdersTotal,
    int Overdue,
    int? Rating,
    DateTimeOffset? LastOrderAt,
    int? LeadTimeDays,
    string? LegalName = null,
    string? Products = null);

/// <summary>Реестр поставщиков: поиск (название, фирма, ИНН, контакт, телефон, что поставляет), категория, активные/отключённые, сортировка.</summary>
public sealed record ProcurementSupplierSearch(string? Search = null, Guid? CategoryId = null, bool? Active = true, string? Sort = null, bool Desc = false, int Page = 1, int PageSize = 50);

public sealed record ProcurementSupplierInput(
    string? Name,
    string? Inn,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address,
    string? BankName,
    string? BankMfo,
    string? BankAccount,
    string? PaymentTerms,
    int? LeadTimeDays,
    string? Notes,
    IReadOnlyList<Guid>? CategoryIds,
    bool IsActive = true,
    string? LegalName = null,
    string? Products = null);

/// <summary>
/// Рейтинг поставщика: итог 0–100 и составляющие (null — нет данных): сроки (доля заказов без просрочки среди тех, чей срок наступил),
/// качество (оценка предложений, 1–5 → 0–100), соблюдение цен (доля строк заказа не дороже предложения). Претензии и полнота — после приёмки.
/// Претензии — доля без проблем среди строк приёмок (брак, недостача) и решений по партиям поставщика (забракована — проблема):
/// QualityChecks — решений по партиям, RejectedBatches — из них забраковано.
/// </summary>
public sealed record ProcurementSupplierRating(
    int? Overall,
    decimal? OnTime,
    decimal? Quality,
    decimal? Prices,
    int DueOrders,
    int OverdueOrders,
    int RatedOffers,
    int PricedLines,
    decimal? Claims = null,
    decimal? Completeness = null,
    int ReceiptLines = 0,
    int ClaimLines = 0,
    int FinishedOrders = 0,
    int LateOrders = 0,
    int QualityChecks = 0,
    int RejectedBatches = 0);

public sealed record ProcurementSupplierOrderRow(Guid Id, string Number, ProcurementOrderStatus Status, DateOnly? OrderDate, DateOnly? ExpectedDate, decimal Total, bool Overdue, int Lines);

public sealed record ProcurementSupplierPriceRow(DateOnly Date, Guid RequestItemId, string ItemName, string UnitCode, decimal UnitPrice, decimal Quantity, string OrderNumber, Guid OrderId);

public sealed record ProcurementCategoryRef(Guid Id, string Name);

public sealed record ProcurementSupplierCard(
    Guid Id,
    string Name,
    string? Inn,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address,
    string? BankName,
    string? BankMfo,
    string? BankAccount,
    string? PaymentTerms,
    int? LeadTimeDays,
    string? Notes,
    bool IsActive,
    IReadOnlyList<ProcurementCategoryRef> Categories,
    ProcurementSupplierRating Rating,
    IReadOnlyList<ProcurementSupplierOrderRow> Orders,
    IReadOnlyList<ProcurementSupplierPriceRow> Prices,
    int Offers,
    decimal OrdersTotal,
    bool CanEdit,
    IReadOnlyList<ProcurementClaimView>? Claims = null,
    string? LegalName = null,
    string? Products = null);

/// <summary>
/// Итог импорта (или проверки файла — DryRun, ничего не сохранено): сколько добавится, обновится и пропустится; Columns — какой столбец файла
/// куда пошёл («Название ← «Поставшик»»); Joined — строки-продолжения, присоединённые к поставщику выше; Duplicates — повторы в файле, объединённые
/// в одного поставщика; Inactive — сколько будут отключены («не работаем»); Sample — первые поставщики, как они будут записаны.
/// </summary>
public sealed record ProcurementSupplierImportResult(
    int Created,
    int Updated,
    int Skipped,
    IReadOnlyList<string> Errors,
    bool DryRun = false,
    IReadOnlyList<string>? Columns = null,
    int Joined = 0,
    int Duplicates = 0,
    int Inactive = 0,
    IReadOnlyList<ProcurementSupplierImportSample>? Sample = null);

public sealed record ProcurementSupplierImportSample(int Row, string Name, string? LegalName, string? Inn, string? Phone, string? ContactPerson, int Products, bool IsActive, bool IsNew);

/// <summary>
/// Поставщики снабжения: реестр с аналитикой (заказы, сумма, просрочки, рейтинг), карточка (реквизиты, категории, история заказов, цен
/// и просрочек), ручное добавление и импорт из Excel/CSV (по ИНН, без ИНН — по названию). Видят снабжение, директор и кладовщик; ведёт
/// реестр снабжение.
/// </summary>
public sealed class ProcurementSupplierService(IAppDbContext db, IReportReader reader)
{
    private static readonly ProcurementOrderStatus[] Placed =
    [
        ProcurementOrderStatus.Approved, ProcurementOrderStatus.Ordered, ProcurementOrderStatus.PartiallyDelivered, ProcurementOrderStatus.Delivered,
        ProcurementOrderStatus.Completed,
    ];

    private static void RequireView(ProcurementContext ctx)
    {
        if (!ctx.ReadsAll && !ctx.IsDirector && !ctx.Warehouse)
        {
            throw new FieldForbiddenException("Поставщиков видят снабжение, директор и кладовщик.");
        }
    }

    private static void RequireManage(ProcurementContext ctx)
    {
        if (!ctx.Manages)
        {
            throw new FieldForbiddenException("Реестр поставщиков ведёт снабжение.");
        }
    }

    // ---------- Реестр ----------

    public async Task<PageResult<ProcurementSupplierRow>> SearchAsync(ProcurementContext ctx, ProcurementSupplierSearch s, CancellationToken ct)
    {
        RequireView(ctx);
        var q = db.ProcurementSuppliers.AsNoTracking();
        if (s.Active is { } active)
        {
            q = q.Where(x => x.IsActive == active);
        }

        if (s.CategoryId is { } category)
        {
            q = q.Where(x => x.Categories.Any(c => c.CategoryId == category));
        }

        if (!string.IsNullOrWhiteSpace(s.Search))
        {
            var text = s.Search.Trim().ToLower();
            q = q.Where(x => x.Name.ToLower().Contains(text) || (x.LegalName != null && x.LegalName.ToLower().Contains(text)) || (x.Inn != null && x.Inn.Contains(text))
                             || (x.ContactPerson != null && x.ContactPerson.ToLower().Contains(text)) || (x.Phone != null && x.Phone.Contains(text))
                             || (x.Products != null && x.Products.ToLower().Contains(text)));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(s.PageSize, 5, 200);
        var page = Math.Max(s.Page, 1);
        var placed = Placed;
        var stats = db.ProcurementPurchaseOrders.AsNoTracking().Where(o => placed.Contains(o.Status));
        var rows = await q
            .Select(x => new
            {
                x.Id, x.Name, x.Inn, x.ContactPerson, x.Phone, x.IsActive, x.LeadTimeDays, x.LegalName, x.Products,
                Categories = x.Categories.Select(c => c.Category!.Name).ToList(),
                Orders = stats.Count(o => o.SupplierId == x.Id),
                Total = stats.Where(o => o.SupplierId == x.Id).Sum(o => (decimal?)o.Total) ?? 0m,
                Last = stats.Where(o => o.SupplierId == x.Id).Max(o => (DateTimeOffset?)o.CreatedAt),
            })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        // Сортировка по аналитике — здесь: поставщиков немного, рейтинг считается из нескольких источников.
        var ratings = await RatingsAsync(rows.Select(r => r.Id).ToList(), ct);
        var list = rows.Select(r => new ProcurementSupplierRow(r.Id, r.Name, r.Inn, r.ContactPerson, r.Phone, r.Categories.Order().ToList(), r.IsActive, r.Orders,
                r.Total, ratings[r.Id].OverdueOrders, ratings[r.Id].Overall, r.Last, r.LeadTimeDays, r.LegalName, r.Products))
            .ToList();
        IEnumerable<ProcurementSupplierRow> sorted = s.Sort switch
        {
            "rating" => s.Desc ? list.OrderByDescending(r => r.Rating ?? -1) : list.OrderBy(r => r.Rating ?? 101),
            "orders" => s.Desc ? list.OrderByDescending(r => r.Orders) : list.OrderBy(r => r.Orders),
            "total" => s.Desc ? list.OrderByDescending(r => r.OrdersTotal) : list.OrderBy(r => r.OrdersTotal),
            "overdue" => s.Desc ? list.OrderByDescending(r => r.Overdue) : list.OrderBy(r => r.Overdue),
            "last" => s.Desc ? list.OrderByDescending(r => r.LastOrderAt) : list.OrderBy(r => r.LastOrderAt),
            _ => s.Desc ? list.OrderByDescending(r => r.Name) : list,
        };
        return new PageResult<ProcurementSupplierRow>(sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList(), total, page, pageSize);
    }

    /// <summary>Активные поставщики для выбора в предложении и заказе (название, ИНН, категории, рейтинг).</summary>
    public async Task<List<ProcurementSupplierRow>> OptionsAsync(ProcurementContext ctx, CancellationToken ct) =>
        (await SearchAsync(ctx, new ProcurementSupplierSearch(PageSize: 200), ct)).Items.ToList();

    // ---------- Карточка ----------

    public async Task<ProcurementSupplierCard> GetAsync(ProcurementContext ctx, Guid id, CancellationToken ct)
    {
        RequireView(ctx);
        var s = await db.ProcurementSuppliers.AsNoTracking().Include(x => x.Categories).ThenInclude(c => c.Category).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new FieldNotFoundException("Поставщик не найден.");
        var today = FieldClock.Today;
        var orders = await db.ProcurementPurchaseOrders.AsNoTracking().Where(o => o.SupplierId == id && o.Status != ProcurementOrderStatus.Draft)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new { o.Id, o.Number, o.Status, o.OrderDate, o.ExpectedDate, o.Total, Lines = o.Items.Count })
            .ToListAsync(ct);
        var prices = await db.ProcurementPurchaseOrderItems.AsNoTracking()
            .Where(i => i.Order!.SupplierId == id && Placed.Contains(i.Order.Status))
            .OrderByDescending(i => i.Order!.CreatedAt)
            .Take(100)
            .Select(i => new { i.RequestItemId, i.Name, i.UnitCode, i.UnitPrice, i.Quantity, i.Order!.Number, i.OrderId, i.Order.OrderDate, i.Order.CreatedAt })
            .ToListAsync(ct);
        var rating = (await RatingsAsync([id], ct))[id];
        return new ProcurementSupplierCard(s.Id, s.Name, s.Inn, s.ContactPerson, s.Phone, s.Email, s.Address, s.BankName, s.BankMfo, s.BankAccount, s.PaymentTerms,
            s.LeadTimeDays, s.Notes, s.IsActive,
            s.Categories.Select(c => new ProcurementCategoryRef(c.CategoryId, c.Category!.Name)).OrderBy(c => c.Name).ToList(), rating,
            orders.Select(o => new ProcurementSupplierOrderRow(o.Id, o.Number, o.Status, o.OrderDate, o.ExpectedDate, o.Total,
                ProcurementOrderRules.IsOverdue(o.Status, o.ExpectedDate, today), o.Lines)).ToList(),
            prices.Select(p => new ProcurementSupplierPriceRow(p.OrderDate ?? DateOnly.FromDateTime(p.CreatedAt.ToOffset(FieldClock.Offset).DateTime), p.RequestItemId,
                p.Name, p.UnitCode, p.UnitPrice, p.Quantity, p.Number, p.OrderId)).ToList(),
            await db.ProcurementOffers.CountAsync(o => o.SupplierId == id, ct),
            orders.Where(o => Placed.Contains(o.Status)).Sum(o => o.Total),
            ctx.Manages,
            await ProcurementReceiptService.ClaimsAsync(db, null, id, ct),
            s.LegalName,
            s.Products);
    }

    /// <summary>Рейтинг поставщиков (см. ProcurementSupplierRating) — пачкой по списку.</summary>
    internal async Task<Dictionary<Guid, ProcurementSupplierRating>> RatingsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var today = FieldClock.Today;
        var live = ProcurementOrderRules.Live;
        // Срок наступил или поставка уже была: опоздание — просрочен сейчас или поставлен позже ожидаемой даты.
        var due = (await db.ProcurementPurchaseOrders.AsNoTracking()
                .Where(o => ids.Contains(o.SupplierId) && Placed.Contains(o.Status) && o.ExpectedDate != null && (o.ExpectedDate < today || o.DeliveredAt != null))
                .Select(o => new { o.SupplierId, o.Status, o.ExpectedDate, o.DeliveredAt })
                .ToListAsync(ct))
            .Select(o => new
            {
                o.SupplierId,
                Overdue = live.Contains(o.Status) && o.ExpectedDate < today,
                Late = (live.Contains(o.Status) && o.ExpectedDate < today)
                       || (o.DeliveredAt is { } at && FieldClock.LocalDate(at) > o.ExpectedDate),
            })
            .ToList();
        var receipts = await db.ProcurementReceiptLines.AsNoTracking()
            .Where(l => ids.Contains(l.Document!.Order!.SupplierId) && l.Document.Status == ProcurementStockDocumentStatus.Posted)
            .GroupBy(l => l.Document!.Order!.SupplierId)
            .Select(g => new { g.Key, Lines = g.Count(), Problems = g.Count(l => l.Defective > 0 || l.Shortage > 0) })
            .ToDictionaryAsync(x => x.Key, ct);
        // Решения лаборатории по партиям поставщика: забракованная партия — такая же проблема поставки, как брак при приёмке.
        var decided = await db.ProcurementQualityChecks.AsNoTracking()
            .Where(c => c.Status == ProcurementQualityCheckStatus.Decided && c.Batch!.SupplierId != null && ids.Contains(c.Batch.SupplierId.Value))
            .GroupBy(c => c.Batch!.SupplierId!.Value)
            .Select(g => new { g.Key, Checks = g.Count(), Rejected = g.Count(c => c.Decision == ProcurementBatchQuality.Rejected) })
            .ToDictionaryAsync(x => x.Key, ct);
        var finished = new[] { ProcurementOrderStatus.Delivered, ProcurementOrderStatus.Completed };
        var completeness = await db.ProcurementPurchaseOrderItems.AsNoTracking()
            .Where(i => ids.Contains(i.Order!.SupplierId) && finished.Contains(i.Order.Status))
            .GroupBy(i => i.Order!.SupplierId)
            .Select(g => new { g.Key, Ordered = g.Sum(i => i.Quantity), Received = g.Sum(i => i.ReceivedQty), Orders = g.Select(i => i.OrderId).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, ct);
        var quality = await db.ProcurementOffers.AsNoTracking().Where(o => ids.Contains(o.SupplierId) && o.Quality != null)
            .Select(o => new { o.SupplierId, Quality = o.Quality!.Value })
            .ToListAsync(ct);
        var priced = await db.ProcurementPurchaseOrderItems.AsNoTracking()
            .Where(i => ids.Contains(i.Order!.SupplierId) && i.Order.OfferId != null && Placed.Contains(i.Order.Status))
            .Select(i => new
            {
                i.Order!.SupplierId,
                i.UnitPrice,
                Offered = db.ProcurementOfferItems.Where(x => x.OfferId == i.Order.OfferId && x.RequestItemId == i.RequestItemId).Select(x => (decimal?)x.UnitPrice).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return ids.ToDictionary(id => id, id =>
        {
            var d = due.Where(x => x.SupplierId == id).ToList();
            var late = d.Count(x => x.Late);
            var q = quality.Where(x => x.SupplierId == id).ToList();
            var p = priced.Where(x => x.SupplierId == id && x.Offered != null).ToList();
            var r = receipts.GetValueOrDefault(id);
            var c = completeness.GetValueOrDefault(id);
            var qc = decided.GetValueOrDefault(id);
            var lines = (r?.Lines ?? 0) + (qc?.Checks ?? 0);
            var problems = (r?.Problems ?? 0) + (qc?.Rejected ?? 0);
            decimal? onTime = d.Count == 0 ? null : Math.Round(100m * (d.Count - late) / d.Count, 1);
            decimal? qual = q.Count == 0 ? null : Math.Round((decimal)q.Average(x => x.Quality) * 20m, 1);
            decimal? prices = p.Count == 0 ? null : Math.Round(100m * p.Count(x => x.UnitPrice <= x.Offered) / p.Count, 1);
            decimal? claims = lines == 0 ? null : Math.Round(100m * (lines - problems) / lines, 1);
            decimal? complete = c is null || c.Ordered <= 0 ? null : Math.Round(Math.Min(100m, 100m * c.Received / c.Ordered), 1);
            var currentOverdue = d.Count(x => x.Overdue);
            return new ProcurementSupplierRating(ProcurementOrderRules.Rating(onTime, qual, prices, claims, complete), onTime, qual, prices, d.Count, currentOverdue,
                q.Count, p.Count, claims, complete, r?.Lines ?? 0, r?.Problems ?? 0, c?.Orders ?? 0, late, qc?.Checks ?? 0, qc?.Rejected ?? 0);
        });
    }

    // ---------- Правка ----------

    /// <summary>
    /// Поставщик из формы покупки или тендера: из справочника (действующий) или по названию — действующий с тем же названием (без учёта
    /// регистра), а если такого нет — новый с одним названием (остальное заполнят в справочнике; запись в истории — у вызывающего). Created —
    /// поставщик новый (ещё не сохранён).
    /// </summary>
    internal static async Task<(ProcurementSupplier Supplier, bool Created)> ResolveAsync(IAppDbContext db, ProcurementContext ctx, Guid? id, string? name, string what,
        CancellationToken ct)
    {
        if (id is { } supplierId)
        {
            var chosen = await db.ProcurementSuppliers.FirstOrDefaultAsync(s => s.Id == supplierId, ct) ?? throw new FieldValidationException("Поставщик не найден.");
            return chosen.IsActive ? (chosen, false) : throw new FieldValidationException($"Поставщик «{chosen.Name}» отключён — выберите другого.");
        }

        var text = name?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw new FieldValidationException(what);
        }

        if (text.Length > 200)
        {
            throw new FieldValidationException("Название поставщика — до 200 символов.");
        }

        var lower = text.ToLower();
        var existing = await db.ProcurementSuppliers.Where(s => s.IsActive && s.Name.ToLower() == lower).OrderBy(s => s.CreatedAt).FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            return (existing, false);
        }

        var supplier = new ProcurementSupplier { Name = text, CreatedById = ctx.UserId };
        db.ProcurementSuppliers.Add(supplier);
        return (supplier, true);
    }

    public async Task<Guid> CreateAsync(ProcurementContext ctx, ProcurementSupplierInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var supplier = new ProcurementSupplier { Name = "", CreatedById = ctx.UserId };
        await ApplyAsync(supplier, input, ct);
        db.ProcurementSuppliers.Add(supplier);
        Log(ctx, supplier.Id, "created", "Поставщик добавлен");
        await db.SaveChangesAsync(ct);
        return supplier.Id;
    }

    public async Task UpdateAsync(ProcurementContext ctx, Guid id, ProcurementSupplierInput input, CancellationToken ct)
    {
        RequireManage(ctx);
        var supplier = await db.ProcurementSuppliers.Include(x => x.Categories).FirstOrDefaultAsync(x => x.Id == id, ct)
                       ?? throw new FieldNotFoundException("Поставщик не найден.");
        await ApplyAsync(supplier, input, ct);
        supplier.UpdatedAt = DateTimeOffset.UtcNow;
        Log(ctx, supplier.Id, "updated", "Карточка поставщика изменена");
        await db.SaveChangesAsync(ct);
    }

    private async Task ApplyAsync(ProcurementSupplier supplier, ProcurementSupplierInput input, CancellationToken ct)
    {
        var name = Text(input.Name, 200, "Название") ?? throw new FieldValidationException("Укажите название поставщика.");
        var inn = NormalizeInn(input.Inn);
        if (inn is not null && await db.ProcurementSuppliers.AnyAsync(x => x.Id != supplier.Id && x.Inn == inn, ct))
        {
            throw new FieldValidationException($"Поставщик с ИНН {inn} уже есть.");
        }

        var email = Text(input.Email, 200, "Email");
        if (email is not null && !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            throw new FieldValidationException("Email указан неверно.");
        }

        if (input.LeadTimeDays is < 0 or > 365)
        {
            throw new FieldValidationException("Срок поставки — от 0 до 365 дней.");
        }

        var categoryIds = (input.CategoryIds ?? []).Distinct().ToList();
        if (categoryIds.Count > 0 && await db.ProcurementCategories.CountAsync(c => categoryIds.Contains(c.Id), ct) != categoryIds.Count)
        {
            throw new FieldValidationException("Категория не найдена.");
        }

        // Сначала все проверки, потом запись: при ошибке карточка не меняется наполовину.
        var (legalName, contact, phone, address) =
            (Text(input.LegalName, 300, "Фирма"), Text(input.ContactPerson, 200, "Контактное лицо"), Text(input.Phone, 200, "Телефон"), Text(input.Address, 500, "Адрес"));
        var (bank, mfo, account, terms) =
            (Text(input.BankName, 200, "Банк"), Text(input.BankMfo, 20, "МФО"), Text(input.BankAccount, 40, "Расчётный счёт"), Text(input.PaymentTerms, 500, "Условия оплаты"));
        var (products, notes) = (Text(input.Products, 2000, "Что поставляет"), Text(input.Notes, 2000, "Примечание"));
        (supplier.Name, supplier.LegalName, supplier.Inn, supplier.ContactPerson, supplier.Phone, supplier.Email, supplier.Address) = (name, legalName, inn, contact, phone, email, address);
        (supplier.BankName, supplier.BankMfo, supplier.BankAccount, supplier.PaymentTerms) = (bank, mfo, account, terms);
        (supplier.LeadTimeDays, supplier.Products, supplier.Notes, supplier.IsActive) = (input.LeadTimeDays, products, notes, input.IsActive);
        foreach (var gone in supplier.Categories.Where(c => !categoryIds.Contains(c.CategoryId)).ToList())
        {
            supplier.Categories.Remove(gone);
        }

        foreach (var id in categoryIds.Where(id => supplier.Categories.All(c => c.CategoryId != id)))
        {
            supplier.Categories.Add(new ProcurementSupplierCategory { SupplierId = supplier.Id, CategoryId = id });
        }
    }

    /// <summary>ИНН: только цифры (пробелы и дефисы убираются), 9–14 цифр; пусто — null.</summary>
    public static string? NormalizeInn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length is >= 9 and <= 14 ? digits : throw new FieldValidationException($"ИНН «{value.Trim()}» — от 9 до 14 цифр.");
    }

    // ---------- Импорт ----------

    /// <summary>
    /// Заголовки столбцов. Сравнение без регистра и знаков препинания, «ё» = «е»: сначала точное совпадение, затем начало заголовка
    /// («Наименование поставщика», «ИНН поставщика»). Русские, узбекские (кириллица и латиница) названия и частые опечатки («Поставшик»).
    /// </summary>
    private static readonly TableImport.Column[] Headers = TableImport.Columns(
    [
        ("name", "Название", ["название", "наименование", "поставщик", "поставшик", "поставщики", "компания", "контрагент", "етказиб берувчи", "етказувчи", "таъминотчи",
            "yetkazib beruvchi", "nomi", "номи"]),
        ("legal", "Фирма", ["фирма", "фирмаси", "юрлицо", "юр лицо", "юридическое лицо", "юридическое название", "организация", "ташкилот", "firma", "firmasi", "tashkilot"]),
        ("inn", "ИНН", ["инн", "стир", "stir", "inn"]),
        ("contact", "Контактное лицо", ["контактное лицо", "контакт", "контакты", "ответственный", "представитель", "менеджер", "ишловчи ходим", "ходим", "масъул шахс",
            "kontakt"]),
        ("phone", "Телефон", ["телефон", "тел", "тел номер", "номер телефона", "телефоны", "мобильный", "телефон раками", "phone", "tel", "telefon", "telefon raqami"]),
        ("email", "Email", ["email", "e mail", "почта", "эл почта", "электронная почта"]),
        ("address", "Адрес", ["адрес", "город", "регион", "етказувчи манзили", "манзил", "манзили", "шахар", "manzil", "manzili"]),
        ("bank", "Банк", ["банк", "bank"]),
        ("mfo", "МФО", ["мфо", "mfo"]),
        ("account", "Расчётный счёт", ["расчетный счет", "р с", "счет", "хисоб раками", "hisob raqami"]),
        ("terms", "Условия оплаты", ["условия оплаты", "оплата", "тулов шартлари"]),
        ("lead", "Срок поставки", ["срок поставки", "срок поставки дней", "срок дней"]),
        ("categories", "Категории", ["категории", "категория", "группа", "тури", "сирё", "сырье"]),
        ("products", "Что поставляет", ["что поставляет", "что закупаем", "товары", "продукция", "материалы", "номенклатура", "ассортимент", "олинадиган хомашёлар",
            "хомашёлар", "хомашё", "хом ашё", "маҳсулотлар", "mahsulotlar", "xomashyo"]),
        ("active", "Работаем", ["работаем", "работаем сейчас", "статус", "активен", "хозирда ишланади ёки ишланмаяпти", "хозирда ишланади", "ишлаймизми", "холати", "holati"]),
        ("notes", "Примечание", ["примечание", "комментарий", "изох", "izoh"]),
        ("number", "№", ["№", "n", "no", "п п", "номер"]),
    ]);

    /// <summary>Поля, где значений может быть несколько (строки-продолжения добавляют, а не заменяют): как их склеивать.</summary>
    private static readonly Dictionary<string, string> Many = new()
    {
        ["legal"] = ", ", ["contact"] = ", ", ["phone"] = ", ", ["address"] = ", ", ["categories"] = ", ", ["products"] = "\n", ["notes"] = "\n",
    };

    private static readonly HashSet<string> No = ["нет", "йук", "йўқ", "yoq", "yo q", "не работаем", "не работает", "ишламаймиз", "ишланмаяпти", "ишламайди", "отключен", "неактивен", "false", "0"];

    private static readonly HashSet<string> Yes = ["да", "ха", "ҳа", "ha", "работаем", "работает", "ишлаймиз", "ишланади", "активен", "true", "1"];

    private static string Norm(string? text) => TableImport.Norm(text);

    /// <summary>Поставщик из файла: первая строка и строки-продолжения под ней (без названия), значения по полям без повторов.</summary>
    private sealed class ImportRecord(int row, string name)
    {
        public int Row { get; } = row;
        public string Name { get; } = name;
        public Dictionary<string, List<string>> Values { get; } = [];

        public void Add(string field, string value)
        {
            var list = Values.TryGetValue(field, out var existing) ? existing : Values[field] = [];
            if (!list.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(value);
            }
        }

        public string? Get(string field) =>
            Values.TryGetValue(field, out var list) ? Many.TryGetValue(field, out var separator) ? string.Join(separator, list) : list[0] : null;
    }

    /// <summary>
    /// Импорт из Excel/CSV. Строка заголовков — в первых 20 строках любого листа (нужен столбец с названием поставщика), названия столбцов —
    /// см. Headers. Строка без названия — продолжение поставщика выше (ещё телефон, контакт, позиция «что поставляет»); повторы в файле (тот же
    /// ИНН, без ИНН — то же название) объединяются. Значения «нет», «-» — пусто; «Работаем: нет / йўқ» — поставщик отключён; столбцы без
    /// известного заголовка — в примечание. Совпадение с реестром по ИНН, без ИНН — по названию: найден — обновляются заполненные поля,
    /// нет — создаётся. Строка с ошибкой пропускается с пояснением. dryRun — только проверка: ничего не сохраняется.
    /// </summary>
    public async Task<ProcurementSupplierImportResult> ImportAsync(ProcurementContext ctx, Stream content, string fileName, CancellationToken ct, bool dryRun = false)
    {
        RequireManage(ctx);
        ReportDocument document;
        try
        {
            document = reader.Read(content, fileName);
        }
        catch (NotSupportedException e)
        {
            throw new FieldValidationException(e.Message);
        }

        var table = TableImport.Find(document, Headers, found => found.ContainsKey("name"));
        if (table is null)
        {
            throw new FieldValidationException(
                "Не найдена строка заголовков: нужен столбец с названием поставщика («Название», «Наименование», «Поставщик», «Етказиб берувчи»…). Остальные — по желанию: ИНН, Фирма, Телефон, Контактное лицо, Адрес, Что поставляет…");
        }

        // Столбцы с непонятным заголовком (и без заголовка) — в примечание, чтобы ничего из файла не потерялось.
        var (rows, headerIndex, columns) = (table.Rows, table.Header, table.Columns);
        var width = rows.Skip(headerIndex).Max(r => r.Count);
        var extra = Enumerable.Range(0, width).Where(i => !columns.ContainsValue(i)).ToList();
        string Title(int i) => table.Title(i);
        var mapped = TableImport.Mapped(Headers, table, "number");
        if (extra.Any(i => Title(i).Length > 0))
        {
            mapped.Add($"Примечание ← {string.Join(", ", extra.Where(i => Title(i).Length > 0).Select(i => $"«{Title(i)}»"))}");
        }

        // 1. Строки → поставщики.
        var records = new List<ImportRecord>();
        var byKey = new Dictionary<string, ImportRecord>();
        ImportRecord? last = null;
        int skipped = 0, joined = 0, duplicates = 0;
        var errors = new List<string>();
        void Error(string text)
        {
            if (errors.Count < 50)
            {
                errors.Add(text);
            }
        }

        for (var r = headerIndex + 1; r < rows.Count; r++)
        {
            var row = rows[r];
            string Raw(int i) => i < row.Count ? row[i].Trim() : "";
            string? Cell(string field)
            {
                if (!columns.TryGetValue(field, out var i))
                {
                    return null;
                }

                var value = Raw(i);
                var norm = Norm(value);
                return norm.Length == 0 || (field != "active" && TableImport.Blank.Contains(norm)) ? null : value;
            }

            var notes = extra.Select(i => (Title: Title(i), Value: Raw(i))).Where(x => Norm(x.Value).Length > 0).ToList();
            var note = notes.Count == 0 ? null
                : string.Join("; ", notes.Where(x => x.Title.Length > 0).Select(x => $"{x.Title}: {x.Value}")
                    .Append(string.Join(" ", notes.Where(x => x.Title.Length == 0).Select(x => x.Value))).Where(x => x.Length > 0));
            var fields = Headers.Select(h => h.Field).Where(f => f is not ("name" or "number")).ToList();
            var hasData = note is not null || fields.Any(f => Cell(f) is not null);
            var name = Cell("name");
            ImportRecord entry;
            if (name is null)
            {
                if (!hasData)
                {
                    continue; // пустая строка или только номер
                }

                if (last is null || Cell("number") is not null)
                {
                    skipped++;
                    Error($"Строка {r + 1}: нет названия — пропущена.");
                    continue;
                }

                entry = last;
                joined++;
            }
            else
            {
                if (name.Length > 200)
                {
                    skipped++;
                    Error($"Строка {r + 1}: название длиннее 200 символов — пропущена.");
                    last = null;
                    continue;
                }

                var digits = new string((Cell("inn") ?? "").Where(char.IsDigit).ToArray());
                var key = digits.Length is >= 9 and <= 14 ? $"inn:{digits}" : $"name:{name.ToLowerInvariant()}";
                if (byKey.TryGetValue(key, out var same))
                {
                    entry = same;
                    duplicates++;
                }
                else
                {
                    entry = byKey[key] = new ImportRecord(r + 1, name);
                    records.Add(entry);
                }

                last = entry;
            }

            foreach (var field in fields)
            {
                if (Cell(field) is { } value)
                {
                    entry.Add(field, value);
                }
            }

            if (note is not null)
            {
                entry.Add("notes", note);
            }
        }

        // 2. Поставщики → реестр.
        var categories = await db.ProcurementCategories.AsNoTracking().ToListAsync(ct);
        var existing = await db.ProcurementSuppliers.Include(x => x.Categories).ToListAsync(ct);
        var missingCategories = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        int created = 0, updated = 0, inactive = 0;
        var sample = new List<ProcurementSupplierImportSample>();
        foreach (var entry in records)
        {
            try
            {
                var inn = NormalizeInn(entry.Get("inn"));
                var supplier = (inn is not null ? existing.FirstOrDefault(x => x.Inn == inn) : null)
                               ?? existing.FirstOrDefault(x => string.Equals(x.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
                var isNew = supplier is null;
                supplier ??= new ProcurementSupplier { Name = entry.Name, CreatedById = ctx.UserId };
                var lead = entry.Get("lead") is { } leadText && int.TryParse(new string(leadText.Where(char.IsDigit).ToArray()), out var days) ? days : supplier.LeadTimeDays;
                var active = entry.Get("active") is { } state ? No.Contains(Norm(state)) ? false : Yes.Contains(Norm(state)) ? true : supplier.IsActive : supplier.IsActive;
                var categoryIds = new List<Guid>();
                foreach (var text in (entry.Get("categories") ?? "").Split(',', ';').Select(c => c.Trim()).Where(c => c.Length > 0))
                {
                    if (categories.FirstOrDefault(x => TableImport.Loose(x.Name) == TableImport.Loose(text)) is { } category)
                    {
                        categoryIds.Add(category.Id);
                    }
                    else
                    {
                        missingCategories.Add(text);
                    }
                }

                await ApplyAsync(supplier, new ProcurementSupplierInput(entry.Name, inn ?? supplier.Inn, entry.Get("contact") ?? supplier.ContactPerson,
                    entry.Get("phone") ?? supplier.Phone, entry.Get("email") ?? supplier.Email, entry.Get("address") ?? supplier.Address,
                    entry.Get("bank") ?? supplier.BankName, entry.Get("mfo") ?? supplier.BankMfo, entry.Get("account") ?? supplier.BankAccount,
                    entry.Get("terms") ?? supplier.PaymentTerms, lead, entry.Get("notes") ?? supplier.Notes,
                    categoryIds.Count > 0 ? categoryIds : supplier.Categories.Select(c => c.CategoryId).ToList(), active,
                    entry.Get("legal") ?? supplier.LegalName, entry.Get("products") ?? supplier.Products), ct);
                if (isNew)
                {
                    db.ProcurementSuppliers.Add(supplier);
                    existing.Add(supplier);
                    created++;
                }
                else
                {
                    supplier.UpdatedAt = DateTimeOffset.UtcNow;
                    updated++;
                }

                inactive += supplier.IsActive ? 0 : 1;
                if (sample.Count < 8)
                {
                    sample.Add(new ProcurementSupplierImportSample(entry.Row, supplier.Name, supplier.LegalName, supplier.Inn, supplier.Phone, supplier.ContactPerson,
                        supplier.Products?.Split('\n').Length ?? 0, supplier.IsActive, isNew));
                }
            }
            catch (FieldValidationException e)
            {
                skipped++;
                Error($"Строка {entry.Row} («{entry.Name}»): {e.Message}");
            }
        }

        if (missingCategories.Count > 0)
        {
            errors.Insert(0, $"Категории не найдены и не проставлены: {string.Join(", ", missingCategories.Select(c => $"«{c}»"))} — их можно добавить в настройках заявок.");
        }

        if (!dryRun && created + updated > 0)
        {
            db.ProcurementAuditLogs.Add(new ProcurementAuditLog
            {
                DocType = ProcurementOrderRules.SupplierDoc, DocId = Guid.Empty, ActorId = ctx.UserId, Action = "import",
                Text = $"Импорт поставщиков из «{fileName}»: добавлено {created}, обновлено {updated}, пропущено {skipped}",
            });
            await db.SaveChangesAsync(ct);
        }
        else
        {
            db.ChangeTracker.Clear(); // проверка файла: ничего не сохраняется
        }

        return new ProcurementSupplierImportResult(created, updated, skipped, errors, dryRun, mapped, joined, duplicates, inactive, sample);
    }

    private void Log(ProcurementContext ctx, Guid id, string action, string text) =>
        db.ProcurementAuditLogs.Add(new ProcurementAuditLog { DocType = ProcurementOrderRules.SupplierDoc, DocId = id, ActorId = ctx.UserId, Action = action, Text = text });

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
