import { notFound } from "next/navigation";
import { KpiTile } from "@/components/sales/bits";
import { CategoryRegionsTable, SkuTable } from "@/components/sales/categories";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGetOrNull } from "@/lib/server-api";
import { kg, money, monthName, num, pct } from "@/lib/sales/format";
import { apiQuery, categoriesHref, scopeKeys, scopedQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { CategoryView } from "@/lib/sales/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Категория · Продажи") };
}

/** Категория в охвате, из которого её открыли: плитки, артикулы и — для республики и направления — регионы. */
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
  const data = await apiGetOrNull<CategoryView>(`/api/sales/categories/${encodeURIComponent(id)}${apiQuery(sp, [...scopeKeys])}`, path);
  if (!data) notFound();

  const query = scopedQuery(sp);
  const all = categoriesHref(data.scope, sp);
  const prevMonth = data.period.month === 1 ? 12 : data.period.month - 1;
  const prevLabel = t(monthName(prevMonth, t).replace(/^./, (c) => c.toUpperCase()));
  const card = data.card;
  // «Без категории» (id none) и охват «Республика» / «Экспорт и опт» — подписи сервера, переводятся; названия категорий и регионов — данные.
  const name = data.categoryId === "none" ? t(data.name) : data.name;
  const scopeName = t(data.scopeName);

  return (
    <SalesFrame
      title={name}
      subtitle={`${t("Категория")} · ${scopeName}`}
      crumbs={[{ label: t("Все категории"), href: all }, { label: name }]}
      back={all}
      sp={sp}
      period={data.period}
    >
      {card ? (
        <>
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <KpiTile label={t("Факт, кг")} value={kg(card.factKg)} unit={t("кг")}>
              {card.prevMonthKg ? t("прошлый месяц целиком: {kg} кг", { kg: kg(card.prevMonthKg) }) : t("в прошлом месяце продаж не было")}
            </KpiTile>
            <KpiTile label={t("Выручка")} value={money(card.revenue, t)} unit={t("сум")} />
            <KpiTile label={t("Продаётся SKU")} value={num(card.skuSold)} unit={`/ ${num(card.skuTotal)}`}>
              {card.lost > 0 ? <span className="font-semibold text-bad">{t("пропало {n}", { n: num(card.lost) })}</span> : t("ничего не пропало")}
            </KpiTile>
            <KpiTile label={t("ТТ")} value={num(card.akb)}>
              {t("дистрибуция {pct}", { pct: pct(card.distribution) })}
            </KpiTile>
          </div>
          <SkuTable card={card} prevLabel={prevLabel} query={query} mono={data.mono} />
        </>
      ) : (
        <p className="rounded-xl border border-line bg-surface px-4 py-6 text-center text-sm text-ink-3">
          {t("В охвате «{scope}» продаж категории «{name}» ни в этом, ни в прошлом месяце нет.", { scope: scopeName, name })}
        </p>
      )}
      {data.regions.length > 0 && <CategoryRegionsTable rows={data.regions} categoryId={data.categoryId} query={query} />}
    </SalesFrame>
  );
}
