"use client";

import { useSyncExternalStore } from "react";
import { AkbChart } from "./AkbChart";
import { kg, money, num } from "@/lib/sales/format";
import type { AkbByMonth } from "@/lib/sales/types";

export type AkbMetric = "akb" | "kg" | "sum";

// Выбор «АКБ / кг / сум» общий для всех карточек «по месяцам» на всех страницах раздела и живёт до перезагрузки (DOC-filters §12):
// хранится в модуле, не в адресе, — переходы между страницами его не сбрасывают.
let current: AkbMetric = "akb";
const listeners = new Set<() => void>();
const subscribe = (listener: () => void) => {
  listeners.add(listener);
  return () => listeners.delete(listener);
};
const snapshot = () => current;
const serverSnapshot = (): AkbMetric => "akb";
function select(metric: AkbMetric) {
  current = metric;
  listeners.forEach((listener) => listener());
}

/** Текущая мера карточек «по месяцам» и переключение. */
export function useAkbMetric(): [AkbMetric, (metric: AkbMetric) => void] {
  return [useSyncExternalStore(subscribe, snapshot, serverSnapshot), select];
}

const options: { key: AkbMetric; label: string }[] = [
  { key: "akb", label: "АКБ" },
  { key: "kg", label: "кг" },
  { key: "sum", label: "сум" },
];

const views = {
  akb: { title: "АКБ по месяцам", hint: "уникальные точки, купившие за месяц", head: "Категория — АКБ, точек", total: "Всего точек", format: num },
  kg: { title: "Продажи по месяцам, кг", hint: "чистый вес за месяц", head: "Категория — кг", total: "Итого, кг", format: kg },
  sum: { title: "Продажи по месяцам, сум", hint: "чистая выручка за месяц", head: "Категория — сум", total: "Итого, сум", format: money },
} satisfies Record<AkbMetric, { title: string; hint: string; head: string; total: string; format: (v: number | null | undefined) => string }>;

/** Данные выбранной меры в форме «АКБ по месяцам»: те же месяцы и строки, значения — с сервера (kg и sum лежат рядом с АКБ). */
function dataOf(data: AkbByMonth, metric: AkbMetric): AkbByMonth {
  const measure = metric === "kg" ? data.kg : metric === "sum" ? data.sum : undefined;
  return measure ? { ...data, total: measure.total, categories: measure.categories, average: measure.average } : data;
}

/** Переключатель меры «по месяцам»: кнопки АКБ / кг / сум. */
export function AkbMetricSwitch() {
  const [metric, setMetric] = useAkbMetric();
  return (
    <div className="flex overflow-hidden rounded-lg border border-line text-xs" role="group" aria-label="Мера">
      {options.map((o) => (
        <button
          key={o.key}
          type="button"
          aria-pressed={metric === o.key}
          onClick={() => setMetric(o.key)}
          className={`px-2.5 py-1.5 ${metric === o.key ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}

/**
 * «АКБ по месяцам» вторички (DOC-rules §9.1, DOC-filters §1): АКБ, кг или сум по одним строкам — сервер отдаёт все три меры и средние,
 * здесь только выбор, что показать. Категории — без скрытых и не больше семи (сервер), остальные есть в карточках категорий.
 */
export function AkbMonthsCard({ data }: { data: AkbByMonth }) {
  const [metric] = useAkbMetric();
  const view = views[metric];
  return (
    <AkbChart
      data={dataOf(data, metric)}
      title={view.title}
      hint={view.hint}
      head={view.head}
      totalLabel={view.total}
      format={view.format}
      actions={<AkbMetricSwitch />}
      note={
        metric === "akb" ? undefined : (
          <>
            Чистые {metric === "kg" ? "кг" : "суммы"} за месяц (заказы минус возвраты) по тем же месяцам и категориям, что АКБ; итог — все продажи
            подразделения, в том числе категорий, которых на графике нет.{" "}
          </>
        )
      }
    />
  );
}
