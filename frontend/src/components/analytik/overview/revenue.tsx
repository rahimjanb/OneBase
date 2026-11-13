"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { compact, compactRange, date, dayMonth, forecastAccuracy, niceTicks, num, shortDate, type Dashboard, type ForecastView } from "@/lib/analytik";
import { BlockError, Bone, MoreLink, OCard } from "./parts";

type Point = { date: string; fact: number | null; forecast: number | null; lower: number | null; upper: number | null };

/** Ширина элемента (график рисуется в пикселях: подписи не растягиваются). */
function useWidth<T extends HTMLElement>() {
  const ref = useRef<T>(null);
  const [width, setWidth] = useState(0);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const observer = new ResizeObserver(() => setWidth(el.clientWidth));
    observer.observe(el);
    setWidth(el.clientWidth);
    return () => observer.disconnect();
  }, []);
  return [ref, width] as const;
}

/**
 * График выручки: факт — линия accent с заливкой, прогноз — пунктир violet от последней точки факта, диапазон прогноза — область,
 * «Сегодня» — вертикальная линия с чипом и точка последнего факта. Наведение (касание) — дата, факт или прогноз с диапазоном.
 * compact — телефон: без оси Y, 3 линии сетки, 4 подписи дат.
 */
function Chart({ points, compact: small = false }: { points: Point[]; compact?: boolean }) {
  const t = useT();
  const [box, width] = useWidth<HTMLDivElement>();
  const [hover, setHover] = useState<number | null>(null);
  const height = small ? 150 : 250;
  const left = small ? 4 : 44;
  const right = small ? 4 : 8;
  const top = small ? 22 : 26;
  const bottom = 22;
  const n = points.length;
  const today = points.reduce((last, p, i) => (p.fact != null ? i : last), -1);
  const max = Math.max(1, ...points.flatMap((p) => [p.fact ?? 0, p.forecast ?? 0, p.upper ?? 0]));
  const ticks = niceTicks(max, small ? 2 : 4);
  const yMax = ticks[ticks.length - 1] || 1;
  const plotW = Math.max(10, width - left - right);
  const plotH = height - top - bottom;
  const x = useCallback((i: number) => left + (n <= 1 ? plotW / 2 : (i * plotW) / (n - 1)), [left, n, plotW]);
  const y = useCallback((v: number) => top + plotH - (v / yMax) * plotH, [top, plotH, yMax]);

  const paths = useMemo(() => {
    const line = (idx: number[], value: (p: Point) => number | null) =>
      idx
        .filter((i) => value(points[i]) != null)
        .map((i, k) => `${k === 0 ? "M" : "L"}${x(i).toFixed(1)},${y(value(points[i])!).toFixed(1)}`)
        .join(" ");
    const factIdx = points.map((_, i) => i).filter((i) => points[i].fact != null);
    const futureIdx = points.map((_, i) => i).filter((i) => points[i].forecast != null);
    const fact = line(factIdx, (p) => p.fact);
    const area = factIdx.length > 1 ? `${fact} L${x(factIdx[factIdx.length - 1]).toFixed(1)},${y(0).toFixed(1)} L${x(factIdx[0]).toFixed(1)},${y(0).toFixed(1)} Z` : "";
    // Прогноз и диапазон начинаются от последней точки факта — без разрыва.
    const start = today >= 0 ? [today] : [];
    const forecastLine = line([...start, ...futureIdx], (p) => p.forecast ?? p.fact);
    const bandIdx = [...start, ...futureIdx];
    const band =
      bandIdx.length > 1
        ? `${bandIdx.map((i, k) => `${k === 0 ? "M" : "L"}${x(i).toFixed(1)},${y(points[i].upper ?? points[i].fact ?? 0).toFixed(1)}`).join(" ")} ${[...bandIdx]
            .reverse()
            .map((i) => `L${x(i).toFixed(1)},${y(points[i].lower ?? points[i].fact ?? 0).toFixed(1)}`)
            .join(" ")} Z`
        : "";
    return { fact, area, forecastLine, band };
  }, [points, x, y, today]);

  const labels = useMemo(() => {
    if (n === 0) return [] as { i: number; text: string }[];
    if (small) {
      const mid = today > 0 ? Math.round(today / 2) : Math.round(n / 3);
      return [
        { i: 0, text: shortDate(points[0].date) },
        { i: mid, text: shortDate(points[mid].date) },
        ...(today > 0 && today < n - 1 ? [{ i: today, text: t("Сегодня") }] : []),
        { i: n - 1, text: shortDate(points[n - 1].date) },
      ];
    }
    const count = Math.min(6, n);
    return Array.from({ length: count }, (_, k) => Math.round((k * (n - 1)) / Math.max(1, count - 1))).map((i) => ({ i, text: shortDate(points[i].date) }));
  }, [n, points, small, today, t]);

  const pick = (clientX: number) => {
    const el = box.current;
    if (!el || n === 0) return;
    const rel = clientX - el.getBoundingClientRect().left - left;
    setHover(Math.max(0, Math.min(n - 1, Math.round((rel / plotW) * (n - 1)))));
  };
  const hp = hover != null ? points[hover] : null;
  const gradient = small ? "rev-fill-m" : "rev-fill";

  return (
    <div ref={box} className="relative w-full select-none" style={{ height }}>
      {width > 0 && (
        <svg
          width={width}
          height={height}
          className="block touch-pan-y"
          onPointerMove={(e) => pick(e.clientX)}
          onPointerDown={(e) => pick(e.clientX)}
          onPointerLeave={(e) => e.pointerType === "mouse" && setHover(null)}
          role="img"
          aria-label={t("Выручка по дням: факт и прогноз на 30 дней")}
        >
          <defs>
            <linearGradient id={gradient} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#3d7bff" stopOpacity="0.25" />
              <stop offset="100%" stopColor="#3d7bff" stopOpacity="0" />
            </linearGradient>
          </defs>
          {ticks.map((v) => (
            <g key={v}>
              <line x1={left} x2={width - right} y1={y(v)} y2={y(v)} stroke="var(--color-line)" strokeWidth="1" />
              {!small && (
                <text x={0} y={y(v) + 3} className="fill-ink-4" style={{ fontSize: 10 }}>
                  {v === 0 ? "0" : compact(v)}
                </text>
              )}
            </g>
          ))}
          {paths.band && <path d={paths.band} fill="#8b5cf6" fillOpacity="0.15" />}
          {paths.area && <path d={paths.area} fill={`url(#${gradient})`} />}
          {paths.fact && <path d={paths.fact} fill="none" stroke="var(--color-accent)" strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" />}
          {paths.forecastLine && <path d={paths.forecastLine} fill="none" stroke="var(--color-violet)" strokeWidth="2" strokeDasharray="6 5" strokeLinejoin="round" />}
          {today >= 0 && today < n - 1 && (
            <>
              <line x1={x(today)} x2={x(today)} y1={top - 6} y2={height - bottom} stroke="var(--color-line-strong)" strokeWidth="1" />
              {!small && (
                <g transform={`translate(${x(today)}, ${top - 14})`}>
                  <rect x={-31} y={-9} width={62} height={18} rx={6} className="fill-muted" stroke="var(--color-line-strong)" />
                  <text x={0} y={4} textAnchor="middle" className="fill-ink-2" style={{ fontSize: 10, fontWeight: 600 }}>
                    {t("Сегодня")}
                  </text>
                </g>
              )}
            </>
          )}
          {today >= 0 && points[today].fact != null && (
            <circle cx={x(today)} cy={y(points[today].fact!)} r={5} fill="var(--color-accent)" stroke="var(--color-surface)" strokeWidth={2} />
          )}
          {hover != null && <line x1={x(hover)} x2={x(hover)} y1={top} y2={height - bottom} stroke="var(--color-ink-4)" strokeDasharray="3 3" />}
          {labels.map((l) => (
            <text
              key={`${l.i}-${l.text}`}
              x={x(l.i)}
              y={height - 5}
              textAnchor={l.i === 0 ? "start" : l.i === n - 1 ? "end" : "middle"}
              className={l.i === today ? "fill-ink-2" : "fill-ink-4"}
              style={{ fontSize: 10, fontWeight: l.i === today ? 600 : 400 }}
            >
              {l.text}
            </text>
          ))}
        </svg>
      )}
      {hp && hover != null && (
        <div
          className="pointer-events-none absolute z-10 min-w-[150px] rounded-xl border border-line bg-surface-solid px-3 py-2 text-[11px] shadow-lg"
          style={{ left: Math.min(Math.max(0, x(hover) + 12), Math.max(0, width - 170)), top: 4 }}
        >
          <div className="font-semibold text-ink">{date(hp.date)}</div>
          {hp.fact != null ? (
            <div className="mt-0.5 flex items-center gap-1.5 text-ink-2">
              <span className="size-2 rounded-full bg-accent" />
              {t("Факт")}: <b className="text-ink">{compact(hp.fact)}</b>
            </div>
          ) : hp.forecast != null ? (
            <>
              <div className="mt-0.5 flex items-center gap-1.5 text-ink-2">
                <span className="size-2 rounded-full bg-violet" />
                {t("Прогноз")}: <b className="text-ink">{compact(hp.forecast)}</b>
              </div>
              {hp.lower != null && hp.upper != null && <div className="text-ink-4">{t("диапазон")}: {compactRange(hp.lower, hp.upper)}</div>}
            </>
          ) : null}
        </div>
      )}
      <table className="sr-only">
        <caption>{t("Выручка по дням")}</caption>
        <thead>
          <tr>
            <th>{t("Дата")}</th>
            <th>{t("Факт")}</th>
            <th>{t("Прогноз")}</th>
          </tr>
        </thead>
        <tbody>
          {points.map((p) => (
            <tr key={p.date}>
              <td>{date(p.date)}</td>
              <td>{p.fact == null ? "—" : compact(p.fact)}</td>
              <td>{p.forecast == null ? "—" : `${compact(p.forecast)} (${compactRange(p.lower ?? p.forecast, p.upper ?? p.forecast)})`}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/** Итог под графиком; на телефоне — мини-блок с короткой подписью и значением (short). */
function Stat({ label, value, tone, short }: { label: string; value: string; tone: string; short?: { label: string; value: string } }) {
  const t = useT();
  return (
    <div className="min-w-0 border-l border-line pl-4 max-md:rounded-xl max-md:border max-md:bg-muted max-md:px-2.5 max-md:py-2">
      <p className="truncate text-[10px] font-semibold text-ink-4 max-md:uppercase max-md:tracking-wide">
        <span className={short ? "max-md:hidden" : undefined}>{t(label)}</span>
        {short && <span className="md:hidden">{t(short.label)}</span>}
      </p>
      <p className={`mt-0.5 truncate text-[14px] font-bold tabular-nums max-md:text-[13px] ${tone}`}>
        <span className={short ? "max-md:hidden" : undefined}>{value}</span>
        {short && <span className="md:hidden">{short.value}</span>}
      </p>
    </div>
  );
}

/**
 * «Выручка: история и прогноз на 30 дней»: «День / Неделя» — шаг прогноза на сервере; прогноз грузится отдельным запросом (история —
 * полгода продаж), пока его нет — факт по дням периода; ошибка — плашка с «Повторить», страница работает.
 */
export function RevenueCard({ data }: { data: Dashboard }) {
  const t = useT();
  const [step, setStep] = useState<"day" | "week">("day");
  const [state, setState] = useState<{ step: string; view: ForecastView | null; error: string | null }>({ step: "", view: null, error: null });
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    let alive = true;
    bff<ForecastView>(`analytik/forecast?metric=sales.revenue&granularity=${step}&horizon=${step === "day" ? 30 : 4}`)
      .then((view) => alive && setState({ step, view, error: null }))
      .catch((e: Error) => alive && setState({ step, view: null, error: e.message }));
    return () => {
      alive = false;
    };
  }, [step, attempt]);
  const view = state.step === step ? state.view : null;
  const error = state.step === step ? state.error : null;

  const points: Point[] = useMemo(() => {
    if (view) {
      const history = view.history.slice(step === "day" ? -45 : -12);
      return [
        ...history.map((h) => ({ date: h.date, fact: h.value, forecast: null, lower: null, upper: null })),
        ...view.result.points.map((p) => ({ date: p.date, fact: null, forecast: p.value, lower: p.lower, upper: p.upper })),
      ];
    }
    return (data.sales?.daily ?? []).map((d) => ({ date: d.date, fact: d.revenue, forecast: null, lower: null, upper: null }));
  }, [view, step, data.sales?.daily]);

  const revenue = data.sales?.kpis.find((k) => k.code === "sales.revenue")?.value ?? null;
  const accuracy = view ? forecastAccuracy(view) : null;
  const horizon = step === "day" ? t("Прогноз на 30 дней") : t("Прогноз на 4 недели");
  const toggle = (
    <div className="flex items-center gap-3">
      <div role="radiogroup" aria-label={t("Шаг графика")} className="inline-flex rounded-[10px] border border-line bg-muted p-[3px]">
        {(["day", "week"] as const).map((s) => (
          <button
            key={s}
            type="button"
            role="radio"
            aria-checked={step === s}
            onClick={() => setStep(s)}
            className={`rounded-[7px] px-3 py-1 text-[12px] font-semibold ${step === s ? "bg-surface text-ink shadow-sm" : "text-ink-3 hover:text-ink"}`}
          >
            {t(s === "day" ? "День" : "Неделя")}
          </button>
        ))}
      </div>
      <span className="max-md:hidden">
        <MoreLink href="/analytik/forecasts" />
      </span>
    </div>
  );

  return (
    <OCard
      title={
        <>
          <span className="max-md:hidden">{t("Выручка: история и прогноз на 30 дней")}</span>
          <span className="md:hidden">{t("Выручка и прогноз")}</span>
        </>
      }
      subtitle={
        <>
          <span className="max-md:hidden">{t("млн сум в день · прогноз строит AI по продажам, сезонности и плану")}</span>
          <span className="md:hidden">{t("млн сум в день · 30 дней вперёд")}</span>
        </>
      }
      action={<span className="max-md:hidden">{toggle}</span>}
      className="h-full"
    >
      {!view && !error && points.length === 0 ? (
        <Bone className="h-[250px] w-full max-md:h-[150px]" />
      ) : points.length === 0 ? (
        // Нет ни факта, ни прогноза (данных продаж ещё нет) — спокойное пустое состояние с причиной, а не ошибка.
        <div className="py-10 text-center">
          <p className="text-[13px] text-ink-2">{t("Нет данных о выручке за период")}</p>
          {error && <p className="mx-auto mt-1 max-w-[520px] text-[11px] text-ink-4">{error}</p>}
        </div>
      ) : (
        <>
          <div className="max-md:hidden">
            <Chart points={points} />
          </div>
          <div className="md:hidden">
            <Chart points={points} compact />
          </div>
        </>
      )}
      {error && points.length > 0 && (
        <div className="mt-3">
          <BlockError text={`${t("Прогноз не построен")}: ${error}`} onRetry={() => setAttempt((a) => a + 1)} />
        </div>
      )}
      <div className="mt-auto flex flex-wrap items-end justify-between gap-x-6 gap-y-3 border-t border-line pt-3.5 max-md:mt-3 max-md:border-t-0 max-md:pt-0">
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[11px] text-ink-3 max-md:hidden">
          <span className="inline-flex items-center gap-1.5">
            <span className="h-0.5 w-4 rounded-full bg-accent" />
            {t("Факт")}
          </span>
          <span className="inline-flex items-center gap-1.5">
            <span className="h-0 w-4 border-t-2 border-dashed border-violet" />
            {t("Прогноз")}
          </span>
          <span className="inline-flex items-center gap-1.5">
            <span className="h-2.5 w-4 rounded-sm bg-violet/25" />
            {t("Диапазон прогноза")}
          </span>
        </div>
        <div className="grid grid-cols-4 gap-y-2 max-md:w-full max-md:grid-cols-3 max-md:gap-2">
          <span className="max-md:hidden">
            <Stat label={`${t("Факт с")} ${dayMonth(data.period.from)}`} value={revenue == null ? "—" : compact(revenue)} tone="text-ink" />
          </span>
          <Stat
            label={horizon}
            value={view ? compact(view.result.total) : "—"}
            tone="text-violet"
            short={{ label: step === "day" ? "Прогноз 30 дней" : "Прогноз 4 нед.", value: view ? compact(view.result.total) : "—" }}
          />
          <Stat
            label="Диапазон"
            value={view ? compactRange(view.result.totalLower, view.result.totalUpper) : "—"}
            tone="text-ink-2"
            short={{ label: "Диапазон", value: view ? compactRange(view.result.totalLower, view.result.totalUpper).replace(/\u00a0\S+$/, "").replace(" – ", "–") : "—" }}
          />
          <Stat
            label="Точность модели"
            value={accuracy == null ? "—" : `${num(accuracy)}%`}
            tone={accuracy == null ? "text-ink-4" : "text-ok"}
            short={{ label: "Точность", value: accuracy == null ? "—" : `${num(accuracy)}%` }}
          />
        </div>
      </div>
    </OCard>
  );
}
