"use client";

import { useEffect, useRef, useState } from "react";
import { Building2, ChevronDown, Rows3, Rows4, Search } from "lucide-react";
import { useT } from "@/i18n/client";
import type { PeriodPreset } from "@/lib/hr-timesheet";
import { DateRangePicker } from "./DateRangePicker";

const focus = "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent)";
const field = `h-[38px] rounded-[9px] border border-(--ts-border-strong) bg-(--ts-surface) text-[13.5px] text-(--ts-text) transition-colors duration-150 hover:border-(--ts-subtle) ${focus}`;

const presets: { key: PeriodPreset; label: string }[] = [
  { key: "month", label: "Этот месяц" },
  { key: "prevMonth", label: "Прошлый месяц" },
  { key: "week", label: "Неделя" },
];

export type Density = "normal" | "compact";

/**
 * Панель фильтров: пресеты периода (синхронизированы с полем дат), период, подразделение, поиск по ФИО (через 300 мс после ввода),
 * «Только с нарушениями» и плотность строк. Всё применяется сразу, без кнопки «Показать»; состояние — в адресе страницы.
 */
export function TimesheetFilters({
  from,
  to,
  today,
  preset,
  divisions,
  divisionId,
  search,
  violations,
  density,
  onPreset,
  onPeriod,
  onDivision,
  onSearch,
  onViolations,
  onDensity,
}: {
  from: string;
  to: string;
  today: string;
  preset: PeriodPreset | null;
  divisions: { value: string; label: string }[];
  divisionId: string;
  search: string;
  violations: boolean;
  density: Density;
  onPreset: (preset: PeriodPreset) => void;
  onPeriod: (from: string, to: string) => void;
  onDivision: (id: string) => void;
  onSearch: (q: string) => void;
  onViolations: (on: boolean) => void;
  onDensity: (d: Density) => void;
}) {
  const t = useT();
  const [q, setQ] = useState(search);
  const applied = useRef(search);

  // Адрес сменился снаружи (сброс фильтров, «назад») — поле поиска следует за ним.
  useEffect(() => {
    applied.current = search;
    setQ(search);
  }, [search]);

  useEffect(() => {
    if (q.trim() === applied.current.trim()) return;
    const timer = setTimeout(() => {
      applied.current = q;
      onSearch(q.trim());
    }, 300);
    return () => clearTimeout(timer);
  }, [q, onSearch]);

  return (
    <div className="flex flex-wrap items-center gap-2.5">
      <div role="group" aria-label={t("Период")} className="flex gap-0.5 rounded-[9px] bg-(--ts-surface-2) p-[3px]">
        {presets.map((p) => (
          <button
            key={p.key}
            type="button"
            aria-pressed={preset === p.key}
            onClick={() => onPreset(p.key)}
            className={`h-8 cursor-pointer rounded-[7px] px-3 text-[13px] transition-colors duration-150 ${focus} ${
              preset === p.key ? "bg-(--ts-surface) font-semibold text-(--ts-text) shadow-(--ts-seg-shadow)" : "font-medium text-(--ts-muted) hover:text-(--ts-text)"
            }`}
          >
            {t(p.label)}
          </button>
        ))}
      </div>

      <DateRangePicker from={from} to={to} today={today} onChange={onPeriod} />

      <label className="relative block min-w-[170px] max-w-[220px] flex-[1_1_180px] max-sm:max-w-none max-sm:basis-full">
        <span className="sr-only">{t("Подразделение")}</span>
        <Building2 aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-[15px] -translate-y-1/2 text-(--ts-subtle)" />
        <select value={divisionId} onChange={(e) => onDivision(e.target.value)} className={`${field} w-full cursor-pointer appearance-none truncate pl-[34px] pr-8`}>
          <option value="">{t("Все подразделения")}</option>
          {divisions.map((d) => (
            <option key={d.value} value={d.value}>
              {d.label}
            </option>
          ))}
        </select>
        <ChevronDown aria-hidden className="pointer-events-none absolute right-3 top-1/2 size-[15px] -translate-y-1/2 text-(--ts-subtle)" />
      </label>

      <label className="relative block min-w-[170px] max-w-[260px] flex-[1_1_180px] max-sm:max-w-none max-sm:basis-full">
        <span className="sr-only">{t("Поиск по ФИО")}</span>
        <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-[15px] -translate-y-1/2 text-(--ts-subtle)" />
        <input
          type="search"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder={t("Поиск по ФИО")}
          className={`${field} w-full pl-[34px] pr-3 placeholder:text-(--ts-subtle) [&::-webkit-search-cancel-button]:cursor-pointer`}
        />
      </label>

      <button
        type="button"
        role="switch"
        aria-checked={violations}
        onClick={() => onViolations(!violations)}
        className={`group ml-1 flex cursor-pointer items-center gap-2 rounded-md py-1 text-[13.5px] text-(--ts-text-2) ${focus}`}
      >
        <span className={`relative h-[18px] w-8 shrink-0 rounded-full transition-colors duration-150 ${violations ? "bg-(--ts-accent)" : "bg-(--ts-border-strong)"}`}>
          <span className={`absolute top-0.5 size-3.5 rounded-full bg-white shadow-sm transition-[left] duration-150 ${violations ? "left-4" : "left-0.5"}`} />
        </span>
        {t("Только с нарушениями")}
      </button>

      <div role="group" aria-label={t("Плотность строк")} className="ml-auto flex gap-0.5 rounded-[9px] bg-(--ts-surface-2) p-[3px]">
        {(
          [
            ["normal", Rows3, "Обычные строки"],
            ["compact", Rows4, "Компактные строки"],
          ] as const
        ).map(([key, Icon, label]) => (
          <button
            key={key}
            type="button"
            aria-pressed={density === key}
            aria-label={t(label)}
            title={t(label)}
            onClick={() => onDensity(key)}
            className={`grid size-8 cursor-pointer place-items-center rounded-[7px] transition-colors duration-150 ${focus} ${
              density === key ? "bg-(--ts-surface) text-(--ts-text) shadow-(--ts-seg-shadow)" : "text-(--ts-subtle) hover:text-(--ts-text)"
            }`}
          >
            <Icon className="size-4" />
          </button>
        ))}
      </div>
    </div>
  );
}
