import { notFound } from "next/navigation";
import { OrderDetailView } from "@/components/procurement/OrderDetailView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { getT } from "@/i18n/server";
import type { OrderDetail } from "@/lib/procurement";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Заказ поставщику · OneBase") };
}

/** Карточка заказа поставщику: доступ решает API (снабжение, директор, кладовщик). */
export default async function OrderPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const { id } = await params;
  const order = await apiGetOrNull<OrderDetail>(`/api/procurement/orders/${id}`, `/procurement/orders/${id}`);
  if (!order) notFound();
  return <OrderDetailView order={order} />;
}
