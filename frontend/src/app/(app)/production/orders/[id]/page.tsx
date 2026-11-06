import { notFound } from "next/navigation";
import { requireProduction } from "@/components/production/guard";
import { OrderDetailView } from "@/components/production/OrderDetailView";
import { getT } from "@/i18n/server";
import type { ProductionOrderDetail } from "@/lib/production";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Задание на выпуск · OneBase") };
}

/** Карточка задания на выпуск: потребность, выдачи и возвраты по партиям, сверка нормы после закрытия. */
export default async function OrderPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const path = `/production/orders/${id}`;
  await requireProduction(path);
  const order = await apiGetOrNull<ProductionOrderDetail>(`/api/production/orders/${id}`, path);
  if (!order) notFound();
  return <OrderDetailView order={order} />;
}
