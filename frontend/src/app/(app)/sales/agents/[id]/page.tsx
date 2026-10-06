import { notFound } from "next/navigation";
import { AkbMonthsCard } from "@/components/sales/AkbMonthsCard";
import { CollapsedSections, FlagPills, KpiTile, Section, levelTone } from "@/components/sales/bits";
import { CategoryPlanBars, IndicatorBars } from "@/components/sales/blocks";
import { AgentStoresTable, LaggingTable, ProductsTable } from "@/components/sales/assortment-tables";
import { CategoryCards } from "@/components/sales/categories";
import { ReportMonthExit } from "@/components/sales/MonthExit";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { NewMarketsTable, SameDaysTable, SilentMarketsTable } from "@/components/sales/tables";
import { apiGetOrNull } from "@/lib/server-api";
import { kg, money, monthGenitive, monthName, num, ordersLabel, pct } from "@/lib/sales/format";
import { apiQuery, periodQuery, queryWith, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { AgentFlag, AgentView } from "@/lib/sales/types";

const findingStyles = {
  Critical: "border-bad bg-bad-soft",
  Risk: "border-warn bg-warn-soft",
  Info: "border-line bg-muted",
} as const;

function Findings({ flags }: { flags: AgentFlag[] }) {
  return (
    <Section title="Что нашлось">
      {flags.length === 0 ? (
        <p className="rounded-lg bg-ok-soft px-4 py-3 text-sm text-ok">Замечаний нет: показатели в пределах нормы региона.</p>
      ) : (
        <div className="space-y-2">
          {flags.map((f) => (
            <div key={f.kind} className={`rounded-lg border-l-4 px-4 py-3 ${findingStyles[f.severity]}`}>
              <div className="text-sm font-semibold text-ink">{f.title}</div>
              <p className="mt-0.5 text-sm text-ink-2">{f.explanation}</p>
            </div>
          ))}
        </div>
      )}
    </Section>
  );
}

/**
 * Плана в кг нет — что есть в Linko (API планов, staff_balance): план по выручке, план в штуках или ничего.
 * Штуки в кг не пересчитываются: в показателе нет веса единицы.
 */
function planNote(data: AgentView, month: string): string {
  if (data.revenuePlan != null) return `в кг плана нет; в Linko — план по выручке ${money(data.revenuePlan)} сум`;
  if (data.indicators.some((i) => i.planType === "product_sales_amount")) return "в кг плана нет; в Linko план в штуках — см. «Планы Linko» ниже";
  if (data.indicators.length > 0) return "в кг плана нет; в Linko только другие показатели — см. «Планы Linko» ниже";
  return `в Linko плана на ${month} нет`;
}

export default async function AgentPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SalesSearchParams>;
}) {
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const path = `/sales/agents/${id}`;
  const data = await apiGetOrNull<AgentView>(`/api/sales/agents/${id}${apiQuery(sp)}`, path);
  if (!data) notFound();

  const q = periodQuery(sp);
  const { period } = data;
  const prevMonth = period.month === 1 ? 12 : period.month - 1;
  const cutoffDay = Number(period.previousCutoff.slice(8, 10));

  const crumbs = [{ label: "Республика", href: withQuery("/sales/republic", q) }];
  if (data.directionId) crumbs.push({ label: data.directionName ?? "Направление", href: withQuery(`/sales/directions/${data.directionId}`, q) });
  if (data.regionId) crumbs.push({ label: data.regionName ?? "Регион", href: withQuery(`/sales/regions/${data.regionId}`, q) });

  const subtitle = [data.directionName, data.regionName, `ID ${data.agentId}`, data.isVacancy ? "вакансия" : null].filter(Boolean).join(" · ");
  const regionHref = data.regionId ? `/sales/regions/${data.regionId}` : "/sales/republic";

  return (
    <SalesFrame
      title={data.name}
      subtitle={subtitle}
      crumbs={[...crumbs, { label: data.name }]}
      back={withQuery(regionHref, q)}
      sp={sp}
      period={period}
    >
      {/* Смена месяца уходит к региону ТП (DOC-filters §12). */}
      <ReportMonthExit to={regionHref} />
      <div className="mb-4 flex justify-end">
        <FlagPills flags={data.flags.filter((f) => f.severity !== "Info")} empty={null} />
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4 min-[87.5rem]:grid-cols-5">
        <KpiTile label="План ТП на месяц" value={kg(data.planKg)} unit={data.planKg != null ? "кг" : undefined} tone={levelTone(data.executionLevel)}>
          {data.planKg != null ? `выполнено ${pct(data.execution)}` : planNote(data, monthName(period.month))}
        </KpiTile>
        <KpiTile label="Выручка за месяц" value={money(data.revenue)} unit="сум" tone={data.revenuePlan != null ? levelTone(data.revenueExecutionLevel) : "ink"}>
          {kg(data.factKg)} кг
          {data.revenuePlan != null && <> · {pct(data.revenueExecution)} плана ({money(data.revenuePlan)})</>}
        </KpiTile>
        <KpiTile label="Визиты" value={num(data.visits)} title={`${num(data.visitsWithOrder)} визитов, в день которых в этой точке введён заказ`}>
          {ordersLabel(data.orders)} за месяц
        </KpiTile>
        <KpiTile label="Конверсия" value={pct(data.conversion.value, 1)} title="Заказы, принятые в месяце ÷ выполненные визиты; бывает больше 100%">
          медиана региона {pct(data.conversion.regionMedian)}
        </KpiTile>
        <KpiTile label="Сум с визита" value={money(data.sumPerVisit.value)}>
          медиана региона {money(data.sumPerVisit.regionMedian)}
        </KpiTile>
        <KpiTile label="АКБ — точек с отгрузкой" value={num(data.akb)}>
          {data.revenuePerOutlet != null ? `${money(data.revenuePerOutlet)} сум с точки` : "покупок за месяц нет"}
        </KpiTile>
        <KpiTile label="Средний чек" value={money(data.avgCheck.value)} unit="сум">
          медиана региона {money(data.avgCheck.regionMedian)}
        </KpiTile>
        <KpiTile label="Вес на точку" value={kg(data.kgPerOutlet)} unit={data.kgPerOutlet != null ? "кг" : undefined} />
        <KpiTile label="Категорий" value={num(data.categories)} unit={data.planCategories ? `из ${data.planCategories} в плане` : undefined}>
          цель — {num(data.categoryTarget)} на активную точку
        </KpiTile>
        <KpiTile label="Темп к своему среднему" value={pct(data.tempo)}>
          с поправкой на {period.workedDays} из {period.daysInMonth} дней
        </KpiTile>
      </div>

      <Findings flags={data.flags} />
      {data.assortment && (
        <CategoryCards cards={data.assortment.categories} scope="продажах этого ТП" query={queryWith(q, { agent: data.agentId })} />
      )}

      {/* Ключ по месяцу: смена месяца сбрасывает раскрытые секции, сортировки и раскрытые строки (DOC-filters §12). */}
      <CollapsedSections key={`${period.year}-${period.month}`}>
        {data.akbMonths && <AkbMonthsCard data={data.akbMonths} />}
        <IndicatorBars rows={data.indicators} />
        <CategoryPlanBars rows={data.categoryPlan} />
        {data.assortment && (
          <>
            <AgentStoresTable rows={data.assortment.stores} agentId={data.agentId} query={q} />
            <ProductsTable rows={data.assortment.products} outsideReport={data.assortment.productsOutsideReport} />
            <LaggingTable rows={data.assortment.lagging} />
          </>
        )}
        <SameDaysTable rows={[data.sameDays]} nameLabel="ТП" hint={`${monthGenitive(prevMonth)}, 1–${cutoffDay} числа`} query={q} />
        <SilentMarketsTable
          rows={data.silent}
          base={data.silentBase}
          revenue={data.silentPrevRevenue}
          workedDays={period.workedDays}
          daysInMonth={period.daysInMonth}
        />
        <NewMarketsTable rows={data.newMarkets} />
      </CollapsedSections>
    </SalesFrame>
  );
}
