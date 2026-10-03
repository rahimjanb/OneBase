import { Alert, ExecutionBar, FlagCountPills, KpiTile, Note, Section, Stat, SummaryCard, execClass, execTone, targetTone } from "./bits";
import { plural } from "@/lib/format";
import { delta, kg, money, monthLabel, monthShort, num, pct } from "@/lib/sales/format";
import type { CategoryPlanFact, DataQuality, KpiTiles, NextMonthPlan, Period, UnitRow } from "@/lib/sales/types";

/**
 * Шесть плиток KPI — одинаковые на всех уровнях (вторичка, направление, регион); на широком экране — в один ряд.
 * Значение окрашено по уровню цели, под ним одна строка пояснения; подробности (прогноз, ТП с планом) — во всплывающей подсказке.
 * План — сумма планов ТП из Linko.
 */
export function KpiRow({ kpi, period }: { kpi: KpiTiles; period: Period }) {
  const hasPlan = kpi.planKg != null;
  const pace = `по темпу ${period.workedDays} из ${period.daysInMonth} дн.`;
  const planDetails = hasPlan
    ? [
        `${num(kpi.planAgents)} ТП с планом`,
        `прогноз ${kg(kpi.planForecastKg)} кг (${pct(kpi.forecastExecution)}) ${pace}`,
        kpi.planFactKg != null && kpi.planFactKg !== kpi.factKg ? `всего продано ${kg(kpi.factKg)} кг` : null,
      ]
    : [`прогноз ${kg(kpi.forecastKg)} кг ${pace}`];

  return (
    // Порог в rem (87.5rem = 1400px): брейкпоинты Tailwind — в rem, и порог в px проигрывает lg по порядку правил.
    <div className="grid grid-cols-2 gap-3 lg:grid-cols-3 min-[87.5rem]:grid-cols-6">
      <KpiTile label="Выполнение плана" value={pct(kpi.execution, 1)} tone={hasPlan ? "accent" : "muted"} title={planDetails.filter(Boolean).join(" · ")}>
        {hasPlan ? `${kg(kpi.planFactKg)} из ${kg(kpi.planKg)} кг` : `факт ${kg(kpi.factKg)} кг · плана в Linko нет`}
      </KpiTile>
      <KpiTile
        label="Выручка"
        value={money(kpi.revenue)}
        title={
          kpi.revenuePlan
            ? `План по выручке: ${money(kpi.revenuePlan.fact)} из ${money(kpi.revenuePlan.plan)} (${num(kpi.revenuePlan.agents)} ТП с планом) · АКБ ${num(kpi.akb)}`
            : `АКБ ${num(kpi.akb)} · плана по выручке в Linko нет`
        }
      >
        {kpi.revenuePlan ? `сум · ${pct(kpi.revenuePlan.execution)} плана` : `сум · АКБ ${num(kpi.akb)}`}
      </KpiTile>
      <KpiTile label="Конверсия визита" value={pct(kpi.conversion.value)} tone={targetTone(kpi.conversion)} title={`${pct(kpi.conversion.ratio)} от цели`}>
        цель {pct(kpi.conversion.target)}
      </KpiTile>
      <KpiTile
        label="Выручка на ТТ"
        value={money(kpi.revenuePerOutlet.value)}
        tone={targetTone(kpi.revenuePerOutlet)}
        title={`цель ${money(kpi.revenuePerOutlet.target)} сум`}
      >
        сум · {pct(kpi.revenuePerOutlet.ratio)} от цели
      </KpiTile>
      <KpiTile label="АКБ на агента" value={num(kpi.akbPerAgent.value, 1)} tone={targetTone(kpi.akbPerAgent)} title={`${pct(kpi.akbPerAgent.ratio)} от цели`}>
        цель {num(kpi.akbPerAgent.target)}
      </KpiTile>
      <KpiTile label="Визиты без заказа" value={num(kpi.visitsWithoutOrder)} title={`${num(kpi.activeAgents)} ТП с визитами`}>
        из {num(kpi.visitsDone)} визитов
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

/**
 * Карточка республики, РМ / направления или региона: шкала выполнения, шесть итогов, флаги ТП.
 * showFlags=false — на «Вторичке», где флаги уже показаны полосой над карточками.
 */
export function UnitCard({ unit, href, showFlags = true }: { unit: UnitRow; href: string | null; showFlags?: boolean }) {
  const hasPlan = unit.planKg != null;
  const subtitle = [
    unit.subtitle,
    unit.kind !== "region" && unit.regionCount > 0 ? `${num(unit.regionCount)} ${plural(unit.regionCount, ["регион", "региона", "регионов"])}` : null,
    `${num(unit.agents)} ТП`,
  ]
    .filter(Boolean)
    .join(" · ");

  return (
    <SummaryCard title={unit.name} subtitle={subtitle} href={href}>
      <div
        className="mt-4"
        role="progressbar"
        aria-label="Выполнение плана"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={unit.execution == null ? undefined : Math.round(Math.min(1, unit.execution) * 100)}
      >
        <ExecutionBar value={unit.execution} tone="accent" />
      </div>
      <dl className="mt-4 grid grid-cols-2 gap-x-6 gap-y-3">
        <Stat label="План, кг" value={kg(unit.planKg)} />
        <Stat label={hasPlan ? "Прогноз" : "Прогноз, кг"} value={hasPlan ? pct(unit.forecastExecution) : kg(unit.forecastKg)} />
        <Stat label={hasPlan ? "Факт в плане" : "Факт, кг"} value={kg(hasPlan ? unit.planFactKg : unit.factKg)} />
        <Stat label="Страйк" value={pct(unit.strike)} />
        <Stat label="Выполнение" value={pct(unit.execution)} tone={execTone(unit.execution)} />
        <Stat label="Выручка" value={money(unit.revenue)} />
      </dl>
      {/* У направления — какие регионы в него входят; у республики список не нужен. */}
      {unit.kind === "direction" && unit.regionNames.length > 0 && <p className="mt-3 line-clamp-2 text-xs text-ink-3">{unit.regionNames.join(", ")}</p>}
      {showFlags && (
        <div className="mt-auto pt-4">
          <FlagCountPills flags={unit.flags} />
        </div>
      )}
    </SummaryCard>
  );
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
