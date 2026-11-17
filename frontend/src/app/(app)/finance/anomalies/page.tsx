import { AnomaliesView } from "@/components/finance/FinanceViews";
import { FinancePage } from "@/components/finance/FinancePage";
import { financeContext, requireFinance } from "@/components/finance/guard";
import { getT } from "@/i18n/server";
import type { FinanceAnomalies, SearchParams } from "@/lib/finance";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Отклонения · Финансы · OneBase") };
}

/** «Финансы → Отклонения»: факт из 1С:Бухгалтерии. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireFinance();
  const sp = await searchParams;
  const ctx = await financeContext(sp, "/finance/anomalies");
  const data = await apiGet<FinanceAnomalies>(`/api/finance/anomalies?${ctx.query}`, "/finance/anomalies");
  return (
    <FinancePage title="Отклонения" subtitle="Рост расходов, крупные проводки, исправления закрытых месяцев, двойные платежи" ctx={ctx}>
      <AnomaliesView data={data} />
    </FinancePage>
  );
}
