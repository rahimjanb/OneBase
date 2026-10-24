"use client";

// Клиентский модуль: подписи и подсказки (title) собираются из чисел — их переводит useT(). Все пропсы — данные с сервера.
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { Alert, ExecutionBar, FlagCountPills, KpiTile, Note, Section, Stat, SummaryCard, levelBarTone, levelClass, levelTone, targetTone } from "./bits";
import { plural } from "@/lib/format";
import { delta, kg, money, monthLabel, monthShort, num, ordersLabel, paceDays, pct, planMissingText } from "@/lib/sales/format";
import { planOf } from "@/lib/sales/query";
import type { CategoryPlanFact, DataQuality, IndicatorPlan, KpiTiles, NextMonthPlan, Period, PlanKind, PlanSource, UnitRow } from "@/lib/sales/types";

/** Откуда план — для подписей: план РОП, план «Завод» или (планов регионов на месяц нет) сумма планов ТП из Linko. Переводятся при выводе. */
export const planSourceText: Record<PlanSource, string> = {
  rop: "план РОП",
  factory: "план «Завод»",
  linko: "сумма планов ТП из Linko",
};

const planTileLabel: Record<PlanSource, string> = {
  rop: "Выполнение плана РОП",
  factory: "Выполнение плана «Завод»",
  linko: "Выполнение плана",
};

const capitalize = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);

/**
 * Шесть плиток KPI — одинаковые на всех уровнях (вторичка, направление, регион); на широком экране — в один ряд.
 * Значение окрашено по уровню цели, под ним одна строка пояснения; подробности (прогноз, источник плана) — во всплывающей подсказке.
 * План — РОП или «Завод» (переключатель в полосе раздела); если выбранного плана на месяц нет — сумма планов ТП из Linko, и плитка пишет,
 * какого плана нет. Прогноз есть только у идущего месяца (у закрытого сервер его не отдаёт).
 */
export function KpiRow({ kpi, period }: { kpi: KpiTiles; period: Period }) {
  const t = useT();
  const hasPlan = kpi.planKg != null;
  const fromLinko = kpi.planSource === "linko";
  const missing = planMissingText(planOf(period.plan), t); // «плана РОП на этот месяц нет» / «плана «Завод» на этот месяц нет»
  const source = t(planSourceText[kpi.planSource]);
  const pace = t("по темпу {worked} из {days} дн.", { worked: paceDays(period), days: period.daysInMonth });
  const planDetails = hasPlan
    ? [
        fromLinko
          ? t("{missing} — план ТП из Linko: сумма планов {agents} ТП, факт — их продажи", { missing, agents: num(kpi.planAgents) })
          : t("{source}: весь факт к плану (регион без своего плана добавляет факт, но не план)", { source }),
        kpi.planForecastKg != null ? t("прогноз {kg} кг ({pct}) {pace}", { kg: kg(kpi.planForecastKg), pct: pct(kpi.forecastExecution), pace }) : null,
        kpi.planFactKg != null && kpi.planFactKg !== kpi.factKg ? t("всего продано {kg} кг", { kg: kg(kpi.factKg) }) : null,
      ]
    : [
        fromLinko ? t("{missing}, планов ТП в Linko — тоже", { missing }) : t("{source} на этот охват не заведён", { source }),
        kpi.forecastKg != null ? t("прогноз {kg} кг {pace}", { kg: kg(kpi.forecastKg), pace }) : t("факт {kg} кг", { kg: kg(kpi.factKg) }),
      ];

  return (
    // Порог в rem (87.5rem = 1400px): брейкпоинты Tailwind — в rem, и порог в px проигрывает lg по порядку правил.
    <div className="grid grid-cols-2 gap-3 lg:grid-cols-3 min-[87.5rem]:grid-cols-6">
      <KpiTile label={t(planTileLabel[kpi.planSource])} value={pct(kpi.execution, 1)} tone={hasPlan ? "accent" : "muted"} title={planDetails.filter(Boolean).join(" · ")}>
        {hasPlan
          ? t("{fact} из {plan} кг", { fact: kg(kpi.planFactKg), plan: kg(kpi.planKg) })
          : t("факт {kg} кг · плана нет", { kg: kg(kpi.factKg) })}
        {fromLinko && <span className="block">{hasPlan ? t("{missing} — план ТП из Linko", { missing }) : missing}</span>}
      </KpiTile>
      <KpiTile
        label={t("Выручка")}
        value={money(kpi.revenue, t)}
        title={
          kpi.revenuePlan
            ? t("План по выручке: {fact} из {plan} ({agents} ТП с планом) · АКБ {akb}", {
                fact: money(kpi.revenuePlan.fact, t),
                plan: money(kpi.revenuePlan.plan, t),
                agents: num(kpi.revenuePlan.agents),
                akb: num(kpi.akb),
              })
            : t("АКБ {akb} · плана по выручке в Linko нет", { akb: num(kpi.akb) })
        }
      >
        {kpi.revenuePlan ? t("сум · {pct} плана", { pct: pct(kpi.revenuePlan.execution) }) : t("сум · АКБ {akb}", { akb: num(kpi.akb) })}
      </KpiTile>
      <KpiTile
        label={t("Конверсия визита")}
        value={pct(kpi.conversion.value)}
        tone={targetTone(kpi.conversion)}
        title={t("{pct} от цели · заказы ТП, принятые в месяце ÷ их выполненные визиты", { pct: pct(kpi.conversion.ratio) })}
      >
        {t("цель {value}", { value: pct(kpi.conversion.target) })}
      </KpiTile>
      <KpiTile
        label={t("Выручка на ТТ")}
        value={money(kpi.revenuePerOutlet.value, t)}
        tone={targetTone(kpi.revenuePerOutlet)}
        title={t("цель {value} сум", { value: money(kpi.revenuePerOutlet.target, t) })}
      >
        {t("сум · {pct} от цели", { pct: pct(kpi.revenuePerOutlet.ratio) })}
      </KpiTile>
      <KpiTile
        label={t("АКБ на агента")}
        value={num(kpi.akbPerAgent.value, 1)}
        tone={targetTone(kpi.akbPerAgent)}
        title={t("{pct} от цели", { pct: pct(kpi.akbPerAgent.ratio) })}
      >
        {t("цель {value}", { value: num(kpi.akbPerAgent.target) })}
      </KpiTile>
      <KpiTile
        label={t("Визиты без заказа")}
        value={num(kpi.visitsWithoutOrder)}
        title={
          t("Визиты ТП минус их заказы · {agents} ТП в месяце", { agents: num(kpi.activeAgents) }) +
          (kpi.visitsOutsideTeam > 0
            ? t(" · ещё {n} визитов не ТП (операторы, супервайзеры, вне справочника) — в конверсию не входят", { n: num(kpi.visitsOutsideTeam) })
            : "")
        }
      >
        {t("из {n} визитов ТП", { n: num(kpi.visitsDone) })}
        {kpi.visitsOutsideTeam > 0 && <> · {t("не ТП {n}", { n: num(kpi.visitsOutsideTeam) })}</>}
      </KpiTile>
    </div>
  );
}

export function UnassignedWarning({ kgValue, share }: { kgValue: number; share: number | null }) {
  const t = useT();
  if (kgValue <= 0) return null;
  return (
    <Alert>
      <b>{t("{kg} кг факта ({pct})", { kg: kg(kgValue), pct: pct(share) })}</b>{" "}
      {t("не привязаны к агентам — в заказах Linko нет агента. Они входят в итог, но не в показатели ТП.")}
    </Alert>
  );
}

/**
 * Карточка республики, РМ / направления или региона: шкала выполнения, шесть итогов, флаги ТП.
 * showFlags=false — на «Вторичке», где флаги уже показаны полосой над карточками.
 */
export function UnitCard({ unit, href, showFlags = true }: { unit: UnitRow; href: string | null; showFlags?: boolean }) {
  const t = useT();
  const hasPlan = unit.planKg != null;
  const subtitle = [
    unit.subtitle,
    unit.kind !== "region" && unit.regionCount > 0 ? `${num(unit.regionCount)} ${t(plural(unit.regionCount, ["регион", "региона", "регионов"]))}` : null,
    t("{n} ТП", { n: num(unit.agents) }),
  ]
    .filter(Boolean)
    .join(" · ");

  return (
    <SummaryCard title={unit.name} subtitle={subtitle} href={href}>
      <div
        className="mt-4"
        role="progressbar"
        aria-label={t("Выполнение плана")}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={unit.execution == null ? undefined : Math.round(Math.min(1, unit.execution) * 100)}
      >
        <ExecutionBar value={unit.execution} tone="accent" />
      </div>
      <dl className="mt-4 grid grid-cols-2 gap-x-6 gap-y-3">
        <Stat label={t("План, кг")} value={kg(unit.planKg)} />
        <Stat label={hasPlan ? t("Прогноз") : t("Прогноз, кг")} value={hasPlan ? pct(unit.forecastExecution) : kg(unit.forecastKg)} />
        <Stat label={hasPlan ? t("Факт в плане") : t("Факт, кг")} value={kg(hasPlan ? unit.planFactKg : unit.factKg)} />
        <Stat label={t("Страйк")} value={pct(unit.strike)} />
        <Stat label={t("Выполнение")} value={pct(unit.execution)} tone={levelTone(unit.executionLevel)} />
        <Stat label={t("Выручка")} value={money(unit.revenue, t)} />
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
 * «Приняты после отчётного дня» — только у идущего месяца: у закрытого их нет (сервер отдаёт 0, и здесь не показываем).
 */
/** republicWide — страница региона или направления: проверки качества считаются по всей выгрузке, это сказано в подписи. */
export function DataQualityNotes({ quality, period, republicWide = false }: { quality: DataQuality; period: Period; republicWide?: boolean }) {
  const t = useT();
  const serious = quality.deliveredWithoutAcceptance > 0 || quality.returnsWithoutLines > 0;
  const otherCurrency = quality.otherCurrency ?? [];
  const pending = period.closed ? 0 : quality.acceptedInFuture;
  const info = quality.zeroHeaderReturns > 0 || quality.uncategorized.length > 0 || pending > 0 || otherCurrency.length > 0;
  if (!serious && !info) return null;

  const missing = quality.deliveredWithoutAcceptance;
  return (
    <Section title={t("Качество данных")} hint={t(republicWide ? "что учтено иначе, чем выглядит в Linko — по всей республике" : "что учтено иначе, чем выглядит в Linko")}>
      <div className="space-y-2 text-sm">
        {missing > 0 && (
          <Alert tone="bad">
            <b>{num(missing)}</b>{" "}
            {t(
              plural(missing, [
                "доставленный заказ без даты приёмки — в факт не попал. Обычно это значит, что копия Linko загружена не до конца: запустите полную перезагрузку в «Настройки → Интеграции → Linko».",
                "доставленных заказа без даты приёмки — в факт не попали. Обычно это значит, что копия Linko загружена не до конца: запустите полную перезагрузку в «Настройки → Интеграции → Linko».",
                "доставленных заказов без даты приёмки — в факт не попали. Обычно это значит, что копия Linko загружена не до конца: запустите полную перезагрузку в «Настройки → Интеграции → Linko».",
              ]),
            )}
          </Alert>
        )}
        {quality.returnsWithoutLines > 0 && (
          <Alert>
            <b>{num(quality.returnsWithoutLines)}</b>{" "}
            {t("возвратов без строк товара (по шапке {kg} кг) — не вычтены: неизвестно, какой товар вернули. Проверьте эти документы в Linko.", {
              kg: kg(quality.returnsWithoutLinesHeaderKg),
            })}
          </Alert>
        )}
        {otherCurrency.map((c) => (
          <Alert key={c.currency} tone="info">
            {t("Выручка в {currency}:", { currency: c.currency })} <b>{num(c.amount)}</b>{" "}
            {t("({orders}) — курса в данных нет, поэтому с сумами не складывается; вес этих заказов в факт входит.", { orders: ordersLabel(c.orders, t) })}
          </Alert>
        ))}
        {pending > 0 && (
          <Alert tone="info">
            <b>{num(pending)}</b>{" "}
            {t(
              plural(pending, [
                "проданный заказ этого месяца принят после отчётного дня — с датой приёмки в будущем: в факт он попадёт в день приёмки.",
                "проданных заказа этого месяца приняты после отчётного дня — с датой приёмки в будущем: в факт они попадут в день приёмки.",
                "проданных заказов этого месяца приняты после отчётного дня — с датой приёмки в будущем: в факт они попадут в день приёмки.",
              ]),
            )}
          </Alert>
        )}
        {quality.zeroHeaderReturns > 0 && (
          <Alert tone="info">
            <b>{num(quality.zeroHeaderReturns)}</b>{" "}
            {t("возвратов с нулевым весом в шапке учтены по строкам: {kg} кг. Шапке документа верить нельзя — у них вес лежит только в строках.", {
              kg: kg(quality.zeroHeaderReturnsKg),
            })}
          </Alert>
        )}
      </div>
      {quality.uncategorized.length > 0 && (
        <div className="mt-4">
          <div className="text-xs font-semibold uppercase tracking-wide text-ink-3">{t("Вне категорий отчёта")}</div>
          <table className="mt-2 w-full text-sm">
            <tbody>
              {quality.uncategorized.map((u) => (
                <tr key={u.id} className="border-b border-line last:border-0">
                  <td className="py-1.5 text-ink">{u.name}</td>
                  <td className="py-1.5 text-right tabular-nums text-ink-2">{t("{kg} кг", { kg: kg(u.kg) })}</td>
                  <td className="py-1.5 text-right tabular-nums text-ink-2">{money(u.revenue, t)}</td>
                  <td className="py-1.5 text-right tabular-nums text-ink-3">{ordersLabel(u.orders, t)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <Note>{t("Типы товаров Linko, которых нет в категориях отчёта (бонус, оборудование…). В итог они входят, в карточки категорий — нет.")}</Note>
        </div>
      )}
    </Section>
  );
}

/**
 * План и факт по месяцам — горизонтальные бары. План месяца — выбранный план регионов (РОП или «Завод»); месяц без него —
 * сумма планов ТП из Linko (источник у каждого месяца — с сервера).
 */
export function PlanFactMonths({
  months,
  current,
}: {
  months: { month: number; planKg: number | null; factKg: number | null; planSource: PlanSource | null }[];
  current: number;
}) {
  const t = useT();
  const max = Math.max(1, ...months.flatMap((m) => [m.planKg ?? 0, m.factKg ?? 0]));
  // План регионов — первым, планы ТП из Linko — там, где его нет.
  const sources = [...new Set(months.map((m) => m.planSource).filter((x): x is PlanSource => x != null))].sort(
    (a, b) => Number(a === "linko") - Number(b === "linko"),
  );
  const hint = sources.length === 0 ? t("кг · плана нет") : `${t("кг")} · ${sources.map((x) => t(planSourceText[x])).join(t(", где его нет — "))}`;
  return (
    <Section title={t("План и факт по месяцам")} hint={hint}>
      <div className="space-y-1.5">
        {months.filter((m) => m.month <= current || m.planKg != null).map((m) => (
          <div
            key={m.month}
            className="grid grid-cols-[36px_1fr_88px] items-center gap-3 text-xs"
            title={`${m.planSource ? `${t(planSourceText[m.planSource])}: ${kg(m.planKg)}` : t("Плана нет")} · ${t("Факт {kg}", { kg: kg(m.factKg) })}`}
          >
            <span className={m.month === current ? "font-semibold text-ink" : "text-ink-2"}>{monthShort(m.month, t)}</span>
            <div className="relative h-4">
              <div className="absolute inset-y-0 left-0 rounded bg-muted" style={{ width: `${((m.planKg ?? 0) / max) * 100}%` }} />
              <div className="absolute inset-y-1 left-0 rounded bg-accent" style={{ width: `${((m.factKg ?? 0) / max) * 100}%` }} />
            </div>
            <span className="text-right tabular-nums text-ink">{kg(m.factKg)}</span>
          </div>
        ))}
      </div>
      <div className="mt-3 flex gap-4 text-xs text-ink-3">
        <span className="flex items-center gap-1.5"><span className="size-2.5 rounded-sm bg-muted ring-1 ring-line" /> {t("План, кг")}</span>
        <span className="flex items-center gap-1.5"><span className="size-2.5 rounded-sm bg-accent" /> {t("Факт, кг")}</span>
      </div>
    </Section>
  );
}

const indicatorUnit = (type: string, t: T) =>
  t(type === "product_sales_weight" ? "кг" : type === "active_client_count" ? "ТТ" : "сум");

const indicatorValue = (type: string, v: number, t: T) =>
  type === "product_sales_weight" ? kg(v) : type === "active_client_count" ? num(v) : money(v, t);

/** KPI-показатели агента из Linko: план и факт так, как их считает Linko (кг по группам товаров, АКБ, суммы); цвет выполнения — с сервера. */
export function IndicatorBars({ rows }: { rows: IndicatorPlan[] }) {
  const t = useT();
  if (rows.length === 0) return null;
  return (
    <Section title={t("Планы Linko")} hint={t("KPI-показатели агента — план и факт по расчёту Linko")}>
      <div className="space-y-2.5">
        {rows.map((r) => (
          <div
            key={r.indicatorId}
            className="grid grid-cols-1 items-center gap-x-4 gap-y-1 text-xs sm:grid-cols-[minmax(0,280px)_1fr_minmax(0,220px)]"
            title={t("План {plan} {unit} · факт {fact} {unit}", {
              plan: indicatorValue(r.planType, r.plan, t),
              fact: indicatorValue(r.planType, r.fact, t),
              unit: indicatorUnit(r.planType, t),
            })}
          >
            <span className="truncate text-ink-2">{r.name}</span>
            <ExecutionBar value={r.execution} tone={levelBarTone(r.executionLevel)} />
            <span className="text-right tabular-nums text-ink">
              {t("{fact} из {plan} {unit}", {
                fact: indicatorValue(r.planType, r.fact, t),
                plan: indicatorValue(r.planType, r.plan, t),
                unit: indicatorUnit(r.planType, t),
              })}
              <span className={`ml-2 font-semibold ${levelClass(r.executionLevel)}`}>{pct(r.execution)}</span>
            </span>
          </div>
        ))}
      </div>
      <Note>
        {t(
          "Планы загружаются из Linko автоматически после каждого пересчёта. План ТП в плитке — сумма весовых показателей (кг). Факт здесь — по расчёту Linko, он может немного отличаться от факта OneBase из заказов.",
        )}
      </Note>
    </Section>
  );
}

/** Выполнение плана по категориям (карточка агента): план — весовые показатели ТП в Linko; «плана нет» — категория продаётся без плана. */
export function CategoryPlanBars({ rows }: { rows: CategoryPlanFact[] }) {
  const t = useT();
  const limit = 15;
  const shown = rows.slice(0, limit);
  const hidden = rows.slice(limit);
  const noPlans = rows.every((r) => r.planKg == null);

  return (
    <Section title={t("Выполнение плана по категориям")} hint={t("наведите на полосу — план, факт и выручка")}>
      {noPlans && rows.length > 0 && (
        <p className="mb-3 rounded-lg bg-muted px-3 py-2 text-xs text-ink-2">{t("У агента в Linko нет плана по категориям — показан только факт.")}</p>
      )}
      <div className="space-y-2">
        {shown.map((c) => (
          <div
            key={`${c.categoryId ?? c.name}`}
            className={`grid grid-cols-[minmax(0,170px)_1fr_96px] items-center gap-3 text-xs ${c.planKg == null ? "opacity-60" : ""}`}
            title={t("План {plan} кг · Факт {fact} кг · Выручка {revenue}", { plan: kg(c.planKg), fact: kg(c.factKg), revenue: money(c.revenue, t) })}
          >
            <span className="truncate text-ink-2">{t(c.name)}</span>
            {c.planKg == null ? (
              <span className="text-ink-3">{t("плана нет")}</span>
            ) : (
              <ExecutionBar value={c.execution} tone={levelBarTone(c.executionLevel)} />
            )}
            <span className={`whitespace-nowrap text-right tabular-nums ${c.execution == null ? "text-ink-3" : "text-ink"}`}>
              {c.execution == null ? t("{kg} кг", { kg: kg(c.factKg) }) : pct(c.execution)}
            </span>
          </div>
        ))}
      </div>
      {hidden.length > 0 && (
        <p className="mt-2 text-xs text-ink-3">
          {t("Ещё {n} {word}:", { n: hidden.length, word: t(hidden.length === 1 ? "категория" : hidden.length < 5 ? "категории" : "категорий") })}{" "}
          {hidden.map((c) => `${t(c.name)} (${t("{kg} кг", { kg: kg(c.factKg) })})`).join(", ")}
        </p>
      )}
      {rows.length === 0 && <p className="py-4 text-center text-sm text-ink-3">{t("Нет ни плана, ни продаж по категориям")}</p>}
      <Note>
        {t(
          "План — весовые показатели ТП в Linko по категориям («Могуль + Шоколад» — одной строкой, как заведено в Linko). Процент — факт агента к его собственному плану. Серые строки «плана нет» — категория продаётся, а плана на неё не назначено.",
        )}
      </Note>
    </Section>
  );
}

/**
 * План и факт по категориям подразделения. План РОП / «Завод» — сумма планов «регион × категория» его регионов, факт — все продажи
 * категории; без выбранного плана регионов на месяц (plan — какой выбран) — сумма планов ТП по показателям Linko и факт этих ТП.
 * Выполнение, «осталось», прогноз к плану (с цветом) и итог считает сервер — здесь только вывод.
 */
export function CategoryPlanTable({
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
  const t = useT();
  if (rows.length === 0) return null;
  return (
    <Section title={t("План и факт по категориям")} hint={categoryPlanHint(source, t)}>
      <CategoryPlanRows rows={rows} total={total} source={source} plan={plan} />
    </Section>
  );
}

/** Подпись к «План и факт по категориям»: откуда план. t — перевод (useT()). */
export const categoryPlanHint = (source: PlanSource, t: T) =>
  source === "linko"
    ? t("план — сумма планов ТП по показателям Linko, в кг")
    : t("{source} — сумма планов «регион × категория», в кг", { source: t(planSourceText[source]) });

/** Таблица «План и факт по категориям» с пояснением — без секции (её ставит CategoryPlanTable или CategoryPlanCard с графиком). */
export function CategoryPlanRows({
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
  const t = useT();
  const fromLinko = source === "linko";
  const th = "py-2 pr-3 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  return (
    <>
      <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
        <table className="w-full min-w-max text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className={`${th} text-left`}>{t("Категория")}</th>
              <th className={th}>{t("План")}</th>
              <th className={th} title={fromLinko ? t("Факт ТП, у которых есть этот план") : t("Все продажи категории в подразделении")}>
                {fromLinko ? t("Факт ТП с планом") : t("Факт")}
              </th>
              <th className={th}>{t("Выполнение")}</th>
              <th className={`${th} w-32`} />
              <th className={th} title={t("Прогноз факта на конец месяца к плану: от 100% — зелёный, от 90% — оранжевый, ниже — красный; у закрытого месяца прогноза нет")}>
                {t("Прогноз к плану")}
              </th>
              <th className={th}>{t("Осталось")}</th>
              {fromLinko && (
                <th className={th} title={t("Весь факт подразделения по этим категориям")}>
                  {t("Весь факт")}
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={`${r.categoryId ?? r.name}`} className={`border-b border-line last:border-0 ${r.planKg == null ? "text-ink-3" : ""}`}>
                <td className={`py-2 pr-3 ${r.planKg == null ? "" : "font-medium text-ink"}`}>{t(r.name)}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{r.planKg == null ? t("плана нет") : kg(r.planKg)}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{r.planKg == null && fromLinko ? "—" : kg(r.factKg)}</td>
                <td className={`py-2 pr-3 text-right tabular-nums ${levelClass(r.executionLevel)}`}>{pct(r.execution)}</td>
                <td className="py-2 pr-3">{r.planKg != null && <ExecutionBar value={r.execution} tone={levelBarTone(r.executionLevel)} />}</td>
                <td className={`py-2 pr-3 text-right tabular-nums ${levelClass(r.forecastLevel)}`}>{pct(r.forecastExecution)}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{kg(r.remainingKg)}</td>
                {fromLinko && <td className="py-2 pr-3 text-right tabular-nums">{kg(r.scopeFactKg ?? r.factKg)}</td>}
              </tr>
            ))}
          </tbody>
          {total && (
            <tfoot>
              <tr className="bg-muted/60 font-semibold">
                <td className="py-2 pr-3">{t(total.name)}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{kg(total.planKg)}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{kg(total.factKg)}</td>
                <td className={`py-2 pr-3 text-right tabular-nums ${levelClass(total.executionLevel)}`}>{pct(total.execution)}</td>
                <td />
                <td className={`py-2 pr-3 text-right tabular-nums ${levelClass(total.forecastLevel)}`}>{pct(total.forecastExecution)}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{kg(total.remainingKg)}</td>
                {fromLinko && <td />}
              </tr>
            </tfoot>
          )}
        </table>
      </div>
      <Note>
        {fromLinko
          ? t(
              "{missing} — план по показателям ТП в Linko. Показатели заведены по цехам: «Могуль + Шоколад», «Трубочка + Печенье» — одной строкой, так они и показаны. Факт — только ТП с этим планом, весь факт — справа. ",
              { missing: capitalize(planMissingText(planOf(plan), t)) },
            )
          : t(
              "План категории — сумма плана «регион × категория» по регионам подразделения (фасовки помадки — одна категория); факт — все продажи категории, а не только ТП с планом. У ТП плана по категориям из планов регионов нет — в карточке ТП его план из Linko. ",
            )}
        {t("Серые строки — категории с продажами, на которые плана нет; в итог они не входят.")}
      </Note>
    </>
  );
}

/** Цвета категорий в «Из чего складывается объём» — по порядку долей; различимы в светлой и тёмной теме. */
const mixPalette = ["#3b82f6", "#f97316", "#10b981", "#eab308", "#ec4899", "#8b5cf6", "#ef4444", "#06b6d4", "#84cc16", "#64748b"];

/** «Из чего складывается объём» (DOC-rules §9.1): доля категории в выручке региона за месяц — считает сервер, здесь полоса и список. */
export function VolumeMix({ categories }: { categories: { categoryId: number | null; name: string; revenue: number; share: number | null }[] }) {
  const t = useT();
  if (categories.length === 0) return null;
  return (
    <Section title={t("Из чего складывается объём")} hint={t("доля категории в выручке за месяц")}>
      <div className="flex h-3 w-full overflow-hidden rounded-full bg-muted" role="img" aria-label={t("Доли категорий в выручке")}>
        {categories.map((c, i) => (
          <div key={c.categoryId ?? c.name} title={`${c.categoryId == null ? t(c.name) : c.name}: ${pct(c.share, 1)}`} style={{ width: `${(c.share ?? 0) * 100}%`, background: mixPalette[i % mixPalette.length] }} />
        ))}
      </div>
      <ul className="mt-3 grid gap-x-6 gap-y-1.5 text-sm sm:grid-cols-2 lg:grid-cols-3">
        {categories.map((c, i) => (
          <li key={c.categoryId ?? c.name} className="flex items-center gap-2">
            <span className="size-2.5 shrink-0 rounded-sm" style={{ background: mixPalette[i % mixPalette.length] }} />
            <span className="min-w-0 flex-1 truncate text-ink">{c.categoryId == null ? t(c.name) : c.name}</span>
            <span className="tabular-nums text-ink-2">{money(c.revenue, t)}</span>
            <span className="w-14 text-right font-semibold tabular-nums text-ink">{pct(c.share, 1)}</span>
          </li>
        ))}
      </ul>
      <Note>{t("Доля = выручка категории ÷ выручка всех категорий региона за месяц (чистая, минус возвраты).")}</Note>
    </Section>
  );
}

/**
 * «План на следующий месяц» — планы регионов выбранного вида (РОП / «Завод»), когда они на него уже заведены, иначе — планы ТП из Linko.
 * Плашка и столбец «Разница» — изменение к плану текущего месяца, его считает сервер.
 */
export function NextMonthCard({ plan, rowsTitle }: { plan: NextMonthPlan; rowsTitle: string }) {
  const t = useT();
  const diff = plan.change;
  return (
    <Section
      title={t("План на {month}", { month: monthLabel(plan.year, plan.month, t).toLowerCase() })}
      hint={plan.source === "linko" ? t("из Linko · {n} ТП с планом", { n: num(plan.agents) }) : t(planSourceText[plan.source])}
    >
      <div className="flex flex-wrap items-baseline gap-3">
        <span className="text-[1.75rem] font-semibold tabular-nums text-ink">{kg(plan.planKg)}</span>
        <span className="text-sm text-ink-3">{t("кг")}</span>
        {diff != null && (
          <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${diff >= 0 ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>
            {delta(diff)} {t("к текущему месяцу")}
          </span>
        )}
      </div>
      <div className="-mx-4 mt-3 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
        <table className="w-full min-w-max text-sm">
          <thead>
            <tr className="border-b border-line text-[11px] uppercase tracking-wide text-ink-3">
              <th className="py-2 pr-3 text-left font-semibold">{t(rowsTitle)}</th>
              <th className="py-2 pr-3 text-right font-semibold">{t("План")}</th>
              <th className="py-2 pr-3 text-right font-semibold">{t("Текущий месяц")}</th>
              <th className="py-2 text-right font-semibold">{t("Разница")}</th>
            </tr>
          </thead>
          <tbody>
            {plan.rows.map((r) => {
              const d = r.change;
              return (
                <tr key={r.id} className="border-b border-line last:border-0">
                  <td className="py-1.5 pr-3 text-ink">{t(r.name)}</td>
                  <td className="py-1.5 pr-3 text-right tabular-nums">{kg(r.planKg)}</td>
                  <td className="py-1.5 pr-3 text-right tabular-nums text-ink-2">{r.currentPlanKg == null ? "—" : kg(r.currentPlanKg)}</td>
                  <td className={`py-1.5 text-right tabular-nums ${d == null ? "text-ink-3" : d >= 0 ? "text-ok" : "text-bad"}`}>{d == null ? t("новый") : delta(d)}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </Section>
  );
}
