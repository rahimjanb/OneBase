import { SalesFrame } from "@/components/sales/SalesFrame";
import { SystemReport } from "@/components/sales/system";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { monthLabel } from "@/lib/sales/format";
import { apiQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { SystemView } from "@/lib/sales/system";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Система · Продажи") };
}

/** «Система»: как работает конвейер продаж за месяц полосы раздела — маршрут, качество точки, деньги и запас дилера; всё считает сервер. */
export default async function SystemPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<SystemView>(`/api/sales/system${apiQuery(sp)}`, "/sales/system");
  const { period } = data;

  return (
    <SalesFrame
      title={t("Система")}
      subtitle={t("Не сколько продали, а как работает конвейер за {month}: маршрут, качество точки, деньги и запас дилера", {
        month: monthLabel(period.year, period.month, t)
          .replace(/^\S+/, (w) => t(w))
          .toLowerCase(),
      })}
      sp={sp}
    >
      <SystemReport key={`${period.year}-${period.month}`} data={data} />
    </SalesFrame>
  );
}
