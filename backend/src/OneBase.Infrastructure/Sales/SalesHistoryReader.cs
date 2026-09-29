using Microsoft.EntityFrameworkCore;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Sales;

/// <summary>
/// Агрегаты по прошлым месяцам одним SQL-запросом: выгружать ради них сотни тысяч строк продаж было бы дорого.
/// Правила те же, что в SalesAnalytics: продажа минус возврат, АКБ — ТТ с чистой выручкой &gt; 0.
/// </summary>
public sealed class SalesHistoryReader(OneBaseDbContext db, SalesOptions options) : ISalesHistoryReader
{
    public async Task<IReadOnlyList<MonthlyAkb>> AkbByMonthAsync(
        DateOnly from, DateOnly to, IReadOnlyDictionary<long, long> categoryGroups, CancellationToken ct)
    {
        var orderDate = options.DateField == SaleDateField.Delivery ? """coalesce(o."DeliveryDate", o."CreatedDate")""" : """o."CreatedDate" """;
        var sql = $$"""
            with lines as (
                select date_trunc('month', {{orderDate}})::date m, o."BranchId" branch, o."MarketId" market, p."TypeId" type_id, l."TotalPrice" revenue
                from linko."OrderLines" l
                join linko."Orders" o on o."Id" = l."OrderId"
                left join linko."Products" p on p."Id" = l."ProductId"
                where o."Status" = any({0}) and {{orderDate}} between {2} and {3} and o."MarketId" is not null
                union all
                select date_trunc('month', r."CreatedDate")::date, r."BranchId", r."MarketId", p."TypeId", -(rl."Price" * rl."Amount")
                from linko."OrderReturnLines" rl
                join linko."OrderReturns" r on r."Id" = rl."ReturnId"
                left join linko."Products" p on p."Id" = rl."ProductId"
                where r."Status" = any({1}) and r."CreatedDate" between {2} and {3} and r."MarketId" is not null
            ),
            g as (
                select l.m, l.branch, l.market, coalesce(map.grp, l.type_id) grp, sum(l.revenue) s
                from lines l
                left join unnest({4}::bigint[], {5}::bigint[]) as map(type_id, grp) on map.type_id = l.type_id
                group by 1, 2, 3, 4
            ),
            res as (
                select m, false by_branch, null::bigint branch, false total, grp, count(*) filter (where s > 0) akb
                    from (select m, market, grp, sum(s) s from g group by 1, 2, 3) x group by m, grp
                union all
                select m, false, null, true, null, count(*) filter (where s > 0)
                    from (select m, market, sum(s) s from g group by 1, 2) x group by m
                union all
                select m, true, branch, false, grp, count(*) filter (where s > 0) from g group by m, branch, grp
                union all
                select m, true, branch, true, null, count(*) filter (where s > 0)
                    from (select m, branch, market, sum(s) s from g group by 1, 2, 3) x group by m, branch
            )
            select extract(year from m)::int "Year", extract(month from m)::int "Month", by_branch "ByBranch", branch "BranchId",
                   total "IsTotal", grp "CategoryId", akb::int "Akb"
            from res
            where akb > 0
            """;

        var rows = await db.Database
            .SqlQueryRaw<AkbRow>(sql,
                options.SoldStatuses,
                options.ReturnStatuses,
                from,
                to,
                categoryGroups.Keys.ToArray(),
                categoryGroups.Values.ToArray())
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
