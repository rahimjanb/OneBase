import { redirect } from "next/navigation";
import { MovementsView } from "@/components/procurement/MovementsView";
import { SheetImports } from "@/components/procurement/SheetImports";
import { StockImport } from "@/components/procurement/StockImport";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { MovementRow, Page, RequestNav, WarehousesView } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Движения склада · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** Журнал движений склада: приход, выдача, списание, инвентаризация, перемещение — склад, кто, когда, документ, остаток до и после. */
export default async function MovementsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRequests();
  const t = await getT();
  const sp = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/movements");
  if (!nav.seesStock) redirect("/requests");
  const [data, stores] = await Promise.all([
    apiGet<Page<MovementRow>>(
      `/api/procurement/movements${qs({ kind: one(sp, "kind"), q: one(sp, "q"), from: one(sp, "from"), to: one(sp, "to"), page: one(sp, "page"), pageSize: 50, warehouse: one(sp, "warehouse") })}`,
      "/procurement/movements",
    ),
    apiGet<WarehousesView>("/api/procurement/warehouses", "/procurement/movements"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Движения")}
        subtitle={t("Журнал склада: приход, выдача, списание, инвентаризация и перемещения — не правится, исправление — новым документом")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Движения") }]}
        actions={
          nav.warehouse ? (
            <span className="flex flex-wrap items-center gap-2">
              <SheetImports warehouses={stores.warehouses} />
              <StockImport warehouses={stores.warehouses} />
            </span>
          ) : undefined
        }
      />
      <PageBody>
        <MovementsView data={data} warehouses={stores.warehouses} canReverse={nav.warehouse} />

      </PageBody>
    </>
  );
}
