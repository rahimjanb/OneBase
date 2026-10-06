import { notFound } from "next/navigation";
import { KpiTile } from "@/components/sales/bits";
import { ProductBreakdownTable, SkuStatusChip } from "@/components/sales/categories";
import { TopChip } from "@/components/sales/outstock";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGetOrNull } from "@/lib/server-api";
import { kg, money, monthName, num, pct } from "@/lib/sales/format";
import { apiQuery, categoriesHref, scopeKeys, scopedQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { ProductView } from "@/lib/sales/types";

export const metadata = { title: "Артикул · Продажи" };

/** Артикул в охвате: факт, ТТ с товаром, цена за кг и где он идёт, а где нет. */
export default async function ProductPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SalesSearchParams>;
}) {
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const path = `/sales/assortment/product/${id}`;
  const data = await apiGetOrNull<ProductView>(`/api/sales/products/${encodeURIComponent(id)}${apiQuery(sp, [...scopeKeys])}`, path);
  if (!data) notFound();

  const query = scopedQuery(sp);
  const category = withQuery(`/sales/assortment/category/${encodeURIComponent(data.categoryId)}`, query);
  const prevMonth = data.period.month === 1 ? 12 : data.period.month - 1;
  const prevLabel = monthName(prevMonth).replace(/^./, (c) => c.toUpperCase());
  const code = data.code ? ` · Артикул ${data.code}` : "";

  return (
    <SalesFrame
      title={data.name}
      subtitle={`${data.category}${code} · ${data.scopeName}`}
      crumbs={[{ label: "Все категории", href: categoriesHref(data.scope, sp) }, { label: data.category, href: category }, { label: data.name }]}
      back={category}
      sp={sp}
      period={data.period}
    >
      <div className="mb-4 flex items-center gap-2 text-sm text-ink-2">
        Статус в охвате: <SkuStatusChip status={data.status} />
        {data.isTop && <TopChip title="Товар из списка ТОП" />}
      </div>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
        <KpiTile label="Факт, кг" value={kg(data.factKg)} unit="кг">
          {data.prevMonthKg ? `прошлый месяц целиком: ${kg(data.prevMonthKg)} кг` : "в прошлом месяце продаж не было"}
        </KpiTile>
        <KpiTile label="Выручка" value={money(data.revenue)} unit="сум" />
        <KpiTile label="ТТ с товаром" value={num(data.tt)} title="Точки с положительной строкой товара: кг или сумма больше нуля">
          {pct(data.distribution)} из {num(data.outlets)} ТТ с покупкой
        </KpiTile>
        <KpiTile label="Только он" value={num(data.solo)} title={`Точки, которые из всех SKU купили только этот; всего таких точек в охвате — ${num(data.mono)}`}>
          {data.solo > 0 ? `${pct(data.soloShare, 1)} его ТТ · всего таких точек ${num(data.mono)}` : `всего таких точек ${num(data.mono)}`}
        </KpiTile>
        <KpiTile label="Цена за кг" value={data.pricePerKg == null ? "—" : num(data.pricePerKg)} unit={data.pricePerKg == null ? undefined : "сум"}>
          выручка ÷ факт, кг
        </KpiTile>
      </div>
      <ProductBreakdownTable data={data} prevLabel={prevLabel} query={query} />
    </SalesFrame>
  );
}
