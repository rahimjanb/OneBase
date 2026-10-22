import { AkbChart } from "@/components/sales/AkbChart";
import { AssortmentMatrix, AssortmentRegionsTable, ProductsTable } from "@/components/sales/assortment-tables";
import { CollapsedSections, KpiTile } from "@/components/sales/bits";
import { kg, money, num } from "@/lib/sales/format";
import { DataQualityNotes } from "@/components/sales/blocks";
import { CategoryCards } from "@/components/sales/categories";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { ScopeSelect } from "@/components/sales/ScopeSelect";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { apiQuery, param, periodQuery, queryWith, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { AssortmentView, GroupView } from "@/lib/sales/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Ассортимент · Продажи") };
}

export default async function AssortmentPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const [data, republic] = await Promise.all([
    apiGet<AssortmentView>(`/api/sales/assortment${apiQuery(sp, ["region", "direction"])}`, "/sales/assortment"),
    apiGet<GroupView>(`/api/sales/republic${apiQuery(sp)}`, "/sales/assortment"),
  ]);
  const q = periodQuery(sp);

  const options = [
    { value: "", label: t("Вся республика") },
    ...republic.cards.filter((c) => c.kind === "direction").map((c) => ({ value: `direction:${c.id}`, label: t("{name} — целиком", { name: c.name }), group: t("Региональные менеджеры") })),
    ...republic.regions
      .filter((r) => r.factKg !== 0 || r.akb > 0)
      .map((r) => ({ value: `region:${r.id}`, label: r.name, group: t("Регионы") })),
  ];
  // Охват — название региона или РМ (данные) либо подпись сервера («Республика», «Направление»): подписи переводятся по словарю.
  const scopeName = t(data.scopeName);
  const scopeLabel = data.scopeName === "Республика" ? t("республике") : scopeName;
  const s = data.summary;

  return (
    <SalesFrame
      title={t("Ассортимент")}
      subtitle={`${t("Что и где продаётся: категории, артикулы, дистрибуция")} · ${scopeName}`}
      crumbs={[{ label: t("Ассортимент") }]}
      sp={sp}
      period={data.period}
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <ScopeSelect options={options} />
      </div>
      <div className="mt-4 grid grid-cols-2 gap-3 xl:grid-cols-4">
        <KpiTile label={t("Факт, кг")} value={kg(s.factKg)} unit={t("кг")}>
          {s.prevMonthKg ? t("прошлый месяц целиком: {kg} кг", { kg: kg(s.prevMonthKg) }) : t("в прошлом месяце продаж не было")}
        </KpiTile>
        <KpiTile label={t("Выручка")} value={money(s.revenue, t)} unit={t("сум")} />
        <KpiTile
          label={t("Продаётся SKU")}
          value={num(s.skuSold)}
          unit={`/ ${num(s.skuTotal)}`}
          title={t("Из SKU, которые продавались во вторичке с 1 января по конец месяца; «продаётся» — есть точка с положительной строкой")}
        >
          {s.skuLost > 0 ? <span className="font-semibold text-bad">{t("пропало {n}", { n: num(s.skuLost) })}</span> : t("ничего не пропало")}
        </KpiTile>
        <KpiTile label={t("ТТ")} value={num(s.outlets)} title={t("«Только он» — точки, которые из всех SKU купили ровно один")}>
          {t("точек с покупкой за месяц · только один SKU — {n}", { n: num(s.mono) })}
        </KpiTile>
      </div>
      <CategoryCards cards={data.categories} scope={scopeLabel} query={queryWith(q, { direction: param(sp, "direction"), region: param(sp, "region") })} />
      <CollapsedSections>
        <AkbChart data={data.akbMonths} />
        {data.regions.length > 0 && <AssortmentRegionsTable rows={data.regions} query={q} />}
        <AssortmentMatrix data={data} />
        <ProductsTable rows={data.products} hint={`${t("по выручке за месяц")} · ${scopeName}`} outsideReport={data.productsOutsideReport} quality />
        <DataQualityNotes quality={data.quality} period={data.period} />
      </CollapsedSections>
      <p className="mt-4 text-xs text-ink-3">
        {t("Регион подробнее — на его странице во «Вторичке»:")}{" "}
        <a className="text-accent hover:underline" href={withQuery("/sales/republic", q)}>
          {t("все регионы")}
        </a>
        .
      </p>
    </SalesFrame>
  );
}
