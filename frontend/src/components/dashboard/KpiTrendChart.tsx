"use client";

import { useState } from "react";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import type { TrendSeries } from "@/lib/dashboard.mock";
import { trendScale, trendStats } from "@/lib/dashboard-home";
import { money } from "@/lib/sales/format";
import { CardHeader, DashCard } from "./blocks";

const HEIGHT = 200;

const pct = (v: number, digits = 1) => `${v.toLocaleString("ru-RU", { minimumFractionDigits: digits, maximumFractionDigits: digits })}%`;
const valueText = (s: TrendSeries, v: number, t: T, digits = 1) => (s.unit === "money" ? money(v, t) : pct(v, digits));

/**
 * «Динамика по неделям»: вкладки рядов (продажи к плану, выручка, явка — какие есть), сводка (среднее, лучшая неделя, недель выше цели;
 * у ряда без цели — сумма), 12 столбиков на шкале ряда (trendScale — общая с телефоном) с сеткой и пунктирной линией цели; неделя без
 * данных — без столбика. При наведении — подпись недели и значения. Только div, без библиотек графиков.
 */
export function KpiTrendChart({ series }: { series: TrendSeries[] }) {
  const t = useT();
  const [key, setKey] = useState(series[0]?.key ?? "");
  const [hover, setHover] = useState<number | null>(null);
  const active = series.find((s) => s.key === key) ?? series[0];
  if (!active) {
    return (
      <DashCard className="flex min-w-0 flex-col gap-5 p-4 sm:p-5">
        <CardHeader title={t("Динамика по неделям")} />
        <p className="py-16 text-center text-[13px] text-ink-3">{t("Нет данных за период")}</p>
      </DashCard>
    );
  }
  const values = active.values;
  const target = active.target;
  const { lo, hi } = trendScale(active);
  const ticks = [0, 1, 2, 3, 4].map((i) => hi - ((hi - lo) * i) / 4);
  const stats = trendStats(active);
  const y = (v: number) => ((hi - Math.min(hi, Math.max(lo, v))) / (hi - lo)) * HEIGHT;

  return (
    <DashCard className="flex min-w-0 flex-col gap-5 p-4 sm:p-5">
      <CardHeader
        title={t("Динамика по неделям")}
        subtitle={target != null ? t("По неделям · цель {n}%", { n: target }) : t("По неделям")}
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

      <dl className="flex flex-wrap gap-x-8 gap-y-3">
        <div>
          <dt className="text-xs text-ink-4">{t("Среднее за период")}</dt>
          <dd className="mt-0.5 text-xl font-semibold text-ink tabular-nums">{stats.average == null ? "—" : valueText(active, stats.average, t)}</dd>
        </div>
        <div>
          <dt className="text-xs text-ink-4">{t("Лучшая неделя")}</dt>
          <dd className="mt-0.5 text-xl font-semibold text-ok tabular-nums">{stats.best == null ? "—" : valueText(active, stats.best, t)}</dd>
        </div>
        {stats.above != null ? (
          <div>
            <dt className="text-xs text-ink-4">{t("Недель выше цели")}</dt>
            <dd className="mt-0.5 text-xl font-semibold text-ink tabular-nums">{t("{n} из {total}", { n: stats.above, total: stats.count })}</dd>
          </div>
        ) : (
          <div>
            <dt className="text-xs text-ink-4">{t("За {n} нед.", { n: values.length })}</dt>
            <dd className="mt-0.5 text-xl font-semibold text-ink tabular-nums">{valueText(active, stats.total, t)}</dd>
          </div>
        )}
      </dl>

      <div className="flex gap-3">
        <div className="relative w-9 shrink-0 text-[11px] text-ink-4" style={{ height: HEIGHT }} aria-hidden>
          {ticks.map((v) => (
            <span key={v} className="absolute right-0 -translate-y-1/2 whitespace-nowrap tabular-nums" style={{ top: y(v) }}>
              {valueText(active, v, t, 0)}
            </span>
          ))}
        </div>
        <div className="min-w-0 flex-1">
          <div className="relative" style={{ height: HEIGHT }}>
            {ticks.map((v) => (
              <span key={v} className={`absolute inset-x-0 h-px ${v === lo ? "bg-line-strong" : "bg-line/60"}`} style={{ top: y(v) }} aria-hidden />
            ))}
            {/* Линия цели — пунктир цветом предупреждения, метка над ней слева; у ряда без цели линии нет. */}
            {target != null && (
              <span className="absolute inset-x-0 border-t border-dashed border-warn" style={{ top: y(target) }} aria-hidden>
                <span className="absolute -top-5 left-0 rounded bg-warn/10 px-1.5 py-0.5 text-[10.5px] font-semibold leading-3 text-warn">{t("Цель {n}%", { n: target })}</span>
              </span>
            )}
            <div className="absolute inset-0 flex items-end gap-1.5 sm:gap-3.5" role="img" aria-label={t("Динамика по неделям")}>
              {values.map((v, i) => {
                if (v == null) return <div key={active.weeks[i]} className="flex-1" aria-hidden />;
                const last = i === values.length - 1;
                const color = last
                  ? "bg-gradient-to-t from-accent to-accent-bright"
                  : target == null
                    ? "bg-accent/40"
                    : v >= target
                      ? "bg-accent/55"
                      : "bg-accent/20";
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
                        <span className="text-ink-3">{active.weeks[i]}</span> · <span className="font-semibold tabular-nums">{valueText(active, v, t)}</span>
                      </span>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
          <div className="mt-2 flex gap-1.5 text-[11px] text-ink-4 sm:gap-3.5">
            {active.weeks.map((w, i) => (
              <span
                key={w}
                className={`min-w-0 flex-1 whitespace-nowrap text-center tabular-nums ${i === active.weeks.length - 1 ? "font-medium text-ink" : ""} ${
                  (active.weeks.length - 1 - i) % 2 === 1 ? "max-sm:invisible" : ""
                }`}
              >
                {w}
              </span>
            ))}
          </div>
        </div>
      </div>
    </DashCard>
  );
}
