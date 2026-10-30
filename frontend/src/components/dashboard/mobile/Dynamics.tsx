"use client";

import { useEffect, useRef, useState } from "react";
import { useT } from "@/i18n/client";
import type { TrendSeries } from "@/lib/dashboard.mock";
import { fmt } from "@/lib/dashboard-mobile";
import { plural } from "@/lib/format";
import m from "./mobile.module.css";
import { card, focusRing, SectionHeader } from "./ui";

const H = 130;
const week = (label: string) => label.replace(/^\D+/, "");
const pct = (v: number) => `${fmt(v, 1)}%`;

/**
 * «Динамика KPI»: сегменты KPI · Выручка · Задачи, три итога (среднее, лучшая неделя, недель выше цели) и 12 недель столбиками.
 * Шкала — от 60 до 100 % (шире, если значения выходят за неё), без подписей оси. Линия цели — позади столбиков; у ряда без цели линии нет.
 * Столбики ниже цели — бледные, выше — ярче, текущая неделя — градиентом. Тап по столбику — значение недели.
 */
export function Dynamics({ series, days }: { series: TrendSeries[]; days: number }) {
  const t = useT();
  const [key, setKey] = useState(series[0]?.key);
  const [tip, setTip] = useState<number | null>(null);
  const chart = useRef<HTMLDivElement>(null);

  // Подсказка закрывается тапом мимо графика.
  useEffect(() => {
    if (tip === null) return;
    const away = (e: PointerEvent) => {
      if (!chart.current?.contains(e.target as Node)) setTip(null);
    };
    document.addEventListener("pointerdown", away);
    return () => document.removeEventListener("pointerdown", away);
  }, [tip]);

  const s = series.find((x) => x.key === key) ?? series[0];
  if (!s) return null;
  const values = s.values;
  const target: number | undefined = s.target;
  const last = values.length - 1;
  const avg = values.reduce((a, b) => a + b, 0) / values.length;
  const best = Math.max(...values);
  const above = target === undefined ? null : values.filter((v) => v >= target).length;
  const lo = Math.min(60, Math.floor((Math.min(...values) - 5) / 10) * 10);
  const hi = Math.max(100, Math.ceil(Math.max(...values) / 10) * 10);
  const h = (v: number) => ((Math.max(lo, Math.min(hi, v)) - lo) / (hi - lo)) * H;

  const stats = [
    { label: t("Среднее"), value: pct(avg), tone: "text-(--ts-text)" },
    { label: t("Лучшая неделя"), value: pct(best), tone: "text-(--ts-success)" },
    { label: t("Выше цели"), value: above === null ? "—" : t("{n} из {total}", { n: above, total: values.length }), tone: "text-(--ts-text)" },
  ];

  return (
    <section className="flex flex-col gap-[12px]">
      <SectionHeader title={t("Динамика KPI")} href="/reports" action={t("Подробнее")} />
      <div className={`${card} flex flex-col gap-[16px] p-[18px]`}>
        <div role="tablist" aria-label={t("Показатель")} className="grid grid-cols-3 gap-[2px] rounded-[12px] border border-(--ts-border) bg-(--ts-bg) p-[3px]">
          {series.map((x) => {
            const active = x.key === s.key;
            return (
              <button
                key={x.key}
                type="button"
                role="tab"
                aria-selected={active}
                onClick={() => {
                  setKey(x.key);
                  setTip(null);
                }}
                className={`h-[34px] rounded-[10px] border text-[13px] font-semibold transition-colors duration-150 ${focusRing} ${
                  active ? "border-(--ts-border-strong) bg-(--ts-surface-2) text-(--ts-text)" : "border-transparent text-(--ts-muted)"
                }`}
              >
                {t(x.label)}
              </button>
            );
          })}
        </div>

        <div className="grid grid-cols-3">
          {stats.map((st, i) => (
            <div key={st.label} className={`flex min-w-0 flex-col gap-[4px] ${i > 0 ? "border-l border-(--ts-border) pl-[14px]" : "pr-[8px]"}`}>
              <span className="truncate text-[11px] text-(--ts-muted)">{st.label}</span>
              <span className={`text-[17px] font-bold leading-tight ${st.tone}`}>{st.value}</span>
            </div>
          ))}
        </div>

        <div ref={chart}>
          <div
            role="group"
            aria-label={t("{name} по неделям: среднее {avg}, лучшая неделя {best}", { name: t(s.label), avg: pct(avg), best: pct(best) })}
            className="relative"
            style={{ height: H }}
          >
            {/* Цель — позади столбиков: рисуется первой. */}
            {target !== undefined && (
              <>
                <span aria-hidden className="absolute inset-x-0 border-t border-dashed border-[color-mix(in_srgb,var(--ts-warning)_70%,transparent)]" style={{ bottom: h(target) }} />
                <span
                  aria-hidden
                  className="absolute left-0 rounded-[6px] bg-(--ts-warning-soft) px-[6px] py-[2px] text-[10px] font-semibold leading-[14px] text-(--ts-warning)"
                  style={{ bottom: h(target) + 4 }}
                >
                  {t("цель {n}%", { n: fmt(target) })}
                </span>
              </>
            )}
            <div key={s.key} className="absolute inset-0 flex items-end gap-[5px]">
              {values.map((v, i) => {
                const current = i === last;
                const color = current
                  ? "bg-[linear-gradient(to_bottom,#6e9bff,#3d7bff)]"
                  : target !== undefined && v >= target
                    ? "bg-[color-mix(in_srgb,var(--ts-accent)_55%,transparent)]"
                    : "bg-[color-mix(in_srgb,var(--ts-accent)_25%,transparent)]";
                const align = i < 2 ? "left-0" : i > last - 2 ? "right-0" : "left-1/2 -translate-x-1/2";
                return (
                  <button
                    key={s.weeks[i]}
                    type="button"
                    data-press="off"
                    onClick={() => setTip((x) => (x === i ? null : i))}
                    aria-label={t("Неделя {w}: {v}", { w: week(s.weeks[i]), v: pct(v) })}
                    aria-pressed={tip === i}
                    className={`relative flex h-full min-w-0 flex-1 items-end rounded-t-[5px] ${focusRing}`}
                  >
                    <span
                      className={`block w-full rounded-t-[5px] ${m.grow} ${color} ${tip === i ? "brightness-125" : ""}`}
                      style={{ height: Math.max(3, h(v)), animationDelay: `${i * 25}ms` }}
                    />
                    {tip === i && (
                      <span
                        role="tooltip"
                        className={`pointer-events-none absolute z-20 whitespace-nowrap rounded-[8px] border border-(--ts-pop-border) bg-(--ts-pop-bg) px-[8px] py-[4px] text-[11px] font-medium text-(--ts-pop-text) shadow-(--ts-pop-shadow) ${align}`}
                        style={{ bottom: Math.max(3, h(v)) + 6 }}
                      >
                        {t("Нед. {w}", { w: week(s.weeks[i]) })} · <b className="font-bold">{pct(v)}</b>
                      </span>
                    )}
                  </button>
                );
              })}
            </div>
          </div>
          <div aria-hidden className="mt-[8px] flex gap-[5px]">
            {s.weeks.map((w, i) => (
              <span key={w} className={`min-w-0 flex-1 text-center text-[10px] ${i === last ? "font-bold text-(--ts-text)" : "text-(--ts-muted)"}`}>
                {week(w)}
              </span>
            ))}
          </div>
        </div>

        <p className="text-[11px] text-(--ts-muted)">
          {t("Нед. {w} — текущая, данные за {d}", { w: week(s.weeks[last]), d: `${days} ${t(plural(days, ["день", "дня", "дней"]))}` })}
        </p>
      </div>
    </section>
  );
}
