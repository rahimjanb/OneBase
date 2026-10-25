import Link from "next/link";
import { DayBadges, HrFilters, Pager } from "@/components/hr/HrBits";
import { Empty, HrHeader, loadHr, SyncBanner } from "@/components/hr/HrPage";
import { HrEventsTable } from "@/components/hr/HrTables";
import { PageBody } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { addDays, clock, dateParam, dayLabel, divisionOptions, duration, type HrDayRow, type HrEvent, type PageResult, param, qs, todayTashkent, weekday } from "@/lib/hr";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Посещаемость · HR · OneBase") };
}

type Search = Promise<Record<string, string | string[] | undefined>>;

const statuses = [
  { value: "present", label: "Пришли" },
  { value: "late", label: "Опоздали" },
  { value: "absent", label: "Отсутствуют" },
  { value: "leave", label: "По документу" },
  { value: "expected", label: "Ожидаются" },
  { value: "unknown", label: "Не ясно" },
  { value: "dayoff", label: "Выходной" },
  { value: "noschedule", label: "Нет графика" },
];

/** «Посещаемость»: итог дня по каждому сотруднику (график, приход, уход, опоздание, отработано) или журнал проходов за период. */
export default async function HrAttendancePage({ searchParams }: { searchParams: Search }) {
  const sp = await searchParams;
  const ctx = await loadHr("/hr/attendance");
  const t = await getT();
  const view = param(sp.view) === "events" ? "events" : "day";
  const today = todayTashkent();
  const divisions = divisionOptions(ctx.divisions);

  const tabs = (
    <div className="mb-4 inline-flex rounded-lg border border-line bg-surface p-0.5 text-sm">
      {[
        { key: "day", label: "Итоги дня", href: "/hr/attendance" },
        { key: "events", label: "Журнал проходов", href: "/hr/attendance?view=events" },
      ].map((tab) => (
        <Link
          key={tab.key}
          href={tab.href}
          aria-current={view === tab.key ? "page" : undefined}
          className={`rounded-md px-3 py-1.5 font-medium ${view === tab.key ? "bg-accent text-white" : "text-ink-2 hover:text-ink"}`}
        >
          {t(tab.label)}
        </Link>
      ))}
    </div>
  );

  if (view === "events") {
    const from = dateParam(sp.from) ?? today;
    const to = dateParam(sp.to) ?? from;
    const events = await apiGet<PageResult<HrEvent>>(
      `/api/hr/events${qs({ from, to, divisionId: param(sp.divisionId), page: param(sp.page), pageSize: 100 })}`,
      "/hr/attendance",
    );
    return (
      <>
        <HrHeader title="Посещаемость" subtitle="Проходы турникетов и отметки терминалов Verifix" />
        <PageBody>
          <SyncBanner sync={ctx.sync} manages={ctx.managesIntegration} />
          {tabs}
          <HrFilters
            keep={{ view: "events" }}
            fields={[
              { kind: "date", name: "from", label: "С", value: from },
              { kind: "date", name: "to", label: "По", value: to },
              { kind: "select", name: "divisionId", label: "Подразделение", all: "Все подразделения", options: divisions },
            ]}
          />
          {events.items.length === 0 ? <Empty text="За период проходов нет." /> : <HrEventsTable events={events.items} />}
          <Pager page={events.page} pageSize={events.pageSize} total={events.total} />
          <p className="mt-3 text-xs text-ink-3">
            {t("Тип отметки — как в Verifix на момент загрузки (может измениться — тогда обновится та же строка). Отметка, исчезнувшая из Verifix, показана бледной и в расчёте не участвует.")}
          </p>
        </PageBody>
      </>
    );
  }

  const date = dateParam(sp.date) ?? today;
  const rows = await apiGet<HrDayRow[]>(
    `/api/hr/attendance${qs({ date, divisionId: param(sp.divisionId), status: param(sp.status), search: param(sp.search) })}`,
    "/hr/attendance",
  );
  const counts = rows.reduce(
    (acc, r) => {
      acc[r.day.status] = (acc[r.day.status] ?? 0) + 1;
      if (r.day.late) acc.late = (acc.late ?? 0) + 1;
      return acc;
    },
    {} as Record<string, number>,
  );

  return (
    <>
      <HrHeader title="Посещаемость" subtitle="Итог дня по графику Verifix: приход, уход, опоздания и отработанное время" />
      <PageBody>
        <SyncBanner sync={ctx.sync} manages={ctx.managesIntegration} />
        {tabs}
        <HrFilters
          fields={[
            { kind: "date", name: "date", label: "День", value: date },
            { kind: "select", name: "divisionId", label: "Подразделение", all: "Все подразделения", options: divisions },
            { kind: "select", name: "status", label: "Итог", all: "Все", options: statuses },
            { kind: "search", name: "search", label: "Сотрудник", placeholder: "ФИО" },
          ]}
        />
        <div className="mb-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-ink-2">
          <span className="font-semibold text-ink">
            {dayLabel(date, true)} ({t(weekday(date))})
          </span>
          <span>{t("пришли: {n}", { n: counts.Present ?? 0 })}</span>
          <span className="text-warn">{t("опоздали: {n}", { n: counts.late ?? 0 })}</span>
          <span className="text-bad">{t("отсутствуют: {n}", { n: counts.Absent ?? 0 })}</span>
          <span>{t("по документу: {n}", { n: counts.OnLeave ?? 0 })}</span>
          <span className="text-ink-3">{t("не ясно: {n}", { n: counts.Unknown ?? 0 })}</span>
          <span className="ml-auto flex gap-2 text-xs">
            <Link href={`/hr/attendance${qs({ date: addDays(date, -1), divisionId: param(sp.divisionId), status: param(sp.status) })}`} className="text-accent-strong hover:underline">
              ← {t("предыдущий день")}
            </Link>
            {date < today && (
              <Link href={`/hr/attendance${qs({ date: addDays(date, 1), divisionId: param(sp.divisionId), status: param(sp.status) })}`} className="text-accent-strong hover:underline">
                {t("следующий день")} →
              </Link>
            )}
          </span>
        </div>

        {rows.length === 0 ? (
          <Empty text="Сотрудников по этому фильтру нет." />
        ) : (
          <div className="overflow-x-auto rounded-xl border border-line bg-surface">
            <table className="w-full min-w-[900px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="px-4 py-2.5 font-semibold">{t("Сотрудник")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Итог")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("График")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Приход")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Уход")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Опоздание")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Отработано")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Отметок")}</th>
                  <th className="px-4 py-2.5 font-semibold">{t("Пояснение")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {rows.map((r) => (
                  <tr key={r.employeeId} className="hover:bg-muted/40">
                    <td className="px-4 py-2">
                      <Link href={`/hr/employees/${r.employeeId}`} className="font-medium text-ink hover:text-accent-strong">
                        {r.employee}
                      </Link>
                      <div className="text-xs text-ink-3">{[r.division, r.job].filter(Boolean).join(" · ")}</div>
                    </td>
                    <td className="px-3 py-2">
                      <DayBadges day={r.day} />
                    </td>
                    <td className="whitespace-nowrap px-3 py-2 tabular-nums text-ink-2">{r.day.planBegin ? `${clock(r.day.planBegin)}–${clock(r.day.planEnd)}` : "—"}</td>
                    <td className="px-3 py-2 tabular-nums">{clock(r.day.firstIn)}</td>
                    <td className="px-3 py-2 tabular-nums">{clock(r.day.lastOut)}</td>
                    <td className={`px-3 py-2 text-right tabular-nums ${r.day.late ? "font-semibold text-warn" : "text-ink-3"}`}>{r.day.late ? duration(r.day.lateMinutes, t) : "—"}</td>
                    <td className="px-3 py-2 text-right tabular-nums text-ink-2">{duration(r.day.workedMinutes, t)}</td>
                    <td className="px-3 py-2 text-right tabular-nums text-ink-3">{r.day.events}</td>
                    <td className="px-4 py-2 text-xs text-ink-3">{r.day.reason ? t(r.day.reason) : ""}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </PageBody>
    </>
  );
}
