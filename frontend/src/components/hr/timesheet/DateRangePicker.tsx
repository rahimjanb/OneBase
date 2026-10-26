"use client";

import { useEffect, useRef, useState } from "react";
import { Calendar, ChevronLeft, ChevronRight } from "lucide-react";
import { formatDate } from "@/i18n/core";
import { useLocale, useT } from "@/i18n/client";
import { daysBetween, fullDate, MAX_PERIOD_DAYS, shiftDate } from "@/lib/hr-timesheet";

const focus = "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent)";
const weekdays = ["пн", "вт", "ср", "чт", "пт", "сб", "вс"];

const monthStart = (value: string) => `${value.slice(0, 8)}01`;
const addMonths = (value: string, n: number) => {
  const d = new Date(`${value.slice(0, 8)}01T12:00:00Z`);
  d.setUTCMonth(d.getUTCMonth() + n);
  return d.toISOString().slice(0, 10);
};

/** Дни сетки месяца с понедельника; null — пустые клетки до 1-го числа. */
function monthGrid(first: string): (string | null)[] {
  const d = new Date(`${first}T12:00:00Z`);
  const offset = (d.getUTCDay() + 6) % 7;
  const days = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 0)).getUTCDate();
  return [...Array.from({ length: offset }, () => null), ...Array.from({ length: days }, (_, i) => shiftDate(first, i))];
}

/**
 * Поле периода «01.10.2026 — 26.10.2026»: по нажатию — календарь, первый щелчок — начало, второй — конец (порядок любой). Будущие дни
 * недоступны, период — не больше 62 дней (предел табеля на сервере): дальше конец подрезается.
 */
export function DateRangePicker({ from, to, today, onChange }: { from: string; to: string; today: string; onChange: (from: string, to: string) => void }) {
  const t = useT();
  const locale = useLocale();
  const [open, setOpen] = useState(false);
  const [month, setMonth] = useState(monthStart(to));
  const [start, setStart] = useState<string | null>(null);
  const [hover, setHover] = useState<string | null>(null);
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

  const toggle = () => {
    setOpen((o) => !o);
    setMonth(monthStart(to));
    setStart(null);
    setHover(null);
  };

  const pick = (day: string) => {
    if (!start) {
      setStart(day);
      return;
    }
    let [a, b] = start <= day ? [start, day] : [day, start];
    if (daysBetween(a, b) > MAX_PERIOD_DAYS) b = shiftDate(a, MAX_PERIOD_DAYS - 1);
    setStart(null);
    setOpen(false);
    onChange(a, b);
  };

  // Подсветка: выбранный период или черновой (начало + наведение).
  const [lo, hi] = start ? (hover && hover < start ? [hover, start] : [start, hover ?? start]) : [from, to];
  const title = formatDate(new Date(`${month}T12:00:00Z`), locale, { month: "long", year: "numeric" });

  return (
    <div ref={box} className="relative max-sm:w-full">
      <button
        type="button"
        onClick={toggle}
        aria-expanded={open}
        aria-haspopup="dialog"
        className={`flex h-[38px] w-[236px] cursor-pointer max-sm:w-full items-center gap-2 rounded-[9px] border border-(--ts-border-strong) bg-(--ts-surface) px-3 text-left text-[13.5px] text-(--ts-text) transition-colors duration-150 hover:border-(--ts-subtle) ${focus}`}
      >
        <Calendar aria-hidden className="size-[15px] shrink-0 text-(--ts-subtle)" />
        <span className="tabular-nums">
          {fullDate(from)} — {fullDate(to)}
        </span>
      </button>
      {open && (
        <div role="dialog" aria-label={t("Выбор периода")} className="absolute left-0 top-[calc(100%+6px)] z-40 w-[292px] rounded-[12px] border border-(--ts-border) bg-(--ts-surface) p-3 shadow-[0_12px_32px_#0e13221f]">
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
              const future = day > today;
              const inRange = day >= lo && day <= hi;
              const edge = day === lo || day === hi;
              return (
                <button
                  key={day}
                  type="button"
                  disabled={future}
                  onClick={() => pick(day)}
                  onMouseEnter={() => start && setHover(day)}
                  aria-label={fullDate(day)}
                  aria-pressed={inRange}
                  className={`h-8 cursor-pointer text-[12.5px] tabular-nums transition-colors duration-150 disabled:cursor-default disabled:text-(--ts-border-strong) ${focus} ${
                    edge
                      ? "rounded-[7px] bg-(--ts-accent) font-semibold text-(--ts-on-accent)"
                      : inRange
                        ? "bg-(--ts-accent-soft) text-(--ts-text)"
                        : `rounded-[7px] text-(--ts-text-2) hover:bg-(--ts-surface-2) ${day === today ? "font-semibold text-(--ts-accent)" : ""}`
                  }`}
                >
                  {Number(day.slice(8, 10))}
                </button>
              );
            })}
          </div>
          <p className="mt-2 text-[11.5px] text-(--ts-subtle)">{start ? t("Выберите конец периода") : t("Выберите начало периода · не больше {n} дней", { n: MAX_PERIOD_DAYS })}</p>
        </div>
      )}
    </div>
  );
}
