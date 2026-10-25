import Link from "next/link";
import { DayCode, HrFilters } from "@/components/hr/HrBits";
import { Empty, HrHeader, loadHr, SyncBanner } from "@/components/hr/HrPage";
import { PageBody } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { dateParam, dayCodeLegend, dayLabel, divisionOptions, duration, hours, type HrTimesheet, param, qs, todayTashkent, weekday } from "@/lib/hr";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Рабочее время и табели · HR · OneBase") };
}

type Search = Promise<Record<string, string | string[] | undefined>>;

/** «Рабочее время и табели»: по сотруднику — буква каждого дня, явка, опоздания, отсутствия, отработано и план за период (до 62 дней). */
export default async function HrTimesheetsPage({ searchParams }: { searchParams: Search }) {
  const sp = await searchParams;
  const ctx = await loadHr("/hr/timesheets");
  const t = await getT();
  const today = todayTashkent();
  const to = dateParam(sp.to) ?? today;
  const from = dateParam(sp.from) ?? `${to.slice(0, 8)}01`;
  const sheet = await apiGet<HrTimesheet>(`/api/hr/timesheet${qs({ from, to, divisionId: param(sp.divisionId), search: param(sp.search) })}`, "/hr/timesheets");
  const total = sheet.total;

  return (
    <>
      <HrHeader title="Рабочее время и табели" subtitle="Табель посещаемости по графикам Verifix: явка, опоздания, отсутствия и отработанные часы" />
      <PageBody>
        <SyncBanner sync={sheet.sync} manages={ctx.managesIntegration} />
        <HrFilters
          fields={[
            { kind: "date", name: "from", label: "С", value: sheet.from },
            { kind: "date", name: "to", label: "По", value: sheet.to },
            { kind: "select", name: "divisionId", label: "Подразделение", all: "Все подразделения", options: divisionOptions(ctx.divisions) },
            { kind: "search", name: "search", label: "Сотрудник", placeholder: "ФИО" },
          ]}
        />
        <div className="mb-3 flex flex-wrap gap-x-4 gap-y-1 text-sm text-ink-2">
          <span>{t("сотрудников: {n}", { n: sheet.rows.length })}</span>
          <span>{t("рабочих дней по графику: {n}", { n: total.planDays })}</span>
          <span className="text-ok">{t("с приходом: {n}", { n: total.presentDays })}</span>
          <span className="text-warn">{t("опозданий: {n}", { n: total.lateDays })}</span>
          <span className="text-bad">{t("отсутствий: {n}", { n: total.absentDays })}</span>
          <span>{t("отработано: {h} ч из {plan} ч по плану", { h: hours(total.workedMinutes), plan: hours(total.planMinutes) })}</span>
        </div>

        {sheet.rows.length === 0 ? (
          <Empty text="Сотрудников за период нет." />
        ) : (
          <div className="overflow-x-auto rounded-xl border border-line bg-surface">
            <table className="text-xs">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="sticky left-0 z-10 min-w-52 bg-surface px-4 py-2 font-semibold">{t("Сотрудник")}</th>
                  {sheet.dates.map((d) => (
                    <th key={d} className={`px-0.5 py-2 text-center font-semibold ${["сб", "вс"].includes(weekday(d)) ? "text-ink-4" : ""}`}>
                      <div className="tabular-nums">{d.slice(8, 10)}</div>
                      <div className="font-normal normal-case">{t(weekday(d))}</div>
                    </th>
                  ))}
                  <th className="px-2 py-2 text-right font-semibold">{t("Явка")}</th>
                  <th className="px-2 py-2 text-right font-semibold">{t("Опозд.")}</th>
                  <th className="px-2 py-2 text-right font-semibold">{t("Отс.")}</th>
                  <th className="px-2 py-2 text-right font-semibold">{t("Докум.")}</th>
                  <th className="px-2 py-2 text-right font-semibold">{t("Отработано")}</th>
                  <th className="px-4 py-2 text-right font-semibold">{t("План, ч")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {sheet.rows.map((r) => (
                  <tr key={r.employeeId}>
                    <td className="sticky left-0 z-10 bg-surface px-4 py-1.5">
                      <Link href={`/hr/employees/${r.employeeId}${qs({ from: sheet.from, to: sheet.to })}`} className="font-medium text-ink hover:text-accent-strong">
                        {r.employee}
                      </Link>
                      {r.division && <div className="text-[11px] text-ink-3">{r.division}</div>}
                    </td>
                    {r.codes.map((code, i) => (
                      <td key={sheet.dates[i]} className="px-0.5 py-1.5 text-center" title={`${dayLabel(sheet.dates[i], true)}: ${code || "—"}`}>
                        <DayCode code={code} />
                      </td>
                    ))}
                    <td className="px-2 py-1.5 text-right tabular-nums">{r.summary.presentDays}</td>
                    <td className={`px-2 py-1.5 text-right tabular-nums ${r.summary.lateDays > 0 ? "text-warn" : "text-ink-3"}`} title={duration(r.summary.lateMinutes, t)}>
                      {r.summary.lateDays}
                    </td>
                    <td className={`px-2 py-1.5 text-right tabular-nums ${r.summary.absentDays > 0 ? "font-semibold text-bad" : "text-ink-3"}`}>{r.summary.absentDays}</td>
                    <td className="px-2 py-1.5 text-right tabular-nums text-ink-2">{r.summary.leaveDays}</td>
                    <td className="px-2 py-1.5 text-right tabular-nums">{hours(r.summary.workedMinutes)}</td>
                    <td className="px-4 py-1.5 text-right tabular-nums text-ink-3">{hours(r.summary.planMinutes)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <ul className="mt-3 flex flex-wrap gap-x-4 gap-y-1.5 text-xs text-ink-3">
          {dayCodeLegend.map(([code, label]) => (
            <li key={code} className="flex items-center gap-1.5">
              <DayCode code={code.split(/[,/ ]/)[0]} /> <span>{code.includes(",") || code.includes("/") ? `${code} — ` : ""}{t(label)}</span>
            </li>
          ))}
        </ul>
        <p className="mt-2 text-xs text-ink-3">
          {t("Отработано — от первого прихода до последнего ухода без обеденного перерыва по графику. Табель — расчёт OneBase по данным Verifix; утверждённый табель ведётся в Verifix.")}
        </p>
      </PageBody>
    </>
  );
}
