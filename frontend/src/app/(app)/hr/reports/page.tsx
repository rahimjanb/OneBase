import Link from "next/link";
import { Download } from "lucide-react";
import { HrFilters } from "@/components/hr/HrBits";
import { Empty, HrHeader, loadHr, SyncBanner } from "@/components/hr/HrPage";
import { PageBody } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { dateParam, dayLabel, divisionOptions, duration, hours, type HrReport, type HrTimesheetRow, param, percentText, qs, todayTashkent } from "@/lib/hr";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Отчёты · HR · OneBase") };
}

type Search = Promise<Record<string, string | string[] | undefined>>;

/** «Отчёты»: посещаемость по подразделениям за период, частые опоздания и отсутствия, выгрузка в Excel. */
export default async function HrReportsPage({ searchParams }: { searchParams: Search }) {
  const sp = await searchParams;
  const ctx = await loadHr("/hr/reports");
  const t = await getT();
  const today = todayTashkent();
  const to = dateParam(sp.to) ?? today;
  const from = dateParam(sp.from) ?? `${to.slice(0, 8)}01`;
  const divisionId = param(sp.divisionId);
  const report = await apiGet<HrReport>(`/api/hr/reports${qs({ from, to, divisionId })}`, "/hr/reports");
  const s = report.total;

  const tiles: [string, string, string][] = [
    ["Сотрудников", String(report.employees), "text-ink"],
    ["Явка", percentText(report.attendanceRate), "text-ok"],
    ["Без опозданий", percentText(report.punctuality), "text-accent-strong"],
    ["Опозданий", `${s.lateDays} · ${duration(s.lateMinutes, t)}`, s.lateDays > 0 ? "text-warn" : "text-ink"],
    ["Отсутствий", String(s.absentDays), s.absentDays > 0 ? "text-bad" : "text-ink"],
    ["Дней по документам", String(s.leaveDays), "text-violet-light"],
    ["Отработано, ч", `${hours(s.workedMinutes)} / ${hours(s.planMinutes)}`, "text-ink"],
  ];

  const People = ({ title, rows, value }: { title: string; rows: HrTimesheetRow[]; value: (r: HrTimesheetRow) => string }) => (
    <section className="min-w-0 rounded-xl border border-line bg-surface p-4 sm:p-5">
      <h2 className="mb-3 text-sm font-semibold text-ink">{t(title)}</h2>
      {rows.length === 0 ? (
        <p className="py-4 text-center text-sm text-ink-3">{t("Нет.")}</p>
      ) : (
        <ol className="divide-y divide-line text-sm">
          {rows.map((r) => (
            <li key={r.employeeId} className="flex items-center gap-3 py-2">
              <Link href={`/hr/employees/${r.employeeId}${qs({ from: report.from, to: report.to })}`} className="min-w-0 flex-1 truncate text-ink hover:text-accent-strong">
                {r.employee}
                {r.division && <span className="ml-2 text-xs text-ink-3">{r.division}</span>}
              </Link>
              <span className="shrink-0 tabular-nums text-ink-2">{value(r)}</span>
            </li>
          ))}
        </ol>
      )}
    </section>
  );

  return (
    <>
      <HrHeader
        title="Отчёты"
        subtitle="Посещаемость и дисциплина по подразделениям за период"
        actions={
          <a
            href={`/bff/api/hr/reports/export${qs({ from: report.from, to: report.to, divisionId })}`}
            className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted"
          >
            <Download className="size-4" /> {t("Скачать Excel")}
          </a>
        }
      />
      <PageBody>
        <SyncBanner sync={report.sync} manages={ctx.managesIntegration} />
        <HrFilters
          fields={[
            { kind: "date", name: "from", label: "С", value: report.from },
            { kind: "date", name: "to", label: "По", value: report.to },
            { kind: "select", name: "divisionId", label: "Подразделение", all: "Все подразделения", options: divisionOptions(ctx.divisions) },
          ]}
        />
        <p className="mb-3 text-sm font-semibold text-ink">{t("Период: {from} — {to}", { from: dayLabel(report.from, true), to: dayLabel(report.to, true) })}</p>
        <div className="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-4 xl:grid-cols-7">
          {tiles.map(([label, value, tone]) => (
            <div key={label} className="rounded-xl border border-line bg-surface p-3">
              <div className={`text-lg font-semibold tabular-nums ${tone}`}>{value}</div>
              <div className="text-xs text-ink-3">{t(label)}</div>
            </div>
          ))}
        </div>

        {report.divisions.length === 0 ? (
          <Empty text="За период данных нет." />
        ) : (
          <div className="overflow-x-auto rounded-xl border border-line bg-surface">
            <table className="w-full min-w-[860px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="px-4 py-2.5 font-semibold">{t("Подразделение")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Сотрудников")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Рабочих дней")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Явка")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Без опозданий")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Опозданий")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Ранних уходов")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("Отсутствий")}</th>
                  <th className="px-3 py-2.5 text-right font-semibold">{t("По документам")}</th>
                  <th className="px-4 py-2.5 text-right font-semibold">{t("Отработано, ч")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {report.divisions.map((g) => (
                  <tr key={g.divisionId ?? "none"}>
                    <td className="px-4 py-2 font-medium text-ink">{t(g.division)}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{g.employees}</td>
                    <td className="px-3 py-2 text-right tabular-nums text-ink-2">{g.summary.planDays}</td>
                    <td className="px-3 py-2 text-right tabular-nums text-ok">{percentText(g.attendanceRate)}</td>
                    <td className="px-3 py-2 text-right tabular-nums">{percentText(g.punctuality)}</td>
                    <td className={`px-3 py-2 text-right tabular-nums ${g.summary.lateDays > 0 ? "text-warn" : "text-ink-3"}`}>{g.summary.lateDays}</td>
                    <td className="px-3 py-2 text-right tabular-nums text-ink-2">{g.summary.earlyDays}</td>
                    <td className={`px-3 py-2 text-right tabular-nums ${g.summary.absentDays > 0 ? "font-semibold text-bad" : "text-ink-3"}`}>{g.summary.absentDays}</td>
                    <td className="px-3 py-2 text-right tabular-nums text-ink-2">{g.summary.leaveDays}</td>
                    <td className="px-4 py-2 text-right tabular-nums">{hours(g.summary.workedMinutes)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="mt-4 grid gap-4 lg:grid-cols-2">
          <People title="Чаще всего опаздывали" rows={report.topLate} value={(r) => t("{n} раз · {time}", { n: r.summary.lateDays, time: duration(r.summary.lateMinutes, t) })} />
          <People title="Больше всего отсутствий" rows={report.topAbsent} value={(r) => t("{n} дн.", { n: r.summary.absentDays })} />
        </div>
        <p className="mt-3 text-xs text-ink-3">
          {t("Явка — доля рабочих дней с приходом среди дней, где итог ясен (пришёл или отсутствовал). В Excel — итоги подразделений, табель сотрудников по дням и список отклонений.")}
        </p>
      </PageBody>
    </>
  );
}
