import { notFound } from "next/navigation";
import { AkbMonthsCard } from "@/components/sales/AkbMonthsCard";
import { CollapsedSections, FlagPills, KpiTile, Section, levelTone } from "@/components/sales/bits";
import { CategoryPlanBars, IndicatorBars } from "@/components/sales/blocks";
import { AgentStoresTable, LaggingTable, ProductsTable } from "@/components/sales/assortment-tables";
import { CategoryCards } from "@/components/sales/categories";
import { ReportMonthExit } from "@/components/sales/MonthExit";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { NewMarketsTable, SameDaysTable, SilentMarketsTable } from "@/components/sales/tables";
import { getT } from "@/i18n/server";
import type { T } from "@/i18n/core";
import { apiGetOrNull } from "@/lib/server-api";
import { kg, money, monthGenitive, monthName, num, pct, plural } from "@/lib/sales/format";
import { apiQuery, periodQuery, queryWith, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { AgentFlag, AgentView } from "@/lib/sales/types";

const findingStyles = {
  Critical: "border-bad bg-bad-soft",
  Risk: "border-warn bg-warn-soft",
  Info: "border-line bg-muted",
} as const;

async function Findings({ flags }: { flags: AgentFlag[] }) {
  const t = await getT();
  return (
    <Section title={t("Что нашлось")}>
      {flags.length === 0 ? (
        <p className="rounded-lg bg-ok-soft px-4 py-3 text-sm text-ok">{t("Замечаний нет: показатели в пределах нормы региона.")}</p>
      ) : (
        <div className="space-y-2">
          {flags.map((f) => (
            <div key={f.kind} className={`rounded-lg border-l-4 px-4 py-3 ${findingStyles[f.severity]}`}>
              <div className="text-sm font-semibold text-ink">{t(f.title)}</div>
              <p className="mt-0.5 text-sm text-ink-2">{t(f.explanation)}</p>
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
function planNote(data: AgentView, month: string, t: T): string {
  if (data.revenuePlan != null) return t("в кг плана нет; в Linko — план по выручке {sum} сум", { sum: money(data.revenuePlan, t) });
  if (data.indicators.some((i) => i.planType === "product_sales_amount")) return t("в кг плана нет; в Linko план в штуках — см. «Планы Linko» ниже");
  if (data.indicators.length > 0) return t("в кг плана нет; в Linko только другие показатели — см. «Планы Linko» ниже");
  return t("в Linko плана на {month} нет", { month });
}

export default async function AgentPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SalesSearchParams>;
}) {
  const t = await getT();
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const path = `/sales/agents/${id}`;
  const data = await apiGetOrNull<AgentView>(`/api/sales/agents/${id}${apiQuery(sp)}`, path);
  if (!data) notFound();

  const q = periodQuery(sp);
  const { period } = data;
  const prevMonth = period.month === 1 ? 12 : period.month - 1;
  const cutoffDay = Number(period.previousCutoff.slice(8, 10));

  const crumbs = [{ label: t("Республика"), href: withQuery("/sales/republic", q) }];
  if (data.directionId) crumbs.push({ label: data.directionName ?? t("Направление"), href: withQuery(`/sales/directions/${data.directionId}`, q) });
  if (data.regionId) crumbs.push({ label: data.regionName ?? t("Регион"), href: withQuery(`/sales/regions/${data.regionId}`, q) });

  // Без региона сервер присылает подпись «Без региона» — она переводится; название региона — данные.
  const regionName = data.regionId ? data.regionName : data.regionName && t(data.regionName);
  const subtitle = [data.directionName, regionName, `ID ${data.agentId}`, data.isVacancy ? t("вакансия") : null].filter(Boolean).join(" · ");
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
        <KpiTile label={t("План ТП на месяц")} value={kg(data.planKg)} unit={data.planKg != null ? t("кг") : undefined} tone={levelTone(data.executionLevel)}>
          {data.planKg != null ? t("выполнено {pct}", { pct: pct(data.execution) }) : planNote(data, t(monthName(period.month, t)), t)}
        </KpiTile>
        <KpiTile label={t("Выручка за месяц")} value={money(data.revenue, t)} unit={t("сум")} tone={data.revenuePlan != null ? levelTone(data.revenueExecutionLevel) : "ink"}>
          {kg(data.factKg)} {t("кг")}
          {data.revenuePlan != null && <> · {t("{pct} плана ({sum})", { pct: pct(data.revenueExecution), sum: money(data.revenuePlan, t) })}</>}
        </KpiTile>
        <KpiTile label={t("Визиты")} value={num(data.visits)} title={t("{n} визитов, в день которых в этой точке введён заказ", { n: num(data.visitsWithOrder) })}>
          {t("{orders} за месяц", { orders: `${num(data.orders)} ${t(plural(data.orders, ["заказ", "заказа", "заказов"]))}` })}
        </KpiTile>
        <KpiTile label={t("Конверсия")} value={pct(data.conversion.value, 1)} title={t("Заказы, принятые в месяце ÷ выполненные визиты; бывает больше 100%")}>
          {t("медиана региона {value}", { value: pct(data.conversion.regionMedian) })}
        </KpiTile>
        <KpiTile label={t("Сум с визита")} value={money(data.sumPerVisit.value, t)}>
          {t("медиана региона {value}", { value: money(data.sumPerVisit.regionMedian, t) })}
        </KpiTile>
        <KpiTile label={t("АКБ — точек с отгрузкой")} value={num(data.akb)}>
          {data.revenuePerOutlet != null ? t("{sum} сум с точки", { sum: money(data.revenuePerOutlet, t) }) : t("покупок за месяц нет")}
        </KpiTile>
        <KpiTile label={t("Средний чек")} value={money(data.avgCheck.value, t)} unit={t("сум")}>
          {t("медиана региона {value}", { value: money(data.avgCheck.regionMedian, t) })}
        </KpiTile>
        <KpiTile label={t("Вес на точку")} value={kg(data.kgPerOutlet)} unit={data.kgPerOutlet != null ? t("кг") : undefined} />
        <KpiTile label={t("Категорий")} value={num(data.categories)} unit={data.planCategories ? t("из {n} в плане", { n: data.planCategories }) : undefined}>
          {t("цель — {n} на активную точку", { n: num(data.categoryTarget) })}
        </KpiTile>
        <KpiTile label={t("Темп к своему среднему")} value={pct(data.tempo)}>
          {t("с поправкой на {worked} из {days} дней", { worked: period.workedDays, days: period.daysInMonth })}
        </KpiTile>
      </div>

      <Findings flags={data.flags} />
      {data.assortment && (
        <CategoryCards cards={data.assortment.categories} scope={t("продажах этого ТП")} query={queryWith(q, { agent: data.agentId })} plans={data.categoryPlan} />
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
        <SameDaysTable rows={[data.sameDays]} nameLabel={t("ТП")} hint={t("{month}, 1–{day} числа", { month: t(monthGenitive(prevMonth, t)), day: cutoffDay })} query={q} />
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
