import Link from "next/link";
import { redirect } from "next/navigation";
import { Plus } from "lucide-react";
import { OrdersView } from "@/components/procurement/OrdersView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { OrderRow, OrderSummary, Page, RequestNav, SupplierRow } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Заказы поставщикам · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** «Снабжение → Заказы»: заказы поставщикам с контролем просрочек, ожидаемых и частичных поставок, фильтр по поставщику и ответственному. */
export default async function OrdersPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRequests();
  const t = await getT();
  const sp = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/orders");
  if (!nav.readsAll && !nav.isDirector && !nav.warehouse) redirect("/requests");
  const [data, summary, suppliers, responsibles] = await Promise.all([
    apiGet<Page<OrderRow>>(
      `/api/procurement/orders${qs({ status: one(sp, "status"), supplier: one(sp, "supplier"), responsible: one(sp, "responsible"), q: one(sp, "q"), sort: one(sp, "sort"), desc: one(sp, "desc"), page: one(sp, "page"), pageSize: 25 })}`,
      "/procurement/orders",
    ),
    apiGet<OrderSummary>("/api/procurement/orders/summary", "/procurement/orders"),
    apiGet<SupplierRow[]>("/api/procurement/suppliers/options", "/procurement/orders"),
    apiGet<{ id: string; name: string }[]>("/api/procurement/orders/responsibles", "/procurement/orders"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Заказы поставщикам")}
        subtitle={t("Закупки по утверждённым заявкам: сроки поставки, просрочки, частичные поставки")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Заказы") }]}
        actions={
          nav.manages ? (
            <Link href="/procurement/orders/new" className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong">
              <Plus className="size-4" aria-hidden /> {t("Новый заказ")}
            </Link>
          ) : undefined
        }
      />
      <PageBody>
        <OrdersView data={data} summary={summary} suppliers={suppliers.map((s) => ({ id: s.id, name: s.name }))} responsibles={responsibles} />
      </PageBody>
    </>
  );
}
