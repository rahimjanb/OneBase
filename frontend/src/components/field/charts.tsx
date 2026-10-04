import { money, num } from "@/lib/sales/format";
import type { FieldDayPoint } from "@/lib/field/types";

/**
 * Продажи по дням месяца: столбики (сумы или кг), линия дневного плана (кг). Пустые дни показываются, чтобы был виден ритм.
 */
export function DayBars({ days, year, month, metric = "sum", dailyPlan }: { days: FieldDayPoint[]; year: number; month: number; metric?: "sum" | "kg"; dailyPlan?: number | null }) {
  const count = new Date(Date.UTC(year, month, 0)).getUTCDate();
  const byDay = new Map(days.map((d) => [Number(d.date.slice(8, 10)), d]));
  const values = Array.from({ length: count }, (_, i) => {
    const d = byDay.get(i + 1);
    return d ? (metric === "sum" ? d.sum : d.kg) : 0;
  });
  const max = Math.max(...values, metric === "kg" && dailyPlan ? dailyPlan : 0, 1);
  const total = values.reduce((a, b) => a + b, 0);

  return (
    <div>
      <div className="mb-2 flex items-baseline justify-between text-xs text-ink-3">
        <span>
          Итого: <span className="font-medium text-ink">{metric === "sum" ? `${money(total)} сум` : `${num(total)} кг`}</span>
        </span>
        {metric === "kg" && dailyPlan ? <span>— — план в день {num(dailyPlan)} кг</span> : null}
      </div>
      <div className="relative flex h-32 items-end gap-[2px]">
        {metric === "kg" && dailyPlan ? <div className="absolute inset-x-0 border-t border-dashed border-ink-3" style={{ bottom: `${(dailyPlan / max) * 100}%` }} /> : null}
        {values.map((v, i) => {
          const date = new Date(Date.UTC(year, month - 1, i + 1));
          const sunday = date.getUTCDay() === 0;
          return (
            <div key={i} className="group relative flex h-full min-w-0 flex-1 items-end" title={`${i + 1}: ${metric === "sum" ? `${money(v)} сум` : `${num(v)} кг`}`}>
              <div className={`w-full rounded-t-sm ${sunday ? "bg-line" : metric === "kg" && dailyPlan && v >= dailyPlan ? "bg-ok" : "bg-accent"}`} style={{ height: `${Math.max(v > 0 ? 2 : 0, (v / max) * 100)}%` }} />
            </div>
          );
        })}
      </div>
      <div className="mt-1 flex justify-between text-[10px] text-ink-3">
        <span>1</span>
        <span>{Math.round(count / 2)}</span>
        <span>{count}</span>
      </div>
    </div>
  );
}

/** Горизонтальная полоса «план — факт» с отметкой ожидаемого темпа. */
export function PlanFactBar({ label, fact, plan, unit, expected }: { label: string; fact: number; plan: number | null; unit: string; expected?: number }) {
  const share = plan && plan > 0 ? fact / plan : null;
  return (
    <div>
      <div className="flex items-baseline justify-between gap-2 text-sm">
        <span className="text-ink-2">{label}</span>
        <span className="tabular-nums text-ink">
          <span className="font-semibold">{unit === "сум" ? money(fact) : num(fact)}</span>
          {plan ? <span className="text-ink-3"> / {unit === "сум" ? money(plan) : num(plan)} {unit}</span> : <span className="text-ink-3"> {unit} · плана нет</span>}
        </span>
      </div>
      {share != null && (
        <div className="relative mt-1.5 h-2 w-full overflow-hidden rounded-full bg-muted">
          <div className={`h-full rounded-full ${share >= 1 ? "bg-ok" : share >= (expected ?? 0.7) ? "bg-accent" : "bg-warn"}`} style={{ width: `${Math.min(100, share * 100)}%` }} />
          {expected != null && <div className="absolute top-0 h-full w-0.5 bg-ink" style={{ left: `${Math.min(100, expected * 100)}%` }} title={`Ожидаемый темп: ${Math.round(expected * 100)}%`} />}
        </div>
      )}
      {share != null && (
        <div className="mt-1 flex justify-between text-xs text-ink-3">
          <span>{Math.round(share * 100)}% выполнено</span>
          {expected != null && <span>ожидается {Math.round(expected * 100)}%</span>}
        </div>
      )}
    </div>
  );
}
