import { redirect } from "next/navigation";
import { TenderForm } from "@/components/procurement/TenderForm";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RequestFormData, RequestNav, SupplierRow, ToBuyView, WarehousesView } from "@/lib/procurement";
import { apiGet, apiTry } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Новый тендер · OneBase") };
}

/** Новая запись журнала тендерных закупок — снабжение. */
export default async function NewTenderPage() {
  await requireRequests();
  const t = await getT();
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/tenders/new");
  if (!nav.manages) redirect("/procurement/tenders");
  const [form, suppliers, toBuy, stores] = await Promise.all([
    apiGet<RequestFormData>("/api/procurement/form", "/procurement/tenders/new"),
    apiGet<SupplierRow[]>("/api/procurement/suppliers/options", "/procurement/tenders/new"),
    apiTry<ToBuyView>("/api/procurement/to-buy"),
    apiTry<WarehousesView>("/api/procurement/warehouses"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Новый тендер")}
        subtitle={t("Запишите проведённый тендер: что закупали, кто победил, по какой цене и когда поставка")}
        back="/procurement/tenders"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Тендеры"), href: "/procurement/tenders" }, { label: t("Новый тендер") }]}
      />
      <PageBody>
        <TenderForm
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
