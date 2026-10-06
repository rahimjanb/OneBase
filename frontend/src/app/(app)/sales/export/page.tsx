import { CollapsedSections, KpiTile, Note, deltaTone } from "@/components/sales/bits";
import { CategoryCards } from "@/components/sales/categories";
import { ExportAgentsTable, ExportMarketsTable, ProductsTable } from "@/components/sales/assortment-tables";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { delta, kg, money, num, ordersLabel } from "@/lib/sales/format";
import { apiQuery, periodQuery, queryWith, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { ExportView } from "@/lib/sales/types";

export const metadata = { title: "Экспорт и опт · Продажи" };

export default async function ExportPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const data = await apiGet<ExportView>(`/api/sales/export${apiQuery(sp)}`, "/sales/export");
  const q = periodQuery(sp);
  const s = data.summary;

  return (
    <SalesFrame
      title="Экспорт и опт"
      subtitle="Филиал «Завод» в Linko — экспорт и крупный опт. Во вторичку не входит, иначе завысил бы факт республики."
      crumbs={[{ label: "Экспорт и опт" }]}
      back={withQuery("/sales", q)}
      sp={sp}
      period={data.period}
    >
      {!s ? (
        <p className="rounded-xl border border-line bg-surface px-4 py-8 text-center text-sm text-ink-3">За месяц продаж филиала «Завод» в Linko нет.</p>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <KpiTile label="Факт, кг" value={kg(s.factKg)}>
              {data.period.closed ? "месяц закрыт" : `прогноз ${kg(s.forecastKg)} кг`}
            </KpiTile>
            <KpiTile label="Выручка" value={money(s.revenue)} unit="сум">
              {ordersLabel(s.orders)}
            </KpiTile>
            <KpiTile label="АКБ" value={num(s.akb)}>
              точек с отгрузкой
            </KpiTile>
            <KpiTile
              label="К прошлому месяцу"
              value={delta(s.vsPrevMonth)}
              tone={deltaTone(s.vsPrevMonth)}
              title={data.period.closed ? "Прогноз месяца к прошлому месяцу — только у идущего месяца, как у карточек категорий" : "Прогноз месяца к факту прошлого месяца"}
            >
              прошлый месяц {kg(s.prevMonthKg)} кг
            </KpiTile>
          </div>
          {data.otherCurrency.length > 0 && (
            <p className="mt-4 rounded-lg border border-line bg-muted px-4 py-3 text-sm text-ink-2">
              Кроме сумов:{" "}
              {data.otherCurrency.map((c, i) => (
                <span key={c.currency}>
                  {i > 0 && ", "}
                  <b className="text-ink">
                    {num(c.amount)} {c.currency}
                  </b>{" "}
                  ({ordersLabel(c.orders)})
                </span>
              ))}
              . Курса в данных нет — эти суммы в выручку не сложены, вес заказов учтён.
            </p>
          )}
          <CategoryCards cards={data.categories} scope="экспорте" query={queryWith(q, { export: true })} />
          <CollapsedSections>
            <ExportMarketsTable rows={data.markets} />
            <ExportAgentsTable rows={data.agents} />
            <ProductsTable rows={data.products} outsideReport={data.productsOutsideReport} />
          </CollapsedSections>
          <Note>
            Разбивки по странам в Linko External API нет: в артефакте «Полевого контроля» она шла из отдельных файлов по странам. Здесь — то, что
            Linko отдаёт по филиалу «Завод»: покупатели, агенты и товары. Суммы — в валюте заказов, без пересчёта.
          </Note>
        </>
      )}
    </SalesFrame>
  );
}
