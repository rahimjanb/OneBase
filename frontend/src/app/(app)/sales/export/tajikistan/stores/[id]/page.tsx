import { notFound } from "next/navigation";
import { CollapsedSections } from "@/components/sales/bits";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { TajikCategoryTiles, TajikKpis } from "@/components/sales/tajik/TajikBlocks";
import { TajikDocumentsTable, TajikProductsTable, TajikStoreMonthsTable } from "@/components/sales/tajik/TajikTables";
import { getT } from "@/i18n/server";
import { monthLabel } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import { NO_REGION, TAJIK_BASE, regionSourceLabel, type TajikStoreView } from "@/lib/sales/tajik";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Точка · Таджикистан · Продажи") };
}

/** Точка Таджикистана: регион и откуда он, ТП, итоги месяца, категории, товары, накладные со строками и продажи по месяцам. */
export default async function TajikStorePage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const { id } = await params;
  const sp = await searchParams;
  const data = await apiGetOrNull<TajikStoreView>(`/api/sales/tajik/stores/${encodeURIComponent(id)}${apiQuery(sp)}`, `${TAJIK_BASE}/stores/${id}`);
  if (!data) notFound();

  const q = periodQuery(sp);
  const region = data.regionId === NO_REGION ? t(data.regionName) : data.regionName;
  const p = data.period;

  return (
    <SalesFrame
      title={data.name}
      subtitle={`${region} (${t(regionSourceLabel[data.regionSource])})${data.agent ? ` · ${t("ТП")} ${data.agent}` : ""} · ${monthLabel(p.year, p.month, t)}`}
      crumbs={[
        { label: t("Экспорт и опт"), href: withQuery("/sales/export", q) },
        { label: t("Таджикистан"), href: withQuery(TAJIK_BASE, q) },
        { label: region, href: withQuery(`${TAJIK_BASE}/regions/${data.regionId}`, q) },
        { label: data.name },
      ]}
      back={withQuery(`${TAJIK_BASE}/regions/${data.regionId}`, q)}
      sp={sp}
    >
      <TajikKpis totals={data.totals} prev={null} store />
      <TajikCategoryTiles rows={data.categories} />
      <div className="mt-6">
        <TajikDocumentsTable rows={data.documents} />
      </div>
      <CollapsedSections key={`${p.year}-${p.month}`}>
        <TajikProductsTable rows={data.products} />
        <TajikStoreMonthsTable rows={data.months} />
      </CollapsedSections>
    </SalesFrame>
  );
}
