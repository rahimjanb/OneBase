import Link from "next/link";
import { Settings2 } from "lucide-react";
import { Alert, CollapsedSections, Note } from "@/components/sales/bits";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { TajikCategoryTiles, TajikKpis, TajikRegionCards, TajikSyncNotice } from "@/components/sales/tajik/TajikBlocks";
import { TajikAgentsTable, TajikProductsTable, TajikRegionsTable, TajikStockTable } from "@/components/sales/tajik/TajikTables";
import { getT } from "@/i18n/server";
import { date, dayMonth, kg, monthLabel, num, pct } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import { NO_REGION, TAJIK_BASE, type TajikOverview } from "@/lib/sales/tajik";
import { apiGet, apiSession } from "@/lib/server-api";
import { has, type Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Таджикистан · Экспорт · Продажи") };
}

/**
 * «Экспорт → Таджикистан»: продажи дилера по его 1С (раз в 30 минут) — итоги месяца, категории, все регионы карточками и таблицей, ТП,
 * товары и остаток его склада. Регион точки — по городу или району в её названии, по ТП или вручную («Регионы»).
 */
export default async function TajikistanPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const sp = await searchParams;
  const [data, me] = await Promise.all([apiGet<TajikOverview>(`/api/sales/tajik${apiQuery(sp)}`, TAJIK_BASE), apiSession<Me>("/api/auth/me")]);
  const q = periodQuery(sp);
  const p = data.period;
  const month = monthLabel(p.year, p.month, t);
  const empty = data.totals.documents === 0;

  return (
    <SalesFrame
      title={t("Таджикистан")}
      subtitle={`${t("Продажи дилера по его 1С")} · ${month}${p.dataThrough ? ` · ${p.closed ? t("месяц закрыт") : t("данные по {date}", { date: dayMonth(p.dataThrough) })}` : ""}`}
      crumbs={[{ label: t("Экспорт и опт"), href: withQuery("/sales/export", q) }, { label: t("Таджикистан") }]}
      back={withQuery("/sales/export", q)}
      sp={sp}
    >
      <TajikSyncNotice sync={data.sync} canSetup={has(me, "integrations.manage")} />

      {empty ? (
        <p className="mt-4 rounded-xl border border-line bg-surface px-4 py-8 text-center text-sm text-ink-3">
          {p.dataFrom
            ? t("За {month} продаж в 1С Таджикистана нет. Данные есть с {date}.", { month, date: date(p.dataFrom) })
            : t("Продажи из 1С Таджикистана ещё не загружались.")}
        </p>
      ) : (
        <>
          <div className="mt-4">
            <TajikKpis totals={data.totals} prev={data.prev} />
          </div>

          {data.unresolved.stores > 0 && (
            <Alert tone="warn">
              {t("{n} точек без региона — {kg} кг ({share} веса): в названии нет города или района, а ТП не закреплён за регионом.", {
                n: num(data.unresolved.stores),
                kg: kg(data.unresolved.kg),
                share: pct(data.unresolved.share),
              })}{" "}
              <Link href={`${TAJIK_BASE}/setup`} className="font-medium text-accent-strong hover:underline">
                {t("Настроить регионы")}
              </Link>
            </Alert>
          )}

          <div className="mt-8 flex flex-wrap items-end justify-between gap-3">
            <h2 className="text-lg font-semibold text-ink">{t("Все регионы")}</h2>
            <Link href={`${TAJIK_BASE}/setup`} className="inline-flex items-center gap-1.5 text-sm font-medium text-ink-2 hover:text-accent-strong">
              <Settings2 className="size-4" aria-hidden />
              {t("Регионы и точки")}
            </Link>
          </div>
          <TajikRegionCards rows={data.regions.filter((r) => r.kg > 0 || r.id === NO_REGION)} query={q} />

          <TajikCategoryTiles rows={data.categories} />

          <CollapsedSections key={`${p.year}-${p.month}`}>
            <TajikRegionsTable rows={data.regions} query={q} />
            <TajikAgentsTable rows={data.agents} />
            <TajikProductsTable rows={data.products} />
          </CollapsedSections>
        </>
      )}

      <div className="mt-6">
        <TajikStockTable rows={data.stock} stockDate={data.stockDate} />
      </div>

      <Note>
        {t(
          "Источник — API 1С дилера в Таджикистане: расходные накладные (точка, ТП, товар, количество, цена, скидка, сумма) и остаток склада. Суммы — в сомони, без пересчёта в сумы: курса в данных нет. Вес — количество × вес единицы товара. Категории — по товару Linko с тем же номером в начале названия. Во вторичку и в «Завод» эти продажи не входят.",
        )}
        {data.unknownWeightProducts > 0 && ` ${t("У {n} товаров вес единицы неизвестен — в килограммы они не попадают.", { n: num(data.unknownWeightProducts) })}`}
      </Note>
    </SalesFrame>
  );
}
