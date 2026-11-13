"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { Download, FileSpreadsheet, FileText, Loader2, Plus } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  compact,
  date,
  dateTime,
  kpiValue,
  methodLabel,
  metricOptions,
  moduleLabel,
  num,
  periodKinds,
  reportKinds,
  type ForecastView,
  type Insight,
  type ReportDoc,
  type ReportListRow,
} from "@/lib/analytik";
import { Card, Empty, InsightCard, KpiGrid, LineChart, Notes, SeverityBadge } from "./ui";

// ---------- Прогнозы ----------

export function ForecastsView({ initial, canSales }: { initial: ForecastView | null; canSales: boolean }) {
  const t = useT();
  const [metric, setMetric] = useState(initial?.metric ?? (canSales ? "sales.revenue" : "procurement.consumption"));
  const [granularity, setGranularity] = useState<"day" | "week" | "month">(initial?.granularity ?? "day");
  const [horizon, setHorizon] = useState(initial?.result.points.length ?? 30);
  const [scenario, setScenario] = useState(0);
  const [data, setData] = useState<ForecastView | null>(initial);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let alive = true;
    setLoading(true);
    setError(null);
    bff<ForecastView>(`analytik/forecast?metric=${metric}&granularity=${granularity}&horizon=${horizon}${scenario ? `&scenario=${scenario}` : ""}`)
      .then((f) => alive && setData(f))
      .catch((e: Error) => alive && (setError(e.message), setData(null)))
      .finally(() => alive && setLoading(false));
    return () => {
      alive = false;
    };
  }, [metric, granularity, horizon, scenario]);

  const history = data?.history.slice(granularity === "day" ? -60 : -24) ?? [];
  const format = (v: number) => (data?.unit === "сум" ? compact(v) : num(v, 1));
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end gap-3">
        <label className="text-xs text-ink-3">
          {t("Показатель")}
          <select value={metric} onChange={(e) => setMetric(e.target.value)} className="mt-1 block h-9 rounded-lg border border-line bg-surface px-2 text-sm text-ink">
            {metricOptions.map((m) => (
              <option key={m.metric} value={m.metric}>
                {t(m.label)}
              </option>
            ))}
          </select>
        </label>
        <label className="text-xs text-ink-3">
          {t("Шаг")}
          <select value={granularity} onChange={(e) => { const g = e.target.value as typeof granularity; setGranularity(g); setHorizon(g === "day" ? 30 : g === "week" ? 8 : 3); }} className="mt-1 block h-9 rounded-lg border border-line bg-surface px-2 text-sm text-ink">
            <option value="day">{t("По дням")}</option>
            <option value="week">{t("По неделям")}</option>
            <option value="month">{t("По месяцам")}</option>
          </select>
        </label>
        <label className="text-xs text-ink-3">
          {t("Горизонт")}
          <input type="number" min={1} max={granularity === "day" ? 120 : granularity === "week" ? 26 : 12} value={horizon} onChange={(e) => setHorizon(Math.max(1, Number(e.target.value) || 1))} className="mt-1 block h-9 w-24 rounded-lg border border-line bg-surface px-2 text-sm text-ink" />
        </label>
        <label className="text-xs text-ink-3">
          {t("Сценарий «что если», %")}
          <input type="number" min={-90} max={500} step={5} value={scenario} onChange={(e) => setScenario(Number(e.target.value) || 0)} className="mt-1 block h-9 w-28 rounded-lg border border-line bg-surface px-2 text-sm text-ink" />
        </label>
        {loading && <Loader2 className="mb-2 size-4 animate-spin text-ink-4" />}
      </div>
      {error && <Empty text={error} />}
      {data && (
        <>
          <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-4">
            <Stat label="Пессимистичный" value={`${format(data.result.totalLower)} ${data.unit}`} />
            <Stat label="Реалистичный" value={`${format(data.result.total)} ${data.unit}`} strong />
            <Stat label="Оптимистичный" value={`${format(data.result.totalUpper)} ${data.unit}`} />
            <Stat label="Ошибка на истории (WAPE)" value={data.result.wape == null ? "—" : `${num(data.result.wape * 100, 1)} %`} hint={data.result.reliable ? "прогноз надёжный" : "ориентировочно"} />
          </div>
          <Card title={`${t(data.title)}: ${t("история и прогноз")}`} subtitle={`${t("Метод")}: ${t(methodLabel[data.result.method] ?? data.result.method)} · ${t("доверительный диапазон")} ${Math.round(data.result.confidence * 100)}%${data.scope ? ` · ${data.scope}` : ""}`}>
            <LineChart
              labels={[...history.map((h) => date(h.date)), ...data.result.points.map((p) => date(p.date))]}
              series={[
                { name: "Факт", values: [...history.map((h) => h.value), ...data.result.points.map(() => null)], color: "var(--color-accent)" },
                { name: "Прогноз", values: [...history.map((_, i) => (i === history.length - 1 ? history[i].value : null)), ...data.result.points.map((p) => p.value)], color: "var(--color-violet)", dashed: true },
              ]}
              band={{ lower: [...history.map(() => null), ...data.result.points.map((p) => p.lower)], upper: [...history.map(() => null), ...data.result.points.map((p) => p.upper)] }}
              format={format}
              height={220}
            />
          </Card>
          <div className="grid gap-4 lg:grid-cols-2">
            <Card title="Проверенные методы" subtitle={`${t("Ошибка на отложенной части истории")}: ${data.result.holdoutPoints} ${t("точек")}`}>
              <ul className="space-y-1 text-[12.5px]">
                {data.result.methods.map((m) => (
                  <li key={m.method} className={`flex justify-between gap-3 ${m.method === data.result.method ? "font-semibold text-ink" : "text-ink-3"}`}>
                    <span>{t(methodLabel[m.method] ?? m.method)}</span>
                    <span>{m.wape == null ? "—" : `${num(m.wape * 100, 1)} %`}</span>
                  </li>
                ))}
              </ul>
              {data.past && (
                <p className="mt-3 text-xs text-ink-3">
                  {t("Точность прошлых прогнозов")}: WAPE {data.past.wape == null ? "—" : `${num(data.past.wape * 100, 1)} %`} {t("по")} {data.past.evaluated} {t("сверенным с фактом")} ({date(data.past.from)} — {date(data.past.to)})
                </p>
              )}
            </Card>
            <Card title="Допущения прогноза">
              <Notes notes={[...data.result.assumptions, "Прогноз — не гарантированный результат: диапазон показывает неопределённость."]} sources={data.sources} />
            </Card>
          </div>
        </>
      )}
    </div>
  );
}

function Stat({ label, value, hint, strong }: { label: string; value: string; hint?: string; strong?: boolean }) {
  const t = useT();
  return (
    <div className={`rounded-xl border p-3 ${strong ? "border-accent bg-accent-soft" : "border-line bg-surface"}`}>
      <p className="text-xs text-ink-3">{t(label)}</p>
      <p className="mt-1 text-lg font-semibold text-ink">{value}</p>
      {hint && <p className="text-[11px] text-ink-4">{t(hint)}</p>}
    </div>
  );
}

// ---------- Риски ----------

export function RisksView({ active, resolved, focus = null }: { active: Insight[]; resolved: Insight[]; focus?: string | null }) {
  const t = useT();
  // Находка из «Обзора» (?risk=…) — раскрыта и прокручена к себе.
  useEffect(() => {
    if (!focus) return;
    document.getElementById(`risk-${focus}`)?.scrollIntoView({ block: "center" });
  }, [focus]);
  const [module, setModule] = useState<string>("all");
  const [kind, setKind] = useState<string>("all");
  const [showResolved, setShowResolved] = useState(false);
  const list = (showResolved ? resolved : active).filter((i) => (module === "all" || i.module === module) && (kind === "all" || i.kind === kind));
  const modules = useMemo(() => Array.from(new Set(active.concat(resolved).map((i) => i.module))), [active, resolved]);
  const counts = { critical: active.filter((i) => i.severity === "critical").length, warning: active.filter((i) => i.severity === "warning").length, info: active.filter((i) => i.severity === "info").length };
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-3 gap-2.5">
        {(["critical", "warning", "info"] as const).map((s) => (
          <div key={s} className="rounded-xl border border-line bg-surface p-3">
            <SeverityBadge value={s} />
            <p className="mt-1.5 text-2xl font-semibold text-ink">{counts[s]}</p>
          </div>
        ))}
      </div>
      <div className="flex flex-wrap items-center gap-2 text-xs">
        <select value={module} onChange={(e) => setModule(e.target.value)} className="h-8 rounded-lg border border-line bg-surface px-2 text-ink" aria-label={t("Модуль")}>
          <option value="all">{t("Все модули")}</option>
          {modules.map((m) => (
            <option key={m} value={m}>
              {t(moduleLabel[m] ?? m)}
            </option>
          ))}
        </select>
        <select value={kind} onChange={(e) => setKind(e.target.value)} className="h-8 rounded-lg border border-line bg-surface px-2 text-ink" aria-label={t("Вид")}>
          <option value="all">{t("Все виды")}</option>
          <option value="risk">{t("Риски")}</option>
          <option value="anomaly">{t("Отклонения")}</option>
          <option value="quality">{t("Качество данных")}</option>
          <option value="opportunity">{t("Возможности")}</option>
        </select>
        <label className="inline-flex items-center gap-1.5 text-ink-3">
          <input type="checkbox" checked={showResolved} onChange={(e) => setShowResolved(e.target.checked)} />
          {t("Закрытые за 30 дней")}
        </label>
      </div>
      {list.length === 0 ? (
        <Empty text={showResolved ? "Закрытых находок нет" : "Активных рисков и отклонений нет"} hint="Поиск выполняется каждый час правилами по данным OneBase." />
      ) : (
        <div className="space-y-2">
          {list.map((i) => (
            <div key={i.id} id={`risk-${i.id}`} className={i.id === focus ? "scroll-mt-24 rounded-xl ring-2 ring-accent/50" : "scroll-mt-24"}>
              <InsightCard insight={i} open={i.id === focus} />
            </div>
          ))}
        </div>
      )}
      <p className="text-[11px] text-ink-4">{t("Причина показывается, только если она подтверждена данными; остальное — гипотезы. Повторная находка обновляется, а не создаётся заново; уведомление — при новой находке или росте критичности.")}</p>
    </div>
  );
}

// ---------- Отчёты ----------

export function ReportsView({ reports }: { reports: ReportListRow[] }) {
  const t = useT();
  const router = useRouter();
  const [kind, setKind] = useState("health");
  const [period, setPeriod] = useState("month");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      const row = await bff<ReportListRow>("analytik/reports", { method: "POST", body: JSON.stringify({ kind, period }) });
      router.push(`/analytik/reports/${row.id}`);
    } catch (e) {
      setError((e as Error).message);
      setBusy(false);
    }
  };
  return (
    <div className="space-y-4">
      <Card title="Сформировать отчёт" subtitle="Цифры считает OneBase по данным на момент формирования; в отчёте — только разделы по вашим правам">
        <div className="flex flex-wrap items-end gap-2">
          <label className="text-xs text-ink-3">
            {t("Вид")}
            <select value={kind} onChange={(e) => setKind(e.target.value)} className="mt-1 block h-9 rounded-lg border border-line bg-surface px-2 text-sm text-ink">
              {reportKinds.map((k) => (
                <option key={k.kind} value={k.kind}>
                  {t(k.label)}
                </option>
              ))}
            </select>
          </label>
          <label className="text-xs text-ink-3">
            {t("Период")}
            <select value={period} onChange={(e) => setPeriod(e.target.value)} className="mt-1 block h-9 rounded-lg border border-line bg-surface px-2 text-sm text-ink">
              {periodKinds.filter((p) => p.kind !== "custom").map((p) => (
                <option key={p.kind} value={p.kind}>
                  {t(p.label)}
                </option>
              ))}
            </select>
          </label>
          <button type="button" onClick={() => void create()} disabled={busy} className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-accent px-4 text-sm font-medium text-white disabled:opacity-50">
            {busy ? <Loader2 className="size-4 animate-spin" /> : <Plus className="size-4" />}
            {t("Сформировать")}
          </button>
        </div>
        {error && <p className="mt-2 text-xs text-bad">{error}</p>}
      </Card>
      <Card title="Отчёты" subtitle="Плановые (ежедневный, недельный, месячный) формируются автоматически; ваши — видны только вам">
        {reports.length === 0 ? (
          <Empty text="Отчётов пока нет" hint="Первый плановый отчёт появится после ежедневного запуска." />
        ) : (
          <ul className="divide-y divide-line">
            {reports.map((r) => (
              <li key={r.id} className="flex items-center justify-between gap-3 py-2.5">
                <Link href={`/analytik/reports/${r.id}`} className="min-w-0 flex-1">
                  <p className="truncate text-[13px] font-medium text-ink hover:underline">{r.title}</p>
                  <p className="text-[11px] text-ink-4">
                    {t(r.scheduled ? "Плановый" : "Мой")} · {t("сформирован")} {dateTime(r.createdAt)}
                  </p>
                </Link>
                <FileLinks id={r.id} title={r.title} />
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}

function FileLinks({ id, title }: { id: string; title: string }) {
  const t = useT();
  const name = encodeURIComponent(title.replace(/[\\/:*?"<>|«»]/g, " ").replace(/\s+/g, " ").trim() || "Отчёт");
  return (
    <span className="flex shrink-0 gap-1.5">
      <a href={`/bff/api/analytik/reports/${id}/xlsx/${name}.xlsx`} className="inline-flex h-8 items-center gap-1 rounded-lg border border-line px-2.5 text-xs text-ink-2 hover:border-line-strong">
        <FileSpreadsheet className="size-3.5" />
        Excel
      </a>
      <a href={`/bff/api/analytik/reports/${id}/pdf/${name}.pdf`} className="inline-flex h-8 items-center gap-1 rounded-lg border border-line px-2.5 text-xs text-ink-2 hover:border-line-strong">
        <FileText className="size-3.5" />
        PDF
      </a>
      <span className="sr-only">{t("Скачать")}</span>
    </span>
  );
}

export function ReportView({ id, doc }: { id: string; doc: ReportDoc }) {
  const t = useT();
  return (
    <div className="space-y-4">
      <Card
        title={doc.title}
        subtitle={`${t("Сформирован")} ${dateTime(doc.generatedAt)} · ${t("период")} ${date(doc.from)} — ${date(doc.to)}`}
        action={<FileLinks id={id} title={doc.title} />}
      >
        <p className="text-[13.5px] leading-relaxed text-ink-2">{doc.summary}</p>
      </Card>
      {doc.sections.length === 0 && <Empty text="В отчёте нет разделов, доступных по вашим правам" />}
      {doc.sections.map((s) => (
        <Card key={s.key} title={s.title}>
          {s.kpis.length > 0 && <KpiGrid kpis={s.kpis} columns="sm:grid-cols-3 xl:grid-cols-5" compact />}
          {s.tables.map((table) =>
            table.rows.length === 0 ? null : (
              <div key={table.title} className="mt-3 overflow-x-auto">
                <p className="mb-1.5 text-xs font-medium text-ink-3">{t(table.title)}</p>
                <table className="w-full min-w-[560px] text-[12px]">
                  <thead>
                    <tr className="border-b border-line text-ink-4">
                      {table.columns.map((c) => (
                        <th key={c.header} className={`px-2 py-1.5 font-normal ${["text"].includes(c.format) ? "text-left" : "text-right"}`}>
                          {t(c.header)}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {table.rows.slice(0, 100).map((row, i) => (
                      <tr key={i} className="border-b border-line/60">
                        {row.map((cell, j) => {
                          const format = table.columns[j]?.format ?? "text";
                          return (
                            <td key={j} className={`px-2 py-1.5 ${format === "text" ? "text-left text-ink-2" : "text-right tabular-nums text-ink"}`}>
                              {cell == null ? "—" : format === "money" ? compact(cell as number) : format === "percent" ? kpiValue(cell as number, "%") : format === "date" ? date(String(cell)) : typeof cell === "number" ? num(cell, format === "integer" ? 0 : 2) : String(cell)}
                            </td>
                          );
                        })}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ),
          )}
          <Notes notes={s.notes} sources={s.sources} />
        </Card>
      ))}
      <a href={`/bff/api/analytik/reports/${id}/xlsx/report.xlsx`} className="inline-flex items-center gap-1.5 text-xs text-accent-strong hover:underline">
        <Download className="size-3.5" />
        {t("Скачать отчёт целиком в Excel")}
      </a>
    </div>
  );
}
