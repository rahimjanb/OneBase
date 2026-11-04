import { notFound, redirect } from "next/navigation";
import { OrderForm } from "@/components/procurement/OrderForm";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { Need, OrderDetail, SupplierRow } from "@/lib/procurement";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Изменить заказ · OneBase") };
}

/** Правка заказа: черновик — свободно; утверждённый и заказанный — существенное изменение уйдёт директору повторно. */
export default async function EditOrderPage({ params }: { params: Promise<{ id: string }> }) {
  const me = await requireRequests();
  const t = await getT();
  const { id } = await params;
  const [order, needs, suppliers, responsibles] = await Promise.all([
    apiGetOrNull<OrderDetail>(`/api/procurement/orders/${id}`, `/procurement/orders/${id}/edit`),
    apiGet<Need[]>("/api/procurement/orders/needs", `/procurement/orders/${id}/edit`),
    apiGet<SupplierRow[]>("/api/procurement/suppliers/options", `/procurement/orders/${id}/edit`),
    apiGet<{ id: string; name: string }[]>("/api/procurement/orders/responsibles", `/procurement/orders/${id}/edit`),
  ]);
  if (!order) notFound();
  if (!order.can.edit) redirect(`/procurement/orders/${id}`);
  return (
    <>
      <PageHeader
        title={t("Изменить заказ")}
        subtitle={`${order.number} · ${order.supplierName}`}
        back={`/procurement/orders/${id}`}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: order.number, href: `/procurement/orders/${id}` }, { label: t("Изменить") }]}
      />
      <PageBody>
        <OrderForm needs={needs} suppliers={suppliers} responsibles={responsibles} me={me?.id ?? ""} initial={order} />
      </PageBody>
    </>
  );
}
