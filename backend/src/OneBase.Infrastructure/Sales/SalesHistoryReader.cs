using Microsoft.EntityFrameworkCore;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Sales;

/// <summary>
/// Агрегаты по прошлым месяцам одним SQL-запросом: выгружать ради них сотни тысяч строк продаж было бы дорого.
/// Правила те же, что во вторичке: дата реализации по настройке, без исключённых филиалов; возвраты по строкам и дате
/// создания; АКБ — ТТ с чистым весом за месяц больше нуля (SalesMath.Akb).
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

        // АКБ — уникальные ТТ, у которых чистый вес за месяц (заказы минус возвраты) больше нуля: в каждом разрезе
        // (республика / филиал / агент, все категории / категория) чистый вес считается отдельно.
        var sql = $$"""
            with s as (
                select date_trunc('month', {{orderDate}})::date m, o."BranchId" branch, o."MarketId" market,
                       coalesce(map.grp, p."TypeId") grp, l."TotalWeight" kg, o."AgentId" agent
                from linko."OrderLines" l
                join linko."Orders" o on o."Id" = l."OrderId"
                left join linko."Products" p on p."Id" = l."ProductId"
                left join unnest({3}::bigint[], {4}::bigint[]) as map(type_id, grp) on map.type_id = p."TypeId"
                where o."Status" = any({0}) and {{orderDate}} between {1} and {2} and o."MarketId" is not null
                  and lower(coalesce(o."BranchName", '')) <> all({5})
                union all
                select date_trunc('month', r."CreatedDate")::date, r."BranchId", r."MarketId",
                       coalesce(map.grp, p."TypeId"), -l."TotalWeight", r."AgentId"
                from linko."OrderReturnLines" l
                join linko."OrderReturns" r on r."Id" = l."ReturnId"
                left join linko."Products" p on p."Id" = l."ProductId"
                left join unnest({3}::bigint[], {4}::bigint[]) as map(type_id, grp) on map.type_id = p."TypeId"
                where r."Status" = any({6}) and r."CreatedDate" between {1} and {2} and r."MarketId" is not null
                  and lower(coalesce(r."BranchName", '')) <> all({5})
            ),
            res as (
                select m, false by_branch, null::bigint branch, false total, grp, count(*) akb, null::bigint agent
                from (select m, market, grp from s group by m, market, grp having sum(kg) > 0) x group by m, grp
                union all
                select m, false, null, true, null, count(*), null
                from (select m, market from s group by m, market having sum(kg) > 0) x group by m
                union all
                select m, true, branch, false, grp, count(*), null
                from (select m, branch, market, grp from s group by m, branch, market, grp having sum(kg) > 0) x group by m, branch, grp
                union all
                select m, true, branch, true, null, count(*), null
                from (select m, branch, market from s group by m, branch, market having sum(kg) > 0) x group by m, branch
                union all
                select m, false, null, false, grp, count(*), agent
                from (select m, agent, market, grp from s where agent is not null group by m, agent, market, grp having sum(kg) > 0) x group by m, agent, grp
                union all
                select m, false, null, true, null, count(*), agent
                from (select m, agent, market from s where agent is not null group by m, agent, market having sum(kg) > 0) x group by m, agent
            )
            select extract(year from m)::int "Year", extract(month from m)::int "Month", by_branch "ByBranch", branch "BranchId",
                   total "IsTotal", grp "CategoryId", akb::int "Akb", agent "AgentId"
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
                options.ExcludedBranches.Select(b => b.Trim().ToLowerInvariant()).ToArray(),
                options.ReturnStatuses)
            .ToListAsync(ct);

        return rows.Select(r => new MonthlyAkb(r.Year, r.Month, r.ByBranch, r.BranchId, r.IsTotal, r.CategoryId, r.Akb, r.AgentId)).ToList();
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
        public long? AgentId { get; set; }
    }
}
