import { TrialBalanceView } from "@/components/finance/FinanceViews";
import { FinancePage } from "@/components/finance/FinancePage";
import { financeContext, requireFinance } from "@/components/finance/guard";
import { getT } from "@/i18n/server";
import type { FinanceTrialBalance, SearchParams } from "@/lib/finance";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Счета · Финансы · OneBase") };
}

/** «Финансы → Счета»: факт из 1С:Бухгалтерии. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireFinance();
  const sp = await searchParams;
  const ctx = await financeContext(sp, "/finance/accounts");
  const data = await apiGet<FinanceTrialBalance>(`/api/finance/accounts?${ctx.query}`, "/finance/accounts");
  return (
    <FinancePage title="Счета" subtitle="Оборотно-сальдовая ведомость: сальдо и обороты по счетам 1С" ctx={ctx}>
      <TrialBalanceView data={data} />
    </FinancePage>
  );
}
