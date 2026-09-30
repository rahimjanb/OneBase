import { PrimaryExport } from "@/components/sales/primary";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { date, monthLabel } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { PrimaryView } from "@/lib/sales/types";

export const metadata = { title: "Первичка · Экспорт · Продажи" };

/** «Первичка → Экспорт»: заказы филиала «Завод» экспортным точкам — по месяцам, странам и дням. */
export default async function PrimaryExportPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const data = await apiGet<PrimaryView>(`/api/sales/primary/export${apiQuery(sp)}`, "/sales/primary/export");
  const q = periodQuery(sp);
  const running = data.forecastKg != null;

  return (
    <SalesFrame
      title="Первичка"
      subtitle={`Завод — на экспорт · ${monthLabel(data.year, data.month)} · ${running ? `данные по ${date(data.dataThrough)}` : data.dataThrough ? "месяц закрыт" : "отгрузок нет"}`}
      crumbs={[{ label: "Первичка", href: withQuery("/sales/primary", q) }, { label: "Экспорт" }]}
      back={withQuery("/sales/primary", q)}
      sp={sp}
    >
      <PrimaryExport data={data} />
    </SalesFrame>
  );
}
