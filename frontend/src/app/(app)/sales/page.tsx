import { KpiRow, UnitCard } from "@/components/sales/blocks";
import { ExportSummaryCard, FlagsStrip } from "@/components/sales/overview";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { OverviewView } from "@/lib/sales/types";

export const metadata = { title: "Продажи · OneBase" };

/** «Вторичка», верхний уровень: шесть компактных плиток, полоса с флагами ТП и сводки «Республика» и «Экспорт и опт». */
export default async function SalesPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const data = await apiGet<OverviewView>(`/api/sales/overview${apiQuery(sp)}`, "/sales");
  const q = periodQuery(sp);

  return (
    <SalesFrame title="Вторичка" subtitle="План, факт и работа торговых представителей" sp={sp} period={data.period}>
      <KpiRow kpi={data.kpi} period={data.period} />
      <FlagsStrip activeAgents={data.activeAgents} flags={data.flags} vacancies={data.vacancies} href={withQuery("/sales/problems", q)} />
      <div className="mt-5 grid gap-4 md:grid-cols-2">
        {/* Флаги уже в полосе выше, в карточке их не повторяем. */}
        <UnitCard unit={data.republic} href={withQuery("/sales/republic", q)} showFlags={false} />
        {data.excluded && <ExportSummaryCard data={data.excluded} href={withQuery("/sales/export", q)} />}
      </div>
    </SalesFrame>
  );
}
