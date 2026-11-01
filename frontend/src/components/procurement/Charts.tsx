"use client";

import { useState } from "react";
import { useT } from "@/i18n/client";
import { compactMoney, money, type Amount } from "@/lib/procurement";

/** Единицы компактных сумм на языке интерфейса. */
export function useUnits() {
  const t = useT();
  return { bn: t("млрд"), mn: t("млн"), th: t("тыс.") };
}

/**
 * Столбцы по месяцам — одна серия (цвет акцента, без легенды: название говорит, что показано): столбцы не толще 24px со скруглением
 * сверху от общей базы, тонкая сетка из трёх линий, подписаны только максимум и последний месяц; наведение или фокус на столбце —
 * подсказка (месяц, сумма, число заказов); «Таблица» — все значения без наведения.
 */
export function MonthColumns({ data, countLabel }: { data: Amount[]; countLabel: string }) {
  const t = useT();
  const units = useUnits();
  const [hover, setHover] = useState<number | null>(null);
  const [table, setTable] = useState(false);
  const max = Math.max(0, ...data.map((d) => d.amount));
  const top = niceCeil(max);
  const maxIndex = data.findIndex((d) => d.amount === max && max > 0);
  const last = data.length - 1;
  if (max === 0) return <p className="py-8 text-center text-sm text-ink-3">{t("За эти месяцы закупок не было.")}</p>;
  return (
    <div>
      {table ? (
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-1.5 pr-3 font-semibold">{t("Месяц")}</th>
                <th className="py-1.5 pr-3 text-right font-semibold">{t("Сумма, сум")}</th>
                <th className="py-1.5 text-right font-semibold">{t(countLabel)}</th>
              </tr>
            </thead>
            <tbody>
              {data.map((d) => (
                <tr key={d.key} className="border-b border-line last:border-0">
                  <td className="py-1.5 pr-3 text-ink-2">{d.label}</td>
                  <td className="py-1.5 pr-3 text-right tabular-nums text-ink">{money(d.amount)}</td>
                  <td className="py-1.5 text-right tabular-nums text-ink-2">{d.count}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="relative">
          <div className="relative ml-12 h-44">
            {[0, 0.5, 1].map((f) => (
              <div key={f} className="absolute inset-x-0 border-t border-line" style={{ bottom: `${f * 100}%` }}>
                <span className="absolute -left-12 -translate-y-1/2 text-[10px] tabular-nums text-ink-3">{f === 0 ? "0" : compactMoney(top * f, units)}</span>
              </div>
            ))}
            <div className="absolute inset-0 flex items-end gap-[2px]">
              {data.map((d, i) => {
                const h = top > 0 ? (d.amount / top) * 100 : 0;
                const labeled = (i === maxIndex || i === last) && d.amount > 0;
                return (
                  <button
                    key={d.key}
                    type="button"
                    onPointerEnter={() => setHover(i)}
                    onPointerLeave={() => setHover(null)}
                    onFocus={() => setHover(i)}
                    onBlur={() => setHover(null)}
                    aria-label={`${d.label}: ${money(d.amount)} ${t("сум")}`}
                    className="group relative flex h-full flex-1 items-end justify-center focus:outline-none"
                  >
                    {labeled && (
                      <span className="absolute w-max -translate-y-1 text-[10px] font-medium tabular-nums text-ink-2" style={{ bottom: `${h}%` }}>
                        {compactMoney(d.amount, units)}
                      </span>
                    )}
                    <span
                      className={`block w-full max-w-6 rounded-t bg-accent transition-opacity ${hover !== null && hover !== i ? "opacity-50" : ""} group-focus-visible:ring-2 group-focus-visible:ring-accent/40`}
                      style={{ height: `${Math.max(d.amount > 0 ? 1 : 0, h)}%` }}
                    />
                  </button>
                );
              })}
            </div>
            {hover !== null && (
              <div
                role="tooltip"
                className="pointer-events-none absolute z-10 -translate-x-1/2 rounded-lg border border-line bg-surface px-2.5 py-1.5 text-xs shadow-lg"
                style={{ left: `${((hover + 0.5) / data.length) * 100}%`, bottom: "100%" }}
              >
                <div className="font-semibold tabular-nums text-ink">
                  {money(data[hover].amount)} {t("сум")}
                </div>
                <div className="text-ink-3">
                  {data[hover].label} · {t(countLabel)}: {data[hover].count}
                </div>
              </div>
            )}
          </div>
          <div className="ml-12 mt-1 flex gap-[2px]">
            {data.map((d, i) => (
              <span key={d.key} className={`flex-1 text-center text-[10px] tabular-nums ${i === last ? "font-medium text-ink-2" : "text-ink-3"}`}>
                {d.label.slice(0, 2)}
              </span>
            ))}
          </div>
        </div>
      )}
      <button type="button" onClick={() => setTable(!table)} className="mt-2 text-xs font-medium text-accent-strong hover:underline">
        {table ? t("Показать график") : t("Таблица")}
      </button>
    </div>
  );
}

/**
 * Рейтинг по сумме (отделы, категории): горизонтальные полосы одного цвета от общей базы, значение — у конца полосы текстом, не цветом;
 * больше восьми — остальные в «Прочее»; наведение или фокус — подсказка с числом позиций.
 */
export function AmountBars({ data, empty }: { data: Amount[]; empty: string }) {
  const t = useT();
  const units = useUnits();
  const [hover, setHover] = useState<string | null>(null);
  if (data.length === 0) return <p className="py-6 text-center text-sm text-ink-3">{t(empty)}</p>;
  const shown = data.length > 8 ? [...data.slice(0, 7), { key: "other", label: t("Прочее"), amount: data.slice(7).reduce((s, d) => s + d.amount, 0), count: data.slice(7).reduce((s, d) => s + d.count, 0) }] : data;
  const max = Math.max(...shown.map((d) => d.amount), 1);
  return (
    <ul className="space-y-2.5">
      {shown.map((d) => (
        <li key={d.key}>
          <button
            type="button"
            onPointerEnter={() => setHover(d.key)}
            onPointerLeave={() => setHover(null)}
            onFocus={() => setHover(d.key)}
            onBlur={() => setHover(null)}
            className="relative block w-full text-left focus:outline-none"
            aria-label={`${d.label}: ${money(d.amount)} ${t("сум")}, ${t("позиций: {n}", { n: d.count })}`}
          >
            <span className="flex items-baseline justify-between gap-3 text-sm">
              <span className="truncate text-ink-2">{d.label}</span>
              <span className="shrink-0 font-medium tabular-nums text-ink">{compactMoney(d.amount, units)}</span>
            </span>
            <span className="mt-1 block h-2 w-full">
              <span className={`block h-2 rounded-r bg-accent transition-opacity ${hover !== null && hover !== d.key ? "opacity-50" : ""}`} style={{ width: `${Math.max(1, (d.amount / max) * 100)}%` }} />
            </span>
            {hover === d.key && (
              <span role="tooltip" className="pointer-events-none absolute right-0 top-full z-10 mt-1 rounded-lg border border-line bg-surface px-2.5 py-1.5 text-xs shadow-lg">
                <span className="block font-semibold tabular-nums text-ink">
                  {money(d.amount)} {t("сум")}
                </span>
                <span className="block text-ink-3">{t("позиций: {n}", { n: d.count })}</span>
              </span>
            )}
          </button>
        </li>
      ))}
    </ul>
  );
}

/** Верх шкалы — «круглое» число не меньше максимума: 1, 2, 2,5, 5 × 10ⁿ. */
function niceCeil(value: number): number {
  if (value <= 0) return 1;
  const power = 10 ** Math.floor(Math.log10(value));
  for (const step of [1, 2, 2.5, 5, 10]) if (step * power >= value) return step * power;
  return 10 * power;
}
