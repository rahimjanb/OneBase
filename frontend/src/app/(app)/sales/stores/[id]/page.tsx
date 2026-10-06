import { notFound } from "next/navigation";
import { KpiTile, Section } from "@/components/sales/bits";
import { StoreProductsTable } from "@/components/sales/assortment-tables";
import { ReportMonthExit } from "@/components/sales/MonthExit";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGetOrNull } from "@/lib/server-api";
import { kg, money, num, ordersLabel, pct } from "@/lib/sales/format";
import { apiQuery, param, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { StoreView } from "@/lib/sales/types";

export default async function StorePage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<SalesSearchParams> }) {
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const path = `/sales/stores/${id}`;
  const data = await apiGetOrNull<StoreView>(`/api/sales/stores/${id}${apiQuery(sp, ["agent"])}`, path);
  if (!data) notFound();

  const q = periodQuery(sp);
  const agent = param(sp, "agent");
  const crumbs = [
    { label: "Республика", href: withQuery("/sales/republic", q) },
    ...(data.regionId ? [{ label: data.regionName ?? "Регион", href: withQuery(`/sales/regions/${data.regionId}`, q) }] : []),
    ...(agent && data.agentName ? [{ label: data.agentName, href: withQuery(`/sales/agents/${agent}`, q) }] : []),
    { label: data.name },
  ];
  const max = Math.max(1, ...data.categoryRows.map((c) => c.revenue));

  return (
    <SalesFrame
      title={data.name}
      subtitle={[data.regionName, data.agentName ? `ТП: ${data.agentName}` : data.agents.length ? `ТП: ${data.agents.join(", ")}` : null, `№ ${data.marketId}`]
        .filter(Boolean)
        .join(" · ")}
      crumbs={crumbs}
      back={crumbs[crumbs.length - 2]?.href ?? withQuery("/sales", q)}
      period={data.period}
      sp={sp}
    >
      {/* Смена месяца уходит к региону магазина (DOC-filters §12). */}
      <ReportMonthExit to={data.regionId ? `/sales/regions/${data.regionId}` : "/sales/republic"} />
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile label="Выручка за месяц" value={money(data.revenue)} unit="сум">
          {kg(data.factKg)} кг · {ordersLabel(data.orders)}
        </KpiTile>
        <KpiTile label="Категорий" value={num(data.categories)} />
        <KpiTile label="Позиций" value={num(data.positions)}>
          разных артикулов в заказах
        </KpiTile>
        <KpiTile label={data.agentName ? "Доля в объёме ТП" : "Прошлый месяц"} value={data.agentName ? pct(data.shareOfAgent, 1) : money(data.prevMonthRevenue)}>
          прошлый месяц: {kg(data.prevMonthKg)} кг
        </KpiTile>
      </div>

      <Section title="Факт по категориям" hint="выручка за месяц">
        {data.categoryRows.length === 0 ? (
          <p className="py-4 text-center text-sm text-ink-3">В этом месяце точка ничего не купила</p>
        ) : (
          <div className="space-y-1.5">
            {data.categoryRows.map((c) => (
              <div key={c.name} className="grid grid-cols-[minmax(0,160px)_1fr_120px] items-center gap-3 text-xs" title={`${kg(c.kg)} кг`}>
                <span className="truncate text-ink-2">{c.name}</span>
                <div className="h-2.5 rounded bg-muted">
                  <div className="h-full rounded bg-accent" style={{ width: `${Math.max(0, (c.revenue / max) * 100)}%` }} />
                </div>
                <span className="text-right tabular-nums text-ink">
                  {money(c.revenue)} · {pct(c.share)}
                </span>
              </div>
            ))}
          </div>
        )}
      </Section>
      <StoreProductsTable rows={data.products} />
    </SalesFrame>
  );
}
