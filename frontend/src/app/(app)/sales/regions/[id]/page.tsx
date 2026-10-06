import { notFound } from "next/navigation";
import { AkbMonthsCard } from "@/components/sales/AkbMonthsCard";
import { CollapsedSections } from "@/components/sales/bits";
import { CategoryPlanTable, DataQualityNotes, KpiRow, NextMonthCard, PlanFactMonths, UnassignedWarning, VolumeMix } from "@/components/sales/blocks";
import { MonthCalendarTable, VisitCalendarTable } from "@/components/sales/calendars";
import { CategoryCards } from "@/components/sales/categories";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { NotBoughtTable, NotInDirectoryTable, SameDaysTable, TeamTable } from "@/components/sales/tables";
import { apiGetOrNull } from "@/lib/server-api";
import { monthGenitive, monthName } from "@/lib/sales/format";
import { apiQuery, param, periodQuery, queryWith, withQuery, type SalesSearchParams } from "@/lib/sales/query";
import type { RegionView } from "@/lib/sales/types";

export default async function RegionPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SalesSearchParams>;
}) {
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const path = `/sales/regions/${id}`;
  const data = await apiGetOrNull<RegionView>(`/api/sales/regions/${id}${apiQuery(sp, ["from", "to", "metric", "category"])}`, path);
  if (!data) notFound();

  const q = periodQuery(sp);
  const { period } = data;
  const from = Number(param(sp, "from") ?? 1);
  const to = Number(param(sp, "to") ?? Math.max(1, period.workedDays));
  const prevMonth = period.month === 1 ? 12 : period.month - 1;
  const cutoffDay = Number(period.previousCutoff.slice(8, 10));

  // Регион без РМ: крошки «Республика → Регион», без служебной группы «Без направления».
  const republic = { label: "Республика", href: withQuery("/sales/republic", q) };
  const parent = data.directionId
    ? { label: data.directionName ?? "Направление", href: withQuery(`/sales/directions/${data.directionId}`, q) }
    : null;

  // СВР и дилер — оргструктура OneBase («Настройки продаж»): в Linko их нет.
  const people = [data.supervisor && `СВР: ${data.supervisor}`, data.dealer && `дилер: ${data.dealer}`].filter(Boolean).join(", ");

  return (
    <SalesFrame
      title={data.name}
      subtitle={people || (data.isChannel ? undefined : "СВР и дилер не указаны — их можно задать в настройках продаж")}
      crumbs={[republic, ...(parent ? [parent] : []), { label: data.name }]}
      back={(parent ?? republic).href}
      sp={sp}
      period={period}
    >
      <KpiRow kpi={data.kpi} period={period} />
      <UnassignedWarning kgValue={data.unassigned.kg} share={data.unassigned.share} />

      <PlanFactMonths months={data.months} current={period.month} />
      <VolumeMix categories={data.categories} />
      {data.nextMonth && <NextMonthCard plan={data.nextMonth} rowsTitle="Категория" />}
      <CategoryCards cards={data.categoryCards} scope="регионе" query={queryWith(q, { region: id })} />

      {/* Ключ по месяцу: смена месяца сбрасывает раскрытые секции, сортировки и раскрытые строки (DOC-filters §12). */}
      <CollapsedSections key={`${period.year}-${period.month}`}>
        <CategoryPlanTable rows={data.categoryPlans} total={data.categoryPlanTotal} source={data.kpi.planSource} plan={period.plan} />
        <AkbMonthsCard data={data.akbMonths} />
        <MonthCalendarTable
          calendar={data.calendar}
          year={period.year}
          month={period.month}
          categories={data.categories.filter((c) => c.categoryId != null).map((c) => ({ id: c.categoryId!, name: c.name }))}
        />
        <VisitCalendarTable
          rows={data.visitCalendar}
          flatten
          totalName={data.name}
          from={from}
          to={to}
          maxDay={period.daysInMonth}
          factDays={period.workedDays}
          year={period.year}
          month={period.month}
        />
        <TeamTable rows={data.team} query={q} />
        <SameDaysTable rows={data.sameDays} nameLabel="ТП" hint={`${monthGenitive(prevMonth)}, 1–${cutoffDay} числа`} link="agent" query={q} />
        <NotBoughtTable
          rows={data.notBought}
          total={data.notBoughtTotal}
          nameLabel="ТП"
          hint={`база — ${monthName(prevMonth)}, кто пока молчит`}
          workedDays={period.workedDays}
          daysInMonth={period.daysInMonth}
          expandable
        />
        {data.notInDirectory.length > 0 && <NotInDirectoryTable rows={data.notInDirectory} query={q} />}
        <DataQualityNotes quality={data.quality} period={period} />
      </CollapsedSections>
    </SalesFrame>
  );
}
