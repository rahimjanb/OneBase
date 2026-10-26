"use client";

import Link from "next/link";
import { memo } from "react";
import { useT } from "@/i18n/client";
import type { HrTimesheetRow } from "@/lib/hr";
import { dayKind, dayKindLabel, decimal, nameParts } from "@/lib/hr-timesheet";
import { DayCodeButton } from "./DayCodeChip";

/** Колонки итогов: ширина и отступ справа для закрепления (sticky right). */
export const TOTALS = [
  { key: "present", label: "Явка", width: 60, right: 344 },
  { key: "late", label: "Опозд.", width: 64, right: 280 },
  { key: "absent", label: "Отс.", width: 56, right: 224 },
  { key: "leave", label: "Докум.", width: 64, right: 160 },
  { key: "worked", label: "Отработано / план", width: 160, right: 0 },
] as const;

export const DAY_WIDTH = 32;
export const TOTALS_WIDTH = TOTALS.reduce((a, c) => a + c.width, 0);

/** Сетка строки: сотрудник (не уже --ts-name-col), дни по 32 px, итоги. */
export const gridTemplate = (days: number) => `minmax(var(--ts-name-col), 1fr) repeat(${days}, ${DAY_WIDTH}px) ${TOTALS.map((c) => `${c.width}px`).join(" ")}`;
export const gridMinWidth = (days: number) => `calc(var(--ts-name-col) + ${days * DAY_WIDTH + TOTALS_WIDTH}px)`;

export type ColumnKind = "plain" | "weekend" | "today" | "selected";

/** Фон колонки дня на всю высоту: выбранный день, сегодня, выходной. */
export const columnBg: Record<ColumnKind, string> = {
  plain: "",
  weekend: "bg-(--ts-weekend) group-hover:bg-(--ts-weekend-hover)",
  today: "bg-(--ts-accent-soft)",
  selected: "bg-(--ts-surface-2)",
};

const sticky = "sticky z-10 bg-(--ts-surface) group-hover:bg-(--ts-row-hover)";

/** Цвет мини-бара отработанного: от 80 % плана — зелёный, от 50 % — жёлтый, меньше — красный. */
const barTone = (pct: number) => (pct >= 80 ? "bg-(--ts-success)" : pct >= 50 ? "bg-(--ts-warning)" : "bg-(--ts-danger)");

export type ChipHandlers = {
  onChipEnter: (row: HrTimesheetRow, index: number, el: HTMLButtonElement) => void;
  onChipLeave: () => void;
  /** keyboard — нажатие с клавиатуры (Enter, пробел): фокус уходит в карточку дня. */
  onChipClick: (row: HrTimesheetRow, index: number, el: HTMLButtonElement, keyboard: boolean) => void;
};

/**
 * Строка сотрудника: аватар с инициалами, «Фамилия Имя» и «Отчество · подразделение» (ссылка на карточку), чипы дней, итоги —
 * явка, опоздания, отсутствия (от 10 — бейджем), документы, отработано из плана с мини-баром.
 */
export const TimesheetRow = memo(function TimesheetRow({
  row,
  template,
  dates,
  columns,
  selectedIndex,
  dayLabels,
  density,
  shadowLeft,
  shadowRight,
  cardHref,
  last,
  handlers,
}: {
  row: HrTimesheetRow;
  template: string;
  dates: string[];
  columns: ColumnKind[];
  selectedIndex: number | null;
  dayLabels: string[];
  density: "normal" | "compact";
  shadowLeft: boolean;
  shadowRight: boolean;
  cardHref: string | null;
  last: boolean;
  handlers: ChipHandlers;
}) {
  const t = useT();
  const { name, patronymic, initials } = nameParts(row.employee);
  const s = row.summary;
  const worked = s.workedMinutes / 60;
  const plan = s.planMinutes / 60;
  const pct = plan > 0 ? (100 * worked) / plan : 0;
  const second = [patronymic, row.division].filter(Boolean).join(" · ");
  const person = (
    <>
      <span className="grid size-[30px] shrink-0 place-items-center rounded-full bg-(--ts-surface-2) text-[11px] font-semibold text-(--ts-muted)">{initials}</span>
      <span className="min-w-0">
        <span className="block truncate text-[13.5px] leading-[18px] font-medium text-(--ts-text)">{name}</span>
        {second && <span className="block truncate text-[11.5px] leading-4 text-(--ts-subtle)">{second}</span>}
      </span>
    </>
  );

  return (
    <div role="row" style={{ gridTemplateColumns: template }} className={`group grid items-center bg-(--ts-surface) transition-colors duration-150 hover:bg-(--ts-row-hover) ${density === "compact" ? "h-10" : "h-[52px]"} ${last ? "" : "border-b border-(--ts-border)"}`}>
      <div role="rowheader" className={`${sticky} left-0 flex h-full min-w-0 items-center px-5 ${shadowLeft ? "shadow-[6px_0_10px_-6px_var(--ts-sticky-shadow)]" : ""}`}>
        {cardHref ? (
          <Link href={cardHref} className="flex min-w-0 items-center gap-3 rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent)">
            {person}
          </Link>
        ) : (
          <span className="flex min-w-0 items-center gap-3">{person}</span>
        )}
      </div>
      {row.codes.map((code, i) => {
        const kind = dayKind(code);
        const column = selectedIndex === i ? "selected" : columns[i];
        return (
          <div key={dates[i]} role="cell" className={`grid h-full place-items-center ${columnBg[column]}`}>
            {kind !== "none" && (
              <DayCodeButton
                code={code}
                label={t("{date} — {status}", { date: dayLabels[i], status: t(dayKindLabel[kind]) })}
                selected={selectedIndex === i}
                onMouseEnter={(e) => handlers.onChipEnter(row, i, e.currentTarget)}
                onMouseLeave={handlers.onChipLeave}
                onClick={(e) => handlers.onChipClick(row, i, e.currentTarget, e.detail === 0)}
              />
            )}
          </div>
        );
      })}
      <div role="cell" style={{ right: 344 }} className={`${sticky} flex h-full items-center justify-end border-l border-(--ts-border) pr-3 text-[13.5px] font-semibold text-(--ts-text) tabular-nums ${shadowRight ? "shadow-[-6px_0_10px_-6px_var(--ts-sticky-shadow)]" : ""}`}>
        {s.presentDays}
      </div>
      <div role="cell" style={{ right: 280 }} className={`${sticky} flex h-full items-center justify-end pr-3 text-[13.5px] tabular-nums ${s.lateDays > 0 ? "font-semibold text-(--ts-warning)" : "text-(--ts-subtle)"}`}>
        {s.lateDays}
      </div>
      <div role="cell" style={{ right: 224 }} className={`${sticky} flex h-full items-center justify-end pr-3 text-[13.5px] tabular-nums`}>
        {s.absentDays >= 10 ? (
          <span className="rounded-md bg-(--ts-danger-soft) px-[7px] py-0.5 text-[13px] font-bold text-(--ts-danger)">{s.absentDays}</span>
        ) : (
          <span className={s.absentDays > 0 ? "font-semibold text-(--ts-danger)" : "text-(--ts-subtle)"}>{s.absentDays}</span>
        )}
      </div>
      <div role="cell" style={{ right: 160 }} className={`${sticky} flex h-full items-center justify-end pr-3 text-[13.5px] text-(--ts-subtle) tabular-nums`}>
        {s.leaveDays}
      </div>
      <div role="cell" style={{ right: 0 }} className={`${sticky} flex h-full flex-col items-end justify-center gap-1 pr-5 tabular-nums`}>
        <span className="whitespace-nowrap leading-4">
          <span className="text-[13.5px] font-semibold text-(--ts-text)">{decimal(worked, 1)}</span>
          <span className="text-xs text-(--ts-subtle)"> / {t("{h} ч", { h: decimal(plan, 1) })}</span>
        </span>
        <span className="block h-1 w-24 overflow-hidden rounded-sm bg-(--ts-surface-2)" aria-hidden>
          {worked > 0 && <span className={`block h-full rounded-sm ${barTone(pct)}`} style={{ width: `${Math.min(100, pct)}%` }} />}
        </span>
      </div>
    </div>
  );
});
