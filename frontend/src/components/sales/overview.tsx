"use client";

// Клиентский модуль: подписи собираются из чисел — их переводит useT(). Все пропсы — данные /api/sales/overview, демо-данных здесь нет.
import Link from "next/link";
import {
  Activity,
  ArrowRight,
  ArrowUpRight,
  Banknote,
  CalendarDays,
  ChevronRight,
  CircleCheck,
  CircleDashed,
  DoorOpen,
  Flag,
  Gauge,
  Globe,
  Info,
  MapPin,
  Repeat,
  Store,
  Target,
  TrendingDown,
  TrendingUp,
  UserRoundCheck,
  UserRoundX,
  Users,
  type LucideIcon,
} from "lucide-react";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { plural } from "@/lib/format";
import { dayMonth, delta, kg, money, monthName, num, pct, regionsLabel } from "@/lib/sales/format";
import { queryWith, withQuery } from "@/lib/sales/query";
import {
  agentsSplit,
  monthDays,
  planBlock,
  planLink,
  planNote,
  targetKpi,
  toneOf,
  visitsNoOrder,
  type KpiBar,
  type PaceRow,
  type PlanBlock,
  type Tone,
} from "@/lib/sales-secondary";
import type { ExcludedSummary, FlagCounts, KpiTiles, Period, TargetLevel, UnitRow } from "@/lib/sales/types";
import { planSourceText, planTileLabel } from "./blocks";

/**
 * Экран «Продажи → Вторичка» (ТЗ «Вторичка», макеты Laptop и Mobile): главный блок «Выполнение плана» с темпом, пять KPI с полосой
 * «факт + прогноз», блок ТП с разбивкой «критично / риск / в норме», карточки «Республика» и «Экспорт и опт», мини-полоса дней месяца.
 * Статусы и полосы — lib/sales-secondary.ts (цвета — по уровням сервера). Палитра — ТЗ, только на этой странице: data-palette="sales-secondary"
 * в app/(app)/sales/page.tsx, значения — в globals.css. Телефон (уже md) — тот же DOM, компактнее, как в макете Mobile.
 */

const textTone: Record<Tone, string> = { ok: "text-ok", warn: "text-warn", bad: "text-bad", none: "text-ink" };
const fillTone: Record<Tone, string> = { ok: "bg-ok", warn: "bg-warn", bad: "bg-bad", none: "bg-ink-3" };
const paleTone: Record<Tone, string> = { ok: "bg-ok/25", warn: "bg-warn/25", bad: "bg-bad/25", none: "bg-ink-3/25" };
// Прогноз на полосе главного блока и «Республики» — всегда warning 25% (ТЗ: «бледная заливка цветом прогноза (warning 25%)»), в легенде — 40%.
const forecastFill = "bg-warn/25";
const forecastSwatch = "bg-warn/40";
const badgeTone: Record<Tone, string> = { ok: "bg-ok-soft text-ok", warn: "bg-warn-soft text-warn", bad: "bg-bad-soft text-bad", none: "bg-muted text-ink-2" };
// Фон и рамка главного блока — по статусу, как в макете: рамка — цвет 25%, фон — основа с оттенком статуса (globals.css: в тёмной теме
// bg + 7%, как #1A0E14 у danger; в светлой — белый + 3%) и градиент цвета 12% из угла, к 70% — прозрачный; без статуса — обычная карточка.
const panelTone: Record<Tone, string> = {
  ok: "border-ok/25 bg-[color:color-mix(in_srgb,var(--color-ok)_var(--sec-panel-tint),var(--sec-panel-base))] bg-linear-135 from-ok/12 to-transparent to-70%",
  warn: "border-warn/25 bg-[color:color-mix(in_srgb,var(--color-warn)_var(--sec-panel-tint),var(--sec-panel-base))] bg-linear-135 from-warn/12 to-transparent to-70%",
  bad: "border-bad/25 bg-[color:color-mix(in_srgb,var(--color-bad)_var(--sec-panel-tint),var(--sec-panel-base))] bg-linear-135 from-bad/12 to-transparent to-70%",
  none: "border-line bg-surface",
};

const card = "rounded-2xl border border-line bg-surface shadow-sm";

/** Две подписи: полная — с md, короткая — на телефоне (как в макете Mobile). */
function Responsive({ full, short }: { full: React.ReactNode; short: React.ReactNode }) {
  return (
    <>
      <span className="max-md:hidden">{full}</span>
      <span className="md:hidden">{short}</span>
    </>
  );
}

// ---------- Дни месяца (шапка) ----------

/** «Данные по 07.11 · прошло 7 из 30 дней» и мини-полоса прошедшей доли месяца; на телефоне — «по 07.11 · 7 из 30 дн.». Только идущий месяц. */
export function MonthDays({ period }: { period: Period }) {
  const t = useT();
  if (period.closed) return null;
  const d = monthDays(period);
  const date = dayMonth(period.dataThrough);
  const days = t(plural(d.days, ["дня", "дней", "дней"]));
  return (
    <div
      className="flex flex-col items-end gap-1.5 max-md:mt-1"
      title={t("Отчёт — по {date} включительно; за сегодняшний день — данные на время последней синхронизации Linko (каждые 30 минут)", { date: date })}
    >
      <span className="inline-flex items-center gap-1.5 text-xs tabular-nums max-md:hidden">
        <CalendarDays className="size-[14px] text-ink-3" aria-hidden />
        <span className="font-semibold text-ink-2">{t("Данные по {date} · прошло {n} из {days} {word}", { date, n: d.worked, days: d.days, word: days })}</span>
      </span>
      <span
        className="block h-1 w-60 overflow-hidden rounded-full bg-muted max-md:hidden"
        role="progressbar"
        aria-label={t("Прошло {n} из {days} {word}", { n: d.worked, days: d.days, word: days })}
        aria-valuemin={0}
        aria-valuemax={d.days}
        aria-valuenow={d.worked}
      >
        <span className="block h-full rounded-full bg-ink-3" style={{ width: `${d.share * 100}%` }} />
      </span>
      <span className="text-xs tabular-nums text-ink-3 md:hidden">{t("по {date} · {n} из {days} дн.", { date, n: d.worked, days: d.days })}</span>
    </div>
  );
}

// ---------- Выполнение плана ----------

const statusIcon: Record<Tone | "done", LucideIcon> = { ok: TrendingUp, warn: Activity, bad: TrendingDown, none: CircleDashed, done: CircleCheck };

const planName = (period: Period) => (period.plan === "factory" ? "План «Завод»" : "План РОП");

function paceRowView(row: PaceRow, block: PlanBlock, hasPlan: boolean, t: T): { icon: LucideIcon; label: string; short?: string; value: string; tone: Tone; mobile: boolean } {
  switch (row.key) {
    case "pace":
      return { icon: Gauge, label: row.closed ? t("Средний темп") : t("Текущий темп"), value: row.kg == null ? "—" : t("{kg} кг/день", { kg: num(row.kg) }), tone: "none", mobile: true };
    case "required":
      return {
        icon: Target,
        label: t("Нужно до конца месяца"),
        short: t("Нужно до конца"),
        value: t("{kg} кг/день", { kg: num(row.kg) }),
        tone: row.behind ? "warn" : "none",
        mobile: true,
      };
    case "over":
      return { icon: CircleCheck, label: t("Перевыполнение"), value: `+${pct(row.share)}`, tone: "ok", mobile: true };
    case "short":
      return { icon: Target, label: t("Не хватило до плана"), value: t("{kg} кг", { kg: kg(row.kg) }), tone: block.tone === "ok" ? "warn" : block.tone, mobile: true };
    case "forecast":
      return {
        icon: Flag,
        label: hasPlan ? t("Прогноз при текущем темпе") : t("Прогноз на конец месяца"),
        value: row.kg == null ? "—" : `≈ ${money(row.kg, t)} ${t("кг")}${row.share != null ? ` · ${pct(row.share)}` : ""}`,
        tone: "none",
        // На телефоне прогноз — в легенде под полосой; без плана легенды нет, и строка остаётся.
        mobile: !hasPlan,
      };
    case "result":
      return { icon: Flag, label: t("Итог месяца"), value: `${kg(row.kg)} ${t("кг")}${row.share != null ? ` · ${pct(row.share, 1)}` : ""}`, tone: "none", mobile: true };
  }
}

/**
 * Главный блок: факт к плану крупно, полоса — факт, отметка «должно быть к дате» и прогноз на конец месяца (подсказка при наведении),
 * справа — текущий темп, сколько нужно в день и прогноз. Плана РОП («Завод») на месяц нет — пометка и «Задать план →» (если есть права).
 */
export function PlanCard({ kpi, period, permissions }: { kpi: KpiTiles; period: Period; permissions: readonly string[] }) {
  const t = useT();
  const block = planBlock(kpi, period);
  const hasPlan = block.kind !== "noPlan";
  const note = planNote(kpi);
  const link = note ? planLink(period.plan, permissions) : null;
  const month = monthName(period.month, t);
  const Icon = statusIcon[block.kind === "done" ? "done" : block.tone];
  const execution = kpi.execution ?? (hasPlan ? (kpi.planFactKg ?? kpi.factKg) / kpi.planKg! : null);
  const rows = block.rows.map((row) => paceRowView(row, block, hasPlan, t));

  return (
    <section aria-labelledby="plan-title" className={`rounded-[20px] border p-[18px] shadow-sm md:px-6 md:py-[22px] ${panelTone[block.tone]}`}>
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_380px] lg:gap-x-7 lg:gap-y-3.5">
        <div className="min-w-0">
          <div className="flex items-center justify-between gap-2.5 md:justify-start">
            <h2 id="plan-title" className="text-sm font-semibold leading-[18px] text-ink-2 max-md:text-[13px]">
              {t(planTileLabel[kpi.planSource])}
            </h2>
            {block.label && (
              <span className={`inline-flex items-center gap-[5px] rounded-[7px] px-2 py-0.5 text-[11px] font-bold leading-[14px] max-md:text-[10px] max-md:leading-[13px] ${badgeTone[block.tone]}`}>
                <Icon className="size-3 max-md:size-[11px]" aria-hidden />
                <Responsive full={t(block.label)} short={t(block.short ?? block.label)} />
              </span>
            )}
          </div>

          <div className="mt-3.5 flex flex-wrap items-baseline gap-x-3 gap-y-1">
            <span className="text-[36px] font-bold leading-none tracking-[-1.5px] text-ink tabular-nums md:text-[44px]">
              {hasPlan ? pct(execution, 1) : kg(kpi.factKg)}
              {!hasPlan && <span className="ml-1.5 text-base font-medium text-ink-3">{t("кг")}</span>}
            </span>
            <span className="text-[12px] font-medium text-ink-2 tabular-nums md:text-[15px]">
              {hasPlan
                ? t("{fact} из {plan} кг", { fact: kg(kpi.planFactKg ?? kpi.factKg), plan: kg(kpi.planKg) })
                : t("факт за {month}", { month })}
            </span>
          </div>

          {block.bar && hasPlan && <PlanBar block={block} kpi={kpi} period={period} execution={execution ?? 0} />}
        </div>

        {rows.length > 0 && (
          <dl className="self-center overflow-hidden rounded-2xl border border-ink/8 bg-ink/[0.03] lg:row-span-2">
            {rows.map((row, i) => (
              <div
                key={i}
                className={`flex items-center gap-2.5 border-ink/8 px-4 py-2.5 md:py-3.5 [&:not(:first-child)]:border-t ${row.mobile ? "" : "max-md:hidden"}`}
              >
                <row.icon className="size-[15px] shrink-0 text-ink-3 max-md:hidden" aria-hidden />
                <dt className="min-w-0 flex-1 text-[13px] font-medium text-ink-3 max-md:text-[12px]">{row.short ? <Responsive full={row.label} short={row.short} /> : row.label}</dt>
                <dd className={`whitespace-nowrap text-[14px] font-bold tabular-nums max-md:text-[13px] ${textTone[row.tone]}`}>{row.value}</dd>
              </div>
            ))}
          </dl>
        )}

        {(note || kpi.altPlan) && (
          <div className="space-y-1 text-xs font-medium leading-[15px] text-ink-4 max-md:text-[11px] lg:col-start-1">
            {note && (
              <p className="flex flex-wrap items-center gap-x-2 gap-y-1">
                <Info className="size-[13px] shrink-0 max-md:hidden" aria-hidden />
                <span>
                  {/* С заглавной: в узбекском шаблон начинается с названия месяца. */}
                  {note === "none" ? (
                    capitalize(t("План на {month} не задан", { month }))
                  ) : (
                    <Responsive
                      full={capitalize(t("{plan} на {month} не задан — используется план ТП из Linko", { plan: t(planName(period)), month }))}
                      short={t("{plan} не задан — используется план ТП из Linko", { plan: t(planName(period)) })}
                    />
                  )}
                </span>
                {link && (
                  <Link href={link} className="inline-flex items-center gap-1 font-semibold text-accent-strong hover:underline max-md:min-h-[44px]">
                    {note === "none" ? t("Задать план") : period.plan === "factory" ? t("Задать план «Завод»") : t("Задать план РОП")}
                    <ArrowRight className="size-3.5" aria-hidden />
                  </Link>
                )}
              </p>
            )}
            {/* Второй план регионов — рядом, а не вместо: видно и «по своему плану», и «по плану другой стороны». */}
            {kpi.altPlan && (
              <p className={note ? "md:pl-[22px]" : undefined} title={t("{source}: весь факт к нему, в выполнение не входит", { source: capitalize(t(planSourceText[kpi.altPlan.source])) })}>
                {t("{source}: {pct} из {plan} кг", { source: capitalize(t(planSourceText[kpi.altPlan.source])), pct: pct(kpi.altPlan.execution, 1), plan: kg(kpi.altPlan.planKg) })}
              </p>
            )}
          </div>
        )}
      </div>
    </section>
  );
}

const capitalize = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);

/** Полоса главного блока: прогноз бледно, факт ярко, отметка «должно быть к дате»; при наведении и фокусе — подсказка с кг. */
function PlanBar({ block, kpi, period, execution }: { block: PlanBlock; kpi: KpiTiles; period: Period; execution: number }) {
  const t = useT();
  const bar = block.bar!;
  const date = dayMonth(period.dataThrough);
  const planKg = kpi.planKg ?? 0;
  const factLabel = pct(execution, 1);
  const aria = [
    t("Факт {v}", { v: factLabel }),
    bar.expected != null ? t("ожидается {v}", { v: pct(bar.expected) }) : null,
    bar.forecast != null ? t("прогноз {v}", { v: pct(kpi.forecastExecution) }) : null,
  ]
    .filter(Boolean)
    .join(", ");

  return (
    <>
      <div
        tabIndex={0}
        role="progressbar"
        aria-label={aria}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.round(Math.min(1, execution) * 100)}
        className="group relative mt-3.5 flex h-[30px] items-center rounded-full outline-none focus-visible:ring-2 focus-visible:ring-accent/50 max-md:h-6 lg:max-w-[820px]"
      >
        <div className="relative h-[10px] w-full rounded-full bg-ink/[0.07] max-md:h-2">
          {bar.forecast != null && <div className={`absolute inset-y-0 left-0 rounded-full ${forecastFill}`} style={{ width: `${bar.forecast * 100}%` }} />}
          <div className={`absolute inset-y-0 left-0 rounded-full ${fillTone[block.tone]}`} style={{ width: `${bar.fact * 100}%` }} />
          {bar.expected != null && (
            <div className="absolute top-1/2 h-[22px] w-[2px] -translate-x-1/2 -translate-y-1/2 rounded-full bg-ink max-md:h-[18px]" style={{ left: `${bar.expected * 100}%` }} />
          )}
        </div>
        <div
          role="tooltip"
          className="pointer-events-none absolute bottom-full left-0 z-20 mb-1 hidden w-max max-w-[min(24rem,calc(100vw-3rem))] rounded-lg border border-line bg-surface-solid px-3 py-2 text-xs leading-relaxed text-ink-2 shadow-lg group-hover:block group-focus-visible:block"
        >
          <p>
            <b className="font-semibold text-ink">{t("Факт")}</b>: {t("{kg} кг", { kg: kg(kpi.planFactKg ?? kpi.factKg) })} · {factLabel}
          </p>
          {bar.expected != null && (
            <p>
              <b className="font-semibold text-ink">{t("Должно быть к {date}", { date })}</b>: {t("{kg} кг", { kg: kg(planKg * bar.expected) })} · {pct(bar.expected)}
            </p>
          )}
          {bar.forecast != null && (
            <p>
              <b className="font-semibold text-ink">{t("Прогноз на конец месяца")}</b>: {t("{kg} кг", { kg: kg(kpi.planForecastKg) })} · {pct(kpi.forecastExecution)}
            </p>
          )}
        </div>
      </div>
      <ul className="mt-3.5 flex flex-wrap gap-x-[18px] gap-y-1.5 text-xs font-medium leading-[15px] text-ink-2 tabular-nums max-md:mt-2.5 max-md:gap-x-3 max-md:text-[11px]">
        <li className="inline-flex items-center gap-1.5">
          <span className={`h-[6px] w-[10px] rounded-[2px] max-md:w-[9px] ${fillTone[block.tone]}`} aria-hidden />
          <Responsive full={t("Факт {v}", { v: factLabel })} short={t("факт {v}", { v: pct(execution) })} />
        </li>
        {bar.expected != null && (
          <li className="inline-flex items-center gap-1.5">
            <span className="h-3 w-[2px] rounded-[2px] bg-ink max-md:h-2.5" aria-hidden />
            <Responsive full={t("Должно быть к {date} — {v}", { date, v: pct(bar.expected) })} short={t("нужно {v}", { v: pct(bar.expected) })} />
          </li>
        )}
        {bar.forecast != null && (
          <li className="inline-flex items-center gap-1.5">
            <span className={`h-[6px] w-[10px] rounded-[2px] max-md:w-[9px] ${forecastSwatch}`} aria-hidden />
            <Responsive
              full={t("Прогноз на конец месяца — {v}", { v: pct(kpi.forecastExecution) })}
              short={t("прогноз {v}", { v: pct(kpi.forecastExecution) })}
            />
          </li>
        )}
      </ul>
    </>
  );
}

// ---------- KPI ----------

/** Полоса KPI: яркая — факт к цели, бледная — прогноз к цели; цвет — статус. */
function KpiProgress({ bar, tone, label }: { bar: KpiBar; tone: Tone; label: string }) {
  return (
    <div
      className="relative mt-2.5 h-[5px] overflow-hidden rounded-full bg-muted max-md:h-1"
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={Math.round(bar.fact * 100)}
    >
      {bar.forecast != null && <div className={`absolute inset-y-0 left-0 rounded-full ${paleTone[tone]}`} style={{ width: `${bar.forecast * 100}%` }} />}
      <div className={`absolute inset-y-0 left-0 rounded-full ${fillTone[tone]}`} style={{ width: `${bar.fact * 100}%` }} />
    </div>
  );
}

function KpiCard({
  label,
  icon: Icon,
  value,
  unit,
  tone,
  bar,
  barLabel,
  caption,
  title,
  href,
}: {
  label: string;
  icon: LucideIcon;
  value: string;
  unit?: string;
  tone: Tone;
  bar: KpiBar | null;
  barLabel?: string;
  caption: React.ReactNode;
  title: string;
  href: string;
}) {
  return (
    <Link href={href} title={title} className="group block min-w-0 max-lg:active:opacity-75">
      <div className={`flex h-full flex-col px-[18px] py-4 transition-colors group-hover:border-accent/40 max-md:p-3.5 ${card}`}>
        <div className="flex items-start justify-between gap-2">
          <span className="text-xs font-semibold leading-[15px] text-ink-3 max-md:text-[11px]">{label}</span>
          <Icon className="size-[15px] shrink-0 text-ink-4 max-md:hidden" aria-hidden />
        </div>
        <div className="mt-2.5 flex flex-wrap items-baseline gap-x-[5px]">
          <span className={`text-[24px] font-bold leading-[1.1] tracking-[-0.4px] tabular-nums max-md:text-[20px] ${textTone[tone]}`}>{value}</span>
          {unit && <span className="text-xs font-medium text-ink-3 max-md:hidden">{unit}</span>}
        </div>
        {bar && <KpiProgress bar={bar} tone={tone} label={barLabel ?? label} />}
        <p className="pt-2.5 text-[11px] font-medium leading-[14px] text-ink-4 max-md:text-[10px]">{caption}</p>
      </div>
    </Link>
  );
}

/**
 * Пять KPI: выручка (без цели — без полосы и цвета), конверсия визита, выручка на ТТ, АКБ на агента (полоса «факт + прогноз» к цели,
 * цвет — уровень сервера), визиты без заказа (больше 40% визитов — предупреждение). На телефоне — сетка 2×2 и визиты строкой.
 * Карточка ведёт в «Республику» — регионы, ТП и визиты подробно.
 */
export function KpiCards({ kpi, period, href }: { kpi: KpiTiles; period: Period; href: string }) {
  const t = useT();
  const closed = period.closed;
  const conversion = targetKpi(kpi.conversion, closed);
  const perOutlet = targetKpi(kpi.revenuePerOutlet, closed);
  const akb = targetKpi(kpi.akbPerAgent, closed);
  const visits = visitsNoOrder(kpi);
  const barLabel = (name: string, x: { share: number | null; forecastShare: number | null }) =>
    x.forecastShare != null
      ? t("{name}: факт {fact} цели, прогноз {forecast} цели", { name, fact: pct(x.share), forecast: pct(x.forecastShare) })
      : t("{name}: {fact} цели", { name, fact: pct(x.share) });

  const revenueCaption = kpi.revenuePlan
    ? kpi.revenuePlan.agents < kpi.activeAgents
      ? t("план у {n} ТП: {pct}", { n: num(kpi.revenuePlan.agents), pct: pct(kpi.revenuePlan.execution) })
      : t("{pct} плана", { pct: pct(kpi.revenuePlan.execution) })
    : t("АКБ {akb}", { akb: num(kpi.akb) });

  const perOutletCaption =
    perOutlet.forecastShare != null && kpi.revenuePerOutlet.forecast != null ? (
      <Responsive
        full={t("{pct} цели · прогноз {value} ({fpct})", { pct: pct(perOutlet.share), value: money(kpi.revenuePerOutlet.forecast, t), fpct: pct(perOutlet.forecastShare) })}
        short={t("{pct} цели · прогноз {fpct}", { pct: pct(perOutlet.share), fpct: pct(perOutlet.forecastShare) })}
      />
    ) : (
      t("цель {value} · {pct} цели", { value: money(kpi.revenuePerOutlet.target, t), pct: pct(perOutlet.share) })
    );

  const akbCaption =
    akb.forecastShare != null && kpi.akbPerAgent.forecast != null ? (
      <Responsive
        full={t("цель {target} · прогноз {value} ({pct} цели)", { target: num(kpi.akbPerAgent.target), value: num(kpi.akbPerAgent.forecast, 1), pct: pct(akb.forecastShare) })}
        short={t("цель {target} · прогноз {value} ({pct})", { target: num(kpi.akbPerAgent.target), value: num(kpi.akbPerAgent.forecast, 1), pct: pct(akb.forecastShare) })}
      />
    ) : (
      t("цель {target} · {pct} цели", { target: num(kpi.akbPerAgent.target), pct: pct(akb.share) })
    );

  const visitsTitle =
    t("Визиты ТП, в день которых в этой точке не было их заказа · {agents} ТП в месяце", { agents: num(kpi.activeAgents) }) +
    (kpi.visitsOutsideTeam > 0 ? t(" · ещё {n} визитов не ТП (операторы, супервайзеры, вне справочника) — в конверсию не входят", { n: num(kpi.visitsOutsideTeam) }) : "");
  const outside = kpi.visitsOutsideTeam > 0 ? ` · ${t("не ТП {n}", { n: num(kpi.visitsOutsideTeam) })}` : "";

  return (
    <div className="grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-5">
      <KpiCard
        label={t("Выручка")}
        icon={Banknote}
        value={money(kpi.revenue, t)}
        unit={t("сум")}
        tone="none"
        bar={null}
        caption={revenueCaption}
        href={href}
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
      />
      <KpiCard
        label={t("Конверсия визита")}
        icon={Repeat}
        value={pct(kpi.conversion.value)}
        tone={conversion.tone}
        bar={conversion.bar}
        barLabel={barLabel(t("Конверсия визита"), conversion)}
        caption={t("цель {value}", { value: pct(kpi.conversion.target) })}
        href={href}
        title={t("{pct} от цели · визиты ТП с заказом ÷ их выполненные визиты", { pct: pct(kpi.conversion.ratio) })}
      />
      {/* Выручка на ТТ и АКБ на агента копятся весь месяц: цвет идущего месяца — по прогнозу на конец месяца (сервер). */}
      <KpiCard
        label={t("Выручка на ТТ")}
        icon={Store}
        value={money(kpi.revenuePerOutlet.value, t)}
        unit={t("сум")}
        tone={perOutlet.tone}
        bar={perOutlet.bar}
        barLabel={barLabel(t("Выручка на ТТ"), perOutlet)}
        caption={perOutletCaption}
        href={href}
        title={
          closed
            ? t("цель {value} сум", { value: money(kpi.revenuePerOutlet.target, t) })
            : kpi.revenuePerOutlet.forecast != null
              ? t("прогноз на конец месяца: выручка по темпу ÷ прогноз АКБ · цель {value} сум", { value: money(kpi.revenuePerOutlet.target, t) })
              : t("в начале месяца прогноза ещё нет · цель {value} сум", { value: money(kpi.revenuePerOutlet.target, t) })
        }
      />
      <KpiCard
        label={t("АКБ на агента")}
        icon={Users}
        value={num(kpi.akbPerAgent.value, 1)}
        tone={akb.tone}
        bar={akb.bar}
        barLabel={barLabel(t("АКБ на агента"), akb)}
        caption={akbCaption}
        href={href}
        title={
          closed
            ? t("{pct} от цели", { pct: pct(kpi.akbPerAgent.ratio) })
            : kpi.akbPerAgent.forecast != null
              ? t("прогноз на конец месяца — {pct} от цели: АКБ растёт так же, как в прошлом месяце от тех же дней до конца", { pct: pct(kpi.akbPerAgent.ratio) })
              : t("в начале месяца прогноза ещё нет")
        }
      />
      {/* «Визиты без заказа»: на компьютере — карточка в ряду, на телефоне — строка во всю ширину (подписи слева, число и доля справа). */}
      <Link href={href} title={visitsTitle} className="group col-span-2 block min-w-0 max-lg:active:opacity-75 md:col-span-1">
        <div className={`flex h-full flex-col px-[18px] py-4 transition-colors group-hover:border-accent/40 max-md:p-3.5 ${card}`}>
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <span className="block text-xs font-semibold leading-[15px] text-ink-3 max-md:text-[11px]">{t("Визиты без заказа")}</span>
              <p className="mt-1 text-[10px] font-medium text-ink-4 tabular-nums md:hidden">
                {t("из {n} визитов ТП", { n: num(kpi.visitsDone) })}
                {outside}
              </p>
            </div>
            <DoorOpen className="size-[15px] shrink-0 text-ink-4 max-md:hidden" aria-hidden />
            <div className="shrink-0 text-right md:hidden">
              <span className={`block text-[20px] font-bold leading-tight tabular-nums ${textTone[visits.tone]}`}>{num(kpi.visitsWithoutOrder)}</span>
              <span className={`text-[10px] font-semibold tabular-nums ${visits.tone === "warn" ? "text-warn" : "text-ink-3"}`}>{pct(visits.share)}</span>
            </div>
          </div>
          <div className="mt-2.5 flex flex-wrap items-baseline gap-x-[5px] max-md:hidden">
            <span className={`text-[24px] font-bold leading-[1.1] tracking-[-0.4px] tabular-nums ${textTone[visits.tone]}`}>{num(kpi.visitsWithoutOrder)}</span>
            <span className="text-xs font-medium text-ink-3 tabular-nums">{t("из {n}", { n: num(kpi.visitsDone) })}</span>
          </div>
          {visits.bar && (
            <div className="max-md:hidden">
              <KpiProgress bar={visits.bar} tone={visits.tone} label={t("{pct} визитов без заказа", { pct: pct(visits.share) })} />
            </div>
          )}
          <p className="pt-2.5 text-[11px] font-medium leading-[14px] text-ink-4 tabular-nums max-md:hidden">
            {t("{pct} визитов", { pct: pct(visits.share) })}
            {outside}
          </p>
        </div>
      </Link>
    </div>
  );
}

// ---------- Торговые представители ----------

const segmentFill = { critical: "bg-bad", risk: "bg-warn", ok: "bg-ok" } as const;
const agentsTile: Record<Tone, { icon: LucideIcon; className: string }> = {
  bad: { icon: UserRoundX, className: "bg-bad-soft text-bad" },
  warn: { icon: UserRoundX, className: "bg-warn-soft text-warn" },
  ok: { icon: UserRoundCheck, className: "bg-ok-soft text-ok" },
  none: { icon: UserRoundCheck, className: "bg-muted text-ink-3" },
};

/**
 * «Торговые представители: 67 из 123 требуют внимания» — полоса «критично / риск / в норме» (у каждого ТП — худший флаг, как на сервере);
 * сегмент и строка легенды открывают рейтинг агентов с этим статусом, «Рейтинг агентов →» — весь рейтинг.
 */
export function AgentsCard({ activeAgents, flags, vacancies, query }: { activeAgents: number; flags: FlagCounts; vacancies: number; query: string }) {
  const t = useT();
  const split = agentsSplit(activeAgents, flags);
  const rating = withQuery("/sales/agents", query);
  const statusHref = (status: string) => withQuery("/sales/agents", queryWith(query, { status }));
  const tile = agentsTile[split.total > 0 ? split.tone : "none"];
  const vacancyWord = plural(vacancies, ["вакансия", "вакансии", "вакансий"]);
  const shown = split.segments.filter((s) => s.n > 0);

  const title =
    split.total === 0
      ? { full: t("Торговых представителей в этом месяце нет"), short: t("ТП в этом месяце нет") }
      : split.attention > 0
        ? {
            full: t("Торговые представители: {n} из {all} требуют внимания", { n: num(split.attention), all: num(split.total) }),
            short: t("{n} из {all} ТП — внимание", { n: num(split.attention), all: num(split.total) }),
          }
        : { full: t("Торговые представители: {all}, замечаний нет", { all: num(split.total) }), short: t("{all} ТП — замечаний нет", { all: num(split.total) }) };

  return (
    <section aria-labelledby="agents-title" className={`px-5 py-4 max-md:p-4 ${card}`}>
      <div className="flex items-center gap-2.5">
        <span className={`grid size-[34px] shrink-0 place-items-center rounded-[10px] max-md:size-[32px] ${tile.className}`}>
          <tile.icon className="size-4 max-md:size-[15px]" aria-hidden />
        </span>
        <div className="min-w-0 flex-1">
          <h2 id="agents-title" className="text-[15px] font-bold leading-[19px] text-ink tabular-nums max-md:text-[14px]">
            <Responsive full={title.full} short={title.short} />
          </h2>
          {vacancies > 0 && (
            <p className="mt-px text-xs font-medium leading-[15px] text-ink-3 max-md:text-[11px]">
              <Responsive
                full={`${t("ещё {n} {word}", { n: num(vacancies), word: t(vacancyWord) })} — ${vacancyWord === "вакансия" ? t("не входит в рейтинг") : t("не входят в рейтинг")}`}
                short={t("+{n} {word} вне рейтинга", { n: num(vacancies), word: t(vacancyWord) })}
              />
            </p>
          )}
        </div>
        <Link
          href={rating}
          className="inline-flex shrink-0 items-center gap-1.5 rounded-[10px] border border-line-strong bg-muted px-3 py-2 text-[13px] font-semibold leading-[17px] text-ink hover:border-ink-4 max-md:hidden"
        >
          {t("Рейтинг агентов")}
          <ArrowRight className="size-[14px] text-ink-2" aria-hidden />
        </Link>
        <Link href={rating} aria-label={t("Рейтинг агентов")} className="-mr-2 grid size-[44px] shrink-0 place-items-center text-ink-4 md:hidden">
          <ChevronRight className="size-5" aria-hidden />
        </Link>
      </div>

      {split.total > 0 && (
        <>
          <div
            className="mt-3.5 flex h-[10px] gap-[3px] max-md:h-2"
            role="img"
            aria-label={split.segments.map((s) => `${t(s.label)} ${num(s.n)}`).join(", ")}
          >
            {shown.map((s) => (
              <Link
                key={s.key}
                href={statusHref(s.key)}
                tabIndex={-1}
                aria-hidden
                title={`${t(s.label)} ${num(s.n)}`}
                className={`h-full min-w-[6px] rounded-[2px] transition-opacity first:rounded-l-[5px] last:rounded-r-[5px] hover:opacity-80 ${segmentFill[s.key]}`}
                style={{ flexGrow: s.n, flexBasis: 0 }}
              />
            ))}
          </div>
          <div className="mt-3.5 flex flex-wrap gap-x-5 max-md:mt-1.5 max-md:justify-between max-md:gap-x-3">
            {split.segments.map((s) => (
              <Link
                key={s.key}
                href={statusHref(s.key)}
                className="inline-flex items-center gap-[7px] text-[13px] font-medium leading-[17px] text-ink-2 hover:text-ink max-md:min-h-[44px] max-md:text-[12px] max-md:font-semibold"
              >
                <span className={`size-2.5 rounded-[3px] max-md:size-2 ${segmentFill[s.key]}`} aria-hidden />
                {t(s.label)}
                <b className="font-bold text-ink tabular-nums max-md:font-semibold max-md:text-ink-2">{num(s.n)}</b>
              </Link>
            ))}
          </div>
        </>
      )}
    </section>
  );
}

// ---------- Направления ----------

type StatCell = { label: string; value: string; tone?: Tone; phone: boolean };

/** Сетка показателей с разделителями: cols колонок, линии — только внутри. */
function StatGrid({ stats, cols, className }: { stats: StatCell[]; cols: 2 | 3; className: string }) {
  return (
    <dl className={`grid ${cols === 3 ? "grid-cols-3" : "grid-cols-2"} ${className}`}>
      {stats.map((s, i) => (
        <div
          key={s.label}
          className={`min-w-0 border-line ${i % cols ? "border-l pl-3.5" : "pr-3.5"} ${i >= cols ? "border-t pt-3" : ""} ${i >= stats.length - (stats.length % cols || cols) ? "" : "pb-4 max-md:pb-3"}`}
        >
          <dt className="truncate text-[11px] font-medium leading-[14px] text-ink-3">{s.label}</dt>
          <dd className={`mt-[3px] truncate text-[18px] font-bold leading-[23px] tabular-nums max-md:text-base max-md:leading-[20px] ${textTone[s.tone ?? "none"]}`}>{s.value}</dd>
        </div>
      ))}
    </dl>
  );
}

function DirectionCard({
  icon: Icon,
  iconClass,
  title,
  subtitle,
  href,
  bar,
  stats,
}: {
  icon: LucideIcon;
  iconClass: string;
  title: string;
  subtitle: string;
  href: string;
  bar?: React.ReactNode;
  stats: StatCell[];
}) {
  const t = useT();
  return (
    <Link href={href} aria-label={t("Открыть «{title}»", { title })} className="group block min-w-0 max-lg:active:opacity-75">
      <section className="flex h-full flex-col gap-4 rounded-[18px] border border-line bg-surface p-5 shadow-sm transition-colors group-hover:border-accent/40 max-md:gap-3.5 max-md:p-4">
        <div className="flex items-center gap-3">
          <span className={`grid size-10 shrink-0 place-items-center rounded-xl max-md:size-9 ${iconClass}`}>
            <Icon className="size-[19px] max-md:size-[17px]" aria-hidden />
          </span>
          <div className="min-w-0 flex-1">
            <h2 className="truncate text-[17px] font-bold leading-[22px] text-ink max-md:text-[15px]">{title}</h2>
            <p className="mt-0.5 line-clamp-2 text-xs font-medium leading-[15px] text-ink-3 max-md:text-[11px]">{subtitle}</p>
          </div>
          <span className="grid size-[32px] shrink-0 place-items-center rounded-full border border-line-strong bg-muted text-ink-2 group-hover:text-ink max-md:hidden">
            <ArrowUpRight className="size-[15px]" aria-hidden />
          </span>
          <ChevronRight className="size-5 shrink-0 text-ink-4 md:hidden" aria-hidden />
        </div>
        {bar}
        <StatGrid stats={stats} cols={3} className="max-md:hidden" />
        <StatGrid stats={stats.filter((s) => s.phone)} cols={2} className="md:hidden" />
      </section>
    </Link>
  );
}

/**
 * «Республика»: полоса «факт + прогноз» цветом статуса, сетка 3×2; на телефоне — 2×2.
 * level — уровень главного блока (kpi.pace.level): охват тот же, и факт на полосе окрашен так же, как «Выполнение плана» над ней
 * (у карточки с сервера прогноз окрашен порогами выполнения — 90% и 60%). Прогноз — цветом прогноза (warning), как в макете.
 */
export function RepublicCard({ unit, period, href, level }: { unit: UnitRow; period: Period; href: string; level?: TargetLevel | null }) {
  const t = useT();
  const hasPlan = unit.planKg != null && unit.planKg > 0;
  const fact = hasPlan ? (unit.planFactKg ?? unit.factKg) : unit.factKg;
  const done = hasPlan && fact >= unit.planKg!;
  const status = level !== undefined ? level : period.closed ? unit.executionLevel : unit.forecastExecutionLevel;
  const tone: Tone = done ? "ok" : toneOf(status);
  const forecast = !period.closed && unit.forecastExecution != null ? Math.min(1, unit.forecastExecution) : null;
  const execution = unit.execution == null ? 0 : Math.min(1, unit.execution);
  const subtitle = [unit.regionCount > 0 ? regionsLabel(unit.regionCount, t) : null, t("{n} ТП", { n: num(unit.agents) })].filter(Boolean).join(" · ");

  const bar = hasPlan ? (
    <div
      className="relative h-2 overflow-hidden rounded-full bg-muted max-md:h-1.5"
      role="progressbar"
      aria-label={
        forecast != null
          ? t("Республика: факт {fact}, прогноз {forecast}", { fact: pct(unit.execution), forecast: pct(unit.forecastExecution) })
          : t("Республика: факт {fact}", { fact: pct(unit.execution) })
      }
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={Math.round(execution * 100)}
    >
      {forecast != null && <div className={`absolute inset-y-0 left-0 rounded-full ${forecastFill}`} style={{ width: `${forecast * 100}%` }} />}
      <div className={`absolute inset-y-0 left-0 rounded-full ${fillTone[tone]}`} style={{ width: `${execution * 100}%` }} />
    </div>
  ) : (
    <div className="hatched h-2 w-full rounded-full max-md:h-1.5" title={t("Плана нет")} />
  );

  const stats: StatCell[] = [
    { label: t("План, кг"), value: kg(unit.planKg), phone: true },
    { label: hasPlan ? t("Факт в плане, кг") : t("Факт, кг"), value: kg(fact), phone: false },
    { label: t("Выполнение"), value: pct(unit.execution), tone: toneOf(unit.executionLevel), phone: true },
    // Прогноз — цветом прогноза, как его заливка на полосе (warning, макет); план уже выполнен — зелёным.
    period.closed
      ? { label: t("Всего продано, кг"), value: kg(unit.factKg), phone: true }
      : hasPlan
        ? { label: t("Прогноз"), value: pct(unit.forecastExecution), tone: done ? "ok" : unit.forecastExecution == null ? "none" : "warn", phone: true }
        : { label: t("Прогноз, кг"), value: kg(unit.forecastKg), phone: true },
    { label: t("Страйк"), value: pct(unit.strike), phone: true },
    { label: t("Выручка"), value: money(unit.revenue, t), phone: false },
  ];

  // Название с сервера («Республика») — через словарь, как в прежней карточке.
  return <DirectionCard icon={MapPin} iconClass="bg-accent-soft text-accent" title={t(unit.name)} subtitle={subtitle} href={href} bar={bar} stats={stats} />;
}

/** «Экспорт и опт»: филиал «Завод» из Linko — отдельно от вторички, чтобы не завышать республику. Сетка 3×2; на телефоне — 2×2. */
export function ExportCard({ data, href }: { data: ExcludedSummary; href: string }) {
  const t = useT();
  const change = data.vsPrevMonth;
  const stats: StatCell[] = [
    { label: t("Факт, кг"), value: kg(data.factKg), phone: true },
    { label: t("Прогноз, кг"), value: kg(data.forecastKg), phone: true },
    { label: t("Выручка"), value: money(data.revenue, t), phone: true },
    { label: t("Заказов"), value: num(data.orders), phone: false },
    { label: t("АКБ"), value: num(data.akb), phone: false },
    { label: t("К прошлому месяцу"), value: change == null ? delta(change) : delta(change).replace(" ", ""), tone: change == null ? "none" : change >= 0 ? "ok" : "bad", phone: true },
  ];
  return (
    <DirectionCard
      icon={Globe}
      iconClass="bg-violet-soft text-violet"
      title={t("Экспорт и опт")}
      subtitle={t("филиал «Завод» в Linko · вне вторички")}
      href={href}
      stats={stats}
    />
  );
}
