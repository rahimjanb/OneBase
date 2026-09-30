using OneBase.Application.Security;

namespace OneBase.AI.Knowledge;

/// <summary>Источник знаний: какие данные OneBase за ним стоят, в каких таблицах и отчётах, какое право нужно пользователю.</summary>
public sealed record KnowledgeSource(string Code, string Name, string Description, string Tables, string Reports, string? RequiredPermission);

/// <summary>Источники знаний AI-сотрудников — по карте данных OneBase (docs/ai-data-map.md).</summary>
public static class KnowledgeSources
{
    public const string SalesSecondary = "sales.secondary";
    public const string SalesVisits = "sales.visits";
    public const string SalesPlans = "sales.plans";
    public const string SalesTeam = "sales.team";
    public const string SalesPrimary = "sales.primary";
    public const string SalesStock = "sales.stock";
    public const string FinancePayments = "finance.payments";
    public const string SupplyProviders = "supply.providers";
    public const string Documents = "knowledge.documents";

    public static readonly IReadOnlyList<KnowledgeSource> All =
    [
        new(SalesSecondary, "Продажи (вторичка)",
            "Факт продаж в кг и сумах, возвраты, АКБ, категории и SKU — по республике, регионам, агентам, торговым точкам.",
            "linko.Orders, OrderLines, OrderReturns, OrderReturnLines, Markets, Products",
            "Продажи: обзор, республика, регионы, агенты, ассортимент, проблемные агенты",
            Permissions.SalesRead),
        new(SalesVisits, "Визиты и страйк", "Визиты торговых представителей, визиты с заказом и без, конверсия (страйк).",
            "linko.Visits", "Продажи: агенты, проблемные агенты", Permissions.SalesRead),
        new(SalesPlans, "Планы продаж", "Планы ТП из API планов Linko (вес, АКБ, сумма), план региона — сумма планов его ТП, выполнение и прогноз.",
            "linko.KpiPlans, sales.StaffPlans, sales.RegionPlans", "Продажи: планы, регионы", Permissions.SalesRead),
        new(SalesTeam, "Команда продаж", "Торговые представители и их должности, регионы, вакансии оргструктуры продаж.",
            "linko.Users, sales.Regions, sales.AgentProfiles", "Продажи: регионы, команда", Permissions.SalesRead),
        new(SalesPrimary, "Первичка", "Отгрузки завода дилерам в кг, коробках и сумах, план первички, возвраты дилеров.",
            "linko.StockTransfers, StockTransferLines, PriceLists", "Продажи: первичка", Permissions.SalesRead),
        new(SalesStock, "Остатки на складах", "Остатки на складах регионов и завода, дни покрытия, дефицит и затоварка.",
            "linko.Stocks, ProductBalances", "Продажи: остатки", Permissions.SalesRead),
        new(FinancePayments, "Оплаты",
            "Оплаты торговых точек из Linko: наличные и банк, принятые и нет. Расходов, прибыли и бюджетов в OneBase нет.",
            "linko.Payments", "—", Permissions.FinanceRead),
        new(SupplyProviders, "Поставщики", "Справочник поставщиков Linko (только названия; закупок и цен закупки в OneBase нет).",
            "linko.Providers", "—", Permissions.SalesRead),
        new(Documents, "База знаний", "Документы, загруженные в базу знаний AI: регламенты, отчёты, выгрузки Excel и CSV.",
            "ai.KnowledgeDocuments, ai.KnowledgeChunks", "Настройки → AI → База знаний", Permissions.FilesRead),
    ];

    public static KnowledgeSource? Find(string code) => All.FirstOrDefault(s => s.Code == code);
}
