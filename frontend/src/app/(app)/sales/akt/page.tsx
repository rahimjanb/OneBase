import { AktReport } from "@/components/sales/akt";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import type { SalesSearchParams } from "@/lib/sales/query";
import type { AktView } from "@/lib/sales/settlements";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Акт сверки · Продажи") };
}

/**
 * «Акт сверки»: взаиморасчёты с контрагентами за месяц. Свой выбор месяца (year, month — месяцы от начала реестра оплат, по умолчанию
 * текущий). Отгрузки — Linko, оплаты, расходы и остатки — реестры Google Sheets; всё считает сервер.
 */
export default async function AktPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const one = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v);
  const month = one(sp.year) && one(sp.month) ? `?month=${one(sp.year)}-${Number(one(sp.month))}` : "";
  const data = await apiGet<AktView>(`/api/sales/akt${month}`, "/sales/akt");

  return (
    <SalesFrame title={t("Акт сверки")} subtitle={t("Взаиморасчёты с дилерами и клиентами за месяц: остаток, отгрузка, оплаты, расходы завода")} sp={sp}>
      <AktReport key={data.month.key} data={data} />
    </SalesFrame>
  );
}
