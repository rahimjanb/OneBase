"use client";

import { useState } from "react";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { formatDate } from "@/i18n/core";
import { useLocale, useT } from "@/i18n/client";
import { DateRangePicker } from "@/components/hr/timesheet/DateRangePicker";
import { presetOf, presetRange, shiftRange, type Preset } from "@/lib/hr-employee";
import { focus } from "../overview/bits";

const SEGMENTS: { key: Preset; label: string; short: string }[] = [
  { key: "week", label: "Неделя", short: "Неделя" },
  { key: "2weeks", label: "2 недели", short: "2 недели" },
  { key: "month", label: "Месяц", short: "Месяц" },
  { key: "custom", label: "Свой период", short: "Период" },
];

/** «17 – 30 октября 2026», «28 сентября – 4 октября 2026», «28 декабря 2025 – 3 января 2026». */
function useRangeLabel() {
  const locale = useLocale();
  return (from: string, to: string) => {
    const f = (d: string, o: Intl.DateTimeFormatOptions) => formatDate(new Date(`${d}T12:00:00Z`), locale, { ...o, timeZone: "UTC" });
    const year = to.slice(0, 4);
    if (from.slice(0, 7) === to.slice(0, 7)) return `${Number(from.slice(8, 10))} – ${f(to, { day: "numeric", month: "long" })} ${year}`;
    if (from.slice(0, 4) === year) return `${f(from, { day: "numeric", month: "long" })} – ${f(to, { day: "numeric", month: "long" })} ${year}`;
    return `${f(from, { day: "numeric", month: "long" })} ${from.slice(0, 4)} – ${f(to, { day: "numeric", month: "long" })} ${year}`;
  };
}

/**
 * Период карточки: «Неделя · 2 недели · Месяц · Свой период», стрелки и кнопка с датами (тот же календарь, что «Свой период»).
 * Кнопки «Показать» нет — смена периода сразу загружает данные; период хранится в адресе. Вперёд — не дальше сегодня.
 */
export function PeriodBar({ from, to, today, onChange }: { from: string; to: string; today: string; onChange: (from: string, to: string) => void }) {
  const t = useT();
  const label = useRangeLabel();
  const [picking, setPicking] = useState(false);
  const preset = presetOf(from, to, today);
  const atEnd = to >= today;

  const segment = (key: Preset) => {
    if (key === "custom") return setPicking(true);
    const [f, tt] = presetRange(key, to, today);
    onChange(f, tt);
  };
  const arrow = `grid size-[38px] shrink-0 cursor-pointer place-items-center rounded-[10px] border border-(--ts-border-strong) bg-(--ts-surface) text-(--ts-muted) transition-colors duration-150 hover:bg-(--ts-surface-2) disabled:cursor-default disabled:opacity-40 md:size-[34px] ${focus}`;

  return (
    <div className="flex flex-col gap-3 md:flex-row md:flex-wrap md:items-center md:justify-between">
      <div role="radiogroup" aria-label={t("Период")} className="grid grid-cols-4 rounded-[12px] border border-(--ts-border) bg-(--ts-surface) p-[3px] md:inline-flex">
        {SEGMENTS.map((s) => {
          const on = preset === s.key;
          return (
            <button
              key={s.key}
              type="button"
              role="radio"
              aria-checked={on}
              onClick={() => segment(s.key)}
              className={`h-[34px] cursor-pointer whitespace-nowrap rounded-[9px] border px-2 text-[13px] font-medium transition-colors duration-150 md:px-3.5 ${focus} ${
                on ? "border-(--ts-border-strong) bg-(--ts-surface-2) text-(--ts-text)" : "border-transparent text-(--ts-muted) hover:text-(--ts-text-2)"
              }`}
            >
              <span className="md:hidden">{t(s.short)}</span>
              <span className="max-md:hidden">{t(s.label)}</span>
            </button>
          );
        })}
      </div>

      <div className="flex items-center justify-between gap-2 md:justify-end">
        <button type="button" aria-label={t("Предыдущий период")} onClick={() => onChange(...shiftRange(from, to, preset, -1, today))} className={arrow}>
          <ChevronLeft className="size-4" />
        </button>
        <DateRangePicker
          from={from}
          to={to}
          today={today}
          onChange={onChange}
          open={picking}
          onOpenChange={setPicking}
          align="right"
          label={label(from, to)}
          className="flex h-[38px] items-center gap-2 rounded-[10px] border border-(--ts-border-strong) bg-(--ts-surface) px-3.5 text-sm font-medium text-(--ts-text) transition-colors duration-150 hover:bg-(--ts-surface-2) md:h-[34px]"
        />
        <button type="button" aria-label={t("Следующий период")} disabled={atEnd} onClick={() => onChange(...shiftRange(from, to, preset, 1, today))} className={arrow}>
          <ChevronRight className="size-4" />
        </button>
      </div>
    </div>
  );
}
