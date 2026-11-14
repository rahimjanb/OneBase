using System.Text;
using OneBase.Application.Analytik.Query;
using OneBase.Application.Sales;

namespace OneBase.Application.Analytik.Semantic;

/// <summary>Фильтр после сопоставления названий: колонка факта и значения (id, текст в нижнем регистре) или «пусто» (без региона и т.п.).</summary>
public sealed record ResolvedFilter(string Dimension, bool Exclude, IReadOnlyList<object> Values, bool MatchNull, string Description);

/// <summary>Собранный SQL факта: текст, параметры, таблицы, коды измерений и показателей в порядке колонок.</summary>
public sealed record FactSql(string Sql, IReadOnlyList<SqlParam> Parameters, string Tables, IReadOnlyList<string> Dimensions, IReadOnlyList<string> Metrics);

/// <summary>
/// SQL фактов движка. Модель SQL не пишет: измерения и показатели — только из белого списка ниже, имена таблиц и колонок — константы,
/// значения (даты, статусы, id, названия) — параметры. Правила продаж — как SalesHistoryReader / SecondarySales: заказы проданных статусов
/// по дате реализации (сумма строки в сумах, другой валюты — 0), возвраты по строкам и дате создания, без пропускаемых филиалов,
/// «Завод» — экспорт и опт (не вторичка), точки-каналы из филиалов первички — в своём регионе в закрытых месяцах.
/// </summary>
public sealed class SemanticSql(SemanticContext ctx, SalesOptions options, DateOnly? today = null)
{
    private readonly List<SqlParam> _params = [];

    private string P(object? value, string cast)
    {
        var name = $"p{_params.Count}";
        _params.Add(new SqlParam(name, value));
        return $"@{name}::{cast}";
    }

    private string OrderDate => options.DateField switch
    {
        SaleDateField.Accepted => """o."AcceptedDate" """,
        SaleDateField.Delivery => """coalesce(o."DeliveryDate", o."CreatedDate")""",
        _ => """o."CreatedDate" """,
    };

    /// <summary>Выражение измерения над строками факта x (у даты — шаг).</summary>
    public static string DimensionExpr(string fact, string dim, string grain) => dim switch
    {
        "date" when grain == "day" => "x.d",
        "date" => $"date_trunc('{grain}', x.d)::date",
        "region" => "x.region",
        "branch" => "x.branch",
        "direction" => "x.dir",
        "manager" => "x.manager",
        "supervisor" => "x.sup",
        "dealer" => "x.dealer",
        "sales_channel" => "(case when x.chan then 'ch:' || x.branch::text when x.factory then 'factory' else 'regions' end)",
        "agent" => "x.agent",
        "market" => "x.market",
        "market_type" => "x.mtype",
        "product" or "sku" => "x.product",
        "product_type" => "x.type_id",
        "product_group" => "x.grp",
        "stock" => "x.stock",
        "source" => "'linko'::text",
        _ => throw new AnalyticsQueryException("invalid", $"Измерение «{dim}» не поддерживается фактом «{fact}»."),
    };

    /// <summary>Агрегат показателя продаж (один уровень).</summary>
    private static string SalesAgg(string metric) => metric switch
    {
        "revenue" => "sum(x.rev)",
        "gross_revenue" => "sum(x.gross_rev)",
        "returns_revenue" => "sum(x.ret_rev)",
        "discount" => "sum(x.disc)",
        "kg" => "sum(x.kg)",
        "gross_kg" => "sum(x.gross_kg)",
        "returns_kg" => "sum(x.ret_kg)",
        "units" => "sum(x.qty)",
        "orders" => "count(distinct x.order_id)",
        "other_currency_revenue" => "sum(x.other_rev)",
        _ => throw new AnalyticsQueryException("invalid", $"Показатель «{metric}» не считается из строк продаж."),
    };

    private string Base(string column) =>
        $"(coalesce(btrim({column}), '') = '' or lower(btrim({column})) = {P(options.BaseCurrency.Trim().ToLowerInvariant(), "text")})";

    /// <summary>Строки факта продаж x: заказы и возвраты с оргструктурой, категорией и (при нужде) типом точки.</summary>
    private string SalesRows(DateOnly from, DateOnly to, string segment, SalesRowScope scope, bool markets)
    {
        var od = OrderDate;
        var channels = ctx.Channels.SelectMany(c => c.Markets.Select(m => (Market: m, c.Branch, c.Since))).DistinctBy(c => c.Market).ToList();
        var closed = SecondarySales.ClosedThrough(DateTimeOffset.UtcNow);
        var fromP = P(from, "date");
        var toP = P(to, "date");
        var excluded = P(ctx.ExcludedLower.ToArray(), "text[]");
        var notSecondary = P(ctx.ExcludedLower.Concat(ctx.IgnoredLower).Distinct().ToArray(), "text[]");
        var ignored = P(ctx.IgnoredLower.ToArray(), "text[]");
        var primary = P(ctx.PrimaryLower.ToArray(), "text[]");
        var closedP = P(closed, "date");

        string Segment(string branchName) => segment switch
        {
            "factory" => $"(ch.vb is null and lower(coalesce({branchName}, '')) = any({excluded}))",
            "all" => $"(ch.vb is not null or lower(coalesce({branchName}, '')) <> all({ignored}))",
            _ => $"(ch.vb is not null or lower(coalesce({branchName}, '')) <> all({notSecondary}))",
        };

        string Scope(string branch, string agent) => scope.Level switch
        {
            SalesRowScope.RegionLevel => $" and {branch} = any({P(scope.BranchIds.ToArray(), "bigint[]")})",
            SalesRowScope.AgentLevel => $" and {agent} = {P(scope.AgentId ?? -1, "bigint")}",
            _ => "",
        };

        var baseO = Base("o.\"Currency\"");
        var sold = P(options.SoldStatuses, "text[]");
        var returned = P(options.ReturnStatuses, "text[]");
        var sb = new StringBuilder();
        sb.Append($$"""
            ch as (select * from unnest({{P(channels.Select(c => c.Market).ToArray(), "bigint[]")}}, {{P(channels.Select(c => c.Branch).ToArray(), "bigint[]")}}, {{P(channels.Select(c => c.Since).ToArray(), "date[]")}}) as c(market, vb, since)),
            f as (
                select {{od}} d, o."Id" order_id, coalesce(ch.vb, o."BranchId") branch, ch.vb is not null chan,
                       (ch.vb is null and lower(coalesce(o."BranchName", '')) = any({{excluded}})) factory,
                       o."MarketId" market, o."AgentId" agent, l."ProductId" product, p."TypeId" type_id,
                       l."TotalWeight" kg, l."TotalWeight" gross_kg, 0::numeric ret_kg,
                       case when {{baseO}} then l."TotalPrice" else 0 end rev, case when {{baseO}} then l."TotalPrice" else 0 end gross_rev, 0::numeric ret_rev,
                       l."Amount" qty, coalesce(l."TotalDiscount", 0) disc, case when {{baseO}} then 0 else l."TotalPrice" end other_rev
                from linko."OrderLines" l
                join linko."Orders" o on o."Id" = l."OrderId"
                left join linko."Products" p on p."Id" = l."ProductId"
                left join ch on ch.market = o."MarketId" and lower(coalesce(o."BranchName", '')) = any({{primary}}) and {{od}} <= {{closedP}} and {{od}} >= ch.since
                where o."Status" = any({{sold}}) and {{od}} between {{fromP}} and {{toP}} and {{Segment("o.\"BranchName\"")}}{{Scope("o.\"BranchId\"", "o.\"AgentId\"")}}
                union all
                select r."CreatedDate", null::bigint, coalesce(ch.vb, r."BranchId"), ch.vb is not null,
                       (ch.vb is null and lower(coalesce(r."BranchName", '')) = any({{excluded}})),
                       r."MarketId", r."AgentId", l."ProductId", p."TypeId",
                       -l."TotalWeight", 0::numeric, l."TotalWeight",
                       -(l."Price" * l."Amount"), 0::numeric, l."Price" * l."Amount",
                       -l."Amount", 0::numeric, 0::numeric
                from linko."OrderReturnLines" l
                join linko."OrderReturns" r on r."Id" = l."ReturnId"
                left join linko."Products" p on p."Id" = l."ProductId"
                left join ch on ch.market = r."MarketId" and lower(coalesce(r."BranchName", '')) = any({{primary}}) and r."CreatedDate" <= {{closedP}} and r."CreatedDate" >= ch.since
                where r."Status" = any({{returned}}) and r."CreatedDate" between {{fromP}} and {{toP}} and {{Segment("r.\"BranchName\"")}}{{Scope("r.\"BranchId\"", "r.\"AgentId\"")}}
            ),
            x as (
                select f.*, coalesce(cm.grp, f.type_id) grp, {{Structure("f.branch")}}{{(markets ? ", mk.\"MarketTypeName\" mtype" : "")}}
                from f
                left join unnest({{P(ctx.Categories.Mapping.Keys.ToArray(), "bigint[]")}}, {{P(ctx.Categories.Mapping.Values.ToArray(), "bigint[]")}}) as cm(type_id, grp) on cm.type_id = f.type_id
                {{StructureJoins("f.branch")}}{{(markets ? "\n                left join linko.\"Markets\" mk on mk.\"Id\" = f.market" : "")}}
            )
            """);
        return sb.ToString();
    }

    private static string Structure(string _) => "br.region, rg.dir, rg.sup, rg.dealer, dr.manager";

    /// <summary>Филиал → регион вторички → направление, СВР, дилер → РМ направления (параметрами, как в отчётах продаж).</summary>
    private string StructureJoins(string branch)
    {
        var regions = ctx.Regions.Values.ToList();
        var directions = ctx.Directions.Values.ToList();
        return $"""
            left join unnest({P(ctx.BranchRegion.Keys.ToArray(), "bigint[]")}, {P(ctx.BranchRegion.Values.ToArray(), "uuid[]")}) as br(branch, region) on br.branch = {branch}
                            left join unnest({P(regions.Select(r => r.Id).ToArray(), "uuid[]")}, {P(regions.Select(r => r.DirectionId).ToArray(), "uuid[]")}, {P(regions.Select(r => r.Supervisor).ToArray(), "text[]")}, {P(regions.Select(r => r.Dealer).ToArray(), "text[]")}) as rg(id, dir, sup, dealer) on rg.id = br.region
                            left join unnest({P(directions.Select(d => d.Id).ToArray(), "uuid[]")}, {P(directions.Select(d => d.ManagerName).ToArray(), "text[]")}) as dr(id, manager) on dr.id = rg.dir
            """;
    }

    /// <summary>Строки оплат x: принятые, без удалённых, по дате создания; регион — по филиалу точки.</summary>
    private string PaymentRows(DateOnly from, DateOnly to, SalesRowScope scope)
    {
        var scopeSql = scope.Level switch
        {
            SalesRowScope.RegionLevel => $" and mk.\"BranchId\" = any({P(scope.BranchIds.ToArray(), "bigint[]")})",
            SalesRowScope.AgentLevel => $" and p.\"UserId\" = {P(scope.AgentId ?? -1, "bigint")}",
            _ => "",
        };
        var baseP = Base("p.\"CurrencyName\"");
        return $$"""
            f as (
                select p."CreatedDate" d, p."MarketId" market, p."UserId" agent, mk."BranchId" branch, mk."MarketTypeName" mtype,
                       case when {{baseP}} then p."Amount" else 0 end amount, case when {{baseP}} then 0 else p."Amount" end other_amount
                from linko."Payments" p
                left join linko."Markets" mk on mk."Id" = p."MarketId"
                where not p."IsDelete" and p."Status" = 'accepted' and p."CreatedDate" between {{P(from, "date")}} and {{P(to, "date")}}{{scopeSql}}
            ),
            x as (
                select f.*, {{Structure("f.branch")}}
                from f
                {{StructureJoins("f.branch")}}
            )
            """;
    }

    /// <summary>
    /// Строки визитов x — правила «Календаря визитов» (SalesAnalytics.RouteSplit): визиты с ТП и точкой; «план» — визит маршрута, кроме
    /// сегодняшних и будущих невыполненных; «не посещено» — визит маршрута прошедшего дня в статусе pending; «с заказом» — выполненный визит,
    /// у ТП которого в этой точке есть проданный заказ, введённый в день визита (без исключённых и пропускаемых филиалов). Регион — по филиалу точки.
    /// </summary>
    private string VisitRows(DateOnly from, DateOnly to, SalesRowScope scope)
    {
        var scopeSql = scope.Level switch
        {
            SalesRowScope.RegionLevel => $" and mk.\"BranchId\" = any({P(scope.BranchIds.ToArray(), "bigint[]")})",
            SalesRowScope.AgentLevel => $" and v.\"UserId\" = {P(scope.AgentId ?? -1, "bigint")}",
            _ => "",
        };
        var fromP = P(from, "date");
        var toP = P(to, "date");
        var todayP = P(today ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5)), "date");
        return $$"""
            vo as (
                select distinct o."AgentId" agent, o."MarketId" market, o."CreatedDate" d
                from linko."Orders" o
                where o."Status" = any({{P(options.SoldStatuses, "text[]")}}) and o."CreatedDate" between {{fromP}} and {{toP}}
                  and o."AgentId" is not null and o."MarketId" is not null and lower(coalesce(o."BranchName", '')) <> all({{P(options.NotSecondaryBranchesLower(), "text[]")}})
            ),
            f as (
                select v."Day" d, v."UserId" agent, v."MarketId" market, mk."BranchId" branch, mk."MarketTypeName" mtype,
                       v."Status" = 'done' done, v."IsInPlan" in_plan,
                       v."IsInPlan" and not (v."Status" = 'pending' and v."Day" >= {{todayP}}) planned,
                       v."IsInPlan" and v."Status" = 'pending' and v."Day" < {{todayP}} missed,
                       v."Status" = 'done' and vo.agent is not null with_order
                from linko."Visits" v
                left join linko."Markets" mk on mk."Id" = v."MarketId"
                left join vo on vo.agent = v."UserId" and vo.market = v."MarketId" and vo.d = v."Day"
                where v."Day" between {{fromP}} and {{toP}} and v."UserId" is not null and v."MarketId" is not null{{scopeSql}}
            ),
            x as (
                select f.*, {{Structure("f.branch")}}
                from f
                {{StructureJoins("f.branch")}}
            )
            """;
    }

    /// <summary>Агрегат показателя визитов.</summary>
    private static string VisitAgg(string metric) => metric switch
    {
        "visits_planned" => "count(*) filter (where x.planned)",
        "visits_done" => "count(*) filter (where x.done)",
        "visits_done_in_plan" => "count(*) filter (where x.done and x.in_plan)",
        "visits_off_plan" => "count(*) filter (where x.done and not x.in_plan)",
        "visits_missed" => "count(*) filter (where x.missed)",
        "visits_with_order" => "count(*) filter (where x.with_order)",
        "visited_outlets" => "count(distinct x.market) filter (where x.done)",
        _ => throw new AnalyticsQueryException("invalid", $"«{metric}» — не показатель визитов."),
    };

    /// <summary>Строки остатков x: текущий снимок linko.ProductBalances (единицы товара) по складам.</summary>
    private string StockRows() => $$"""
        x as (
            select b."ProductId" product, p."TypeId" type_id, coalesce(cm.grp, p."TypeId") grp, b."StockId" stock, b."Balance" units
            from linko."ProductBalances" b
            left join linko."Products" p on p."Id" = b."ProductId"
            left join unnest({{P(ctx.Categories.Mapping.Keys.ToArray(), "bigint[]")}}, {{P(ctx.Categories.Mapping.Values.ToArray(), "bigint[]")}}) as cm(type_id, grp) on cm.type_id = p."TypeId"
        )
        """;

    /// <summary>Условия фильтров над строками x.</summary>
    private string Where(string fact, IReadOnlyList<ResolvedFilter> filters)
    {
        var parts = new List<string>();
        foreach (var f in filters)
        {
            var expr = DimensionExpr(fact, f.Dimension, "day");
            var (cast, values) = f.Dimension switch
            {
                "region" or "direction" => ("uuid[]", (object)f.Values.Cast<Guid>().ToArray()),
                "manager" or "supervisor" or "dealer" or "market_type" or "sales_channel" or "source" => ("text[]", f.Values.Cast<string>().ToArray()),
                _ => ("bigint[]", f.Values.Cast<long>().ToArray()),
            };
            var compare = cast == "text[]" && f.Dimension is not ("sales_channel" or "source") ? $"lower({expr})" : expr;
            var any = f.Values.Count > 0 ? $"{compare} = any({P(values, cast)})" : "false";
            var condition = f.MatchNull ? $"({any} or {expr} is null)" : any;
            parts.Add(f.Exclude ? $"not coalesce({condition}, false)" : condition);
        }

        return parts.Count == 0 ? "" : " where " + string.Join(" and ", parts);
    }

    /// <summary>
    /// SQL одного факта: строки x → группировка по измерениям. АКБ (outlets) — двумя уровнями, как в «Продажах»: сначала группа × точка
    /// (чистый вес точки в группе), потом число точек с весом больше нуля без точек-каналов; заказы на втором уровне — сумма различных заказов
    /// точек (заказ принадлежит одной точке).
    /// </summary>
    public FactSql Build(string fact, IReadOnlyList<string> dimensions, IReadOnlyList<string> metrics, string grain, DateOnly from, DateOnly to,
        string segment, SalesRowScope scope, IReadOnlyList<ResolvedFilter> filters)
    {
        var dims = dimensions.Select((d, i) => (Code: d, Expr: DimensionExpr(fact, d, grain), Alias: $"d{i}")).ToList();
        var needMarkets = dimensions.Contains("market_type") || filters.Any(f => f.Dimension == "market_type");
        var rows = fact switch
        {
            SemanticCatalog.Sales => SalesRows(from, to, segment, scope, needMarkets),
            SemanticCatalog.Payments => PaymentRows(from, to, scope),
            SemanticCatalog.Visits => VisitRows(from, to, scope),
            SemanticCatalog.Stock => StockRows(),
            _ => throw new AnalyticsQueryException("invalid", $"Факт «{fact}» не читается SQL."),
        };
        var where = Where(fact, filters);
        var select = dims.Select(d => $"{d.Expr} {d.Alias}").ToList();
        var groupBy = dims.Count == 0 ? "" : " group by " + string.Join(", ", dims.Select((_, i) => (i + 1).ToString()));
        string sql;
        var tables = fact switch
        {
            SemanticCatalog.Sales => "linko.Orders, linko.OrderLines, linko.OrderReturns, linko.OrderReturnLines, linko.Products" + (needMarkets ? ", linko.Markets" : ""),
            SemanticCatalog.Payments => "linko.Payments, linko.Markets",
            SemanticCatalog.Visits => "linko.Visits, linko.Markets, linko.Orders",
            _ => "linko.ProductBalances, linko.Products",
        };

        if (fact == SemanticCatalog.Sales && metrics.Contains("outlets"))
        {
            var inner = metrics.Where(m => m != "outlets").Append("kg").Distinct().ToList();
            var innerAggs = inner.Select(m => $"{SalesAgg(m)} m_{m}");
            var innerSql = $"select {string.Join(", ", select.Append("x.market mkt").Concat(innerAggs).Append("bool_or(x.chan) chan"))} from x{where}" +
                           $" group by {string.Join(", ", Enumerable.Range(1, dims.Count + 1))}";
            var outer = dims.Select(d => d.Alias)
                .Concat(metrics.Select(m => m == "outlets" ? "count(*) filter (where y.mkt is not null and y.m_kg > 0 and not y.chan) m_outlets" : $"sum(y.m_{m}) m_{m}"));
            var outerGroup = dims.Count == 0 ? "" : " group by " + string.Join(", ", dims.Select(d => d.Alias));
            sql = $"with {rows},\ny as ({innerSql})\nselect {string.Join(", ", outer)} from y{outerGroup}";
        }
        else
        {
            var aggs = metrics.Select(m => fact switch
            {
                SemanticCatalog.Sales => $"{SalesAgg(m)} m_{m}",
                SemanticCatalog.Visits => $"{VisitAgg(m)} m_{m}",
                SemanticCatalog.Payments => m == "payments" ? "sum(x.amount) m_payments" : m == "other_currency_payments" ? "sum(x.other_amount) m_other_currency_payments" : throw new AnalyticsQueryException("invalid", $"«{m}» — не показатель оплат."),
                _ => m == "stock_units" ? "sum(x.units) m_stock_units" : throw new AnalyticsQueryException("invalid", $"«{m}» — не показатель остатков."),
            });
            sql = $"with {rows}\nselect {string.Join(", ", select.Concat(aggs))} from x{where}{groupBy}";
        }

        return new FactSql(sql, _params.ToList(), tables, dimensions, metrics);
    }
}
