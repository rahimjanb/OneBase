import { DsoReport } from "@/components/sales/dso";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import type { SalesSearchParams } from "@/lib/sales/query";
import type { DsoView } from "@/lib/sales/settlements";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("DSO · Продажи") };
}

/** «DSO»: долги дилеров по неоплаченным отгрузкам, просрочка по норме и прогноз оплат — на дату данных; всё считает сервер. */
export default async function DsoPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<DsoView>("/api/sales/dso", "/sales/dso");

  return (
    <SalesFrame title={t("DSO")} subtitle={t("Долги дилеров, просрочка и когда ждать оплату")} sp={sp}>
      <DsoReport data={data} />
    </SalesFrame>
  );
}
