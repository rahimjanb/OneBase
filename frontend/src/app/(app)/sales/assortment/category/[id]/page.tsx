import { notFound } from "next/navigation";
import { CategoryScreen } from "@/components/sales/assortment/CategoryScreen";
import { CategoryRegionsTable } from "@/components/sales/categories";
import { getT } from "@/i18n/server";
import { apiGet, apiGetOrNull, apiTry } from "@/lib/server-api";
import { monthName } from "@/lib/sales/format";
import { apiQuery, categoriesHref, scopeKeys, scopedQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { ProductPhotos } from "@/lib/sales/product-photos";
import type { CategoryView, SalesMonth } from "@/lib/sales/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Категория · Продажи") };
}

/**
 * Категория в охвате, из которого её открыли (макеты «Продажи · Ассортимент · Бамбук»): обложка, KPI, артикулы с фото — таблица или
 * карточки; у республики и направления — ещё «По регионам». Месяцы — для шторки выбора месяца в шапке телефона.
 */
export default async function CategoryPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SalesSearchParams>;
}) {
  const t = await getT();
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const path = `/sales/assortment/category/${id}`;
  // Фото товаров — отдельным запросом (не в кэше отчёта продаж): загруженное сотрудником видно сразу; не пришли — страница без фото.
  const [data, months, photos] = await Promise.all([
    apiGetOrNull<CategoryView>(`/api/sales/categories/${encodeURIComponent(id)}${apiQuery(sp, [...scopeKeys])}`, path),
    apiGet<SalesMonth[]>("/api/sales/months", path),
    apiTry<ProductPhotos>("/api/sales/product-photos"),
  ]);
  if (!data) notFound();

  const query = scopedQuery(sp);
  const prevMonth = data.period.month === 1 ? 12 : data.period.month - 1;
  const prevLabel = monthName(prevMonth, t).replace(/^./, (c) => c.toUpperCase());

  return (
    <CategoryScreen data={data} months={months} photos={photos} allHref={categoriesHref(data.scope, sp)} query={query} prevLabel={prevLabel}>
      {data.regions.length > 0 && <CategoryRegionsTable rows={data.regions} categoryId={data.categoryId} query={query} />}
    </CategoryScreen>
  );
}
