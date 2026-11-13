"use client";

import Link from "next/link";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Banknote,
  BrainCircuit,
  CalendarDays,
  CalendarRange,
  ChartNoAxesCombined,
  Download,
  Ellipsis,
  HandCoins,
  Layers,
  Package,
  ReceiptText,
  RotateCcw,
  SlidersHorizontal,
  Sparkles,
  Table2,
  Wheat,
  type LucideIcon,
} from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { NotificationBell } from "@/components/shell/NotificationBell";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { analytikTabs } from "@/components/shell/sections";
import { useT } from "@/i18n/client";
import { compact as compactNum, type ForecastView, type SupplyPlan } from "@/lib/analytik";
import {
  HORIZONS,
  METRICS,
  STEPS,
  amount,
  backtestQuery,
  calendarOf,
  chartPoints,
  dataAsOf,
  forecastQuery,
  localizeUnits,
  METHOD_NAMES,
  pct,
  prevPeriodKey,
  previousTotal,
  scenarioCards,
  simulatorHref,
  toCsv,
  toQuery,
  type BacktestView,
  type ForecastMetric,
  type ForecastParams,
  type Horizon,
  type Step,
} from "@/lib/forecasts";
import { ForecastChart } from "./ForecastChart";
import { AccuracyCard, AccuracyTile, Assumptions, FieldLabel, Methods, PastAccuracy, Picker, ScenarioCards, Segments, WhatIf, card } from "./parts";

const METRIC_ICON: Record<ForecastMetric, LucideIcon> = {
  "sales.revenue": Banknote,
  "sales.kg": Package,
  "sales.orders": ReceiptText,
  "finance.payments": HandCoins,
  "procurement.consumption": Wheat,
};

type Failure = { message: string; noData: boolean };

/** Запрос к API через /bff: ошибка — текст сервера и признак «нет данных» (code no_data); отменённый запрос — null. */
async function load<T>(path: string, signal: AbortSignal): Promise<T | null> {
  let response: Response;
  try {
    response = await fetch(`/bff/api/${path}`, { signal, headers: { "Content-Type": "application/json" } });
  } catch (e) {
    if (signal.aborted) return null;
    throw { message: (e as Error).message, noData: false } satisfies Failure;
  }
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw { message: body?.error ?? `HTTP ${response.status}`, noData: body?.code === "no_data" } satisfies Failure;
  }
  return (await response.json()) as T;
}

const ddmm = (iso: string | null, year = false) => (iso ? `${iso.slice(8, 10)}.${iso.slice(5, 7)}${year ? `.${iso.slice(0, 4)}` : ""}` : "—");

/**
 * «Аналитик → Прогнозы» по макету (UVDBc — компьютер, CBnGo — телефон): показатель, срез, шаг, горизонт, «что если», выбираемые сценарии,
 * точность, график с прошлыми прогнозами, проверенные методы, точность прошлых месяцев, допущения. Все параметры — в адресе. Прогноз
 * считает сервер; новые параметры — запрос через 300 мс, прошлый отменяется, старые цифры видны приглушёнными до ответа.
 */
export function ForecastsScreen({ initial, metrics, permissions }: { initial: ForecastParams; metrics: ForecastMetric[]; permissions: string[] }) {
  const t = useT();
  const [p, setP] = useState(initial);
  const [view, setView] = useState<ForecastView | null>(null);
  const [failure, setFailure] = useState<Failure | null>(null);
  const [loading, setLoading] = useState(true);
  const [retry, setRetry] = useState(0);
  const [backtest, setBacktest] = useState<BacktestView | null>(null);
  const [btLoading, setBtLoading] = useState(true);
  const [items, setItems] = useState<{ key: string; label: string; hint?: string }[] | null>(null);
  const [table, setTable] = useState(false);
  const [menu, setMenu] = useState(false);
  const [wapeSheet, setWapeSheet] = useState(false);
  const set = useCallback((patch: Partial<ForecastParams>) => setP((x) => ({ ...x, ...patch })), []);

  // Адрес страницы — без перехода (сервер ничего не пересчитывает: данные берёт экран).
  useEffect(() => {
    const url = `${window.location.pathname}?${toQuery(p)}`;
    if (url !== `${window.location.pathname}${window.location.search}`) window.history.replaceState(window.history.state, "", url);
  }, [p]);

  const fq = forecastQuery(p);
  useEffect(() => {
    const ctrl = new AbortController();
    setLoading(true);
    const timer = window.setTimeout(() => {
      load<ForecastView>(`analytik/forecast?${fq}`, ctrl.signal)
        .then((v) => {
          if (!v) return;
          setView(v);
          setFailure(null);
          setLoading(false);
        })
        .catch((e: Failure) => {
          if (ctrl.signal.aborted) return;
          setFailure(e);
          if (e.noData) setView(null);
          setLoading(false);
        });
    }, 300);
    return () => (window.clearTimeout(timer), ctrl.abort());
  }, [fq, retry]);

  const bq = backtestQuery(p);
  useEffect(() => {
    const ctrl = new AbortController();
    setBtLoading(true);
    load<BacktestView>(`analytik/forecast/backtest?${bq}`, ctrl.signal)
      .then((v) => v && (setBacktest(v), setBtLoading(false)))
      .catch(() => !ctrl.signal.aborted && (setBacktest(null), setBtLoading(false)));
    return () => ctrl.abort();
  }, [bq, retry]);

  // Срез расхода сырья — позиции с расходом (план закупок Аналитика), только когда выбран этот показатель.
  useEffect(() => {
    if (p.metric !== "procurement.consumption" || items) return;
    const ctrl = new AbortController();
    load<SupplyPlan>("analytik/supply", ctrl.signal)
      .then((plan) => plan && setItems(plan.items.filter((i) => i.avgDailyConsumption > 0).sort((a, b) => a.name.localeCompare(b.name, "ru")).map((i) => ({ key: i.itemId, label: i.name, hint: i.unit }))))
      .catch(() => !ctrl.signal.aborted && setItems([]));
    return () => ctrl.abort();
  }, [p.metric, items]);

  const unit = view?.unit ?? (p.metric === "sales.kg" ? "кг" : p.metric === "sales.orders" ? "шт" : p.metric === "procurement.consumption" ? "ед." : "сум");
  const points = useMemo(() => (view ? chartPoints(view, p.scenario, p.past ? backtest : null) : []), [view, p.scenario, p.past, backtest]);
  const cards = useMemo(() => (view ? scenarioCards(view) : []), [view]);
  const n = view?.result.points.length ?? 0;
  const prevText = t(prevPeriodKey(view?.granularity ?? p.step), { n });
  const lu = (text: string) => localizeUnits(text, t);
  const money = (v: number) => {
    const a = amount(v, unit);
    return lu(`${a.head} ${a.tail}`);
  };
  const asOf = dataAsOf(view);
  const method = view ? METHOD_NAMES[view.result.method] : null;
  const typical = view && view.history.length ? [...view.history].map((h) => h.value).sort((a, b) => a - b)[Math.floor(view.history.length / 2)] : 0;
  const perUnit = `${lu(amount(typical || 1, unit).tail)} ${view?.granularity === "week" ? t("в неделю") : view?.granularity === "month" ? t("в месяц") : t("в день")}`;
  const stepWord = (count: number) =>
    t(view?.granularity === "week" ? (count % 10 === 1 && count % 100 !== 11 ? "неделя" : "недель") : view?.granularity === "month" ? "мес." : count % 10 === 1 && count % 100 !== 11 ? "день" : count % 10 >= 2 && count % 10 <= 4 && (count % 100 < 12 || count % 100 > 14) ? "дня" : "дней");
  const sim = simulatorHref(p);
  const whatIfNote = p.whatIf ? t("с учётом {v}", { v: `${p.whatIf > 0 ? "+" : "−"}${Math.abs(p.whatIf)}%` }) : null;
  const season = view ? (view.granularity === "day" ? 7 : view.granularity === "month" ? 12 : 0) : 0;
  const shortHistory = !!view && !view.result.reliable && (view.result.wape == null || (season > 0 && view.result.history < 2 * season + 2));
  const realistic = cards.find((c) => c.key === "realistic");
  const prev = view ? previousTotal(view) : null;

  const metricItems = METRICS.filter((m) => metrics.includes(m.metric));
  const sliceItems =
    p.metric === "procurement.consumption"
      ? [{ key: "", label: t("Всё сырьё") }, ...(items ?? [])]
      : p.metric === "finance.payments"
        ? [{ key: "", label: t("Все точки") }]
        : [{ key: "", label: t("Вторичка"), hint: view?.scope ?? undefined }];
  const stepItems = STEPS.map((s) => ({ key: s.step, label: t(s.label) }));
  const horizonItems = HORIZONS.map((h) => ({ key: h, label: t("{n} дн.", { n: h }) }));

  const exportCsv = () => {
    if (!view) return;
    const csv = toCsv(points, [t("Дата"), t("Факт"), t("Прогноз"), "P10", "P90", t("Прошлый прогноз")]);
    const blob = new Blob([csv], { type: "text/csv;charset=utf-8" });
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = `forecast-${p.metric}-${p.step}-${asOf ?? "today"}.csv`;
    a.click();
    URL.revokeObjectURL(a.href);
  };

  const title = view ? t(view.title) : t(METRICS.find((m) => m.metric === p.metric)?.label ?? "");
  const chartLabel = view
    ? t("{title}: история за {h} и прогноз на {n}; реалистичный — {v}, диапазон {lo} – {hi}.", {
        title,
        h: `${Math.min(view.history.length, points.filter((x) => x.kind === "fact").length)} ${stepWord(points.filter((x) => x.kind === "fact").length)}`,
        n: `${n} ${stepWord(n)}`,
        v: money(view.result.total),
        lo: lu(compactNum(view.result.totalLower)),
        hi: lu(compactNum(view.result.totalUpper)),
      })
    : title;

  // ---------- блоки ----------

  const scenariosBlock = (mobile: boolean) =>
    view ? (
      <ScenarioCards cards={cards} value={p.scenario} onChange={(scenario) => set({ scenario })} unit={unit} prevText={prevText} compact={mobile} loading={loading} />
    ) : loading ? (
      <div className={`grid grid-cols-3 ${mobile ? "gap-1.5" : "gap-3"}`} aria-busy="true">
        {[0, 1, 2].map((i) => (
          <div key={i} className={`animate-pulse rounded-2xl border border-line bg-surface ${mobile ? "h-[86px]" : "h-[132px]"}`} />
        ))}
      </div>
    ) : null;

  const chartHeader = (mobile: boolean) => (
    <div className="flex items-start gap-3">
      <div className="min-w-0 flex-1">
        <h2 className={`font-bold text-ink ${mobile ? "text-[15px]" : "text-[16px]"}`}>{mobile ? title : t("{title}: история и прогноз", { title })}</h2>
        <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
          {method && (
            <span className="inline-flex items-center gap-1 rounded-md bg-violet/15 px-1.5 py-0.5 text-[10px] font-bold text-violet md:text-[11px]">
              <Sparkles aria-hidden className="size-[11px]" />
              {t(method.name)}
              {!mobile && ` · ${t(method.about)}`}
            </span>
          )}
          {!mobile && <span className="rounded-md bg-muted px-1.5 py-0.5 text-[11px] font-semibold text-ink-2">{t("доверительный интервал {n}%", { n: Math.round((view?.result.confidence ?? 0.8) * 100) })}</span>}
          <span className="text-[10px] text-ink-4 md:text-[11px]">
            {mobile
              ? `${t("интервал {n}%", { n: Math.round((view?.result.confidence ?? 0.8) * 100) })} · ${perUnit}`
              : `${t("история {n}", { n: `${view?.result.history ?? 0} ${stepWord(view?.result.history ?? 0)}` })} · ${perUnit}`}
            {whatIfNote && ` · ${whatIfNote}`}
          </span>
        </div>
      </div>
      {mobile ? (
        <div className="relative" data-popover>
          <button type="button" onClick={() => setMenu((m) => !m)} aria-label={t("Ещё")} aria-expanded={menu} className="grid size-8 place-items-center rounded-full bg-muted text-ink-2">
            <Ellipsis aria-hidden className="size-4" />
          </button>
          {menu && (
            <div role="menu" className="absolute right-0 z-30 mt-1.5 w-60 rounded-xl border border-line bg-surface-solid p-1.5 shadow-lg">
              <MenuToggle label={t("Прошлые прогнозы")} on={p.past} onClick={() => (set({ past: !p.past }), setMenu(false))} />
              <MenuButton icon={Table2} label={table ? t("Скрыть таблицу") : t("Показать таблицей")} onClick={() => (setTable((x) => !x), setMenu(false))} />
              <MenuButton icon={Download} label={t("Экспорт CSV")} onClick={() => (exportCsv(), setMenu(false))} />
            </div>
          )}
        </div>
      ) : (
        <label className="inline-flex shrink-0 cursor-pointer items-center gap-2.5 text-[13px] text-ink-2">
          <span className="relative inline-flex">
            <input type="checkbox" role="switch" checked={p.past} onChange={(e) => set({ past: e.target.checked })} className="peer sr-only" />
            <span className="h-6 w-11 rounded-full bg-line-strong transition-colors peer-checked:bg-accent peer-focus-visible:ring-2 peer-focus-visible:ring-accent" aria-hidden />
            <span className="absolute left-0.5 top-0.5 size-5 rounded-full bg-white transition-transform peer-checked:translate-x-5" aria-hidden />
          </span>
          {t("Прошлые прогнозы")}
        </label>
      )}
    </div>
  );

  const chartBody = (mobile: boolean) =>
    failure && !failure.noData ? (
      <div className="flex flex-wrap items-center gap-3 rounded-xl bg-bad/10 px-4 py-3 text-[13px] text-bad">
        <span className="min-w-0 flex-1">{t("Прогноз не построился: {m}", { m: failure.message })}</span>
        <button type="button" onClick={() => setRetry((r) => r + 1)} className="inline-flex h-8 items-center gap-1.5 rounded-lg bg-bad/15 px-3 font-semibold">
          <RotateCcw aria-hidden className="size-3.5" />
          {t("Повторить")}
        </button>
      </div>
    ) : failure?.noData ? (
      <div className="flex flex-col items-center gap-2 py-10 text-center">
        <ChartNoAxesCombined aria-hidden className="size-8 text-ink-4" />
        <p className="text-[14px] font-semibold text-ink">{t("Нет данных за период")}</p>
        <p className="max-w-sm text-[12px] text-ink-3">{t("По этому показателю и срезу в OneBase нет истории. Выберите другой показатель или срез.")}</p>
      </div>
    ) : view ? (
      <div className={loading ? "opacity-50 transition-opacity" : "transition-opacity"}>
        <ForecastChart points={points} step={view.granularity} history={view.history} unit={unit} showPast={p.past} compact={mobile} label={chartLabel} calendar={calendarOf(view)} />
      </div>
    ) : (
      <div className={`animate-pulse rounded-xl bg-muted ${mobile ? "h-[190px]" : "h-[322px]"}`} aria-busy="true" aria-label={t("Загрузка")} />
    );

  const legend = (mobile: boolean) => (
    <div className={`flex flex-wrap items-center text-ink-3 ${mobile ? "gap-3 text-[10px]" : "gap-x-5 gap-y-2 text-[12px]"}`}>
      <LegendLine color="bg-accent" label={t("Факт")} />
      <LegendLine color="bg-violet" label={mobile ? t("Прогноз") : t("Прогноз ({s})", { s: t(p.scenario === "pessimistic" ? "пессимистичный" : p.scenario === "optimistic" ? "оптимистичный" : "реалистичный") })} />
      <span className="inline-flex items-center gap-1.5">
        <span aria-hidden className="h-2.5 w-3.5 rounded-sm bg-violet/30" />
        {mobile ? t("Диапазон") : t("Диапазон {n}%", { n: Math.round((view?.result.confidence ?? 0.8) * 100) })}
      </span>
      {p.past && (backtest?.past.length ?? 0) > 0 && <LegendLine color="bg-warn/70" label={t("Прошлые прогнозы")} />}
    </div>
  );

  const dataTable = view && table && (
    <div className="mt-3 max-h-80 overflow-auto rounded-xl border border-line">
      <table className="w-full text-[12px]">
        <caption className="sr-only">{t("{title}: история и прогноз", { title })}</caption>
        <thead className="sticky top-0 bg-muted text-ink-3">
          <tr>
            {[t("Дата"), t("Факт"), t("Прогноз"), "P10", "P90", ...(p.past ? [t("Прошлый прогноз")] : [])].map((h) => (
              <th key={h} scope="col" className="px-3 py-2 text-right font-semibold first:text-left">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="tabular-nums text-ink-2">
          {points.map((r) => (
            <tr key={r.date} className="border-t border-line">
              <td className="px-3 py-1.5">{ddmm(r.date, true)}</td>
              {[r.fact, r.forecast, r.lower, r.upper, ...(p.past ? [r.past] : [])].map((v, i) => (
                <td key={i} className="px-3 py-1.5 text-right">
                  {v == null ? "" : lu(compactNum(v))}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );

  const bottomMethods = (mobile: boolean) => (
    <section className={`${card} h-full ${mobile ? "p-3.5" : "p-5"}`}>
      <div className="mb-2 flex items-baseline justify-between gap-2">
        <h2 className={`font-bold text-ink ${mobile ? "text-[15px]" : "text-[16px]"}`}>{t("Проверенные методы")}</h2>
        {mobile && <span className="text-[11px] text-ink-4">{t("ошибка")}</span>}
      </div>
      {view ? (
        <Methods methods={view.result.methods} chosen={view.result.method} holdout={view.result.holdoutPoints} limit={mobile ? 4 : undefined} compact={mobile} />
      ) : (
        <div className="h-40 animate-pulse rounded-xl bg-muted" />
      )}
    </section>
  );

  const bottomPast = (mobile: boolean) => (
    <section className={`${card} h-full ${mobile ? "p-3.5" : "p-5"}`}>
      <h2 className={`font-bold text-ink ${mobile ? "text-[15px]" : "text-[16px]"}`}>{t("Точность прошлых прогнозов")}</h2>
      <p className="mb-3 text-[11px] text-ink-4">{t("прогноз на месяц против факта")}</p>
      <PastAccuracy data={backtest} unit={unit} compact={mobile} loading={btLoading} />
    </section>
  );

  const bottomAssumptions = (mobile: boolean) => (
    <section className={`${card} h-full ${mobile ? "p-3.5" : "p-5"}`}>
      <h2 className={`font-bold text-ink ${mobile ? "text-[15px]" : "text-[16px]"}`}>{t("Допущения прогноза")}</h2>
      {!mobile && <p className="mb-3 text-[11px] text-ink-4">{t("что учтено и что нет")}</p>}
      <div className={mobile ? "mt-2.5" : ""}>
        <Assumptions
          days={view?.result.history ?? 0}
          from={view?.history[0]?.date ?? null}
          to={asOf}
          stepWord={stepWord(view?.result.history ?? 0)}
          seasonality={view?.result.seasonality ?? 0}
          calendar={calendarOf(view)}
          simulatorHref={sim}
          compact={mobile}
        />
      </div>
    </section>
  );

  const banners = (
    <>
      {p.whatIf !== 0 && (
        <div className="flex flex-wrap items-center gap-2 rounded-xl bg-accent/10 px-4 py-2.5 text-[13px] text-ink-2">
          <SlidersHorizontal aria-hidden className="size-4 text-accent-strong" />
          <span className="min-w-0 flex-1">
            {t("Сценарий «что если»")}: <b className="text-accent-strong">{p.whatIf > 0 ? "+" : "−"}{Math.abs(p.whatIf)}%</b> {t("к прогнозу — все три сценария и линия сдвинуты")}
          </span>
          <button type="button" onClick={() => set({ whatIf: 0 })} className="font-semibold text-accent-strong hover:underline">
            {t("Сбросить")}
          </button>
        </div>
      )}
      {shortHistory && (
        <div className="flex items-center gap-2 rounded-xl bg-warn/10 px-4 py-2.5 text-[13px] text-warn">
          <ChartNoAxesCombined aria-hidden className="size-4" />
          {t("Истории мало — прогноз приблизительный")}
        </div>
      )}
    </>
  );

  return (
    <>
      {/* ---------- компьютер: заголовок ---------- */}
      <div className="max-lg:hidden">
        <PageHeader
          variant="display"
          title={t("Прогнозы")}
          subtitle={t("Продажи, оплаты и расход сырья: прогноз с диапазоном, сценарии «что если», точность на истории и прошлых прогнозов")}
          breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("Прогнозы") }]}
          actions={
            <div className="flex flex-wrap items-center gap-2.5">
              {asOf && (
                <span className="inline-flex items-center gap-1.5 text-[12.5px] text-ink-3">
                  <span aria-hidden className="size-1.5 rounded-full bg-ok" />
                  {t("Данные на {d}", { d: ddmm(asOf, true) })}
                </span>
              )}
              <Link href={sim} className="inline-flex h-9 items-center gap-1.5 rounded-[10px] border border-line-strong bg-surface px-3 text-[13px] font-medium text-ink hover:bg-muted">
                <SlidersHorizontal aria-hidden className="size-4" />
                {t("В симулятор")}
              </Link>
              <button type="button" onClick={exportCsv} disabled={!view} className="inline-flex h-9 items-center gap-1.5 rounded-[10px] border border-line-strong bg-surface px-3 text-[13px] font-medium text-ink hover:bg-muted disabled:opacity-40">
                <Download aria-hidden className="size-4" />
                {t("Экспорт")}
              </button>
            </div>
          }
        />
      </div>

      <PageBody variant="display">
        {/* ---------- телефон и планшет: своя шапка и разделы модуля ---------- */}
        <MobileHeader asOf={asOf} permissions={permissions} />

        {/* ================= компьютер (от 1024) ================= */}
        <div className="flex flex-col gap-4 max-lg:hidden">
          <section className="flex flex-wrap items-end gap-x-5 gap-y-3 rounded-2xl border border-line bg-surface px-[18px] py-3.5">
            <div>
              <FieldLabel>{t("Показатель")}</FieldLabel>
              <Segments
                label={t("Показатель")}
                items={metricItems.map((m) => ({ key: m.metric, label: t(m.label), icon: METRIC_ICON[m.metric] }))}
                value={p.metric}
                onChange={(metric) => set({ metric, item: null })}
              />
            </div>
            <div>
              <FieldLabel>{t("Срез")}</FieldLabel>
              <Picker label={t("Срез")} icon={Layers} items={sliceItems} value={p.item ?? ""} onChange={(k) => set({ item: k || null })} />
            </div>
            <div>
              <FieldLabel>{t("Шаг")}</FieldLabel>
              <Segments label={t("Шаг")} items={stepItems} value={p.step} onChange={(step: Step) => set({ step })} size="sm" />
            </div>
            <div>
              <FieldLabel>{t("Горизонт")}</FieldLabel>
              <Segments label={t("Горизонт")} items={horizonItems} value={p.horizon} onChange={(horizon: Horizon) => set({ horizon })} size="sm" />
            </div>
            <div className="ml-auto">
              <FieldLabel>{t("Сценарий «что если»")}</FieldLabel>
              <WhatIf value={p.whatIf} onChange={(whatIf) => set({ whatIf })} />
            </div>
          </section>

          {banners}

          <div className="grid gap-3 xl:grid-cols-[minmax(0,1fr)_300px]">
            {scenariosBlock(false)}
            {view ? <AccuracyCard wape={view.result.wape} holdout={view.result.holdoutPoints} /> : loading ? <div className="h-[132px] animate-pulse rounded-2xl border border-line bg-surface" /> : null}
          </div>

          <section className={`${card} p-5`}>
            {chartHeader(false)}
            <div className="mt-4">{chartBody(false)}</div>
            {view && !failure && (
              <div className="mt-4 flex flex-wrap items-end justify-between gap-4 border-t border-line pt-4">
                <div className="flex flex-col gap-2">
                  {legend(false)}
                  <button type="button" onClick={() => setTable((x) => !x)} className="inline-flex items-center gap-1.5 self-start text-[12px] font-semibold text-accent-strong hover:underline">
                    <Table2 aria-hidden className="size-3.5" />
                    {table ? t("Скрыть таблицу") : t("Показать таблицей")}
                  </button>
                </div>
                <dl className="flex divide-x divide-line">
                  <Total label={t("Прогноз на {p}", { p: prevText })} value={realistic ? money(realistic.value) : "—"} tone="text-violet" note={whatIfNote} />
                  <Total label={t("Диапазон")} value={lu(`${compactNum(view.result.totalLower)} – ${compactNum(view.result.totalUpper)}`)} />
                  <Total label={t("Прошлые {p}", { p: prevText })} value={prev == null ? "—" : money(prev)} />
                </dl>
              </div>
            )}
            {dataTable}
          </section>

          <div className="grid gap-4 xl:grid-cols-3 lg:grid-cols-2">
            {bottomMethods(false)}
            {bottomPast(false)}
            <div className="lg:col-span-2 xl:col-span-1">{bottomAssumptions(false)}</div>
          </div>
        </div>

        {/* ================= телефон и планшет (до 1024) ================= */}
        <div className="flex flex-col gap-3 pb-[calc(150px+env(safe-area-inset-bottom))] lg:hidden">
          <Segments
            label={t("Показатель")}
            full
            items={metricItems.map((m) => ({ key: m.metric, label: t(m.short), icon: METRIC_ICON[m.metric], aria: t(m.label) }))}
            value={p.metric}
            onChange={(metric) => set({ metric, item: null })}
          />
          <div className="grid grid-cols-3 gap-1.5">
            <Picker compact label={t("Срез")} icon={Layers} items={sliceItems} value={p.item ?? ""} onChange={(k) => set({ item: k || null })} />
            <Picker compact label={t("Шаг")} icon={CalendarDays} items={STEPS.map((s) => ({ key: s.step, label: t(s.mobile) }))} value={p.step} onChange={(step) => set({ step: step as Step })} />
            <Picker compact label={t("Горизонт")} icon={CalendarRange} items={HORIZONS.map((h) => ({ key: String(h), label: t("{n} дн.", { n: h }) }))} value={String(p.horizon)} onChange={(h) => set({ horizon: Number(h) as Horizon })} />
          </div>
          {banners}
          {scenariosBlock(true)}

          <section className={`${card} flex flex-col gap-2.5 p-3.5`}>
            {chartHeader(true)}
            {chartBody(true)}
            {view && !failure && legend(true)}
            {dataTable}
          </section>

          <div className="grid grid-cols-2 gap-2">
            {view ? <AccuracyTile wape={view.result.wape} onOpen={() => setWapeSheet(true)} /> : <div className="h-[104px] animate-pulse rounded-[14px] bg-surface" />}
            <div className={`${card} flex flex-col gap-2 p-3`}>
              <span className="text-[11px] text-ink-3">{t("Что если, %")}</span>
              <WhatIf value={p.whatIf} onChange={(whatIf) => set({ whatIf })} compact />
            </div>
          </div>

          <div className="grid gap-3 md:grid-cols-2">
            {bottomMethods(true)}
            {bottomPast(true)}
          </div>
          {bottomAssumptions(true)}
        </div>
      </PageBody>

      <Sheet open={wapeSheet} onClose={() => setWapeSheet(false)} title={t("Ошибка прогноза (WAPE)")}>
        {view && (
          <div className="flex flex-col gap-4 pb-2">
            <AccuracyCard wape={view.result.wape} holdout={view.result.holdoutPoints} />
            <p className="text-[13px] leading-snug text-ink-2">
              {t("WAPE — средняя ошибка прогноза: сумма отклонений от факта, делённая на сумму факта, на последних {n} точках истории, которые метод не видел.", { n: view.result.holdoutPoints })}
            </p>
            <Methods methods={view.result.methods} chosen={view.result.method} holdout={view.result.holdoutPoints} compact />
          </div>
        )}
      </Sheet>
    </>
  );
}

/** Телефон и планшет: знак, «Прогнозы», свежесть данных, колокольчик; ниже — разделы модуля чипами, текущий прокручен в видимую зону. */
function MobileHeader({ asOf, permissions }: { asOf: string | null; permissions: string[] }) {
  const t = useT();
  const nav = useRef<HTMLElement>(null);
  const tabs = useMemo(
    () => analytikTabs.filter((tab) => !tab.permission || (Array.isArray(tab.permission) ? tab.permission : [tab.permission]).some((x) => permissions.includes(x))),
    [permissions],
  );
  // Текущий раздел — в видимую зону ленты чипов (только по горизонтали: scrollIntoView сдвигал бы и страницу).
  useEffect(() => {
    const el = nav.current;
    const chip = el?.querySelector<HTMLElement>('[aria-current="page"]');
    if (el && chip) el.scrollLeft = chip.offsetLeft - (el.clientWidth - chip.offsetWidth) / 2;
  }, []);
  return (
    <div className="-mx-4 -mt-3.5 mb-3 sm:-mx-6 md:-mt-[18px] lg:hidden">
      <header className="flex min-h-14 items-center gap-2.5 px-4 pt-[env(safe-area-inset-top)] sm:px-6">
        <span aria-hidden className="grid size-9 shrink-0 place-items-center rounded-[11px] bg-[linear-gradient(135deg,#5b97ff,#8b5cf6)] text-white">
          <BrainCircuit className="size-[18px]" />
        </span>
        <div className="min-w-0 flex-1">
          <h1 className="text-[17px] font-bold leading-tight text-ink">{t("Прогнозы")}</h1>
          <p className="flex items-center gap-1.5 text-[11px] text-ink-3">
            <span aria-hidden className="size-1.5 rounded-full bg-ok" />
            {t("Аналитик")}
            {asOf && ` · ${t("данные на {d}", { d: ddmm(asOf) })}`}
          </p>
        </div>
        <NotificationBell permissions={permissions} />
      </header>
      <nav ref={nav} aria-label={t("Разделы модуля")} className="no-scrollbar flex gap-1.5 overflow-x-auto px-4 pb-1 pt-2 sm:px-6">
        {tabs.map((tab) => {
          const on = tab.key === "forecasts";
          return (
            <Link
              key={tab.key}
              href={tab.href}
              aria-current={on ? "page" : undefined}
              className={`inline-flex h-[34px] shrink-0 items-center gap-1.5 whitespace-nowrap rounded-[10px] px-[11px] text-[13px] ${on ? "bg-accent font-semibold text-white" : "border border-line bg-surface text-ink-2"}`}
            >
              <tab.icon className={`size-3.5 shrink-0 ${tab.key === "chat" && !on ? "text-violet" : ""}`} aria-hidden strokeWidth={1.9} />
              {t(tab.label)}
            </Link>
          );
        })}
      </nav>
    </div>
  );
}

function LegendLine({ color, label }: { color: string; label: string }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <span aria-hidden className={`h-[3px] w-3.5 rounded-full ${color}`} />
      {label}
    </span>
  );
}

function Total({ label, value, tone = "text-ink", note }: { label: string; value: string; tone?: string; note?: string | null }) {
  return (
    <div className="px-5 first:pl-0 last:pr-0">
      <dt className="text-[10.5px] font-semibold text-ink-4">{label}</dt>
      <dd className={`mt-1 text-[14px] font-bold tabular-nums ${tone}`}>{value}</dd>
      {note && <dd className="text-[10px] text-ink-4">{note}</dd>}
    </div>
  );
}

function MenuButton({ icon: Icon, label, onClick }: { icon: LucideIcon; label: string; onClick: () => void }) {
  return (
    <button type="button" role="menuitem" onClick={onClick} className="flex w-full items-center gap-2 rounded-lg px-2.5 py-2.5 text-left text-[13px] text-ink hover:bg-muted">
      <Icon aria-hidden className="size-4 text-ink-3" />
      {label}
    </button>
  );
}

function MenuToggle({ label, on, onClick }: { label: string; on: boolean; onClick: () => void }) {
  return (
    <button type="button" role="menuitemcheckbox" aria-checked={on} onClick={onClick} className="flex w-full items-center gap-2 rounded-lg px-2.5 py-2.5 text-left text-[13px] text-ink hover:bg-muted">
      <span aria-hidden className={`relative h-5 w-9 shrink-0 rounded-full transition-colors ${on ? "bg-accent" : "bg-line-strong"}`}>
        <span className={`absolute top-0.5 size-4 rounded-full bg-white transition-transform ${on ? "left-[18px]" : "left-0.5"}`} />
      </span>
      {label}
    </button>
  );
}

