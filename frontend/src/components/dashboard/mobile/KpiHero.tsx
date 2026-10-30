"use client";

import Link from "next/link";
import { useLayoutEffect, useRef, useState } from "react";
import { TrendingDown, TrendingUp } from "lucide-react";
import { useT } from "@/i18n/client";
import { fmt, signed, type MobileKpi } from "@/lib/dashboard-mobile";
import m from "./mobile.module.css";
import { focusRing, press } from "./ui";

/** Число при первом показе считает от нуля до значения за 400 мс (ease-out); без анимации движения — сразу значение. */
export function useCountUp(value: number, duration = 400) {
  const [shown, setShown] = useState(value);
  const from = useRef(0);
  useLayoutEffect(() => {
    const start = from.current;
    from.current = value;
    if (start === value || window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      setShown(value);
      return;
    }
    let frame = 0;
    const t0 = performance.now();
    const step = (now: number) => {
      const p = Math.min(1, (now - t0) / duration);
      setShown(start + (value - start) * (1 - (1 - p) ** 3));
      if (p < 1) frame = requestAnimationFrame(step);
    };
    setShown(start);
    frame = requestAnimationFrame(step);
    return () => {
      cancelAnimationFrame(frame);
      // Повторный запуск эффекта (строгий режим, смена значения) снова считает от прежней точки.
      from.current = start;
    };
  }, [value, duration]);
  return shown;
}

const R = 33;
const C = 2 * Math.PI * R;

/** Кольцо цели 72×72: прогресс зелёный, если цель достигнута, иначе оранжевый; в центре — «цель 90%». */
function GoalRing({ value, target, label }: { value: number; target: number; label: string }) {
  const t = useT();
  const share = Math.max(0, Math.min(1, value / 100));
  return (
    <div role="img" aria-label={label} className="relative size-[72px] shrink-0">
      <svg viewBox="0 0 72 72" className="size-full -rotate-90" aria-hidden>
        <circle cx="36" cy="36" r={R} fill="none" strokeWidth="6" stroke="var(--mh-track)" />
        <circle
          cx="36"
          cy="36"
          r={R}
          fill="none"
          strokeWidth="6"
          strokeLinecap="round"
          stroke={value >= target ? "var(--ts-success)" : "var(--ts-warning)"}
          strokeDasharray={C}
          strokeDashoffset={C * (1 - share)}
          className={m.ring}
          style={{ "--c": C } as React.CSSProperties}
        />
      </svg>
      <span aria-hidden className="absolute inset-0 flex flex-col items-center justify-center leading-none">
        <span className="text-[9px] text-(--ts-muted)">{t("цель")}</span>
        <span className="mt-[3px] text-[13px] font-bold text-(--ts-text)">{fmt(target)}%</span>
      </span>
    </div>
  );
}

/**
 * Главная карточка: выполнение планов крупно, динамика к прошлому периоду и кольцо цели; ниже — общий KPI, задачи и просрочки
 * (тап ведёт в раздел). Уменьшение просрочек — хорошо (зелёным), рост — плохо.
 */
export function KpiHero({ kpi }: { kpi: MobileKpi }) {
  const t = useT();
  const plan = useCountUp(kpi.plan.value);
  const total = useCountUp(kpi.kpi.value);
  const active = useCountUp(kpi.tasks.active);
  const overdue = useCountUp(kpi.tasks.overdue);
  const up = kpi.plan.delta >= 0;
  const Trend = up ? TrendingUp : TrendingDown;

  const columns = [
    {
      key: "kpi",
      label: t("Общий KPI"),
      value: `${fmt(total, 1)}%`,
      valueClass: "text-(--ts-text)",
      delta: `${signed(kpi.kpi.delta, 1)}%`,
      deltaClass: kpi.kpi.delta >= 0 ? "text-(--ts-success)" : "text-(--ts-danger)",
      href: "/reports",
    },
    {
      key: "tasks",
      label: t("Задачи"),
      value: fmt(active),
      valueClass: "text-(--ts-text)",
      delta: t("{n} сегодня", { n: signed(kpi.tasks.createdToday) }),
      deltaClass: "text-(--ts-link)",
      href: "/tasks",
    },
    {
      key: "overdue",
      label: t("Просрочено"),
      value: fmt(overdue),
      valueClass: kpi.tasks.overdue > 0 ? "text-(--ts-warning)" : "text-(--ts-text)",
      delta: t("{n} за нед.", { n: signed(kpi.tasks.overdueDeltaWeek) }),
      deltaClass: kpi.tasks.overdueDeltaWeek <= 0 ? "text-(--ts-success)" : "text-(--ts-danger)",
      href: "/tasks/all?status=overdue",
    },
  ];

  return (
    <section aria-label={t("Выполнение планов")} className={`rounded-[22px] border p-[20px] ${m.kpi}`}>
      <div className="flex items-start justify-between gap-[12px]">
        <div className="min-w-0">
          <p className="text-[13px] font-medium text-(--ts-muted)">{t("Выполнение планов")}</p>
          <p className="mt-[6px] text-[44px] font-bold leading-none tracking-[-1.5px] text-(--ts-text)">{fmt(plan, 1)}%</p>
          <p className="mt-[12px] flex flex-wrap items-center gap-x-[8px] gap-y-[4px]">
            <span
              className={`inline-flex items-center gap-[4px] rounded-[8px] px-[7px] py-[3px] text-[12px] font-semibold ${
                up ? "bg-(--ts-success-soft) text-(--ts-success)" : "bg-(--ts-danger-soft) text-(--ts-danger)"
              }`}
            >
              <Trend aria-hidden className="size-[13px]" strokeWidth={2.4} />
              {signed(kpi.plan.delta, 1)}%
            </span>
            <span className="text-[12px] text-(--ts-muted)">{t("к прошлому периоду")}</span>
          </p>
        </div>
        <GoalRing
          value={kpi.plan.value}
          target={kpi.plan.target}
          label={t("Выполнение плана {v}%, цель {g}%", { v: fmt(kpi.plan.value, 1), g: fmt(kpi.plan.target) })}
        />
      </div>

      <div aria-hidden className="my-[18px] h-px bg-(--mh-divider)" />

      <div className="grid grid-cols-3">
        {columns.map((c, i) => (
          <Link
            key={c.key}
            href={c.href}
            className={`flex min-h-[44px] min-w-0 flex-col gap-[4px] rounded-[10px] ${i > 0 ? "border-l border-(--mh-divider) pl-[14px]" : "pr-[8px]"} ${press} ${focusRing}`}
          >
            <span className="truncate text-[11px] font-medium text-(--ts-muted)">{c.label}</span>
            <span className={`text-[20px] font-bold leading-none tracking-[-0.4px] ${c.valueClass}`}>{c.value}</span>
            <span className={`truncate text-[11px] font-semibold ${c.deltaClass}`}>{c.delta}</span>
          </Link>
        ))}
      </div>
    </section>
  );
}
