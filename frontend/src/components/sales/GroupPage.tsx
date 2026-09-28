import { KpiRow, UnassignedWarning, UnitCard } from "./blocks";
import { VisitCalendarTable } from "./calendars";
import { NotBoughtTable, RegionsTable, SameDaysTable } from "./tables";
import { monthGenitive, monthName } from "@/lib/sales/format";
import { param, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { GroupView } from "@/lib/sales/types";

const NO_REGION = "00000000-0000-0000-0000-000000000000";

/** Содержимое уровней «Республика» и «РМ / направление». */
export function GroupPage({
  data,
  sp,
  query,
  cardsTitle,
  cardLink,
  regionsTitle,
}: {
  data: GroupView;
  sp: SalesSearchParams;
  query: string;
  cardsTitle: string;
  cardLink: "direction" | "region";
  regionsTitle: string;
}) {
  const { period } = data;
  const from = Number(param(sp, "from") ?? 1);
  const to = Number(param(sp, "to") ?? Math.max(1, period.workedDays));
  const prevMonth = period.month === 1 ? 12 : period.month - 1;
  const cutoffDay = Number(period.previousCutoff.slice(8, 10));

  return (
    <>
      <KpiRow kpi={data.kpi} period={period} />
      <UnassignedWarning kgValue={data.unassigned.kg} share={data.unassigned.share} />

      <h2 className="mt-8 text-lg font-semibold text-ink">{cardsTitle}</h2>
      <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {data.cards.map((c) => (
          <UnitCard
            key={c.id}
            unit={c}
            href={
              cardLink === "direction"
                ? withQuery(`/sales/directions/${c.id}`, query)
                : c.id === NO_REGION
                  ? null
                  : withQuery(`/sales/regions/${c.id}`, query)
            }
          />
        ))}
      </div>

      <RegionsTable
        rows={data.regions}
        query={query}
        title={regionsTitle}
        hint={`текущий темп × ${period.daysInMonth} дн. (отработано ${period.workedDays})`}
      />
      <VisitCalendarTable rows={data.visitCalendar} totalName="Итого" from={from} to={to} maxDay={period.daysInMonth} />
      <SameDaysTable
        rows={data.sameDays}
        nameLabel="Регион"
        hint={`${monthGenitive(prevMonth)}, 1–${cutoffDay} числа`}
        link="region"
        query={query}
      />
      <NotBoughtTable
        rows={data.notBought}
        nameLabel="Регион"
        hint={`база — ${monthName(prevMonth)}, кто пока молчит`}
        workedDays={period.workedDays}
        daysInMonth={period.daysInMonth}
      />
    </>
  );
}
