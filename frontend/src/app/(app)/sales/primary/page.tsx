import { KpiTile, Note, Section } from "@/components/sales/bits";
import { PrimaryCalendar, PrimaryCategoriesTable, PrimaryDealersTable, PrimaryItemsTable } from "@/components/sales/assortment-tables";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { date, dateTime, delta, kg, money, monthLabel, monthShort, num } from "@/lib/sales/format";
import { apiQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { PrimaryView } from "@/lib/sales/types";

export const metadata = { title: "Первичка · Продажи" };

export default async function PrimaryPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const data = await apiGet<PrimaryView>(`/api/sales/primary${apiQuery(sp)}`, "/sales/primary");
  const months = data.months.filter((m) => m.month <= data.month);
  const max = Math.max(1, ...months.map((m) => m.kg));
  const vsPrev = data.prevMonthKg ? (data.forecastKg ?? data.kg) / data.prevMonthKg - 1 : null;

  return (
    <SalesFrame
      title="Первичка"
      subtitle={`Отгрузки завода дилерам · ${monthLabel(data.year, data.month)}`}
      crumbs={[{ label: "Первичка" }]}
      sp={sp}
    >
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <KpiTile label="Отгружено" value={kg(data.kg)} unit="кг">
          {data.forecastKg != null ? `прогноз на месяц ${kg(data.forecastKg)} кг` : data.dataThrough ? `по ${date(data.dataThrough)}` : "отгрузок нет"}
        </KpiTile>
        <KpiTile label="Сумма" value={money(data.sum)} unit="сум">
          по прайсу перемещения
        </KpiTile>
        <KpiTile label="Отгрузок" value={num(data.shipments)}>
          {num(data.dealers)} дилеров · {num(data.shipmentDays)} дней с отгрузками
        </KpiTile>
        <KpiTile label="К прошлому месяцу" value={delta(vsPrev)}>
          прошлый месяц {kg(data.prevMonthKg)} кг{data.forecastKg != null ? " · сравнивается прогноз" : ""}
        </KpiTile>
      </div>

      <Section title="По месяцам" hint={`${data.year}, кг`}>
        <div className="space-y-1.5">
          {months.map((m) => (
            <div key={m.month} className="grid grid-cols-[36px_1fr_110px] items-center gap-3 text-xs" title={`${kg(m.kg)} кг · ${money(m.sum)}`}>
              <span className={m.month === data.month ? "font-semibold text-ink" : "text-ink-2"}>{monthShort(m.month)}</span>
              <div className="relative h-4">
                <div className={`absolute inset-y-0 left-0 rounded ${m.month === data.month && data.forecastKg != null ? "border-2 border-accent" : "bg-accent"}`} style={{ width: `${(m.kg / max) * 100}%` }} />
              </div>
              <span className="text-right tabular-nums text-ink">{kg(m.kg)}</span>
            </div>
          ))}
        </div>
        <Note>
          Текущий месяц ещё идёт — его полоса контуром.{" "}
          {data.toExportKg > 0 && `С завода на склад «${data.exportStock ?? "Экспорт"}» в этом месяце: ${kg(data.toExportKg)} кг — это экспорт, в первичку не входит. `}
          {data.toFactoryKg > 0 && `Перемещения на склад завода: ${kg(data.toFactoryKg)} кг (${num(data.toFactoryTransfers)} шт.) — показаны отдельно и не вычитаются.`}
        </Note>
      </Section>

      <PrimaryDealersTable data={data} />
      <PrimaryCategoriesTable data={data} />
      <PrimaryCalendar data={data} />
      <PrimaryItemsTable data={data} />

      <Note>
        Источник — перемещения Linko со склада «{data.factoryStock ?? "Завод"}» на склады регионов в статусах «отдано» или «принято» (stock_transfers), на{" "}
        {dateTime(data.syncedAt)}. Дата отгрузки — время выдачи, без него — время приёмки. Вес — брутто строк, сумма — по прайс-листу перемещения (какой
        это прайс, завода или дилера, в API не указано). Плана первички в Linko нет — выполнение не считается.
      </Note>
    </SalesFrame>
  );
}
