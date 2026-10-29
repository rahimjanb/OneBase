"use client";

import { useEffect, useRef, useState } from "react";
import { Calendar as CalendarIcon, ChevronDown, ChevronLeft, ChevronRight } from "lucide-react";
import { formatDate } from "@/i18n/core";
import { useLocale, useT } from "@/i18n/client";
import { addMonths, monthGrid, monthStart } from "@/components/hr/timesheet/DateRangePicker";
import { fullDate } from "@/lib/hr-timesheet";
import { focus } from "./bits";

const weekdays = ["пн", "вт", "ср", "чт", "пт", "сб", "вс"];

/** Календарь месяца: один щелчок — день; будущие дни недоступны. «Сегодня» — быстрый возврат. */
export function DayCalendar({ value, today, onPick }: { value: string; today: string; onPick: (day: string) => void }) {
  const t = useT();
  const locale = useLocale();
  const [month, setMonth] = useState(monthStart(value));
  const title = formatDate(new Date(`${month}T12:00:00Z`), locale, { month: "long", year: "numeric" });

  return (
    <div>
      <div className="mb-2 flex items-center justify-between">
        <button type="button" aria-label={t("Предыдущий месяц")} onClick={() => setMonth(addMonths(month, -1))} className={`grid size-8 cursor-pointer place-items-center rounded-[8px] text-(--ts-muted) hover:bg-(--ts-surface-2) ${focus}`}>
          <ChevronLeft className="size-4" />
        </button>
        <span className="text-[13.5px] font-semibold capitalize text-(--ts-text)">{title}</span>
        <button
          type="button"
          aria-label={t("Следующий месяц")}
          disabled={addMonths(month, 1) > today}
          onClick={() => setMonth(addMonths(month, 1))}
          className={`grid size-8 cursor-pointer place-items-center rounded-[8px] text-(--ts-muted) hover:bg-(--ts-surface-2) disabled:cursor-default disabled:opacity-30 ${focus}`}
        >
          <ChevronRight className="size-4" />
        </button>
      </div>
      <div className="grid grid-cols-7 gap-y-0.5 text-center">
        {weekdays.map((w) => (
          <span key={w} className="py-1 text-[11px] font-medium text-(--ts-subtle)">
            {t(w)}
          </span>
        ))}
        {monthGrid(month).map((day, i) => {
          if (!day) return <span key={`e${i}`} />;
          const selected = day === value;
          return (
            <button
              key={day}
              type="button"
              disabled={day > today}
              onClick={() => onPick(day)}
              aria-label={fullDate(day)}
              aria-pressed={selected}
              className={`h-8 cursor-pointer rounded-[7px] text-[12.5px] tabular-nums transition-colors duration-150 disabled:cursor-default disabled:text-(--ts-border-strong) ${focus} ${
                selected
                  ? "bg-(--ts-accent) font-semibold text-(--ts-on-accent)"
                  : `text-(--ts-text-2) hover:bg-(--ts-surface-2) ${day === today ? "font-semibold text-(--ts-accent)" : ""}`
              }`}
            >
              {Number(day.slice(8, 10))}
            </button>
          );
        })}
      </div>
      {value !== today && (
        <button type="button" onClick={() => onPick(today)} className={`mt-2 w-full cursor-pointer rounded-[8px] py-1.5 text-[12.5px] font-medium text-(--ts-link) hover:bg-(--ts-surface-2) ${focus}`}>
          {t("Сегодня")}
        </button>
      )}
    </div>
  );
}

/** Подпись выбранного дня: «Сегодня», «Вчера» или «29 окт.». */
export function useDayLabel() {
  const t = useT();
  const locale = useLocale();
  return (day: string, today: string, yesterday: string) =>
    day === today ? t("Сегодня") : day === yesterday ? t("Вчера") : formatDate(new Date(`${day}T12:00:00Z`), locale, { day: "numeric", month: "short", timeZone: "UTC" });
}

/** Кнопка «Сегодня ▾» (вторичная) с календарём во всплывающем окне. */
export function DayPickerButton({ value, today, yesterday, onChange }: { value: string; today: string; yesterday: string; onChange: (day: string) => void }) {
  const t = useT();
  const label = useDayLabel();
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => !box.current?.contains(e.target as Node) && setOpen(false);
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <div ref={box} className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-haspopup="dialog"
        aria-label={t("День: {day}", { day: fullDate(value) })}
        className={`inline-flex h-10 cursor-pointer items-center gap-2 rounded-[10px] border border-(--ts-border-strong) bg-(--ts-surface) px-3.5 text-sm font-medium text-(--ts-text) transition-colors duration-150 hover:bg-(--ts-surface-2) ${focus}`}
      >
        <CalendarIcon aria-hidden className="size-4 text-(--ts-muted)" />
        {label(value, today, yesterday)}
        <ChevronDown aria-hidden className="size-4 text-(--ts-subtle)" />
      </button>
      {open && (
        <div role="dialog" aria-label={t("Выбор дня")} className="absolute right-0 top-[calc(100%+6px)] z-40 w-[292px] rounded-[12px] border border-(--ts-border) bg-(--ts-surface) p-3 shadow-[0_12px_32px_#0e13221f]">
          <DayCalendar
            value={value}
            today={today}
            onPick={(day) => {
              setOpen(false);
              onChange(day);
            }}
          />
        </div>
      )}
    </div>
  );
}
