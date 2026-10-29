"use client";

import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import { duration } from "@/lib/hr";
import type { DayFilter, PeriodStats } from "@/lib/hr-employee";
import { card, focus } from "../overview/bits";

type Kpi = { key: string; label: string; value: string; tone: string; hint: string | null; hintShort: string | null; filter?: DayFilter };

/**
 * Итоги периода — те же, что сумма строк таблицы. Колонка шире 1040 — пять карточек в ряд; уже (телефон, ноутбук с боковым меню) —
 * «Отработано» отдельной строкой и четыре карточки сеткой. «Опоздания», «Ранние уходы» и «Отсутствия» включают фильтр таблицы.
 */
export function StatsCards({ s, filter, onFilter }: { s: PeriodStats; filter: DayFilter; onFilter: (f: DayFilter) => void }) {
  const t = useT();
  const dur = (m: number) => duration(m, t);
  const offDays = s.dayOffWorked > 0 ? `+${s.dayOffWorked} ${t(plural(s.dayOffWorked, ["выход", "выхода", "выходов"]))}` : null;

  const worked = {
    value: dur(s.worked),
    hint: `${s.daysWithArrival} ${t(plural(s.daysWithArrival, ["день с приходом", "дня с приходом", "дней с приходом"]))}`,
  };
  const items: Kpi[] = [
    {
      key: "workdays",
      label: "Рабочих дней",
      value: String(s.workingDays),
      tone: "text-(--ts-text)",
      hint: offDays && `${offDays} ${t(s.dayOffSundays ? "в воскресенье" : "в нерабочие дни")}`,
      hintShort: offDays && `${offDays} ${t(s.dayOffSundays ? "в вс" : "в выходные")}`,
    },
    {
      key: "late",
      label: "Опоздания",
      value: String(s.lateDays),
      tone: s.lateDays > 0 ? "text-(--ts-warning)" : "text-(--ts-success)",
      hint: s.lateDays > 0 ? `${dur(s.lateMinutes)}${s.avgLate != null ? ` · ${t("в ср. {n}", { n: dur(s.avgLate) })}` : ""}` : null,
      hintShort: s.lateDays > 0 ? t("{n} всего", { n: dur(s.lateMinutes) }) : null,
      filter: "late",
    },
    {
      key: "early",
      label: "Ранние уходы",
      value: String(s.earlyDays),
      tone: s.earlyDays > 0 ? "text-(--ts-danger)" : "text-(--ts-success)",
      hint: s.typicalLeave ? t("обычно уходит ~{time}", { time: s.typicalLeave }) : null,
      hintShort: s.typicalLeave ? t("обычно ~{time}", { time: s.typicalLeave }) : null,
      filter: "early",
    },
    {
      key: "absent",
      label: "Отсутствия",
      value: String(s.absentDays),
      tone: s.absentDays > 0 ? "text-(--ts-danger)" : "text-(--ts-success)",
      hint: t("по документу — {n}", { n: s.leaveDays }),
      hintShort: t("документов — {n}", { n: s.leaveDays }),
      filter: "absent",
    },
  ];

  const box = `${card} flex min-w-0 flex-col gap-1 px-4 py-3.5 text-left lg:px-[18px] lg:py-4`;
  const value = "whitespace-nowrap text-[22px] font-bold leading-tight tracking-[-0.3px] @min-[1040px]:text-2xl";

  return (
    <div className="@container">
      <div className="grid grid-cols-2 gap-3 @min-[560px]:grid-cols-4 @min-[1040px]:grid-cols-5">
        <div className={`${box} col-span-2 @min-[560px]:col-span-4 @min-[1040px]:col-span-1`}>
          <span className="flex items-baseline justify-between gap-2">
            <span className="truncate text-xs font-medium text-(--ts-muted)">
              <span className="@min-[1040px]:hidden">{t("Отработано за период")}</span>
              <span className="hidden @min-[1040px]:inline">{t("Отработано")}</span>
            </span>
            <span className="truncate text-[11px] font-medium text-(--ts-subtle) @min-[1040px]:hidden">{worked.hint}</span>
          </span>
          <span className="whitespace-nowrap text-[28px] font-bold leading-tight tracking-[-0.4px] text-(--ts-text) @min-[1040px]:text-2xl">{worked.value}</span>
          <span className="hidden truncate text-[11px] font-medium text-(--ts-subtle) @min-[1040px]:block">{worked.hint}</span>
        </div>
        {items.map((k) => {
          const body = (
            <>
              <span className="truncate text-xs font-medium text-(--ts-muted)">{t(k.label)}</span>
              <span className={`${value} ${k.tone}`}>{k.value}</span>
              {k.hint && (
                <span className="truncate text-[11px] font-medium text-(--ts-subtle)" title={k.hint}>
                  <span className="@min-[560px]:hidden">{k.hintShort}</span>
                  <span className="hidden @min-[560px]:inline">{k.hint}</span>
                </span>
              )}
            </>
          );
          if (!k.filter) return <div key={k.key} className={box}>{body}</div>;
          const on = filter === k.filter;
          return (
            <button
              key={k.key}
              type="button"
              aria-pressed={on}
              onClick={() => onFilter(on ? "all" : k.filter!)}
              className={`${box} cursor-pointer transition-colors duration-150 hover:border-(--ts-border-strong) ${on ? "border-(--ts-accent)!" : ""} ${focus}`}
            >
              {body}
            </button>
          );
        })}
      </div>
    </div>
  );
}
