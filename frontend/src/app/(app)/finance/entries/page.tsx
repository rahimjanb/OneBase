import { EntriesView } from "@/components/finance/FinanceViews";
import { FinancePage } from "@/components/finance/FinancePage";
import { financeContext, requireFinance } from "@/components/finance/guard";
import { getT } from "@/i18n/server";
import { one, type FinanceEntries, type SearchParams } from "@/lib/finance";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Проводки · Финансы · OneBase") };
}

/** Проводки 1С с отбором по счёту, контрагенту или документу — переход к исходным данным из отчётов и ответов AI. */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireFinance();
  const sp = await searchParams;
  const filters = { account: one(sp, "account"), counterparty: one(sp, "counterparty"), registrar: one(sp, "registrar") };
  const ctx = await financeContext(sp, "/finance/entries");
  const extra = new URLSearchParams();
  for (const [k, v] of Object.entries({ ...filters, page: one(sp, "page") ?? "1", pageSize: "50" })) if (v) extra.set(k, v);
  const data = await apiGet<FinanceEntries>(`/api/finance/entries?${ctx.query}&${extra.toString()}`, "/finance/entries");
  return (
    <FinancePage title="Проводки" subtitle="Бухгалтерские проводки 1С с аналитикой (субконто), валютой и количеством" ctx={ctx}>
      <EntriesView data={data} filters={filters} />
    </FinancePage>
  );
}
