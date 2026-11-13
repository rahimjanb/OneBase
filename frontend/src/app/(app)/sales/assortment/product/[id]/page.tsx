import { notFound } from "next/navigation";
import { ProductPhotoPanel } from "@/components/sales/assortment/ProductPhoto";
import { KpiTile } from "@/components/sales/bits";
import { ProductBreakdownTable, SkuStatusChip } from "@/components/sales/categories";
import { TopChip } from "@/components/sales/outstock";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGetOrNull, apiTry } from "@/lib/server-api";
import { kg, money, monthName, num, pct } from "@/lib/sales/format";
import { skuName } from "@/lib/sales/assortment-view";
import { apiQuery, categoriesHref, scopeKeys, scopedQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { ProductPhotos } from "@/lib/sales/product-photos";
import type { ProductView } from "@/lib/sales/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Артикул · Продажи") };
}

/** Артикул в охвате: фото, факт, ТТ с товаром, цена за кг и где он идёт, а где нет. Название — без кода Linko в начале (код — в подзаголовке). */
export default async function ProductPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SalesSearchParams>;
}) {
  const t = await getT();
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const path = `/sales/assortment/product/${id}`;
  const [data, photos] = await Promise.all([
    apiGetOrNull<ProductView>(`/api/sales/products/${encodeURIComponent(id)}${apiQuery(sp, [...scopeKeys])}`, path),
    apiTry<ProductPhotos>(`/api/sales/product-photos?ids=${encodeURIComponent(id)}`),
  ]);
  if (!data) notFound();

  const query = scopedQuery(sp);
  const category = withQuery(`/sales/assortment/category/${encodeURIComponent(data.categoryId)}`, query);
  const prevMonth = data.period.month === 1 ? 12 : data.period.month - 1;
  const prevLabel = t(monthName(prevMonth, t).replace(/^./, (c) => c.toUpperCase()));
  const code = data.code ? ` · ${t("Артикул")} ${data.code}` : "";
  // «Без категории» (id none) и охват «Республика» / «Экспорт и опт» — подписи сервера, переводятся; остальное — данные.
  const categoryName = data.categoryId === "none" ? t(data.category) : data.category;
  const name = skuName(data);

  return (
    <SalesFrame
      title={name}
      subtitle={`${categoryName}${code} · ${t(data.scopeName)}`}
      crumbs={[{ label: t("Все категории"), href: categoriesHref(data.scope, sp) }, { label: categoryName, href: category }, { label: name }]}
      back={category}
      sp={sp}
      period={data.period}
    >
      <ProductPhotoPanel photos={photos} productId={data.productId} code={data.code} name={name}>
        {t("Статус в охвате:")} <SkuStatusChip status={data.status} />
        {data.isTop && <TopChip title={t("Товар из списка ТОП")} />}
      </ProductPhotoPanel>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
        <KpiTile label={t("Факт, кг")} value={kg(data.factKg)} unit={t("кг")}>
          {data.prevMonthKg ? t("прошлый месяц целиком: {kg} кг", { kg: kg(data.prevMonthKg) }) : t("в прошлом месяце продаж не было")}
        </KpiTile>
        <KpiTile label={t("Выручка")} value={money(data.revenue, t)} unit={t("сум")} />
        <KpiTile label={t("ТТ с товаром")} value={num(data.tt)} title={t("Точки с положительной строкой товара: кг или сумма больше нуля")}>
          {t("{pct} из {n} ТТ с покупкой", { pct: pct(data.distribution), n: num(data.outlets) })}
        </KpiTile>
        <KpiTile
          label={t("Только он")}
          value={num(data.solo)}
          title={t("Точки, которые из всех SKU купили только этот; всего таких точек в охвате — {n}", { n: num(data.mono) })}
        >
          {data.solo > 0
            ? t("{pct} его ТТ · всего таких точек {n}", { pct: pct(data.soloShare, 1), n: num(data.mono) })
            : t("всего таких точек {n}", { n: num(data.mono) })}
        </KpiTile>
        <KpiTile label={t("Цена за кг")} value={data.pricePerKg == null ? "—" : num(data.pricePerKg)} unit={data.pricePerKg == null ? undefined : t("сум")}>
          {t("выручка ÷ факт, кг")}
        </KpiTile>
      </div>
      <ProductBreakdownTable data={data} prevLabel={prevLabel} query={query} />
    </SalesFrame>
  );
}
