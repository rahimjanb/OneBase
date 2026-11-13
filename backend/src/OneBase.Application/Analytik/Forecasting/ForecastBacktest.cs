namespace OneBase.Application.Analytik.Forecasting;

/// <summary>
/// Месяц проверки прогноза: прогноз на месяц (сумма), факт, точность 1 − |прогноз − факт| ÷ факт (null — факта нет), метод. Saved = true —
/// настоящий сохранённый прогноз (analytik.Forecasts), false — проверка на истории («как сработал бы прогноз»).
/// </summary>
public sealed record BacktestMonth(DateOnly Month, decimal Forecast, decimal Fact, decimal? Accuracy, string Method, bool Saved);

/// <summary>Проверка на истории: прошлые месяцы и «прошлые прогнозы» по дням — что ждал прогноз, сделанный в начале месяца.</summary>
public sealed record BacktestResult(IReadOnlyList<BacktestMonth> Months, IReadOnlyList<SeriesPoint> Past);

/// <summary>
/// Проверка прогноза на истории («задним числом»): на начало каждого из прошлых месяцев прогноз строится той же процедурой, что и сейчас
/// (Forecaster: выбор метода по отложенной части, дневной шаг, 182 дня истории), но только на данных до этого дня, и сравнивается с фактом
/// месяца. Текущий месяц — до вчера: он даёт линию «Прошлые прогнозы» рядом с фактом, но в точность месяцев не входит.
/// </summary>
public static class ForecastBacktest
{
    /// <summary>Сколько дней истории берёт прогноз — как у прогноза по дням (ForecastService.HistoryAsync).</summary>
    public const int TrainDays = 182;

    /// <summary>Меньше двух недель истории до начала месяца — месяц не проверяется.</summary>
    public const int MinTrainDays = 14;

    /// <param name="daily">Дневной ряд факта без пропусков (последний день — вчера).</param>
    /// <param name="today">Сегодня (по Ташкенту).</param>
    /// <param name="months">Сколько прошлых полных месяцев проверить.</param>
    public static BacktestResult Run(IReadOnlyList<SeriesPoint> daily, DateOnly today, int months = 3)
    {
        var byDate = daily.ToDictionary(p => p.Date, p => p.Value);
        // Нули до начала данных (ряд продаж начинается с запрошенной даты, даже если Linko загружен позже) — не история.
        var firstData = daily.Where(p => p.Value != 0).Select(p => (DateOnly?)p.Date).Min();
        var end = today.AddDays(-1);
        var current = new DateOnly(today.Year, today.Month, 1);
        var rows = new List<BacktestMonth>();
        var past = new List<SeriesPoint>();
        if (firstData is not { } first)
        {
            return new BacktestResult(rows, past);
        }

        for (var k = months; k >= 0; k--)
        {
            var start = current.AddMonths(-k);
            var last = k == 0 ? end : start.AddMonths(1).AddDays(-1);
            var trainFrom = start.AddDays(-TrainDays) > first ? start.AddDays(-TrainDays) : first;
            var trainDays = start.DayNumber - trainFrom.DayNumber;
            if (last < start || trainDays < MinTrainDays || !byDate.ContainsKey(last))
            {
                continue;
            }

            var train = Enumerable.Range(0, trainDays).Select(i => trainFrom.AddDays(i)).Select(d => new SeriesPoint(d, byDate.GetValueOrDefault(d))).ToList();
            var result = Forecaster.Forecast(train, "day", last.DayNumber - start.DayNumber + 1);
            past.AddRange(result.Points.Select(p => new SeriesPoint(p.Date, p.Value)));
            if (k > 0)
            {
                var forecast = result.Points.Sum(p => p.Value);
                var fact = Enumerable.Range(0, last.DayNumber - start.DayNumber + 1).Sum(i => byDate.GetValueOrDefault(start.AddDays(i)));
                rows.Add(new BacktestMonth(start, Math.Round(forecast, 2), fact, Accuracy(forecast, fact), result.Method, false));
            }
        }

        return new BacktestResult(rows, past);
    }

    /// <summary>Точность прогноза: 1 − |прогноз − факт| ÷ факт, не ниже нуля; без факта — null.</summary>
    public static decimal? Accuracy(decimal forecast, decimal fact) =>
        fact <= 0 ? null : Math.Round(Math.Max(0, 1 - Math.Abs(forecast - fact) / fact), 4);
}
