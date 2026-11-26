import Link from "next/link";
import { notFound } from "next/navigation";
import { Alert, CollapsedSections, Note } from "@/components/sales/bits";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { TajikCategoryTiles, TajikKpis } from "@/components/sales/tajik/TajikBlocks";
import { TajikAgentsTable, TajikProductsTable, TajikStoresTable } from "@/components/sales/tajik/TajikTables";
import { getT } from "@/i18n/server";
import { dayMonth, monthLabel } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import { NO_REGION, TAJIK_BASE, tajikHref, type TajikRegionView } from "@/lib/sales/tajik";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Регион · Таджикистан · Продажи") };
}

/**
 * Регион Таджикистана: какие точки купили и что — по категориям (плитка категории — фильтр), товары с точками, где они проданы, ТП.
 * category — название категории отчёта.
 */
export default async function TajikRegionPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const { id } = await params;
  const sp = await searchParams;
  const path = `${TAJIK_BASE}/regions/${id}`;
  const data = await apiGetOrNull<TajikRegionView>(`/api/sales/tajik/regions/${encodeURIComponent(id)}${apiQuery(sp, ["category"])}`, path);
  if (!data) notFound();

  const q = periodQuery(sp);
  const name = data.id === NO_REGION ? t(data.name) : data.name;
  const p = data.period;
  const filter = data.category;

  return (
    <SalesFrame
      title={name}
      subtitle={`${t("Таджикистан")} · ${monthLabel(p.year, p.month, t)}${p.dataThrough && !p.closed ? ` · ${t("данные по {date}", { date: dayMonth(p.dataThrough) })}` : ""}${filter ? ` · ${t(filter)}` : ""}`}
      crumbs={[
        { label: t("Экспорт и опт"), href: withQuery("/sales/export", q) },
        { label: t("Таджикистан"), href: withQuery(TAJIK_BASE, q) },
        { label: name },
      ]}
      back={withQuery(TAJIK_BASE, q)}
      sp={sp}
    >
      {data.id === NO_REGION ? (
        <Alert tone="warn" className="mb-4">
          {t("В названиях этих точек нет города или района из списка регионов, а их ТП не закреплён за регионом.")}{" "}
          <Link href={`${TAJIK_BASE}/setup`} className="font-medium text-accent-strong hover:underline">
            {t("Закрепить ТП или точки за регионами")}
          </Link>
        </Alert>
      ) : (
        data.keywords.length > 0 && (
          <p className="mb-4 text-xs text-ink-3">
            {t("Точка попадает в регион, если в её названии есть слово: {words}.", { words: data.keywords.join(", ") })}{" "}
            <Link href={`${TAJIK_BASE}/setup`} className="hover:text-accent-strong hover:underline">
              {t("Изменить")}
            </Link>
          </p>
        )
      )}

      <TajikKpis totals={data.totals} prev={data.prev} />

      <TajikCategoryTiles
        rows={data.categories}
        active={filter}
        allHref={tajikHref(path, q)}
        hrefOf={(c) => tajikHref(path, q, { category: c })}
      />

      <div className="mt-6">
        <TajikStoresTable
          rows={data.stores}
          query={q}
          title={filter ? t("Точки, где брали «{category}»", { category: t(filter) }) : t("Точки")}
        />
      </div>
      <CollapsedSections key={`${p.year}-${p.month}-${filter ?? ""}`}>
        <TajikProductsTable rows={data.products} query={q} withWhere title={filter ? t("Товары категории «{category}»", { category: t(filter) }) : t("Товары и где они проданы")} />
        <TajikAgentsTable rows={data.agents} />
      </CollapsedSections>
      <Note>{t("Вес — количество × вес единицы товара; суммы — в сомони. «К прошлому месяцу» — за те же дни прошлого месяца.")}</Note>
    </SalesFrame>
  );
}
