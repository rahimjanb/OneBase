import { CashView } from "@/components/finance/FinanceViews";
import { FinancePage } from "@/components/finance/FinancePage";
import { financeContext, requireFinance } from "@/components/finance/guard";
import { getT } from "@/i18n/server";
import type { FinanceCash, SearchParams } from "@/lib/finance";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Деньги · Финансы · OneBase") };
}

/** «Финансы → Деньги»: факт из 1С:Бухгалтерии. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireFinance();
  const sp = await searchParams;
  const ctx = await financeContext(sp, "/finance/cash");
  const data = await apiGet<FinanceCash>(`/api/finance/cash?${ctx.query}`, "/finance/cash");
  return (
    <FinancePage title="Деньги" subtitle="Поступления и выплаты по статьям, остатки на счетах и в кассе" ctx={ctx}>
      <CashView data={data} />
    </FinancePage>
  );
}
