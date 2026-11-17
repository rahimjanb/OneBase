import { OverviewView } from "@/components/finance/FinanceViews";
import { FinancePage } from "@/components/finance/FinancePage";
import { financeContext, requireFinance } from "@/components/finance/guard";
import { getT } from "@/i18n/server";
import type { FinanceOverview, SearchParams } from "@/lib/finance";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Финансы · OneBase") };
}

/** «Финансы»: обзор по данным 1С — выручка, прибыль, деньги, долги, запасы, 12 месяцев и где выросли расходы. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireFinance();
  const sp = await searchParams;
  const ctx = await financeContext(sp, "/finance");
  const data = await apiGet<FinanceOverview>(`/api/finance/overview?${ctx.query}`, "/finance");
  return (
    <FinancePage title="Финансы" subtitle="Факт из 1С:Бухгалтерии — без пересчёта и без управленческих данных OneBase" ctx={ctx}>
      <OverviewView data={data} />
    </FinancePage>
  );
}
