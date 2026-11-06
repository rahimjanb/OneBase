import { notFound, redirect } from "next/navigation";
import { TenderForm } from "@/components/procurement/TenderForm";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RequestFormData, SupplierRow, TenderDetail, ToBuyView, WarehousesView } from "@/lib/procurement";
import { apiGet, apiGetOrNull, apiTry } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Изменить тендер · OneBase") };
}

/** Правка записи журнала тендерных закупок — снабжение; отменённую не правят. */
export default async function EditTenderPage({ params }: { params: Promise<{ id: string }> }) {
  await requireRequests();
  const { id } = await params;
  const path = `/procurement/tenders/${id}/edit`;
  const tender = await apiGetOrNull<TenderDetail>(`/api/procurement/tenders/${id}`, path);
  if (!tender) notFound();
  if (!tender.can.edit) redirect(`/procurement/tenders/${id}`);
  const t = await getT();
  const [form, suppliers, toBuy, stores] = await Promise.all([
    apiGet<RequestFormData>("/api/procurement/form", path),
    apiGet<SupplierRow[]>("/api/procurement/suppliers/options", path),
    apiTry<ToBuyView>("/api/procurement/to-buy"),
    apiTry<WarehousesView>("/api/procurement/warehouses"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Изменить тендер")}
        subtitle={`${tender.number} · ${tender.title}`}
        back={`/procurement/tenders/${id}`}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Отделы"), href: "/departments" },
          { label: t("Снабжение"), href: "/procurement" },
          { label: t("Тендеры"), href: "/procurement/tenders" },
          { label: tender.number, href: `/procurement/tenders/${id}` },
          { label: t("Изменить") },
        ]}
      />
      <PageBody>
        <TenderForm
          initial={tender}
          categories={form.categories}
          units={form.units}
          suppliers={suppliers.map((s) => ({ id: s.id, name: s.name }))}
          requests={(toBuy?.requests ?? []).map((r) => ({ id: r.requestId, number: r.number, title: r.title }))}
          warehouses={stores?.warehouses ?? []}
        />
      </PageBody>
    </>
  );
}
