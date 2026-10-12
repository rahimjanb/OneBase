"use client";

import { useState } from "react";
import { Section } from "./bits";
import { CategoryPlanRows, categoryPlanHint } from "./blocks";
import { kg, pct } from "@/lib/sales/format";
import type { CategoryPlanFact, PlanKind, PlanSource, TargetLevel } from "@/lib/sales/types";

type View = "chart" | "table";

const levelText: Record<TargetLevel, string> = { Good: "text-ok", Warning: "text-warn", Bad: "text-bad" };

/** Шкала оси: шаг 1 / 2 / 2,5 / 5 × 10ⁿ, около четырёх делений. */
function scale(max: number): number[] {
  if (max <= 0) return [0];
  const rough = max / 4;
  const pow = 10 ** Math.floor(Math.log10(rough));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * pow).find((s) => s >= rough) ?? 10 * pow;
  return Array.from({ length: Math.ceil(max / step) + 1 }, (_, i) => i * step);
}

/** Сокращённые килограммы для оси: «12 т», «850 кг». */
const axis = (v: number) => (v >= 1000 ? `${(v / 1000).toLocaleString("ru-RU", { maximumFractionDigits: 1 })} т` : `${v.toLocaleString("ru-RU")} кг`);

/**
 * Столбцы «план / факт» по категориям: план — светлый столбец, факт — акцентный; над парой — выполнение (цвет — уровень с сервера).
 * Категории без плана — только факт, приглушённо. Значения и уровни считает сервер, здесь только рисунок.
 */
function PlanFactBars({ rows }: { rows: CategoryPlanFact[] }) {
  const [hover, setHover] = useState<number | null>(null);
  const ticks = scale(Math.max(0, ...rows.flatMap((r) => [r.planKg ?? 0, r.factKg])));
  const top = ticks[ticks.length - 1] || 1;
  const h = (v: number | null) => `${Math.max(0, ((v ?? 0) / top) * 100)}%`;

  return (
    <div>
      <div className="mb-3 flex flex-wrap items-center gap-4 text-xs text-ink-2">
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm border border-line bg-muted" />
          План
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm bg-accent" />
          Факт
        </span>
      </div>
      {/* Поле графика: сверху 1,5rem под подписи выполнения, снизу 2,5rem под названия; столбцы и сетка — в одной высоте. */}
      <div className="relative h-72" onMouseLeave={() => setHover(null)}>
        <div className="pointer-events-none absolute inset-x-0 bottom-10 top-6">
          {ticks.map((t) => (
            <div key={t} className="absolute inset-x-0 border-t border-line" style={{ bottom: `${(t / top) * 100}%` }}>
              <span className="absolute -top-2 left-0 w-10 text-right text-[10px] tabular-nums text-ink-3">{axis(t)}</span>
            </div>
          ))}
        </div>
        <div className="absolute inset-0 flex gap-2 pl-12">
          {rows.map((r, i) => {
            const planned = r.planKg != null;
            const peak = Math.max(r.planKg ?? 0, r.factKg);
            return (
              <div
                key={`${r.categoryId ?? r.name}`}
                className={`flex min-w-0 flex-1 flex-col ${planned ? "" : "opacity-55"}`}
                onMouseEnter={() => setHover(i)}
                title={`${r.name}: план ${planned ? `${kg(r.planKg)} кг` : "нет"} · факт ${kg(r.factKg)} кг${planned ? ` · выполнение ${pct(r.execution)}` : ""}`}
              >
                <div className={`relative mt-6 flex flex-1 items-end justify-center gap-1 rounded-lg ${hover === i ? "bg-muted/60" : ""}`}>
                  {planned && (
                    <span
                      className={`absolute left-1/2 -translate-x-1/2 text-[11px] font-semibold tabular-nums ${r.executionLevel ? levelText[r.executionLevel] : "text-ink-3"}`}
                      style={{ bottom: `calc(${h(peak)} + 4px)` }}
                    >
                      {pct(r.execution)}
                    </span>
                  )}
                  {planned && <div className="w-full max-w-5 rounded-t-md border border-b-0 border-line bg-muted" style={{ height: h(r.planKg) }} />}
                  <div
                    className="w-full max-w-5 rounded-t-md bg-accent"
                    style={{ height: h(r.factKg), backgroundImage: "linear-gradient(to top, transparent, rgb(255 255 255 / 0.18))" }}
                  />
                </div>
                <div className={`mt-1.5 line-clamp-2 h-8.5 w-full text-center text-[11px] leading-tight ${hover === i ? "text-ink" : "text-ink-2"}`}>{r.name}</div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}

/** «План и факт по категориям»: столбцы (по умолчанию) или подробная таблица — прогноз к плану, «осталось», весь факт. */
export function CategoryPlanCard({
  rows,
  total,
  source,
  plan,
}: {
  rows: CategoryPlanFact[];
  total: CategoryPlanFact | null;
  source: PlanSource;
  plan: PlanKind;
}) {
  const [view, setView] = useState<View>("chart");
  if (rows.length === 0) return null;
  return (
    <Section
      title="План и факт по категориям"
      hint={categoryPlanHint(source)}
      actions={
        <div className="flex overflow-hidden rounded-lg border border-line text-xs" role="group" aria-label="Вид">
          {(
            [
              ["chart", "График"],
              ["table", "Таблица"],
            ] as [View, string][]
          ).map(([key, label]) => (
            <button
              key={key}
              type="button"
              aria-pressed={view === key}
              onClick={() => setView(key)}
              className={`px-2.5 py-1.5 ${view === key ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}
            >
              {label}
            </button>
          ))}
        </div>
      }
    >
      {view === "chart" ? (
        <>
          <PlanFactBars rows={rows} />
          {total && (
            <p className="mt-3 text-xs text-ink-2">
              Итого: план <b className="font-semibold text-ink">{kg(total.planKg)} кг</b>, факт <b className="font-semibold text-ink">{kg(total.factKg)} кг</b>,
              выполнение{" "}
              <b className={`font-semibold ${total.executionLevel ? levelText[total.executionLevel] : "text-ink"}`}>{pct(total.execution)}</b>
              {total.forecastExecution != null && <>, прогноз к плану {pct(total.forecastExecution)}</>}. Подробности — в «Таблице».
            </p>
          )}
        </>
      ) : (
        <CategoryPlanRows rows={rows} total={total} source={source} plan={plan} />
      )}
    </Section>
  );
}
