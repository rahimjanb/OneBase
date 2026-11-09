import { notFound } from "next/navigation";
import { OrderDetailView } from "@/components/procurement/OrderDetailView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { getT } from "@/i18n/server";
import type { ArrivalPage, ExpectedDelivery } from "@/lib/arrivals";
import type { OrderDetail } from "@/lib/procurement";
import { apiGetOrNull, apiTry } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Заказ поставщику · OneBase") };
}

/** Карточка заказа поставщику: доступ решает API (снабжение, директор, кладовщик); у покупки «Куплено — привезёт поставщик» — поставка и машины. */
export default async function OrderPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const { id } = await params;
  const order = await apiGetOrNull<OrderDetail>(`/api/procurement/orders/${id}`, `/procurement/orders/${id}`);
  if (!order) notFound();
  const waits = order.status === "Ordered" || order.status === "PartiallyDelivered";
  const [expected, arrivals] = await Promise.all([
    waits ? apiTry<ExpectedDelivery[]>(`/api/procurement/arrivals/expected?source=${id}`) : null,
    apiTry<ArrivalPage>(`/api/procurement/arrivals?status=all&source=${id}&pageSize=50`),
  ]);
  return <OrderDetailView order={order} expected={expected?.find((d) => d.kind === "order") ?? null} arrivals={arrivals?.items ?? []} />;
}
