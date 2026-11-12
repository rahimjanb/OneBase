"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import {
  ArrowDown,
  ArrowUp,
  ChevronDown,
  CircleCheck,
  Database,
  Download,
  FileSpreadsheet,
  Filter,
  Info,
  Loader2,
  Plus,
  Search,
  Sparkles,
  TriangleAlert,
  X,
} from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { date, dateTime, num } from "@/lib/analytik";
import {
  cellText,
  chartModel,
  compactValue,
  drill,
  drillNext,
  encodeQuery,
  factTitles,
  grainTitles,
  groupRows,
  quickPeriods,
  toCsv,
  type AnalyticsFilter,
  type AnalyticsQuery,
  type AnalyticsResult,
  type CapabilityView,
} from "@/lib/explore";
import { Card, LineChart } from "../ui";

const palette = ["var(--color-accent)", "var(--color-violet)", "var(--color-ok)", "var(--color-warn)", "var(--color-pink)", "var(--color-cyan)", "var(--color-ink-4)"];
/** Строк за один запрос (график и поиск — по ним) и строк таблицы на экране за раз. */
const PageSize = 5000;
const ShowStep = 300;

/** Готовые срезы — то, о чём спрашивают чаще всего; любой можно поменять. */
const presets: { label: string; query: (from: string, to: string) => AnalyticsQuery }[] = [
  { label: "Выручка по группам по дням", query: (from, to) => ({ metrics: ["revenue", "kg"], dimensions: ["date", "product_group"], grain: "day", dateFrom: from, dateTo: to }) },
  { label: "Регион × товарная группа", query: (from, to) => ({ metrics: ["revenue", "kg", "revenue_share"], dimensions: ["region", "product_group"], dateFrom: from, dateTo: to }) },
  { label: "План и факт по регионам", query: (from, to) => ({ metrics: ["kg", "plan_kg", "plan_execution", "forecast_execution"], dimensions: ["region"], grain: "month", dateFrom: from, dateTo: to }) },
  { label: "Группы к прошлому периоду", query: (from, to) => ({ metrics: ["revenue"], dimensions: ["product_group"], dateFrom: from, dateTo: to, compare: "previous", sort: [{ by: "revenue__pct", dir: "asc" }] }) },
  { label: "Топ-10 товаров", query: (from, to) => ({ metrics: ["revenue", "kg", "outlets"], dimensions: ["product"], dateFrom: from, dateTo: to, sort: [{ by: "revenue", dir: "desc" }], limit: 10 }) },
  { label: "Остатки и дни запаса", query: () => ({ metrics: ["stock_units", "cover_days", "need_units"], dimensions: ["product"], sort: [{ by: "cover_days", dir: "asc" }] }) },
];

/**
 * «Аналитик → Срезы»: конструктор среза (показатели, измерения, шаг, период, сравнение, фильтры) и результат — проверка полноты, источники
 * и время загрузки, итоги, график, интерактивная таблица (сортировка на сервере, поиск, группировка с подытогами, переход вглубь щелчком по
 * значению), CSV и Excel. Тот же движок, что у AI-аналитика: запрос из ответа AI открывается здесь по ссылке.
 */
export function ExploreView({ capabilities, initial }: { capabilities: CapabilityView; initial: AnalyticsQuery | null }) {
  const t = useT();
  const today = useMemo(() => new Date(), []);
  const periods = quickPeriods(today);
  const [query, setQuery] = useState<AnalyticsQuery>(() => initial ?? presets[0].query(periods[2].from, periods[2].to));
  const [result, setResult] = useState<AnalyticsResult | null>(null);
  const [rows, setRows] = useState<AnalyticsResult["rows"]>([]);
  const [keys, setKeys] = useState<AnalyticsResult["keys"]>([]);
  const [loading, setLoading] = useState(false);
  const [more, setMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [grouped, setGrouped] = useState(false);
  const [chartMetric, setChartMetric] = useState<string | null>(null);
  const [exporting, setExporting] = useState(false);
  const [showSources, setShowSources] = useState(false);
  const [showData, setShowData] = useState(false);
  const [shown, setShown] = useState(ShowStep);
  // Подписи значений по ключам (id → название) из загруженных строк: фильтр после перехода вглубь показывает «Самарканд», а не id.
  const labels = useRef(new Map<string, string>());

  const allowedFacts = new Set(capabilities.facts.filter((f) => f.allowed).map((f) => f.code));
  const metricsByFact = capabilities.metrics.filter((m) => allowedFacts.has(m.fact));
  const selectedFacts = new Set(capabilities.metrics.filter((m) => query.metrics.includes(m.code)).flatMap((m) => (m.requires ?? []).length ? capabilities.metrics.filter((x) => m.requires!.includes(x.code)).map((x) => x.fact) : [m.fact]));
  const dims = query.dimensions ?? [];
  const availableDims = capabilities.dimensions.filter((d) => [...selectedFacts].every((f) => d.facts.includes(f)));
  const dimTitle = (code: string) => capabilities.dimensions.find((d) => d.code === code)?.title ?? code;

  // Расчёт: через 500 мс после изменения, прежний запрос отменяется; адрес страницы — ссылка на этот срез.
  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      setLoading(true);
      setError(null);
      bff<AnalyticsResult>("analytik/explore/query", { method: "POST", body: JSON.stringify({ ...query, limit: query.limit ?? PageSize, offset: 0 }), signal: controller.signal })
        .then((r) => {
          remember(r.columns, r.rows, r.keys);
          setResult(r);
          setRows(r.rows);
          setKeys(r.keys);
          setShown(ShowStep);
        })
        .catch((e: Error) => !controller.signal.aborted && setError(e.message))
        .finally(() => !controller.signal.aborted && setLoading(false));
      // Адрес — ссылка на этот срез; без перехода Next (серверная часть страницы и реестр не перезапрашиваются).
      window.history.replaceState(window.history.state, "", `/analytik/explore?q=${encodeQuery(query)}`);
    }, 500);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query]);

  const loadMore = async () => {
    if (!result) return;
    setMore(true);
    try {
      const r = await bff<AnalyticsResult>("analytik/explore/query", { method: "POST", body: JSON.stringify({ ...query, limit: PageSize, offset: rows.length }) });
      remember(r.columns, r.rows, r.keys);
      setRows((x) => [...x, ...r.rows]);
      setKeys((x) => [...x, ...r.keys]);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setMore(false);
    }
  };

  function remember(columns: AnalyticsResult["columns"], data: AnalyticsResult["rows"], ids: AnalyticsResult["keys"]) {
    const dimCols = columns.map((c, i) => (c.kind === "dimension" ? i : -1)).filter((i) => i >= 0);
    data.forEach((row, r) => dimCols.forEach((c, k) => ids[r]?.[k] != null && labels.current.set(String(ids[r][k]), String(row[c]))));
  }

  const set = (patch: Partial<AnalyticsQuery>) => setQuery((q) => ({ ...q, ...patch, offset: 0 }));
  const toggleMetric = (code: string) =>
    set({ metrics: query.metrics.includes(code) ? query.metrics.filter((m) => m !== code) : [...query.metrics, code].slice(-8) });
  const toggleDim = (code: string) => set({ dimensions: dims.includes(code) ? dims.filter((d) => d !== code) : [...dims, code].slice(0, 5) });
  const sortBy = (code: string) => {
    const current = query.sort?.[0];
    set({ sort: [{ by: code, dir: current?.by === code && current.dir === "desc" ? "asc" : "desc" }] });
  };

  const exportXlsx = async () => {
    setExporting(true);
    try {
      const response = await fetch("/bff/api/analytik/explore/export", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(query) });
      if (!response.ok) throw new Error(t("Не удалось выгрузить Excel"));
      const blob = await response.blob();
      download(blob, `srez-${query.dateFrom ?? ""}-${query.dateTo ?? ""}.xlsx`);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setExporting(false);
    }
  };
  const exportCsv = () => result && download(new Blob([toCsv(result.columns, rows)], { type: "text/csv;charset=utf-8" }), `srez-${result.from}-${result.to}.csv`);

  const view = result ? { ...result, rows, keys } : null;
  const visible = view
    ? rows.map((_, i) => i).filter((i) => !search.trim() || rows[i].some((v, c) => view.columns[c].kind === "dimension" && String(v).toLowerCase().includes(search.trim().toLowerCase())))
    : [];
  const chart = view ? chartModel({ ...view, rows: visible.map((i) => rows[i]), keys: visible.map((i) => keys[i]) }, chartMetric ?? undefined) : null;
  const failed = result?.checks.filter((c) => !c.ok) ?? [];

  return (
    <div className="space-y-4">
      {/* Готовые срезы */}
      <div className="flex flex-wrap gap-2">
        {presets.map((p) => (
          <button
            key={p.label}
            type="button"
            onClick={() => setQuery(p.query(periods[3].from, periods[3].to))}
            className="inline-flex h-8 items-center gap-1.5 rounded-full border border-line bg-surface px-3 text-[12.5px] text-ink-2 hover:border-accent hover:text-ink"
          >
            <Sparkles className="size-3.5 text-accent-strong" aria-hidden />
            {t(p.label)}
          </button>
        ))}
      </div>

      {/* Конструктор */}
      <Card title="Срез" subtitle="Показатели × измерения × период — движок считает по правилам «Продаж», любые совместимые сочетания">
        <div className="space-y-4">
          <Block title="Показатели">
            <div className="space-y-2">
              {[...new Set(metricsByFact.map((m) => m.fact))].map((fact) => (
                <div key={fact} className="flex flex-wrap items-center gap-1.5">
                  <span className="w-16 shrink-0 text-[11px] font-medium uppercase tracking-wide text-ink-4">{t(factTitles[fact] ?? fact)}</span>
                  {metricsByFact.filter((m) => m.fact === fact).map((m) => (
                    <Chip key={m.code} on={query.metrics.includes(m.code)} onClick={() => toggleMetric(m.code)} title={`${t(m.formula)}${m.note ? ` · ${t(m.note)}` : ""}`}>
                      {t(m.title)}
                    </Chip>
                  ))}
                </div>
              ))}
            </div>
          </Block>
          <Block title="Измерения (по порядку группировки, до 5)">
            <div className="flex flex-wrap gap-1.5">
              {dims.map((d, i) => (
                <Chip key={d} on onClick={() => toggleDim(d)}>
                  <span className="mr-1 rounded bg-white/20 px-1 text-[10px]">{i + 1}</span>
                  {t(dimTitle(d))}
                  <X className="ml-1 size-3" aria-hidden />
                </Chip>
              ))}
              {availableDims.filter((d) => !dims.includes(d.code)).map((d) => (
                <Chip key={d.code} on={false} onClick={() => toggleDim(d.code)} title={t(d.note)}>
                  <Plus className="mr-0.5 size-3" aria-hidden />
                  {t(d.title)}
                </Chip>
              ))}
            </div>
          </Block>
          <div className="flex flex-wrap items-end gap-x-6 gap-y-3">
            {dims.includes("date") && (
              <Block title="Шаг">
                <Segments value={query.grain ?? "day"} onChange={(g) => set({ grain: g })} items={capabilities.grains.map((g) => ({ key: g, label: grainTitles[g] ?? g }))} />
              </Block>
            )}
            {!selectedFacts.has("stock") && (
              <Block title="Период">
                <div className="flex flex-wrap items-center gap-1.5">
                  {periods.map((p) => (
                    <Chip key={p.key} on={query.dateFrom === p.from && query.dateTo === p.to} onClick={() => set({ dateFrom: p.from, dateTo: p.to })}>
                      {t(p.label)}
                    </Chip>
                  ))}
                  <input type="date" value={query.dateFrom ?? ""} onChange={(e) => e.target.value && set({ dateFrom: e.target.value })} aria-label={t("С")} className="h-8 rounded-lg border border-line bg-surface px-2 text-xs text-ink" />
                  <span className="text-ink-4">—</span>
                  <input type="date" value={query.dateTo ?? ""} onChange={(e) => e.target.value && set({ dateTo: e.target.value })} aria-label={t("По")} className="h-8 rounded-lg border border-line bg-surface px-2 text-xs text-ink" />
                </div>
              </Block>
            )}
            {!selectedFacts.has("stock") && (
              <Block title="Сравнение">
                <Segments
                  value={query.compare ?? ""}
                  onChange={(c) => set({ compare: c || null })}
                  items={[{ key: "", label: "Нет" }, { key: "previous", label: "Прошлый период" }, { key: "year_ago", label: "Год назад" }]}
                />
              </Block>
            )}
            {selectedFacts.has("sales") && (
              <Block title="Продажи">
                <Segments
                  value={query.segment ?? "secondary"}
                  onChange={(s) => set({ segment: s })}
                  items={[{ key: "secondary", label: "Вторичка" }, { key: "factory", label: "Завод" }, { key: "all", label: "Всё" }]}
                />
              </Block>
            )}
            {dims.includes("date") && !selectedFacts.has("plan") && (
              <label className="flex h-8 items-center gap-2 text-[12.5px] text-ink-2">
                <input type="checkbox" checked={query.fillZeros === true} onChange={(e) => set({ fillZeros: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
                {t("Дни без продаж — нулём")}
              </label>
            )}
          </div>
          <Filters query={query} dims={availableDims.map((d) => d.code)} dimTitle={dimTitle} labelOf={(v) => labels.current.get(v) ?? v} onChange={(filters) => set({ filters })} />
        </div>
      </Card>

      {error && (
        <p className="flex items-start gap-2 rounded-xl border border-bad/30 bg-bad-soft px-4 py-3 text-[13px] text-bad" role="alert">
          <TriangleAlert className="mt-0.5 size-4 shrink-0" aria-hidden />
          {t(error)}
        </p>
      )}

      {!result && loading && (
        <div className="flex items-center gap-2 rounded-2xl border border-line bg-surface p-5 text-[13px] text-ink-3">
          <Loader2 className="size-5 animate-spin" aria-hidden />
          {t("Считаю срез…")}
        </div>
      )}

      {result && view && (
        <div className={`space-y-4 transition-opacity ${loading ? "opacity-60" : ""}`}>
          {/* Полнота и источники */}
          <section className={`rounded-2xl border px-4 py-3 ${failed.length ? "border-warn/30 bg-warn-soft" : "border-ok/30 bg-ok-soft"}`}>
            <div className="flex flex-wrap items-center gap-2">
              {failed.length ? <TriangleAlert className="size-4 text-warn" aria-hidden /> : <CircleCheck className="size-4 text-ok" aria-hidden />}
              <span className={`text-[13px] font-semibold ${failed.length ? "text-warn" : "text-ok"}`}>
                {failed.length ? t("Данные неполные — учтите перед выводами") : t("Данные полные")}
              </span>
              <span className="text-[12px] text-ink-3">
                {date(result.from)} — {date(result.to)}
                {result.compareFrom && ` · ${t("сравнение")} ${date(result.compareFrom)} — ${date(result.compareTo)}`}
                {` · ${result.totalRows} ${t("строк")} · ${num(result.elapsedMs / 1000, 1)} ${t("с")}`}
                {result.scope && ` · ${t("область")}: ${result.scope}`}
              </span>
              {loading && <Loader2 className="size-4 animate-spin text-accent-strong" aria-label={t("Пересчёт…")} />}
              <button type="button" onClick={() => setShowSources(!showSources)} aria-expanded={showSources} className="ml-auto inline-flex items-center gap-1 text-[12px] font-medium text-accent-strong">
                {t("Проверка и источники")}
                <ChevronDown className={`size-3.5 transition ${showSources ? "rotate-180" : ""}`} aria-hidden />
              </button>
            </div>
            {failed.length > 0 && (
              <ul className="mt-2 space-y-1 text-[12.5px] text-ink-2">
                {failed.map((c) => (
                  <li key={c.code}>• {t(c.text)}</li>
                ))}
              </ul>
            )}
            {showSources && (
              <div className="mt-3 grid gap-3 border-t border-line pt-3 text-[12px] text-ink-2 lg:grid-cols-2">
                <ul className="space-y-1">
                  {result.checks.map((c) => (
                    <li key={c.code} className="flex gap-1.5">
                      {c.ok ? <CircleCheck className="mt-px size-3.5 shrink-0 text-ok" aria-hidden /> : <TriangleAlert className="mt-px size-3.5 shrink-0 text-warn" aria-hidden />}
                      {t(c.text)}
                    </li>
                  ))}
                </ul>
                <div className="space-y-2">
                  {result.sources.map((s) => (
                    <div key={s.title}>
                      <div className="font-medium text-ink">{t(s.title)}</div>
                      <div className="text-ink-3">
                        {s.syncedAt ? `${t("загружено")} ${dateTime(s.syncedAt)}` : ""}
                        {s.dataThrough ? ` · ${t("данные по")} ${date(s.dataThrough)}` : ""}
                      </div>
                      <div className="text-[11px] text-ink-4">{t(s.rules)}</div>
                    </div>
                  ))}
                  {result.notes.map((n) => (
                    <p key={n} className="flex gap-1.5 text-ink-3">
                      <Info className="mt-px size-3.5 shrink-0" aria-hidden />
                      {t(n)}
                    </p>
                  ))}
                </div>
              </div>
            )}
          </section>

          {/* Итоги */}
          <div className="grid grid-cols-2 gap-2.5 md:grid-cols-3 xl:grid-cols-6">
            {result.columns.filter((c) => c.kind === "metric").map((c) => {
              const total = result.totals[c.code];
              const prev = result.compareTotals?.[c.code];
              const delta = total != null && prev != null && prev !== 0 ? ((total - prev) / Math.abs(prev)) * 100 : null;
              return (
                <button
                  key={c.code}
                  type="button"
                  onClick={() => setChartMetric(c.code)}
                  className={`min-w-0 rounded-xl border bg-surface p-3 text-left ${(chartMetric ?? chart?.metric) === c.code ? "border-accent" : "border-line"}`}
                >
                  <div className="truncate text-[11.5px] text-ink-3">{t(c.title)}</div>
                  <div className="mt-0.5 truncate text-[17px] font-semibold tabular-nums text-ink">{compactValue(total ?? null, c.format)}</div>
                  {delta != null && (
                    <div className={`text-[11px] font-medium tabular-nums ${delta >= 0 ? "text-ok" : "text-bad"}`}>
                      {delta > 0 ? "+" : "−"}
                      {num(Math.abs(delta), 1)}% · {t("было")} {compactValue(prev ?? null, c.format)}
                    </div>
                  )}
                </button>
              );
            })}
          </div>

          {/* График */}
          {chart && (
            <Card title={chart.kind === "line" ? "Динамика" : "Сравнение"} subtitle={result.columns.find((c) => c.code === chart.metric)?.title}>
              {chart.kind === "line" ? (
                <>
                  <LineChart
                    labels={chart.labels.map((l) => (/^\d{4}-\d{2}-\d{2}$/.test(l) ? `${l.slice(8, 10)}.${l.slice(5, 7)}` : l))}
                    series={chart.series.map((s, i) => ({ name: s.name, values: s.values, color: palette[i % palette.length] }))}
                    format={(v) => compactValue(v, chart.format)}
                    height={220}
                  />
                </>
              ) : (
                <ul className="space-y-1.5">
                  {chart.bars.map((b) => {
                    const max = Math.max(...chart.bars.map((x) => Math.abs(x.value)), 1);
                    const dim = dims[0];
                    const next = dim ? drill(query, dim, b.key[0] ?? null) : null;
                    return (
                      <li key={b.label}>
                        <button type="button" disabled={!next} onClick={() => next && setQuery(next)} className="group flex w-full items-center gap-2 text-left text-[12px] disabled:cursor-default">
                          <span className="w-44 shrink-0 truncate text-ink-2 group-enabled:group-hover:text-accent-strong" title={b.label}>
                            {b.label}
                          </span>
                          <span className="h-2.5 min-w-0 flex-1 overflow-hidden rounded-full bg-muted">
                            <span className={`block h-full rounded-full ${b.value < 0 ? "bg-bad" : "bg-accent"}`} style={{ width: `${(Math.abs(b.value) / max) * 100}%` }} />
                          </span>
                          <span className="w-24 shrink-0 text-right tabular-nums text-ink">{compactValue(b.value, chart.format)}</span>
                        </button>
                      </li>
                    );
                  })}
                </ul>
              )}
            </Card>
          )}

          {/* Таблица */}
          <Card
            title="Таблица"
            subtitle={dims.length ? "Щелчок по значению — переход вглубь (регион → агенты, группа → товары, месяц → дни); по заголовку — сортировка" : undefined}
            action={
              <div className="flex flex-wrap items-center gap-2">
                <label className="flex h-8 items-center gap-1.5 rounded-lg border border-line bg-surface px-2 text-[12px]">
                  <Search className="size-3.5 text-ink-4" aria-hidden />
                  <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Найти в строках")} aria-label={t("Найти в строках")} className="w-32 bg-transparent text-ink outline-none placeholder:text-ink-4" />
                </label>
                {dims.length >= 2 && (
                  <button type="button" onClick={() => setGrouped(!grouped)} aria-pressed={grouped} className={`inline-flex h-8 items-center gap-1 rounded-lg border px-2.5 text-[12px] ${grouped ? "border-accent bg-accent-soft text-accent-strong" : "border-line text-ink-2"}`}>
                    <Filter className="size-3.5" aria-hidden />
                    {t("По группам")}
                  </button>
                )}
                <button type="button" onClick={exportCsv} className="inline-flex h-8 items-center gap-1 rounded-lg border border-line px-2.5 text-[12px] text-ink-2 hover:border-line-strong">
                  <Download className="size-3.5" aria-hidden />
                  CSV
                </button>
                <button type="button" onClick={exportXlsx} disabled={exporting} className="inline-flex h-8 items-center gap-1 rounded-lg border border-line px-2.5 text-[12px] text-ink-2 hover:border-line-strong disabled:opacity-50">
                  {exporting ? <Loader2 className="size-3.5 animate-spin" aria-hidden /> : <FileSpreadsheet className="size-3.5 text-ok" aria-hidden />}
                  Excel
                </button>
              </div>
            }
          >
            <ResultTable view={view} visible={visible.slice(0, shown)} grouped={grouped && dims.length >= 2} query={query} onSort={sortBy} onDrill={(q) => setQuery(q)} />
            <div className="mt-3 flex flex-wrap items-center justify-between gap-2 text-[12px] text-ink-3">
              <span>
                {t("Показано")} {Math.min(shown, visible.length)} {t("из")} {search.trim() ? visible.length : result.totalRows}
              </span>
              {(shown < visible.length || (rows.length < result.totalRows && !query.limit && !search.trim())) && (
                <button
                  type="button"
                  onClick={() => (shown < visible.length ? setShown((x) => x + ShowStep) : loadMore().then(() => setShown((x) => x + ShowStep)))}
                  disabled={more}
                  className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-line px-3 text-[12px] text-ink-2 hover:border-line-strong disabled:opacity-50"
                >
                  {more && <Loader2 className="size-3.5 animate-spin" aria-hidden />}
                  {t("Показать ещё")}
                </button>
              )}
            </div>
          </Card>
        </div>
      )}

      {/* Какие данные есть */}
      <Card
        title="Какие данные есть"
        subtitle="Реестр возможностей: показатели, измерения, даты, загрузки; что отдают другие разделы и чего нет в источниках"
        action={
          <button type="button" onClick={() => setShowData(!showData)} aria-expanded={showData} className="inline-flex items-center gap-1 text-[12px] font-medium text-accent-strong">
            {showData ? t("Скрыть") : t("Показать")}
            <ChevronDown className={`size-3.5 transition ${showData ? "rotate-180" : ""}`} aria-hidden />
          </button>
        }
      >
        <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
          {capabilities.facts.map((f) => (
            <div key={f.code} className={`rounded-xl border px-3 py-2.5 ${f.allowed ? "border-line" : "border-line opacity-60"}`}>
              <div className="flex items-center gap-1.5 text-[13px] font-semibold text-ink">
                <Database className="size-3.5 text-ink-3" aria-hidden />
                {t(f.title)}
                <span className={`ml-auto rounded px-1.5 text-[10.5px] font-medium ${f.status === "ok" ? "bg-ok-soft text-ok" : f.status === "missing" ? "bg-muted text-ink-4" : "bg-warn-soft text-warn"}`}>
                  {t(f.status === "ok" ? "свежие" : f.status === "stale" ? "отстают" : f.status === "error" ? "ошибка загрузки" : "нет данных")}
                </span>
              </div>
              <div className="mt-1 text-[11.5px] text-ink-3">
                {t(f.source)} · {t(f.detail)}
              </div>
              <div className="mt-1 text-[11.5px] text-ink-2">
                {f.first && f.last ? `${date(f.first)} — ${date(f.last)}` : t("нет данных")}
                {f.syncedAt && ` · ${t("загружено")} ${dateTime(f.syncedAt)}`}
              </div>
              {!f.allowed && <div className="mt-1 text-[11px] text-ink-4">{t("Нет доступа по правам")}</div>}
            </div>
          ))}
        </div>
        {showData && (
          <div className="mt-4 grid gap-4 text-[12.5px] lg:grid-cols-2">
            <div>
              <h3 className="mb-1.5 text-[11px] font-semibold uppercase tracking-wide text-ink-4">{t("Другие разделы")}</h3>
              <ul className="space-y-1.5">
                {capabilities.tools.map((x) => (
                  <li key={x.area}>
                    <span className="font-medium text-ink">{t(x.area)}</span> — <span className="text-ink-2">{t(x.title)}</span>
                    <div className="text-[11px] text-ink-4">{t(x.detail)}</div>
                  </li>
                ))}
              </ul>
            </div>
            <div>
              <h3 className="mb-1.5 text-[11px] font-semibold uppercase tracking-wide text-ink-4">{t("Чего нет в источниках")}</h3>
              <ul className="list-disc space-y-1 pl-4 text-ink-2">
                {capabilities.missing.map((m) => (
                  <li key={m}>{t(m)}</li>
                ))}
              </ul>
              {capabilities.syncRuns.length > 0 && (
                <>
                  <h3 className="mb-1.5 mt-4 text-[11px] font-semibold uppercase tracking-wide text-ink-4">{t("Последние загрузки Linko")}</h3>
                  <ul className="space-y-1 text-ink-2">
                    {capabilities.syncRuns.map((r) => (
                      <li key={r.startedAt} className="flex gap-2">
                        <span className={`rounded px-1 text-[10.5px] ${r.status === "ok" ? "bg-ok-soft text-ok" : "bg-warn-soft text-warn"}`}>{r.status}</span>
                        {dateTime(r.startedAt)} · {r.mode} · {num(r.rows, 0)} {t("строк")}
                        {r.error && <span className="truncate text-bad" title={r.error}>· {r.error}</span>}
                      </li>
                    ))}
                  </ul>
                </>
              )}
            </div>
          </div>
        )}
      </Card>
    </div>
  );
}

function download(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function Block({ title, children }: { title: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <div className="min-w-0">
      <div className="mb-1.5 text-[11px] font-semibold uppercase tracking-wide text-ink-4">{t(title)}</div>
      {children}
    </div>
  );
}

function Chip({ on, onClick, title, children }: { on: boolean; onClick: () => void; title?: string; children: React.ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={on}
      title={title}
      className={`inline-flex h-8 items-center rounded-lg border px-2.5 text-[12.5px] transition ${on ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink-2 hover:border-line-strong hover:text-ink"}`}
    >
      {children}
    </button>
  );
}

function Segments({ value, onChange, items }: { value: string; onChange: (v: string) => void; items: { key: string; label: string }[] }) {
  const t = useT();
  return (
    <div className="inline-flex flex-wrap gap-1 rounded-lg border border-line bg-surface p-0.5" role="radiogroup">
      {items.map((x) => (
        <button
          key={x.key}
          type="button"
          role="radio"
          aria-checked={value === x.key}
          onClick={() => onChange(x.key)}
          className={`h-7 rounded-md px-2.5 text-[12px] ${value === x.key ? "bg-accent-soft font-semibold text-accent-strong" : "text-ink-3 hover:text-ink"}`}
        >
          {t(x.label)}
        </button>
      ))}
    </div>
  );
}

/** Фильтры: «измерение = значения» (через запятую — названия или id), «не», «содержит». */
function Filters({ query, dims, dimTitle, labelOf, onChange }: { query: AnalyticsQuery; dims: string[]; dimTitle: (c: string) => string; labelOf: (v: string) => string; onChange: (f: AnalyticsFilter[]) => void }) {
  const t = useT();
  const filters = query.filters ?? [];
  const options = dims.filter((d) => d !== "date");
  const [dim, setDim] = useState(options[0] ?? "region");
  const [op, setOp] = useState<"in" | "not_in" | "contains">("in");
  const [text, setText] = useState("");
  const add = () => {
    const values = text.split(",").map((v) => v.trim()).filter(Boolean);
    if (!values.length) return;
    onChange([...filters.filter((f) => f.dimension !== dim), { dimension: dim, op, values }]);
    setText("");
  };
  return (
    <Block title="Фильтры">
      <div className="flex flex-wrap items-center gap-1.5">
        {filters.map((f) => (
          <span key={f.dimension} className="inline-flex h-8 items-center gap-1 rounded-lg border border-accent/40 bg-accent-soft px-2.5 text-[12.5px] text-ink">
            {t(dimTitle(f.dimension))} {f.op === "not_in" ? "≠" : f.op === "contains" ? "∋" : "="} {f.values.map((v) => labelOf(v)).map((v) => (/^[0-9a-f-]{36}$/.test(v) ? "…" + v.slice(-4) : v)).join(", ")}
            <button type="button" onClick={() => onChange(filters.filter((x) => x !== f))} aria-label={t("Убрать фильтр")} className="ml-0.5 text-ink-3 hover:text-bad">
              <X className="size-3.5" aria-hidden />
            </button>
          </span>
        ))}
        <select value={dim} onChange={(e) => setDim(e.target.value)} aria-label={t("Измерение фильтра")} className="h-8 rounded-lg border border-line bg-surface px-2 text-[12.5px] text-ink">
          {options.map((d) => (
            <option key={d} value={d}>
              {t(dimTitle(d))}
            </option>
          ))}
        </select>
        <select value={op} onChange={(e) => setOp(e.target.value as typeof op)} aria-label={t("Условие")} className="h-8 rounded-lg border border-line bg-surface px-2 text-[12.5px] text-ink">
          <option value="in">=</option>
          <option value="not_in">≠</option>
          <option value="contains">{t("содержит")}</option>
        </select>
        <input value={text} onChange={(e) => setText(e.target.value)} onKeyDown={(e) => e.key === "Enter" && add()} placeholder={t("Самарканд, Кекс…")} aria-label={t("Значения фильтра")} className="h-8 w-48 rounded-lg border border-line bg-surface px-2 text-[12.5px] text-ink placeholder:text-ink-4" />
        <button type="button" onClick={add} disabled={!text.trim()} className="inline-flex h-8 items-center gap-1 rounded-lg border border-line px-2.5 text-[12.5px] text-ink-2 hover:border-accent disabled:opacity-40">
          <Plus className="size-3.5" aria-hidden />
          {t("Добавить")}
        </button>
      </div>
    </Block>
  );
}

function ResultTable({
  view,
  visible,
  grouped,
  query,
  onSort,
  onDrill,
}: {
  view: AnalyticsResult;
  visible: number[];
  grouped: boolean;
  query: AnalyticsQuery;
  onSort: (code: string) => void;
  onDrill: (q: AnalyticsQuery) => void;
}) {
  const t = useT();
  const sort = query.sort?.[0];
  const dimCols = view.columns.map((c, i) => (c.kind === "dimension" ? i : -1)).filter((i) => i >= 0);
  const numeric = (i: number) => view.columns[i].kind !== "dimension";
  const groups = grouped ? groupRows({ ...view, rows: visible.map((i) => view.rows[i]), keys: visible.map((i) => view.keys[i]) }) : null;
  const tone = (c: number, v: string | number | null) =>
    typeof v === "number" && (view.columns[c].kind === "delta" || view.columns[c].kind === "delta_pct") ? (v > 0 ? "text-ok" : v < 0 ? "text-bad" : "") : "";

  const row = (i: number, indent = false) => (
    <tr key={i} className="border-b border-line/60 hover:bg-muted/40">
      {view.rows[i].map((v, c) => {
        if (!numeric(c)) {
          const dim = view.columns[c].code;
          const next = dim === "date" || drillNext[dim] ? drill(query, dim, view.keys[i]?.[dimCols.indexOf(c)] ?? null) : null;
          return (
            <td key={c} className={`px-2 py-1.5 text-[12.5px] text-ink-2 ${indent && c === dimCols[0] ? "pl-6" : ""}`}>
              {next ? (
                <button type="button" onClick={() => onDrill(next)} className="text-left hover:text-accent-strong hover:underline" title={t("Подробнее")}>
                  {cellText(v, view.columns[c].format)}
                </button>
              ) : (
                cellText(v, view.columns[c].format)
              )}
            </td>
          );
        }

        return (
          <td key={c} className={`px-2 py-1.5 text-right text-[12.5px] tabular-nums text-ink ${tone(c, v)}`}>
            {cellText(v, view.columns[c].format)}
          </td>
        );
      })}
    </tr>
  );

  return (
    <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
      <table className="w-full min-w-[560px]">
        <thead>
          <tr className="border-b border-line">
            {view.columns.map((c) => (
              <th key={c.code} className={`px-2 py-2 text-[11px] font-medium uppercase tracking-wide text-ink-4 ${c.kind === "dimension" ? "text-left" : "text-right"}`}>
                <button type="button" onClick={() => onSort(c.code)} className="inline-flex items-center gap-0.5 hover:text-ink" title={t("Сортировать")}>
                  {t(c.title)}
                  {sort?.by === c.code && (sort.dir === "asc" ? <ArrowUp className="size-3" aria-hidden /> : <ArrowDown className="size-3" aria-hidden />)}
                </button>
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {groups
            ? groups.map((g) => [
                <tr key={`g-${g.label}`} className="bg-muted/50">
                  {view.columns.map((c, ci) => (
                    <td key={c.code} className={`px-2 py-1.5 text-[12.5px] font-semibold text-ink ${numeric(ci) ? "text-right tabular-nums" : ""}`}>
                      {ci === dimCols[0] ? cellText(g.label, c.format) : numeric(ci) ? (g.subtotal[ci] == null ? "" : cellText(g.subtotal[ci], c.format)) : ""}
                    </td>
                  ))}
                </tr>,
                ...g.rows.map((r) => row(visible[r], true)),
              ])
            : visible.map((i) => row(i))}
          {visible.length === 0 && (
            <tr>
              <td colSpan={view.columns.length} className="px-2 py-6 text-center text-[13px] text-ink-3">
                {t("Нет строк за период — продаж с такими условиями не было или данные ещё не загружены (см. «Проверка и источники»).")}
              </td>
            </tr>
          )}
        </tbody>
        {visible.length > 0 && (
          <tfoot>
            <tr className="border-t border-line">
              {view.columns.map((c, ci) => (
                <td key={c.code} className={`px-2 py-2 text-[12.5px] font-semibold text-ink ${numeric(ci) ? "text-right tabular-nums" : ""}`}>
                  {ci === 0 ? t("Итого за период") : c.kind === "metric" ? cellText(view.totals[c.code] ?? null, c.format) : c.kind === "compare" ? cellText(view.compareTotals?.[c.code.split("__")[0]] ?? null, c.format) : ""}
                </td>
              ))}
            </tr>
          </tfoot>
        )}
      </table>
    </div>
  );
}
