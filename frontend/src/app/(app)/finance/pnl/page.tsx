import { PnlView } from "@/components/finance/FinanceViews";
import { FinancePage } from "@/components/finance/FinancePage";
import { financeContext, requireFinance } from "@/components/finance/guard";
import { getT } from "@/i18n/server";
import type { FinancePnl, SearchParams } from "@/lib/finance";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Доходы и расходы · Финансы · OneBase") };
}

/** «Финансы → Доходы и расходы»: факт из 1С:Бухгалтерии. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireFinance();
  const sp = await searchParams;
  const ctx = await financeContext(sp, "/finance/pnl");
  const data = await apiGet<FinancePnl>(`/api/finance/pnl?${ctx.query}`, "/finance/pnl");
  return (
    <FinancePage title="Доходы и расходы" subtitle="Отчёт о финансовых результатах по месяцам, расходы по счетам и подразделениям" ctx={ctx}>
      <PnlView data={data} />
    </FinancePage>
  );
}
