import Link from "next/link";
import { FlagCountPills } from "@/components/sales/bits";
import { ExportCard, KpiRow, UnitCard } from "@/components/sales/blocks";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { num } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { OverviewView } from "@/lib/sales/types";

export const metadata = { title: "Продажи · OneBase" };

/** «Вторичка», верхний уровень: шесть плиток, сводка по флагам и карточки «Республика» и «Экспорт» — как в «Полевом контроле». */
export default async function SalesPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const data = await apiGet<OverviewView>(`/api/sales/overview${apiQuery(sp)}`, "/sales");
  const q = periodQuery(sp);

  return (
    <SalesFrame title="Вторичка" subtitle="Полевой контроль: план, факт и работа торговых представителей" sp={sp}>
      <KpiRow kpi={data.kpi} period={data.period} />

      <div className="mt-4 flex flex-wrap items-center gap-x-2 gap-y-1.5 rounded-lg border border-l-4 border-line border-l-warn bg-surface px-4 py-3 text-sm text-ink-2 shadow-sm">
        <span>
          Из <b className="text-ink">{num(data.activeAgents)}</b> действующих ТП помечены:
        </span>
        <Link href={withQuery("/sales/problems", q)} className="hover:opacity-80" title="Открыть «Проблемные агенты»">
          <FlagCountPills flags={data.flags} />
        </Link>
        <span>
          Ещё <b className="text-ink">{num(data.vacancies)}</b> — вакансии, они в рейтинг не идут.
        </span>
      </div>

      <div className="mt-5 grid gap-4 md:grid-cols-2 xl:grid-cols-3 min-[106.25rem]:grid-cols-4">
        <UnitCard unit={data.republic} href={withQuery("/sales/republic", q)} />
        {data.excluded && <ExportCard data={data.excluded} href={withQuery("/sales/export", q)} />}
      </div>
    </SalesFrame>
  );
}
