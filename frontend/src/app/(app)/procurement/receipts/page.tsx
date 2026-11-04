import { redirect } from "next/navigation";
import { ReceiptsView } from "@/components/procurement/ReceiptsView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ExpectedRow, Page, ReceiptRow, RequestNav } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Приёмка · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/**
 * «Снабжение → Приёмка»: заказы, которые ждут поставки, и журнал приёмок. Принимает товар кладовщик; снабжение и директор видят приёмки,
 * брак, недостачу и претензии.
 */
export default async function ReceiptsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRequests();
  const t = await getT();
  const sp = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/receipts");
  if (!nav.warehouse && !nav.readsAll && !nav.isDirector) redirect("/procurement");
  const [expected, data] = await Promise.all([
    apiGet<ExpectedRow[]>("/api/procurement/receipts/expected", "/procurement/receipts"),
    apiGet<Page<ReceiptRow>>(`/api/procurement/receipts${qs({ status: one(sp, "status"), q: one(sp, "q"), page: one(sp, "page"), pageSize: 25 })}`, "/procurement/receipts"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Приёмка")}
        subtitle={t("Поставки по заказам: фактическое количество, брак, недостача и накладные — склад пополняется только после подтверждения")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Приёмка") }]}
      />
      <PageBody>
        <ReceiptsView expected={expected} data={data} warehouse={nav.warehouse} />
      </PageBody>
    </>
  );
}
