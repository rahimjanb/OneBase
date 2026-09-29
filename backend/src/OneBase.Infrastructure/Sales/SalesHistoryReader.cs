using Microsoft.EntityFrameworkCore;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Sales;

/// <summary>
/// Агрегаты по прошлым месяцам одним SQL-запросом: выгружать ради них сотни тысяч строк продаж было бы дорого.
/// Правила те же, что во вторичке: дата реализации по настройке, без исключённых филиалов; АКБ — ТТ хотя бы с одним заказом.
/// </summary>
public sealed class SalesHistoryReader(OneBaseDbContext db, SalesOptions options) : ISalesHistoryReader
{
    public async Task<IReadOnlyList<MonthlyAkb>> AkbByMonthAsync(
        DateOnly from, DateOnly to, IReadOnlyDictionary<long, long> categoryGroups, CancellationToken ct)
    {
        var orderDate = options.DateField switch
        {
            SaleDateField.Accepted => """o."AcceptedDate" """,
            SaleDateField.Delivery => """coalesce(o."DeliveryDate", o."CreatedDate")""",
            _ => """o."CreatedDate" """,
        };

        // АКБ — уникальные ТТ хотя бы с одним заказом за месяц: возвраты АКБ не уменьшают, поэтому здесь только заказы.
        var sql = $$"""
            with g as (
                select distinct date_trunc('month', {{orderDate}})::date m, o."BranchId" branch, o."MarketId" market,
                       coalesce(map.grp, p."TypeId") grp
                from linko."OrderLines" l
                join linko."Orders" o on o."Id" = l."OrderId"
                left join linko."Products" p on p."Id" = l."ProductId"
                left join unnest({3}::bigint[], {4}::bigint[]) as map(type_id, grp) on map.type_id = p."TypeId"
                where o."Status" = any({0}) and {{orderDate}} between {1} and {2} and o."MarketId" is not null
                  and lower(coalesce(o."BranchName", '')) <> all({5})
            ),
            res as (
                select m, false by_branch, null::bigint branch, false total, grp, count(distinct market) akb from g group by m, grp
                union all
                select m, false, null, true, null, count(distinct market) from g group by m
                union all
                select m, true, branch, false, grp, count(distinct market) from g group by m, branch, grp
                union all
                select m, true, branch, true, null, count(distinct market) from g group by m, branch
            )
            select extract(year from m)::int "Year", extract(month from m)::int "Month", by_branch "ByBranch", branch "BranchId",
                   total "IsTotal", grp "CategoryId", akb::int "Akb"
            from res
            where akb > 0
            """;

        var rows = await db.Database
            .SqlQueryRaw<AkbRow>(sql,
                options.SoldStatuses,
                from,
                to,
                categoryGroups.Keys.ToArray(),
                categoryGroups.Values.ToArray(),
                options.ExcludedBranches.Select(b => b.Trim().ToLowerInvariant()).ToArray())
            .ToListAsync(ct);

        return rows.Select(r => new MonthlyAkb(r.Year, r.Month, r.ByBranch, r.BranchId, r.IsTotal, r.CategoryId, r.Akb)).ToList();
    }

    private sealed class AkbRow
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public bool ByBranch { get; set; }
        public long? BranchId { get; set; }
        public bool IsTotal { get; set; }
        public long? CategoryId { get; set; }
        public int Akb { get; set; }
    }
}
