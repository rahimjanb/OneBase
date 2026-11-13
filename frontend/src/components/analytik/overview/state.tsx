"use client";

import Link from "next/link";
import { useState } from "react";
import {
  AlarmClock,
  Banknote,
  ChevronRight,
  CircleCheck,
  Clock,
  Factory,
  HandCoins,
  OctagonAlert,
  Package,
  ShoppingCart,
  Sparkles,
  Target,
  TriangleAlert,
  Truck,
  UserX,
  type LucideIcon,
} from "lucide-react";
import { useT } from "@/i18n/client";
import {
  change,
  changeTone,
  dateTime,
  healthFactors,
  healthLead,
  kpiParts,
  num,
  planDays,
  plural,
  shortDate,
  type Dashboard,
  type Health,
  type Kpi,
  type Period,
} from "@/lib/analytik";
import { DeltaChip, SourceIcon, SourceLegend, toneText } from "./parts";

// ---------- Состояние компании ----------

const levelStyle = {
  critical: { ring: "var(--color-bad)", pill: "bg-bad-soft text-bad", icon: OctagonAlert, label: "Критическое" },
  attention: { ring: "var(--color-warn)", pill: "bg-warn-soft text-warn", icon: TriangleAlert, label: "Требует внимания" },
  good: { ring: "var(--color-ok)", pill: "bg-ok-soft text-ok", icon: CircleCheck, label: "Под контролем" },
} as const;

function Ring({ health, size }: { health: Health; size: number }) {
  const t = useT();
  const style = levelStyle[health.level] ?? levelStyle.attention;
  const r = 44;
  const length = 2 * Math.PI * r;
  return (
    <svg
      viewBox="0 0 100 100"
      width={size}
      height={size}
      className="shrink-0"
      role="img"
      aria-label={t("Состояние компании: {score} из 100, {level}", { score: health.score, level: t(style.label).toLowerCase() })}
    >
      <circle cx="50" cy="50" r={r} fill="none" stroke="var(--color-muted)" strokeWidth="8" />
      <circle
        cx="50"
        cy="50"
        r={r}
        fill="none"
        stroke={style.ring}
        strokeWidth="8"
        strokeLinecap="round"
        strokeDasharray={`${(Math.max(0, Math.min(100, health.score)) / 100) * length} ${length}`}
        transform="rotate(-90 50 50)"
      />
      <text x="50" y="52" textAnchor="middle" className="fill-ink" style={{ fontSize: 30, fontWeight: 700 }}>
        {health.score}
      </text>
      <text x="50" y="68" textAnchor="middle" className="fill-ink-3" style={{ fontSize: 10 }}>
        {t("из 100")}
      </text>
    </svg>
  );
}

/**
 * «Состояние компании»: кольцо 0–100 цветом уровня, пилюля уровня и главное влияние; ниже — что снижает оценку (штраф и полоса от самого
 * большого), строка ведёт в свой список. Пользователь видит не всё — «Состояние ваших разделов» и что вошло в оценку.
 */
export function HealthCard({ data }: { data: Dashboard }) {
  const t = useT();
  const h = data.health;
  const style = levelStyle[h.level] ?? levelStyle.attention;
  const Icon = style.icon;
  const factors = healthFactors(h);
  const lead = healthLead(h);
  const title = h.coverage ? "Состояние ваших разделов" : "Состояние компании";
  const pill = (
    <span className={`inline-flex w-fit items-center gap-1.5 whitespace-nowrap rounded-lg px-2 py-[3px] text-[12px] font-bold ${style.pill}`}>
      <Icon className="size-3.5" aria-hidden />
      {t(style.label)}
    </span>
  );
  const text =
    lead.length > 0 ? t("Главное влияние — {what}.", { what: lead.map((x) => t(x).toLowerCase()).join(` ${t("и")} `) }) : t("Рисков и отклонений, снижающих оценку, нет.");
  return (
    <section className="flex h-full min-w-0 flex-col rounded-[18px] border border-line bg-surface p-5 max-md:p-4">
      <header className="mb-3.5 max-md:hidden">
        <h2 className="text-[15px] font-bold leading-tight text-ink">{t(title)}</h2>
        <p className="mt-1 text-[11px] font-medium text-ink-4">
          {data.period.label} · {t("на")} {dateTime(data.at)}
        </p>
      </header>
      {/* Ноутбук (состояние во всю ширину над показателями): кольцо слева, факторы справа. */}
      <div className="grid gap-4 lg:@max-[1099px]:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] lg:@max-[1099px]:gap-8">
        <div className="flex items-center gap-4">
          <span className="max-md:hidden">
            <Ring health={h} size={112} />
          </span>
          <span className="md:hidden">
            <Ring health={h} size={92} />
          </span>
          <div className="min-w-0 space-y-2">
            <h2 className="text-[15px] font-bold leading-tight text-ink md:hidden">{t(title)}</h2>
            {pill}
            <p className="text-[12px] leading-relaxed text-ink-2">{text}</p>
            {h.coverage && <p className="text-[11px] leading-snug text-ink-4">{t(h.coverage)}</p>}
          </div>
        </div>

        {factors.length > 0 && (
          <div className="border-t border-line pt-3.5 lg:@max-[1099px]:border-t-0 lg:@max-[1099px]:pt-0">
            <p className="mb-2.5 text-[10px] font-bold uppercase tracking-wider text-ink-4 max-md:hidden">{t("Что снижает оценку")}</p>
            <ul className="space-y-3 max-md:space-y-2">
              {factors.map((f) => {
                const tone = f.severity === "danger" ? "bad" : "warn";
                return (
                  <li key={f.label}>
                    <Link href={f.href} className="group block">
                      <div className="flex items-baseline justify-between gap-3">
                        <span className="min-w-0 truncate">
                          <span aria-hidden className={`mr-2 inline-block size-1.5 rounded-full align-middle md:hidden ${tone === "bad" ? "bg-bad" : "bg-warn"}`} />
                          <span className="text-[12px] font-semibold text-ink group-hover:underline max-md:font-medium max-md:text-ink-2">{t(f.label)}</span>
                          <span className="ml-1.5 text-[11px] text-ink-4 max-md:ml-1">
                            <span className="md:hidden">· </span>
                            {f.sub}
                          </span>
                        </span>
                        <span className={`shrink-0 text-[12px] font-bold tabular-nums ${toneText[tone]}`}>−{f.penalty}</span>
                      </div>
                      <div className="mt-1.5 h-1 overflow-hidden rounded-full bg-muted max-md:hidden">
                        <div className={`h-full rounded-full ${tone === "bad" ? "bg-bad" : "bg-warn"}`} style={{ width: `${Math.max(6, f.share * 100)}%` }} />
                      </div>
                    </Link>
                  </li>
                );
              })}
            </ul>
          </div>
        )}
        {data.errors.length > 0 && <p className="text-[11px] text-warn">{t("Не удалось посчитать")}: {data.errors.join(", ")}</p>}
      </div>
    </section>
  );
}

// ---------- Главные показатели ----------

const months = ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];
const monthsShort = ["янв.", "фев.", "мар.", "апр.", "мая", "июн.", "июл.", "авг.", "сен.", "окт.", "ноя.", "дек."];

/** Подпись сравнения у чипа: месяц — «к окт.», год — «к 2025», иначе — к предыдущему такому же периоду. */
function compareCaption(period: Period, t: (s: string, p?: Record<string, string | number>) => string): string {
  const [y, m] = period.previousFrom.split("-").map(Number);
  switch (period.kind) {
    case "month":
      return `${t("к")} ${t(monthsShort[(m || 1) - 1])}`;
    case "year":
      return `${t("к")} ${y}`;
    case "today":
    case "yesterday":
      return t("к пред. дню");
    case "week":
      return t("к пред. неделе");
    case "quarter":
      return t("к пред. кварталу");
    default:
      return t("к пред. периоду");
  }
}

type Tile = { kpi: Kpi; label: string; icon: LucideIcon; hint: { text: string; tone: "ok" | "bad" | "muted" } | null; danger: boolean; mobile: boolean };

/** Главные показатели панели: 12 из блоков отделов — то, что видно пользователю, в порядке макета; закупки — из плана снабжения. */
export function headlineKpis(d: Dashboard): Kpi[] {
  const pick = (kpis: Kpi[] | undefined, ...codes: string[]) => codes.map((c) => kpis?.find((k) => k.code === c)).filter((k): k is Kpi => !!k);
  const need30 = d.supply?.horizons.find((h) => h.days === 30);
  const supply: Kpi[] = d.supply
    ? [
        {
          code: "procurement.budget30",
          title: "Закупки на 30 дней",
          // Бюджет неизвестен, если ни у одной нужной позиции нет цены — «нет данных», а не ноль.
          value: need30 && need30.items > 0 && need30.itemsWithoutPrice === need30.items ? null : (need30?.budget ?? null),
          unit: "сум",
          previous: null,
          changePercent: null,
          kind: "calculated",
          source: "Аналитик → План закупок",
          href: "/analytik/procurement",
          note: null,
        },
        {
          code: "procurement.critical",
          title: "Сырьё в дефиците",
          value: d.supply.risks.critical ?? 0,
          unit: "позиций",
          previous: null,
          changePercent: null,
          kind: "calculated",
          source: "Аналитик → План закупок",
          href: "/analytik/procurement",
          note: null,
        },
      ]
    : [];
  return [
    ...pick(d.sales?.kpis, "sales.revenue", "sales.kg", "sales.plan_execution", "sales.forecast_execution"),
    ...pick(d.finance?.kpis, "finance.payments", "finance.procurement_spend"),
    ...supply,
    ...pick(d.production?.kpis, "production.execution", "production.overdue_orders"),
    ...pick(d.hr?.kpis, "hr.late", "hr.absent_today"),
  ];
}

const mobileCodes = ["sales.revenue", "sales.kg", "sales.plan_execution", "sales.forecast_execution", "finance.payments", "procurement.critical", "hr.late", "hr.absent_today"];

function tiles(d: Dashboard, t: (s: string, p?: Record<string, string | number>) => string): Tile[] {
  const plan = d.sales?.plan ?? null;
  const monthName = plan ? t(months[plan.month - 1]) : null;
  const need30 = d.supply?.horizons.find((h) => h.days === 30);
  const firstShortage = (d.supply?.top ?? []).filter((r) => r.risk === "critical").map((r) => r.shortageDate).filter((x): x is string => !!x).sort()[0] ?? null;
  const active = d.hr?.kpis.find((k) => k.code === "hr.active")?.value ?? null;
  const conf: Record<string, { label: string; icon: LucideIcon; hint?: (k: Kpi) => Tile["hint"]; danger?: (k: Kpi) => boolean }> = {
    "sales.revenue": { label: "Выручка", icon: Banknote },
    "sales.kg": { label: "Продажи", icon: Package },
    "sales.plan_execution": {
      label: monthName ? `${t("План")} (${monthName})` : t("План"),
      icon: Target,
      hint: () => (plan ? { text: t("прошло {p}% месяца", { p: num(planDays(plan).percent, 0) }), tone: "muted" } : null),
    },
    "sales.forecast_execution": {
      label: t("Прогноз плана"),
      icon: Sparkles,
      hint: () => (plan ? { text: t("к {date} при текущем темпе", { date: shortDate(new Date(Date.UTC(plan.year, plan.month, 0)).toISOString()) }), tone: "muted" } : null),
    },
    "finance.payments": { label: "Оплаты точек", icon: HandCoins },
    "finance.procurement_spend": { label: "Расходы на закупки", icon: ShoppingCart },
    "procurement.budget30": {
      label: "Закупки на 30 дней",
      icon: Truck,
      hint: () =>
        need30
          ? {
              text: `${num(need30.items)} ${t(plural(need30.items, "позиция", "позиции", "позиций"))}${need30.itemsWithoutPrice ? `, ${t("без цены")} — ${need30.itemsWithoutPrice}` : ""}`,
              tone: "muted",
            }
          : null,
    },
    "procurement.critical": {
      label: "Сырьё в дефиците",
      icon: TriangleAlert,
      danger: (k) => (k.value ?? 0) > 0,
      hint: (k) =>
        (k.value ?? 0) > 0
          ? firstShortage
            ? { text: t("закончится {date}", { date: shortDate(firstShortage) }), tone: "bad" }
            : { text: t("уже нет в наличии"), tone: "bad" }
          : { text: t("дефицита нет"), tone: "ok" },
    },
    "production.execution": { label: "План выпуска", icon: Factory },
    "production.overdue_orders": { label: "Просроченные задания", icon: Clock, hint: (k) => (k.value === 0 ? { text: t("всё в срок"), tone: "ok" } : null) },
    "hr.late": { label: "Опоздания", icon: AlarmClock },
    "hr.absent_today": {
      label: "Отсутствуют сегодня",
      icon: UserX,
      hint: () => (active != null && active > 0 ? { text: t("из {n} сотрудников", { n: num(active) }), tone: "muted" } : null),
    },
  };
  return headlineKpis(d).map((kpi) => {
    const c = conf[kpi.code] ?? { label: kpi.title, icon: Target };
    const hint = c.hint?.(kpi) ?? (kpi.note ? { text: t(kpi.note), tone: "muted" as const } : null);
    return { kpi, label: t(c.label), icon: c.icon, hint, danger: c.danger?.(kpi) ?? false, mobile: mobileCodes.includes(kpi.code) };
  });
}

function KpiTile({ tile, caption }: { tile: Tile; caption: string }) {
  const t = useT();
  const { kpi } = tile;
  const parts = kpiParts(kpi.value, kpi.unit);
  const delta = change(kpi.changePercent);
  const tone = changeTone(kpi.code, kpi.changePercent);
  const Icon = tile.icon;
  const danger = tile.danger;
  const body = (
    <div
      className={`flex h-full min-h-[104px] min-w-0 flex-col gap-2 rounded-[14px] border px-3.5 py-3 transition-colors max-md:min-h-[96px] max-md:gap-1.5 max-md:px-3 max-md:py-2.5 ${
        danger ? "border-bad/25 bg-bad-soft" : "border-line bg-muted"
      } ${kpi.href ? "hover:border-line-strong" : ""}`}
    >
      <div className="flex items-start justify-between gap-2">
        <span className="flex min-w-0 items-start gap-1.5">
          <Icon className={`mt-px size-[13px] shrink-0 max-md:hidden ${danger ? "text-bad" : "text-ink-4"}`} aria-hidden />
          <span className={`line-clamp-2 min-w-0 text-[12px] font-medium leading-[15px] max-md:text-[11px] ${danger ? "text-bad" : "text-ink-3"}`}>{tile.label}</span>
        </span>
        <span className="max-md:hidden">
          <SourceIcon kind={kpi.kind} source={kpi.source} />
        </span>
        <span className="md:hidden">
          <SourceIcon kind={kpi.kind} source={kpi.source} small />
        </span>
      </div>
      {parts ? (
        <div className="flex min-w-0 items-baseline gap-1.5">
          <span className={`text-[22px] font-bold leading-none tracking-[-0.3px] tabular-nums max-md:text-[20px] ${danger ? "text-bad" : "text-ink"}`}>{parts.value}</span>
          {parts.unit && <span className="truncate text-[12px] font-medium text-ink-3 max-md:text-[11px]">{t(parts.unit)}</span>}
        </div>
      ) : (
        <span className="text-[16px] font-semibold leading-none text-ink-4">{t("Нет данных")}</span>
      )}
      <div className="mt-auto flex min-h-[18px] min-w-0 items-center gap-1.5">
        {delta && parts ? (
          <>
            <DeltaChip text={delta.replace(/\s%$/, "%")} tone={tone} up={(kpi.changePercent ?? 0) > 0} />
            <span className="truncate text-[11px] text-ink-4 max-md:hidden">{caption}</span>
          </>
        ) : tile.hint ? (
          <span className={`truncate text-[11px] font-medium ${tile.hint.tone === "ok" ? "text-ok" : tile.hint.tone === "bad" ? "text-bad" : "text-ink-4"}`}>{tile.hint.text}</span>
        ) : null}
      </div>
    </div>
  );
  return kpi.href ? (
    <Link href={kpi.href} className="block h-full rounded-[14px] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent/60" title={kpi.source}>
      {body}
    </Link>
  ) : (
    body
  );
}

/**
 * «Главные показатели»: 12 карточек одной высоты (компьютер 4 × 3, ноутбук 3 × 4, планшет и телефон — 2 колонки), у каждой — источник
 * (факт / расчёт / прогноз), изменение с правильным цветом или подсказка; нажатие — источник данных. На телефоне — 8 ключевых и «Все 12».
 */
export function KpiPanel({ data }: { data: Dashboard }) {
  const t = useT();
  const [all, setAll] = useState(false);
  const list = tiles(data, t);
  const caption = compareCaption(data.period, t);
  const hiddenOnPhone = list.filter((x) => !x.mobile).length;
  return (
    <section className="flex h-full min-w-0 flex-col md:rounded-[18px] md:border md:border-line md:bg-surface md:p-5">
      <header className="mb-3.5 flex items-start justify-between gap-3 max-md:mb-2.5 max-md:items-center">
        <div className="min-w-0">
          <h2 className="text-[15px] font-bold text-ink max-md:text-[17px]">{t("Главные показатели")}</h2>
          <p className="mt-1 text-[11px] font-medium text-ink-4 max-md:hidden">{t("Нажмите на показатель — откроется источник данных")}</p>
        </div>
        <span className="max-md:hidden">
          <SourceLegend />
        </span>
        {hiddenOnPhone > 0 && (
          <button type="button" onClick={() => setAll(!all)} className="inline-flex items-center gap-0.5 text-[13px] font-semibold text-accent-strong md:hidden" aria-expanded={all}>
            {all ? t("Главные") : t("Все {n}", { n: list.length })}
            <ChevronRight className={`size-4 transition-transform ${all ? "-rotate-90" : ""}`} aria-hidden />
          </button>
        )}
      </header>
      {list.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line px-4 py-8 text-center text-[13px] text-ink-3">{t("Нет показателей, доступных по вашим правам")}</p>
      ) : (
        <div className="grid flex-1 auto-rows-fr grid-cols-2 gap-2.5 max-md:gap-2 lg:grid-cols-3 lg:@min-[900px]:grid-cols-4">
          {list.map((tile) => (
            <div key={tile.kpi.code} className={!tile.mobile && !all ? "max-md:hidden" : undefined}>
              <KpiTile tile={tile} caption={caption} />
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
