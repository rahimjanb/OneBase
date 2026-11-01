using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OneBase.Application.Abstractions;
using OneBase.Application.Field;
using OneBase.Application.Reports;
using OneBase.Domain.Procurement;

namespace OneBase.Application.Procurement;

public sealed record ProcurementReportInfo(string Key, string Title, string Description);

/// <summary>
/// Отчёты снабжения (Excel, PDF, предпросмотр — одна модель листов AiReportContent): показатели, заявки, заказы, позиции закупок,
/// приёмки и претензии, расходы по разрезам, поставщики, сотрудники снабжения. Фильтры — период, отдел, категория, сотрудник
/// (ответственный за заказ), поставщик. Видят снабжение и директор. Строк на лист — до 10 000 (больше — пояснение под таблицей).
/// </summary>
public sealed class ProcurementReportService(IAppDbContext db, ProcurementAnalyticsService analytics, ProcurementSupplierService suppliers)
{
    public const int MaxRows = 10_000;

    private static bool Uz => UiLanguage.IsUzbek;
    private static string L(string ru, string uz) => Uz ? UiLanguage.Uzbek(uz) : ru;
    private static JsonElement J(object? value) => JsonSerializer.SerializeToElement(value);
    private static string? D(DateOnly? date) => date?.ToString("yyyy-MM-dd");
    private static string? D(DateTimeOffset? at) => at is { } v ? FieldClock.LocalDate(v).ToString("yyyy-MM-dd") : null;
    private static AiReportColumn Text(string header) => new(header, ReportFormats.Text);
    private static AiReportColumn Money(string header, bool sum = true) => new(header, ReportFormats.Money, sum);
    private static AiReportColumn Int(string header, bool sum = true) => new(header, ReportFormats.Integer, sum);
    private static AiReportColumn Num(string header) => new(header, ReportFormats.Number, false);
    private static AiReportColumn Pct(string header) => new(header, ReportFormats.Percent, false);
    private static AiReportColumn Date(string header) => new(header, ReportFormats.Date, false);

    public static IReadOnlyList<ProcurementReportInfo> Kinds =>
    [
        new("kpi", L("Показатели закупок", "Xarid ko'rsatkichlari"), L("Среднее время закупки, поставки в срок, экономия, выполненные заявки, претензии, качество поставщиков", "Xaridning o'rtacha vaqti, o'z vaqtida yetkazish, tejash, bajarilgan arizalar, e'tirozlar, yetkazib beruvchilar sifati")),
        new("spend", L("Расходы", "Xarajatlar"), L("Закупки по отделам, категориям, поставщикам и месяцам", "Bo'limlar, toifalar, yetkazib beruvchilar va oylar bo'yicha xaridlar")),
        new("lines", L("Позиции закупок", "Xarid pozitsiyalari"), L("Каждая позиция заказа: заявка, отдел, категория, цена, получено, экономия", "Buyurtmaning har bir pozitsiyasi: ariza, bo'lim, toifa, narx, olingan, tejash")),
        new("orders", L("Заказы поставщикам", "Yetkazib beruvchilarga buyurtmalar"), L("Статусы, сроки, опоздания и суммы заказов", "Buyurtmalarning holati, muddatlari, kechikishlari va summalari")),
        new("requests", L("Заявки", "Arizalar"), L("Заявки за период: отдел, инициатор, статус, суммы", "Davr bo'yicha arizalar: bo'lim, tashabbuskor, holat, summalar")),
        new("receipts", L("Приёмки и претензии", "Qabullar va e'tirozlar"), L("Что пришло, брак, недостача, принято на склад, претензии", "Nima keldi, yaroqsiz, kamomad, omborga qabul qilindi, e'tirozlar")),
        new("suppliers", L("Поставщики", "Yetkazib beruvchilar"), L("Закупки за период и рейтинг по составляющим", "Davrdagi xaridlar va tarkibiy qismlar bo'yicha reyting")),
        new("staff", L("Сотрудники снабжения", "Ta'minot xodimlari"), L("Нагрузка, просрочки, поставки и сроки исполнения", "Yuklama, kechikishlar, yetkazishlar va bajarish muddatlari")),
    ];

    public async Task<AiReportContent> BuildAsync(ProcurementContext ctx, string kind, ProcurementFilter f, CancellationToken ct)
    {
        if (!ctx.ReadsAll && !ctx.IsDirector)
        {
            throw new FieldForbiddenException("Отчёты снабжения видят снабжение и директор.");
        }

        var info = Kinds.FirstOrDefault(k => k.Key == kind) ?? throw new FieldNotFoundException("Такого отчёта нет.");
        var sheets = kind switch
        {
            "kpi" => [await KpiAsync(f, ct)],
            "spend" => await SpendAsync(f, ct),
            "lines" => [await LinesAsync(f, ct)],
            "orders" => [await OrdersAsync(f, ct)],
            "requests" => [await RequestsAsync(f, ct)],
            "receipts" => [await ReceiptsAsync(f, ct)],
            "suppliers" => [await SuppliersAsync(f, ct)],
            _ => [await StaffAsync(f, ct)],
        };
        return new AiReportContent($"{info.Title} · {f.From:dd.MM.yyyy}–{f.To:dd.MM.yyyy}", sheets, [await FilterTextAsync(f, ct)]);
    }

    /// <summary>Подпись фильтров для шапки: «Снабжение → отчёты; отдел: Финансы; поставщик: …».</summary>
    private async Task<string> FilterTextAsync(ProcurementFilter f, CancellationToken ct)
    {
        var parts = new List<string> { L("OneBase · Снабжение", "OneBase · Ta'minot") };
        if (f.DepartmentId is { } d && await db.Departments.AsNoTracking().Where(x => x.Id == d).Select(x => x.Name).FirstOrDefaultAsync(ct) is { } dept)
        {
            parts.Add($"{L("отдел", "bo'lim")}: {dept}");
        }

        if (f.CategoryId is { } c && await db.ProcurementCategories.AsNoTracking().Where(x => x.Id == c).Select(x => x.Name).FirstOrDefaultAsync(ct) is { } category)
        {
            parts.Add($"{L("категория", "toifa")}: {category}");
        }

        if (f.EmployeeId is { } e && await db.Users.AsNoTracking().Where(x => x.Id == e).Select(x => x.FullName).FirstOrDefaultAsync(ct) is { } employee)
        {
            parts.Add($"{L("сотрудник", "xodim")}: {employee}");
        }

        if (f.SupplierId is { } s && await db.ProcurementSuppliers.AsNoTracking().Where(x => x.Id == s).Select(x => x.Name).FirstOrDefaultAsync(ct) is { } supplier)
        {
            parts.Add($"{L("поставщик", "yetkazib beruvchi")}: {supplier}");
        }

        return string.Join("; ", parts);
    }

    private static AiReportSheet Sheet(string name, IReadOnlyList<AiReportColumn> columns, List<IReadOnlyList<JsonElement>> rows, bool total, string? note = null)
    {
        if (rows.Count > MaxRows)
        {
            note = $"{L($"Показаны первые {MaxRows:#,0} строк из {rows.Count:#,0} — сузьте период или фильтры.", $"{rows.Count:#,0} qatordan birinchi {MaxRows:#,0} tasi ko'rsatilgan — davr yoki filtrlarni toraytiring.")} {note}".Trim();
            rows = rows.Take(MaxRows).ToList();
        }

        return new AiReportSheet(name, columns, rows, total, note);
    }

    // ---------- Показатели ----------

    private async Task<AiReportSheet> KpiAsync(ProcurementFilter f, CancellationToken ct)
    {
        var k = await analytics.KpisAsync(f, ct);
        var noData = L("нет данных", "ma'lumot yo'q");
        List<IReadOnlyList<JsonElement>> rows =
        [
            [J(L("Среднее время закупки, дней", "Xaridning o'rtacha vaqti, kun")), J(k.AvgPurchaseDays?.ToString("0.#") ?? noData),
                J(L($"от утверждения заявки до поставки; заказов: {k.PurchasesMeasured}", $"ariza tasdiqlanganidan yetkazilgunicha; buyurtmalar: {k.PurchasesMeasured}"))],
            [J(L("Поставки в срок", "O'z vaqtida yetkazish")), J(k.OnTimePercent is { } on ? $"{on:0.#} %" : noData),
                J(L($"поставленных заказов со сроком: {k.DeliveriesMeasured}", $"muddatli yetkazilgan buyurtmalar: {k.DeliveriesMeasured}"))],
            [J(L("Экономия, сум", "Tejash, so'm")), J(ProcurementRules.Money(k.Savings) + (k.SavingsPercent is { } p ? $" ({p:0.#} %)" : "")),
                J(L($"цена в заявке − цена заказа с НДС; позиций с ценой в заявке: {k.SavingsLines}", $"arizadagi narx − buyurtma narxi QQS bilan; arizada narxi bor pozitsiyalar: {k.SavingsLines}"))],
            [J(L("Выполненные заявки", "Bajarilgan arizalar")), J(k.FulfilledRequests.ToString()), J(L("перешли в «Выполнена» за период", "davrda «Bajarilgan» holatiga o'tgan"))],
            [J(L("Претензии", "E'tirozlar")), J(k.Claims.ToString()), J(L("строки приёмок с браком или недостачей", "yaroqsiz yoki kamomadli qabul qatorlari"))],
            [J(L("Качество поставщиков (рейтинг 0–100)", "Yetkazib beruvchilar sifati (reyting 0–100)")), J(k.SupplierQuality?.ToString("0.#") ?? noData),
                J(L($"средний рейтинг поставщиков с закупками за период: {k.SuppliersRated}", $"davrda xaridlari bo'lgan yetkazib beruvchilarning o'rtacha reytingi: {k.SuppliersRated}"))],
        ];
        return Sheet(L("Показатели", "Ko'rsatkichlar"), [Text(L("Показатель", "Ko'rsatkich")), Text(L("Значение", "Qiymat")), Text(L("Как считается", "Qanday hisoblanadi"))], rows, false,
            L("Все показатели — из данных OneBase за выбранный период. Оплаты войдут с модулем «Финансы».", "Barcha ko'rsatkichlar — tanlangan davr uchun OneBase ma'lumotlaridan. To'lovlar «Moliya» moduli bilan qo'shiladi."));
    }

    // ---------- Расходы ----------

    private async Task<List<AiReportSheet>> SpendAsync(ProcurementFilter f, CancellationToken ct)
    {
        var lines = await analytics.Lines(f)
            .Select(i => new
            {
                Department = i.RequestItem!.Request!.Department!.Name,
                Category = i.RequestItem.Category!.Name,
                Supplier = i.Order!.Supplier!.Name,
                i.OrderId,
                i.Order.ApprovedAt,
                Amount = i.Order.PricesIncludeVat ? i.Quantity * i.UnitPrice : i.Quantity * i.UnitPrice * (1 + i.Order.VatRate / 100),
            })
            .ToListAsync(ct);
        AiReportSheet By(string name, string header, Func<(string Department, string Category, string Supplier, string Month), string> key)
        {
            var rows = lines.GroupBy(x => key((x.Department, x.Category, x.Supplier, FieldClock.LocalDate(x.ApprovedAt!.Value).ToString("yyyy-MM"))))
                .Select(g => (Key: g.Key, Orders: g.Select(x => x.OrderId).Distinct().Count(), Lines: g.Count(), Amount: Math.Round(g.Sum(x => x.Amount), 2)))
                .OrderByDescending(x => header == L("Месяц", "Oy") ? 0 : x.Amount).ThenBy(x => x.Key)
                .Select(x => (IReadOnlyList<JsonElement>)[J(x.Key), J(x.Orders), J(x.Lines), J(x.Amount)])
                .ToList();
            return Sheet(name, [Text(header), Int(L("Заказов", "Buyurtmalar"), false), Int(L("Позиций", "Pozitsiyalar")), Money(L("Сумма с НДС, сум", "QQS bilan summa, so'm"))], rows, true,
                L("По позициям утверждённых заказов (дата — утверждение заказа), с НДС, без скидки и доставки.", "Tasdiqlangan buyurtmalar pozitsiyalari bo'yicha (sana — buyurtma tasdiqlangan kun), QQS bilan, chegirma va yetkazib berishsiz."));
        }

        var months = By(L("По месяцам", "Oylar bo'yicha"), L("Месяц", "Oy"), x => x.Month);
        months = months with { Rows = months.Rows.OrderBy(r => r[0].GetString()).ToList() };
        return
        [
            By(L("По отделам", "Bo'limlar bo'yicha"), L("Отдел", "Bo'lim"), x => x.Department),
            By(L("По категориям", "Toifalar bo'yicha"), L("Категория", "Toifa"), x => x.Category),
            By(L("По поставщикам", "Yetkazib beruvchilar bo'yicha"), L("Поставщик", "Yetkazib beruvchi"), x => x.Supplier),
            months,
        ];
    }

    // ---------- Позиции закупок ----------

    private async Task<AiReportSheet> LinesAsync(ProcurementFilter f, CancellationToken ct)
    {
        var lines = await analytics.Lines(f).OrderBy(i => i.Order!.ApprovedAt).ThenBy(i => i.Order!.Number).ThenBy(i => i.LineNo).Take(MaxRows + 1)
            .Select(i => new
            {
                Order = i.Order!.Number, i.Order.ApprovedAt, Supplier = i.Order.Supplier!.Name, Request = i.RequestItem!.Request!.Number,
                Department = i.RequestItem.Request.Department!.Name, Category = i.RequestItem.Category!.Name, i.Name, i.Quantity, i.UnitCode,
                Gross = i.Order.PricesIncludeVat ? i.UnitPrice : i.UnitPrice * (1 + i.Order.VatRate / 100), i.ReceivedQty, i.RequestItem.EstimatedPrice,
            })
            .ToListAsync(ct);
        var rows = lines.Select(x => (IReadOnlyList<JsonElement>)
        [
            J(x.Order), J(D(x.ApprovedAt)), J(x.Supplier), J(x.Request), J(x.Department), J(x.Category), J(x.Name), J(x.Quantity), J(x.UnitCode),
            J(Math.Round(x.Gross, 2)), J(Math.Round(x.Gross * x.Quantity, 2)), J(x.ReceivedQty), J(x.EstimatedPrice),
            J(x.EstimatedPrice is { } e ? Math.Round((e - x.Gross) * x.Quantity, 2) : null),
        ]).ToList();
        return Sheet(L("Позиции закупок", "Xarid pozitsiyalari"),
        [
            Text(L("Заказ", "Buyurtma")), Date(L("Утверждён", "Tasdiqlangan")), Text(L("Поставщик", "Yetkazib beruvchi")), Text(L("Заявка", "Ariza")),
            Text(L("Отдел", "Bo'lim")), Text(L("Категория", "Toifa")), Text(L("Наименование", "Nomi")), Num(L("Кол-во", "Miqdor")), Text(L("Ед.", "Birl.")),
            Money(L("Цена с НДС", "QQS bilan narx"), false), Money(L("Сумма с НДС", "QQS bilan summa")), Num(L("Получено", "Olingan")),
            Money(L("Цена в заявке", "Arizadagi narx"), false), Money(L("Экономия", "Tejash")),
        ], rows, true, L("Экономия — по позициям, где в заявке была цена; минус — дороже заявки.", "Tejash — arizada narxi bo'lgan pozitsiyalar bo'yicha; minus — arizadan qimmat."));
    }

    // ---------- Заказы ----------

    private async Task<AiReportSheet> OrdersAsync(ProcurementFilter f, CancellationToken ct)
    {
        var (from, to) = (f.FromUtc, f.ToUtc);
        var today = FieldClock.Today;
        var orders = await analytics.Orders(f).Where(o => o.Status != ProcurementOrderStatus.Draft && o.CreatedAt >= from && o.CreatedAt < to)
            .OrderBy(o => o.CreatedAt).Take(MaxRows + 1)
            .Select(o => new
            {
                o.Number, o.CreatedAt, o.ApprovedAt, Supplier = o.Supplier!.Name, Responsible = o.Responsible!.FullName, o.Status, o.ExpectedDate, o.DeliveredAt,
                Lines = o.Items.Count, o.Total,
            })
            .ToListAsync(ct);
        var rows = orders.Select(o =>
        {
            var late = ProcurementOrderRules.IsOverdue(o.Status, o.ExpectedDate, today) ? L("просрочен", "kechikkan")
                : o.DeliveredAt is { } at && o.ExpectedDate is { } e && FieldClock.LocalDate(at) > e ? L("с опозданием", "kechikib") : "";
            return (IReadOnlyList<JsonElement>)
            [
                J(o.Number), J(D(o.CreatedAt)), J(D(o.ApprovedAt)), J(o.Supplier), J(o.Responsible),
                J(L(ProcurementOrderRules.Label(o.Status), ProcurementRequestService.OrderStatusUz(o.Status))), J(D(o.ExpectedDate)), J(D(o.DeliveredAt)), J(late),
                J(o.Lines), J(o.Total),
            ];
        }).ToList();
        return Sheet(L("Заказы", "Buyurtmalar"),
        [
            Text(L("Заказ", "Buyurtma")), Date(L("Создан", "Yaratilgan")), Date(L("Утверждён", "Tasdiqlangan")), Text(L("Поставщик", "Yetkazib beruvchi")),
            Text(L("Ответственный", "Mas'ul")), Text(L("Статус", "Holat")), Date(L("Ожидается", "Kutilmoqda")), Date(L("Поставлен", "Yetkazilgan")),
            Text(L("Срок", "Muddat")), Int(L("Позиций", "Pozitsiyalar"), false), Money(L("Итог, сум", "Jami, so'm")),
        ], rows, true, L("Заказы, созданные за период (кроме черновиков).", "Davrda yaratilgan buyurtmalar (qoralamalardan tashqari)."));
    }

    // ---------- Заявки ----------

    private async Task<AiReportSheet> RequestsAsync(ProcurementFilter f, CancellationToken ct)
    {
        var (from, to) = (f.FromUtc, f.ToUtc);
        var q = db.ProcurementRequests.AsNoTracking().Where(r => r.SubmittedAt >= from && r.SubmittedAt < to);
        if (f.DepartmentId is { } department)
        {
            q = q.Where(r => r.DepartmentId == department);
        }

        if (f.CategoryId is { } category)
        {
            q = q.Where(r => r.Items.Any(i => i.CategoryId == category));
        }

        if (f.EmployeeId is { } employee)
        {
            q = q.Where(r => db.ProcurementPurchaseOrderItems.Any(i => i.RequestItem!.RequestId == r.Id && i.Order!.ResponsibleId == employee));
        }

        if (f.SupplierId is { } supplier)
        {
            q = q.Where(r => db.ProcurementPurchaseOrderItems.Any(i => i.RequestItem!.RequestId == r.Id && i.Order!.SupplierId == supplier));
        }

        var list = await q.OrderBy(r => r.SubmittedAt).Take(MaxRows + 1)
            .Select(r => new
            {
                r.Number, r.Title, r.SubmittedAt, r.ApprovedAt, Department = r.Department!.Name, Initiator = r.Initiator!.FullName, r.Status, r.Priority,
                Lines = r.Items.Count(i => i.Status != ProcurementItemStatus.Cancelled), r.EstimatedTotal, r.PurchaseTotal,
            })
            .ToListAsync(ct);
        var rows = list.Select(r => (IReadOnlyList<JsonElement>)
        [
            J(r.Number), J(r.Title), J(D(r.SubmittedAt)), J(D(r.ApprovedAt)), J(r.Department), J(r.Initiator),
            J(L(ProcurementRules.Label(r.Status), ProcurementRequestService.StatusUz(r.Status))),
            J(L(ProcurementRules.Label(r.Priority), ProcurementRequestService.PriorityUz(r.Priority))),
            J(r.Lines), J(r.EstimatedTotal), J(r.PurchaseTotal),
        ]).ToList();
        return Sheet(L("Заявки", "Arizalar"),
        [
            Text(L("Заявка", "Ariza")), Text(L("Название", "Nomi")), Date(L("Отправлена", "Yuborilgan")), Date(L("Утверждена", "Tasdiqlangan")),
            Text(L("Отдел", "Bo'lim")), Text(L("Инициатор", "Tashabbuskor")), Text(L("Статус", "Holat")), Text(L("Приоритет", "Ustuvorlik")),
            Int(L("Позиций", "Pozitsiyalar"), false), Money(L("Сумма ≈, сум", "Summa ≈, so'm")), Money(L("К закупке, сум", "Xaridga, so'm")),
        ], rows, true, L("Заявки, отправленные за период; суммы — по предполагаемым ценам.", "Davrda yuborilgan arizalar; summalar — taxminiy narxlar bo'yicha."));
    }

    // ---------- Приёмки ----------

    private async Task<AiReportSheet> ReceiptsAsync(ProcurementFilter f, CancellationToken ct)
    {
        var (from, to) = (f.FromUtc, f.ToUtc);
        var q = db.ProcurementReceiptLines.AsNoTracking()
            .Where(l => l.Document!.Status == ProcurementStockDocumentStatus.Posted && l.Document.PostedAt >= from && l.Document.PostedAt < to);
        if (f.DepartmentId is { } department)
        {
            q = q.Where(l => l.OrderItem!.RequestItem!.Request!.DepartmentId == department);
        }

        if (f.CategoryId is { } category)
        {
            q = q.Where(l => l.OrderItem!.RequestItem!.CategoryId == category);
        }

        if (f.EmployeeId is { } employee)
        {
            q = q.Where(l => l.Document!.Order!.ResponsibleId == employee);
        }

        if (f.SupplierId is { } supplier)
        {
            q = q.Where(l => l.Document!.Order!.SupplierId == supplier);
        }

        var list = await q.OrderBy(l => l.Document!.PostedAt).ThenBy(l => l.LineNo).Take(MaxRows + 1)
            .Select(l => new
            {
                Receipt = l.Document!.Number, l.Document.PostedAt, Order = l.Document.Order!.Number, Supplier = l.Document.Order.Supplier!.Name, l.OrderItem!.Name,
                l.OrderItem.UnitCode, l.Received, l.Defective, l.Shortage, l.Accepted,
                Gross = l.OrderItem.Order!.PricesIncludeVat ? l.OrderItem.UnitPrice : l.OrderItem.UnitPrice * (1 + l.OrderItem.Order.VatRate / 100),
                l.ClaimStatus, l.ClaimResolution,
            })
            .ToListAsync(ct);
        var rows = list.Select(l => (IReadOnlyList<JsonElement>)
        [
            J(l.Receipt), J(D(l.PostedAt)), J(l.Order), J(l.Supplier), J(l.Name), J(l.Received), J(l.Defective), J(l.Shortage), J(l.Accepted), J(l.UnitCode),
            J(Math.Round(l.Accepted * l.Gross, 2)),
            J(l.ClaimStatus switch { ProcurementClaimStatus.Open => L("открыта", "ochiq"), ProcurementClaimStatus.Resolved => L("закрыта", "yopilgan"), _ => "" }),
            J(l.ClaimResolution),
        ]).ToList();
        return Sheet(L("Приёмки", "Qabullar"),
        [
            Text(L("Приёмка", "Qabul")), Date(L("Дата", "Sana")), Text(L("Заказ", "Buyurtma")), Text(L("Поставщик", "Yetkazib beruvchi")),
            Text(L("Наименование", "Nomi")), Num(L("Пришло", "Keldi")), Num(L("Брак", "Yaroqsiz")), Num(L("Недостача", "Kamomad")), Num(L("Принято", "Qabul qilindi")),
            Text(L("Ед.", "Birl.")), Money(L("Принято, сум", "Qabul qilindi, so'm")), Text(L("Претензия", "E'tiroz")), Text(L("Решение", "Qaror")),
        ], rows, true, L("Подтверждённые приёмки за период; сумма — по цене заказа с НДС.", "Davrdagi tasdiqlangan qabullar; summa — buyurtma narxida QQS bilan."));
    }

    // ---------- Поставщики ----------

    private async Task<AiReportSheet> SuppliersAsync(ProcurementFilter f, CancellationToken ct)
    {
        var lines = await analytics.Lines(f)
            .GroupBy(i => new { i.Order!.SupplierId, i.Order.Supplier!.Name, i.Order.Supplier.Inn })
            .Select(g => new
            {
                g.Key.SupplierId, g.Key.Name, g.Key.Inn, Orders = g.Select(i => i.OrderId).Distinct().Count(),
                Amount = g.Sum(i => i.Order!.PricesIncludeVat ? i.Quantity * i.UnitPrice : i.Quantity * i.UnitPrice * (1 + i.Order.VatRate / 100)),
            })
            .ToListAsync(ct);
        var ratings = await suppliers.RatingsAsync(lines.Select(s => s.SupplierId).ToList(), ct);
        var rows = lines.OrderByDescending(s => s.Amount).Select(s =>
        {
            var r = ratings[s.SupplierId];
            return (IReadOnlyList<JsonElement>)
            [
                J(s.Name), J(s.Inn), J(s.Orders), J(Math.Round(s.Amount, 2)), J(r.OnTime), J(r.Quality), J(r.Prices), J(r.Claims), J(r.Completeness), J(r.Overall),
            ];
        }).ToList();
        return Sheet(L("Поставщики", "Yetkazib beruvchilar"),
        [
            Text(L("Поставщик", "Yetkazib beruvchi")), Text(L("ИНН", "STIR")), Int(L("Заказов", "Buyurtmalar")), Money(L("Сумма с НДС, сум", "QQS bilan summa, so'm")),
            Pct(L("Сроки", "Muddatlar")), Num(L("Качество", "Sifat")), Pct(L("Соблюдение цен", "Narxlarga rioya")), Pct(L("Без претензий", "E'tirozsiz")),
            Pct(L("Полнота", "To'liqlik")), Int(L("Рейтинг", "Reyting"), false),
        ], rows, true, L("Закупки за период; рейтинг и составляющие (0–100) — по всей истории поставщика.", "Davrdagi xaridlar; reyting va tarkibiy qismlar (0–100) — yetkazib beruvchining butun tarixi bo'yicha."));
    }

    // ---------- Сотрудники ----------

    private async Task<AiReportSheet> StaffAsync(ProcurementFilter f, CancellationToken ct)
    {
        var head = await analytics.HeadAsync(f, ct);
        var rows = head.Staff.Select(s => (IReadOnlyList<JsonElement>)
        [
            J(s.Name), J(s.OpenTasks), J(s.OverdueTasks), J(s.ActiveOrders), J(s.OverdueOrders), J(s.Delivered), J(s.AvgToOrderDays), J(s.AvgDeliveryDays),
        ]).ToList();
        return Sheet(L("Сотрудники снабжения", "Ta'minot xodimlari"),
        [
            Text(L("Сотрудник", "Xodim")), Int(L("Открытых задач", "Ochiq vazifalar")), Int(L("Просрочено задач", "Kechikkan vazifalar")),
            Int(L("Заказов в работе", "Ishdagi buyurtmalar")), Int(L("Просрочено поставок", "Kechikkan yetkazishlar")), Int(L("Поставлено за период", "Davrda yetkazilgan")),
            Num(L("Дней до заказа", "Buyurtmagacha kunlar")), Num(L("Дней поставки", "Yetkazish kunlari")),
        ], rows, true, L("Дней до заказа — от утверждения заявки до отправки заказа поставщику; дней поставки — от заказа до поставки целиком.", "Buyurtmagacha kunlar — ariza tasdiqlanganidan buyurtma yuborilgunicha; yetkazish kunlari — buyurtmadan to'liq yetkazilgunicha."));
    }
}
