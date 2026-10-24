import { getT } from "@/i18n/server";
import { AkbMonthsCard } from "./AkbMonthsCard";
import { CollapsedSections } from "./bits";
import { DataQualityNotes, KpiRow, NextMonthCard, UnassignedWarning, UnitCard } from "./blocks";
import { CategoryPlanCard } from "./category-plan";
import { MonthCalendarTable, VisitCalendarTable } from "./calendars";
import { CategoryCards } from "./categories";
import { NotBoughtTable, RegionsTable, SameDaysTable } from "./tables";
import { monthGenitive, monthName, paceDays } from "@/lib/sales/format";
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

/** Содержимое уровней «Республика» и «РМ / направление». Серверный (подключают только страницы): перевод — getT(). */
export async function GroupPage({
  data,
  sp,
  query,
  categoryQuery,
  regionsTitle,
  scopeName,
  republicWide = false,
}: {
  data: GroupView;
  sp: SalesSearchParams;
  query: string;
  /** Период и охват для страницы категории («direction=…» у направления). */
  categoryQuery: string;
  regionsTitle: string;
  /** «в республике», «в направлении» — для пояснений. */
  scopeName: string;
  /** Страница направления: «Качество данных» — по всей республике. */
  republicWide?: boolean;
}) {
  const t = await getT();
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

      <Cards title={t("Региональные менеджеры")} cards={directions} query={query} />
      <Cards title={directions.length > 0 ? t("Регионы без РМ") : t("Регионы")} cards={regions} query={query} />

      {data.nextMonth && <NextMonthCard plan={data.nextMonth} rowsTitle={t("Регион")} />}
      <CategoryCards cards={data.categoryCards} scope={scopeName} query={categoryQuery} plans={data.categoryPlans} />

      {/* План и факт по категориям и АКБ по месяцам — сразу под карточками, на широком экране рядом. */}
      <div key={`charts-${period.year}-${period.month}`} className={`grid items-start gap-x-6 ${data.categoryPlans.length > 0 ? "2xl:grid-cols-2" : ""}`}>
        <CategoryPlanCard rows={data.categoryPlans} total={data.categoryPlanTotal} source={data.kpi.planSource} plan={period.plan} />
        <AkbMonthsCard data={data.akbMonths} />
      </div>

      {/* Ключ по месяцу: смена месяца сбрасывает раскрытые секции, сортировки и раскрытые строки (DOC-filters §12). */}
      <CollapsedSections key={`${period.year}-${period.month}`}>
        <RegionsTable
          rows={data.regions}
          query={query}
          title={regionsTitle}
          hint={t("текущий темп × {days} дн. (отработано {worked})", { days: period.daysInMonth, worked: paceDays(period) })}
        />
        <MonthCalendarTable
          calendar={data.calendar}
          year={period.year}
          month={period.month}
          categories={data.categoryCards.filter((c) => c.id !== "none").map((c) => ({ id: Number(c.id), name: c.name }))}
          title={t("Календарь месяца по регионам")}
          rowLabel={t("Регион")}
          rowKind="region"
        />
        <VisitCalendarTable
          rows={data.visitCalendar}
          totalRow={data.visitCalendarTotal}
          totalName={t("Итого")}
          from={from}
          to={to}
          maxDay={period.daysInMonth}
          factDays={period.workedDays}
          year={period.year}
          month={period.month}
        />
        <SameDaysTable
          rows={data.sameDays}
          nameLabel={t("Регион")}
          hint={t("{month}, 1–{day} числа", { month: monthGenitive(prevMonth, t), day: cutoffDay })}
          link="region"
          query={query}
        />
        <NotBoughtTable
          rows={data.notBought}
          total={data.notBoughtTotal}
          nameLabel={t("Регион")}
          hint={t("база — {month}, кто пока молчит", { month: monthName(prevMonth, t) })}
          workedDays={period.workedDays}
          daysInMonth={period.daysInMonth}
        />
        <DataQualityNotes quality={data.quality} period={period} republicWide={republicWide} />
      </CollapsedSections>
    </>
  );
}
