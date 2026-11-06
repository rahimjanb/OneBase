import { redirect } from "next/navigation";
import { StockActions, StockView } from "@/components/procurement/StockView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { activeWarehouses, type Page, type RequestDepartment, type RequestFormData, type RequestNav, type StockRow, type StockSummary, type WarehousesView } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Склад · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/**
 * «Снабжение → Склад»: остатки и каталог — на складе, в резерве заявок, доступно, минимум; по выбранному складу или (?warehouse не задан)
 * по всем складам вместе. Видят кладовщик, снабжение и директор; операции (приход, выдача, списание, инвентаризация, перемещение) —
 * кладовщик, каталог — кладовщик и снабжение.
 */
export default async function StockPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRequests();
  const t = await getT();
  const sp = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/stock");
  if (!nav.seesStock) redirect("/requests");
  const warehouse = one(sp, "warehouse");
  const [data, summary, form, departments, stores] = await Promise.all([
    apiGet<Page<StockRow>>(
      `/api/procurement/stock${qs({ q: one(sp, "q"), category: one(sp, "category"), filter: one(sp, "filter"), page: one(sp, "page"), pageSize: 50, warehouse })}`,
      "/procurement/stock",
    ),
    apiGet<StockSummary>(`/api/procurement/stock/summary${qs({ warehouse })}`, "/procurement/stock"),
    apiGet<RequestFormData>("/api/procurement/form", "/procurement/stock"),
    apiGet<RequestDepartment[]>("/api/procurement/stock/departments", "/procurement/stock"),
    apiGet<WarehousesView>("/api/procurement/warehouses", "/procurement/stock"),
  ]);
  const catalog = nav.warehouse || nav.manages;
  const several = activeWarehouses(stores.warehouses).length > 1;
  return (
    <>
      <PageHeader
        title={t("Склад")}
        subtitle={several ? t("Склады снабжения: остатки по складу или по всем, резерв заявок, доступно и минимум") : t("Центральный склад снабжения: остатки, резерв заявок, доступно и минимум")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Склад") }]}
        actions={
          <StockActions
            warehouse={nav.warehouse}
            catalog={catalog}
            departments={departments}
            categories={form.categories}
            units={form.units}
            warehouses={stores.warehouses}
            warehouseId={warehouse ?? null}
          />
        }
      />
      <PageBody>
        <StockView
          data={data}
          summary={summary}
          categories={form.categories}
          units={form.units}
          departments={departments}
          warehouse={nav.warehouse}
          catalog={catalog}
          warehouses={stores.warehouses}
          quality={Boolean(nav.quality)}
          prices={nav.readsAll || nav.isDirector}
        />
      </PageBody>
    </>
  );
}
