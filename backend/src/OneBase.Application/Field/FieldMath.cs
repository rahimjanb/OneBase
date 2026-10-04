using OneBase.Domain.Field;

namespace OneBase.Application.Field;

/// <summary>Местное время компании (Узбекистан, UTC+5): «сегодня», рабочий день, дата снимка.</summary>
public static class FieldClock
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    public static DateTimeOffset Now => DateTimeOffset.UtcNow.ToOffset(Offset);
    public static DateOnly Today => DateOnly.FromDateTime(Now.DateTime);

    /// <summary>Начало местного дня в UTC — для сравнения с timestamptz (Npgsql пишет только UTC).</summary>
    public static DateTimeOffset DayStartUtc(DateOnly day) => new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();

    /// <summary>Местная дата момента.</summary>
    public static DateOnly LocalDate(DateTimeOffset at) => DateOnly.FromDateTime(at.ToOffset(Offset).DateTime);
}

/// <summary>Геометрия: расстояние между точками и проверка геозоны визита.</summary>
public static class FieldGeo
{
    private const double EarthRadiusM = 6_371_000;

    /// <summary>Коэффициент дороги: путь по улицам длиннее прямой.</summary>
    public const double RoadFactor = 1.3;

    public static double DistanceM(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusM * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public static bool HasCoordinates(double? lat, double? lon) =>
        lat is { } la && lon is { } lo && Math.Abs(la) > 0.0001 && Math.Abs(lo) > 0.0001 && Math.Abs(la) <= 90 && Math.Abs(lo) <= 180;

    /// <summary>
    /// Проверка при начале визита. Неточность GPS прощается до tolerance сверх радиуса (браузер честно сообщает точность —
    /// при плохом сигнале это сотни метров). Ничего не блокирует: результат — отметка для супервайзера.
    /// </summary>
    public static (FieldGeoStatus Status, double? DistanceM) Check(double? targetLat, double? targetLon, double? lat, double? lon, double? accuracyM, int radiusM, int toleranceM)
    {
        if (!HasCoordinates(lat, lon))
        {
            return (FieldGeoStatus.NoGps, null);
        }

        if (!HasCoordinates(targetLat, targetLon))
        {
            return (FieldGeoStatus.NoTarget, null);
        }

        var distance = DistanceM(lat!.Value, lon!.Value, targetLat!.Value, targetLon!.Value);
        var allowance = radiusM + Math.Min(Math.Max(accuracyM ?? 0, 0), toleranceM);
        return (distance <= allowance ? FieldGeoStatus.Ok : FieldGeoStatus.Far, Math.Round(distance));
    }
}

/// <summary>Порядок точек маршрута, длина, время. Чистые функции — проверяются тестами.</summary>
public static class FieldRouting
{
    public sealed record Stop(long MarketId, double? Lat, double? Lon);

    /// <summary>
    /// Порядок обхода: ближайший сосед от старта (позиция агента или первая точка), затем улучшение 2-opt.
    /// Точки без координат — в конце в исходном порядке.
    /// </summary>
    public static List<Stop> Order(IReadOnlyList<Stop> stops, double? startLat = null, double? startLon = null)
    {
        var located = stops.Where(s => FieldGeo.HasCoordinates(s.Lat, s.Lon)).ToList();
        var unlocated = stops.Where(s => !FieldGeo.HasCoordinates(s.Lat, s.Lon)).ToList();
        if (located.Count <= 1)
        {
            return [.. located, .. unlocated];
        }

        var order = new List<Stop>(located.Count);
        var left = new List<Stop>(located);
        var (curLat, curLon) = FieldGeo.HasCoordinates(startLat, startLon) ? (startLat!.Value, startLon!.Value) : (left[0].Lat!.Value, left[0].Lon!.Value);
        while (left.Count > 0)
        {
            var best = 0;
            var bestD = double.MaxValue;
            for (var i = 0; i < left.Count; i++)
            {
                var d = FieldGeo.DistanceM(curLat, curLon, left[i].Lat!.Value, left[i].Lon!.Value);
                if (d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }

            var next = left[best];
            order.Add(next);
            left.RemoveAt(best);
            (curLat, curLon) = (next.Lat!.Value, next.Lon!.Value);
        }

        TwoOpt(order);
        return [.. order, .. unlocated];
    }

    private static void TwoOpt(List<Stop> order)
    {
        if (order.Count < 4)
        {
            return;
        }

        double D(Stop a, Stop b) => FieldGeo.DistanceM(a.Lat!.Value, a.Lon!.Value, b.Lat!.Value, b.Lon!.Value);
        var improved = true;
        var guard = 0;
        while (improved && guard++ < 50)
        {
            improved = false;
            for (var i = 0; i < order.Count - 2; i++)
            {
                for (var j = i + 2; j < order.Count - 1; j++)
                {
                    var before = D(order[i], order[i + 1]) + D(order[j], order[j + 1]);
                    var after = D(order[i], order[j]) + D(order[i + 1], order[j + 1]);
                    if (after + 0.5 < before)
                    {
                        order.Reverse(i + 1, j - i);
                        improved = true;
                    }
                }
            }
        }
    }

    /// <summary>Длина маршрута по порядку, км (прямые × коэффициент дороги).</summary>
    public static decimal LengthKm(IReadOnlyList<Stop> ordered)
    {
        double total = 0;
        Stop? prev = null;
        foreach (var s in ordered.Where(s => FieldGeo.HasCoordinates(s.Lat, s.Lon)))
        {
            if (prev is not null)
            {
                total += FieldGeo.DistanceM(prev.Lat!.Value, prev.Lon!.Value, s.Lat!.Value, s.Lon!.Value);
            }

            prev = s;
        }

        return Math.Round((decimal)(total * FieldGeo.RoadFactor / 1000), 2);
    }

    /// <summary>Плановое время каждой точки и общая длительность: дорога со средней скоростью + время визита.</summary>
    public static (List<TimeOnly> Times, int TotalMinutes) Schedule(IReadOnlyList<Stop> ordered, TimeOnly dayStart, int visitMinutes, int speedKmh)
    {
        var times = new List<TimeOnly>(ordered.Count);
        double minutes = 0;
        Stop? prev = null;
        foreach (var s in ordered)
        {
            if (prev is not null && FieldGeo.HasCoordinates(prev.Lat, prev.Lon) && FieldGeo.HasCoordinates(s.Lat, s.Lon))
            {
                var km = FieldGeo.DistanceM(prev.Lat!.Value, prev.Lon!.Value, s.Lat!.Value, s.Lon!.Value) * FieldGeo.RoadFactor / 1000;
                minutes += km / Math.Max(speedKmh, 1) * 60;
            }

            times.Add(dayStart.AddMinutes(Math.Round(minutes)));
            minutes += visitMinutes;
            prev = s;
        }

        return (times, (int)Math.Round(minutes));
    }

    /// <summary>Куда вставить новую точку с наименьшим удлинением маршрута (индекс вставки в упорядоченный список).</summary>
    public static int InsertionIndex(IReadOnlyList<Stop> ordered, Stop stop)
    {
        if (ordered.Count == 0 || !FieldGeo.HasCoordinates(stop.Lat, stop.Lon))
        {
            return ordered.Count;
        }

        double D(Stop a, Stop b) => FieldGeo.HasCoordinates(a.Lat, a.Lon) && FieldGeo.HasCoordinates(b.Lat, b.Lon)
            ? FieldGeo.DistanceM(a.Lat!.Value, a.Lon!.Value, b.Lat!.Value, b.Lon!.Value)
            : 0;

        var best = ordered.Count;
        var bestCost = D(ordered[^1], stop);
        var first = D(stop, ordered[0]);
        if (first < bestCost)
        {
            best = 0;
            bestCost = first;
        }

        for (var i = 0; i < ordered.Count - 1; i++)
        {
            var cost = D(ordered[i], stop) + D(stop, ordered[i + 1]) - D(ordered[i], ordered[i + 1]);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = i + 1;
            }
        }

        return best;
    }
}

/// <summary>KPI: рабочие дни, темп плана, прогноз, статус агента за день.</summary>
public static class FieldKpi
{
    /// <summary>Рабочие дни месяца: понедельник — суббота (полевые продажи работают шесть дней).</summary>
    public static int WorkingDays(int year, int month) =>
        Enumerable.Range(1, DateTime.DaysInMonth(year, month)).Count(d => new DateTime(year, month, d).DayOfWeek != DayOfWeek.Sunday);

    /// <summary>Сколько рабочих дней прошло к концу дня date (включительно).</summary>
    public static int ElapsedWorkingDays(DateOnly date) =>
        Enumerable.Range(1, date.Day).Count(d => new DateTime(date.Year, date.Month, d).DayOfWeek != DayOfWeek.Sunday);

    public static decimal? Share(decimal fact, decimal? plan) => plan is > 0 ? fact / plan.Value : null;

    /// <summary>План на день: месячный ÷ рабочие дни.</summary>
    public static decimal? DailyPlan(decimal? monthPlan, int year, int month) => monthPlan is > 0 ? monthPlan.Value / WorkingDays(year, month) : null;

    /// <summary>Ожидаемая доля плана к дате при ровном темпе.</summary>
    public static decimal ExpectedShare(DateOnly date) => (decimal)ElapsedWorkingDays(date) / WorkingDays(date.Year, date.Month);

    /// <summary>Прогноз на конец месяца по текущему темпу.</summary>
    public static decimal? Forecast(decimal factToDate, DateOnly date)
    {
        var elapsed = ElapsedWorkingDays(date);
        return elapsed == 0 ? null : factToDate / elapsed * WorkingDays(date.Year, date.Month);
    }

    /// <summary>Отставание от темпа в процентных пунктах (положительное — отстаёт).</summary>
    public static decimal? BehindPace(decimal factToDate, decimal? monthPlan, DateOnly date) =>
        Share(factToDate, monthPlan) is { } share ? (ExpectedShare(date) - share) * 100 : null;

    public enum AgentDayStatus
    {
        NotStarted,
        OnRoute,
        OnVisit,
        Finished,
        Problem,
    }

    /// <summary>
    /// Статус агента за день. «Проблема» — пропущено больше трети маршрута или к вечеру нет ни визитов, ни заказов.
    /// </summary>
    /// <param name="restDay">Нерабочий день (воскресенье): без визитов и заказов — «не начинал», а не проблема.</param>
    public static AgentDayStatus DayStatus(bool visitInProgress, int plannedPoints, int visitedPoints, int skippedPoints, int ordersToday, int visitsToday, bool isPast, int localHour,
        bool restDay = false)
    {
        if (visitInProgress)
        {
            return AgentDayStatus.OnVisit;
        }

        if (plannedPoints > 0 && skippedPoints * 3 > plannedPoints)
        {
            return AgentDayStatus.Problem;
        }

        if (plannedPoints > 0 && visitedPoints + skippedPoints >= plannedPoints)
        {
            return AgentDayStatus.Finished;
        }

        if (visitsToday == 0 && ordersToday == 0)
        {
            return !restDay && (isPast || localHour >= 14) ? AgentDayStatus.Problem : AgentDayStatus.NotStarted;
        }

        return isPast ? AgentDayStatus.Finished : AgentDayStatus.OnRoute;
    }
}

/// <summary>Кто и как может менять статус задачи.</summary>
public static class FieldTaskRules
{
    public static readonly FieldTaskStatus[] Open = [FieldTaskStatus.New, FieldTaskStatus.Accepted, FieldTaskStatus.InProgress, FieldTaskStatus.Postponed];

    public static bool IsOpen(FieldTaskStatus s) => Open.Contains(s);

    /// <summary>
    /// Исполнитель: принять, начать, выполнить, отложить. Супервайзер/РМ: всё то же, плюс подтвердить выполненную,
    /// вернуть в работу и отменить.
    /// </summary>
    public static bool CanTransition(FieldTaskStatus from, FieldTaskStatus to, bool isAssignee, bool canPlan)
    {
        if (from == to)
        {
            return false;
        }

        var assigneeMove = to switch
        {
            FieldTaskStatus.Accepted => from is FieldTaskStatus.New or FieldTaskStatus.Postponed,
            FieldTaskStatus.InProgress => from is FieldTaskStatus.New or FieldTaskStatus.Accepted or FieldTaskStatus.Postponed,
            FieldTaskStatus.Completed => from is FieldTaskStatus.New or FieldTaskStatus.Accepted or FieldTaskStatus.InProgress,
            FieldTaskStatus.Postponed => from is FieldTaskStatus.New or FieldTaskStatus.Accepted or FieldTaskStatus.InProgress,
            _ => false,
        };

        if (isAssignee && assigneeMove)
        {
            return true;
        }

        if (!canPlan)
        {
            return false;
        }

        return assigneeMove || to switch
        {
            FieldTaskStatus.Verified => from == FieldTaskStatus.Completed,
            FieldTaskStatus.Cancelled => from is not (FieldTaskStatus.Verified or FieldTaskStatus.Cancelled),
            FieldTaskStatus.InProgress => from == FieldTaskStatus.Completed, // вернуть в работу
            _ => false,
        };
    }
}
