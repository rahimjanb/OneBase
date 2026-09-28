import { notFound } from "next/navigation";
import { FlagPills, KpiTile, Section } from "@/components/sales/bits";
import { CategoryPlanBars, IndicatorBars, PlanBadge } from "@/components/sales/blocks";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { NewMarketsTable, SameDaysTable, SilentMarketsTable } from "@/components/sales/tables";
import { apiGetOrNull } from "@/lib/server-api";
import { kg, money, monthGenitive, num, pct } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
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

  return (
    <SalesFrame
      title={data.name}
      subtitle={subtitle}
      crumbs={[...crumbs, { label: data.name }]}
      back={data.regionId ? withQuery(`/sales/regions/${data.regionId}`, q) : withQuery("/sales/republic", q)}
      sp={sp}
      period={period}
      returnTo={path}
    >
      <div className="mb-4 flex justify-end">
        <FlagPills flags={data.flags.filter((f) => f.severity !== "Info")} empty={null} />
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <KpiTile label="План ТП на месяц" value={kg(data.planKg)} unit={data.planKg != null ? "кг" : undefined}>
          {data.planKg != null ? `выполнено ${pct(data.execution)}` : "плана нет"}
        </KpiTile>
        <KpiTile
          label="Выручка за месяц"
          value={money(data.revenue)}
          unit="сум"
          badge={data.revenuePlan != null ? <PlanBadge share={data.revenueExecution} /> : undefined}
        >
          {kg(data.factKg)} кг
          {data.revenuePlan != null && <> · план {money(data.revenuePlan)}</>}
        </KpiTile>
        <KpiTile label="Визиты" value={num(data.visits)}>
          {num(data.visitsWithOrder)} с заказом
        </KpiTile>
        <KpiTile label="Конверсия" value={pct(data.conversion.value, 1)}>
          медиана региона {pct(data.conversion.regionMedian)}
        </KpiTile>
        <KpiTile label="Сум с визита" value={money(data.sumPerVisit.value)}>
          медиана региона {money(data.sumPerVisit.regionMedian)}
        </KpiTile>
        <KpiTile label="Средний чек" value={money(data.avgCheck.value)} unit="сум">
          медиана региона {money(data.avgCheck.regionMedian)}
        </KpiTile>
        <KpiTile label="Категорий" value={num(data.categories)} unit={data.planCategories ? `из ${data.planCategories} в плане` : undefined}>
          цель — {num(data.categoryTarget)} на активную точку
        </KpiTile>
        <KpiTile label="Темп к своему среднему" value={pct(data.tempo)}>
          с поправкой на {period.workedDays} из {period.daysInMonth} дней
        </KpiTile>
      </div>

      <Findings flags={data.flags} />
      <IndicatorBars rows={data.indicators} />
      <CategoryPlanBars rows={data.categoryPlan} />
      <SameDaysTable rows={[data.sameDays]} nameLabel="ТП" hint={`${monthGenitive(prevMonth)}, 1–${cutoffDay} числа`} query={q} />
      <SilentMarketsTable
        rows={data.silent}
        base={data.silentBase}
        revenue={data.silentPrevRevenue}
        workedDays={period.workedDays}
        daysInMonth={period.daysInMonth}
      />
      <NewMarketsTable rows={data.newMarkets} />
    </SalesFrame>
  );
}
