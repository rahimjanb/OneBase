"use client";

import { useState } from "react";
import { Calculator, Loader2 } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { num } from "@/lib/analytik";
import type { ScenarioParameter, SimulationResult } from "@/lib/simulator";

export type AnalysisScenario = { baselineId: string; kind: string; horizonDays: number; parameters: ScenarioParameter[]; newRegionName: string | null };

type Mode = "sensitivity" | "break_even" | "goal_seek" | "monte_carlo" | "cases" | "optimize";

const modes: { key: Mode; label: string; hint: string }[] = [
  { key: "sensitivity", label: "Чувствительность", hint: "какие параметры сильнее всего меняют показатель (торнадо)" },
  { key: "break_even", label: "Безубыточность", hint: "при каком значении параметра прибыль станет нулевой" },
  { key: "goal_seek", label: "Подбор под цель", hint: "какое значение параметра нужно для целевого показателя" },
  { key: "monte_carlo", label: "Риск", hint: "распределение показателя при неопределённости параметров (Монте-Карло)" },
  { key: "cases", label: "Худший / лучший", hint: "три случая при разбросе параметров" },
  { key: "optimize", label: "Оптимальный выпуск", hint: "выпуск товаров с наибольшей маржой при ограничениях сырья и мощности" },
];

const parameters: { code: string; label: string }[] = [
  { code: "sales.demand", label: "Спрос и объём продаж, %" },
  { code: "sales.price", label: "Цена, %" },
  { code: "sales.discount", label: "Скидки, п.п." },
  { code: "materials.price", label: "Цена сырья, %" },
  { code: "production.norms", label: "Нормы расхода сырья, %" },
  { code: "production.defect", label: "Брак, п.п." },
  { code: "production.capacity", label: "Мощность, %" },
  { code: "finance.expenses", label: "Расходы компании, %" },
  { code: "finance.customer_terms", label: "Срок оплаты покупателей, дн." },
];

type Row = { code: string; title: string; unit: string; low: number; high: number; metricLow: number | null; metricHigh: number | null; swing: number | null };
type Analysis = { id: string; mode: string; summary: string; result: Record<string, unknown> };

const field = "h-9 rounded-lg border border-line bg-surface px-2.5 text-sm text-ink outline-none focus:border-accent";

/**
 * Анализ сценария (What-If Decision Intelligence): движок симулятора запускается много раз на том же снимке — исходный сценарий не меняется.
 * Каждый анализ сохраняется в журнале расчётов Аналитика.
 */
export function SimAnalysis({ result, scenario }: { result: SimulationResult; scenario: AnalysisScenario }) {
  const t = useT();
  const available = result.metrics.filter((m) => m.scenario != null);
  const [mode, setMode] = useState<Mode>("sensitivity");
  const [metric, setMetric] = useState(
    (available.find((m) => m.code === "finance.gross_profit") ?? available.find((m) => m.code === "sales.revenue") ?? available[0])?.code ?? "sales.revenue",
  );
  const [param, setParam] = useState("sales.demand");
  const [target, setTarget] = useState("");
  const [spread, setSpread] = useState("15");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [analysis, setAnalysis] = useState<Analysis | null>(null);

  const run = async () => {
    setBusy(true);
    setError(null);
    const s = Math.abs(Number(spread.replace(",", ".")) || 15);
    const uncertain = [{ code: param, min: -s, mode: 0, max: s }];
    try {
      setAnalysis(
        await bff<Analysis>("analytik/simulator/analyze", {
          method: "POST",
          body: JSON.stringify({
            baselineId: scenario.baselineId, kind: scenario.kind, horizonDays: scenario.horizonDays, parameters: scenario.parameters,
            newRegionName: scenario.newRegionName, mode,
            metric: mode === "break_even" ? (available.some((m) => m.code === "finance.operating_profit") ? "finance.operating_profit" : "finance.gross_profit") : metric,
            param: mode === "goal_seek" || mode === "break_even" ? param : null,
            target: mode === "goal_seek" ? Number(target.replace(/\s/g, "").replace(",", ".")) : null,
            uncertain: mode === "monte_carlo" || mode === "cases" ? uncertain : null, runs: 300, seed: 42,
          }),
        }),
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setAnalysis(null);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="grid gap-3 px-4 pb-4 md:px-5 md:pb-5">
      <div className="flex flex-wrap gap-1.5">
        {modes.map((m) => (
          <button key={m.key} type="button" onClick={() => (setMode(m.key), setAnalysis(null))}
            className={`rounded-full px-3 py-1 text-xs font-medium ${mode === m.key ? "bg-accent text-white" : "bg-muted text-ink-2 hover:bg-line"}`}>
            {t(m.label)}
          </button>
        ))}
      </div>
      <p className="text-xs text-ink-3">{t(modes.find((m) => m.key === mode)!.hint)}</p>
      <div className="flex flex-wrap items-end gap-2">
        {mode !== "optimize" && mode !== "break_even" && (
          <label className="grid gap-1 text-[11px] text-ink-4">
            {t("Показатель")}
            <select className={field} value={metric} onChange={(e) => setMetric(e.target.value)}>
              {available.map((m) => (
                <option key={m.code} value={m.code}>{t(m.title)}</option>
              ))}
            </select>
          </label>
        )}
        {mode !== "sensitivity" && mode !== "optimize" && (
          <label className="grid gap-1 text-[11px] text-ink-4">
            {t("Параметр")}
            <select className={field} value={param} onChange={(e) => setParam(e.target.value)}>
              {parameters.map((p) => (
                <option key={p.code} value={p.code}>{t(p.label)}</option>
              ))}
            </select>
          </label>
        )}
        {mode === "goal_seek" && (
          <label className="grid gap-1 text-[11px] text-ink-4">
            {t("Цель показателя")}
            <input className={`${field} w-40`} inputMode="decimal" value={target} onChange={(e) => setTarget(e.target.value)} />
          </label>
        )}
        {(mode === "monte_carlo" || mode === "cases") && (
          <label className="grid gap-1 text-[11px] text-ink-4">
            {t("Разброс параметра, ±")}
            <input className={`${field} w-20`} inputMode="decimal" value={spread} onChange={(e) => setSpread(e.target.value)} />
          </label>
        )}
        <button type="button" disabled={busy || (mode === "goal_seek" && !target.trim())} onClick={run}
          className="inline-flex h-9 items-center gap-1.5 rounded-lg bg-accent px-3 text-sm font-medium text-white hover:bg-accent-strong disabled:opacity-60">
          {busy ? <Loader2 className="size-4 animate-spin" /> : <Calculator className="size-4" />}
          {t("Посчитать")}
        </button>
      </div>
      {error && <p className="rounded-xl bg-bad/10 px-3 py-2 text-sm text-bad">{error}</p>}
      {analysis && (
        <div className="grid gap-3">
          <p className="rounded-xl bg-accent-soft px-3 py-2 text-sm text-ink">{analysis.summary}</p>
          <AnalysisResult analysis={analysis} />
          <p className="text-[11px] text-ink-4">{t("Это расчёт сценария по снимку данных при допущениях, а не факт. Исходный сценарий не изменён.")}</p>
        </div>
      )}
    </div>
  );
}

function AnalysisResult({ analysis }: { analysis: Analysis }) {
  const t = useT();
  const r = analysis.result;
  if (analysis.mode === "sensitivity") {
    const rows = (r.rows as Row[]) ?? [];
    const max = Math.max(1, ...rows.map((x) => x.swing ?? 0));
    return (
      <ul className="grid gap-1.5">
        {rows.map((x) => (
          <li key={x.code} className="grid gap-0.5 text-xs">
            <div className="flex justify-between gap-2 text-ink-2">
              <span className="truncate">{t(x.title)} ({x.low} … {x.high} {x.unit})</span>
              <span className="tabular-nums text-ink-3">{num(x.metricLow)} … {num(x.metricHigh)}</span>
            </div>
            <div className="h-2 rounded-full bg-muted">
              <div className="h-2 rounded-full bg-accent" style={{ width: `${Math.max(2, ((x.swing ?? 0) / max) * 100)}%` }} />
            </div>
          </li>
        ))}
      </ul>
    );
  }

  if (analysis.mode === "monte_carlo") {
    const histogram = (r.histogram as { from: number; to: number; count: number }[] | undefined) ?? [];
    const max = Math.max(1, ...histogram.map((h) => h.count));
    return (
      <div className="grid gap-2">
        <dl className="grid grid-cols-3 gap-2 text-xs sm:grid-cols-6">
          {(["p10", "p50", "p90", "mean"] as const).map((k) => (
            <div key={k}>
              <dt className="text-ink-4">{k === "mean" ? t("среднее") : k.toUpperCase()}</dt>
              <dd className="tabular-nums text-ink">{num(r[k] as number)}</dd>
            </div>
          ))}
          <div>
            <dt className="text-ink-4">{t("ниже нуля")}</dt>
            <dd className="tabular-nums text-ink">{Math.round((r.probBelowZero as number) * 100)} %</dd>
          </div>
          <div>
            <dt className="text-ink-4">{t("ниже базы")}</dt>
            <dd className="tabular-nums text-ink">{Math.round((r.probBelowBase as number) * 100)} %</dd>
          </div>
        </dl>
        {histogram.length > 0 && (
          <div className="flex h-16 items-end gap-0.5">
            {histogram.map((h, i) => (
              <div key={i} className="flex-1 rounded-t bg-accent/70" style={{ height: `${(h.count / max) * 100}%` }} title={`${num(h.from)} … ${num(h.to)}: ${h.count}`} />
            ))}
          </div>
        )}
      </div>
    );
  }

  if (analysis.mode === "cases") {
    const cases = (r.cases as { name: string; value: number | null }[]) ?? [];
    const label: Record<string, string> = { worst: "Худший", base: "Базовый", best: "Лучший" };
    return (
      <dl className="grid grid-cols-3 gap-2 text-sm">
        {cases.map((c) => (
          <div key={c.name} className="rounded-xl border border-line px-3 py-2">
            <dt className="text-xs text-ink-4">{t(label[c.name] ?? c.name)}</dt>
            <dd className="tabular-nums font-semibold text-ink">{num(c.value)}</dd>
          </div>
        ))}
      </dl>
    );
  }

  if (analysis.mode === "optimize") {
    const plan = (r.plan as { product: string; kg: number; baseKg: number; marginPerKg: number }[]) ?? [];
    const constraints = (r.constraints as { name: string; shadowPrice: number; used: number; limit: number }[]) ?? [];
    const excluded = (r.excluded as string[]) ?? [];
    return (
      <div className="grid gap-2 text-sm">
        {plan.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[420px]">
              <thead className="text-left text-[11px] text-ink-4">
                <tr>
                  <th className="py-1 font-medium">{t("Товар")}</th>
                  <th className="py-1 font-medium">{t("Оптимально, кг")}</th>
                  <th className="py-1 font-medium">{t("База, кг")}</th>
                  <th className="py-1 font-medium">{t("Маржа на кг")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {plan.map((p) => (
                  <tr key={p.product}>
                    <td className="py-1">{p.product}</td>
                    <td className="py-1 tabular-nums">{num(p.kg)}</td>
                    <td className="py-1 tabular-nums">{num(p.baseKg)}</td>
                    <td className="py-1 tabular-nums">{num(p.marginPerKg)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {constraints.length > 0 && (
          <p className="text-xs text-ink-2">
            {t("Узкие места")}: {constraints.map((c) => `${c.name} (${t("+1 ед. даёт")} ${num(c.shadowPrice)})`).join("; ")}
          </p>
        )}
        {excluded.length > 0 && <p className="text-xs text-ink-4">{t("Не оптимизировались")}: {excluded.slice(0, 8).join("; ")}</p>}
      </div>
    );
  }

  return null;
}
