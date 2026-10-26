"use client";

import { CalendarDays, CircleCheck, CircleX, ClockAlert, Timer, Users, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import type { HrSummary } from "@/lib/hr";
import { decimal, percent, type SummaryFilter } from "@/lib/hr-timesheet";
import styles from "./timesheet.module.css";

type Tone = "subtle" | "success" | "warning" | "danger" | "accent";

const iconTone: Record<Tone, string> = {
  subtle: "text-(--ts-subtle)",
  success: "text-(--ts-success)",
  warning: "text-(--ts-warning)",
  danger: "text-(--ts-danger)",
  accent: "text-(--ts-accent)",
};

/** Подсветка выбранной ячейки: soft-цвет поверх surface (тень на всю ячейку — фон остаётся непрозрачным). */
const activeBg: Record<"success" | "warning" | "danger", string> = {
  success: "shadow-[inset_0_0_0_999px_var(--ts-success-soft)]",
  warning: "shadow-[inset_0_0_0_999px_var(--ts-warning-soft)]",
  danger: "shadow-[inset_0_0_0_999px_var(--ts-danger-soft)]",
};

const int = (n: number) => n.toLocaleString("ru-RU");
const pct = (v: number | null) => (v == null ? "—" : `${decimal(v, 1)}%`);

const cell = "flex min-w-0 flex-col gap-1.5 bg-(--ts-surface) px-5 py-4";

function Head({ icon: Icon, tone, label }: { icon: LucideIcon; tone: Tone; label: string }) {
  return (
    <span title={label} className="flex min-w-0 items-center gap-1.5 text-[12.5px] text-(--ts-muted)">
      <Icon aria-hidden className={`size-3.5 shrink-0 ${iconTone[tone]}`} strokeWidth={2} />
      <span className="truncate">{label}</span>
    </span>
  );
}

const Value = ({ children }: { children: React.ReactNode }) => (
  <span className="text-2xl leading-7 font-semibold tracking-[-0.5px] text-(--ts-text) tabular-nums">{children}</span>
);

/**
 * Сводка за период: сотрудники, рабочие дни по графику, явка (доля от графика), опоздания (доля дней с приходом), отсутствия (доля от
 * графика), отработано из плана с полосой. Явка, опоздания и отсутствия — кнопки: оставляют в таблице сотрудников с этим кодом.
 */
export function TimesheetSummary({
  employees,
  total,
  filter,
  onFilter,
}: {
  employees: number;
  total: HrSummary;
  filter: SummaryFilter | null;
  onFilter: (f: SummaryFilter | null) => void;
}) {
  const t = useT();
  const worked = total.workedMinutes / 60;
  const plan = total.planMinutes / 60;
  const workedPct = percent(total.workedMinutes, total.planMinutes);
  const filters: { key: SummaryFilter; icon: LucideIcon; tone: "success" | "warning" | "danger"; label: string; value: number; note: string }[] = [
    { key: "present", icon: CircleCheck, tone: "success", label: "Явка", value: total.presentDays, note: t("{p} от графика", { p: pct(percent(total.presentDays, total.planDays)) }) },
    { key: "late", icon: ClockAlert, tone: "warning", label: "Опоздания", value: total.lateDays, note: t("{p} дней с приходом", { p: pct(percent(total.lateDays, total.presentDays)) }) },
    { key: "absent", icon: CircleX, tone: "danger", label: "Отсутствия", value: total.absentDays, note: t("{p} от графика", { p: pct(percent(total.absentDays, total.planDays)) }) },
  ];

  return (
    <section
      aria-label={t("Сводка за период")}
      className="grid grid-cols-2 gap-px overflow-clip rounded-[12px] border border-(--ts-border) bg-(--ts-border) sm:grid-cols-3 xl:flex"
    >
      <div className={`${cell} xl:flex-1`}>
        <Head icon={Users} tone="subtle" label={t("Сотрудников")} />
        <Value>{int(employees)}</Value>
      </div>
      <div className={`${cell} xl:flex-1`}>
        <Head icon={CalendarDays} tone="subtle" label={t("Рабочих дней по графику")} />
        <Value>{int(total.planDays)}</Value>
      </div>
      {filters.map((f) => {
        const on = filter === f.key;
        return (
          <button
            key={f.key}
            type="button"
            aria-pressed={on}
            title={on ? t("Показать всех") : t("Показать только сотрудников с этим кодом")}
            onClick={() => onFilter(on ? null : f.key)}
            className={`${cell} cursor-pointer text-left transition-colors duration-150 focus-visible:relative focus-visible:z-10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-(--ts-accent) xl:flex-1 ${
              on ? activeBg[f.tone] : "hover:bg-(--ts-row-hover)"
            }`}
          >
            <Head icon={f.icon} tone={f.tone} label={t(f.label)} />
            <Value>{int(f.value)}</Value>
            <span className={`text-xs font-medium ${iconTone[f.tone]}`}>{f.note}</span>
          </button>
        );
      })}
      <div className={`${cell} xl:w-[340px] xl:shrink-0`}>
        <span className="flex items-center justify-between gap-2">
          <Head icon={Timer} tone="accent" label={t("Отработано")} />
          <span className="text-[12.5px] font-semibold text-(--ts-accent) tabular-nums">{pct(workedPct)}</span>
        </span>
        <span className="flex flex-wrap items-baseline gap-x-1.5">
          <Value>{t("{h} ч", { h: decimal(worked, 1) })}</Value>
          <span className="text-[12.5px] text-(--ts-subtle)">{t("из {h} ч по плану", { h: decimal(plan, 0) })}</span>
        </span>
        <span className="mt-1 block h-1.5 overflow-hidden rounded-[3px] bg-(--ts-surface-2)" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(workedPct ?? 0)} aria-label={t("Отработано от плана")}>
          <span className="block h-full rounded-[3px] bg-(--ts-accent)" style={{ width: `${Math.min(100, workedPct ?? 0)}%` }} />
        </span>
      </div>
    </section>
  );
}

/** Skeleton сводки на время загрузки. */
export function TimesheetSummarySkeleton() {
  return (
    <div className="grid grid-cols-2 gap-px overflow-clip rounded-[12px] border border-(--ts-border) bg-(--ts-border) sm:grid-cols-3 xl:flex" aria-hidden>
      {Array.from({ length: 6 }, (_, i) => (
        <div key={i} className={`${cell} ${i === 5 ? "xl:w-[340px] xl:shrink-0" : "xl:flex-1"}`}>
          <span className={`h-3.5 w-28 rounded ${styles.skeleton}`} />
          <span className={`h-6 w-20 rounded ${styles.skeleton}`} />
          <span className={`h-3 w-24 rounded ${styles.skeleton}`} />
        </div>
      ))}
    </div>
  );
}
