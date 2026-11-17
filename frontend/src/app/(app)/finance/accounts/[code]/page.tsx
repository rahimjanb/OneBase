import { notFound } from "next/navigation";
import { AccountView } from "@/components/finance/FinanceViews";
import { FinancePage } from "@/components/finance/FinancePage";
import { financeContext, requireFinance } from "@/components/finance/guard";
import { getT } from "@/i18n/server";
import type { FinanceAccount, SearchParams } from "@/lib/finance";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata({ params }: { params: Promise<{ code: string }> }) {
  const t = await getT();
  return { title: t("Счёт {code} · Финансы · OneBase", { code: (await params).code }) };
}

/** Карточка счёта: сальдо и обороты, обороты по месяцам, сальдо по аналитике (контрагенты, номенклатура, склады) и переход к проводкам. */
export default async function Page({ params, searchParams }: { params: Promise<{ code: string }>; searchParams: Promise<SearchParams> }) {
  await requireFinance();
  const { code } = await params;
  const sp = await searchParams;
  const path = `/finance/accounts/${code}`;
  const ctx = await financeContext(sp, path);
  const data = await apiGetOrNull<FinanceAccount>(`/api/finance/accounts/${encodeURIComponent(code)}?${ctx.query}`, path);
  if (!data) notFound();
  return (
    <FinancePage title={`${data.account} · ${data.name}`} crumbs={[{ label: "Счета", href: "/finance/accounts" }, { label: data.account }]} ctx={ctx}>
      <AccountView data={data} />
    </FinancePage>
  );
}
