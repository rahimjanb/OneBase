import { SettlementsView } from "@/components/finance/FinanceViews";
import { FinancePage } from "@/components/finance/FinancePage";
import { financeContext, requireFinance } from "@/components/finance/guard";
import { getT } from "@/i18n/server";
import { one, type FinanceSettlements, type SearchParams } from "@/lib/finance";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Расчёты · Финансы · OneBase") };
}

/** «Финансы → Расчёты»: дебиторская и кредиторская задолженность по контрагентам, сроки и просрочка (оценка FIFO). */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireFinance();
  const sp = await searchParams;
  const kind = one(sp, "kind") === "payables" ? "payables" : "receivables";
  const ctx = await financeContext(sp, "/finance/settlements");
  const data = await apiGet<FinanceSettlements>(`/api/finance/settlements?${ctx.query}&kind=${kind}&top=300`, "/finance/settlements");
  return (
    <FinancePage title="Расчёты" subtitle="Кто кому должен на дату: долги по контрагентам, сроки оплаты и просрочка" ctx={ctx} single>
      <SettlementsView data={data} />
    </FinancePage>
  );
}
