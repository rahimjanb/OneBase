import { notFound, redirect } from "next/navigation";
import { requireProduction } from "@/components/production/guard";
import { OrderForm } from "@/components/production/OrderForm";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ProductionOrderDetail, ProductionOrderForm } from "@/lib/production";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Изменить задание · OneBase") };
}

/** Правка черновика задания; выпущенное не правят. */
export default async function EditOrderPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const path = `/production/orders/${id}/edit`;
  await requireProduction(path);
  const order = await apiGetOrNull<ProductionOrderDetail>(`/api/production/orders/${id}`, path);
  if (!order) notFound();
  if (!order.can.edit) redirect(`/production/orders/${id}`);
  const t = await getT();
  const form = await apiGet<ProductionOrderForm>("/api/production/orders/form", path);
  return (
    <>
      <PageHeader
        title={t("Изменить задание")}
        subtitle={`${order.number} · ${order.recipeName}`}
        back={`/production/orders/${id}`}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Отделы"), href: "/departments" },
          { label: t("Производство"), href: "/production" },
          { label: t("Задания"), href: "/production/orders" },
          { label: order.number, href: `/production/orders/${id}` },
          { label: t("Изменить") },
        ]}
      />
      <PageBody>
        <OrderForm form={form} initial={order} />
      </PageBody>
    </>
  );
}
