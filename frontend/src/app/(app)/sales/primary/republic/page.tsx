import { PrimaryRepublic } from "@/components/sales/primary";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { date, monthLabel } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { PrimaryView } from "@/lib/sales/types";

export const metadata = { title: "Первичка · Республика · Продажи" };

/** «Первичка → Республика»: отгрузка завода дилерам за месяц и с начала года. */
export default async function PrimaryRepublicPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const data = await apiGet<PrimaryView>(`/api/sales/primary${apiQuery(sp)}`, "/sales/primary/republic");
  const q = periodQuery(sp);
  const running = data.forecastKg != null;

  return (
    <SalesFrame
      title="Первичка"
      subtitle={`Завод — дилерам · ${monthLabel(data.year, data.month)} · ${running ? `данные по ${date(data.dataThrough)}` : data.dataThrough ? "месяц закрыт" : "отгрузок нет"}`}
      crumbs={[{ label: "Первичка", href: withQuery("/sales/primary", q) }, { label: "Республика" }]}
      back={withQuery("/sales/primary", q)}
      sp={sp}
    >
      <PrimaryRepublic data={data} />
    </SalesFrame>
  );
}
