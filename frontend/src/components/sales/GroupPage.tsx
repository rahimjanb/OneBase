import { AkbMonthsCard } from "./AkbMonthsCard";
import { CollapsedSections } from "./bits";
import { CategoryPlanTable, DataQualityNotes, KpiRow, NextMonthCard, UnassignedWarning, UnitCard } from "./blocks";
import { MonthCalendarTable, VisitCalendarTable } from "./calendars";
import { CategoryCards } from "./categories";
import { NotBoughtTable, RegionsTable, SameDaysTable } from "./tables";
import { monthGenitive, monthName } from "@/lib/sales/format";
import { param, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { GroupView, UnitRow } from "@/lib/sales/types";

const NO_REGION = "00000000-0000-0000-0000-000000000000";

function cardHref(card: UnitRow, query: string): string | null {
  if (card.kind === "direction") return withQuery(`/sales/directions/${card.id}`, query);
  return card.id === NO_REGION ? null : withQuery(`/sales/regions/${card.id}`, query);
}

function Cards({ title, cards, query }: { title: string; cards: UnitRow[]; query: string }) {
  if (cards.length === 0) return null;
  return (
    <>
      <h2 className="mt-8 text-lg font-semibold text-ink">{title}</h2>
      <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        {cards.map((c) => (
          <UnitCard key={c.id} unit={c} href={cardHref(c, query)} />
        ))}
      </div>
    </>
  );
}

/** Содержимое уровней «Республика» и «РМ / направление». */
export function GroupPage({
  data,
  sp,
  query,
  categoryQuery,
  regionsTitle,
  scopeName,
}: {
  data: GroupView;
  sp: SalesSearchParams;
  query: string;
  /** Период и охват для страницы категории («direction=…» у направления). */
  categoryQuery: string;
  regionsTitle: string;
  /** «в республике», «в направлении» — для пояснений. */
  scopeName: string;
}) {
  const { period } = data;
  const from = Number(param(sp, "from") ?? 1);
  const to = Number(param(sp, "to") ?? Math.max(1, period.workedDays));
  const prevMonth = period.month === 1 ? 12 : period.month - 1;
  const cutoffDay = Number(period.previousCutoff.slice(8, 10));

  const directions = data.cards.filter((c) => c.kind === "direction");
  const regions = data.cards.filter((c) => c.kind !== "direction");

  return (
    <>
      <KpiRow kpi={data.kpi} period={period} />
      <UnassignedWarning kgValue={data.unassigned.kg} share={data.unassigned.share} />

      <Cards title="Региональные менеджеры" cards={directions} query={query} />
      <Cards title={directions.length > 0 ? "Регионы без РМ" : "Регионы"} cards={regions} query={query} />

      {data.nextMonth && <NextMonthCard plan={data.nextMonth} rowsTitle="Регион" />}
      <CategoryCards cards={data.categoryCards} scope={scopeName} query={categoryQuery} />

      {/* Ключ по месяцу: смена месяца сбрасывает раскрытые секции, сортировки и раскрытые строки (DOC-filters §12). */}
      <CollapsedSections key={`${period.year}-${period.month}`}>
        <CategoryPlanTable rows={data.categoryPlans} total={data.categoryPlanTotal} source={data.kpi.planSource} plan={period.plan} />
        <AkbMonthsCard data={data.akbMonths} />
        <RegionsTable
          rows={data.regions}
          query={query}
          title={regionsTitle}
          hint={`текущий темп × ${period.daysInMonth} дн. (отработано ${period.workedDays})`}
        />
        <MonthCalendarTable
          calendar={data.calendar}
          year={period.year}
          month={period.month}
          categories={data.categoryCards.filter((c) => c.id !== "none").map((c) => ({ id: Number(c.id), name: c.name }))}
          title="Календарь месяца по регионам"
          rowLabel="Регион"
          rowKind="region"
        />
        <VisitCalendarTable
          rows={data.visitCalendar}
          totalRow={data.visitCalendarTotal}
          totalName="Итого"
          from={from}
          to={to}
          maxDay={period.daysInMonth}
          factDays={period.workedDays}
          year={period.year}
          month={period.month}
        />
        <SameDaysTable
          rows={data.sameDays}
          nameLabel="Регион"
          hint={`${monthGenitive(prevMonth)}, 1–${cutoffDay} числа`}
          link="region"
          query={query}
        />
        <NotBoughtTable
          rows={data.notBought}
          total={data.notBoughtTotal}
          nameLabel="Регион"
          hint={`база — ${monthName(prevMonth)}, кто пока молчит`}
          workedDays={period.workedDays}
          daysInMonth={period.daysInMonth}
        />
        <DataQualityNotes quality={data.quality} period={period} />
      </CollapsedSections>
    </>
  );
}
