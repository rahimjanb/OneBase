"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { ArrowUpDown, ChevronDown, ChevronRight, Search } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import styles from "@/components/hr/timesheet/timesheet.module.css";
import { useT } from "@/i18n/client";
import type { HrReportGroup } from "@/lib/hr";
import {
  absentTone,
  attendanceTone,
  countTone,
  docsTone,
  hours1,
  lateTone,
  matchesName,
  num,
  onTimeTone,
  pct,
  sortGroups,
  strongTone,
  toneBar,
  toneText,
  type SortKey,
  type Tone,
} from "@/lib/hr-report";
import { card, focus } from "../overview/bits";
import { rowId, type Sort } from "./DivisionsTable";

/** Сколько отделов видно сразу; дальше — «Показать все». */
export const MOBILE_ROWS = 8;

export const MOBILE_SORTS: { value: string; sort: Sort; label: string }[] = [
  { value: "attendance-asc", sort: { key: "attendance", dir: "asc" }, label: "Явка ↑" },
  { value: "attendance-desc", sort: { key: "attendance", dir: "desc" }, label: "Явка ↓" },
  { value: "late-desc", sort: { key: "late", dir: "desc" }, label: "Опоздания ↓" },
  { value: "absent-desc", sort: { key: "absent", dir: "desc" }, label: "Отсутствия ↓" },
  { value: "worked-desc", sort: { key: "worked", dir: "desc" }, label: "Отработано ↓" },
  { value: "name-asc", sort: { key: "name", dir: "asc" }, label: "Название А–Я" },
];

const sortValue = (s: Sort) => MOBILE_SORTS.find((o) => o.sort.key === s.key && o.sort.dir === s.dir)?.value ?? `${s.key}-${s.dir}`;

function Meta({ value, label, tone }: { value: string; label: string; tone: Tone }) {
  return (
    <span className={`${toneText[tone]} ${strongTone(tone) ? "font-semibold" : ""}`}>
      {value} {label}
    </span>
  );
}

/**
 * «Подразделения» на телефоне: поиск, сортировка (по умолчанию «Явка ↑» — худшие сверху), 8 отделов и «Показать все». Строка — явка
 * числом и полосой, под ней опоздания, отсутствия и «без опозданий» цветом правил. Тап — шторка со всеми колонками отдела.
 */
export function DivisionsList({
  groups,
  sort,
  onSort,
  expanded,
  onExpand,
  highlight,
  single,
  hrefOf,
}: {
  groups: HrReportGroup[];
  sort: Sort;
  onSort: (sort: Sort) => void;
  expanded: boolean;
  onExpand: () => void;
  highlight: string | null;
  single: boolean;
  hrefOf: (g: HrReportGroup) => string | null;
}) {
  const t = useT();
  const [query, setQuery] = useState("");
  const [open, setOpen] = useState<HrReportGroup | null>(null);
  const rows = useMemo(() => sortGroups(groups.filter((g) => matchesName(t(g.division), query)), sort.key, sort.dir), [groups, query, sort, t]);
  const visible = expanded || query.trim() ? rows : rows.slice(0, MOBILE_ROWS);
  const current = MOBILE_SORTS.find((o) => sortValue(o.sort) === sortValue(sort)) ?? MOBILE_SORTS[0];

  return (
    <section aria-labelledby="divisions-m-title" className={`${card} flex flex-col md:hidden`}>
      <div className="flex flex-col gap-3 px-4 pb-2 pt-4">
        <div className="flex items-center justify-between gap-3">
          <h2 id="divisions-m-title" className="flex items-center gap-2 text-base font-semibold text-(--ts-text)">
            {t("Подразделения")}
            {!single && <span className="text-[13px] font-medium tabular-nums text-(--ts-subtle)">{num(groups.length)}</span>}
          </h2>
          {!single && (
            <label className={`relative inline-flex h-8 shrink-0 items-center gap-1.5 rounded-[9px] border border-(--ts-border-strong) px-2.5 text-[13px] font-medium text-(--ts-text-2) focus-within:ring-2 focus-within:ring-(--ts-accent)`}>
              <ArrowUpDown aria-hidden className="size-3.5 text-(--ts-subtle)" />
              {t(current.label)}
              <select
                aria-label={t("Сортировка")}
                value={current.value}
                onChange={(e) => {
                  const next = MOBILE_SORTS.find((o) => o.value === e.target.value);
                  if (next) onSort(next.sort);
                }}
                className="absolute inset-0 cursor-pointer opacity-0"
              >
                {MOBILE_SORTS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {t(o.label)}
                  </option>
                ))}
              </select>
            </label>
          )}
        </div>
        {!single && (
          <label className="relative block">
            <span className="sr-only">{t("Найти подразделение")}</span>
            <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-(--ts-subtle)" />
            <input
              type="search"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder={t("Найти подразделение")}
              className="h-11 w-full rounded-[10px] border border-(--ts-border-strong) bg-(--ts-surface) pl-9 pr-3 text-[16px] text-(--ts-text) placeholder:text-[14px] placeholder:text-(--ts-subtle) focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent)"
            />
          </label>
        )}
      </div>

      {rows.length === 0 ? (
        <p className="px-4 py-6 text-center text-[13px] text-(--ts-muted)">{t("Ничего не найдено")}</p>
      ) : (
        <ul>
          {visible.map((g) => {
            const s = g.summary;
            const tone = attendanceTone(g.attendanceRate);
            const lit = highlight === rowId(g);
            return (
              <li key={rowId(g)} id={`m-${rowId(g)}`} className="scroll-mt-4 border-t border-(--ts-border) first:border-t-0">
                <button
                  type="button"
                  onClick={() => setOpen(g)}
                  className={`flex w-full cursor-pointer flex-col gap-2 px-4 py-3 text-left transition-colors duration-300 ${lit ? "bg-(--ts-accent-soft)" : ""} ${focus}`}
                >
                  <span className="flex items-baseline justify-between gap-3">
                    <span className="min-w-0">
                      <span className="text-sm font-medium text-(--ts-text)">{t(g.division)}</span>{" "}
                      <span className="whitespace-nowrap text-xs text-(--ts-subtle)">{t("{n} сотр.", { n: num(g.employees) })}</span>
                    </span>
                    <span className={`shrink-0 text-sm font-bold tabular-nums ${toneText[tone]}`}>{pct(g.attendanceRate)}</span>
                  </span>
                  <span className="block h-[5px] overflow-hidden rounded-full bg-(--ts-surface-2)">
                    <span className={`block h-full rounded-full ${toneBar[tone]}`} style={{ width: `${Math.max(0, Math.min(100, g.attendanceRate ?? 0))}%` }} />
                  </span>
                  <span className="flex flex-wrap gap-x-2.5 gap-y-0.5 text-xs tabular-nums text-(--ts-muted)">
                    <Meta value={num(s.lateDays)} label={t("опозд.")} tone={lateTone(s.lateDays)} />
                    <Meta value={num(s.absentDays)} label={t("отсут.")} tone={absentTone(s.absentDays)} />
                    <span className={toneText[onTimeTone(g.punctuality)]}>
                      {t("без опозд.")} {pct(g.punctuality)}
                    </span>
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      )}

      {!expanded && !query.trim() && rows.length > MOBILE_ROWS && (
        <button
          type="button"
          onClick={onExpand}
          className={`flex h-12 cursor-pointer items-center justify-center gap-1 border-t border-(--ts-border) text-sm font-semibold text-(--ts-link) ${focus}`}
        >
          {t("Показать все {n}", { n: num(rows.length) })}
          <ChevronDown aria-hidden className="size-4" />
        </button>
      )}

      <DivisionSheet group={open} onClose={() => setOpen(null)} href={open ? hrefOf(open) : null} />
    </section>
  );
}

/** Шторка отдела: все колонки таблицы и переход в табель за тот же период. */
function DivisionSheet({ group, onClose, href }: { group: HrReportGroup | null; onClose: () => void; href: string | null }) {
  const t = useT();
  if (!group) return null;
  const s = group.summary;
  const rows: [string, React.ReactNode][] = [
    [t("Сотрудников"), num(group.employees)],
    [t("Рабочих дней"), num(s.planDays)],
    [t("Явка"), <span key="a" className={`font-bold ${toneText[attendanceTone(group.attendanceRate)]}`}>{pct(group.attendanceRate)}</span>],
    [t("Без опозданий"), <span key="o" className={toneText[onTimeTone(group.punctuality)]}>{pct(group.punctuality)}</span>],
    [t("Опоздания"), <Meta key="l" value={num(s.lateDays)} label="" tone={lateTone(s.lateDays)} />],
    [t("Ранние уходы"), <Meta key="e" value={num(s.earlyDays)} label="" tone={countTone(s.earlyDays)} />],
    [t("Отсутствия"), <Meta key="ab" value={num(s.absentDays)} label="" tone={absentTone(s.absentDays)} />],
    [t("По документам"), <Meta key="d" value={num(s.leaveDays)} label="" tone={docsTone(s.leaveDays)} />],
    [t("Отработано, ч"), hours1(s.workedMinutes)],
  ];
  return (
    <Sheet open onClose={onClose} title={t(group.division)} subtitle={t("{n} сотр.", { n: num(group.employees) })}>
      <div className={`${styles.root} flex flex-col gap-3`} style={{ background: "transparent" }}>
        <dl className="flex flex-col">
          {rows.map(([label, value]) => (
            <div key={label} className="flex min-h-11 items-center justify-between gap-3 border-b border-(--ts-border) text-[15px] last:border-b-0">
              <dt className="text-(--ts-muted)">{label}</dt>
              <dd className="font-semibold tabular-nums text-(--ts-text)">{value}</dd>
            </div>
          ))}
        </dl>
        {href && (
          <Link href={href} className={`flex h-12 items-center justify-center gap-1 rounded-[12px] bg-(--ts-accent) text-[15px] font-semibold text-(--ts-on-accent) ${focus}`}>
            {t("Табель отдела за период")}
            <ChevronRight aria-hidden className="size-4" />
          </Link>
        )}
      </div>
    </Sheet>
  );
}
