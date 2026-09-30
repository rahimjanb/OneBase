import Link from "next/link";
import { ArrowUpRight } from "lucide-react";
import { Alert, ExecutionBar, FlagCountPills, KpiTile, Note, Section, TargetBadge, execClass } from "./bits";
import { delta, kg, money, monthLabel, monthShort, num, pct } from "@/lib/sales/format";
import type { CategoryPlanFact, DataQuality, ExcludedSummary, KpiTiles, NextMonthPlan, Period, UnitRow } from "@/lib/sales/types";

/** Плашка «X% плана»: ≥100% зелёная, 70–99% оранжевая, меньше — красная. */
export function PlanBadge({ share }: { share: number | null }) {
  if (share == null) return null;
  const tone = share >= 1 ? "bg-ok-soft text-ok" : share >= 0.7 ? "bg-warn-soft text-warn" : "bg-bad-soft text-bad";
  return <span className={`inline-flex rounded-full px-2 py-0.5 text-[11px] font-semibold ${tone}`}>{pct(share)} плана</span>;
}

/** Шесть плиток KPI — одинаковые на всех уровнях; на широком экране — в один ряд. План — сумма планов ТП из Linko. */
export function KpiRow({ kpi, period }: { kpi: KpiTiles; period: Period }) {
  return (
    // Порог в rem (87.5rem = 1400px): брейкпоинты Tailwind — в rem, и порог в px проигрывает lg по порядку правил.
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 min-[87.5rem]:grid-cols-6">
      <KpiTile label="Выполнение плана" value={pct(kpi.execution, 1)}>
        {kpi.planKg != null ? (
          <>
            {kg(kpi.planFactKg)} из {kg(kpi.planKg)} кг{kpi.planAgents > 0 && ` · ${num(kpi.planAgents)} ТП с планом`}
            <br />
            прогноз {kg(kpi.planForecastKg)} кг ({pct(kpi.forecastExecution)}) по темпу {period.workedDays} из {period.daysInMonth} дн.
            {kpi.planFactKg != null && kpi.planFactKg !== kpi.factKg && (
              <>
                <br />
                всего продано {kg(kpi.factKg)} кг
              </>
            )}
          </>
        ) : (
          <>
            факт {kg(kpi.factKg)} кг · плана в Linko нет
            <br />
            прогноз {kg(kpi.forecastKg)} кг по темпу {period.workedDays} из {period.daysInMonth} дн.
          </>
        )}
      </KpiTile>
      <KpiTile
        label="Выручка"
        value={money(kpi.revenue)}
        unit="сум"
        badge={kpi.revenuePlan && <PlanBadge share={kpi.revenuePlan.execution} />}
      >
        АКБ {num(kpi.akb)}
        {kpi.revenuePlan && (
          <>
            <br />
            план по выручке: {money(kpi.revenuePlan.fact)} из {money(kpi.revenuePlan.plan)} ({num(kpi.revenuePlan.agents)} ТП с планом)
            <br />
            прогноз {money(kpi.revenuePlan.forecast)} ({pct(kpi.revenuePlan.forecastExecution)})
          </>
        )}
      </KpiTile>
      <KpiTile
        label="Конверсия визита"
        value={pct(kpi.conversion.value, 1)}
        badge={<TargetBadge target={kpi.conversion} />}
        note={`цель ${pct(kpi.conversion.target)}`}
      />
      <KpiTile
        label="Выручка на ТТ"
        value={money(kpi.revenuePerOutlet.value)}
        unit="сум"
        badge={<TargetBadge target={kpi.revenuePerOutlet} />}
        note={`цель ${money(kpi.revenuePerOutlet.target)}`}
      />
      <KpiTile
        label="АКБ на агента"
        value={num(kpi.akbPerAgent.value, 1)}
        badge={<TargetBadge target={kpi.akbPerAgent} />}
        note={`цель ${num(kpi.akbPerAgent.target)}`}
      />
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
    <div className={`@container flex h-full flex-col rounded-xl border border-line bg-surface p-4 shadow-sm ${href ? "transition-colors group-hover:border-accent/40" : ""}`}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="font-semibold text-ink">{unit.name}</div>
          <div className="mt-0.5 text-xs text-ink-3">
            {unit.subtitle ? `${unit.subtitle} · ` : ""}
            {unit.kind !== "region" && unit.regionCount > 0 ? `${num(unit.regionCount)} рег. · ` : ""}
            {num(unit.agents)} ТП
          </div>
        </div>
        {href && <ArrowUpRight className="size-4 shrink-0 text-ink-3 group-hover:text-accent-strong" />}
      </div>
      <div className="mt-3">
        <ExecutionBar value={unit.execution} tone={unit.execution != null && unit.execution < 0.7 ? "bad" : unit.execution != null && unit.execution < 0.9 ? "warn" : "accent"} />
      </div>
      <dl className="mt-3 grid grid-cols-3 gap-x-3 gap-y-3 text-xs @sm:grid-cols-4">
        <Metric label="План, кг" value={kg(unit.planKg)} />
        {unit.planKg != null ? (
          <Metric label="Факт в плане, кг" value={kg(unit.planFactKg)} />
        ) : (
          <Metric label="Факт, кг" value={kg(unit.factKg)} />
        )}
        <Metric label="Вып." value={<span className={execClass(unit.execution)}>{pct(unit.execution)}</span>} />
        {unit.planKg != null ? (
          <Metric label="Прогноз вып." value={<span className={execClass(unit.forecastExecution)}>{pct(unit.forecastExecution)}</span>} />
        ) : (
          <Metric label="Прогноз, кг" value={kg(unit.forecastKg)} />
        )}
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

/** Плитка «Экспорт и опт»: филиал «Завод» из Linko — отдельно от вторички, чтобы не завышать республику. */
export function ExportCard({ data, href }: { data: ExcludedSummary; href: string }) {
  return (
    <Link href={href} className="group block">
      <div className="@container flex h-full flex-col rounded-xl border border-line bg-surface p-4 shadow-sm transition-colors group-hover:border-accent/40">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="font-semibold text-ink">Экспорт и опт</div>
            <div className="mt-0.5 text-xs text-ink-3">филиал «Завод» в Linko · не входит во вторичку</div>
          </div>
          <ArrowUpRight className="size-4 shrink-0 text-ink-3 group-hover:text-accent-strong" />
        </div>
        <dl className="mt-4 grid grid-cols-3 gap-x-3 gap-y-3 text-xs @sm:grid-cols-4">
          <Metric label="Факт, кг" value={kg(data.factKg)} />
          <Metric label="Выручка" value={money(data.revenue)} />
          <Metric label="АКБ" value={num(data.akb)} />
          <Metric label="Прогноз, кг" value={kg(data.forecastKg)} />
          <Metric label="Заказов" value={num(data.orders)} />
          <Metric label="К прошлому мес." value={<span className={deltaClass(data.vsPrevMonth)}>{delta(data.vsPrevMonth)}</span>} />
        </dl>
      </div>
    </Link>
  );
}

function deltaClass(value: number | null): string {
  if (value == null) return "text-ink-3";
  return value >= 0 ? "text-ok" : value > -0.1 ? "text-warn" : "text-bad";
}

/**
 * Качество данных: что не попало в факт или учтено иначе, чем выглядит в Linko.
 * Серьёзное (заказы без даты приёмки, возвраты без строк) — предупреждением, остальное — справкой.
 */
export function DataQualityNotes({ quality }: { quality: DataQuality }) {
  const serious = quality.deliveredWithoutAcceptance > 0 || quality.returnsWithoutLines > 0;
  const otherCurrency = quality.otherCurrency ?? [];
  const info = quality.zeroHeaderReturns > 0 || quality.uncategorized.length > 0 || quality.acceptedInFuture > 0 || otherCurrency.length > 0;
  if (!serious && !info) return null;

  return (
    <Section title="Качество данных" hint="что учтено иначе, чем выглядит в Linko">
      <div className="space-y-2 text-sm">
        {quality.deliveredWithoutAcceptance > 0 && (
          <Alert tone="bad">
            <b>{num(quality.deliveredWithoutAcceptance)}</b> доставленных заказов без даты приёмки — в факт не попали. Обычно это значит, что копия
            Linko загружена не до конца: запустите полную перезагрузку в «Настройки → Интеграции → Linko».
          </Alert>
        )}
        {quality.returnsWithoutLines > 0 && (
          <Alert>
            <b>{num(quality.returnsWithoutLines)}</b> возвратов без строк товара (по шапке {kg(quality.returnsWithoutLinesHeaderKg)} кг) — не вычтены:
            неизвестно, какой товар вернули. Проверьте эти документы в Linko.
          </Alert>
        )}
        {otherCurrency.map((c) => (
          <Alert key={c.currency} tone="info">
            Выручка в {c.currency}: <b>{num(c.amount)}</b> ({num(c.orders)} заказов) — курса в данных нет, поэтому с сумами не складывается; вес этих
            заказов в факт входит.
          </Alert>
        ))}
        {quality.acceptedInFuture > 0 && (
          <Alert tone="info">
            У <b>{num(quality.acceptedInFuture)}</b> доставленных заказов дата приёмки в Linko стоит в будущем. В факт они попадут, когда этот день наступит, —
            отчётный день не уезжает вперёд.
          </Alert>
        )}
        {quality.zeroHeaderReturns > 0 && (
          <Alert tone="info">
            <b>{num(quality.zeroHeaderReturns)}</b> возвратов с нулевым весом в шапке учтены по строкам: {kg(quality.zeroHeaderReturnsKg)} кг. Шапке
            документа верить нельзя — у них вес лежит только в строках.
          </Alert>
        )}
      </div>
      {quality.uncategorized.length > 0 && (
        <div className="mt-4">
          <div className="text-xs font-semibold uppercase tracking-wide text-ink-3">Вне категорий отчёта</div>
          <table className="mt-2 w-full text-sm">
            <tbody>
              {quality.uncategorized.map((u) => (
                <tr key={u.id} className="border-b border-line last:border-0">
                  <td className="py-1.5 text-ink">{u.name}</td>
                  <td className="py-1.5 text-right tabular-nums text-ink-2">{kg(u.kg)} кг</td>
                  <td className="py-1.5 text-right tabular-nums text-ink-2">{money(u.revenue)}</td>
                  <td className="py-1.5 text-right tabular-nums text-ink-3">{num(u.orders)} заказов</td>
                </tr>
              ))}
            </tbody>
          </table>
          <Note>Типы товаров Linko, которых нет в восьми категориях отчёта (импорт, бонус, оборудование…). В итог они входят, в карточки категорий — нет.</Note>
        </div>
      )}
    </Section>
  );
}

function Metric({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    // Значения ряда — на одной линии, даже если подпись переносится на две строки.
    <div className="flex min-w-0 flex-col justify-between">
      <dt className="text-[10px] font-medium uppercase leading-tight tracking-[0.08em] text-ink-3">{label}</dt>
      <dd className="mt-1 whitespace-nowrap text-sm font-semibold tabular-nums text-ink">{value}</dd>
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

const indicatorUnit = (type: string) =>
  type === "product_sales_weight" ? "кг" : type === "active_client_count" ? "ТТ" : "сум";

const indicatorValue = (type: string, v: number) =>
  type === "product_sales_weight" ? kg(v) : type === "active_client_count" ? num(v) : money(v);

/** KPI-показатели агента из Linko: план и факт так, как их считает Linko (кг по группам товаров, АКБ, суммы). */
export function IndicatorBars({ rows }: { rows: { indicatorId: number; name: string; planType: string; plan: number; fact: number; execution: number | null }[] }) {
  if (rows.length === 0) return null;
  return (
    <Section title="Планы Linko" hint="KPI-показатели агента — план и факт по расчёту Linko">
      <div className="space-y-2.5">
        {rows.map((r) => (
          <div
            key={r.indicatorId}
            className="grid grid-cols-1 items-center gap-x-4 gap-y-1 text-xs sm:grid-cols-[minmax(0,280px)_1fr_minmax(0,220px)]"
            title={`План ${indicatorValue(r.planType, r.plan)} ${indicatorUnit(r.planType)} · факт ${indicatorValue(r.planType, r.fact)} ${indicatorUnit(r.planType)}`}
          >
            <span className="truncate text-ink-2">{r.name}</span>
            <ExecutionBar value={r.execution} tone={r.execution != null && r.execution < 0.7 ? (r.execution < 0.4 ? "bad" : "warn") : "accent"} />
            <span className="text-right tabular-nums text-ink">
              {indicatorValue(r.planType, r.fact)} из {indicatorValue(r.planType, r.plan)} {indicatorUnit(r.planType)}
              <span className={`ml-2 font-semibold ${execClass(r.execution)}`}>{pct(r.execution)}</span>
            </span>
          </div>
        ))}
      </div>
      <Note>
        Планы загружаются из Linko автоматически после каждого пересчёта. План ТП в плитке — сумма весовых показателей (кг). Факт здесь —
        по расчёту Linko, он может немного отличаться от факта OneBase из заказов.
      </Note>
    </Section>
  );
}

/** Выполнение плана по категориям (карточка агента): план — весовые показатели ТП в Linko; «плана нет» — категория продаётся без плана. */
export function CategoryPlanBars({ rows }: { rows: CategoryPlanFact[] }) {
  const limit = 15;
  const shown = rows.slice(0, limit);
  const hidden = rows.slice(limit);
  const noPlans = rows.every((r) => r.planKg == null);

  return (
    <Section title="Выполнение плана по категориям" hint="наведите на полосу — план, факт и выручка">
      {noPlans && rows.length > 0 && (
        <p className="mb-3 rounded-lg bg-muted px-3 py-2 text-xs text-ink-2">У агента в Linko нет плана по категориям — показан только факт.</p>
      )}
      <div className="space-y-2">
        {shown.map((c) => (
          <div
            key={`${c.categoryId ?? c.name}`}
            className={`grid grid-cols-[minmax(0,170px)_1fr_96px] items-center gap-3 text-xs ${c.planKg == null ? "opacity-60" : ""}`}
            title={`План ${kg(c.planKg)} кг · Факт ${kg(c.factKg)} кг · Выручка ${money(c.revenue)}`}
          >
            <span className="truncate text-ink-2">{c.name}</span>
            {c.planKg == null ? (
              <span className="text-ink-3">плана нет</span>
            ) : (
              <ExecutionBar value={c.execution} tone={c.execution != null && c.execution < 0.7 ? "warn" : "accent"} />
            )}
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
        План — весовые показатели ТП в Linko по категориям («Могуль + Шоколад» — одной строкой, как заведено в Linko). Процент — факт агента к его
        собственному плану. Серые строки «плана нет» — категория продаётся, а плана на неё не назначено.
      </Note>
    </Section>
  );
}

/** План и факт по категориям подразделения: сумма планов его ТП в Linko. */
export function CategoryPlanTable({ rows }: { rows: CategoryPlanFact[] }) {
  if (rows.length === 0) return null;
  const planned = rows.filter((r) => r.planKg != null);
  const plan = planned.reduce((s, r) => s + (r.planKg ?? 0), 0);
  const fact = planned.reduce((s, r) => s + r.factKg, 0);
  const th = "py-2 pr-3 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  return (
    <Section title="План и факт по категориям" hint="план — сумма планов ТП по показателям Linko, в кг">
      <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
        <table className="w-full min-w-max text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className={`${th} text-left`}>Категория</th>
              <th className={th}>План</th>
              <th className={th} title="Факт ТП, у которых есть этот план">Факт ТП с планом</th>
              <th className={th}>Выполнение</th>
              <th className={`${th} w-32`} />
              <th className={th}>Осталось</th>
              <th className={th} title="Весь факт подразделения по этим категориям">Весь факт</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={`${r.categoryId ?? r.name}`} className={`border-b border-line last:border-0 ${r.planKg == null ? "text-ink-3" : ""}`}>
                <td className={`py-2 pr-3 ${r.planKg == null ? "" : "font-medium text-ink"}`}>{r.name}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{r.planKg == null ? "плана нет" : kg(r.planKg)}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{r.planKg == null ? "—" : kg(r.factKg)}</td>
                <td className={`py-2 pr-3 text-right tabular-nums ${execClass(r.execution)}`}>{pct(r.execution)}</td>
                <td className="py-2 pr-3">{r.planKg != null && <ExecutionBar value={r.execution} tone={r.execution != null && r.execution < 0.7 ? "warn" : "accent"} />}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{r.planKg == null ? "—" : kg(Math.max(0, r.planKg - r.factKg))}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{kg(r.scopeFactKg ?? r.factKg)}</td>
              </tr>
            ))}
          </tbody>
          {planned.length > 0 && (
            <tfoot>
              <tr className="bg-muted/60 font-semibold">
                <td className="py-2 pr-3">Итого с планом</td>
                <td className="py-2 pr-3 text-right tabular-nums">{kg(plan)}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{kg(fact)}</td>
                <td className={`py-2 pr-3 text-right tabular-nums ${execClass(plan ? fact / plan : null)}`}>{pct(plan ? fact / plan : null)}</td>
                <td />
                <td className="py-2 pr-3 text-right tabular-nums">{kg(Math.max(0, plan - fact))}</td>
                <td />
              </tr>
            </tfoot>
          )}
        </table>
      </div>
      <Note>
        Показатели плана в Linko заведены по цехам: «Могуль + Шоколад», «Трубочка + Печенье» — одной строкой, так они и показаны. Факт — только ТП с
        этим планом (как в плитке выполнения), весь факт — справа. Серые строки — категории с продажами, на которые плана нет.
      </Note>
    </Section>
  );
}

/** «План на следующий месяц» — появляется, когда в Linko заведены планы ТП на него. Плашка — разница с текущим месяцем. */
export function NextMonthCard({ plan, rowsTitle }: { plan: NextMonthPlan; rowsTitle: string }) {
  const diff = plan.currentPlanKg ? plan.planKg / plan.currentPlanKg - 1 : null;
  return (
    <Section title={`План на ${monthLabel(plan.year, plan.month).toLowerCase()}`} hint={`из Linko · ${num(plan.agents)} ТП с планом`}>
      <div className="flex flex-wrap items-baseline gap-3">
        <span className="text-[28px] font-semibold tabular-nums text-ink">{kg(plan.planKg)}</span>
        <span className="text-sm text-ink-3">кг</span>
        {diff != null && (
          <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${diff >= 0 ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>
            {delta(diff)} к текущему месяцу
          </span>
        )}
      </div>
      <div className="-mx-4 mt-3 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
        <table className="w-full min-w-max text-sm">
          <thead>
            <tr className="border-b border-line text-[11px] uppercase tracking-wide text-ink-3">
              <th className="py-2 pr-3 text-left font-semibold">{rowsTitle}</th>
              <th className="py-2 pr-3 text-right font-semibold">План</th>
              <th className="py-2 pr-3 text-right font-semibold">Текущий месяц</th>
              <th className="py-2 text-right font-semibold">Разница</th>
            </tr>
          </thead>
          <tbody>
            {plan.rows.map((r) => {
              const d = r.currentPlanKg ? r.planKg / r.currentPlanKg - 1 : null;
              return (
                <tr key={r.id} className="border-b border-line last:border-0">
                  <td className="py-1.5 pr-3 text-ink">{r.name}</td>
                  <td className="py-1.5 pr-3 text-right tabular-nums">{kg(r.planKg)}</td>
                  <td className="py-1.5 pr-3 text-right tabular-nums text-ink-2">{r.currentPlanKg == null ? "—" : kg(r.currentPlanKg)}</td>
                  <td className={`py-1.5 text-right tabular-nums ${d == null ? "text-ink-3" : d >= 0 ? "text-ok" : "text-bad"}`}>{d == null ? "новый" : delta(d)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </Section>
  );
}
