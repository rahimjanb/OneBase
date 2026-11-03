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
    int? LeadTimeDays);

/// <summary>Реестр поставщиков: поиск (название, ИНН, контакт, телефон), категория, активные/отключённые, сортировка.</summary>
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
    bool IsActive = true);

/// <summary>
/// Рейтинг поставщика: итог 0–100 и составляющие (null — нет данных): сроки (доля заказов без просрочки среди тех, чей срок наступил),
/// качество (оценка предложений, 1–5 → 0–100), соблюдение цен (доля строк заказа не дороже предложения). Претензии и полнота — после приёмки.
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
    int LateOrders = 0);

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
    IReadOnlyList<ProcurementClaimView>? Claims = null);

public sealed record ProcurementSupplierImportResult(int Created, int Updated, int Skipped, IReadOnlyList<string> Errors);

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
            q = q.Where(x => x.Name.ToLower().Contains(text) || (x.Inn != null && x.Inn.Contains(text)) || (x.ContactPerson != null && x.ContactPerson.ToLower().Contains(text))
                             || (x.Phone != null && x.Phone.Contains(text)));
        }

        var total = await q.CountAsync(ct);
        var pageSize = Math.Clamp(s.PageSize, 5, 200);
        var page = Math.Max(s.Page, 1);
        var placed = Placed;
        var stats = db.ProcurementPurchaseOrders.AsNoTracking().Where(o => placed.Contains(o.Status));
        var rows = await q
            .Select(x => new
            {
                x.Id, x.Name, x.Inn, x.ContactPerson, x.Phone, x.IsActive, x.LeadTimeDays,
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
                r.Total, ratings[r.Id].OverdueOrders, ratings[r.Id].Overall, r.Last, r.LeadTimeDays))
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
            await ProcurementReceiptService.ClaimsAsync(db, null, id, ct));
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
            decimal? onTime = d.Count == 0 ? null : Math.Round(100m * (d.Count - late) / d.Count, 1);
            decimal? qual = q.Count == 0 ? null : Math.Round((decimal)q.Average(x => x.Quality) * 20m, 1);
            decimal? prices = p.Count == 0 ? null : Math.Round(100m * p.Count(x => x.UnitPrice <= x.Offered) / p.Count, 1);
            decimal? claims = r is null || r.Lines == 0 ? null : Math.Round(100m * (r.Lines - r.Problems) / r.Lines, 1);
            decimal? complete = c is null || c.Ordered <= 0 ? null : Math.Round(Math.Min(100m, 100m * c.Received / c.Ordered), 1);
            var currentOverdue = d.Count(x => x.Overdue);
            return new ProcurementSupplierRating(ProcurementOrderRules.Rating(onTime, qual, prices, claims, complete), onTime, qual, prices, d.Count, currentOverdue,
                q.Count, p.Count, claims, complete, r?.Lines ?? 0, r?.Problems ?? 0, c?.Orders ?? 0, late);
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

        supplier.Name = name;
        supplier.Inn = inn;
        supplier.ContactPerson = Text(input.ContactPerson, 200, "Контактное лицо");
        supplier.Phone = Text(input.Phone, 50, "Телефон");
        supplier.Email = email;
        supplier.Address = Text(input.Address, 500, "Адрес");
        supplier.BankName = Text(input.BankName, 200, "Банк");
        supplier.BankMfo = Text(input.BankMfo, 20, "МФО");
        supplier.BankAccount = Text(input.BankAccount, 40, "Расчётный счёт");
        supplier.PaymentTerms = Text(input.PaymentTerms, 500, "Условия оплаты");
        supplier.LeadTimeDays = input.LeadTimeDays;
        supplier.Notes = Text(input.Notes, 2000, "Примечание");
        supplier.IsActive = input.IsActive;
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

    private static readonly Dictionary<string, string[]> Headers = new()
    {
        ["name"] = ["название", "наименование", "поставщик", "компания"],
        ["inn"] = ["инн", "stir", "inn"],
        ["contact"] = ["контактное лицо", "контакт", "контактное"],
        ["phone"] = ["телефон", "тел", "phone"],
        ["email"] = ["email", "e-mail", "почта", "эл. почта"],
        ["address"] = ["адрес"],
        ["bank"] = ["банк"],
        ["mfo"] = ["мфо"],
        ["account"] = ["расчётный счёт", "расчетный счет", "р/с", "счёт", "счет"],
        ["terms"] = ["условия оплаты", "оплата"],
        ["lead"] = ["срок поставки", "срок поставки, дней", "срок, дней"],
        ["categories"] = ["категории", "категория"],
        ["notes"] = ["примечание", "комментарий"],
    };

    /// <summary>
    /// Импорт из Excel/CSV: строка заголовков (Название, ИНН, Контактное лицо, Телефон, Email, Адрес, Банк, МФО, Расчётный счёт,
    /// Условия оплаты, Срок поставки, Категории через запятую, Примечание) — дальше по строке на поставщика. Совпадение по ИНН, без ИНН —
    /// по названию: найден — обновляются заполненные поля, нет — создаётся. Строка с ошибкой пропускается с пояснением.
    /// </summary>
    public async Task<ProcurementSupplierImportResult> ImportAsync(ProcurementContext ctx, Stream content, string fileName, CancellationToken ct)
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

        var rows = document.Sheets.FirstOrDefault()?.Rows ?? [];
        var headerIndex = -1;
        var columns = new Dictionary<string, int>();
        for (var r = 0; r < Math.Min(rows.Count, 10) && headerIndex < 0; r++)
        {
            var cells = rows[r].Select(c => c.Trim().ToLowerInvariant().Replace('ё', 'е')).ToList();
            var found = new Dictionary<string, int>();
            foreach (var (field, names) in Headers)
            {
                var index = cells.FindIndex(c => names.Any(n => c == n.Replace('ё', 'е')));
                if (index >= 0)
                {
                    found[field] = index;
                }
            }

            if (found.ContainsKey("name"))
            {
                headerIndex = r;
                columns = found;
            }
        }

        if (headerIndex < 0)
        {
            throw new FieldValidationException("Не найдена строка заголовков: нужен столбец «Название» (и по желанию ИНН, Телефон, Email, Адрес, Банк, МФО, Расчётный счёт…).");
        }

        var categories = await db.ProcurementCategories.AsNoTracking().ToListAsync(ct);
        var existing = await db.ProcurementSuppliers.Include(x => x.Categories).ToListAsync(ct);
        int created = 0, updated = 0, skipped = 0;
        var errors = new List<string>();
        for (var r = headerIndex + 1; r < rows.Count; r++)
        {
            var row = rows[r];
            string? Cell(string field) => columns.TryGetValue(field, out var i) && i < row.Count && !string.IsNullOrWhiteSpace(row[i]) ? row[i].Trim() : null;
            var name = Cell("name");
            if (name is null)
            {
                if (row.Any(c => !string.IsNullOrWhiteSpace(c)))
                {
                    skipped++;
                    errors.Add($"Строка {r + 1}: нет названия — пропущена.");
                }

                continue;
            }

            try
            {
                var inn = NormalizeInn(Cell("inn"));
                var supplier = (inn is not null ? existing.FirstOrDefault(x => x.Inn == inn) : null)
                               ?? existing.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                var isNew = supplier is null;
                supplier ??= new ProcurementSupplier { Name = name, CreatedById = ctx.UserId };
                var lead = Cell("lead") is { } leadText && int.TryParse(new string(leadText.Where(char.IsDigit).ToArray()), out var days) ? days : supplier.LeadTimeDays;
                var categoryIds = (Cell("categories") ?? "").Split(',', ';').Select(c => c.Trim()).Where(c => c.Length > 0)
                    .Select(c => categories.FirstOrDefault(x => string.Equals(x.Name, c, StringComparison.OrdinalIgnoreCase))?.Id).OfType<Guid>().ToList();
                await ApplyAsync(supplier, new ProcurementSupplierInput(name, inn ?? supplier.Inn, Cell("contact") ?? supplier.ContactPerson, Cell("phone") ?? supplier.Phone,
                    Cell("email") ?? supplier.Email, Cell("address") ?? supplier.Address, Cell("bank") ?? supplier.BankName, Cell("mfo") ?? supplier.BankMfo,
                    Cell("account") ?? supplier.BankAccount, Cell("terms") ?? supplier.PaymentTerms, lead, Cell("notes") ?? supplier.Notes,
                    categoryIds.Count > 0 ? categoryIds : supplier.Categories.Select(c => c.CategoryId).ToList(), supplier.IsActive), ct);
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
            }
            catch (FieldValidationException e)
            {
                skipped++;
                if (errors.Count < 50)
                {
                    errors.Add($"Строка {r + 1} («{name}»): {e.Message}");
                }
            }
        }

        if (created + updated > 0)
        {
            db.ProcurementAuditLogs.Add(new ProcurementAuditLog
            {
                DocType = ProcurementOrderRules.SupplierDoc, DocId = Guid.Empty, ActorId = ctx.UserId, Action = "import",
                Text = $"Импорт поставщиков из «{fileName}»: добавлено {created}, обновлено {updated}, пропущено {skipped}",
            });
            await db.SaveChangesAsync(ct);
        }

        return new ProcurementSupplierImportResult(created, updated, skipped, errors);
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
