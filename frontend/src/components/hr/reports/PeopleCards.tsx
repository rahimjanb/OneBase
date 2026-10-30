"use client";

import Link from "next/link";
import { useState } from "react";
import { AlarmClock, ChevronDown, ChevronRight, CircleCheck, Info, TriangleAlert, UserX, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import { qs, type HrTimesheetRow } from "@/lib/hr";
import { displayName, initialsOf } from "@/lib/hr-employee";
import { absentWhole, durationText, lateAverage, num, REPORT_RULES, suspiciousLate, timesText } from "@/lib/hr-report";
import { card, focus, Tip } from "../overview/bits";

/** Сколько строк видно на телефоне; дальше — «Весь топ-10». */
const MOBILE_TOP = 5;

function Head({ id, icon: Icon, tone, title, subtitle, children }: { id: string; icon: LucideIcon; tone: "warning" | "danger"; title: string; subtitle: string; children?: React.ReactNode }) {
  return (
    <div className="flex items-start gap-3 px-4 pb-3 md:px-5 md:pb-3.5">
      <span aria-hidden className={`grid size-8 shrink-0 place-items-center rounded-[9px] ${tone === "warning" ? "bg-(--ts-warning-soft) text-(--ts-warning)" : "bg-(--ts-danger-soft) text-(--ts-danger)"}`}>
        <Icon className="size-4" />
      </span>
      <div className="min-w-0 flex-1">
        <h2 id={id} className="text-[15px] font-semibold leading-snug text-(--ts-text)">
          {title}
        </h2>
        <p className="text-xs text-(--ts-muted)">{subtitle}</p>
      </div>
      {children}
    </div>
  );
}

function Person({ row, index, href, right, hidden }: { row: HrTimesheetRow; index: number; href: string; right: React.ReactNode; hidden: boolean }) {
  const t = useT();
  const name = displayName(row.employee);
  return (
    <li className={`border-t border-(--ts-border) ${hidden ? "max-md:hidden" : ""}`}>
      <Link href={href} className={`flex items-center gap-3 px-4 py-2.5 transition-colors duration-150 hover:bg-(--ts-row-hover) md:py-[9px] md:pl-5 md:pr-5 ${focus}`}>
        <span className="w-4 shrink-0 text-xs font-semibold tabular-nums text-(--ts-subtle)">{index + 1}</span>
        <span aria-hidden className="hidden size-[30px] shrink-0 place-items-center rounded-full bg-(--ts-surface-2) text-[11px] font-semibold text-(--ts-muted) md:grid">
          {initialsOf(name)}
        </span>
        <span className="min-w-0 flex-1">
          <span className="block text-[13px] font-medium leading-snug text-(--ts-text) md:truncate">{name}</span>
          <span className="block truncate text-[11px] text-(--ts-subtle)">{row.division ? t(row.division) : t("Без подразделения")}</span>
        </span>
        {right}
      </Link>
    </li>
  );
}

function NoViolations() {
  const t = useT();
  return (
    <p className="flex flex-1 items-center justify-center gap-2 border-t border-(--ts-border) px-5 py-8 text-sm font-medium text-(--ts-success)">
      <CircleCheck aria-hidden className="size-4" />
      {t("Нарушений нет")}
    </p>
  );
}

function MoreButton({ onClick }: { onClick: () => void }) {
  const t = useT();
  return (
    <button type="button" onClick={onClick} className={`flex h-12 cursor-pointer items-center justify-center gap-1 border-t border-(--ts-border) text-sm font-semibold text-(--ts-link) md:hidden ${focus}`}>
      {t("Весь топ-10")}
      <ChevronDown aria-hidden className="size-4" />
    </button>
  );
}

/**
 * «Чаще всего опаздывали»: топ-10 за период по разам или по времени (переключатель, данные с сервера — оба топа сразу). Справа — сколько
 * раз и сколько всего, под ними — среднее за раз. Среднее больше 4 ч — значок «Проверьте график сотрудника»: так выглядят смены вне графика.
 * Телефон — первые 5 и «Весь топ-10».
 */
export function LateCard({ byCount, byTime, period }: { byCount: HrTimesheetRow[]; byTime: HrTimesheetRow[]; period: { from: string; to: string } }) {
  const t = useT();
  const [mode, setMode] = useState<"count" | "time">("count");
  const [all, setAll] = useState(false);
  const rows = mode === "count" ? byCount : byTime;

  return (
    <section aria-labelledby="late-title" className={`${card} flex flex-col pt-4`}>
      <Head id="late-title" icon={AlarmClock} tone="warning" title={t("Чаще всего опаздывали")} subtitle={t("Топ-10 сотрудников за период")}>
        {byCount.length > 0 && (
          <div role="radiogroup" aria-label={t("Порядок")} className="hidden shrink-0 rounded-[9px] border border-(--ts-border) p-0.5 md:flex">
            {(
              [
                ["count", "по разам"],
                ["time", "по времени"],
              ] as const
            ).map(([key, label]) => (
              <button
                key={key}
                type="button"
                role="radio"
                aria-checked={mode === key}
                onClick={() => setMode(key)}
                className={`h-7 cursor-pointer rounded-[7px] px-2.5 text-xs font-medium transition-colors duration-150 ${focus} ${mode === key ? "bg-(--ts-surface-2) text-(--ts-text)" : "text-(--ts-muted) hover:text-(--ts-text-2)"}`}
              >
                {t(label)}
              </button>
            ))}
          </div>
        )}
      </Head>
      {rows.length === 0 ? (
        <NoViolations />
      ) : (
        <ol className="flex-1">
          {rows.map((r, i) => {
            const s = r.summary;
            const avg = lateAverage(s);
            const longTotal = s.lateMinutes >= REPORT_RULES.lateHoursHigh * 60;
            return (
              <Person
                key={r.employeeId}
                row={r}
                index={i}
                hidden={!all && i >= MOBILE_TOP}
                href={`/hr/employees/${r.employeeId}${qs(period)}`}
                right={
                  <span className="flex shrink-0 items-center gap-2">
                    {suspiciousLate(s) && (
                      <span className="group/tip relative">
                        <TriangleAlert aria-label={t("Проверьте график сотрудника")} className="size-4 text-(--ts-warning)" />
                        <Tip align="end">{t("Проверьте график сотрудника")}</Tip>
                      </span>
                    )}
                    <span className="flex flex-col items-end gap-0.5 text-right tabular-nums">
                      <span className="flex items-baseline gap-2 whitespace-nowrap">
                        <span className="text-[13px] font-bold text-(--ts-warning)">{timesText(s.lateDays, t)}</span>
                        <span className={`text-[11px] font-semibold md:text-[13px] ${longTotal ? "text-(--ts-danger)" : "text-(--ts-text)"} max-md:hidden`}>
                          {durationText(s.lateMinutes, t)}
                        </span>
                      </span>
                      <span className={`whitespace-nowrap text-[11px] font-medium md:hidden ${longTotal ? "text-(--ts-danger)" : "text-(--ts-muted)"}`}>{durationText(s.lateMinutes, t)}</span>
                      <span className={`hidden whitespace-nowrap text-[11px] font-medium md:block ${avg >= REPORT_RULES.lateAvgHigh ? "text-(--ts-danger)" : "text-(--ts-subtle)"}`}>
                        {t("в ср. {time} за раз", { time: durationText(avg, t) })}
                      </span>
                    </span>
                  </span>
                }
              />
            );
          })}
        </ol>
      )}
      {!all && rows.length > MOBILE_TOP && <MoreButton onClick={() => setAll(true)} />}
      <div className="h-2 md:h-3" />
    </section>
  );
}

/**
 * «Больше всего отсутствий»: дней без прихода и без документа. «весь период» — сотрудник за период ни разу не пришёл (только отсутствия);
 * полоса — доля отсутствий от его рабочих дней. Если такие люди есть — подсказка проверить увольнение или отпуск без документа.
 */
export function AbsentCard({ rows, wholePeriod, period, allHref }: { rows: HrTimesheetRow[]; wholePeriod: number; period: { from: string; to: string }; allHref: string }) {
  const t = useT();
  const [all, setAll] = useState(false);
  const whole = rows.filter((r) => absentWhole(r));
  const days = new Set(whole.map((r) => r.summary.absentDays));
  const sameDays = days.size === 1 && whole.length === wholePeriod ? [...days][0] : null;
  const hint =
    wholePeriod > 0
      ? `${t(plural(wholePeriod, ["{n} сотрудник не приходил", "{n} сотрудника не приходили", "{n} сотрудников не приходили"]), { n: num(wholePeriod) })} ${
          sameDays != null
            ? t(plural(sameDays, ["все {d} рабочий день", "все {d} рабочих дня", "все {d} рабочих дней"]), { d: num(sameDays) })
            : t("ни одного рабочего дня за период")
        } — ${t("проверьте увольнение или отпуск без документа")}`
      : null;

  return (
    <section aria-labelledby="absent-title" className={`${card} flex flex-col pt-4`}>
      <Head id="absent-title" icon={UserX} tone="danger" title={t("Больше всего отсутствий")} subtitle={t("Дней без прихода и без документа")}>
        {rows.length > 0 && (
          <Link href={allHref} className={`hidden shrink-0 items-center gap-0.5 rounded text-[13px] font-semibold text-(--ts-link) hover:underline md:inline-flex ${focus}`}>
            {t("Все")}
            <ChevronRight aria-hidden className="size-4" />
          </Link>
        )}
      </Head>
      {hint && (
        <p className="mx-4 mb-3 flex gap-2 rounded-[12px] bg-(--ts-warning-soft) px-3 py-2.5 text-xs leading-snug text-(--ts-text-2) md:hidden">
          <Info aria-hidden className="mt-px size-4 shrink-0 text-(--ts-warning)" />
          {hint}
        </p>
      )}
      {rows.length === 0 ? (
        <NoViolations />
      ) : (
        <ol className="flex-1">
          {rows.map((r, i) => {
            const s = r.summary;
            const share = s.planDays > 0 ? Math.min(100, (100 * s.absentDays) / s.planDays) : 0;
            return (
              <Person
                key={r.employeeId}
                row={r}
                index={i}
                hidden={!all && i >= MOBILE_TOP}
                href={`/hr/employees/${r.employeeId}${qs(period)}`}
                right={
                  <span className="flex shrink-0 items-center gap-3">
                    {absentWhole(r) && (
                      <span className="hidden rounded-[6px] bg-(--ts-danger-soft) px-1.5 py-0.5 text-[10.5px] font-semibold text-(--ts-danger) md:inline">{t("весь период")}</span>
                    )}
                    <span aria-hidden className="block h-1 w-12 overflow-hidden rounded-full bg-(--ts-surface-2) md:h-[5px] md:w-16">
                      <span className="block h-full rounded-full bg-(--ts-danger)" style={{ width: `${share}%` }} />
                    </span>
                    <span className="w-[52px] text-right text-[13px] font-bold tabular-nums text-(--ts-text)">
                      {t("{n} дн.", { n: num(s.absentDays) })}
                      {absentWhole(r) && <span className="sr-only"> · {t("весь период")}</span>}
                    </span>
                  </span>
                }
              />
            );
          })}
        </ol>
      )}
      {!all && rows.length > MOBILE_TOP && <MoreButton onClick={() => setAll(true)} />}
      {hint && (
        <p className="mt-auto hidden items-start gap-2 border-t border-(--ts-border) px-5 py-3 text-xs text-(--ts-text-2) md:flex">
          <Info aria-hidden className="mt-px size-4 shrink-0 text-(--ts-warning)" />
          {hint}
        </p>
      )}
      {!hint && <div className="h-2 md:h-3" />}
    </section>
  );
}
