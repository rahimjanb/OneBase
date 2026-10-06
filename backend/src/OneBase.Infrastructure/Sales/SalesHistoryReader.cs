using Microsoft.EntityFrameworkCore;
using OneBase.Application.Sales;
using OneBase.Application.Sales.Metrics;
using OneBase.Application.Sales.SkuSales;
using OneBase.Infrastructure.Persistence;

namespace OneBase.Infrastructure.Sales;

/// <summary>
/// Агрегаты по прошлым месяцам одним SQL-запросом: выгружать ради них сотни тысяч строк продаж было бы дорого.
/// Правила те же, что во вторичке: статусы Sales:SoldStatuses, дата реализации по настройке, без исключённых и пропускаемых
/// филиалов; возвраты по строкам и дате создания; АКБ — ТТ с чистым весом за месяц больше нуля (SalesMath.Akb).
/// </summary>
public sealed class SalesHistoryReader(OneBaseDbContext db, SalesOptions options) : ISalesHistoryReader
{
    /// <summary>Дата реализации заказа по настройке (по умолчанию — приёмка), как SecondarySales.SaleDate.</summary>
    private string OrderDate => options.DateField switch
    {
        SaleDateField.Accepted => """o."AcceptedDate" """,
        SaleDateField.Delivery => """coalesce(o."DeliveryDate", o."CreatedDate")""",
        _ => """o."CreatedDate" """,
    };

    public async Task<IReadOnlyList<MonthlyAkb>> AkbByMonthAsync(
        DateOnly from, DateOnly to, IReadOnlyDictionary<long, long> categoryGroups, CancellationToken ct)
    {
        var orderDate = OrderDate;
        // Точки-каналы и последний день последнего закрытого месяца (на отчётный день — вчера): идущий месяц каналов не получает.
        var channels = options.ChannelRegions
            .SelectMany((c, i) => c.Markets.Select(m => (Market: m, Branch: SalesOptions.ChannelBranchId(i), Since: c.SinceMonth() ?? DateOnly.MinValue)))
            .DistinctBy(c => c.Market)
            .ToList();
        var afterCutoff = SecondarySales.ReportCutoff(DateTimeOffset.UtcNow).AddDays(1);
        var closedThrough = new DateOnly(afterCutoff.Year, afterCutoff.Month, 1).AddDays(-1);

        // АКБ — уникальные ТТ, у которых чистый вес за месяц (заказы минус возвраты) больше нуля: в каждом разрезе
        // (республика / филиал / агент, все категории / категория) чистый вес считается отдельно. Рядом — чистые кг и выручка разреза
        // (ряды «кг» и «сум» в «АКБ по месяцам»): по всем ТТ, не только с положительным весом; выручка заказа не в базовой валюте — 0.
        // Точки-каналы (Sales:ChannelRegions: базар, сети) из филиалов первички в закрытых месяцах — в своём виртуальном филиале
        // (как SecondarySales.Build): их кг и выручка в рядах есть, в АКБ они не входят.
        var sql = $$"""
            with s as (
                select date_trunc('month', {{orderDate}})::date m, coalesce(ch.vb, o."BranchId") branch, o."MarketId" market, ch.vb is not null chan,
                       coalesce(map.grp, p."TypeId") grp, l."TotalWeight" kg, o."AgentId" agent,
                       case when coalesce(btrim(o."Currency"), '') = '' or lower(btrim(o."Currency")) = {7} then l."TotalPrice" else 0 end rev
                from linko."OrderLines" l
                join linko."Orders" o on o."Id" = l."OrderId"
                left join linko."Products" p on p."Id" = l."ProductId"
                left join unnest({3}::bigint[], {4}::bigint[]) as map(type_id, grp) on map.type_id = p."TypeId"
                left join unnest({8}::bigint[], {9}::bigint[], {12}::date[]) as ch(market, vb, since)
                       on ch.market = o."MarketId" and lower(coalesce(o."BranchName", '')) = any({10}) and {{orderDate}} <= {11}
                          and {{orderDate}} >= ch.since
                where o."Status" = any({0}) and {{orderDate}} between {1} and {2} and o."MarketId" is not null
                  and (lower(coalesce(o."BranchName", '')) <> all({5}) or ch.vb is not null)
                union all
                select date_trunc('month', r."CreatedDate")::date, coalesce(ch.vb, r."BranchId"), r."MarketId", ch.vb is not null,
                       coalesce(map.grp, p."TypeId"), -l."TotalWeight", r."AgentId", -(l."Price" * l."Amount")
                from linko."OrderReturnLines" l
                join linko."OrderReturns" r on r."Id" = l."ReturnId"
                left join linko."Products" p on p."Id" = l."ProductId"
                left join unnest({3}::bigint[], {4}::bigint[]) as map(type_id, grp) on map.type_id = p."TypeId"
                left join unnest({8}::bigint[], {9}::bigint[], {12}::date[]) as ch(market, vb, since)
                       on ch.market = r."MarketId" and lower(coalesce(r."BranchName", '')) = any({10}) and r."CreatedDate" <= {11}
                          and r."CreatedDate" >= ch.since
                where r."Status" = any({6}) and r."CreatedDate" between {1} and {2} and r."MarketId" is not null
                  and (lower(coalesce(r."BranchName", '')) <> all({5}) or ch.vb is not null)
            ),
            res as (
                select m, false by_branch, null::bigint branch, false total, grp, count(*) filter (where kg > 0 and not chan) akb, null::bigint agent, sum(kg) kg, sum(rev) rev
                from (select m, market, grp, sum(kg) kg, sum(rev) rev, bool_or(chan) chan from s group by m, market, grp) x group by m, grp
                union all
                select m, false, null, true, null, count(*) filter (where kg > 0 and not chan), null, sum(kg), sum(rev)
                from (select m, market, sum(kg) kg, sum(rev) rev, bool_or(chan) chan from s group by m, market) x group by m
                union all
                select m, true, branch, false, grp, count(*) filter (where kg > 0 and not chan), null, sum(kg), sum(rev)
                from (select m, branch, market, grp, sum(kg) kg, sum(rev) rev, bool_or(chan) chan from s group by m, branch, market, grp) x group by m, branch, grp
                union all
                select m, true, branch, true, null, count(*) filter (where kg > 0 and not chan), null, sum(kg), sum(rev)
                from (select m, branch, market, sum(kg) kg, sum(rev) rev, bool_or(chan) chan from s group by m, branch, market) x group by m, branch
                union all
                select m, false, null, false, grp, count(*) filter (where kg > 0 and not chan), agent, sum(kg), sum(rev)
                from (select m, agent, market, grp, sum(kg) kg, sum(rev) rev, bool_or(chan) chan from s where agent is not null group by m, agent, market, grp) x group by m, agent, grp
                union all
                select m, false, null, true, null, count(*) filter (where kg > 0 and not chan), agent, sum(kg), sum(rev)
                from (select m, agent, market, sum(kg) kg, sum(rev) rev, bool_or(chan) chan from s where agent is not null group by m, agent, market) x group by m, agent
            )
            select extract(year from m)::int "Year", extract(month from m)::int "Month", by_branch "ByBranch", branch "BranchId",
                   total "IsTotal", grp "CategoryId", akb::int "Akb", agent "AgentId", kg "Kg", rev "Revenue"
            from res
            where akb > 0 or kg <> 0 or rev <> 0
            """;

        var rows = await db.Database
            .SqlQueryRaw<AkbRow>(sql,
                options.SoldStatuses,
                from,
                to,
                categoryGroups.Keys.ToArray(),
                categoryGroups.Values.ToArray(),
                options.NotSecondaryBranchesLower(),
                options.ReturnStatuses,
                options.BaseCurrency.Trim().ToLowerInvariant(),
                channels.Select(c => c.Market).ToArray(),
                channels.Select(c => c.Branch).ToArray(),
                options.PrimaryOrderBranches.Select(b => b.Trim().ToLowerInvariant()).ToArray(),
                closedThrough,
                channels.Select(c => c.Since).ToArray())
            .ToListAsync(ct);

        return rows.Select(r => new MonthlyAkb(r.Year, r.Month, r.ByBranch, r.BranchId, r.IsTotal, r.CategoryId, r.Akb, r.AgentId, r.Kg, r.Revenue)).ToList();
    }

    /// <summary>
    /// «Продажи по SKU» за дни [from; to] одним запросом. Строки — как SecondarySales.Build: заказы проданных статусов по дате реализации
    /// (вес и сумма строки; сумма заказа не в базовой валюте — 0), возвраты по строкам (вес, количество × цена) по дате создания, без
    /// исключённых и пропускаемых филиалов; филиал → регион по branchRegions. Месячная строка — товар × ТТ × месяц (в регионе — × регион),
    /// чистые кг и выручка; АКБ SKU — различные ТТ, у которых такая строка положительна (кг или выручка больше нуля) хотя бы в одном месяце.
    /// </summary>
    public async Task<SkuSalesAggregate> SkuSalesAsync(DateOnly from, DateOnly to, IReadOnlyDictionary<long, Guid> branchRegions, CancellationToken ct)
    {
        var orderDate = OrderDate;
        var sql = $$"""
            with s as (
                select date_trunc('month', {{orderDate}})::date m, l."ProductId" product, o."MarketId" market, reg.region, l."TotalWeight" kg,
                       case when coalesce(btrim(o."Currency"), '') = '' or lower(btrim(o."Currency")) = {7} then l."TotalPrice" else 0 end revenue
                from linko."OrderLines" l
                join linko."Orders" o on o."Id" = l."OrderId"
                left join unnest({3}::bigint[], {4}::uuid[]) as reg(branch, region) on reg.branch = o."BranchId"
                where o."Status" = any({0}) and {{orderDate}} between {1} and {2} and l."ProductId" is not null
                  and lower(coalesce(o."BranchName", '')) <> all({5})
                union all
                select date_trunc('month', r."CreatedDate")::date, l."ProductId", r."MarketId", reg.region, -l."TotalWeight", -(l."Price" * l."Amount")
                from linko."OrderReturnLines" l
                join linko."OrderReturns" r on r."Id" = l."ReturnId"
                left join unnest({3}::bigint[], {4}::uuid[]) as reg(branch, region) on reg.branch = r."BranchId"
                where r."Status" = any({6}) and r."CreatedDate" between {1} and {2} and l."ProductId" is not null
                  and lower(coalesce(r."BranchName", '')) <> all({5})
            ),
            lines as (
                select m, product, region, market, sum(kg) kg, sum(revenue) revenue
                from s where market is not null group by m, product, region, market
            )
            select 'month' "Kind", product "ProductId", region "RegionId", extract(year from m)::int "Year", extract(month from m)::int "Month",
                   sum(kg) "Kg", sum(revenue) "Revenue", 0 "Akb"
            from s group by product, region, m
            union all
            select 'region', product, region, 0, 0, 0, 0, count(distinct market)::int
            from lines where kg > 0 or revenue > 0 group by product, region
            union all
            select 'total', product, null, 0, 0, 0, 0, count(distinct market)::int
            from (select m, product, market from lines group by m, product, market having sum(kg) > 0 or sum(revenue) > 0) x group by product
            """;

        var rows = await db.Database
            .SqlQueryRaw<SkuRow>(sql,
                options.SoldStatuses,
                from,
                to,
                branchRegions.Keys.ToArray(),
                branchRegions.Values.ToArray(),
                options.NotSecondaryBranchesLower(),
                options.ReturnStatuses,
                options.BaseCurrency.Trim().ToLowerInvariant())
            .ToListAsync(ct);

        return new SkuSalesAggregate(
            rows.Where(r => r.Kind == "month").Select(r => new SkuMonthSales(r.ProductId, r.RegionId, r.Year, r.Month, r.Kg, r.Revenue)).ToList(),
            rows.Where(r => r.Kind == "total").ToDictionary(r => r.ProductId, r => r.Akb),
            rows.Where(r => r.Kind == "region").Select(r => new SkuRegionAkb(r.ProductId, r.RegionId, r.Akb)).ToList());
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
        public decimal Kg { get; set; }
        public decimal Revenue { get; set; }
    }

    private sealed class SkuRow
    {
        public string Kind { get; set; } = "";
        public long ProductId { get; set; }
        public Guid? RegionId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Kg { get; set; }
        public decimal Revenue { get; set; }
        public int Akb { get; set; }
    }
}
