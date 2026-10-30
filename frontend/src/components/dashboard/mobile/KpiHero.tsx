"use client";

import Link from "next/link";
import { useLayoutEffect, useRef, useState } from "react";
import { TrendingDown, TrendingUp } from "lucide-react";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import type { HomeFormat, HomeHero, HomeStat, HomeTone } from "@/lib/dashboard-home";
import { fmt } from "@/lib/dashboard-mobile";
import { money } from "@/lib/sales/format";
import m from "./mobile.module.css";
import { card, focusRing, press } from "./ui";

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

const ringColor: Record<HomeTone, string> = { success: "var(--ts-success)", warning: "var(--ts-warning)", danger: "var(--ts-danger)" };

/** Число по формату колонки: «60,6%», «128», «13,49 млрд». */
export const formatStat = (value: number, format: HomeFormat, t: T) =>
  format === "pct1" ? `${fmt(value, 1)}%` : format === "money" ? money(value, t) : fmt(Math.round(value));

/** Кольцо цели 72×72: прогресс — значение, цвет — по правилам раздела (продажи, явка); в центре — «цель 90%». */
function GoalRing({ ring, label }: { ring: HomeHero["ring"]; label: string }) {
  const share = Math.max(0, Math.min(1, ring.value / 100));
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
          stroke={ringColor[ring.tone]}
          strokeDasharray={C}
          strokeDashoffset={C * (1 - share)}
          className={m.ring}
          style={{ "--c": C } as React.CSSProperties}
        />
      </svg>
      <span aria-hidden className="absolute inset-0 flex flex-col items-center justify-center leading-none">
        <span className="text-[9px] text-(--ts-muted)">{ring.label}</span>
        <span className="mt-[3px] text-[13px] font-bold text-(--ts-text)">{fmt(ring.target)}%</span>
      </span>
    </div>
  );
}

const subTone: Record<HomeStat["subTone"], string> = {
  success: "text-(--ts-success)",
  danger: "text-(--ts-danger)",
  link: "text-(--ts-link)",
  muted: "text-(--ts-muted)",
};

function Stat({ stat, first }: { stat: HomeStat; first: boolean }) {
  const t = useT();
  const value = useCountUp(stat.value);
  return (
    <Link
      href={stat.href}
      className={`flex min-h-[44px] min-w-0 flex-col gap-[4px] rounded-[10px] ${first ? "pr-[8px]" : "border-l border-(--mh-divider) pl-[14px]"} ${press} ${focusRing}`}
    >
      <span className="truncate text-[11px] font-medium text-(--ts-muted)">{stat.label}</span>
      <span className={`truncate text-[20px] font-bold leading-none tracking-[-0.4px] ${stat.tone === "warning" ? "text-(--ts-warning)" : "text-(--ts-text)"}`}>
        {formatStat(value, stat.format, t)}
      </span>
      {stat.sub && <span className={`truncate text-[11px] font-semibold ${subTone[stat.subTone]}`}>{stat.sub}</span>}
    </Link>
  );
}

/**
 * Главная карточка: главный показатель крупно (выполнение плана продаж; нет доступа — явка), изменение к прошлому периоду и кольцо цели;
 * ниже — до трёх колонок из того, что есть (прогноз, явка, задачи), тап ведёт в раздел. Нет ни одного показателя — карточки нет.
 */
export function KpiHero({ hero, stats }: { hero: HomeHero | null; stats: HomeStat[] }) {
  const t = useT();
  const value = useCountUp(hero?.value ?? 0);
  if (!hero && stats.length === 0) return null;
  const up = (hero?.delta?.value ?? 0) >= 0;
  const Trend = up ? TrendingUp : TrendingDown;

  return (
    <section aria-label={hero?.label ?? t("Показатели")} className={hero ? `rounded-[22px] border p-[20px] ${m.kpi}` : `${card} p-[18px]`}>
      {hero && (
        <>
          <div className="flex items-start justify-between gap-[12px]">
            <Link href={hero.href} className={`min-w-0 rounded-[12px] ${press} ${focusRing}`}>
              <p className="text-[13px] font-medium text-(--ts-muted)">{hero.label}</p>
              <p className="mt-[6px] text-[44px] font-bold leading-none tracking-[-1.5px] text-(--ts-text)">{fmt(value, 1)}%</p>
              {hero.delta && (
                <p className="mt-[12px] flex flex-wrap items-center gap-x-[8px] gap-y-[4px]">
                  <span
                    className={`inline-flex items-center gap-[4px] rounded-[8px] px-[7px] py-[3px] text-[12px] font-semibold ${
                      up ? "bg-(--ts-success-soft) text-(--ts-success)" : "bg-(--ts-danger-soft) text-(--ts-danger)"
                    }`}
                  >
                    <Trend aria-hidden className="size-[13px]" strokeWidth={2.4} />
                    {hero.delta.text}
                  </span>
                  <span className="text-[12px] text-(--ts-muted)">{hero.delta.hint}</span>
                </p>
              )}
            </Link>
            <GoalRing
              ring={hero.ring}
              label={t("{label} {v}%, {goal} {g}%", { label: hero.label, v: fmt(hero.value, 1), goal: hero.ring.label, g: fmt(hero.ring.target) })}
            />
          </div>
          {stats.length > 0 && <div aria-hidden className="my-[18px] h-px bg-(--mh-divider)" />}
        </>
      )}
      {stats.length > 0 && (
        <div className={`grid ${stats.length === 3 ? "grid-cols-3" : stats.length === 2 ? "grid-cols-2" : "grid-cols-1"}`}>
          {stats.map((s, i) => (
            <Stat key={s.key} stat={s} first={i === 0} />
          ))}
        </div>
      )}
    </section>
  );
}
