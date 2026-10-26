"use client";

import { forwardRef } from "react";
import { dayKind, type DayKind } from "@/lib/hr-timesheet";

/** Фон и цвет чипа по смыслу кода (токены страницы). */
export const chipTone: Record<DayKind, string> = {
  present: "bg-(--ts-success-soft) text-(--ts-success)",
  late: "bg-(--ts-warning-soft) text-(--ts-warning)",
  early: "bg-(--ts-violet-soft) text-(--ts-violet)",
  absent: "bg-(--ts-danger-soft) text-(--ts-danger)",
  dayoffWork: "bg-(--ts-accent-soft) text-(--ts-accent)",
  unknown: "border border-(--ts-border-strong) text-(--ts-subtle)",
  document: "bg-(--ts-surface-2) text-(--ts-text-2)",
  dayoff: "text-(--ts-subtle)",
  holiday: "text-(--ts-subtle)",
  none: "",
};

/** Обводка выбранного чипа — 1.5px цвета кода. */
export const chipRing: Record<DayKind, string> = {
  present: "ring-[1.5px] ring-(--ts-success)",
  late: "ring-[1.5px] ring-(--ts-warning)",
  early: "ring-[1.5px] ring-(--ts-violet)",
  absent: "ring-[1.5px] ring-(--ts-danger)",
  dayoffWork: "ring-[1.5px] ring-(--ts-accent)",
  unknown: "ring-[1.5px] ring-(--ts-subtle)",
  document: "ring-[1.5px] ring-(--ts-text-2)",
  dayoff: "",
  holiday: "",
  none: "",
};

const base = "inline-grid h-[22px] w-7 shrink-0 place-items-center rounded-[5px] text-[9.5px] leading-none font-bold tracking-[0.2px]";

/** Статичный чип (легенда, skeleton): «В» — без плашки (в легенде — на фоне surface-2). */
export function DayCodeChip({ code, legend = false }: { code: string; legend?: boolean }) {
  const kind = dayKind(code);
  if (kind === "none") return <span className={base} aria-hidden />;
  if ((kind === "dayoff" || kind === "holiday") && !legend) return <span className={`${base} font-semibold ${chipTone[kind]}`}>{code}</span>;
  return <span className={`${base} ${kind === "dayoff" || kind === "holiday" ? "bg-(--ts-surface-2) font-semibold text-(--ts-subtle)" : chipTone[kind]}`}>{code}</span>;
}

/**
 * Чип дня в таблице — кнопка: наведение (компьютер) или нажатие открывает карточку дня, с клавиатуры — Enter/пробел.
 * aria-label — полное название кода и дата («9 октября — Опоздание»).
 */
export const DayCodeButton = forwardRef<
  HTMLButtonElement,
  { code: string; label: string; selected: boolean } & Omit<React.ButtonHTMLAttributes<HTMLButtonElement>, "children">
>(function DayCodeButton({ code, label, selected, className = "", ...props }, ref) {
  const kind = dayKind(code);
  const plain = kind === "dayoff" || kind === "holiday";
  return (
    <button
      ref={ref}
      type="button"
      aria-label={label}
      aria-expanded={selected}
      className={`${base} cursor-pointer transition-[box-shadow,filter] duration-150 hover:brightness-95 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent) ${
        plain ? `font-semibold ${chipTone[kind]}` : chipTone[kind]
      } ${selected ? chipRing[kind] : ""} ${className}`}
      {...props}
    >
      {code}
    </button>
  );
});
