import { OperationsView } from "@/components/finance/FinanceViews";
import { FinancePage } from "@/components/finance/FinancePage";
import { financeContext, requireFinance } from "@/components/finance/guard";
import { getT } from "@/i18n/server";
import type { FinanceOperations, SearchParams } from "@/lib/finance";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Продажи и склад · Финансы · OneBase") };
}

/** «Финансы → Продажи и склад»: факт из 1С:Бухгалтерии. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireFinance();
  const sp = await searchParams;
  const ctx = await financeContext(sp, "/finance/operations");
  const data = await apiGet<FinanceOperations>(`/api/finance/operations?${ctx.query}`, "/finance/operations");
  return (
    <FinancePage title="Продажи и склад" subtitle="Продажи, закупки, себестоимость, запасы, выпуск продукции и валюты" ctx={ctx}>
      <OperationsView data={data} />
    </FinancePage>
  );
}
