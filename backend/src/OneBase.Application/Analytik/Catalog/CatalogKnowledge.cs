namespace OneBase.Application.Analytik.Catalog;

/// <summary>Знание о таблице: название, смысл и бизнес-правила, главная дата (если эвристика выбрала бы не ту).</summary>
public sealed record TableKnowledge(string Target, string Title, string Description, string? DateColumn = null);

/// <summary>Знание о колонке: название, бизнес-роль (если эвристика ошиблась бы) и смысл.</summary>
public sealed record ColumnKnowledge(string Target, string Title, string? Role = null, string? Description = null);

/// <summary>Связь, которую не вывести по имени колонки: «schema.table.column» → «schema.table.column», с условием на цель.</summary>
public sealed record RelationKnowledge(string From, string To, string? TargetFilter = null);

/// <summary>
/// Показатель компании: как он считается и какой инструмент OneBase — его владелец (authoritative). Официальные цифры берутся инструментом-
/// владельцем, а не собственным запросом к таблицам: у них свои правила (статусы, даты, исключения филиалов, возвраты).
/// </summary>
public sealed record MetricKnowledge(string Code, string Title, string Module, string Definition, string AuthoritativeTool, string Tables);

/// <summary>
/// Знания OneBase о структуре данных — то, чего нет в pg_catalog: русские названия, бизнес-правила, неявные связи Linko, показатели и их
/// владельцы. Новая таблица без знаний описывается автоматически (по именам колонок и связям); администратор дополняет описание в «Карте
/// данных» (analytik.Annotations) — ручное описание важнее этого файла.
/// </summary>
public static class CatalogKnowledge
{
    private const string SoldRule =
        "Официальная выручка и вес вторички — только заказы в статусах delivered и given по дате приёмки (AcceptedDate), без филиалов «Завод» " +
        "и «К К Мерч», минус возвраты (OrderReturns в статусе delivered по CreatedDate). Для официальных цифр — инструменты продаж (get_sales, " +
        "get_sales_period); сумма по таблице без этих правил с ними не совпадёт.";

    public static readonly IReadOnlyList<TableKnowledge> Tables =
    [
        // ---- продажи (зеркало Linko) ----
        new("linko.Orders", "Заказы (вторичка)",
            "Заказы торговых точек из Linko: точка (MarketId → linko.Markets), агент-ТП (AgentId → linko.Users), филиал Linko (BranchId, " +
            "BranchName), статус, суммы и вес заказа, даты ввода (CreatedDate), доставки (DeliveryDate) и приёмки (AcceptedDate). " + SoldRule,
            "AcceptedDate"),
        new("linko.OrderLines", "Строки заказов",
            "Товары заказа: OrderId → linko.Orders, ProductId → linko.Products, количество в единице товара (Amount — штуки или кг, не деньги), " +
            "цена, сумма строки (TotalPrice), вес (TotalWeight, кг), скидка. Период и статус — по заказу. " + SoldRule),
        new("linko.OrderReturns", "Возвраты", "Возвраты товара от точек: точка, агент, филиал, сумма и вес, дата создания (CreatedDate), статус.",
            "CreatedDate"),
        new("linko.OrderReturnLines", "Строки возвратов", "Товары возврата: ReturnId → linko.OrderReturns, исходный заказ, товар, количество, цена, вес."),
        new("linko.Visits", "Визиты ТП",
            "Визиты торговых представителей в точки: день (Day), точка, ТП (UserId → linko.Users), статус (done — выполнен), по маршруту (IsInPlan). " +
            "Страйк = визиты ТП с заказом ÷ выполненные визиты (инструмент get_visits).", "Day"),
        new("linko.Markets", "Торговые точки",
            "Магазины и другие точки продаж: название, тип (MarketTypeName), ответственный ТП (ResponsibleAgentId → linko.Users), филиал Linko, " +
            "адрес и координаты точки. АКБ — точки с чистым весом продаж больше нуля за период."),
        new("linko.Users", "Сотрудники Linko (ТП, операторы, доставщики)",
            "Пользователи Linko: торговые представители и другие сотрудники продаж. Должность — JobName/PositionName. Это не пользователи OneBase " +
            "(public.Users)."),
        new("linko.Products", "Товары", "Справочник товаров Linko: название, артикул (Code), тип (TypeId → linko.ProductTypes), единица (Measurement: шт или кг)."),
        new("linko.ProductTypes", "Типы и категории товаров", "Дерево типов товаров Linko (ParentId). Категории отчётов продаж — группы типов (помадка, кекс, трубочки…)."),
        new("linko.Stocks", "Склады Linko", "Склады завода и дилеров в Linko (готовая продукция)."),
        new("linko.ProductBalances", "Остатки готовой продукции на складах Linko",
            "Остаток товара на складе Linko в единице товара (штуки или кг), снимок последней синхронизации. Дни покрытия и рекомендуемый заказ — " +
            "инструмент get_stock_reload."),
        new("linko.Reservations", "Бронь на складах Linko", "Забронированное количество и вес по складу и товару — снимок (не доставленные заказы и перемещения в пути)."),
        new("linko.StockTransfers", "Перемещения (первичка)",
            "Перемещения со склада на склад: завод → дилеры (первичка), статус, даты, вес и сумма. Официальная первичка — инструмент get_primary_shipments.",
            "DeliveryDate"),
        new("linko.StockTransferLines", "Строки перемещений", "Товары перемещения: количество, вес брутто и нетто, цена и сумма."),
        new("linko.Payments", "Оплаты точек", "Оплаты торговых точек в Linko: сумма, валюта, тип (наличные, банк), статус, точка, агент, дата.", "CreatedDate"),
        new("linko.KpiPlans", "Планы ТП из Linko", "Планы показателей ТП по месяцам (Year, Month): вес, АКБ, сумма; показатель — IndicatorName."),
        new("linko.PriceLists", "Прайс-листы", "Прайс-листы Linko и их валюта."),
        new("linko.PriceListItems", "Цены прайс-листов", "Цена товара в прайс-листе — цена прайса, а не фактическая цена продажи."),
        new("linko.Promotions", "Акции", "Акции Linko: тип, скидка, даты начала и окончания.", "BeginDate"),
        new("linko.SyncState", "Свежесть синхронизации Linko", "Последняя удачная синхронизация каждой сущности Linko и её ошибка — источник свежести данных продаж."),
        new("sales.Regions", "Регионы продаж", "Регионы OneBase: филиал Linko (LinkoBranchId), направление (DirectionId → sales.Directions), супервайзер, дилер."),
        new("sales.Directions", "Направления (РМ, каналы)", "Направления региональных менеджеров и каналы продаж; регионы направления — sales.Regions."),
        new("sales.RegionPlans", "Планы регионов", "План региона по категории на месяц в кг: вид Rop (план РОП), Factory (план «Завод»), Primary (первичка). CategoryId — тип товара Linko."),
        new("sales.FactoryPlans", "План «Завод» по стране", "План завода по категориям на месяц в кг, итогом по стране."),
        new("sales.StaffPlans", "Планы ТП", "Планы показателей ТП по месяцам из API планов Linko (вес, АКБ, сумма)."),
        new("sales.PnlValues", "P&L дилеров", "Значения P&L дилеров по месяцам: выручка, себестоимость, валовая прибыль, расходы, EBIT, EAT. Официально — get_dealer_pnl."),
        new("sales.DealerLedger", "Реестр взаиморасчётов с дилерами", "Отгрузки, оплаты и расходы дилеров из Google Sheets — основа акта сверки и DSO (get_akt, get_dso)."),

        // ---- снабжение и склад сырья ----
        new("procurement.Items", "Номенклатура снабжения (сырьё и материалы)",
            "Позиции склада снабжения: артикул (Sku), название, категория, базовая единица (UnitCode), минимальный остаток (MinQty, 0 — не задан), " +
            "учёт партий и срок годности. Цены, срока поставки и поставщика по умолчанию у позиции нет."),
        new("procurement.StockBalances", "Остатки склада снабжения",
            "Остаток позиции на складе в базовой единице: OnHand — физически, Reserved — зарезервировано заявками. Доступно = OnHand − Reserved " +
            "(для производства — ещё без просроченных и непроверенных партий)."),
        new("procurement.StockMovements", "Движения склада снабжения",
            "Журнал движений (не меняется задним числом): Quantity со знаком (+ приход, − расход). Kind: Receipt (приход), Issue (выдача отделу), " +
            "WriteOff (списание), Adjustment (инвентаризация), TransferOut/TransferIn, ProductionIssue (выдача в производство), ProductionReturn " +
            "(возврат из производства), ProductionOutput (выпуск на склад), SupplierReturn, Reversal (сторно). Расход = Issue + ProductionIssue − ProductionReturn.",
            "CreatedAt"),
        new("procurement.StockDocuments", "Складские документы", "Приходы, выдачи, списания, инвентаризации, перемещения: номер, вид, статус (Draft/Posted), связи с заказом, тендером, заданием."),
        new("procurement.Batches", "Партии сырья", "Партии: поставщик, даты изготовления, поступления и срока годности, цена единицы, статус качества (Approved/Pending/Rejected/Blocked)."),
        new("procurement.Requests", "Заявки на закупку", "Заявки отделов: вид (обычная, сырьё), статус, желаемая дата, автор и отдел."),
        new("procurement.RequestItems", "Позиции заявок", "Что просят: позиция, количество, со склада (FromStock), к закупке, заказано, получено, выдано, ориентировочная цена."),
        new("procurement.PurchaseOrders", "Заказы поставщикам",
            "Заказы поставщикам и «Куплено»: поставщик, статус (Approved/Ordered/PartiallyDelivered — ожидается поставка), ожидаемая дата (ExpectedDate), сумма."),
        new("procurement.PurchaseOrderItems", "Позиции заказов поставщикам", "Количество, цена, получено и закрыто; позиция — через позицию заявки."),
        new("procurement.Tenders", "Тендеры", "Закупки через тендер: статус (Contracted — договор заключён), победитель, срок поставки.", "CreatedAt"),
        new("procurement.Arrivals", "Приезды машин", "Машина с сырьём на территории: качество → вес → склад; статусы Quality/Ready — ещё не на складе."),
        new("procurement.Suppliers", "Поставщики", "Поставщики: название, юрлицо, условия оплаты, срок поставки (LeadTimeDays, дни), что поставляет, активен ли."),
        new("procurement.Warehouses", "Склады снабжения", "Склады: код, вид (центральный, сырьё, цех), по умолчанию."),

        // ---- производство ----
        new("production.Recipes", "Техкарты (рецептуры)",
            "Версии техкарт: код, версия, статус (Draft/PendingApproval/Active/Archived; одна Active на код), выпуск (OutputName, OutputUnit, " +
            "OutputQuantity — нормы даны на это количество), связь с товаром Linko (LinkoProductId) и с позицией склада (OutputItemId)."),
        new("production.RecipeLines", "Нормы расхода техкарт", "Сырьё техкарты: позиция (ItemId → procurement.Items), норма на базу (Basis), потери (LossPercent)."),
        new("production.RecipeStages", "Этапы техкарт", "Этапы производства: выход (YieldPercent), брак, влажность."),
        new("production.Orders", "Производственные задания (план выпуска)",
            "Задания: техкарта, план (PlannedQty в единице выпуска), дата, статус (Draft/Released/InProgress/Completed/Cancelled), факт выпуска (OutputQty).",
            "PlannedDate"),
        new("production.OrderLines", "Сырьё заданий", "Потребность задания по сырью: норма, потери, нужно (RequiredQty), выдано, возвращено; факт = выдано − возвращено."),
        new("production.Shifts", "Смены производства", "Смены: выпуск годного (GoodOutput), упаковки, коробки; в расчёт идут только утверждённые (Approved)."),
        new("production.ShiftDefects", "Брак смен", "Брак по видам в кг."),
        new("production.ShiftMaterials", "Сырьё смен", "Взвешенное сырьё и брак сырья в единице позиции."),
        new("production.RecipeImportRecipes", "Импорт техкарт (черновики разбора)", "Техкарты из Excel до утверждения: статус разбора, база 1000 кг, предложенный товар Linko."),

        // ---- HR ----
        new("hr.Employees", "Сотрудники (Verifix)", "Все сотрудники компании из Verifix: ФИО, подразделение, должность, график, статус, даты приёма и увольнения. Контакты скрыты."),
        new("hr.TimesheetDays", "Табель по дням (Verifix)", "День сотрудника: план и факт часов, опоздание и ранний уход в минутах, отсутствие, документ.", "Date"),
        new("hr.AttendanceEvents", "Приходы и уходы (Verifix)", "События прохода сотрудников (терминалы Verifix)."),
        new("hr.References", "Справочники Verifix", "Подразделения (division), должности (job), графики (schedule), места (location): ключ — вид (Kind) + VerifixId."),

        // ---- задачи, AI, система ----
        new("work.Tasks", "Задачи отделов", "Задачи: отдел, исполнитель, срок, статус, приоритет, проект; закрытие и причина отмены."),
        new("ai.Alerts", "Находки проактивного анализа", "«Фокус дня»: риски, предупреждения и возможности по продажам, остаткам и первичке (правила без модели).", "DetectedAt"),
        new("ai.Usage", "Вызовы AI-моделей", "Каждый вызов модели: пользователь, агент, назначение, модель, токены (4 счётчика), стоимость в USD, время."),
        new("public.Departments", "Отделы компании", "Отделы OneBase: код и название."),
        new("public.Users", "Пользователи OneBase", "Учётные записи OneBase: имя, отдел, роль, активен ли. Пароль и контакты движку недоступны."),
    ];

    public static readonly IReadOnlyList<ColumnKnowledge> Columns =
    [
        new("linko.Orders.TotalPrice", "Сумма заказа, сум", ColumnRoles.Money),
        new("linko.Orders.TotalWeight", "Вес заказа, кг", ColumnRoles.Weight),
        new("linko.Orders.AcceptedDate", "Дата приёмки", ColumnRoles.Date, "Дата, по которой считается вторичка."),
        new("linko.Orders.Status", "Статус заказа", ColumnRoles.Status, "delivered, given — продано; not_delivered — в пути; другие — не продажа."),
        new("linko.Orders.AgentId", "ТП (агент)", ColumnRoles.Reference),
        new("linko.Orders.BranchName", "Филиал Linko", ColumnRoles.Name),
        new("linko.OrderLines.Amount", "Количество (шт или кг)", ColumnRoles.Quantity),
        new("linko.OrderLines.ReturnAmount", "Возвращено (шт или кг)", ColumnRoles.Quantity),
        new("linko.OrderLines.TotalPrice", "Сумма строки, сум", ColumnRoles.Money),
        new("linko.OrderLines.TotalWeight", "Вес строки, кг", ColumnRoles.Weight),
        new("linko.OrderReturnLines.Amount", "Количество возврата", ColumnRoles.Quantity),
        new("linko.ProductBalances.Balance", "Остаток (единица товара)", ColumnRoles.Quantity),
        new("linko.Reservations.Amount", "Бронь (единица товара)", ColumnRoles.Quantity),
        new("linko.Payments.Amount", "Сумма оплаты", ColumnRoles.Money),
        new("linko.Visits.IsInPlan", "По маршруту", ColumnRoles.Flag),
        new("linko.KpiPlans.Plan", "План показателя", ColumnRoles.Number),
        new("sales.RegionPlans.PlanKg", "План, кг", ColumnRoles.Weight),
        new("sales.FactoryPlans.PlanKg", "План, кг", ColumnRoles.Weight),
        new("procurement.StockBalances.OnHand", "Физический остаток", ColumnRoles.Quantity),
        new("procurement.StockBalances.Reserved", "Зарезервировано", ColumnRoles.Quantity),
        new("procurement.Items.MinQty", "Минимальный остаток", ColumnRoles.Quantity),
        new("procurement.StockMovements.Quantity", "Количество (+ приход, − расход)", ColumnRoles.Quantity),
        new("procurement.StockMovements.UnitCost", "Цена единицы, сум", ColumnRoles.Money),
        new("procurement.Suppliers.LeadTimeDays", "Срок поставки, дней", ColumnRoles.Count),
        new("production.Orders.PlannedQty", "План выпуска", ColumnRoles.Quantity),
        new("production.Orders.OutputQty", "Факт выпуска", ColumnRoles.Quantity),
        new("production.Shifts.GoodOutput", "Выпуск годного", ColumnRoles.Quantity),
        new("hr.TimesheetDays.LateMinutes", "Опоздание, мин", ColumnRoles.Count),
    ];

    public static readonly IReadOnlyList<RelationKnowledge> Relations =
    [
        new("linko.Orders.AgentId", "linko.Users.Id"),
        new("linko.OrderReturns.AgentId", "linko.Users.Id"),
        new("linko.Markets.ResponsibleAgentId", "linko.Users.Id"),
        new("linko.Orders.BranchId", "sales.Regions.LinkoBranchId"),
        new("linko.Markets.BranchId", "sales.Regions.LinkoBranchId"),
        new("linko.OrderReturns.BranchId", "sales.Regions.LinkoBranchId"),
        new("linko.OrderReturnLines.OrderId", "linko.Orders.Id"),
        new("linko.Payments.OrderId", "linko.Orders.Id"),
        // Планы ушедших ТП остаются, а самих ТП в справочнике Linko уже нет — по имени связь подтверждалась бы только на 80–90 %.
        new("linko.KpiPlans.UserId", "linko.Users.Id"),
        new("sales.StaffPlans.LinkoUserId", "linko.Users.Id"),
        new("sales.AgentPlans.LinkoUserId", "linko.Users.Id"),
        new("linko.Visits.UserId", "linko.Users.Id"),
        new("production.Recipes.LinkoProductId", "linko.Products.Id"),
        new("production.RecipeImportRecipes.LinkoProductId", "linko.Products.Id"),
        new("twin.ObjectLinks.LinkoStockId", "linko.Stocks.Id"),
        new("sales.RegionPlans.CategoryId", "linko.ProductTypes.Id"),
        new("sales.FactoryPlans.CategoryId", "linko.ProductTypes.Id"),
        new("sales.AgentPlans.CategoryId", "linko.ProductTypes.Id"),
        new("hr.Employees.DivisionId", "hr.References.VerifixId", "Kind=division"),
        new("hr.Employees.JobId", "hr.References.VerifixId", "Kind=job"),
        new("hr.Employees.ScheduleId", "hr.References.VerifixId", "Kind=schedule"),
        new("hr.TimesheetDays.DivisionId", "hr.References.VerifixId", "Kind=division"),
        new("hr.TimesheetDays.JobId", "hr.References.VerifixId", "Kind=job"),
        new("hr.TimesheetDays.VerifixEmployeeId", "hr.Employees.VerifixId"),
        new("hr.AttendanceEvents.VerifixEmployeeId", "hr.Employees.VerifixId"),
        new("hr.AttendanceEvents.DivisionId", "hr.References.VerifixId", "Kind=division"),
    ];

    public static readonly IReadOnlyList<MetricKnowledge> Metrics =
    [
        new("sales.revenue", "Выручка вторички", "sales", SoldRule, "get_sales, get_sales_period, get_sales_days", "linko.Orders, linko.OrderLines, linko.OrderReturns"),
        new("sales.kg", "Продажи в кг (вторичка)", "sales", "Вес проданных заказов по тем же правилам, что выручка, минус вес возвратов.", "get_sales, get_sales_period",
            "linko.Orders, linko.OrderLines, linko.OrderReturns"),
        new("sales.plan_execution", "Выполнение плана продаж", "sales",
            "Факт ÷ план (план РОП региона по категориям; без него на месяц — сумма планов ТП). Прогноз на конец месяца = факт ÷ прошедшие дни × дни месяца.",
            "get_sales, get_kpi", "sales.RegionPlans, linko.KpiPlans, sales.StaffPlans"),
        new("sales.akb", "АКБ (активная клиентская база)", "sales", "Точки с чистым весом продаж больше нуля за период.", "get_sales, get_customers", "linko.Orders, linko.Markets"),
        new("sales.strike", "Страйк (конверсия визитов)", "sales", "Визиты ТП с заказом ÷ выполненные визиты ТП, 0–100 %.", "get_visits", "linko.Visits, linko.Orders"),
        new("sales.primary", "Первичка", "sales", "Отгрузки завода дилерам (перемещения given/accepted со склада завода по дате доставки) и прямым клиентам завода, нетто возвратов.",
            "get_primary_shipments", "linko.StockTransfers, linko.StockTransferLines, linko.Orders"),
        new("sales.dealer_stock", "Остатки дилеров и дни покрытия", "sales", "Остаток − бронь; скорость продаж за 87 дней с поправкой на дни без товара; дефицит < 15 дней, излишек > 30.",
            "get_stock_reload", "linko.ProductBalances, linko.Reservations"),
        new("finance.dso", "DSO и долги дилеров", "finance", "Долги дилеров по FIFO из реестров Google Sheets, просрочка по нормам оплаты.", "get_dso, get_akt",
            "sales.DealerLedger, sales.DealerTerms"),
        new("finance.dealer_pnl", "P&L дилеров", "finance", "Прибыль завода на дилерах по месяцам (загружается через AI Data Manager).", "get_dealer_pnl", "sales.PnlValues"),
        new("procurement.available", "Доступный остаток сырья", "procurement",
            "OnHand − Reserved; для производства — только годные партии (качество Approved, срок не истёк) минус резерв.", "analytik_warehouse, get_warehouse_stock",
            "procurement.StockBalances, procurement.BatchBalances, procurement.Batches"),
        new("procurement.consumption", "Расход сырья", "procurement", "Выдачи отделам и в производство минус возвраты из производства (движения Issue, ProductionIssue, ProductionReturn).",
            "analytik_warehouse", "procurement.StockMovements"),
        new("procurement.need", "Потребность в закупке", "procurement",
            "Прогноз спроса за горизонт + страховой запас − доступный остаток − подтверждённые поставки (заказы и тендеры в работе, машины на территории).",
            "analytik_procurement_plan", "procurement.*, production.Orders, production.OrderLines"),
        new("production.plan_fact", "План и факт выпуска", "production", "План — PlannedQty заданий, факт — OutputQty и выпуск утверждённых смен.", "analytik_production",
            "production.Orders, production.Shifts"),
        new("hr.attendance", "Посещаемость и дисциплина", "hr", "Опоздания, ранние уходы, отсутствия, часы против плана по табелю Verifix.", "get_hr_timesheet, get_hr_deviations",
            "hr.TimesheetDays"),
    ];

    private static readonly Dictionary<string, TableKnowledge> TableIndex = Tables.ToDictionary(t => t.Target, StringComparer.Ordinal);
    private static readonly Dictionary<string, ColumnKnowledge> ColumnIndex = Columns.ToDictionary(c => c.Target, StringComparer.Ordinal);

    public static TableKnowledge? Table(string schema, string table) => TableIndex.GetValueOrDefault($"{schema}.{table}");

    public static ColumnKnowledge? Column(string schema, string table, string column) => ColumnIndex.GetValueOrDefault($"{schema}.{table}.{column}");
}
