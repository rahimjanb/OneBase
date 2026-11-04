using System.Globalization;

namespace OneBase.Application.Sales.Primary;

/// <summary>
/// Период обзора первички: Type — month, quarter или ytd (с начала года); From–To — границы периода, Through — по какой день есть данные
/// (не позже To; null — данных за период ещё нет: период в будущем).
/// </summary>
public sealed record PrimaryPeriod(string Type, int Year, int? Month, int? Quarter, DateOnly From, DateOnly To, DateOnly? Through);

/// <summary>
/// Направление обзора: republic (завод → дилеры и прямые клиенты) или export (заказы филиала «Завод» экспортным точкам). Тонны и сумма —
/// нетто возвратов; доли — от итога по тоннам и по сумме (0..1, итог 0 — null); Counterparties — контрагенты республики, Countries — страны
/// экспорта, Orders — документы отгрузки (перемещения и заказы) или заказы экспорта; AvgPricePerTon — сумма за тонну (тонн нет — null).
/// </summary>
public sealed record PrimaryOverviewDirection(
    string Id,
    decimal Tons,
    decimal Amount,
    decimal? ShareTons,
    decimal? ShareAmount,
    int? Counterparties,
    int? Countries,
    int Orders,
    decimal? AvgPricePerTon);

/// <summary>Месяц периода — для листа «По месяцам» выгрузки.</summary>
public sealed record PrimaryOverviewMonth(int Year, int Month, decimal RepublicTons, decimal RepublicAmount, decimal ExportTons, decimal ExportAmount);

/// <summary>
/// Обзор «Первички»: отгрузка завода всего (сумма направлений) и два направления за выбранный период; Months — по месяцам периода;
/// Years — годы с отгрузками (для выбора периода); DataThrough — последний день с данными.
/// </summary>
public sealed record PrimaryOverview(
    PrimaryPeriod Period,
    decimal TotalTons,
    decimal TotalAmount,
    IReadOnlyList<PrimaryOverviewDirection> Directions,
    IReadOnlyList<PrimaryOverviewMonth> Months,
    IReadOnlyList<int> Years,
    DateOnly? DataThrough);

/// <summary>Суммы направления за период — до долей и цены за тонну.</summary>
public sealed record PrimaryDirectionSums(decimal Kg, decimal Amount, int? Counterparties, int? Countries, int Orders);

/// <summary>Расчёты обзора первички без базы: период, доли, цена за тонну — проверяются тестами.</summary>
public static class PrimaryOverviewMath
{
    public const string Month = "month";
    public const string Quarter = "quarter";
    public const string Ytd = "ytd";

    /// <summary>
    /// Период из запроса: тип (по умолчанию — с начала года), год (по умолчанию — год последних данных), месяц и квартал (по умолчанию — месяц
    /// последних данных в этом году, у прошлого года — декабрь). Through — по последний день данных, но не дальше конца периода.
    /// </summary>
    public static PrimaryPeriod Resolve(string? type, int? year, int? month, int? quarter, DateOnly? dataThrough, DateOnly today)
    {
        var kind = type?.Trim().ToLowerInvariant() switch
        {
            Month => Month,
            Quarter => Quarter,
            _ => Ytd,
        };
        var y = year is >= 2000 and <= 2100 ? year.Value : dataThrough?.Year ?? today.Year;
        var lastMonth = dataThrough is { } d && d.Year == y ? d.Month : y < (dataThrough?.Year ?? today.Year) ? 12 : 1;
        var m = month is >= 1 and <= 12 ? month.Value : lastMonth;
        var q = quarter is >= 1 and <= 4 ? quarter.Value : (lastMonth - 1) / 3 + 1;
        var (from, to) = kind switch
        {
            Month => (new DateOnly(y, m, 1), new DateOnly(y, m, DateTime.DaysInMonth(y, m))),
            Quarter => (new DateOnly(y, 3 * q - 2, 1), new DateOnly(y, 3 * q, DateTime.DaysInMonth(y, 3 * q))),
            _ => (new DateOnly(y, 1, 1), new DateOnly(y, 12, 31)),
        };
        DateOnly? through = dataThrough is { } last && last >= from ? (last < to ? last : to) : null;
        return new PrimaryPeriod(kind, y, kind == Month ? m : null, kind == Quarter ? q : null, from, to, through);
    }

    /// <summary>Направления с долями и ценой за тонну; итог — их сумма (доли в сумме — 100%).</summary>
    public static (decimal Tons, decimal Amount, IReadOnlyList<PrimaryOverviewDirection> Directions) Combine(PrimaryDirectionSums republic, PrimaryDirectionSums export)
    {
        var totalKg = republic.Kg + export.Kg;
        var totalAmount = republic.Amount + export.Amount;
        PrimaryOverviewDirection Direction(string id, PrimaryDirectionSums s)
        {
            var tons = s.Kg / 1000m;
            return new PrimaryOverviewDirection(id, tons, s.Amount, Share(s.Kg, totalKg), Share(s.Amount, totalAmount), s.Counterparties, s.Countries, s.Orders,
                tons > 0 ? Math.Round(s.Amount / tons, 0) : null);
        }

        return (totalKg / 1000m, totalAmount, [Direction("republic", republic), Direction("export", export)]);
    }

    /// <summary>Доля от итога; итог не больше нуля — null.</summary>
    public static decimal? Share(decimal part, decimal total) => total > 0 ? Math.Round(part / total, 4) : null;

    /// <summary>Период словами для выгрузки: «2026 с начала года», «IV квартал 2026», «ноябрь 2026», с датами данных.</summary>
    public static string Label(PrimaryPeriod p)
    {
        var ru = CultureInfo.GetCultureInfo("ru-RU");
        var name = p.Type switch
        {
            Month => $"{ru.DateTimeFormat.GetMonthName(p.Month!.Value).ToLower(ru)} {p.Year}",
            Quarter => $"{new[] { "I", "II", "III", "IV" }[p.Quarter!.Value - 1]} квартал {p.Year}",
            _ => $"{p.Year} с начала года",
        };
        return p.Through is { } t ? $"{name} ({p.From:dd.MM.yyyy} – {t:dd.MM.yyyy})" : $"{name} (данных нет)";
    }
}
