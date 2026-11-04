import { notFound, redirect } from "next/navigation";
import { ReceiptFormView } from "@/components/procurement/ReceiptFormView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ReceiptForm, RequestFormData, RequestNav } from "@/lib/procurement";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Новая приёмка · OneBase") };
}

/** Новая приёмка по заказу (order): только кладовщик и только заказ, который ждёт поставки. */
export default async function NewReceiptPage({ searchParams }: { searchParams: Promise<{ order?: string }> }) {
  await requireRequests();
  const t = await getT();
  const { order } = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/receipts/new");
  if (!nav.warehouse || !order) redirect("/procurement/receipts");
  const [form, data] = await Promise.all([
    apiGetOrNull<ReceiptForm>(`/api/procurement/receipts/form?order=${encodeURIComponent(order)}`, "/procurement/receipts/new"),
    apiGet<RequestFormData>("/api/procurement/form", "/procurement/receipts/new"),
  ]);
  if (!form) notFound();
  if (!form.canReceive) redirect(`/procurement/orders/${form.orderId}`);
  return (
    <>
      <PageHeader
        title={t("Приёмка по заказу {n}", { n: form.orderNumber })}
        subtitle={form.supplierName}
        back="/procurement/receipts"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Отделы"), href: "/departments" },
          { label: t("Снабжение"), href: "/procurement" },
          { label: t("Приёмка"), href: "/procurement/receipts" },
          { label: form.orderNumber },
        ]}
      />
      <PageBody>
        <ReceiptFormView form={form} categories={data.categories} />
      </PageBody>
    </>
  );
}
