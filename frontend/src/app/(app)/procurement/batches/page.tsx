import { redirect } from "next/navigation";
import { BatchesView } from "@/components/procurement/BatchesView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { BatchRow, Page, RequestNav, WarehousesView } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Партии · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/**
 * «Снабжение → Партии»: партии позиций, которые ведутся по партиям, — по сроку годности (ближайший первым): остаток склада или всех, номер
 * производителя, поставщик, даты, сколько дней осталось. Фильтры — склад, состояние (годные, истекает, просрочено, закончившиеся), поиск.
 */
export default async function BatchesPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRequests();
  const t = await getT();
  const sp = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/batches");
  if (!nav.seesStock) redirect("/requests");
  const state = one(sp, "state");
  const [data, stores] = await Promise.all([
    apiGet<Page<BatchRow>>(
      `/api/procurement/batches${qs({
        q: one(sp, "q"),
        warehouse: one(sp, "warehouse"),
        item: one(sp, "item"),
        state: state === "empty" ? undefined : state,
        empty: state === "empty" ? true : undefined,
        page: one(sp, "page"),
        pageSize: 50,
      })}`,
      "/procurement/batches",
    ),
    apiGet<WarehousesView>("/api/procurement/warehouses", "/procurement/batches"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Партии")}
        subtitle={t("Сырьё по партиям: срок годности, качество, остаток по складам — выдаётся только допущенное и не просроченное")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Партии") }]}
      />
      <PageBody>
        <BatchesView data={data} warehouses={stores.warehouses} />
      </PageBody>
    </>
  );
}
