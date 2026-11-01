import { notFound } from "next/navigation";
import { OffersView } from "@/components/procurement/OffersView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { Comparison, SupplierRow } from "@/lib/procurement";
import { apiGet, apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Предложения поставщиков · OneBase") };
}

/** Сравнение коммерческих предложений по заявке (снабжение и директор). */
export default async function OffersPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const t = await getT();
  const { id } = await params;
  const [data, suppliers] = await Promise.all([
    apiGetOrNull<Comparison>(`/api/procurement/requests/${id}/offers`, `/requests/${id}/offers`),
    apiGet<SupplierRow[]>("/api/procurement/suppliers/options", `/requests/${id}/offers`),
  ]);
  if (!data) notFound();
  return (
    <>
      <PageHeader
        title={t("Предложения поставщиков")}
        subtitle={`${data.number} · ${data.title}`}
        back={`/requests/${id}`}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Заявки"), href: "/requests" }, { label: data.number, href: `/requests/${id}` }, { label: t("Предложения") }]}
      />
      <PageBody>
        <OffersView data={data} suppliers={suppliers} />
      </PageBody>
    </>
  );
}
