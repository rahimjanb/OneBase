import { notFound } from "next/navigation";
import { KpiTile } from "@/components/sales/bits";
import { CategoryRegionsTable, SkuTable } from "@/components/sales/categories";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGetOrNull } from "@/lib/server-api";
import { kg, money, monthName, num, pct } from "@/lib/sales/format";
import { apiQuery, categoriesHref, scopeKeys, scopedQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { CategoryView } from "@/lib/sales/types";

export const metadata = { title: "Категория · Продажи" };

/** Категория в охвате, из которого её открыли: плитки, артикулы и — для республики и направления — регионы. */
export default async function CategoryPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SalesSearchParams>;
}) {
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const path = `/sales/assortment/category/${id}`;
  const data = await apiGetOrNull<CategoryView>(`/api/sales/categories/${encodeURIComponent(id)}${apiQuery(sp, [...scopeKeys])}`, path);
  if (!data) notFound();

  const query = scopedQuery(sp);
  const all = categoriesHref(data.scope, sp);
  const prevMonth = data.period.month === 1 ? 12 : data.period.month - 1;
  const prevLabel = monthName(prevMonth).replace(/^./, (c) => c.toUpperCase());
  const card = data.card;

  return (
    <SalesFrame
      title={data.name}
      subtitle={`Категория · ${data.scopeName}`}
      crumbs={[{ label: "Все категории", href: all }, { label: data.name }]}
      back={all}
      sp={sp}
    >
      {card ? (
        <>
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <KpiTile label="Факт, кг" value={kg(card.factKg)} unit="кг">
              {card.prevMonthKg ? `прошлый месяц целиком: ${kg(card.prevMonthKg)} кг` : "в прошлом месяце продаж не было"}
            </KpiTile>
            <KpiTile label="Выручка" value={money(card.revenue)} unit="сум" />
            <KpiTile label="Продаётся SKU" value={num(card.skuSold)} unit={`/ ${num(card.skuTotal)}`}>
              {card.lost > 0 ? <span className="font-semibold text-bad">пропало {num(card.lost)}</span> : "ничего не пропало"}
            </KpiTile>
            <KpiTile label="ТТ" value={num(card.akb)}>
              дистрибуция {pct(card.distribution)}
            </KpiTile>
          </div>
          <SkuTable card={card} prevLabel={prevLabel} query={query} />
        </>
      ) : (
        <p className="rounded-xl border border-line bg-surface px-4 py-6 text-center text-sm text-ink-3">
          В охвате «{data.scopeName}» продаж категории «{data.name}» ни в этом, ни в прошлом месяце нет.
        </p>
      )}
      {data.regions.length > 0 && <CategoryRegionsTable rows={data.regions} categoryId={data.categoryId} query={query} />}
    </SalesFrame>
  );
}
