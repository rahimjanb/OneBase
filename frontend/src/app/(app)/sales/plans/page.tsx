import { KpiTile, levelTone } from "@/components/sales/bits";
import { PeoplePlansTable, RegionPlansTable } from "@/components/sales/plans";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { kg, money, monthLabel, num, pct } from "@/lib/sales/format";
import { apiQuery, periodQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { PlansView } from "@/lib/sales/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Планы · Продажи") };
}

export default async function PlansPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<PlansView>(`/api/sales/plans${apiQuery(sp)}`, "/sales/plans");
  const q = periodQuery(sp);
  const { period } = data;

  return (
    <SalesFrame
      title={t("Планы")}
      subtitle={t("Все планы из Linko за {month} — обновляются автоматически после каждого пересчёта", {
        month: monthLabel(period.year, period.month, t).replace(/^\S+/, (w) => t(w)).toLowerCase(),
      })}
      sp={sp}
    >
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile label={t("План ТП, кг")} value={kg(data.weightPlan)} tone={levelTone(data.weightExecutionLevel)}>
          {t("факт по Linko {kg} кг", { kg: kg(data.weightFact) })}
          {data.weightExecution != null && t(" · {pct} плана", { pct: pct(data.weightExecution) })}
        </KpiTile>
        <KpiTile label={t("План по выручке")} value={money(data.revenuePlan, t)} unit={data.revenuePlan != null ? t("сум") : undefined}>
          {data.revenuePlan != null ? t("факт по Linko {sum}", { sum: money(data.revenueFact, t) }) : t("денежных планов нет")}
        </KpiTile>
        <KpiTile label={t("ТП с планом")} value={num(data.agentsWithPlan)}>
          {t("показателей всего {n}", { n: num(data.indicators) })}
        </KpiTile>
        <KpiTile label={t("Планы супервайзеров")} value={num(data.teamPlans.length)}>
          {t("планы команд — в суммы не входят")}
        </KpiTile>
      </div>

      {/* Ключ по месяцу: смена месяца сбрасывает сортировки и раскрытые строки (DOC-filters §12). */}
      <RegionPlansTable key={`r${period.year}-${period.month}`} rows={data.regions} query={q} />
      <PeoplePlansTable
        key={`a${period.year}-${period.month}`}
        rows={data.agents}
        query={q}
        title={t("Планы торговых представителей")}
        hint={t("нажмите на строку — все показатели агента")}
        note={t(
          "План ТП в кг — сумма весовых показателей Linko; план по выручке — показатели «сумма продаж»; АКБ — план активных клиентов. Факт и выполнение здесь — по расчёту Linko. Во «Вторичке» план региона и республики — план РОП или «Завод»; эти планы там — план ТП, а для месяца без планов регионов — и план подразделений.",
        )}
      />
      {data.teamPlans.length > 0 && (
        <PeoplePlansTable
          key={`t${period.year}-${period.month}`}
          rows={data.teamPlans}
          query={q}
          team
          title={t("Планы супервайзеров")}
          hint={t("планы их команд")}
          note={t(
            "Эти планы не складываются с планами агентов, иначе план задвоился бы. Список должностей, которые считаются планами команд, — в настройке сервера Sales:StaffPlanExcludeJobs.",
          )}
        />
      )}
    </SalesFrame>
  );
}
