import { AgentsReport } from "@/components/sales/agents";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { monthLabel } from "@/lib/sales/format";
import { apiQuery, periodQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { AgentsView } from "@/lib/sales/agents";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Агенты · Продажи") };
}

const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);

/**
 * «Агенты»: все ТП месяца полосы раздела карточками — выполнение плана, прогноз, выручка, АКБ, визиты, страйк и замечания. Поиск по имени
 * или региону и фильтры — на странице (q, rm, region, status в адресе задают начальные значения).
 */
export default async function AgentsPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<AgentsView>(`/api/sales/agents${apiQuery(sp)}`, "/sales/agents");
  const { period } = data;

  return (
    <SalesFrame
      title={t("Агенты")}
      subtitle={t("Все ТП за {month}: выполнение плана, прогноз и главные цифры · нажмите на карточку — подробности", {
        month: monthLabel(period.year, period.month, t)
          .replace(/^\S+/, (w) => t(w))
          .toLowerCase(),
      })}
      sp={sp}
    >
      <AgentsReport
        key={`${period.year}-${period.month}`}
        data={data}
        query={periodQuery(sp)}
        initial={{ q: one(sp.q), rm: one(sp.rm), region: one(sp.region), status: one(sp.status) }}
      />
    </SalesFrame>
  );
}
