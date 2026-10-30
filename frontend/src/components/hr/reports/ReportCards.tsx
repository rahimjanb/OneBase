"use client";

import { useRef, useState } from "react";
import { AlarmClock, CircleCheck, TrendingDown, UserX, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import type { HrReport, HrReportGroup } from "@/lib/hr";
import {
  attendanceTone,
  departmentsText,
  docDaysText,
  durationText,
  employeesText,
  hours1,
  hoursInt,
  inDivisionsText,
  num,
  pct,
  toneBar,
  toneText,
  topDivisions,
  workedShare,
  type Tone,
  type TopKind,
} from "@/lib/hr-report";
import { card, focus } from "../overview/bits";

export type SortTarget = "late" | "absent";

function Bar({ value, tone, height = "h-1" }: { value: number | null; tone: Tone; height?: string }) {
  return (
    <span className={`block ${height} w-full overflow-hidden rounded-full bg-(--ts-surface-2)`}>
      <span className={`block h-full rounded-full ${toneBar[tone]}`} style={{ width: `${Math.max(0, Math.min(100, value ?? 0))}%` }} />
    </span>
  );
}

/** unit — единица после числа («ч»): мельче числа, чтобы длинная подпись на узбекском («soat») не переносила значение. */
type Kpi = { key: string; label: string; value: string; unit?: string; tone: Tone; bar?: { value: number | null; tone: Tone }; sub: string; sort?: SortTarget };

/**
 * KPI периода. Ноутбук — 6 карточек в ряд, планшет — по 3; телефон — «Явка за период» крупно и 2×2 (подписи короче). «Опоздания» и
 * «Отсутствия» — кнопки: таблица подразделений сортируется по этой колонке по убыванию.
 */
export function KpiCards({ report, onSort }: { report: HrReport; onSort: (target: SortTarget) => void }) {
  const t = useT();
  const s = report.total;
  const worked = workedShare(s);
  const attendance = attendanceTone(report.attendanceRate);
  const kpis: Kpi[] = [
    { key: "employees", label: t("Сотрудников"), value: num(report.employees), tone: "text", sub: inDivisionsText(report.divisions.length, t) },
    { key: "attendance", label: t("Явка"), value: pct(report.attendanceRate), tone: "text", bar: { value: report.attendanceRate, tone: attendance }, sub: t("среди дней с ясным итогом") },
    { key: "onTime", label: t("Без опозданий"), value: pct(report.punctuality), tone: "text", bar: { value: report.punctuality, tone: "warning" }, sub: t("смен начаты вовремя") },
    { key: "late", label: t("Опоздания"), value: num(s.lateDays), tone: s.lateDays > 0 ? "warning" : "text", sub: t("{time} суммарно", { time: durationText(s.lateMinutes, t) }), sort: "late" },
    {
      key: "absent",
      label: t("Отсутствия"),
      value: num(s.absentDays),
      tone: s.absentDays > 0 ? "danger" : "text",
      sub: s.leaveDays > 0 ? docDaysText(s.leaveDays, t) : t("по документам — нет"),
      sort: "absent",
    },
    {
      key: "worked",
      label: t("Отработано"),
      value: hoursInt(s.workedMinutes),
      unit: t("ч"),
      tone: "text",
      bar: { value: worked, tone: "accent" },
      sub: worked == null ? t("графика нет") : t("из {plan} ч по графику · {share}", { plan: hoursInt(s.planMinutes), share: pct(Math.round(worked * 10) / 10) }),
    },
  ];

  const body = (k: Kpi) => (
    <>
      <span className="text-xs font-medium text-(--ts-muted)">{k.label}</span>
      <span className={`mt-1.5 whitespace-nowrap text-2xl font-bold leading-tight tabular-nums ${toneText[k.tone]}`}>
        {k.value}
        {k.unit && <span className="ml-1 text-lg">{k.unit}</span>}
      </span>
      <span className="mt-auto flex flex-col gap-2 pt-2">
        {k.bar && <Bar value={k.bar.value} tone={k.bar.tone} />}
        <span className="line-clamp-2 text-[11px] font-medium leading-snug text-(--ts-subtle)">{k.sub}</span>
      </span>
    </>
  );

  // Телефон: короткие подписи 2×2.
  const mobile: Kpi[] = [
    { ...kpis[2], sub: t("смен вовремя"), bar: undefined },
    { ...kpis[3], sub: durationText(s.lateMinutes, t) },
    { ...kpis[4], sub: s.leaveDays > 0 ? t(plural(s.leaveDays, ["+{n} день по док.", "+{n} дня по док.", "+{n} дней по док."]), { n: num(s.leaveDays) }) : t("по документам — нет") },
    { ...kpis[5], sub: worked == null ? t("графика нет") : t("{share} от графика", { share: pct(Math.round(worked * 10) / 10) }), bar: undefined },
  ];

  return (
    <>
      <div className="hidden gap-3 md:grid md:grid-cols-3 lg:grid-cols-6">
        {kpis.map((k) =>
          k.sort ? (
            <button
              key={k.key}
              type="button"
              onClick={() => onSort(k.sort!)}
              title={t("Сортировать подразделения по убыванию")}
              className={`${card} flex min-h-[112px] min-w-0 cursor-pointer flex-col px-[18px] py-4 text-left transition-colors duration-150 hover:border-(--ts-border-strong) ${focus}`}
            >
              {body(k)}
            </button>
          ) : (
            <div key={k.key} className={`${card} flex min-h-[112px] min-w-0 flex-col px-[18px] py-4`}>
              {body(k)}
            </div>
          ),
        )}
      </div>

      <div className="flex flex-col gap-2.5 md:hidden">
        <div className={`${card} flex flex-col gap-1 p-4`}>
          <div className="flex items-baseline justify-between gap-3">
            <span className="text-[13px] font-medium text-(--ts-muted)">{t("Явка за период")}</span>
            <span className="truncate text-xs text-(--ts-subtle)">
              {employeesText(report.employees, t)} · {departmentsText(report.divisions.length, t)}
            </span>
          </div>
          <div className="flex items-baseline gap-2.5">
            <span className={`text-[36px] font-bold leading-[1.15] tracking-[-0.5px] tabular-nums ${toneText.text}`}>{pct(report.attendanceRate)}</span>
            <span className="text-xs text-(--ts-subtle)">{t("дни с ясным итогом")}</span>
          </div>
          <span className="mt-2">
            <Bar value={report.attendanceRate} tone={attendance} height="h-1.5" />
          </span>
        </div>
        <div className="grid grid-cols-2 gap-2.5">
          {mobile.map((k) => {
            const inner = (
              <>
                <span className="text-xs font-medium text-(--ts-muted)">{k.label}</span>
                <span className={`mt-1 whitespace-nowrap text-[22px] font-bold leading-tight tabular-nums ${toneText[k.tone]}`}>
                  {k.value}
                  {k.unit && <span className="ml-1 text-lg">{k.unit}</span>}
                </span>
                <span className="mt-1 truncate text-[11px] font-medium text-(--ts-subtle)">{k.sub}</span>
              </>
            );
            return k.sort ? (
              <button key={k.key} type="button" onClick={() => onSort(k.sort!)} className={`${card} flex min-w-0 cursor-pointer flex-col p-3.5 text-left ${focus}`}>
                {inner}
              </button>
            ) : (
              <div key={k.key} className={`${card} flex min-w-0 flex-col p-3.5`}>
                {inner}
              </div>
            );
          })}
        </div>
      </div>
    </>
  );
}

const TOPS: { kind: TopKind; title: string; icon: LucideIcon; tone: "danger" | "warning" }[] = [
  { kind: "attendance", title: "Низкая явка", icon: TrendingDown, tone: "danger" },
  { kind: "late", title: "Больше всего опозданий", icon: AlarmClock, tone: "warning" },
  { kind: "absent", title: "Больше всего отсутствий", icon: UserX, tone: "danger" },
];

const tile = { danger: "bg-(--ts-danger-soft) text-(--ts-danger)", warning: "bg-(--ts-warning-soft) text-(--ts-warning)" };

function TopCard({ kind, title, icon: Icon, tone, groups, onPick, className = "" }: (typeof TOPS)[number] & { groups: HrReportGroup[]; onPick: (g: HrReportGroup) => void; className?: string }) {
  const t = useT();
  const rows = topDivisions(groups, kind);
  return (
    <section aria-label={t(title)} className={`${card} flex flex-col gap-3 px-[18px] py-4 ${className}`}>
      <h2 className="flex items-center gap-2.5 text-sm font-semibold text-(--ts-text)">
        <span aria-hidden className={`grid size-7 shrink-0 place-items-center rounded-[8px] ${tile[tone]}`}>
          <Icon className="size-4" />
        </span>
        <span className="min-w-0 leading-snug">{t(title)}</span>
      </h2>
      {rows.length === 0 ? (
        <p className="flex items-center gap-2 py-2 text-[13px] font-medium text-(--ts-success)">
          <CircleCheck aria-hidden className="size-4" />
          {t("Нарушений нет")}
        </p>
      ) : (
        <ol className="flex flex-col gap-0.5">
          {rows.map((r, i) => (
            <li key={r.group.divisionId ?? "none"}>
              <button
                type="button"
                onClick={() => onPick(r.group)}
                className={`-mx-1.5 flex w-[calc(100%+12px)] cursor-pointer items-center gap-2.5 rounded-[7px] px-1.5 py-1 text-left text-[13px] transition-colors duration-150 hover:bg-(--ts-surface-2) ${focus}`}
              >
                <span className="w-3.5 shrink-0 text-xs font-semibold text-(--ts-subtle)">{i + 1}</span>
                <span className="min-w-0 flex-1 truncate font-medium text-(--ts-text-2)">{t(r.group.division)}</span>
                <span className={`shrink-0 font-semibold tabular-nums ${toneText[kind === "attendance" ? attendanceTone(r.value) : tone]}`}>
                  {kind === "attendance" ? pct(r.value) : num(r.value)}
                </span>
              </button>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}

/**
 * Топ-3 подразделений: «Низкая явка», «Больше всего опозданий», «Больше всего отсутствий». Ноутбук и планшет — три карточки в ряд;
 * телефон — карусель (листается пальцем, точки показывают карточку). Щелчок по отделу — его строка в таблице (прокрутка и подсветка).
 */
export function TopDivisions({ groups, onPick }: { groups: HrReportGroup[]; onPick: (g: HrReportGroup) => void }) {
  const t = useT();
  const strip = useRef<HTMLDivElement>(null);
  const [index, setIndex] = useState(0);

  const onScroll = () => {
    const el = strip.current;
    if (!el) return;
    const width = el.firstElementChild instanceof HTMLElement ? el.firstElementChild.offsetWidth + 12 : el.clientWidth;
    setIndex(Math.max(0, Math.min(TOPS.length - 1, Math.round(el.scrollLeft / width))));
  };
  const go = (i: number) => {
    const el = strip.current;
    const item = el?.children[i];
    if (el && item instanceof HTMLElement) el.scrollTo({ left: item.offsetLeft - el.offsetLeft - 16, behavior: "smooth" });
  };

  return (
    <>
      <div className="hidden gap-4 md:grid md:grid-cols-3">
        {TOPS.map((top) => (
          <TopCard key={top.kind} {...top} groups={groups} onPick={onPick} />
        ))}
      </div>
      <div className="-mx-4 flex flex-col gap-3 md:hidden">
        <div
          ref={strip}
          onScroll={onScroll}
          className="flex snap-x snap-mandatory gap-3 overflow-x-auto scroll-px-4 px-4 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden"
        >
          {TOPS.map((top) => (
            <TopCard key={top.kind} {...top} groups={groups} onPick={onPick} className="w-[290px] max-w-[82vw] shrink-0 snap-start" />
          ))}
        </div>
        <div role="tablist" aria-label={t("Топ подразделений")} className="flex justify-center gap-1.5">
          {TOPS.map((top, i) => (
            <button
              key={top.kind}
              type="button"
              role="tab"
              aria-selected={index === i}
              aria-label={t(top.title)}
              onClick={() => go(i)}
              className={`h-1.5 cursor-pointer rounded-full transition-all duration-200 ${index === i ? "w-4 bg-(--ts-accent)" : "w-1.5 bg-(--ts-border-strong)"}`}
            />
          ))}
        </div>
      </div>
    </>
  );
}

/** Телефон: «Итого за период» — то, что не вошло в карточки. */
export function TotalsCard({ report }: { report: HrReport }) {
  const t = useT();
  const s = report.total;
  const rows: [string, string][] = [
    [t("Рабочих дней"), num(s.planDays)],
    [t("Ранние уходы"), num(s.earlyDays)],
    [t("Дней по документам"), num(s.leaveDays)],
    [t("Отработано / по графику"), t("{worked} / {plan} ч", { worked: hours1(s.workedMinutes), plan: hoursInt(s.planMinutes) })],
  ];
  return (
    <section className="rounded-[16px] border border-(--ts-border) bg-(--ts-surface-2) px-4 py-3.5 md:hidden">
      <h2 className="mb-2 text-sm font-semibold text-(--ts-text)">{t("Итого за период")}</h2>
      <dl className="flex flex-col gap-1.5 text-[13px]">
        {rows.map(([label, value]) => (
          <div key={label} className="flex items-baseline justify-between gap-3">
            <dt className="text-(--ts-muted)">{label}</dt>
            <dd className="font-semibold tabular-nums text-(--ts-text)">{value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
