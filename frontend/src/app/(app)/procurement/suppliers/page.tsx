import { redirect } from "next/navigation";
import { SupplierActions, SuppliersView } from "@/components/procurement/SuppliersView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { Page, RequestFormData, RequestNav, SupplierRow } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Поставщики · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** «Снабжение → Поставщики»: реестр с рейтингом и аналитикой (заказы, сумма, просрочки), добавление и импорт из Excel. */
export default async function SuppliersPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRequests();
  const t = await getT();
  const sp = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/suppliers");
  if (!nav.readsAll && !nav.isDirector && !nav.warehouse) redirect("/requests");
  const [data, form] = await Promise.all([
    apiGet<Page<SupplierRow>>(
      `/api/procurement/suppliers${qs({ q: one(sp, "q"), category: one(sp, "category"), active: one(sp, "active"), sort: one(sp, "sort"), desc: one(sp, "desc"), pageSize: 200 })}`,
      "/procurement/suppliers",
    ),
    apiGet<RequestFormData>("/api/procurement/form", "/procurement/suppliers"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Поставщики")}
        subtitle={t("Реестр поставщиков: реквизиты, категории, рейтинг, заказы и просрочки")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Поставщики") }]}
        actions={nav.manages ? <SupplierActions categories={form.categories} /> : undefined}
      />
      <PageBody>
        <SuppliersView data={data} categories={form.categories} />
      </PageBody>
    </>
  );
}
