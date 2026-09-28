import Link from "next/link";
import { ArrowUpRight } from "lucide-react";
import { Alert, ExecutionBar, FlagCountPills, KpiTile, Note, Section, TargetBadge, execClass } from "./bits";
import { kg, money, monthShort, num, pct } from "@/lib/sales/format";
import type { KpiTiles, Period, UnitRow } from "@/lib/sales/types";

/** Шесть плиток KPI — одинаковые на всех уровнях. */
export function KpiRow({ kpi, period }: { kpi: KpiTiles; period: Period }) {
  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-6">
      <KpiTile label="Выполнение плана" value={pct(kpi.execution, 1)}>
        {kpi.planKg != null ? (
          <>
            {kg(kpi.factKg)} из {kg(kpi.planKg)} кг
            <br />
            прогноз {kg(kpi.forecastKg)} кг ({pct(kpi.forecastExecution)}) по темпу {period.workedDays} из {period.daysInMonth} дн.
          </>
        ) : (
          <>
            факт {kg(kpi.factKg)} кг · плана нет
            <br />
            прогноз {kg(kpi.forecastKg)} кг по темпу {period.workedDays} из {period.daysInMonth} дн.
          </>
        )}
      </KpiTile>
      <KpiTile label="Выручка" value={money(kpi.revenue)} unit="сум">
        АКБ {num(kpi.akb)}
      </KpiTile>
      <KpiTile label="Конверсия визита" value={pct(kpi.conversion.value, 1)} badge={<TargetBadge target={kpi.conversion} />}>
        цель {pct(kpi.conversion.target)}
      </KpiTile>
      <KpiTile label="Выручка на ТТ" value={money(kpi.revenuePerOutlet.value)} badge={<TargetBadge target={kpi.revenuePerOutlet} />}>
        цель {money(kpi.revenuePerOutlet.target)}
      </KpiTile>
      <KpiTile label="АКБ на агента" value={num(kpi.akbPerAgent.value, 1)} badge={<TargetBadge target={kpi.akbPerAgent} />}>
        цель {num(kpi.akbPerAgent.target)}
      </KpiTile>
      <KpiTile label="Визиты без заказа" value={num(kpi.visitsWithoutOrder)}>
        из {num(kpi.visitsDone)} визитов · {num(kpi.activeAgents)} ТП
      </KpiTile>
    </div>
  );
}

export function UnassignedWarning({ kgValue, share }: { kgValue: number; share: number | null }) {
  if (kgValue <= 0) return null;
  return (
    <Alert>
      <b>{kg(kgValue)} кг факта ({pct(share)})</b> не привязаны к агентам — в заказах Linko нет агента. Они входят в итог, но не в показатели ТП.
    </Alert>
  );
}

/** Карточка РМ / направления / региона. */
export function UnitCard({ unit, href }: { unit: UnitRow; href: string | null }) {
  const body = (
    <div className={`flex h-full flex-col rounded-xl border border-line bg-surface p-4 ${href ? "transition-colors group-hover:border-accent/40" : ""}`}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="font-semibold text-ink">{unit.name}</div>
          <div className="mt-0.5 text-xs text-ink-3">
            {unit.subtitle ? `${unit.subtitle} · ` : ""}
            {unit.regionCount > 0 ? `${num(unit.regionCount)} рег. · ` : ""}
            {num(unit.agents)} ТП
          </div>
        </div>
        {href && <ArrowUpRight className="size-4 shrink-0 text-ink-3 group-hover:text-accent-strong" />}
      </div>
      <div className="mt-3">
        <ExecutionBar value={unit.execution} tone={unit.execution != null && unit.execution < 0.7 ? "bad" : unit.execution != null && unit.execution < 0.9 ? "warn" : "accent"} />
      </div>
      <dl className="mt-3 grid grid-cols-3 gap-x-3 gap-y-2 text-xs">
        <Metric label="План, кг" value={kg(unit.planKg)} />
        <Metric label="Факт, кг" value={kg(unit.factKg)} />
        <Metric label="Вып." value={<span className={execClass(unit.execution)}>{pct(unit.execution)}</span>} />
        <Metric label="Прогноз, кг" value={kg(unit.forecastKg)} />
        <Metric label="Страйк" value={pct(unit.strike)} />
        <Metric label="Выручка" value={money(unit.revenue)} />
      </dl>
      {unit.regionNames.length > 1 && <p className="mt-3 line-clamp-2 text-xs text-ink-3">{unit.regionNames.join(", ")}</p>}
      <div className="mt-auto pt-3">
        <FlagCountPills flags={unit.flags} />
      </div>
    </div>
  );

  return href ? (
    <Link href={href} className="group block">
      {body}
    </Link>
  ) : (
    body
  );
}

function Metric({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div>
      <dt className="text-[11px] text-ink-3">{label}</dt>
      <dd className="mt-0.5 font-semibold tabular-nums text-ink">{value}</dd>
    </div>
  );
}

/** План и факт по месяцам — горизонтальные бары. */
export function PlanFactMonths({ months, current }: { months: { month: number; planKg: number | null; factKg: number | null }[]; current: number }) {
  const max = Math.max(1, ...months.flatMap((m) => [m.planKg ?? 0, m.factKg ?? 0]));
  return (
    <Section title="План и факт по месяцам" hint="кг">
      <div className="space-y-1.5">
        {months.filter((m) => m.month <= current || m.planKg != null).map((m) => (
          <div key={m.month} className="grid grid-cols-[36px_1fr_88px] items-center gap-3 text-xs" title={`План ${kg(m.planKg)} · Факт ${kg(m.factKg)}`}>
            <span className={m.month === current ? "font-semibold text-ink" : "text-ink-2"}>{monthShort(m.month)}</span>
            <div className="relative h-4">
              <div className="absolute inset-y-0 left-0 rounded bg-muted" style={{ width: `${((m.planKg ?? 0) / max) * 100}%` }} />
              <div className="absolute inset-y-1 left-0 rounded bg-accent" style={{ width: `${((m.factKg ?? 0) / max) * 100}%` }} />
            </div>
            <span className="text-right tabular-nums text-ink">{kg(m.factKg)}</span>
          </div>
        ))}
      </div>
      <div className="mt-3 flex gap-4 text-xs text-ink-3">
        <span className="flex items-center gap-1.5"><span className="size-2.5 rounded-sm bg-muted ring-1 ring-line" /> План, кг</span>
        <span className="flex items-center gap-1.5"><span className="size-2.5 rounded-sm bg-accent" /> Факт, кг</span>
      </div>
    </Section>
  );
}

/** Из чего складывается объём: доля выручки по категориям. */
export function CategoryShares({ categories }: { categories: { categoryId: number | null; name: string; revenue: number; share: number | null }[] }) {
  const top = categories.slice(0, 12);
  const rest = categories.slice(12);
  const restRevenue = rest.reduce((s, c) => s + c.revenue, 0);
  const restShare = rest.reduce((s, c) => s + (c.share ?? 0), 0);
  const max = Math.max(0.0001, ...top.map((c) => c.share ?? 0));

  return (
    <Section title="Из чего складывается объём" hint="выручка за месяц">
      <div className="space-y-1.5">
        {top.map((c) => (
          <div key={c.categoryId ?? "none"} className="grid grid-cols-[minmax(0,140px)_1fr_48px] items-center gap-3 text-xs" title={`${c.name}: ${money(c.revenue)}`}>
            <span className="truncate text-ink-2">{c.name}</span>
            <div className="h-2.5 rounded bg-muted">
              <div className="h-full rounded bg-accent" style={{ width: `${((c.share ?? 0) / max) * 100}%` }} />
            </div>
            <span className="text-right tabular-nums text-ink">{pct(c.share)}</span>
          </div>
        ))}
        {rest.length > 0 && (
          <div className="grid grid-cols-[minmax(0,140px)_1fr_48px] items-center gap-3 text-xs" title={money(restRevenue)}>
            <span className="text-ink-3">Остальные ({rest.length})</span>
            <div className="h-2.5 rounded bg-muted">
              <div className="h-full rounded bg-ink-3/50" style={{ width: `${(restShare / max) * 100}%` }} />
            </div>
            <span className="text-right tabular-nums text-ink-3">{pct(restShare)}</span>
          </div>
        )}
      </div>
      {categories.length === 0 && <p className="py-4 text-center text-sm text-ink-3">Продаж за период нет</p>}
    </Section>
  );
}

/** Выполнение плана по категориям (карточка агента). */
export function CategoryPlanBars({ rows }: { rows: { categoryId: number | null; name: string; planKg: number | null; factKg: number; revenue: number; execution: number | null }[] }) {
  const limit = 15;
  const shown = rows.slice(0, limit);
  const hidden = rows.slice(limit);
  const noPlans = rows.every((r) => r.planKg == null);

  return (
    <Section title="Выполнение плана по категориям" hint="наведите на полосу — план, факт и выручка">
      {noPlans && rows.length > 0 && (
        <p className="mb-3 rounded-lg bg-muted px-3 py-2 text-xs text-ink-2">
          Плана по категориям у агента нет — показан только факт. План можно загрузить в «Продажи → Настройки → Планы».
        </p>
      )}
      <div className="space-y-2">
        {shown.map((c) => (
          <div
            key={c.categoryId ?? "none"}
            className="grid grid-cols-[minmax(0,150px)_1fr_76px] items-center gap-3 text-xs"
            title={`План ${kg(c.planKg)} кг · Факт ${kg(c.factKg)} кг · Выручка ${money(c.revenue)}`}
          >
            <span className="truncate text-ink-2">{c.name}</span>
            <ExecutionBar value={c.execution} tone={c.execution != null && c.execution < 0.7 ? "warn" : "accent"} />
            <span className={`whitespace-nowrap text-right tabular-nums ${c.execution == null ? "text-ink-3" : "text-ink"}`}>
              {c.execution == null ? `${kg(c.factKg)} кг` : pct(c.execution)}
            </span>
          </div>
        ))}
      </div>
      {hidden.length > 0 && (
        <p className="mt-2 text-xs text-ink-3">
          Ещё {hidden.length} {hidden.length === 1 ? "категория" : hidden.length < 5 ? "категории" : "категорий"}:{" "}
          {hidden.map((c) => `${c.name} (${kg(c.factKg)} кг)`).join(", ")}
        </p>
      )}
      {rows.length === 0 && <p className="py-4 text-center text-sm text-ink-3">Нет ни плана, ни продаж по категориям</p>}
      <Note>
        Процент — факт агента к его собственному плану по категории. Штриховкой — категории без плана: их продажи есть, а плана на них не назначено.
      </Note>
    </Section>
  );
}
