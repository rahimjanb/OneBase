"use client";

import { useState } from "react";
import { useT } from "@/i18n/client";
import type { TrendSeries } from "@/lib/dashboard.mock";
import { CardHeader, DashCard } from "./blocks";

const MIN = 60;
const MAX = 100;
const HEIGHT = 200;
const ticks = [100, 90, 80, 70, 60];

const pct = (v: number, digits = 1) => `${v.toLocaleString("ru-RU", { minimumFractionDigits: digits, maximumFractionDigits: digits })}%`;

/**
 * «Динамика общего KPI»: вкладки KPI / Выручка / Задачи, сводка (среднее, лучшая неделя, недель выше цели), 12 столбиков по неделям
 * на шкале 60–100 с сеткой и пунктирной линией цели; при наведении — подпись недели и значения. Только div, без библиотек графиков.
 */
export function KpiTrendChart({ series }: { series: TrendSeries[] }) {
  const t = useT();
  const [key, setKey] = useState(series[0]?.key ?? "kpi");
  const [hover, setHover] = useState<number | null>(null);
  const active = series.find((s) => s.key === key) ?? series[0];
  const values = active.values;
  const average = values.reduce((a, b) => a + b, 0) / values.length;
  const best = Math.max(...values);
  const above = values.filter((v) => v >= active.target).length;
  const y = (v: number) => ((MAX - Math.min(MAX, Math.max(MIN, v))) / (MAX - MIN)) * HEIGHT;

  return (
    <DashCard className="flex flex-col gap-5 p-5">
      <CardHeader
        title={t("Динамика общего KPI")}
        subtitle={t("По неделям · цель {n}%", { n: active.target })}
        action={
          <div role="tablist" className="flex gap-0.5 rounded-[9px] border border-line bg-muted/40 p-[3px]">
            {series.map((s) => (
              <button
                key={s.key}
                role="tab"
                type="button"
                aria-selected={s.key === active.key}
                onClick={() => setKey(s.key)}
                className={`h-[26px] rounded-md px-2.5 text-xs transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
                  s.key === active.key ? "bg-muted font-semibold text-ink" : "font-medium text-ink-3 hover:text-ink"
                }`}
              >
                {t(s.label)}
              </button>
            ))}
          </div>
        }
      />

      <dl className="flex flex-wrap gap-8">
        <div>
          <dt className="text-xs text-ink-4">{t("Среднее за период")}</dt>
          <dd className="mt-0.5 text-xl font-semibold text-ink tabular-nums">{pct(average)}</dd>
        </div>
        <div>
          <dt className="text-xs text-ink-4">{t("Лучшая неделя")}</dt>
          <dd className="mt-0.5 text-xl font-semibold text-ok tabular-nums">{pct(best)}</dd>
        </div>
        <div>
          <dt className="text-xs text-ink-4">{t("Недель выше цели")}</dt>
          <dd className="mt-0.5 text-xl font-semibold text-ink tabular-nums">{t("{n} из {total}", { n: above, total: values.length })}</dd>
        </div>
      </dl>

      <div className="flex gap-3">
        <div className="relative w-9 shrink-0 text-[11px] text-ink-4" style={{ height: HEIGHT }} aria-hidden>
          {ticks.map((v) => (
            <span key={v} className="absolute right-0 -translate-y-1/2 tabular-nums" style={{ top: y(v) }}>
              {v}%
            </span>
          ))}
        </div>
        <div className="min-w-0 flex-1">
          <div className="relative" style={{ height: HEIGHT }}>
            {ticks.map((v) => (
              <span key={v} className={`absolute inset-x-0 h-px ${v === MIN ? "bg-line-strong" : "bg-line/60"}`} style={{ top: y(v) }} aria-hidden />
            ))}
            {/* Линия цели — пунктир цветом предупреждения, метка над ней слева. */}
            <span className="absolute inset-x-0 border-t border-dashed border-warn" style={{ top: y(active.target) }} aria-hidden>
              <span className="absolute -top-5 left-0 rounded bg-warn/10 px-1.5 py-0.5 text-[10.5px] font-semibold leading-3 text-warn">{t("Цель {n}%", { n: active.target })}</span>
            </span>
            <div className="absolute inset-0 flex items-end gap-3.5" role="img" aria-label={t("Динамика по неделям")}>
              {values.map((v, i) => {
                const last = i === values.length - 1;
                const color = last ? "bg-gradient-to-t from-accent to-accent-bright" : v >= active.target ? "bg-accent/55" : "bg-accent/20";
                return (
                  <div
                    key={active.weeks[i]}
                    className="relative flex flex-1 items-end"
                    style={{ height: HEIGHT }}
                    onMouseEnter={() => setHover(i)}
                    onMouseLeave={() => setHover((h) => (h === i ? null : h))}
                  >
                    <span className={`w-full rounded-t-[5px] rounded-b-sm transition-[filter] duration-150 ${color} ${hover === i ? "brightness-125" : ""}`} style={{ height: Math.max(2, HEIGHT - y(v)) }} />
                    {hover === i && (
                      <span className="pointer-events-none absolute left-1/2 z-10 -translate-x-1/2 whitespace-nowrap rounded-md border border-line bg-surface-solid px-2 py-1 text-[11px] text-ink shadow-lg" style={{ bottom: HEIGHT - y(v) + 6 }}>
                        <span className="text-ink-3">{active.weeks[i]}</span> · <span className="font-semibold tabular-nums">{pct(v)}</span>
                      </span>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
          <div className="mt-2 flex gap-3.5 text-[11px] text-ink-4">
            {active.weeks.map((w, i) => (
              <span key={w} className={`flex-1 text-center tabular-nums ${i === active.weeks.length - 1 ? "font-medium text-ink" : ""}`}>
                {w}
              </span>
            ))}
          </div>
        </div>
      </div>
    </DashCard>
  );
}
