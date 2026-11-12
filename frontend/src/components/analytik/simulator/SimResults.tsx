"use client";

import Link from "next/link";
import { Fragment, useMemo, useState } from "react";
import {
  ArrowDownRight,
  ArrowRight,
  ArrowUpRight,
  Banknote,
  CalendarRange,
  ChartArea,
  ChartLine,
  ChevronDown,
  ChevronRight,
  CircleAlert,
  CircleCheck,
  ClipboardList,
  DatabaseZap,
  Factory,
  FileSearch,
  FileText,
  Info,
  Lightbulb,
  Link2,
  List,
  Loader2,
  MapPin,
  MapPinPlus,
  Package,
  Pencil,
  Search,
  ShieldAlert,
  ShoppingCart,
  Sparkles,
  Tag,
  TrendingUp,
  TriangleAlert,
  Truck,
  Users,
  Wallet,
  Wheat,
} from "lucide-react";
import { useT } from "@/i18n/client";
import { date, dateTime, num } from "@/lib/analytik";
import {
  aiSections,
  chainTitles,
  deltaTone,
  explainResult,
  groupLabel,
  isChanged,
  isKeyRow,
  isMissing,
  kpiMetrics,
  metricRows,
  missingReason,
  pct,
  plural,
  shortValue,
  simDelta,
  simValue,
  valueParts,
  type BaselineView,
  type Completeness,
  type GraphNode,
  type KindView,
  type MetricRow,
  type ScenarioHistoryEntry,
  type SimMetric,
  type SimulationResult,
} from "@/lib/simulator";
import { LineChart } from "../ui";

type Icon = React.ComponentType<{ className?: string; "aria-hidden"?: boolean }>;

export const card = "rounded-[18px] border border-line bg-surface";
const toneText = { ok: "text-ok", bad: "text-bad", muted: "text-ink-4" };

/** Разница со знаком в коротком виде: «+0,25 млрд» → «+249,9 млн», «+1 785 сум», «−8,5 млн»; проценты — в п.п. */
const shortDelta = (delta: number | null, unit: string) =>
  delta == null || delta === 0 ? "—" : unit === "%" ? (simDelta(delta, "%") ?? "—") : `${delta > 0 ? "+" : "−"}${shortValue(Math.abs(delta), unit)}`;
const toneChip = { ok: "bg-ok-soft text-ok", bad: "bg-bad-soft text-bad", muted: "bg-muted text-ink-4" };

/** Текст перевода с подстановками-элементами: «Выручка вырастет на {p} — до {sum}» → {p} цветным числом. */
function rich(template: string, parts: Record<string, React.ReactNode>): React.ReactNode[] {
  return template.split(/(\{\w+\})/).map((piece, i) => {
    const key = /^\{(\w+)\}$/.exec(piece)?.[1];
    return key && key in parts ? <Fragment key={i}>{parts[key]}</Fragment> : <Fragment key={i}>{piece}</Fragment>;
  });
}

/** Чип изменения в %: стрелка и знак (не только цвет), цвет — по смыслу показателя (рост расходов — красный). */
export function DeltaChip({ m, size = "sm" }: { m: Pick<SimMetric, "code" | "delta" | "deltaPercent" | "unit">; size?: "sm" | "md" }) {
  const tone = deltaTone(m.code, m.delta);
  if (!isChanged(m as SimMetric)) return <span className={`rounded-md px-1.5 py-0.5 text-[11px] font-bold ${toneChip.muted}`}>—</span>;
  const up = (m.delta ?? 0) > 0;
  const Arrow = up ? ArrowUpRight : ArrowDownRight;
  const text = m.unit === "%" ? simDelta(m.delta, "%") : m.deltaPercent != null ? pct(m.deltaPercent) : simDelta(m.delta, m.unit);
  return (
    <span className={`inline-flex shrink-0 items-center gap-0.5 rounded-md px-1.5 py-0.5 font-bold tabular-nums ${size === "md" ? "text-[12px]" : "text-[11px]"} ${toneChip[tone]}`}>
      <Arrow className="size-3" aria-hidden />
      {text}
    </span>
  );
}

function NeedData({ reason, compact }: { reason: string; compact?: boolean }) {
  const t = useT();
  return (
    <span className="inline-flex items-center gap-1 rounded-md bg-warn-soft px-1.5 py-0.5 text-[11px] font-bold text-warn" title={t(reason)}>
      <DatabaseZap className="size-3" aria-hidden />
      {compact ? t("данные") : t("Нужны данные")}
    </span>
  );
}

// ---------- «Расчёт неполный» ----------

const issueAction = { recipes: { label: "Связать техкарты", href: "/production/recipes" } } as const;

/** Одна плашка вместо одинаковых «Себестоимость неизвестна…» в каждой строке: что не считается, почему и что сделать. */
export function IncompleteBanner({ info, onInputs, onOpen }: { info: Completeness; onInputs: () => void; onOpen: () => void }) {
  const t = useT();
  if (info.complete) return null;
  const title = info.missing.length > 0 ? t("Расчёт неполный: {what} не считаются", { what: joinRu(info.missing.map((m) => t(m)), t) }) : t("Расчёт неполный: ограничения проверены не полностью");
  const reasons = info.issues.map((i) => t(i.text, i.vars)).join(" · ");
  const recipes = info.issues.some((i) => i.code === "recipes");
  return (
    <>
      {/* Компьютер и планшет */}
      <section className="flex flex-wrap items-center gap-3 rounded-2xl border border-warn/25 bg-warn-soft px-4 py-3.5 max-md:hidden" role="status">
        <span className="grid size-9 shrink-0 place-items-center rounded-xl bg-surface text-warn">
          <TriangleAlert className="size-[18px]" aria-hidden />
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-[13px] font-bold text-ink">{title}</p>
          {reasons && <p className="mt-0.5 text-[12px] text-ink-2">{reasons}</p>}
        </div>
        <div className="flex shrink-0 flex-wrap gap-2">
          {recipes && (
            <Link href={issueAction.recipes.href} className="inline-flex h-9 items-center gap-1.5 rounded-[10px] bg-warn px-3.5 text-[12px] font-bold text-[#1a1206] hover:brightness-110">
              <Link2 className="size-3.5" aria-hidden />
              {t(issueAction.recipes.label)}
            </Link>
          )}
          <button type="button" onClick={onInputs} className="inline-flex h-9 items-center gap-1.5 rounded-[10px] border border-line-strong bg-surface px-3.5 text-[12px] font-semibold text-ink hover:border-warn">
            <Pencil className="size-3.5" aria-hidden />
            {t("Ввести данные")}
          </button>
        </div>
      </section>
      {/* Телефон: коротко, подробности — в листе */}
      <button type="button" onClick={onOpen} className="flex w-full items-center gap-3 rounded-2xl border border-warn/25 bg-warn-soft px-4 py-3 text-left md:hidden">
        <TriangleAlert className="size-5 shrink-0 text-warn" aria-hidden />
        <span className="min-w-0 flex-1">
          <span className="block text-[14px] font-bold text-ink">{t("Расчёт неполный")}</span>
          <span className="block text-[12px] text-ink-2">{info.missing.length > 0 ? t("{what} не считаются — нет данных", { what: capital(joinRu(info.missing.map((m) => t(m)), t)) }) : t("Ограничения проверены не полностью")}</span>
        </span>
        <ChevronRight className="size-5 shrink-0 text-warn" aria-hidden />
      </button>
    </>
  );
}

/** Подробности «Расчёт неполный» на телефоне (содержимое листа). */
export function IncompleteDetails({ info, onInputs }: { info: Completeness; onInputs: () => void }) {
  const t = useT();
  return (
    <div className="space-y-2.5 py-4">
      {info.missing.length > 0 && <p className="text-[13px] text-ink-2">{t("Не считаются: {what}.", { what: joinRu(info.missing.map((m) => t(m)), t) })}</p>}
      <ul className="space-y-2">
        {info.issues.map((i) => (
          <li key={i.code} className="rounded-xl bg-muted px-3 py-2.5 text-[13px] text-ink">
            {capital(t(i.text, i.vars))}
          </li>
        ))}
      </ul>
      <div className="flex flex-col gap-2 pt-1">
        {info.issues.some((i) => i.code === "recipes") && (
          <Link href={issueAction.recipes.href} className="inline-flex h-11 items-center justify-center gap-1.5 rounded-xl bg-warn text-[14px] font-bold text-[#1a1206]">
            <Link2 className="size-4" aria-hidden />
            {t(issueAction.recipes.label)}
          </Link>
        )}
        <button type="button" onClick={onInputs} className="inline-flex h-11 items-center justify-center gap-1.5 rounded-xl border border-line-strong bg-muted text-[14px] font-semibold text-ink">
          <Pencil className="size-4" aria-hidden />
          {t("Ввести данные")}
        </button>
      </div>
    </div>
  );
}

const capital = (s: string) => (s ? s.charAt(0).toUpperCase() + s.slice(1) : s);
function joinRu(list: string[], t: (s: string) => string): string {
  if (list.length <= 1) return list.join("");
  return `${list.slice(0, -1).join(", ")} ${t("и")} ${list[list.length - 1]}`;
}

// ---------- Вывод ----------

type HeadlineProps = { result: SimulationResult; baseline: BaselineView | null; changed: number; running: boolean };

function useHeadline({ result, baseline, changed }: HeadlineProps) {
  const t = useT();
  const revenue = result.metrics.find((m) => m.code === "sales.revenue");
  const cash = result.metrics.find((m) => m.code === "finance.net_cash");
  const explanation = explainResult(result).map((x) => t(x.text, x.vars));
  const n = (m: SimMetric) => <strong className={`font-bold ${toneText[deltaTone(m.code, m.delta)] === "text-ink-4" ? "text-ink" : toneText[deltaTone(m.code, m.delta)]}`}>{pct(m.deltaPercent)}</strong>;
  let phrase: React.ReactNode;
  if (changed === 0 && revenue?.scenario != null) {
    phrase = t("Базовый сценарий на {n} дн. по факту {from} — {to}: выручка {sum}", { n: result.horizonDays, from: date(baseline?.from ?? result.from), to: date(baseline?.to ?? result.to), sum: simValue(revenue.scenario, "сум") });
  } else if (revenue?.scenario != null && revenue.base != null) {
    const up = (revenue.delta ?? 0) > 0;
    const head = !isChanged(revenue) ? t("Выручка не изменится — {sum}") : up ? t("Выручка вырастет на {p} — до {sum}") : t("Выручка снизится на {p} — до {sum}");
    const cashPart = cash && isChanged(cash) && cash.deltaPercent != null ? rich(t(", а деньги {c}"), { c: n(cash) }) : null;
    phrase = (
      <>
        {rich(head, { p: n(revenue), sum: <span className="text-ink">{simValue(revenue.scenario, "сум")}</span> })}
        {cashPart}
      </>
    );
  } else {
    phrase = t(result.conclusion.headline);
  }
  return { revenue, phrase, explanation };
}

export function Headline(props: HeadlineProps) {
  const t = useT();
  const { result, baseline, running } = props;
  const { revenue, phrase, explanation } = useHeadline(props);
  return (
    <>
      <section id="sim-headline" className={`relative scroll-mt-4 overflow-hidden p-5 max-md:hidden ${card}`} aria-busy={running}>
        <div aria-hidden className="pointer-events-none absolute -right-24 -top-28 size-80 rounded-full bg-[radial-gradient(closest-side,rgb(61_123_255/0.16),transparent)]" />
        <div className="relative flex flex-wrap items-center gap-x-2.5 gap-y-1.5 text-[11px]">
          <span className="inline-flex items-center gap-1.5 rounded-lg bg-muted px-2 py-1 font-semibold text-ink-2">
            <CalendarRange className="size-3.5" aria-hidden />
            {t("Горизонт {n} дн.", { n: result.horizonDays })} · {date(result.from).slice(0, 5)} — {date(result.to)}
          </span>
          {baseline && <span className="text-ink-4">{t("база: факт {from} — {to}", { from: date(baseline.from).slice(0, 5), to: date(baseline.to) })}</span>}
          {running && (
            <span className="ml-auto inline-flex items-center gap-1 font-medium text-accent-strong">
              <Loader2 className="size-3.5 animate-spin" aria-hidden />
              {t("Пересчёт…")}
            </span>
          )}
        </div>
        <p className="relative mt-3 text-[22px] font-semibold leading-snug text-ink-2">{phrase}</p>
        {explanation.length > 0 && <p className="relative mt-2 text-[13px] leading-relaxed text-ink-3">{explanation.join(" ")}</p>}
      </section>
      <section id="sim-headline-m" className={`relative scroll-mt-3 overflow-hidden p-4 md:hidden ${card}`} aria-busy={running}>
        <div aria-hidden className="pointer-events-none absolute -right-16 -top-20 size-56 rounded-full bg-[radial-gradient(closest-side,rgb(61_123_255/0.18),transparent)]" />
        {revenue?.scenario != null ? (
          <div className="relative">
            <div className="text-[10px] font-bold uppercase tracking-wide text-ink-4">
              {t(plural(result.horizonDays, "Выручка за {n} день", "Выручка за {n} дня", "Выручка за {n} дней"), { n: result.horizonDays })}
            </div>
            <div className="mt-1 flex flex-wrap items-center gap-2">
              <span className="text-[28px] font-bold leading-tight tabular-nums text-ink">{shortValue(revenue.scenario, "сум")}</span>
              {isChanged(revenue) && <DeltaChip m={revenue} size="md" />}
              {running && <Loader2 className="size-4 animate-spin text-accent-strong" aria-label={t("Пересчёт…")} />}
            </div>
            <p className="mt-1 text-[12px] text-ink-3">
              {t("база")} {shortValue(revenue.base, "сум")}
              {explanation[0] ? ` · ${lowerFirst(explanation[0]).replace(/\.$/, "")}` : ""}
            </p>
          </div>
        ) : (
          <p className="relative text-[15px] font-semibold text-ink-2">{phrase}</p>
        )}
      </section>
    </>
  );
}

const lowerFirst = (s: string) => (s.length > 1 && s[1] !== s[1].toUpperCase() ? s.charAt(0).toLowerCase() + s.slice(1) : s);

// ---------- KPI ----------

const kpiIcons: Record<string, Icon> = {
  "sales.revenue": Banknote,
  "sales.kg": Package,
  "sales.avg_price": Tag,
  "finance.net_cash": Wallet,
  "finance.gross_profit": TrendingUp,
  "procurement.budget": ShoppingCart,
  "production.output": Factory,
  "production.required": Factory,
  "production.utilization": Factory,
  "materials.need_value": Wheat,
  "materials.items_short": Wheat,
  "materials.end_stock_value": Wheat,
  "workforce.headcount": Users,
  "finance.payroll": Users,
};
const kpiTitle: Record<string, string> = { "sales.avg_price": "Средняя цена" };
const kpiReason = (m: SimMetric) => (/^finance\.(gross|cogs|cost|operating)/.test(m.code) && /Себестоимость/.test(m.note ?? "") ? "нужна себестоимость" : missingReason(m));

export function KpiGrid({ result, kind }: { result: SimulationResult; kind: KindView | undefined }) {
  const t = useT();
  const list = kpiMetrics(result, kind);
  const mobile = list.filter((m) => m.code !== "sales.revenue").slice(0, 4);
  return (
    <>
      <div className="grid grid-cols-2 gap-3 max-md:hidden @[640px]/res:grid-cols-3">
        {list.map((m) => {
          const Icon = kpiIcons[m.code] ?? ChartLine;
          const parts = valueParts(m.scenario, m.unit);
          const missing = m.scenario == null;
          return (
            <div key={m.code} className={`flex h-32 min-w-0 flex-col p-4 ${card}`} title={m.note ? t(m.note) : undefined}>
              <div className="flex items-center gap-1.5">
                <Icon className="size-3.5 shrink-0 text-ink-4" aria-hidden />
                <span className="min-w-0 flex-1 truncate text-[12px] text-ink-3">{t(kpiTitle[m.code] ?? m.title)}</span>
                {!missing && isChanged(m) && <DeltaChip m={m} />}
              </div>
              <div className="mt-2 flex min-w-0 items-baseline gap-1.5">
                {missing ? (
                  <span className="inline-flex items-center gap-1.5 rounded-lg bg-warn-soft px-2 py-1 text-[13px] font-bold text-warn">
                    <DatabaseZap className="size-3.5" aria-hidden />
                    {t("Нужны данные")}
                  </span>
                ) : (
                  <>
                    <span className="truncate text-[24px] font-bold leading-tight tabular-nums text-ink">{parts.value}</span>
                    <span className="shrink-0 text-[12px] text-ink-3">{t(parts.unit)}</span>
                  </>
                )}
              </div>
              <div className="mt-auto flex items-center justify-between gap-2 border-t border-line pt-2 text-[11px]">
                <span className="truncate">
                  <span className="text-ink-4">{t("база")}</span> <span className="font-semibold tabular-nums text-ink-2">{m.base == null ? "—" : shortValue(m.base, m.unit)}</span>
                </span>
                {missing ? <span className="truncate text-warn">{t(kpiReason(m))}</span> : !isChanged(m) ? <span className="text-ink-4">{t("без изменений")}</span> : null}
              </div>
            </div>
          );
        })}
      </div>
      <div className="grid grid-cols-2 gap-3 md:hidden">
        {mobile.map((m) => {
          const missing = m.scenario == null;
          return (
            <div key={m.code} className={`min-h-[86px] min-w-0 p-3.5 ${card}`}>
              <div className="truncate text-[13px] text-ink-3">{t(m.code === "sales.kg" ? "Продажи" : (kpiTitle[m.code] ?? m.title))}</div>
              {missing ? (
                <>
                  <span className="mt-1.5 inline-flex items-center gap-1 rounded-lg bg-warn-soft px-2 py-1 text-[13px] font-bold text-warn">
                    <DatabaseZap className="size-3.5" aria-hidden />
                    {t("Нужны данные")}
                  </span>
                  <div className="mt-1 truncate text-[11px] text-ink-4">{t(kpiReason(m))}</div>
                </>
              ) : (
                <>
                  <div className="mt-1 truncate text-[18px] font-bold tabular-nums text-ink">{m.unit === "сум" ? shortValue(m.scenario, "сум") : simValue(m.scenario, m.unit)}</div>
                  <div className="mt-1.5">{isChanged(m) ? <DeltaChip m={m} /> : <span className="text-[11px] text-ink-4">{t("без изменений")}</span>}</div>
                </>
              )}
            </div>
          );
        })}
      </div>
    </>
  );
}

// ---------- База и сценарий, ограничения, коротко ----------

const barCodes = ["sales.revenue", "finance.net_cash", "finance.working_capital", "procurement.budget", "finance.gross_profit"];

/** «База и сценарий»: по денежным показателям — две полосы по 8 px с подписями; масштаб — свой у строки. */
export function MoneyBars({ result }: { result: SimulationResult }) {
  const t = useT();
  const rows = barCodes.map((c) => result.metrics.find((m) => m.code === c)).filter((m): m is SimMetric => m != null && m.base != null && m.scenario != null);
  return (
    <section className={`p-5 ${card}`}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <h3 className="text-[15px] font-bold text-ink">{t("База и сценарий")}</h3>
          <p className="text-[11px] text-ink-4">{t("денежные показатели за горизонт")}</p>
        </div>
        <div className="flex gap-3 text-[11px] text-ink-3">
          <span className="inline-flex items-center gap-1.5">
            <span className="h-2 w-3 rounded-full bg-line-strong" />
            {t("база")}
          </span>
          <span className="inline-flex items-center gap-1.5">
            <span className="h-2 w-3 rounded-full bg-accent" />
            {t("сценарий")}
          </span>
        </div>
      </div>
      {rows.length === 0 ? (
        <p className="mt-4 text-[13px] text-ink-3">{t("Денежных показателей нет — нет продаж или цен в исходных данных.")}</p>
      ) : (
        <div className="mt-4 space-y-4">
          {rows.map((m) => {
            const max = Math.max(1, Math.abs(m.base!), Math.abs(m.scenario!));
            return (
              <div key={m.code}>
                <div className="flex items-baseline justify-between gap-2">
                  <span className="truncate text-[12px] font-semibold text-ink">{t(m.title)}</span>
                  <span className={`shrink-0 text-[12px] font-bold tabular-nums ${toneText[deltaTone(m.code, m.delta)]}`}>{isChanged(m) ? (m.deltaPercent != null ? pct(m.deltaPercent) : simDelta(m.delta, m.unit)) : "0%"}</span>
                </div>
                {[
                  { v: m.base!, cls: "bg-line-strong", label: "база" },
                  { v: m.scenario!, cls: m.scenario! < 0 ? "bg-bad" : "bg-accent", label: "сценарий" },
                ].map((bar) => (
                  <div key={bar.label} className="mt-1.5 flex items-center gap-2.5">
                    <div className="h-2 min-w-0 flex-1 overflow-hidden rounded-full bg-muted" title={t(bar.label)}>
                      <div className={`h-full rounded-full ${bar.cls}`} style={{ width: `${(Math.abs(bar.v) / max) * 100}%` }} />
                    </div>
                    <span className="w-[84px] shrink-0 text-right text-[11px] font-semibold tabular-nums text-ink-2">{shortValue(bar.v, m.unit)}</span>
                  </div>
                ))}
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}

const constraintType: Record<string, string> = {
  capacity: "мощность",
  capacity_high: "мощность",
  capacity_unknown: "мощность",
  labour: "мощность",
  material_shortage: "сырьё",
  min_lot: "сырьё",
  warehouse: "сырьё",
  fg_surplus: "сырьё",
  order_late: "сроки",
  lead_time_assumed: "сроки",
  credit_limit: "деньги",
  cash_gap: "деньги",
  demand_not_revenue: "продажи",
};

/** Ограничения: нет критичных и предупреждений — «сценарий выполним» (или «проверены не все», если проверять было нечем). */
export function ConstraintsCard({ result, unchecked }: { result: SimulationResult; unchecked: string | null }) {
  const t = useT();
  const order = { critical: 0, warning: 1, info: 2 };
  const list = [...result.constraints].sort((a, b) => order[a.severity] - order[b.severity]);
  const serious = list.filter((c) => c.severity !== "info");
  return (
    <section className={`p-5 ${card}`}>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="text-[15px] font-bold text-ink">{t("Ограничения")}</h3>
        <span className="text-[11px] text-ink-4">{t("мощность · сырьё · сроки · деньги")}</span>
      </div>
      <div className="mt-3 space-y-2">
        {serious.length === 0 &&
          (unchecked ? (
            <p className="flex items-start gap-2 rounded-xl bg-muted px-3 py-2.5 text-[12px] font-medium text-ink-2">
              <Info className="mt-px size-4 shrink-0 text-ink-3" aria-hidden />
              {t("Ограничений не найдено среди проверенных — {what} не проверены", { what: t(unchecked) })}
            </p>
          ) : (
            <p className="flex items-start gap-2 rounded-xl bg-ok-soft px-3 py-2.5 text-[12px] font-semibold text-ok">
              <CircleCheck className="mt-px size-4 shrink-0" aria-hidden />
              {t("Ограничений не найдено — сценарий выполним")}
            </p>
          ))}
        {list.map((c, i) => {
          const Icon = c.severity === "critical" ? CircleAlert : c.severity === "warning" ? TriangleAlert : Info;
          const tone = c.severity === "critical" ? "border-bad/25 bg-bad-soft text-bad" : c.severity === "warning" ? "border-warn/25 bg-warn-soft text-warn" : "border-line bg-muted text-ink-3";
          return (
            <div key={`${c.code}-${i}`} className={`flex gap-2.5 rounded-xl border px-3 py-2.5 ${tone}`}>
              <Icon className="mt-0.5 size-4 shrink-0" aria-hidden />
              <div className="min-w-0 flex-1 text-[12px]">
                <div className="flex flex-wrap items-baseline justify-between gap-x-2">
                  <span className="font-semibold">{t(c.title)}</span>
                  {constraintType[c.code] && <span className="text-[10px] font-semibold uppercase tracking-wide opacity-80">{t(constraintType[c.code])}</span>}
                </div>
                <p className="mt-0.5 text-ink-2">{t(c.text)}</p>
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}

export function ShortCard({ result }: { result: SimulationResult }) {
  const t = useT();
  const critical = result.constraints.filter((c) => c.severity === "critical").length;
  const short = result.materials.filter((m) => m.lostDays > 0).length;
  const limited = result.products.filter((p) => p.limit).length;
  const facts: [string, number, "bad" | "warn"][] = [
    ["Критичных ограничений", critical, "bad"],
    ["Рисков", result.risks.length, "warn"],
    ["Сырьё в дефиците", short, "bad"],
    ["Товаров ограничено", limited, "bad"],
  ];
  return (
    <section className={`p-5 ${card}`}>
      <h3 className="text-[15px] font-bold text-ink">{t("Коротко")}</h3>
      <dl className="mt-3 grid grid-cols-2 gap-2">
        {facts.map(([label, value, tone]) => (
          <div key={label} className="rounded-xl bg-muted px-3 py-2.5">
            <dt className="text-[11px] text-ink-3">{t(label)}</dt>
            <dd className={`mt-0.5 text-[20px] font-bold tabular-nums ${value === 0 ? "text-ok" : tone === "warn" ? "text-warn" : "text-bad"}`}>{value}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}

/** Телефон: одна строка ограничений и число рисков; есть ограничения — раскрывается список. */
export function ConstraintsRow({ result, unchecked }: { result: SimulationResult; unchecked: string | null }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const serious = result.constraints.filter((c) => c.severity !== "info");
  const critical = serious.filter((c) => c.severity === "critical").length;
  const risks = result.risks.length;
  const riskText = risks > 0 ? t(plural(risks, "{n} риск", "{n} риска", "{n} рисков"), { n: risks }) : null;
  if (serious.length === 0) {
    return (
      <div className={`flex items-center gap-2 rounded-2xl px-4 py-3 text-[13px] md:hidden ${unchecked ? "bg-muted text-ink-2" : "bg-ok-soft font-semibold text-ok"}`}>
        {unchecked ? <Info className="size-4 shrink-0" aria-hidden /> : <CircleCheck className="size-4 shrink-0" aria-hidden />}
        <span className="min-w-0 flex-1">{unchecked ? t("Ограничений нет среди проверенных") : t("Ограничений нет — сценарий выполним")}</span>
        {riskText && <span className="shrink-0 text-[12px] font-semibold text-warn">{riskText}</span>}
      </div>
    );
  }
  return (
    <div className="md:hidden">
      <button type="button" onClick={() => setOpen(!open)} aria-expanded={open} className={`flex w-full items-center gap-2 rounded-2xl px-4 py-3 text-left text-[13px] font-semibold ${critical ? "bg-bad-soft text-bad" : "bg-warn-soft text-warn"}`}>
        <CircleAlert className="size-4 shrink-0" aria-hidden />
        <span className="min-w-0 flex-1">{t(plural(serious.length, "{n} ограничение", "{n} ограничения", "{n} ограничений"), { n: serious.length })}</span>
        {riskText && <span className="shrink-0 text-[12px] text-warn">{riskText}</span>}
        <ChevronDown className={`size-4 shrink-0 transition ${open ? "rotate-180" : ""}`} aria-hidden />
      </button>
      {open && (
        <ul className="mt-2 space-y-2">
          {serious.map((c, i) => (
            <li key={`${c.code}-${i}`} className={`rounded-xl px-3 py-2.5 text-[12px] ${c.severity === "critical" ? "bg-bad-soft" : "bg-warn-soft"}`}>
              <div className={`font-semibold ${c.severity === "critical" ? "text-bad" : "text-warn"}`}>{t(c.title)}</div>
              <p className="mt-0.5 text-ink-2">{t(c.text)}</p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

// ---------- Подробные результаты ----------

type Tab = "metrics" | "products" | "regions" | "materials" | "expansion" | "charts" | "sources";

const groupIcons: Record<string, Icon> = { sales: ChartLine, production: Factory, materials: Wheat, procurement: ShoppingCart, finance: Wallet, workforce: Users, risks: ShieldAlert };
const detailGroup: Record<string, string> = { materials: "Сырьё и закупки", procurement: "Сырьё и закупки" };

export function DetailResults({ result, nodes, baseline, history }: { result: SimulationResult; nodes: GraphNode[]; baseline: BaselineView | null; history: ScenarioHistoryEntry[] }) {
  const t = useT();
  const [tab, setTab] = useState<Tab>("metrics");
  const [onlyChanged, setOnlyChanged] = useState(false);
  const tabs: { key: Tab; label: string; icon: Icon; count?: number }[] = [
    { key: "metrics", label: "Показатели", icon: List, count: result.metrics.length },
    ...(result.products.length ? [{ key: "products" as Tab, label: "Товары", icon: Package, count: result.products.length }] : []),
    ...(result.regions.length ? [{ key: "regions" as Tab, label: "Регионы", icon: MapPin, count: result.regions.length }] : []),
    ...(result.materials.length ? [{ key: "materials" as Tab, label: "Сырьё и закупки", icon: Wheat, count: result.materials.length }] : []),
    ...(result.expansion ? [{ key: "expansion" as Tab, label: "Новый регион", icon: MapPinPlus }] : []),
    ...(result.series.length ? [{ key: "charts" as Tab, label: "Графики", icon: ChartArea, count: result.series.length }] : []),
    { key: "sources", label: "Источники", icon: FileSearch },
  ];
  const active = tabs.some((x) => x.key === tab) ? tab : "metrics";
  const filterable = active === "metrics" || active === "products" || active === "regions";
  return (
    <section className={`overflow-hidden ${card}`}>
      <div className="flex flex-wrap items-start justify-between gap-3 px-4 pt-4 md:px-5 md:pt-5">
        <div className="min-w-0">
          <h3 className="text-[15px] font-bold text-ink">{t("Подробные результаты")}</h3>
          <p className="text-[11px] text-ink-4 max-md:hidden">{t("база → сценарий · «введено» — по данным пользователя")}</p>
        </div>
        {filterable && (
          <label className="inline-flex cursor-pointer items-center gap-2 text-[12px] text-ink-2 max-md:hidden">
            <span className="relative inline-flex">
              <input type="checkbox" role="switch" checked={onlyChanged} onChange={(e) => setOnlyChanged(e.target.checked)} className="peer sr-only" />
              <span className="h-5 w-9 rounded-full bg-line-strong transition peer-checked:bg-accent peer-focus-visible:ring-2 peer-focus-visible:ring-accent/40" />
              <span className="absolute left-0.5 top-0.5 size-4 rounded-full bg-white shadow transition peer-checked:translate-x-4" />
            </span>
            {t("Только изменённые")}
          </label>
        )}
      </div>
      <div className="px-4 pt-3 md:px-5">
        <div className="-mx-4 overflow-x-auto px-4 [scrollbar-width:none] md:mx-0 md:px-0">
          <div role="tablist" aria-label={t("Подробные результаты")} className="inline-flex gap-1 md:rounded-xl md:border md:border-line md:bg-page md:p-1">
            {tabs.map((x) => {
              const Icon = x.icon;
              const on = active === x.key;
              return (
                <button
                  key={x.key}
                  type="button"
                  role="tab"
                  aria-selected={on}
                  onClick={() => setTab(x.key)}
                  className={`inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-xl px-3 text-[12px] max-md:h-9 max-md:text-[13px] md:h-8 md:rounded-lg ${
                    on ? "font-semibold text-ink max-md:bg-accent max-md:text-white md:border md:border-line md:bg-muted" : "text-ink-3 hover:text-ink max-md:border max-md:border-line max-md:bg-muted"
                  }`}
                >
                  <Icon className="size-3.5 max-md:hidden" aria-hidden />
                  {t(x.label)}
                  {x.count != null && <span className={on ? "text-ink-3 max-md:text-white/70" : "text-ink-4"}>{x.count}</span>}
                </button>
              );
            })}
          </div>
        </div>
      </div>
      <div className="mt-3">
        {active === "metrics" && <MetricsTable result={result} onlyChanged={onlyChanged} />}
        {active === "products" && <ProductsTable result={result} onlyChanged={onlyChanged} />}
        {active === "regions" && <RegionsTable result={result} onlyChanged={onlyChanged} />}
        {active === "materials" && <MaterialsTable result={result} />}
        {active === "expansion" && result.expansion && <ExpansionView result={result} />}
        {active === "charts" && <SeriesCharts result={result} />}
        {active === "sources" && <SourcesView result={result} nodes={nodes} baseline={baseline} history={history} />}
      </div>
    </section>
  );
}

const th = "bg-muted px-3 py-2 text-[10px] font-bold uppercase tracking-wide text-ink-4";
const stickyCell = "sticky left-0 z-[1] bg-surface";

function MissingTitle({ row }: { row: Extract<MetricRow, { type: "missing" }> }) {
  const t = useT();
  const titles = row.metrics.map((m, i) => (i === 0 ? t(m.title) : lowerFirst(t(m.title))).replace(/\s*\(в среднем\)/, ""));
  return <>{titles.join(", ")}</>;
}

function MetricsTable({ result, onlyChanged }: { result: SimulationResult; onlyChanged: boolean }) {
  const t = useT();
  const [all, setAll] = useState(false);
  const groups = useMemo(() => metricRows(result.metrics), [result.metrics]);
  const visible = (row: MetricRow) => (onlyChanged ? row.type === "metric" && isChanged(row.metric) : all || isKeyRow(row));
  const shown = groups.map((g) => ({ ...g, rows: g.rows.filter(visible) })).filter((g) => g.rows.length > 0);
  // Группы «Сырьё» и «Закупки» движка — одна группа экрана «Сырьё и закупки».
  const merged: { group: string; rows: MetricRow[] }[] = [];
  for (const g of shown) {
    const label = detailGroup[g.group] ?? groupLabel[g.group] ?? g.group;
    const same = merged.find((x) => (detailGroup[x.group] ?? groupLabel[x.group] ?? x.group) === label);
    if (same) same.rows.push(...g.rows);
    else merged.push({ group: g.group, rows: [...g.rows] });
  }
  const hidden = groups.reduce((n, g) => n + g.rows.filter((r) => !visible(r)).length, 0);
  const inputBadge = (m: SimMetric) => m.kind === "input" && !isMissing(m) && <span className="ml-1.5 rounded bg-violet-soft px-1 py-px text-[10px] font-bold text-violet">{t("введено")}</span>;
  return (
    <>
      {merged.length === 0 && <p className="px-5 py-6 text-center text-[13px] text-ink-3">{t("Изменившихся показателей нет.")}</p>}
      {/* Компьютер и планшет — таблица; на узком экране прокрутка вбок, «Показатель» закреплён */}
      {merged.length > 0 && (
        <div className="overflow-x-auto max-md:hidden">
          <table className="w-full min-w-[680px] border-collapse">
            <thead>
              <tr>
                <th className={`${th} sticky left-0 z-[1] pl-5 text-left`}>{t("Показатель")}</th>
                <th className={`${th} w-[150px] text-right`}>{t("База")}</th>
                <th className={`${th} w-[150px] text-right`}>{t("Сценарий")}</th>
                <th className={`${th} w-[130px] text-right`}>{t("Разница")}</th>
                <th className={`${th} w-[80px] pr-5 text-right`}>%</th>
              </tr>
            </thead>
            {merged.map((g) => {
              const Icon = groupIcons[g.group] ?? List;
              return (
                <tbody key={g.group}>
                  <tr>
                    <td colSpan={5} className={`${stickyCell} px-5 pb-1.5 pt-4`}>
                      <span className="inline-flex items-center gap-1.5 text-[12px] font-bold text-ink">
                        <Icon className="size-3.5 text-accent-strong" aria-hidden />
                        {t(detailGroup[g.group] ?? groupLabel[g.group] ?? g.group)}
                      </span>
                    </td>
                  </tr>
                  {g.rows.map((row) =>
                    row.type === "missing" ? (
                      <tr key={row.metrics[0].code} className="border-b border-line">
                        <td className={`${stickyCell} py-[9px] pl-5 pr-3 text-[13px] text-ink-2`}>
                          <MissingTitle row={row} />
                        </td>
                        <td colSpan={4} className="py-[9px] pl-3 pr-5 text-right">
                          <span className="inline-flex flex-wrap items-center justify-end gap-2" title={row.note ? t(row.note) : undefined}>
                            <NeedData reason={row.reason} />
                            <span className="text-[11px] text-ink-4">{t(row.reason)}</span>
                          </span>
                        </td>
                      </tr>
                    ) : (
                      <MetricLine key={row.metric.code} m={row.metric} badge={inputBadge(row.metric)} />
                    ),
                  )}
                </tbody>
              );
            })}
          </table>
        </div>
      )}
      {/* Телефон — строки «база → сценарий» */}
      <ul className="divide-y divide-line md:hidden">
        {merged.flatMap((g) => g.rows).map((row) =>
          row.type === "missing" ? (
            <li key={row.metrics[0].code} className="flex items-center gap-3 px-4 py-3">
              <div className="min-w-0 flex-1">
                <div className="text-[13px] font-medium text-ink">
                  <MissingTitle row={row} />
                </div>
                <div className="text-[11px] text-ink-4">{t(row.reason)}</div>
              </div>
              <NeedData reason={row.reason} compact />
            </li>
          ) : (
            <li key={row.metric.code} className="flex items-center gap-3 px-4 py-3">
              <div className="min-w-0 flex-1">
                <div className="text-[13px] font-medium text-ink">
                  {t(row.metric.title)}
                  {inputBadge(row.metric)}
                </div>
                <div className="flex items-center gap-1 text-[11px] text-ink-4">
                  <span className="tabular-nums">{shortValue(row.metric.base, row.metric.unit)}</span>
                  <ArrowRight className="size-3" aria-hidden />
                  <span className={`tabular-nums ${isChanged(row.metric) ? "font-bold text-ink" : ""}`}>{shortValue(row.metric.scenario, row.metric.unit)}</span>
                </div>
              </div>
              {isChanged(row.metric) && <DeltaChip m={row.metric} />}
            </li>
          ),
        )}
      </ul>
      {!onlyChanged && (hidden > 0 || all) && (
        <button type="button" onClick={() => setAll(!all)} aria-expanded={all} className="flex w-full items-center justify-center gap-1 border-t border-line py-3 text-[12px] font-semibold text-accent-strong hover:bg-muted/50">
          {all ? t("Свернуть") : t(plural(result.metrics.length, "Показать все {n} показатель", "Показать все {n} показателя", "Показать все {n} показателей"), { n: result.metrics.length })}
          <ChevronDown className={`size-3.5 transition ${all ? "rotate-180" : ""}`} aria-hidden />
        </button>
      )}
    </>
  );
}

function MetricLine({ m, badge }: { m: SimMetric; badge: React.ReactNode }) {
  const t = useT();
  const changed = isChanged(m);
  const tone = toneText[deltaTone(m.code, m.delta)];
  return (
    <tr className="border-b border-line" title={m.note ? t(m.note) : undefined}>
      <td className={`${stickyCell} py-[9px] pl-5 pr-3 text-[13px] text-ink-2`}>
        {t(m.title)}
        {badge}
      </td>
      <td className="px-3 py-[9px] text-right text-[13px] tabular-nums text-ink-3">{shortValue(m.base, m.unit)}</td>
      <td className={`px-3 py-[9px] text-right text-[13px] tabular-nums text-ink ${changed ? "font-bold" : ""}`}>{shortValue(m.scenario, m.unit)}</td>
      <td className={`px-3 py-[9px] text-right text-[13px] font-semibold tabular-nums ${changed ? tone : "text-ink-4"}`}>{changed ? shortDelta(m.delta, m.unit) : "—"}</td>
      <td className="py-[9px] pl-3 pr-5 text-right">{changed && m.unit !== "%" && m.deltaPercent != null ? <DeltaChip m={m} /> : <span className="text-[12px] text-ink-4">—</span>}</td>
    </tr>
  );
}

/** Поиск и сортировка для «Товаров» и «Регионов». */
function ListTools({ query, onQuery, sort, onSort }: { query: string; onQuery: (v: string) => void; sort: string; onSort: (v: string) => void }) {
  const t = useT();
  return (
    <div className="flex flex-wrap gap-2 px-4 pb-3 md:px-5">
      <label className="flex h-9 min-w-0 flex-1 items-center gap-2 rounded-lg border border-line bg-page px-2.5 text-[13px]">
        <Search className="size-4 shrink-0 text-ink-4" aria-hidden />
        <input value={query} onChange={(e) => onQuery(e.target.value)} placeholder={t("Поиск")} aria-label={t("Поиск")} className="w-full min-w-0 bg-transparent text-ink outline-none placeholder:text-ink-4" />
      </label>
      <select value={sort} onChange={(e) => onSort(e.target.value)} aria-label={t("Сортировка")} className="h-9 rounded-lg border border-line bg-page px-2.5 text-[13px] text-ink">
        <option value="delta">{t("По разнице")}</option>
        <option value="revenue">{t("По выручке")}</option>
        <option value="name">{t("По названию")}</option>
      </select>
    </div>
  );
}

type Line = { id: string; name: string; tag?: string | null; base: number; scenario: number; sub: string; warn?: string | null };

function RevenueLines({ lines, onlyChanged, empty }: { lines: Line[]; onlyChanged: boolean; empty: string }) {
  const t = useT();
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState("delta");
  const [all, setAll] = useState(false);
  const q = query.trim().toLowerCase();
  const list = lines
    .filter((l) => (!q || l.name.toLowerCase().includes(q)) && (!onlyChanged || Math.abs(l.scenario - l.base) > 0.5))
    .sort((a, b) => (sort === "name" ? a.name.localeCompare(b.name, "ru") : sort === "revenue" ? b.scenario - a.scenario : Math.abs(b.scenario - b.base) - Math.abs(a.scenario - a.base)));
  const shown = all ? list : list.slice(0, 30);
  const metric = (l: Line) => ({ code: "sales.revenue", unit: "сум", delta: l.scenario - l.base, deltaPercent: l.base ? Math.round(((l.scenario - l.base) / l.base) * 1000) / 10 : null });
  return (
    <>
      <ListTools query={query} onQuery={setQuery} sort={sort} onSort={setSort} />
      {shown.length === 0 && <p className="px-5 pb-6 text-center text-[13px] text-ink-3">{t(empty)}</p>}
      {shown.length > 0 && (
        <div className="overflow-x-auto max-md:hidden">
          <table className="w-full min-w-[680px] border-collapse">
            <thead>
              <tr>
                <th className={`${th} sticky left-0 z-[1] pl-5 text-left`}>{t("Название")}</th>
                <th className={`${th} w-[150px] text-right`}>{t("Выручка база")}</th>
                <th className={`${th} w-[150px] text-right`}>{t("Сценарий")}</th>
                <th className={`${th} w-[130px] text-right`}>{t("Разница")}</th>
                <th className={`${th} w-[80px] pr-5 text-right`}>%</th>
              </tr>
            </thead>
            <tbody>
              {shown.map((l) => {
                const m = metric(l);
                return (
                  <tr key={l.id} className="border-b border-line">
                    <td className={`${stickyCell} py-[9px] pl-5 pr-3`}>
                      <div className="text-[13px] text-ink-2">
                        {l.name}
                        {l.tag && <span className="ml-1.5 rounded bg-accent-soft px-1 text-[10px] font-bold text-accent-strong">{t(l.tag)}</span>}
                      </div>
                      <div className="text-[11px] text-ink-4">
                        {l.sub}
                        {l.warn && <span className="text-warn"> · {t(l.warn)}</span>}
                      </div>
                    </td>
                    <td className="px-3 py-[9px] text-right text-[13px] tabular-nums text-ink-3">{shortValue(l.base, "сум")}</td>
                    <td className={`px-3 py-[9px] text-right text-[13px] tabular-nums text-ink ${isChanged(m as SimMetric) ? "font-bold" : ""}`}>{shortValue(l.scenario, "сум")}</td>
                    <td className={`px-3 py-[9px] text-right text-[13px] font-semibold tabular-nums ${isChanged(m as SimMetric) ? toneText[deltaTone("sales.revenue", m.delta)] : "text-ink-4"}`}>{isChanged(m as SimMetric) ? shortDelta(m.delta, "сум") : "—"}</td>
                    <td className="py-[9px] pl-3 pr-5 text-right">{isChanged(m as SimMetric) && m.deltaPercent != null ? <DeltaChip m={m} /> : <span className="text-[12px] text-ink-4">—</span>}</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      <ul className="divide-y divide-line border-t border-line md:hidden">
        {shown.map((l) => {
          const m = metric(l);
          return (
            <li key={l.id} className="flex items-center gap-3 px-4 py-3">
              <div className="min-w-0 flex-1">
                <div className="truncate text-[13px] font-medium text-ink">{l.name}</div>
                <div className="flex items-center gap-1 text-[11px] text-ink-4">
                  <span className="tabular-nums">{shortValue(l.base, "сум")}</span>
                  <ArrowRight className="size-3" aria-hidden />
                  <span className="font-bold tabular-nums text-ink">{shortValue(l.scenario, "сум")}</span>
                </div>
                {l.warn && <div className="text-[11px] text-warn">{t(l.warn)}</div>}
              </div>
              {isChanged(m as SimMetric) && m.deltaPercent != null && <DeltaChip m={m} />}
            </li>
          );
        })}
      </ul>
      {list.length > 30 && (
        <button type="button" onClick={() => setAll(!all)} className="flex w-full items-center justify-center gap-1 border-t border-line py-3 text-[12px] font-semibold text-accent-strong hover:bg-muted/50">
          {all ? t("Свернуть") : t("Показать все: {n}", { n: list.length })}
          <ChevronDown className={`size-3.5 transition ${all ? "rotate-180" : ""}`} aria-hidden />
        </button>
      )}
    </>
  );
}

function ProductsTable({ result, onlyChanged }: { result: SimulationResult; onlyChanged: boolean }) {
  const t = useT();
  const lines: Line[] = result.products.map((p) => ({
    id: String(p.id),
    name: p.name,
    base: p.baseRevenue,
    scenario: p.revenue,
    sub: `${num(p.baseKg, 0)} → ${num(p.soldKg, 0)} ${t("кг")}${p.hasRecipe ? "" : ` · ${t("без техкарты")}`}`,
    warn: p.limit,
  }));
  return <RevenueLines lines={lines} onlyChanged={onlyChanged} empty="Товаров не найдено." />;
}

function RegionsTable({ result, onlyChanged }: { result: SimulationResult; onlyChanged: boolean }) {
  const t = useT();
  const lines: Line[] = result.regions.map((r) => ({
    id: r.id,
    name: r.name,
    tag: r.new ? "новый" : null,
    base: r.baseRevenue,
    scenario: r.revenue,
    sub: `${num(r.baseKg, 0)} → ${num(r.kg, 0)} ${t("кг")}${r.execution != null ? ` · ${t("план")} ${num(r.execution, 1)}%` : ""}`,
  }));
  return <RevenueLines lines={lines} onlyChanged={onlyChanged} empty="Регионов не найдено." />;
}

const riskStyle: Record<string, string> = { critical: "bg-bad-soft text-bad", high: "bg-warn-soft text-warn", medium: "bg-accent-soft text-accent-strong", low: "bg-muted text-ink-3", none: "bg-muted text-ink-4" };
const riskName: Record<string, string> = { critical: "дефицит", high: "поздно", medium: "закупить", low: "излишек", none: "норма" };

function MaterialsTable({ result }: { result: SimulationResult }) {
  const t = useT();
  const [query, setQuery] = useState("");
  const q = query.trim().toLowerCase();
  const order = { critical: 0, high: 1, medium: 2, low: 3, none: 4 };
  const list = result.materials.filter((m) => !q || m.name.toLowerCase().includes(q)).sort((a, b) => order[a.risk] - order[b.risk]);
  return (
    <>
      <div className="px-4 pb-3 md:px-5">
        <label className="flex h-9 items-center gap-2 rounded-lg border border-line bg-page px-2.5 text-[13px]">
          <Search className="size-4 shrink-0 text-ink-4" aria-hidden />
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t("Поиск")} aria-label={t("Поиск")} className="w-full min-w-0 bg-transparent text-ink outline-none placeholder:text-ink-4" />
        </label>
      </div>
      <div className="overflow-x-auto max-md:hidden">
        <table className="w-full min-w-[860px] border-collapse">
          <thead>
            <tr>
              <th className={`${th} sticky left-0 z-[1] pl-5 text-left`}>{t("Позиция")}</th>
              <th className={`${th} text-right`}>{t("Расход/день")}</th>
              <th className={`${th} text-right`}>{t("Доступно")}</th>
              <th className={`${th} text-right`}>{t("Закупить")}</th>
              <th className={`${th} text-right`}>{t("Дефицит с")}</th>
              <th className={`${th} text-right`}>{t("Заказать до")}</th>
              <th className={`${th} pr-5 text-left`}>{t("Риск")}</th>
            </tr>
          </thead>
          <tbody>
            {list.slice(0, 150).map((m) => (
              <tr key={m.itemId} className="border-b border-line">
                <td className={`${stickyCell} py-[9px] pl-5 pr-3`}>
                  <div className="text-[13px] text-ink-2">{m.name}</div>
                  <div className="text-[11px] text-ink-4">
                    {m.supplier ?? t("поставщик не определён")} · {t("срок")} {m.leadTimeDays} {t("дн.")}
                  </div>
                </td>
                <td className="px-3 py-[9px] text-right text-[13px] tabular-nums text-ink">
                  {num(m.daily, 2)} {m.unit}
                  {m.baseDaily !== m.daily && (
                    <div className="text-[11px] text-ink-4">
                      {t("база")} {num(m.baseDaily, 2)}
                    </div>
                  )}
                </td>
                <td className="px-3 py-[9px] text-right text-[13px] tabular-nums text-ink-3">{num(m.available, 2)}</td>
                <td className="px-3 py-[9px] text-right text-[13px] tabular-nums text-ink">
                  {m.purchase > 0 ? num(m.purchase, 2) : "—"}
                  {m.purchase > 0 && <div className="text-[11px] text-ink-4">{m.purchaseCost != null ? shortValue(m.purchaseCost, "сум") : t("нет цены")}</div>}
                </td>
                <td className={`px-3 py-[9px] text-right text-[13px] tabular-nums ${m.shortageDate ? "font-semibold text-bad" : "text-ink-4"}`}>
                  {m.shortageDate ? date(m.shortageDate) : "—"}
                  {m.lostDays > 0 && <div className="text-[11px]">{t("без сырья {n} дн.", { n: num(m.lostDays, 1) })}</div>}
                </td>
                <td className={`px-3 py-[9px] text-right text-[13px] tabular-nums ${m.orderLate ? "text-warn" : "text-ink-3"}`}>{m.orderBy ? date(m.orderBy) : "—"}</td>
                <td className="py-[9px] pl-3 pr-5">
                  <span className={`rounded-md px-1.5 py-0.5 text-[11px] font-semibold ${riskStyle[m.risk]}`}>{t(riskName[m.risk])}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ul className="divide-y divide-line border-t border-line md:hidden">
        {list.slice(0, 60).map((m) => (
          <li key={m.itemId} className="flex items-center gap-3 px-4 py-3">
            <div className="min-w-0 flex-1">
              <div className="truncate text-[13px] font-medium text-ink">{m.name}</div>
              <div className="text-[11px] text-ink-4">
                {t("расход")} {num(m.baseDaily, 2)} → <span className="font-bold text-ink">{num(m.daily, 2)}</span> {m.unit}/{t("день")}
                {m.shortageDate && <span className="text-bad"> · {t("дефицит с {d}", { d: date(m.shortageDate) })}</span>}
              </div>
            </div>
            <span className={`shrink-0 rounded-md px-1.5 py-0.5 text-[11px] font-semibold ${riskStyle[m.risk]}`}>{t(riskName[m.risk])}</span>
          </li>
        ))}
      </ul>
    </>
  );
}

function ExpansionView({ result }: { result: SimulationResult }) {
  const t = useT();
  const e = result.expansion!;
  const rows: [string, string][] = [
    ["Продажи за горизонт", simValue(e.kg, "кг")],
    ["Выручка за горизонт", simValue(e.revenue, "сум")],
    ["Расходы за горизонт", simValue(e.cost, "сум")],
    ["Постоянные расходы в месяц", simValue(e.fixedMonth, "сум")],
    ["Прибыль в месяц", simValue(e.profitMonth, "сум")],
    ["Точка безубыточности", e.breakEvenKgMonth != null ? `${num(e.breakEvenKgMonth, 0)} ${t("кг/мес")}` : "—"],
    ["Срок окупаемости", e.paybackMonths != null ? `${num(e.paybackMonths, 1)} ${t("мес")}` : "—"],
    ["Нужно сотрудников (по образцу)", e.staffNeed != null ? String(e.staffNeed) : "—"],
    ["Заложено сотрудников", String(e.staff)],
  ];
  return (
    <div className="space-y-3 px-4 pb-5 md:px-5">
      <div className="text-[14px] font-semibold text-ink">
        {e.name}
        {e.reference && <span className="ml-1 font-normal text-ink-3">· {t("образец")}: {e.reference}</span>}
      </div>
      <dl className="grid gap-2 @[560px]/res:grid-cols-2">
        {rows.map(([k, v]) => (
          <div key={k} className="flex justify-between gap-3 rounded-xl bg-muted px-3 py-2.5 text-[13px]">
            <dt className="text-ink-3">{t(k)}</dt>
            <dd className="font-semibold tabular-nums text-ink">{v}</dd>
          </div>
        ))}
      </dl>
      {e.notes.map((n) => (
        <p key={n} className="text-[12px] text-ink-3">
          {t(n)}
        </p>
      ))}
    </div>
  );
}

/** Ряды по дням горизонта: деньги, запас сырья, критичные позиции. */
function SeriesCharts({ result }: { result: SimulationResult }) {
  const t = useT();
  const start = new Date(`${result.from}T00:00:00`);
  const labels = Array.from({ length: result.horizonDays }, (_, i) => {
    const d = new Date(start);
    d.setDate(start.getDate() + i);
    return d.toLocaleDateString("ru-RU", { day: "2-digit", month: "2-digit" });
  });
  return (
    <div className="grid gap-3 px-4 pb-5 md:px-5 @[760px]/res:grid-cols-2">
      {result.series.map((s) => (
        <div key={s.code} className="rounded-xl border border-line bg-page/40 p-3">
          <div className="text-[13px] font-semibold text-ink">{s.code.startsWith("stock:") ? `${t("Запас")}: ${s.title}` : t(s.title)}</div>
          <div className="mb-2 text-[11px] text-ink-4">{s.unit === "сум" ? t("сум, по дням горизонта") : `${s.unit}, ${t("по дням горизонта")}`}</div>
          <LineChart
            labels={labels}
            series={[
              { name: "База", values: s.base, color: "var(--color-ink-4)", dashed: true },
              { name: "Сценарий", values: s.scenario, color: s.code === "cash" ? "var(--color-accent)" : "var(--color-violet)" },
            ]}
            format={(v) => simValue(v, s.unit)}
            height={150}
          />
        </div>
      ))}
    </div>
  );
}

/** Источники, использованные значения, допущения, цепочка пересчёта и история расчётов. */
function SourcesView({ result, nodes, baseline, history }: { result: SimulationResult; nodes: GraphNode[]; baseline: BaselineView | null; history: ScenarioHistoryEntry[] }) {
  const t = useT();
  const chain = chainTitles(result.chain, nodes);
  const h4 = "mb-1.5 text-[10px] font-bold uppercase tracking-wide text-ink-4";
  return (
    <div className="space-y-5 px-4 pb-5 md:px-5">
      {chain.length > 0 && (
        <div>
          <h4 className={h4}>{t("Пересчитано по цепочке")}</h4>
          <div className="flex flex-wrap items-center gap-1 text-[11.5px] text-ink-3">
            {chain.map((c, i) => (
              <span key={c} className="inline-flex items-center gap-1">
                {i > 0 && <ArrowRight className="size-3 text-ink-4" aria-hidden />}
                <span className="rounded-md bg-muted px-1.5 py-0.5">{t(c)}</span>
              </span>
            ))}
          </div>
        </div>
      )}
      <div className="grid gap-5 @[640px]/res:grid-cols-2">
        <div>
          <h4 className={h4}>{t("Источники")}</h4>
          <ul className="space-y-1 text-[12.5px] text-ink-2">
            {result.conclusion.sources.map((s) => (
              <li key={s}>{t(s)}</li>
            ))}
          </ul>
          {result.inputs.length > 0 && (
            <>
              <h4 className={`${h4} mt-4`}>{t("Использованные значения")}</h4>
              <ul className="space-y-1 text-[12.5px] text-ink-2">
                {result.inputs.map((i) => (
                  <li key={i.code}>
                    {t(i.title)}: <span className="font-semibold tabular-nums text-ink">{i.value != null ? `${num(i.value, 2)} ${i.unit}` : "—"}</span> <span className="text-ink-4">({t(i.origin)})</span>
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>
        <div>
          <h4 className={h4}>{t("Допущения")}</h4>
          <ul className="list-disc space-y-1 pl-4 text-[12.5px] text-ink-2">
            {result.conclusion.assumptions.map((a) => (
              <li key={a}>{t(a)}</li>
            ))}
          </ul>
          {baseline && baseline.notes.length > 0 && (
            <>
              <h4 className={`${h4} mt-4`}>{t("Замечания к данным")}</h4>
              <ul className="list-disc space-y-1 pl-4 text-[12.5px] text-ink-2">
                {baseline.notes.map((n) => (
                  <li key={n}>{t(n)}</li>
                ))}
              </ul>
            </>
          )}
        </div>
      </div>
      {history.length > 0 && (
        <div>
          <h4 className={h4}>{t("История расчётов")}</h4>
          <ul className="space-y-1.5 text-[12.5px]">
            {history.map((h) => (
              <li key={`${h.revision}-${h.baselineId}`} className="rounded-lg bg-muted px-3 py-2">
                <div className="text-ink-3">
                  {t("Расчёт №{n}", { n: h.revision })} · {dateTime(h.at)} · {t("база")} {date(h.from)} — {date(h.to)} · {t("данные на")} {date(h.asOf)}
                </div>
                {h.headline && <div className="mt-0.5 text-ink-2">{t(h.headline)}</div>}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

// ---------- Риски и рекомендации ----------

type RecAction = { label: string; icon: Icon; href?: string; action?: "inputs" | "params" };

/** Действие к рекомендации движка — по её смыслу (тексты рекомендаций собирает сервер). */
function recommendationAction(text: string): RecAction | null {
  if (/техкарт/i.test(text)) return { label: "Открыть техкарты", icon: ClipboardList, href: "/production/recipes" };
  if (/приход/i.test(text)) return { label: "Открыть приходы", icon: FileText, href: "/procurement/receipts" };
  if (/срок[и]? поставки|поставщик/i.test(text)) return { label: "Открыть поставщиков", icon: Truck, href: "/procurement/suppliers" };
  if (/эластичност/i.test(text)) return { label: "Задать эластичность", icon: Pencil, action: "params" };
  if (/введите|мощност|зарплат|деньги на начало/i.test(text)) return { label: "Ввести данные", icon: Pencil, action: "inputs" };
  return null;
}

export function RisksCard({ result, className = "" }: { result: SimulationResult; className?: string }) {
  const t = useT();
  return (
    <section className={`p-5 ${card} ${className}`}>
      <h3 className="flex items-center gap-2 text-[15px] font-bold text-ink">
        {t("Риски")}
        {result.risks.length > 0 && <span className="rounded-md bg-warn-soft px-1.5 text-[11px] font-bold text-warn">{result.risks.length}</span>}
      </h3>
      {result.risks.length === 0 ? (
        <p className="mt-3 flex items-center gap-2 text-[13px] text-ok">
          <CircleCheck className="size-4" aria-hidden />
          {t("Рисков не найдено.")}
        </p>
      ) : (
        <ul className="mt-3 space-y-2">
          {result.risks.map((r) => (
            <li key={r.code} className="flex gap-3 rounded-xl bg-muted p-3">
              <span className={`grid size-[30px] shrink-0 place-items-center rounded-lg ${r.level === "critical" ? "bg-bad-soft text-bad" : "bg-warn-soft text-warn"}`}>
                <ShieldAlert className="size-4" aria-hidden />
              </span>
              <span className="min-w-0">
                <span className="block text-[13px] font-semibold text-ink">{t(r.title)}</span>
                <span className="mt-0.5 block text-[12px] text-ink-3">{t(r.text)}</span>
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

export function RecommendationsCard({ result, onAction, className = "" }: { result: SimulationResult; onAction: (a: "inputs" | "params") => void; className?: string }) {
  const t = useT();
  return (
    <section className={`p-5 ${card} ${className}`}>
      <h3 className="text-[15px] font-bold text-ink">{t("Рекомендации")}</h3>
      <ul className="mt-3 space-y-2">
        {result.conclusion.recommendations.map((r, i) => {
          const a = recommendationAction(r);
          const AIcon = a?.icon;
          return (
            <li key={i} className="flex gap-3 rounded-xl bg-muted p-3">
              <span className="grid size-[30px] shrink-0 place-items-center rounded-lg bg-accent-soft text-accent-strong">
                <Lightbulb className="size-4" aria-hidden />
              </span>
              <span className="min-w-0">
                <span className="block text-[12px] leading-relaxed text-ink-2">{t(r)}</span>
                {a && AIcon && (a.href ? (
                  <Link href={a.href} className="mt-1 inline-flex items-center gap-1 text-[11px] font-semibold text-accent-strong hover:underline">
                    <AIcon className="size-3.5" aria-hidden />
                    {t(a.label)}
                  </Link>
                ) : (
                  <button type="button" onClick={() => onAction(a.action!)} className="mt-1 inline-flex items-center gap-1 text-[11px] font-semibold text-accent-strong hover:underline">
                    <AIcon className="size-3.5" aria-hidden />
                    {t(a.label)}
                  </button>
                ))}
              </span>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

/** Телефон: риски и рекомендации одним блоком. */
export function RisksAndRecsCompact({ result, onAction }: { result: SimulationResult; onAction: (a: "inputs" | "params") => void }) {
  const t = useT();
  const items = [
    ...result.risks.map((r) => ({ key: `r-${r.code}`, risk: true, text: `${t(r.title)} — ${lowerFirst(t(r.text))}`, action: null as RecAction | null })),
    ...result.conclusion.recommendations.map((r, i) => ({ key: `c-${i}`, risk: false, text: t(r), action: recommendationAction(r) })),
  ];
  if (items.length === 0) return null;
  return (
    <section className={`p-4 md:hidden ${card}`}>
      <h3 className="text-[15px] font-bold text-ink">{t("Риски и рекомендации")}</h3>
      <ul className="mt-3 space-y-2">
        {items.map((x) => {
          const AIcon = x.action?.icon;
          return (
            <li key={x.key} className="flex gap-3 rounded-xl bg-muted p-3">
              <span className={`grid size-7 shrink-0 place-items-center rounded-lg ${x.risk ? "bg-warn-soft text-warn" : "bg-accent-soft text-accent-strong"}`}>
                {x.risk ? <ShieldAlert className="size-3.5" aria-hidden /> : <Lightbulb className="size-3.5" aria-hidden />}
              </span>
              <span className="min-w-0 text-[13px] leading-snug text-ink-2">
                {x.text}
                {x.action && AIcon && (
                  <span className="mt-1 block">
                    {x.action.href ? (
                      <Link href={x.action.href} className="inline-flex items-center gap-1 text-[12px] font-semibold text-accent-strong">
                        <AIcon className="size-3.5" aria-hidden />
                        {t(x.action.label)}
                      </Link>
                    ) : (
                      <button type="button" onClick={() => onAction(x.action!.action!)} className="inline-flex items-center gap-1 text-[12px] font-semibold text-accent-strong">
                        <AIcon className="size-3.5" aria-hidden />
                        {t(x.action.label)}
                      </button>
                    )}
                  </span>
                )}
              </span>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

// ---------- AI-анализ ----------

export function AiPanel({
  text,
  loading,
  error,
  stale,
  savedAt,
  disabled,
  onRun,
}: {
  text: string | null;
  loading: boolean;
  error: string | null;
  stale: boolean;
  savedAt: string | null;
  disabled: boolean;
  onRun: () => void;
}) {
  const t = useT();
  const sections = text ? aiSections(text) : [];
  const button = (
    <button
      type="button"
      onClick={onRun}
      disabled={disabled || loading}
      className="inline-flex h-10 items-center justify-center gap-2 rounded-xl bg-[linear-gradient(90deg,#3d7bff,#8b5cf6)] px-4 text-[13px] font-semibold text-white shadow-[0_6px_20px_-8px_rgb(139_92_246/0.8)] hover:brightness-110 disabled:opacity-50 max-md:w-full"
    >
      {loading ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Sparkles className="size-4" aria-hidden />}
      {text ? t("Обновить анализ") : t("Получить AI-анализ")}
    </button>
  );
  return (
    <section className={`relative overflow-hidden border-violet/25 p-4 md:p-5 ${card}`}>
      <div aria-hidden className="pointer-events-none absolute inset-0 bg-[linear-gradient(135deg,rgb(139_92_246/0.10),transparent_55%)]" />
      <div className="relative flex flex-wrap items-center gap-3">
        <span className="grid size-[38px] shrink-0 place-items-center rounded-xl bg-[linear-gradient(135deg,#5b97ff,#8b5cf6)] text-white">
          <Sparkles className="size-[18px]" aria-hidden />
        </span>
        <div className="min-w-0 flex-1">
          <h3 className="flex flex-wrap items-center gap-2 text-[15px] font-bold text-ink">
            {t("AI-анализ результатов")}
            {text && stale && <span className="rounded-md bg-warn-soft px-1.5 py-0.5 text-[10px] font-bold text-warn">{t("устарел")}</span>}
          </h3>
          <p className="text-[12px] text-ink-3">
            <span className="max-md:hidden">{t("Вывод, изменения, ограничения, риски и рекомендации по числам расчёта — один вызов модели")}</span>
            <span className="md:hidden">{t("объяснит числа и предложит действия")}</span>
          </p>
        </div>
        <div className="max-md:hidden">{button}</div>
      </div>
      <div className="relative mt-3 md:hidden">{button}</div>
      <div className="relative mt-3" aria-live="polite">
        {error && <p className="mb-2 rounded-xl bg-bad-soft px-3 py-2 text-[13px] text-bad">{error}</p>}
        {loading ? (
          <div className="space-y-2 rounded-xl bg-muted/60 p-3">
            <p className="flex items-center gap-1.5 text-[12px] font-medium text-violet">
              <Loader2 className="size-3.5 animate-spin" aria-hidden />
              {t("AI анализирует…")}
            </p>
            {[92, 78, 85, 60].map((w) => (
              <div key={w} className="h-3 animate-pulse rounded bg-line-strong/60" style={{ width: `${w}%` }} />
            ))}
          </div>
        ) : text ? (
          <>
            {stale && <p className="mb-2 text-[12px] text-warn">{t("Параметры изменились после анализа — обновите его, чтобы AI объяснил новые числа.")}</p>}
            <div className="grid gap-2 @[640px]/res:grid-cols-2">
              {sections.map((s, i) => (
                <div key={i} className={`rounded-xl bg-muted/70 p-3 ${s.title && /вывод/i.test(s.title) ? "@[640px]/res:col-span-2" : ""}`}>
                  {s.title && <div className="mb-1 text-[12px] font-bold text-ink">{s.title}</div>}
                  <div className="space-y-1 text-[13px] leading-relaxed text-ink-2">
                    {s.paragraphs.map((p, j) => (
                      <p key={j}>{p.map((part, k) => (part.bold ? <strong key={k} className="font-semibold text-ink">{part.text}</strong> : <span key={k}>{part.text}</span>))}</p>
                    ))}
                  </div>
                </div>
              ))}
            </div>
            {savedAt && !stale && <p className="mt-2 text-[11px] text-ink-4">{t("Сохранено в сценарии")} · {dateTime(savedAt)}</p>}
          </>
        ) : (
          <p className="flex items-start gap-2 rounded-xl border border-line bg-page/50 px-3 py-2.5 text-[12px] text-ink-3">
            <Info className="mt-px size-4 shrink-0" aria-hidden />
            {t("Анализа ещё нет. Числа уже посчитаны движком выше — AI объяснит их и предложит действия.")}
          </p>
        )}
      </div>
    </section>
  );
}

// ---------- Исходные данные снимка (поповер «Данные на») ----------

export function BaselineDetails({ baseline }: { baseline: BaselineView }) {
  const t = useT();
  const s = baseline.summary;
  const tiles: [string, string, string?][] = [
    ["Выручка базы", simValue(s.revenue, "сум"), `${num(s.kg, 0)} ${t("кг")}`],
    ["Покрыто техкартами", s.recipeShare != null ? `${num(s.recipeShare * 100, 1)}%` : "—", `${s.productsWithRecipe} / ${s.products} ${t("товаров")}`],
    ["Мощность", s.capacityKgDay != null ? `${num(s.capacityKgDay, 0)} ${t("кг/сут")}` : t("нет данных"), s.capacityKgDay != null ? t("по сменам") : t("введите в исходных данных")],
    ["Сырьё с ценой", `${s.materialsPriced} / ${s.materials}`, s.materialsLeadAssumed > 0 ? `${s.materialsLeadAssumed} ${t("без срока поставки")}` : undefined],
    ["Оплаты к выручке", s.collectionRate != null ? `${num(s.collectionRate * 100, 1)}%` : "—", s.paymentLagDays != null ? `${t("срок")} ${num(s.paymentLagDays, 1)} ${t("дн.")}` : undefined],
    ["Численность", s.employees != null ? `${num(s.employees, 0)} ${t("чел.")}` : "—", s.divisions ? `${s.divisions} ${t("подразделений")}` : undefined],
  ];
  return (
    <div className="space-y-3">
      <p className="text-[12px] text-ink-3">
        {t("База")} {date(baseline.from)} — {date(baseline.to)} ({baseline.days} {t("дн.")}) · {t("данные на")} {date(baseline.asOf)}
        {baseline.salesScope ? ` · ${t("область")}: ${baseline.salesScope}` : ""}
      </p>
      <ul className="space-y-1.5">
        {baseline.sources.map((x) => (
          <li key={x.code} className="flex items-start gap-2 text-[12px]">
            <span className={`mt-1.5 size-2 shrink-0 rounded-full ${x.status === "ok" ? "bg-ok" : "bg-warn"}`} aria-hidden />
            <span className="min-w-0 flex-1">
              <span className="font-medium text-ink">{t(x.title)}</span>
              <span className="text-ink-4">{x.updatedAt ? ` · ${t("загружено")} ${new Date(x.updatedAt).toLocaleString("ru-RU", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}` : ""}</span>
              {x.note && <span className="block text-ink-3">{t(x.note)}</span>}
            </span>
          </li>
        ))}
      </ul>
      <div className="grid grid-cols-2 gap-2">
        {tiles.map(([k, v, sub]) => (
          <div key={k} className="rounded-xl bg-muted px-3 py-2">
            <div className="text-[11px] text-ink-3">{t(k)}</div>
            <div className="text-[14px] font-semibold tabular-nums text-ink">{v}</div>
            {sub && <div className="text-[11px] text-ink-4">{sub}</div>}
          </div>
        ))}
      </div>
    </div>
  );
}

export { groupLabel };
