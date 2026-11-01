import { notFound } from "next/navigation";
import { SupplierCardView } from "@/components/procurement/SupplierCardView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { getT } from "@/i18n/server";
import type { ProcDocument, RequestFormData, SupplierCard } from "@/lib/procurement";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Поставщик · OneBase") };
}

/** Карточка поставщика: доступ решает API (снабжение, директор, кладовщик). */
export default async function SupplierPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const { id } = await params;
  const [supplier, documents, form] = await Promise.all([
    apiGetOrNull<SupplierCard>(`/api/procurement/suppliers/${id}`, `/procurement/suppliers/${id}`),
    apiGet<ProcDocument[]>(`/api/procurement/documents?owner=supplier&id=${id}`, `/procurement/suppliers/${id}`).catch(() => []),
    apiGet<RequestFormData>("/api/procurement/form", `/procurement/suppliers/${id}`),
  ]);
  if (!supplier) notFound();
  return <SupplierCardView supplier={supplier} documents={documents} categories={form.categories} />;
}
