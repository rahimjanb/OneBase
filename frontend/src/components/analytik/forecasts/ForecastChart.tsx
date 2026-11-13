"use client";

import { useCallback, useEffect, useId, useMemo, useRef, useState } from "react";
import { useLang, useT } from "@/i18n/client";
import { formatDate, localeOf } from "@/i18n/core";
import { compact } from "@/lib/analytik";
import { edgeOf, factorText, localizeUnits, niceTicks, sameWeekday, type CalendarEffect, type ChartPoint, type Step } from "@/lib/forecasts";

/** Дата точки как Date (полдень по UTC — день не съезжает в часовых поясах). */
const at = (iso: string) => new Date(`${iso}T12:00:00Z`);
const ddmm = (iso: string) => `${iso.slice(8, 10)}.${iso.slice(5, 7)}`;

/**
 * График прогноза по макету: факт (линия и заливка), пунктир выбранного сценария от последней точки факта, диапазон 80 %, прошлые прогнозы,
 * зона прогноза, «Сегодня». Подсказка — наведением, касанием и перетаскиванием, стрелками ← → с клавиатуры. compact — телефон: без оси Y,
 * 3 линии сетки, 4 подписи дат.
 */
export function ForecastChart({
  points,
  step,
  history,
  unit,
  showPast,
  compact: mobile = false,
  label,
  calendar = [],
}: {
  points: ChartPoint[];
  step: Step;
  /** Вся история (для «того же дня недели» в подсказке). */
  history: { date: string; value: number }[];
  unit: string;
  showPast: boolean;
  compact?: boolean;
  /** Описание для экранного диктора. */
  label: string;
  /** Край месяца, учтённый в прогнозе по дням: в подсказке у таких дней — множитель. */
  calendar?: CalendarEffect[];
}) {
  const t = useT();
  const lang = useLang();
  const locale = localeOf(lang);
  const id = useId().replace(/:/g, "");
  const box = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(0);
  const [active, setActive] = useState<number | null>(null);
  const touch = useRef(false);

  useEffect(() => {
    const el = box.current;
    if (!el) return;
    const ro = new ResizeObserver(([entry]) => setWidth(Math.round(entry.contentRect.width)));
    ro.observe(el);
    return () => ro.disconnect();
  }, []);

  const height = mobile ? 170 : 300;
  const left = mobile ? 0 : 44;
  const right = mobile ? 2 : 8;
  const top = mobile ? 8 : 30;
  const bottom = 4;
  const n = points.length;
  const today = points.findLastIndex((p) => p.kind === "fact");
  const max = useMemo(
    () => Math.max(1, ...points.flatMap((p) => [p.fact, p.forecast, p.upper, showPast ? p.past : null].filter((v): v is number => v != null))),
    [points, showPast],
  );
  const ticks = niceTicks(max * 1.04, mobile ? 2 : 4);
  const yMax = ticks[ticks.length - 1] || 1;
  const plotW = Math.max(1, width - left - right);
  const plotH = height - top - bottom;
  const x = useCallback((i: number) => left + (n <= 1 ? plotW / 2 : (i * plotW) / (n - 1)), [left, n, plotW]);
  const y = (v: number) => top + (1 - v / yMax) * plotH;
  const path = (idx: number[], value: (i: number) => number) => idx.map((i, k) => `${k ? "L" : "M"}${x(i).toFixed(1)},${y(value(i)).toFixed(1)}`).join("");

  const factIdx = points.map((p, i) => (p.fact != null ? i : -1)).filter((i) => i >= 0);
  const fcIdx = points.map((p, i) => (p.forecast != null ? i : -1)).filter((i) => i >= 0);
  const factPath = path(factIdx, (i) => points[i].fact!);
  const area = factIdx.length > 1 ? `${factPath}L${x(factIdx.at(-1)!).toFixed(1)},${y(0)}L${x(factIdx[0]).toFixed(1)},${y(0)}Z` : "";
  const anchor = today >= 0 ? [today] : [];
  const value = (i: number, key: "forecast" | "upper" | "lower") => (i === today ? points[i].fact! : points[i][key]!);
  const forecastPath = path([...anchor, ...fcIdx], (i) => value(i, "forecast"));
  const band = fcIdx.length
    ? `${path([...anchor, ...fcIdx], (i) => value(i, "upper"))}${[...fcIdx].reverse().concat(anchor).map((i) => `L${x(i).toFixed(1)},${y(value(i, "lower")).toFixed(1)}`).join("")}Z`
    : "";
  // Прошлые прогнозы — отрезками: где проверки нет (до первого проверенного месяца), линии нет.
  const pastPaths: string[] = [];
  if (showPast) {
    let run: number[] = [];
    points.forEach((p, i) => {
      if (p.past != null) run.push(i);
      else if (run.length) (pastPaths.push(path(run, (k) => points[k].past!)), (run = []));
    });
    if (run.length) pastPaths.push(path(run, (k) => points[k].past!));
  }

  // Подписи дат: телефон — первая, середина истории, «Сегодня», последняя; компьютер — 7 через равный шаг от «Сегодня».
  const labels = useMemo(() => {
    if (n === 0) return [] as number[];
    if (mobile) return [...new Set([0, Math.max(0, Math.round(today / 2)), Math.max(0, today), n - 1])];
    const stepK = Math.max(1, Math.round((n - 1) / 6));
    const set = new Set<number>([Math.max(0, today)]);
    for (let i = today - stepK; i >= 0; i -= stepK) set.add(i);
    for (let i = today + stepK; i < n; i += stepK) set.add(i);
    return [...set].sort((a, b) => a - b);
  }, [mobile, n, today]);
  const dateLabel = (iso: string) =>
    step === "month" ? formatDate(at(iso), locale, { month: "short", year: "2-digit", timeZone: "UTC" }) : ddmm(iso);

  const pick = (clientX: number) => {
    const el = box.current;
    if (!el || n === 0) return;
    const rect = el.getBoundingClientRect();
    const rel = clientX - rect.left - left;
    setActive(Math.max(0, Math.min(n - 1, Math.round((rel / plotW) * (n - 1)))));
  };

  const onKey = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowRight" || e.key === "ArrowLeft") {
      e.preventDefault();
      setActive((a) => Math.max(0, Math.min(n - 1, (a ?? today) + (e.key === "ArrowRight" ? 1 : -1))));
    } else if (e.key === "Escape") setActive(null);
  };

  const p = active != null ? points[active] : null;
  const fmt = (v: number | null | undefined) => (v == null ? "—" : localizeUnits(compact(v), t));
  const tipWidth = mobile ? 184 : 200;
  const tipLeft = active != null ? Math.max(0, Math.min(width - tipWidth, x(active) + (x(active) > width / 2 ? -tipWidth - 12 : 12))) : 0;
  const tipDate = (iso: string) =>
    step === "day"
      ? (() => {
          const s = formatDate(at(iso), locale, { weekday: "short", day: "numeric", month: "long", timeZone: "UTC" });
          return s.charAt(0).toUpperCase() + s.slice(1);
        })()
      : step === "week"
        ? t("Неделя с {date}", { date: ddmm(iso) })
        : (() => {
            const s = formatDate(at(iso), locale, { month: "long", year: "numeric", timeZone: "UTC" });
            return s.charAt(0).toUpperCase() + s.slice(1);
          })();
  const same = p?.kind === "forecast" && step === "day" ? sameWeekday(history, p.date) : null;
  const edge = p?.kind === "forecast" && step === "day" ? calendar.find((c) => c.kind === edgeOf(p.date)) : undefined;

  return (
    <div className="relative select-none">
      <div
        ref={box}
        role="img"
        aria-label={label}
        tabIndex={0}
        onKeyDown={onKey}
        onFocus={() => setActive((a) => a ?? (today >= 0 ? today : null))}
        onBlur={() => !touch.current && setActive(null)}
        onPointerDown={(e) => {
          touch.current = e.pointerType !== "mouse";
          pick(e.clientX);
        }}
        onPointerMove={(e) => (e.pointerType === "mouse" || e.buttons) && pick(e.clientX)}
        onPointerLeave={(e) => e.pointerType === "mouse" && setActive(null)}
        className="relative rounded-lg outline-none [touch-action:pan-y] focus-visible:ring-2 focus-visible:ring-accent"
        style={{ height }}
      >
        {width > 0 && (
          <svg width={width} height={height} className="block overflow-visible" aria-hidden>
            <defs>
              <linearGradient id={`fact-${id}`} x1="0" x2="0" y1="0" y2="1">
                <stop offset="0%" stopColor="var(--color-accent)" stopOpacity="0.2" />
                <stop offset="100%" stopColor="var(--color-accent)" stopOpacity="0" />
              </linearGradient>
            </defs>
            {/* Зона прогноза */}
            {today >= 0 && today < n - 1 && <rect x={x(today)} y={top} width={x(n - 1) - x(today)} height={plotH} fill="var(--color-violet)" opacity={0.03} />}
            {/* Сетка и ось Y */}
            {ticks.map((v) => (
              <g key={v}>
                <line x1={left} x2={width - right} y1={y(v)} y2={y(v)} stroke="var(--color-line)" strokeWidth={1} />
                {!mobile && (
                  <text x={0} y={y(v) + 3.5} className="fill-ink-4 text-[10px]">
                    {v === yMax ? localizeUnits(compact(v), t) : compact(v).replace(/\s?(млрд|млн|тыс\.)$/u, "")}
                  </text>
                )}
              </g>
            ))}
            {/* Диапазон 80 % */}
            {band && <path d={band} fill="var(--color-violet)" opacity={0.18} />}
            {/* Прошлые прогнозы */}
            {pastPaths.map((d, i) => (
              <path key={i} d={d} fill="none" stroke="var(--color-warn)" strokeOpacity={0.6} strokeWidth={mobile ? 1.3 : 1.6} strokeLinejoin="round" />
            ))}
            {/* Факт */}
            {area && <path d={area} fill={`url(#fact-${id})`} />}
            <path d={factPath} fill="none" stroke="var(--color-accent)" strokeWidth={mobile ? 1.5 : 2} strokeLinejoin="round" strokeLinecap="round" />
            {/* Прогноз выбранного сценария */}
            {fcIdx.length > 0 && <path d={forecastPath} fill="none" stroke="var(--color-violet)" strokeWidth={mobile ? 1.6 : 2.2} strokeDasharray="6 5" strokeLinecap="round" />}
            {/* Сегодня */}
            {today >= 0 && (
              <>
                <line x1={x(today)} x2={x(today)} y1={mobile ? top : top - 6} y2={y(0)} stroke="var(--color-line-strong)" strokeWidth={1} />
                <circle cx={x(today)} cy={y(points[today].fact!)} r={mobile ? 4 : 5} fill="var(--color-accent)" stroke="var(--color-surface-solid)" strokeWidth={2} />
              </>
            )}
            {/* Курсор подсказки */}
            {p && active != null && (
              <>
                <line x1={x(active)} x2={x(active)} y1={top} y2={y(0)} stroke="var(--color-violet)" strokeOpacity={0.4} strokeWidth={1} />
                <circle
                  cx={x(active)}
                  cy={y((p.kind === "fact" ? p.fact : p.forecast) ?? 0)}
                  r={5}
                  fill={p.kind === "fact" ? "var(--color-accent)" : "var(--color-violet)"}
                  stroke="var(--color-surface-solid)"
                  strokeWidth={2}
                />
              </>
            )}
          </svg>
        )}
        {/* Чип «Сегодня» (компьютер) */}
        {!mobile && width > 0 && today >= 0 && (
          <span
            className="pointer-events-none absolute top-0 -translate-x-1/2 whitespace-nowrap rounded-md border border-line-strong bg-muted px-2 py-0.5 text-[10px] font-semibold text-ink-2"
            style={{ left: x(today) }}
          >
            {t("Сегодня")}
          </span>
        )}
        {/* Подсказка */}
        {p && width > 0 && (
          <div
            role="status"
            className="pointer-events-none absolute z-10 rounded-xl border border-line-strong bg-muted p-3 shadow-[0_12px_32px_rgb(0_0_0/0.35)]"
            style={{ left: tipLeft, top: mobile ? 4 : top + 16, width: tipWidth }}
          >
            <p className="text-[11px] font-semibold text-ink-3">
              {tipDate(p.date)} · {p.kind === "fact" ? t("факт") : t("прогноз")}
            </p>
            <p className={`mt-1 text-[20px] font-bold leading-none tabular-nums ${p.kind === "fact" ? "text-ink" : "text-violet"}`}>
              {fmt(p.kind === "fact" ? p.fact : p.forecast)} <span className="text-[12px] font-medium text-ink-3">{localizeUnits(unit, t)}</span>
            </p>
            <dl className="mt-2 space-y-1 text-[11.5px]">
              {p.kind === "forecast" && (
                <Row label={t("Диапазон 80%")} value={`${fmt(p.lower)} – ${fmt(p.upper)}`} />
              )}
              {p.kind === "forecast" && p.median != null && p.median !== p.forecast && <Row label={t("Реалистичный")} value={fmt(p.median)} />}
              {same != null && <Row label={t("Тот же день недели")} value={fmt(same)} />}
              {edge && <Row label={edge.kind === "last" ? t("Конец месяца") : t("1-е число месяца")} value={factorText(edge.factor)} />}
              {p.kind === "fact" && showPast && p.past != null && <Row label={t("Прошлый прогноз")} value={fmt(p.past)} />}
            </dl>
          </div>
        )}
      </div>
      {/* Даты */}
      <div className="relative mt-2 h-4 text-[10px] text-ink-4" style={{ marginLeft: 0 }} aria-hidden>
        {width > 0 &&
          labels.map((i) => {
            const isToday = i === today;
            const first = i === labels[0];
            const last = i === labels[labels.length - 1];
            return (
              <span
                key={i}
                className={`absolute whitespace-nowrap ${isToday ? "font-semibold text-ink-2" : ""}`}
                style={{ left: x(i), transform: first ? "none" : last ? "translateX(-100%)" : "translateX(-50%)" }}
              >
                {isToday && mobile ? t("Сегодня") : dateLabel(points[i].date)}
              </span>
            );
          })}
      </div>
    </div>
  );
}

function Row({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-2">
      <dt className="text-ink-4">{label}</dt>
      <dd className="font-semibold tabular-nums text-ink">{value}</dd>
    </div>
  );
}
