using OneBase.Application.Security;

namespace OneBase.Application.Analytik.Compute;

/// <summary>
/// Метод вычислительного движка: код, отдел, название, алгоритм (формула словами), версия, что нужно на входе (показатели, измерение, шаг),
/// параметры и ограничения. Версия меняется при изменении формулы — старые записи журнала остаются со своей версией.
/// </summary>
public sealed record ComputeMethod(
    string Code,
    string Domain,
    string Title,
    string Algorithm,
    string Version,
    int MinMetrics,
    int MaxMetrics,
    bool NeedsDimension,
    bool NeedsGrain,
    bool NeedsCompare,
    string Params,
    string Limits);

/// <summary>
/// Метод отдела (Expert Analytics): чем он считается — методом движка (compute), существующим инструментом OneBase (tool) или не считается,
/// потому что данных нет (missing, с объяснением). Право — без него метод пользователю не предлагается.
/// </summary>
public sealed record ExpertMethod(string Domain, string Code, string Title, string Kind, string Implementation, string? Permission, string Note);

/// <summary>Реестр методов движка и методов отделов: единственное место, где написано, что и как Аналитик умеет считать.</summary>
public static class ComputeCatalog
{
    public const string Version = "1.0";

    public static readonly IReadOnlyList<ComputeMethod> Methods =
    [
        new("describe", "general", "Описательная статистика", "n, сумма, среднее, медиана, σ, квартили, коэффициент вариации, асимметрия по участникам измерения или шагам времени",
            Version, 1, 1, false, false, false, "", "Описывает данные, связей и причин не проверяет"),
        new("trend", "general", "Тренд", "МНК y = a + b·t по шагам времени; наклон за шаг, в % от среднего, p наклона (t-тест), R²; скользящее среднее",
            Version, 1, 1, false, true, false, "window — окно скользящего среднего", "Линейный тренд; переломы и сезонность в нём не видны"),
        new("seasonality", "general", "Сезонность", "Индекс дня недели (среднее дня ÷ среднее ряда, шаг day) или месяца (нужно 24 месяца, шаг month)",
            Version, 1, 1, false, true, false, "", "Меньше 14 дней или 24 месяцев — не считается"),
        new("anomalies", "general", "Аномалии", "Робастная z-оценка по медиане и MAD против того же дня недели (шаг day) или прошлых шагов",
            Version, 1, 1, false, true, false, "check_last — сколько последних шагов проверить (по умолчанию 7)", "Находит отклонения, причин не объясняет"),
        new("forecast", "general", "Прогноз", "Выбор метода по отложенной выборке (WAPE): наивный, скользящее среднее, тренд, сезонный, Holt, Holt-Winters; диапазон по ошибке",
            Version, 1, 1, false, true, false, "horizon — шагов вперёд (по умолчанию 7 дней / 4 недели / 3 месяца)", "Прогноз — не факт; WAPE > 35 % — ориентировочно"),
        new("correlation", "general", "Корреляция", "Пирсон и Спирмен первого показателя с остальными по участникам измерения или шагам времени; p (t-тест)",
            Version, 2, 5, false, false, false, "", "Корреляция — не причина: класс вывода не выше «статистически подтверждённая связь»"),
        new("regression", "general", "Регрессия", "МНК: первый показатель — отклик, остальные — признаки; коэффициенты, SE, p, R², скорр. R², F",
            Version, 2, 5, false, false, false, "", "Линейная связь; коллинеарные признаки — отказ"),
        new("lag", "general", "Анализ запаздывания", "Корреляция x(t) и y(t+k), k = 0…max_lag; лучший сдвиг, p с поправкой Бонферрони",
            Version, 2, 2, false, true, false, "max_lag — наибольший сдвиг (по умолчанию 7)", "Опережение — не доказательство причины"),
        new("drivers", "general", "Вклад в изменение (Driver Analysis)", "Изменение итога периода к периоду сравнения по участникам измерения: вклад, доля в изменении, вклад в рост (п.п.), концентрация",
            Version, 1, 1, true, false, true, "", "Показывает, где изменилось, но не почему"),
        new("pvm", "sales", "Цена — объём — ассортимент (PVM)", "Объём = ΔV·P̄₀; ассортимент = Σq₁p₀ − V₁P̄₀; цена = Σq₁(p₁ − p₀); новые и выбывшие позиции отдельно; сумма = Δ выручки",
            Version, 0, 0, false, false, true, "dimension — product (по умолчанию) или product_group", "Цена — средняя цена кг позиции (со скидками)"),
        new("before_after", "general", "До / после", "Средние по дням до и после даты события (t-тест Уэлча), тренд «до» и эффект от продолжения тренда",
            Version, 1, 1, false, false, false, "change_date — дата события (обязательно)", "Без контрольной группы — не причина"),
        new("did", "general", "Разность разностей (контрольная группа)", "Регрессия y = b0 + b1·группа + b2·после + b3·группа×после; эффект — b3; проверка параллельных трендов до события",
            Version, 1, 1, true, false, false, "change_date (обязательно), treatment — участники группы, control — участники контроля (по умолчанию все остальные)",
            "Причинный вывод — только при параллельных трендах до события"),
        new("abc", "sales", "ABC-анализ", "Позиции по убыванию; A — накопленная доля до позиции < 80 %, B — < 95 %, C — остальные; HHI и доля топ-10",
            Version, 1, 1, true, false, false, "a, b — пороги, %", "Классы зависят от периода"),
        new("abc_xyz", "sales", "ABC/XYZ-анализ", "ABC по сумме; XYZ по коэффициенту вариации по шагам времени: X ≤ 10 %, Y ≤ 25 %, Z — больше или без продаж",
            Version, 1, 1, true, true, false, "x, y — пороги CV", "Нужно хотя бы 3 шага времени"),
        new("rfm", "sales", "RFM-анализ клиентов", "R — давность последней покупки (окна 7/14/30/60/90 дней), F — заказы, M — выручка; баллы 1–5 по квинтилям; сегменты",
            Version, 0, 0, false, false, false, "dimension — market (по умолчанию) или agent", "Давность — по окнам, а не точная дата"),
        new("cohorts", "sales", "Когорты удержания", "Клиенты по месяцу первой покупки в окне; доля покупавших в следующих месяцах",
            Version, 0, 0, false, false, false, "dimension — market (по умолчанию)", "Первая покупка — в пределах окна, а не за всю историю"),
        new("clusters", "general", "Кластеризация (k-средних)", "Стандартизация признаков, k-means++ (seed), итерации Ллойда; центры, размеры, силуэт",
            Version, 2, 5, true, false, false, "k — число кластеров (по умолчанию 4)", "Кластеры описывают похожесть, а не причину"),
        new("churn", "sales", "Отток клиентов", "Клиенты базового периода без покупки в последнем: число, доля, выручка базы под риском; по участникам измерения",
            Version, 0, 0, false, false, true, "base — период сравнения (compare_from/compare_to), recent — основной период", "Нет покупки в окне ≠ клиент ушёл навсегда"),
        new("mix", "sales", "Структура (Product Mix)", "Доли участников в итоге в двух периодах и изменение долей (п.п.)",
            Version, 1, 1, true, false, true, "", "Доли — от итога среза"),
        new("plan_variance", "finance", "Отклонение от плана (Variance)", "План, факт для плана, отклонение и выполнение по участникам; вклад участников в общее отклонение",
            Version, 0, 0, false, false, false, "dimension — region (по умолчанию), agent, product_group", "План — помесячный; бюджетов расходов в OneBase нет"),
        new("unit_economics", "finance", "Юнит-экономика продаж", "Выручка и кг на точку, на заказ, на агента; средняя цена кг; по участникам измерения",
            Version, 0, 0, false, false, false, "dimension — region (по умолчанию)", "Себестоимости и расходов в OneBase нет — маржа не считается"),
        new("cash_conversion", "finance", "Поступление денег", "Оплаты к выручке по шагам времени, лаг оплат к продажам (корреляция со сдвигом)",
            Version, 0, 0, false, true, false, "max_lag — сдвиг в шагах", "Оплаты в OneBase — с 01.09.2026"),
        new("stock_health", "warehouse", "Здоровье запаса (дилеры)", "Остаток, продажи в день за 30 дней, дни запаса; дефицит (< lead_days), излишек (> excess_days), без продаж 30/90 дней (неликвид)",
            Version, 0, 0, false, false, false, "lead_days (по умолчанию 7), excess_days (по умолчанию 60), dimension — product (по умолчанию) или product_group",
            "Остатки — текущий снимок Linko; истории остатков нет"),
    ];

    public static ComputeMethod? Method(string code) => Methods.FirstOrDefault(m => m.Code == code.Trim().ToLowerInvariant());

    public static readonly IReadOnlyList<ExpertMethod> Expert =
    [
        new("sales", "rfm", "RFM", "compute", "rfm", Permissions.SalesRead, "Сегменты клиентов по давности, частоте и деньгам"),
        new("sales", "abc_xyz", "ABC/XYZ", "compute", "abc_xyz", Permissions.SalesRead, "Вклад и стабильность товаров, клиентов, агентов"),
        new("sales", "forecast", "Прогноз продаж", "compute", "forecast", Permissions.SalesRead, "Прогноз с диапазоном и точностью метода"),
        new("sales", "churn", "Отток клиентов", "compute", "churn", Permissions.SalesRead, "Кто перестал покупать и сколько выручки под риском"),
        new("sales", "product_mix", "Product Mix", "compute", "mix", Permissions.SalesRead, "Изменение структуры продаж"),
        new("sales", "contribution", "Revenue Contribution", "compute", "drivers", Permissions.SalesRead, "Вклад регионов, групп, агентов в изменение"),
        new("sales", "pvm", "Цена — объём — ассортимент", "compute", "pvm", Permissions.SalesRead, "За счёт чего изменилась выручка"),
        new("finance", "cash_flow", "Cash Flow (поступления)", "compute", "cash_conversion", Permissions.FinanceRead,
            "Только поступления от точек; выплат и остатков денег в OneBase нет — полный денежный поток не считается"),
        new("finance", "profitability", "Прибыльность дилеров", "tool", "get_dealer_pnl", Permissions.FinanceRead, "P&L дилеров (если загружен); прибыли компании в OneBase нет"),
        new("finance", "variance", "Variance Analysis", "compute", "plan_variance", Permissions.SalesRead, "План и факт продаж; бюджетов расходов нет"),
        new("finance", "unit_economics", "Unit Economics", "compute", "unit_economics", Permissions.SalesRead, "На точку, заказ, агента; без себестоимости"),
        new("finance", "forecast", "Финансовый прогноз", "compute", "forecast", Permissions.FinanceRead, "Прогноз выручки и оплат"),
        new("finance", "budget_deviation", "Budget Deviation", "missing", "", Permissions.FinanceRead, "Бюджетов расходов в OneBase нет — отклонение от бюджета не считается"),
        new("production", "yield", "Production Yield и потери", "tool", "analytik_production", null, "Выпуск, брак, расход против нормы по заданиям и сменам"),
        new("production", "material_loss", "Material Loss", "tool", "analytik_production", null, "Перерасход сырья против техкарт"),
        new("production", "cost", "Production Cost", "tool", "analytik_production", null, "Себестоимость сырья по техкартам и рост цен"),
        new("production", "capacity", "Capacity Utilization", "tool", "analytik_simulate", null, "Загрузка мощности — по сменам или введённой мощности в симуляторе"),
        new("production", "bottleneck", "Bottleneck Detection", "tool", "analytik_simulate", null, "Узкие места — ограничения сценария (мощность, сырьё, сроки)"),
        new("production", "optimization", "Production Optimization", "tool", "analytik_scenario_analysis", null, "Оптимальный выпуск по марже при ограничениях сырья и мощности"),
        new("supply", "eoq_rop", "EOQ, страховой запас, точка заказа", "tool", "analytik_procurement_plan", null,
            "MRP с σ расхода и сроком поставки; EOQ — при заданных затратах на заказ и хранение"),
        new("supply", "supplier_performance", "Supplier Performance", "tool", "get_supplier_history", null, "Поставки и цены поставщиков"),
        new("supply", "procurement_optimization", "Procurement Optimization", "tool", "compare_supplier_offers", null, "Сравнение предложений поставщиков"),
        new("warehouse", "stock_turnover", "Оборачиваемость и дни запаса", "compute", "stock_health", Permissions.SalesRead, "Готовая продукция у дилеров"),
        new("warehouse", "dead_stock", "Dead Stock и Inventory Aging", "compute", "stock_health", Permissions.SalesRead, "Без продаж 30/90 дней"),
        new("warehouse", "stockout_risk", "Stockout Risk и Excess Inventory", "compute", "stock_health", Permissions.SalesRead, "Дефицит и излишек по дням запаса"),
        new("warehouse", "raw_materials", "Сырьё: дефицит, излишки, без движения", "tool", "analytik_warehouse", null, "Склад сырья OneBase"),
        new("hr", "productivity", "Workforce Productivity", "compute", "unit_economics", Permissions.SalesRead, "Продажи на агента; производительности цехов в OneBase нет"),
        new("hr", "performance", "Employee Performance", "compute", "plan_variance", Permissions.SalesRead, "Выполнение плана агентами"),
        new("hr", "attendance", "Дисциплина и посещаемость", "tool", "get_hr_timesheet", Permissions.HrRead, "Опоздания, отсутствия, явка (Verifix)"),
        new("hr", "turnover", "Turnover Analysis", "tool", "get_hr_employees", Permissions.HrRead, "Приём и увольнение по данным Verifix"),
        new("hr", "workload", "Workload Analysis", "tool", "get_department_tasks", null, "Задачи отделов: открыто, просрочено"),
        new("marketing", "cac", "CAC", "missing", "", null, "Расходов на привлечение и лидов в OneBase нет"),
        new("marketing", "romi", "ROMI", "missing", "", null, "Маркетинговых расходов в OneBase нет"),
        new("marketing", "conversion", "Conversion Analysis", "tool", "get_visits", Permissions.SalesRead, "Конверсия визитов в заказы; воронки лидов нет"),
        new("marketing", "campaigns", "Campaign Performance", "tool", "get_promo", Permissions.SalesRead, "Акции и скидки Linko; рекламных кампаний нет"),
    ];
}
