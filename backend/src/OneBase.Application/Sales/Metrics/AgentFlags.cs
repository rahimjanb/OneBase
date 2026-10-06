using System.Globalization;

namespace OneBase.Application.Sales.Metrics;

public enum FlagKind
{
    LowConversion,
    VisitsNoSales,
    SmallCheck,
    NarrowAssortment,
    TempoDrop,
    DataMismatch,
    LowData,
}

public enum FlagSeverity
{
    Critical,
    Risk,
    Info,
}

public sealed record AgentFlag(FlagKind Kind, FlagSeverity Severity, string Label, string Title, string Explanation);

/// <summary>
/// Визиты агента за месяц и его заказы. Orders — заказы агента, принятые в месяце: те же, что дали факт (статусы вторички,
/// приёмка не позже отчётного дня, без «Завода» и пропускаемых филиалов). WithOrder — для справки: выполненные визиты, в день
/// которых агент ввёл заказ в этой ТТ (по дню ввода, created_date).
/// </summary>
public sealed record VisitSummary(int Done, int WithOrder, int Orders)
{
    /// <summary>Визиты без заказа = выполненные визиты − заказы (как в «Полевом контроле»); у агента бывает и меньше нуля.</summary>
    public int WithoutOrder => Done - Orders;

    /// <summary>Конверсия визита («Страйк») = заказы, принятые в месяце ÷ выполненные визиты; может быть больше 100%.</summary>
    public decimal? Conversion => SalesMath.Ratio(Orders, Done);

    /// <summary>Заказов больше, чем визитов (конверсия больше 100%) — данные не сходятся.</summary>
    public bool DataMismatch => Orders > Done;

    /// <summary>visitLines — заказы по дню ввода (сшивка визит ↔ заказ); orders — строки факта месяца агента (заказы по приёмке).</summary>
    public static VisitSummary Of(IEnumerable<VisitRecord> visits, IEnumerable<SaleLine> visitLines, IEnumerable<SaleLine> orders)
    {
        // Визит «с заказом» — есть заказ того же агента в той же ТТ в тот же день.
        var orderKeys = visitLines
            .Where(l => l.OrderId != null && l.AgentId != null && l.MarketId != null)
            .Select(l => (l.AgentId!.Value, l.MarketId!.Value, l.Date))
            .ToHashSet();

        var done = visits.Where(v => v.Status == VisitStatus.Done).ToList();
        var withOrder = done.Count(v => orderKeys.Contains((v.AgentId, v.MarketId, v.Date)));
        return new VisitSummary(done.Count, withOrder, SalesMath.OrderCount(orders));
    }
}

/// <summary>Показатели агента за месяц, из которых считаются флаги и медианы.</summary>
public sealed record AgentStats(
    long AgentId,
    decimal Kg,
    decimal Revenue,
    int Akb,
    int Categories,
    VisitSummary Visits,
    decimal? Tempo,
    bool IsVacancy)
{
    /// <summary>Заказы агента, принятые в месяце: дали факт и выручку.</summary>
    public int Orders => Visits.Orders;
    public decimal? Conversion => Visits.Conversion;
    public decimal? SumPerVisit => SalesMath.Ratio(Revenue, Visits.Done);

    /// <summary>Средний чек — выручка на заказ (не на визит); заказы — те же, что дали выручку.</summary>
    public decimal? AvgCheck => SalesMath.Ratio(Revenue, Orders);
}

/// <summary>
/// Медианы региона — по агентам, с которыми честно сравнивать: не вакансии, не меньше MinVisits визитов и без флага
/// «данные не сходятся» (заказов больше, чем визитов).
/// </summary>
public sealed record RegionMedians(decimal? Conversion, decimal? SumPerVisit, decimal? AvgCheck)
{
    public static RegionMedians Of(IEnumerable<AgentStats> agents, int minVisits)
    {
        var eligible = agents.Where(a => !a.IsVacancy && a.Visits.Done >= minVisits && !a.Visits.DataMismatch).ToList();
        return new RegionMedians(
            SalesMath.Median(eligible.Where(a => a.Conversion != null).Select(a => a.Conversion!.Value)),
            SalesMath.Median(eligible.Where(a => a.SumPerVisit != null).Select(a => a.SumPerVisit!.Value)),
            SalesMath.Median(eligible.Where(a => a.AvgCheck != null).Select(a => a.AvgCheck!.Value)));
    }
}

/// <summary>Критерии «Проблемных агентов». Пороги — FlagThresholds (конфиг).</summary>
public static class AgentFlags
{
    public static IReadOnlyList<AgentFlag> Evaluate(
        AgentStats a,
        RegionMedians m,
        FlagThresholds t,
        int categoryTarget,
        int workedDays,
        int daysInMonth)
    {
        if (a.IsVacancy)
        {
            return [];
        }

        var v = a.Visits;
        if (v.Done < t.MinVisits)
        {
            return [new AgentFlag(FlagKind.LowData, FlagSeverity.Info, "Мало данных", "Мало данных",
                $"{v.Done} {SalesFormat.Plural(v.Done, "визит", "визита", "визитов")} за месяц — меньше {t.MinVisits}, агент не оценивается.")];
        }

        var flags = new List<AgentFlag>();

        // Заказов больше, чем визитов (конверсия больше 100%) — вопрос к данным, а не к работе агента: риск, без оценки конверсии.
        if (v.DataMismatch)
        {
            flags.Add(new AgentFlag(FlagKind.DataMismatch, FlagSeverity.Risk, "Данные", "Данные не сходятся",
                $"Заказов ({v.Orders}) больше, чем визитов ({v.Done}) — конверсия больше 100%. Конверсия не оценивается, агент исключён из медиан региона."));
        }
        else if (a.Conversion is { } conv)
        {
            var severity =
                conv < t.ConversionCritical || Below(conv, m.Conversion, t.ConversionCriticalOfMedian) ? FlagSeverity.Critical
                : conv < t.ConversionRisk || Below(conv, m.Conversion, t.ConversionRiskOfMedian) ? FlagSeverity.Risk
                : (FlagSeverity?)null;
            if (severity is { } s)
            {
                flags.Add(new AgentFlag(FlagKind.LowConversion, s, "Конверсия", "Низкая конверсия",
                    $"Конверсия {SalesFormat.Pct(conv)}{VersusMedian(m.Conversion, SalesFormat.Pct)}: " +
                    $"{v.Orders} {SalesFormat.Plural(v.Orders, "заказ", "заказа", "заказов")} на {v.Done} {SalesFormat.Plural(v.Done, "визит", "визита", "визитов")}."));
            }
        }

        if (a.Revenue <= 0)
        {
            flags.Add(new AgentFlag(FlagKind.VisitsNoSales, FlagSeverity.Critical, "Без продаж", "Ходит, но не продаёт",
                $"{v.Done} {SalesFormat.Plural(v.Done, "визит", "визита", "визитов")} за месяц и ни одной продажи."));
        }
        else if (a.SumPerVisit is { } spv && Below(spv, m.SumPerVisit, t.SumPerVisitRiskOfMedian))
        {
            flags.Add(new AgentFlag(FlagKind.VisitsNoSales, FlagSeverity.Risk, "Мало с визита", "Визиты дают мало выручки",
                $"{SalesFormat.Money(spv)} сум с визита против {SalesFormat.Money(m.SumPerVisit!.Value)} у медианного агента региона."));
        }

        if (a.AvgCheck is { } check && a.Conversion >= t.SmallCheckMinConversion && a.Orders >= t.SmallCheckMinOrders
            && Below(check, m.AvgCheck, t.SmallCheckOfMedian))
        {
            flags.Add(new AgentFlag(FlagKind.SmallCheck, FlagSeverity.Risk, "Чек", "Мелкий чек",
                $"Средний чек {SalesFormat.Money(check)} против {SalesFormat.Money(m.AvgCheck!.Value)} у медианного агента региона — при {a.Orders} заказах. " +
                "Конверсия при этом нормальная, значит дело не в отказах, а в том, что в заказе мало товара."));
        }

        if (a.Revenue > 0)
        {
            var severity = a.Categories <= t.NarrowAssortmentCritical ? FlagSeverity.Critical
                : a.Categories == categoryTarget - 1 ? FlagSeverity.Risk
                : (FlagSeverity?)null;
            if (severity is { } s)
            {
                flags.Add(new AgentFlag(FlagKind.NarrowAssortment, s, "Ассортимент", "Узкий ассортимент",
                    $"Продаёт {a.Categories} {SalesFormat.Plural(a.Categories, "категорию", "категории", "категорий")} при цели {categoryTarget}."));
            }
        }

        if (a.Tempo is { } tempo)
        {
            var severity = tempo < t.TempoCritical ? FlagSeverity.Critical : tempo < t.TempoRisk ? FlagSeverity.Risk : (FlagSeverity?)null;
            if (severity is { } s)
            {
                var raw = daysInMonth == 0 ? tempo : tempo * workedDays / daysInMonth;
                flags.Add(new AgentFlag(FlagKind.TempoDrop, s, "Темп", "Падение темпа",
                    $"Текущий месяц — {SalesFormat.Pct(tempo)} от собственного среднего. Считается с поправкой на неполный месяц: " +
                    $"отработано {workedDays} из {daysInMonth} дней, без поправки вышло бы {SalesFormat.Pct(raw)}."));
            }
        }

        return flags
            .OrderBy(f => f.Severity)
            .ToList();
    }

    public static bool IsEvaluated(IReadOnlyList<AgentFlag> flags) => flags.All(f => f.Kind != FlagKind.LowData);

    public static FlagSeverity? Worst(IEnumerable<AgentFlag> flags) =>
        flags.Where(f => f.Severity != FlagSeverity.Info).Select(f => (FlagSeverity?)f.Severity).Min();

    /// <summary>value ниже доли от медианы (если медиана есть).</summary>
    private static bool Below(decimal value, decimal? median, decimal share) => median is > 0 && value < median.Value * share;

    private static string VersusMedian(decimal? median, Func<decimal, string> format) =>
        median is { } m ? $" против {format(m)} у медианного агента региона" : string.Empty;
}

/// <summary>Форматирование чисел в пояснениях: «229 тыс», «412,1 млн», «11,32 млрд», «86,4%».</summary>
public static class SalesFormat
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static string Money(decimal value)
    {
        var abs = Math.Abs(value);
        return abs switch
        {
            >= 1_000_000_000 => (value / 1_000_000_000).ToString("0.##", Ru) + " млрд",
            >= 1_000_000 => (value / 1_000_000).ToString("0.#", Ru) + " млн",
            >= 1_000 => (value / 1_000).ToString("0", Ru) + " тыс",
            _ => value.ToString("0", Ru),
        };
    }

    public static string Pct(decimal share) => (share * 100).ToString(share * 100 >= 10 ? "0" : "0.#", Ru) + "%";

    public static string Plural(int n, string one, string few, string many)
    {
        var mod10 = n % 10;
        var mod100 = n % 100;
        if (mod10 == 1 && mod100 != 11) return one;
        if (mod10 is >= 2 and <= 4 && mod100 is < 12 or > 14) return few;
        return many;
    }
}
