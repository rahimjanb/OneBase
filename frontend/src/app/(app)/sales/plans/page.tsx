import { KpiTile, planTone } from "@/components/sales/bits";
import { PeoplePlansTable, RegionPlansTable } from "@/components/sales/plans";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { kg, money, monthLabel, num, pct } from "@/lib/sales/format";
import { apiQuery, periodQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { PlansView } from "@/lib/sales/types";

export const metadata = { title: "Планы · Продажи" };

export default async function PlansPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const data = await apiGet<PlansView>(`/api/sales/plans${apiQuery(sp)}`, "/sales/plans");
  const q = periodQuery(sp);
  const { period } = data;

  return (
    <SalesFrame
      title="Планы"
      subtitle={`Все планы из Linko за ${monthLabel(period.year, period.month).toLowerCase()} — обновляются автоматически после каждого пересчёта`}
      sp={sp}
    >
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile label="План ТП, кг" value={kg(data.weightPlan)} tone={data.weightPlan != null ? planTone(data.weightExecution) : "muted"}>
          факт по Linko {kg(data.weightFact)} кг{data.weightExecution != null && ` · ${pct(data.weightExecution)} плана`}
        </KpiTile>
        <KpiTile label="План по выручке" value={money(data.revenuePlan)} unit={data.revenuePlan != null ? "сум" : undefined}>
          {data.revenuePlan != null ? `факт по Linko ${money(data.revenueFact)}` : "денежных планов нет"}
        </KpiTile>
        <KpiTile label="ТП с планом" value={num(data.agentsWithPlan)}>
          показателей всего {num(data.indicators)}
        </KpiTile>
        <KpiTile label="Планы супервайзеров" value={num(data.teamPlans.length)}>
          планы команд — в суммы не входят
        </KpiTile>
      </div>

      <RegionPlansTable rows={data.regions} query={q} />
      <PeoplePlansTable
        rows={data.agents}
        query={q}
        title="Планы торговых представителей"
        hint="нажмите на строку — все показатели агента"
        note="План ТП в кг — сумма весовых показателей Linko; план по выручке — показатели «сумма продаж»; АКБ — план активных клиентов. Факт и выполнение здесь — по расчёту Linko."
      />
      {data.teamPlans.length > 0 && (
        <PeoplePlansTable
          rows={data.teamPlans}
          query={q}
          team
          title="Планы супервайзеров"
          hint="планы их команд"
          note="Эти планы не складываются с планами агентов, иначе план задвоился бы. Список должностей, которые считаются планами команд, — в настройке сервера Sales:StaffPlanExcludeJobs."
        />
      )}
    </SalesFrame>
  );
}
