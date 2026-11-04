import { redirect } from "next/navigation";
import { OrderForm } from "@/components/procurement/OrderForm";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { Comparison, Need, RequestNav, SupplierRow } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Новый заказ · OneBase") };
}

/** Новый заказ поставщику: из утверждённых заявок (request — одной заявки), из предложения (offer) — с его ценами и условиями. */
export default async function NewOrderPage({ searchParams }: { searchParams: Promise<{ request?: string; offer?: string }> }) {
  const me = await requireRequests();
  const t = await getT();
  const { request, offer } = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/orders/new");
  if (!nav.manages) redirect("/procurement/orders");
  const [needs, suppliers, responsibles, comparison] = await Promise.all([
    apiGet<Need[]>(`/api/procurement/orders/needs${qs({ request })}`, "/procurement/orders/new"),
    apiGet<SupplierRow[]>("/api/procurement/suppliers/options", "/procurement/orders/new"),
    apiGet<{ id: string; name: string }[]>("/api/procurement/orders/responsibles", "/procurement/orders/new"),
    request && offer ? apiGet<Comparison>(`/api/procurement/requests/${request}/offers`, "/procurement/orders/new") : Promise.resolve(null),
  ]);
  return (
    <>
      <PageHeader
        title={t("Новый заказ поставщику")}
        subtitle={t("Только по утверждённым директором заявкам: что, сколько и почём заказать")}
        back={request ? `/requests/${request}` : "/procurement/orders"}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Заказы"), href: "/procurement/orders" }, { label: t("Новый заказ") }]}
      />
      <PageBody>
        <OrderForm needs={needs} suppliers={suppliers} responsibles={responsibles} me={me?.id ?? ""} offer={comparison?.offers.find((o) => o.id === offer) ?? null} />
      </PageBody>
    </>
  );
}
