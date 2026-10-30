"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useMemo, useState } from "react";
import { ArrowDown, ArrowUp, Search } from "lucide-react";
import { useT } from "@/i18n/client";
import type { HrReportGroup } from "@/lib/hr";
import {
  absentTone,
  attendanceTone,
  countTone,
  divisionsText,
  docsTone,
  hours1,
  lateTone,
  lowAttendance,
  matchesName,
  num,
  onTimeTone,
  pct,
  REPORT_RULES,
  sortGroups,
  strongTone,
  toneBar,
  toneText,
  totalOf,
  type SortKey,
  type Tone,
} from "@/lib/hr-report";
import { card, focus } from "../overview/bits";

export type Sort = { key: SortKey; dir: "asc" | "desc" };

/** Первое направление по щелчку: название — А–Я, явка и «без опозданий» — худшие сверху, количества — больше сверху. */
export const firstDir = (key: SortKey): "asc" | "desc" => (key === "name" || key === "attendance" || key === "onTime" ? "asc" : "desc");

/**
 * Колонки. Раскладка таблицы — автоматическая: числа занимают столько, сколько им нужно (подписи переносятся по словам и не режутся),
 * название получает не меньше четверти ширины, явка — полосу фиксированной ширины.
 */
const COLUMNS: { key: SortKey; label: string; align: "left" | "right"; width: string }[] = [
  { key: "name", label: "Подразделение", align: "left", width: "w-[26%] min-w-[170px]" },
  { key: "employees", label: "Сотр.", align: "right", width: "" },
  { key: "days", label: "Раб. дней", align: "right", width: "" },
  { key: "attendance", label: "Явка", align: "left", width: "w-[168px] 2xl:w-[200px]" },
  { key: "onTime", label: "Без опозданий", align: "right", width: "" },
  { key: "late", label: "Опоздания", align: "right", width: "" },
  { key: "early", label: "Ранние уходы", align: "right", width: "" },
  { key: "absent", label: "Отсутствия", align: "right", width: "" },
  { key: "docs", label: "По док.", align: "right", width: "" },
  { key: "worked", label: "Отработано, ч", align: "right", width: "" },
];

export const rowId = (g: HrReportGroup) => `div-${g.divisionId ?? "none"}`;

function Count({ value, tone }: { value: number; tone: Tone }) {
  return <span className={`${toneText[tone]} ${strongTone(tone) ? "font-semibold" : ""}`}>{num(value)}</span>;
}

/** Явка: полоса и процент цветом правила (число есть всегда — цвет не единственный носитель смысла). */
export function AttendanceCell({ value, compact = false }: { value: number | null; compact?: boolean }) {
  const tone = attendanceTone(value);
  return (
    <span className="flex items-center gap-2.5">
      <span className={`block h-1.5 min-w-[48px] flex-1 overflow-hidden rounded-full bg-(--ts-surface-2) ${compact ? "" : "2xl:max-w-[130px]"}`}>
        <span className={`block h-full rounded-full ${toneBar[tone]}`} style={{ width: `${Math.max(0, Math.min(100, value ?? 0))}%` }} />
      </span>
      <span className={`w-[46px] shrink-0 text-right text-[13px] font-semibold ${toneText[tone]}`}>{pct(value)}</span>
    </span>
  );
}

const legend: [Tone, string][] = [
  ["success", `≥ ${REPORT_RULES.attendance.good}%`],
  ["warning", `${REPORT_RULES.attendance.warn}–${REPORT_RULES.attendance.good}%`],
  ["danger", `< ${REPORT_RULES.attendance.warn}%`],
];

/**
 * «По подразделениям» (ноутбук и планшет): поиск, легенда цвета явки, сортировка по любой колонке (aria-sort), итог по видимым строкам
 * (суммы и средневзвешенные проценты). Шапка таблицы прилипает к верху при прокрутке страницы. Планшет — горизонтальная прокрутка
 * с закреплённой первой колонкой. Строка — переход в табель отдела за тот же период; отдел из топа подсвечивается.
 */
export function DivisionsTable({
  groups,
  sort,
  onSort,
  highlight,
  single,
  hrefOf,
}: {
  groups: HrReportGroup[];
  sort: Sort;
  onSort: (key: SortKey) => void;
  highlight: string | null;
  single: boolean;
  hrefOf: (g: HrReportGroup) => string | null;
}) {
  const t = useT();
  const router = useRouter();
  const [query, setQuery] = useState("");
  const rows = useMemo(() => sortGroups(groups.filter((g) => matchesName(t(g.division), query)), sort.key, sort.dir), [groups, query, sort, t]);
  const total = totalOf(rows);

  const stickyFirst = "max-lg:sticky max-lg:left-0 max-lg:z-[1]";
  const cell = "whitespace-nowrap px-1.5 py-[11px] tabular-nums first:pl-5 first:whitespace-normal last:pr-5";

  return (
    <section aria-labelledby="divisions-title" className={`${card} hidden overflow-clip md:block`}>
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2.5 px-5 py-3.5">
        <h2 id="divisions-title" className="flex items-center gap-2 text-[15px] font-semibold text-(--ts-text)">
          {t("По подразделениям")}
          {!single && <span className="rounded-[6px] bg-(--ts-surface-2) px-1.5 py-0.5 text-xs font-semibold tabular-nums text-(--ts-muted)">{num(groups.length)}</span>}
        </h2>
        <div className="flex items-center gap-4">
          <ul aria-label={t("Цвет явки")} className="hidden items-center gap-3 text-[11px] font-medium text-(--ts-muted) lg:flex">
            {legend.map(([tone, label]) => (
              <li key={tone} className="flex items-center gap-1.5">
                <span aria-hidden className={`size-2 rounded-[2px] ${toneBar[tone]}`} />
                {label}
              </li>
            ))}
          </ul>
          {!single && (
            <label className="relative block w-[240px]">
              <span className="sr-only">{t("Найти подразделение")}</span>
              <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-(--ts-subtle)" />
              <input
                type="search"
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder={t("Найти подразделение")}
                className="h-9 w-full rounded-[9px] border border-(--ts-border-strong) bg-(--ts-surface) pl-9 pr-3 text-[13px] text-(--ts-text) placeholder:text-(--ts-subtle) focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent)"
              />
            </label>
          )}
        </div>
      </div>

      <div className="max-lg:overflow-x-auto">
        <table className="w-full text-sm max-lg:min-w-[960px]">
          <caption className="sr-only">{t("Посещаемость по подразделениям")}</caption>
          <thead>
            <tr>
              {COLUMNS.map((c, i) => {
                const active = sort.key === c.key;
                const Arrow = sort.dir === "asc" ? ArrowUp : ArrowDown;
                return (
                  <th
                    key={c.key}
                    scope="col"
                    aria-sort={active ? (sort.dir === "asc" ? "ascending" : "descending") : "none"}
                    className={`sticky top-0 z-[2] border-y border-(--ts-border) bg-(--ts-surface-2) px-1.5 py-2.5 align-bottom first:pl-5 last:pr-5 ${c.width} ${c.align === "right" ? "text-right" : "text-left"} ${i === 0 ? "max-lg:left-0 max-lg:z-[3] max-lg:w-[200px]" : ""}`}
                  >
                    <button
                      type="button"
                      onClick={() => onSort(c.key)}
                      className={`relative cursor-pointer rounded text-[11px] font-semibold uppercase leading-tight tracking-[0.5px] transition-colors duration-150 hover:text-(--ts-text-2) ${c.align === "right" ? "text-right" : "text-left"} ${active ? "text-(--ts-text-2)" : "text-(--ts-subtle)"} ${focus}`}
                    >
                      {t(c.label)}
                      {/* Стрелка — вне потока: у числовых колонок слева от подписи, у остальных — справа; места под неё колонки не держат. */}
                      {active && (
                        <Arrow
                          aria-hidden
                          className={`absolute bottom-px size-3 text-(--ts-accent) ${c.align === "right" ? "right-[calc(100%+3px)]" : "left-[calc(100%+3px)]"}`}
                          strokeWidth={2.5}
                        />
                      )}
                    </button>
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && (
              <tr>
                <td colSpan={COLUMNS.length} className="px-5 py-8 text-center text-[13px] text-(--ts-muted)">
                  {t("Ничего не найдено")}
                </td>
              </tr>
            )}
            {rows.map((g) => {
              const href = hrefOf(g);
              const low = lowAttendance(g.attendanceRate);
              const lit = highlight === rowId(g);
              const bg = lit ? "bg-(--ts-accent-soft)" : low ? "bg-(--ts-danger)/[0.03]" : "";
              const stickyBg = lit
                ? "max-lg:bg-[color-mix(in_srgb,var(--ts-accent)_12%,var(--ts-surface))]"
                : low
                  ? "max-lg:bg-[color-mix(in_srgb,var(--ts-danger)_3%,var(--ts-surface))]"
                  : "max-lg:bg-(--ts-surface) max-lg:group-hover:bg-(--ts-row-hover)";
              const s = g.summary;
              return (
                <tr
                  key={rowId(g)}
                  id={rowId(g)}
                  onClick={href ? () => router.push(href) : undefined}
                  className={`group scroll-mt-14 border-b border-(--ts-border) transition-colors duration-300 last:border-b-0 ${bg} ${href ? "cursor-pointer hover:bg-(--ts-row-hover)" : ""}`}
                >
                  <th scope="row" className={`${cell} ${stickyFirst} ${stickyBg} text-left text-sm font-medium text-(--ts-text)`}>
                    {href ? (
                      <Link href={href} onClick={(e) => e.stopPropagation()} className={`rounded hover:text-(--ts-link) ${focus}`}>
                        {t(g.division)}
                      </Link>
                    ) : (
                      t(g.division)
                    )}
                  </th>
                  <td className={`${cell} text-right text-(--ts-muted)`}>{num(g.employees)}</td>
                  <td className={`${cell} text-right text-(--ts-muted)`}>{num(s.planDays)}</td>
                  <td className={cell}>
                    <AttendanceCell value={g.attendanceRate} />
                  </td>
                  <td className={`${cell} text-right ${toneText[onTimeTone(g.punctuality)]}`}>{pct(g.punctuality)}</td>
                  <td className={`${cell} text-right`}>
                    <Count value={s.lateDays} tone={lateTone(s.lateDays)} />
                  </td>
                  <td className={`${cell} text-right`}>
                    <Count value={s.earlyDays} tone={countTone(s.earlyDays)} />
                  </td>
                  <td className={`${cell} text-right`}>
                    <Count value={s.absentDays} tone={absentTone(s.absentDays)} />
                  </td>
                  <td className={`${cell} text-right`}>
                    <Count value={s.leaveDays} tone={docsTone(s.leaveDays)} />
                  </td>
                  <td className={`${cell} text-right text-sm font-semibold text-(--ts-text)`}>{hours1(s.workedMinutes)}</td>
                </tr>
              );
            })}
          </tbody>
          {rows.length > 0 && (
            <tfoot>
              <tr className="border-t border-(--ts-border) bg-(--ts-surface-2) text-[13px] font-bold text-(--ts-text)">
                <th scope="row" className={`${cell} ${stickyFirst} text-left max-lg:bg-(--ts-surface-2)`}>
                  {t("Итого · {n}", { n: divisionsText(rows.length, t) })}
                </th>
                <td className={`${cell} text-right`}>{num(total.employees)}</td>
                <td className={`${cell} text-right`}>{num(total.summary.planDays)}</td>
                <td className={`${cell} text-right ${toneText[attendanceTone(total.attendanceRate)]}`}>{pct(total.attendanceRate)}</td>
                <td className={`${cell} text-right`}>{pct(total.punctuality)}</td>
                <td className={`${cell} text-right ${total.summary.lateDays > 0 ? toneText.warning : ""}`}>{num(total.summary.lateDays)}</td>
                <td className={`${cell} text-right`}>{num(total.summary.earlyDays)}</td>
                <td className={`${cell} text-right ${total.summary.absentDays > 0 ? toneText.danger : ""}`}>{num(total.summary.absentDays)}</td>
                <td className={`${cell} text-right`}>{num(total.summary.leaveDays)}</td>
                <td className={`${cell} text-right`}>{hours1(total.summary.workedMinutes)}</td>
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </section>
  );
}
