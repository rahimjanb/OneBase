import { SalesFrame } from "@/components/sales/SalesFrame";
import { KpiReport } from "@/components/sales/kpi";
import { getT } from "@/i18n/server";
import { apiGet, apiSession } from "@/lib/server-api";
import { monthLabel } from "@/lib/sales/format";
import { apiQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { KpiView } from "@/lib/sales/types";
import { has, type Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("KPI · Продажи") };
}

/** «KPI»: премии и выполнение KPI-показателей Linko по людям и регионам за месяц полосы раздела. */
export default async function KpiPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const [data, me] = await Promise.all([apiGet<KpiView>(`/api/sales/kpi${apiQuery(sp)}`, "/sales/kpi"), apiSession<Me>("/api/auth/me")]);
  const { period } = data;

  return (
    <SalesFrame
      title={t("KPI")}
      subtitle={t("Премии и выполнение KPI из Linko за {month} · {tp} ТП и {svr} СВР", {
        month: monthLabel(period.year, period.month, t).replace(/^\S+/, (w) => t(w)).toLowerCase(),
        tp: data.tpCount,
        svr: data.svrCount,
      })}
      sp={sp}
    >
      <KpiReport key={`${period.year}-${period.month}`} data={data} canAssign={has(me, "sales.manage")} />
    </SalesFrame>
  );
}
