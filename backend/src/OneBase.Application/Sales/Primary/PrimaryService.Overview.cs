using Microsoft.EntityFrameworkCore;
using OneBase.Application.Sales.Metrics;

namespace OneBase.Application.Sales.Primary;

/// <summary>
/// Обзор «Первички» за месяц, квартал или с начала года: республика — те же строки, что у первички (перемещения со склада завода дилерам и
/// заказы прямых клиентов, нетто возвратов, не позже отчётного дня), экспорт — те же заказы «Завода» экспортным точкам, что у «Экспорта».
/// Методика направлений — как у их страниц; здесь только сумма за выбранные даты.
/// </summary>
public sealed partial class PrimaryService
{
    public Task<PrimaryOverview> OverviewAsync(string? type, int? year, int? month, int? quarter, CancellationToken ct = default) =>
        SalesViewCache.GetAsync(cache, signal, $"sales:primary-overview:{type}:{year}:{month}:{quarter}", () => BuildOverviewAsync(type, year, month, quarter, ct));

    private async Task<PrimaryOverview> BuildOverviewAsync(string? type, int? year, int? month, int? quarter, CancellationToken ct)
    {
        // Республика — строки общего расчёта первички (тот же кэш, что у «Акта сверки»).
        var primary = await SalesViewCache.GetAsync(cache, signal, "sales:primary::", () => BuildAsync(null, null, ct));
        var republicRows = primary.AllRows ?? [];

        // Экспорт — заказы «Завода» экспортным точкам; отчётный день — как у страницы «Экспорт» (сегодня, не позже синхронизации заказов).
        var export = await LoadExportAsync(ct);
        var syncedAt = (await db.LinkoSyncStates.AsNoTracking().FirstOrDefaultAsync(s => s.Entity == "orders", ct))?.LastSuccessAt;
        var cutoff = PrimaryMath.DataLimit(SecondarySales.ReportCutoff(DateTimeOffset.UtcNow, options.IncludeToday), SyncedOn([syncedAt]));
        var exportRows = export.Sales.Where(s => s.Date <= cutoff).ToList();

        DateOnly? exportLast = exportRows.Count == 0 ? null : exportRows.Max(s => s.Date);
        var dataThrough = new[] { primary.AsOf, exportLast }.Where(d => d is not null).Max();
        var period = PrimaryOverviewMath.Resolve(type, year, month, quarter, dataThrough, SecondarySales.Today(DateTimeOffset.UtcNow));
        bool In(DateOnly d) => d >= period.From && d <= period.To;

        var republic = republicRows.Where(s => In(s.Date)).ToList();
        var shipments = republic.Where(s => !s.IsReturn).ToList();
        var exported = exportRows.Where(s => In(s.Date)).ToList();
        var (tons, amount, directions) = PrimaryOverviewMath.Combine(
            new PrimaryDirectionSums(republic.Sum(s => s.Amounts.Kg), republic.Sum(s => s.Amounts.SumFactory), shipments.Select(s => s.Counterparty).Distinct().Count(), null,
                shipments.Select(s => s.Doc).Distinct().Count()),
            new PrimaryDirectionSums(exported.Sum(s => s.Kg), exported.Sum(s => s.Sum), null, exported.Select(s => export.Countries[s.MarketId]).Distinct().Count(),
                exported.Where(s => s.OrderId != null).Select(s => s.OrderId).Distinct().Count()));

        var months = new List<PrimaryOverviewMonth>();
        for (var m = new DateOnly(period.From.Year, period.From.Month, 1); m <= period.To && m <= (period.Through ?? period.From.AddDays(-1)); m = m.AddMonths(1))
        {
            var (y, mo) = (m.Year, m.Month);
            var r = republic.Where(s => s.Date.Year == y && s.Date.Month == mo).ToList();
            var e = exported.Where(s => s.Date.Year == y && s.Date.Month == mo).ToList();
            months.Add(new PrimaryOverviewMonth(y, mo, r.Sum(s => s.Amounts.Kg) / 1000m, r.Sum(s => s.Amounts.SumFactory), e.Sum(s => s.Kg) / 1000m, e.Sum(s => s.Sum)));
        }

        var years = republicRows.Select(s => s.Date.Year).Concat(exportRows.Select(s => s.Date.Year)).Append(period.Year).Distinct().OrderByDescending(y => y).ToList();
        return new PrimaryOverview(period, tons, amount, directions, months, years, dataThrough);
    }
}
