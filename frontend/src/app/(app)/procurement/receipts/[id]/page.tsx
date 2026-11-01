import { notFound } from "next/navigation";
import { ReceiptDetailView } from "@/components/procurement/ReceiptDetailView";
import { ReceiptFormView } from "@/components/procurement/ReceiptFormView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ReceiptDetail, RequestFormData } from "@/lib/procurement";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Приёмка · OneBase") };
}

/** Приёмка: черновик у кладовщика — форма правки; подтверждённая (или чужой взгляд на черновик) — карточка. Доступ решает API. */
export default async function ReceiptPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const t = await getT();
  const { id } = await params;
  const receipt = await apiGetOrNull<ReceiptDetail>(`/api/procurement/receipts/${id}`, `/procurement/receipts/${id}`);
  if (!receipt) notFound();
  if (!receipt.can.edit || !receipt.form) return <ReceiptDetailView receipt={receipt} />;
  const data = await apiGet<RequestFormData>("/api/procurement/form", `/procurement/receipts/${id}`);
  return (
    <>
      <PageHeader
        title={t("Черновик приёмки {n}", { n: receipt.number })}
        subtitle={receipt.supplierName}
        back="/procurement/receipts"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Снабжение"), href: "/procurement" },
          { label: t("Приёмка"), href: "/procurement/receipts" },
          { label: receipt.number },
        ]}
      />
      <PageBody>
        <ReceiptFormView form={receipt.form} categories={data.categories} draft={receipt} />
      </PageBody>
    </>
  );
}
