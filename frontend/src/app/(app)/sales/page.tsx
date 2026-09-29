import Link from "next/link";
import { KpiRow, UnitCard } from "@/components/sales/blocks";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { apiGet } from "@/lib/server-api";
import { num } from "@/lib/sales/format";
import { apiQuery, periodQuery, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { OverviewView } from "@/lib/sales/types";

export const metadata = { title: "Продажи · OneBase" };

export default async function SalesPage({ searchParams }: { searchParams: Promise<SalesSearchParams> }) {
  const sp = await searchParams;
  const data = await apiGet<OverviewView>(`/api/sales/overview${apiQuery(sp)}`, "/sales");
  const q = periodQuery(sp);

  return (
    <SalesFrame title="Продажи" subtitle="Полевой контроль: план, факт и работа торговых представителей" sp={sp}>
      <KpiRow kpi={data.kpi} period={data.period} />

      <p className="mt-4 rounded-lg border border-line bg-surface px-4 py-3 text-sm text-ink-2">
        Из <b className="text-ink">{num(data.activeAgents)}</b> действующих ТП помечены:{" "}
        <Link href={withQuery("/sales/problems", q)} className="font-semibold text-bad hover:underline">
          {num(data.flags.critical)} критично
        </Link>
        ,{" "}
        <Link href={withQuery("/sales/problems", q)} className="font-semibold text-warn hover:underline">
          {num(data.flags.risk)} риск
        </Link>
        . Ещё {num(data.vacancies)} — вакансии.
      </p>

      <h2 className="mt-8 text-lg font-semibold text-ink">Подразделения</h2>
      <p className="mt-1 text-sm text-ink-2">Нажмите на карточку, чтобы перейти к регионам и торговым представителям.</p>
      <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <UnitCard unit={data.republic} href={withQuery("/sales/republic", q)} />
      </div>
    </SalesFrame>
  );
}
