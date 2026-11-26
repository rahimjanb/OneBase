import { CollapsedSections, KpiTile, Note, deltaTone } from "@/components/sales/bits";
import { CategoryCards } from "@/components/sales/categories";
import { ExportAgentsTable, ExportMarketsTable, ProductsTable } from "@/components/sales/assortment-tables";
import { TajikExportCard } from "@/components/sales/tajik/TajikBlocks";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { delta, kg, money, num, plural } from "@/lib/sales/format";
import { apiQuery, periodQuery, queryWith, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { ExportView } from "@/lib/sales/types";
import type { TajikOverview } from "@/lib/sales/tajik";
import { unstable_rethrow } from "next/navigation";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Экспорт и опт · Продажи") };
}

/** Продажи дилера в Таджикистане (1С) за тот же месяц; не загрузились — страница экспорта показывается без карточки. */
async function tajikOrNull(sp: SalesSearchParams): Promise<TajikOverview | null> {
  try {
    return await apiGet<TajikOverview>(`/api/sales/tajik${apiQuery(sp)}`, "/sales/export");
  } catch (error) {
    unstable_rethrow(error);
    return null;
  }
}

export default async function ExportPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const t = await getT();
  const orders = (n: number) => `${num(n)} ${t(plural(n, ["заказ", "заказа", "заказов"]))}`;
  const sp = await searchParams;
  const [data, tajik] = await Promise.all([apiGet<ExportView>(`/api/sales/export${apiQuery(sp)}`, "/sales/export"), tajikOrNull(sp)]);
  const q = periodQuery(sp);
  const s = data.summary;

  return (
    <SalesFrame
      title={t("Экспорт и опт")}
      subtitle={t("Филиал «Завод» в Linko — экспорт и крупный опт. Во вторичку не входит, иначе завысил бы факт республики.")}
      crumbs={[{ label: t("Экспорт и опт") }]}
      back={withQuery("/sales", q)}
      sp={sp}
      period={data.period}
    >
      {tajik && (
        <>
          <TajikExportCard data={tajik} query={q} className="mb-8" />
          <h2 className="mb-3 text-lg font-semibold text-ink">{t("Филиал «Завод» в Linko")}</h2>
        </>
      )}
      {!s ? (
        <p className="rounded-xl border border-line bg-surface px-4 py-8 text-center text-sm text-ink-3">{t("За месяц продаж филиала «Завод» в Linko нет.")}</p>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <KpiTile label={t("Факт, кг")} value={kg(s.factKg)}>
              {data.period.closed ? t("месяц закрыт") : t("прогноз {kg} кг", { kg: kg(s.forecastKg) })}
            </KpiTile>
            <KpiTile label={t("Выручка")} value={money(s.revenue, t)} unit={t("сум")}>
              {orders(s.orders)}
            </KpiTile>
            <KpiTile label={t("АКБ")} value={num(s.akb)}>
              {t("точек с отгрузкой")}
            </KpiTile>
            <KpiTile
              label={t("К прошлому месяцу")}
              value={delta(s.vsPrevMonth)}
              tone={deltaTone(s.vsPrevMonth)}
              title={data.period.closed ? t("Прогноз месяца к прошлому месяцу — только у идущего месяца, как у карточек категорий") : t("Прогноз месяца к факту прошлого месяца")}
            >
              {t("прошлый месяц {kg} кг", { kg: kg(s.prevMonthKg) })}
            </KpiTile>
          </div>
          {data.otherCurrency.length > 0 && (
            <p className="mt-4 rounded-lg border border-line bg-muted px-4 py-3 text-sm text-ink-2">
              {t("Кроме сумов:")}{" "}
              {data.otherCurrency.map((c, i) => (
                <span key={c.currency}>
                  {i > 0 && ", "}
                  <b className="text-ink">
                    {num(c.amount)} {c.currency}
                  </b>{" "}
                  ({orders(c.orders)})
                </span>
              ))}
              . {t("Курса в данных нет — эти суммы в выручку не сложены, вес заказов учтён.")}
            </p>
          )}
          <CategoryCards cards={data.categories} scope={t("экспорте")} query={queryWith(q, { export: true })} />
          <CollapsedSections>
            <ExportMarketsTable rows={data.markets} />
            <ExportAgentsTable rows={data.agents} />
            <ProductsTable rows={data.products} outsideReport={data.productsOutsideReport} />
          </CollapsedSections>
          <Note>
            {t(
              "Разбивки по странам в Linko External API нет: в артефакте «Полевого контроля» она шла из отдельных файлов по странам. Здесь — то, что Linko отдаёт по филиалу «Завод»: покупатели, агенты и товары. Суммы — в валюте заказов, без пересчёта.",
            )}
          </Note>
        </>
      )}
    </SalesFrame>
  );
}
