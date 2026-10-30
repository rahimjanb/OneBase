"use client";

import { useEffect, useRef, useState } from "react";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import type { TrendSeries } from "@/lib/dashboard.mock";
import { trendHref, trendScale, trendStats } from "@/lib/dashboard-home";
import { fmt } from "@/lib/dashboard-mobile";
import { plural } from "@/lib/format";
import { money } from "@/lib/sales/format";
import m from "./mobile.module.css";
import { card, focusRing, SectionHeader } from "./ui";

const H = 130;
const week = (label: string) => label.replace(/^\D+/, "");
/** Значение недели: «92,0%» или «3,41 млрд». */
const valueText = (s: TrendSeries, v: number, t: T) => (s.unit === "money" ? money(v, t) : `${fmt(v, 1)}%`);

/**
 * «Динамика»: вкладки рядов (продажи к плану, выручка, явка — какие есть), три итога и 12 недель столбиками. Шкала — общая с компьютером
 * (trendScale), без подписей оси. Линия цели — позади столбиков; у ряда без цели (выручка) линии нет, третий итог — сумма за период.
 * Столбики ниже цели — бледные, выше — ярче, текущая неделя — градиентом; неделя без данных — пустое место. Тап по столбику — значение.
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
  if (!s) {
    return (
      <section className="flex flex-col gap-[12px]">
        <SectionHeader title={t("Динамика")} />
        <p className={`${card} px-[18px] py-[22px] text-center text-[14px] text-(--ts-muted)`}>{t("Нет данных за период")}</p>
      </section>
    );
  }
  const values = s.values;
  const target: number | undefined = s.target;
  const last = values.length - 1;
  const { lo, hi } = trendScale(s);
  const h = (v: number) => ((Math.max(lo, Math.min(hi, v)) - lo) / (hi - lo)) * H;
  const st = trendStats(s);

  const stats = [
    { label: t("Среднее"), value: st.average == null ? "—" : valueText(s, st.average, t), tone: "text-(--ts-text)" },
    { label: t("Лучшая неделя"), value: st.best == null ? "—" : valueText(s, st.best, t), tone: "text-(--ts-success)" },
    st.above != null
      ? { label: t("Выше цели"), value: t("{n} из {total}", { n: st.above, total: st.count }), tone: "text-(--ts-text)" }
      : { label: t("За {n} нед.", { n: values.length }), value: valueText(s, st.total, t), tone: "text-(--ts-text)" },
  ];

  return (
    <section className="flex flex-col gap-[12px]">
      <SectionHeader title={t("Динамика")} href={trendHref(s)} action={t("Подробнее")} />
      <div className={`${card} flex flex-col gap-[16px] p-[18px]`}>
        <div
          role="tablist"
          aria-label={t("Показатель")}
          className={`grid ${series.length === 3 ? "grid-cols-3" : series.length === 2 ? "grid-cols-2" : "grid-cols-1"} gap-[2px] rounded-[12px] border border-(--ts-border) bg-(--ts-bg) p-[3px]`}
        >
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
              {/* Суммы («38,18 млрд») длиннее процентов — мельче, чтобы не переносились в узкой колонке. */}
              <span className={`whitespace-nowrap font-bold leading-tight ${s.unit === "money" ? "text-[clamp(13px,3.6vw,15px)]" : "text-[17px]"} ${st.tone}`}>{st.value}</span>
            </div>
          ))}
        </div>

        <div ref={chart}>
          <div
            role="group"
            aria-label={t("{name} по неделям: среднее {avg}, лучшая неделя {best}", { name: s.label, avg: stats[0].value, best: stats[1].value })}
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
                // Неделя без данных (до начала истории, нет плана) — пустое место той же ширины.
                if (v == null) return <span key={s.weeks[i]} aria-hidden className="min-w-0 flex-1" />;
                const current = i === last;
                const color = current
                  ? "bg-[linear-gradient(to_bottom,#6e9bff,#3d7bff)]"
                  : target === undefined
                    ? "bg-[color-mix(in_srgb,var(--ts-accent)_40%,transparent)]"
                    : v >= target
                      ? "bg-[color-mix(in_srgb,var(--ts-accent)_55%,transparent)]"
                      : "bg-[color-mix(in_srgb,var(--ts-accent)_25%,transparent)]";
                const align = i < 2 ? "left-0" : i > last - 2 ? "right-0" : "left-1/2 -translate-x-1/2";
                return (
                  <button
                    key={s.weeks[i]}
                    type="button"
                    data-press="off"
                    onClick={() => setTip((x) => (x === i ? null : i))}
                    aria-label={t("Неделя {w}: {v}", { w: week(s.weeks[i]), v: valueText(s, v, t) })}
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
                        {t("Нед. {w}", { w: week(s.weeks[i]) })} · <b className="font-bold">{valueText(s, v, t)}</b>
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
