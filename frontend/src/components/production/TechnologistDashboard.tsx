"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useState, useTransition } from "react";
import {
  Boxes,
  ChevronLeft,
  ChevronRight,
  CircleX,
  ClipboardList,
  Croissant,
  Hammer,
  Layers,
  Package,
  PackageCheck,
  PackageX,
  RotateCw,
  Scale,
  Target,
  TriangleAlert,
  type LucideIcon,
} from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { NotificationBell } from "@/components/shell/NotificationBell";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import { compactMoney, money } from "@/lib/procurement";
import { dayRu, type ProductionDashboard } from "@/lib/production";
import {
  barsTotal,
  completionTone,
  consumptionRatio,
  consumptionTone,
  counters,
  defectTone,
  kg,
  parsePeriod,
  pct,
  quickLinks,
  reportHref,
  type TechPeriod,
} from "@/lib/technologist-view";
import { OrderStatusPill } from "./ProdKit";
import { AllLink, AttentionList, BarRows, CounterChips, CounterList, DailyChart, EmptyRow, Kpi, NormRows, PeriodSwitch, QuickLinkCard, Skel } from "./TechnologistParts";

type T = ReturnType<typeof useT>;
type State = { data: ProductionDashboard; loading: false } | { data: null; loading: boolean };

/** Стоимость брака по цене партий (как и раньше): сумма; без цены партии — сколько материалов не посчитано (их список — в подсказке). */
function defectCost(data: ProductionDashboard, t: T): { text: string; title?: string } | null {
  const missing = data.defectCostUnpriced;
  const units = { bn: t("млрд"), mn: t("млн"), th: t("тыс.") };
  // Сумма — компактно («3,6 млн сум»), полная — в подсказке вместе с материалами без цены партии.
  const full = data.defectCost !== null ? t("{s} сум", { s: money(data.defectCost) }) : null;
  const title = [full, missing.length > 0 ? `${t("Без цены партии")}: ${missing.join(", ")}` : null].filter(Boolean).join(" · ") || undefined;
  if (data.defectCost !== null) {
    const s = compactMoney(data.defectCost, units);
    return { text: missing.length > 0 ? t("{s} сум · без цены: {n}", { s, n: missing.length }) : t("{s} сум", { s }), title };
  }
  return missing.length > 0 ? { text: t("стоимость: нет цены партии"), title } : null;
}

type KpiSpec = { key: string; icon: LucideIcon; label: string; short: string; value: string | null; unit?: string; note?: string | null; shortNote?: string | null; extra?: { text: string; title?: string } | null; tone?: ReturnType<typeof completionTone> };

function kpis(d: ProductionDashboard | null, t: T): KpiSpec[] {
  const ratio = d ? consumptionRatio(d.normDeviationPercent) : null;
  return [
    { key: "today", icon: PackageCheck, label: "Выпуск сегодня", short: "Выпуск сегодня", value: d ? kg(d.todayKg) : null, unit: "кг" },
    {
      key: "period", icon: Boxes, label: "Выпуск за период", short: "За период", value: d ? kg(d.goodKg) : null, unit: "кг",
      note: d ? (d.planKg > 0 ? t("план: {kg} кг", { kg: kg(d.planKg) }) : t("плана на период нет")) : null,
      shortNote: d ? (d.planKg > 0 ? t("план: {kg} кг", { kg: kg(d.planKg) }) : t("плана нет")) : null,
    },
    { key: "plan", icon: Target, label: "Выполнение плана", short: "План", value: d ? pct(d.completionPercent) : null, tone: d ? completionTone(d.completionPercent) : null },
    {
      key: "defect", icon: CircleX, label: "Брак", short: "Брак", value: d ? kg(d.defectKg) : null, unit: "кг", tone: d ? defectTone(d.defectKg) : null,
      note: d?.defectPercent != null ? t("{p} от выпуска", { p: pct(d.defectPercent) ?? "" }) : null,
      extra: d ? defectCost(d, t) : null,
    },
    {
      key: "norm", icon: Scale, label: "Расход к норме", short: "Расход к норме", value: ratio === null ? null : pct(ratio), tone: d ? consumptionTone(d.normDeviationPercent) : null,
      note: d ? (d.normKg > 0 ? t("норма {a} · факт {b}", { a: kg(d.normKg), b: kg(d.factKg) }) : t("закрытых заданий нет")) : null,
      shortNote: d ? (d.normKg > 0 ? t("норма {a} · факт {b}", { a: kg(d.normKg), b: kg(d.factKg) }) : t("заданий нет")) : null,
    },
    { key: "bake", icon: Croissant, label: "Выход после выпечки", short: "Выход выпечки", value: d ? pct(d.bakeYieldPercent) : null },
  ];
}

type SectionKey = "products" | "stages" | "types" | "shifts" | "packaging" | "norm" | "orders";
type SectionMeta = { key: SectionKey; title: string; icon: LucideIcon; empty: string; mobileEmpty: string; forms: [string, string, string] };

/** Разделы: название, иконка, пустое состояние (тексты макета) и как считать строки в итоге на телефоне. */
const SECTIONS: SectionMeta[] = [
  { key: "products", title: "По продуктам", icon: Package, empty: "Выпуска за период нет", mobileEmpty: "Выпуска за период нет", forms: ["продукт", "продукта", "продуктов"] },
  { key: "stages", title: "Брак по этапам", icon: Layers, empty: "Брака за период нет", mobileEmpty: "Брака за период нет", forms: ["этап", "этапа", "этапов"] },
  { key: "types", title: "Брак по видам", icon: CircleX, empty: "Брака за период нет", mobileEmpty: "Брака за период нет", forms: ["вид", "вида", "видов"] },
  { key: "shifts", title: "По сменам", icon: ClipboardList, empty: "Выпуска за период нет", mobileEmpty: "Выпуска за период нет", forms: ["смена", "смены", "смен"] },
  { key: "packaging", title: "Брак упаковки", icon: PackageX, empty: "Брака упаковки за период нет", mobileEmpty: "Брака упаковки нет", forms: ["позиция", "позиции", "позиций"] },
  { key: "norm", title: "Отклонения расхода", icon: Scale, empty: "Закрытых заданий за период нет", mobileEmpty: "Закрытых заданий нет", forms: ["позиция", "позиции", "позиций"] },
  { key: "orders", title: "Задания в работе", icon: Hammer, empty: "Выпущенных заданий нет", mobileEmpty: "Выпущенных заданий нет", forms: ["задание", "задания", "заданий"] },
];

type BarRowsData = ProductionDashboard["byProduct"];
const barsOf = (d: ProductionDashboard, k: SectionKey): BarRowsData | null =>
  ({ products: d.byProduct, stages: d.defectsByStage, types: d.defectsByType, shifts: d.byShift, packaging: d.packagingDefects } as Record<string, BarRowsData>)[k] ?? null;

const countOf = (d: ProductionDashboard, k: SectionKey) => (k === "norm" ? d.normFact.length : k === "orders" ? d.orders.length : (barsOf(d, k)?.length ?? 0));

/** Полный список раздела — отчёт производства за тот же период (задания — их список). */
const reportOf = (d: ProductionDashboard, k: SectionKey) =>
  k === "orders" ? "/production/orders"
  : reportHref(k === "products" || k === "shifts" ? "output" : k === "packaging" ? "packaging" : k === "norm" ? "check" : "defects", d.from, d.to);

/** Итог раздела для строки на телефоне: «12 продуктов · 4 820 кг», «5 позиций · до +8,2%», «3 задания»; пусто — null. */
function summaryOf(d: ProductionDashboard, m: SectionMeta, t: T): string | null {
  const n = countOf(d, m.key);
  if (n === 0) return null;
  const head = `${n} ${t(plural(n, m.forms))}`;
  const bars = barsOf(d, m.key);
  if (bars) {
    const total = barsTotal(bars);
    return total.kg > 0 ? `${head} · ${kg(total.kg)} ${t("кг")}` : head;
  }
  if (m.key === "norm") {
    const worst = d.normFact.reduce<number | null>((w, x) => (x.percent !== null && (w === null || x.percent > w) ? x.percent : w), null);
    return worst !== null && worst > 0 ? `${head} · ${t("до +{p}", { p: pct(worst) ?? "" })}` : head;
  }
  return head;
}

/** Содержимое раздела: строки с полосами, отклонения расхода или задания. limit — сколько строк на карточке компьютера. */
function SectionBody({ d, k, limit }: { d: ProductionDashboard; k: SectionKey; limit?: number }) {
  if (k === "norm") return <NormRows rows={d.normFact} limit={limit} />;
  if (k === "orders") return <OrdersList d={d} limit={limit} />;
  return <BarRows rows={barsOf(d, k) ?? []} limit={limit} extraLabel={k === "products" ? "брак {kg} кг" : undefined} />;
}

/** Задания в работе: номер (ссылка), дата, продукт, нехватка сырья, выпущено из плана, статус. */
function OrdersList({ d, limit }: { d: ProductionDashboard; limit?: number }) {
  const t = useT();
  return (
    <ul className="divide-y divide-line">
      {(limit ? d.orders.slice(0, limit) : d.orders).map((o) => (
        <li key={o.id} className="flex items-center gap-3 py-2.5 text-[13px] first:pt-0 last:pb-0">
          <span className="min-w-0 flex-1">
            <Link href={`/production/orders/${o.id}`} className="font-semibold tabular-nums text-accent-strong hover:underline">
              {o.number}
            </Link>
            <span className="text-ink-3"> · {dayRu(o.plannedDate)}</span>
            <span className="block truncate text-ink">{o.product}</span>
            {o.shortages > 0 && <span className="block text-[11px] text-bad">{t("не хватает сырья: позиций {n}", { n: o.shortages })}</span>}
          </span>
          <span className="shrink-0 text-right">
            <span className="block whitespace-nowrap tabular-nums text-ink-2">
              {kg(o.good)} / {kg(o.planned)} {t(o.unit)}
            </span>
            <OrderStatusPill status={o.status} />
          </span>
        </li>
      ))}
    </ul>
  );
}

/**
 * «Производство → Технолог» (макеты kPu1u и RkFy8): шапка, кнопки «Техкарты / Смены / Отчёты», период, шесть KPI, счётчики, выпуск и брак по
 * дням, шесть разделов, задания в работе и что требует внимания. Данные и расчёты — прежние (утверждённые смены и закрытые задания).
 * Компьютер и планшет — сетка; телефон (уже 768) — кнопки во всю ширину, KPI 2 × 3, разделы списком со шторкой. Колонки KPI и разделов —
 * по ширине страницы (слева меню приложения), а не окна. Период — в адресе (?period=…); пока грузится новый период — лёгкий скелетон.
 * Без данных: loading — скелетоны (кнопки видны сразу), error — плашка с «Повторить».
 */
export function TechnologistDashboard({ data, period, loading = false, error = false }: { data: ProductionDashboard | null; period: TechPeriod | null; loading?: boolean; error?: boolean }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, start] = useTransition();
  // Пока грузится (loading.tsx не знает адреса) — период из адреса: ?period=…, без него и без дат — «Этот месяц».
  const [chosen, setChosen] = useState<TechPeriod | null>(period ?? parsePeriod(params.get("period")) ?? (params.get("from") || params.get("to") ? null : "this_month"));
  const [sheet, setSheet] = useState<SectionKey | null>(null);
  const state: State = data ? { data, loading: false } : { data: null, loading };
  const busy = state.loading || pending;
  const d = state.data;

  const choose = (p: TechPeriod) => {
    if (p === chosen && !pending) return;
    setChosen(p);
    const next = new URLSearchParams(params.toString());
    next.delete("from");
    next.delete("to");
    next.set("period", p);
    start(() => router.push(`${pathname}?${next}`, { scroll: false }));
  };
  const retry = () => start(() => router.refresh());

  const links = quickLinks(d ? { activeOrders: d.activeOrders, openShifts: d.openShifts, closedShifts: d.closedShifts, pendingRecipes: d.pendingRecipes } : null);
  const counterItems = counters(d ?? { activeOrders: 0, openShifts: 0, closedShifts: 0, pendingRecipes: 0 });
  const kpiItems = kpis(d, t);
  const dim = pending ? "animate-pulse opacity-60" : "";
  const errorBanner = error && !d && (
    <div role="alert" className="flex flex-wrap items-center gap-3 rounded-2xl border border-bad/25 bg-bad-soft px-4 py-3">
      <TriangleAlert aria-hidden className="size-5 shrink-0 text-bad" />
      <p className="min-w-0 flex-1 text-[13px] text-ink-2">{t("Не удалось загрузить данные технолога. Попробуйте ещё раз через несколько секунд.")}</p>
      <button type="button" onClick={retry} disabled={pending} className="flex h-9 items-center gap-2 rounded-lg bg-bad px-3.5 text-[13px] font-semibold text-white disabled:opacity-60">
        <RotateCw aria-hidden className={`size-4 ${pending ? "animate-spin" : ""}`} />
        {t("Повторить")}
      </button>
    </div>
  );
  const showBlocks = d !== null || state.loading;
  const openSection = SECTIONS.find((s) => s.key === sheet) ?? null;

  return (
    <div data-palette="sales-secondary" data-glow="both" className="min-h-full">
      {/* Телефон и планшет: своя шапка вместо общей (как в макете). */}
      <header className="flex h-14 items-center gap-2.5 px-3 lg:hidden">
        <Link href="/production" aria-label={t("Назад")} className="grid size-[40px] shrink-0 place-items-center rounded-full bg-surface text-ink">
          <ChevronLeft aria-hidden className="size-5" />
        </Link>
        <div className="min-w-0 flex-1 text-center">
          <h1 className="truncate text-[15px] font-bold text-ink">{t("Технолог")}</h1>
          <p className="text-[11px] font-medium text-ink-3">{t("Производство")}</p>
        </div>
        <span className="grid size-[40px] shrink-0 place-items-center rounded-full bg-surface [&_button]:size-[40px]">
          <NotificationBell />
        </span>
      </header>

      {/* Компьютер: крошки, название, описание. */}
      <header className="border-b border-line max-lg:hidden">
        <div className="mx-auto max-w-[1800px] px-6 pb-5 pt-[18px]">
          <nav aria-label={t("Навигация")} className="flex flex-wrap gap-1.5 text-[12px] font-medium text-ink-3">
            {[["OneBase", "/"], ["Отделы", "/departments"], ["Производство", "/production"]].map(([label, href]) => (
              <span key={href} className="flex gap-1.5">
                <Link href={href} className="hover:text-ink">{t(label)}</Link>
                <span aria-hidden>/</span>
              </span>
            ))}
            <span className="text-ink-2">{t("Технолог")}</span>
          </nav>
          <h1 className="mt-2 text-[28px] font-bold leading-tight tracking-[-0.4px] text-ink">{t("Технолог")}</h1>
          <p className="mt-2 text-[14px] font-medium text-ink-3">
            {t("Производство по техкартам: выпуск, брак по этапам, расход к норме и выход выпечки — по утверждённым сменам и закрытым заданиям")}
          </p>
        </div>
      </header>

      {/* ---------- Телефон (уже 768) ---------- */}
      <div className="flex flex-col gap-3.5 px-4 pb-6 pt-1 md:hidden">
        <p className="text-[13px] leading-[1.45] text-ink-3">{t("Выпуск, брак по этапам, расход к норме и выход выпечки — по утверждённым сменам и закрытым заданиям")}</p>
        <nav aria-label={t("Разделы технолога")} className="flex flex-col gap-2">
          {links.map((l) => (
            <QuickLinkCard key={l.key} link={l} loading={state.loading} unknown={!d && !state.loading} mobile />
          ))}
        </nav>
        <PeriodSwitch value={chosen} onChange={choose} mobile />
        {errorBanner}
        {showBlocks && (
          <div aria-busy={busy} className={`flex flex-col gap-3.5 ${dim}`}>
            <div className="grid grid-cols-2 gap-2">
              {kpiItems.map((k) => (
                <Kpi key={k.key} icon={k.icon} label={k.short} value={k.value} unit={k.unit} note={k.shortNote ?? k.note} extra={k.extra} tone={k.tone} loading={state.loading} mobile />
              ))}
            </div>
            <CounterList items={counterItems} loading={state.loading} />
            {d && d.pieceOutputs > 0 && <p className="-mt-1.5 text-[12px] text-ink-3">{t("смен по продуктам в штуках: {n} — в килограммы не складываются", { n: d.pieceOutputs })}</p>}
            <DailyChart days={d?.days ?? []} loading={state.loading} mobile />
            <ul className="divide-y divide-line overflow-hidden rounded-2xl border border-line bg-surface" aria-label={t("Разделы")}>
              {SECTIONS.map((s) => {
                const Icon = s.icon;
                return (
                  <li key={s.key}>
                    <button
                      type="button"
                      onClick={() => setSheet(s.key)}
                      disabled={!d}
                      className="flex w-full items-center gap-3 px-3.5 py-3 text-left active:bg-muted disabled:opacity-100"
                    >
                      <span className="grid size-8 shrink-0 place-items-center rounded-[10px] bg-muted">
                        <Icon aria-hidden className="size-[15px] text-ink-3" />
                      </span>
                      <span className="min-w-0 flex-1">
                        <span className="block text-[13px] font-semibold text-ink">{t(s.title)}</span>
                        {!d ? <Skel className="mt-1 h-3 w-28" /> : <span className="block truncate text-[12px] text-ink-3">{summaryOf(d, s, t) ?? t(s.mobileEmpty)}</span>}
                      </span>
                      <ChevronRight aria-hidden className="size-4 shrink-0 text-ink-4" />
                    </button>
                  </li>
                );
              })}
            </ul>
            <section className="rounded-2xl border border-line bg-surface px-4 py-3.5">
              <h2 className="mb-3 text-[14px] font-bold text-ink">{t("Требует внимания")}</h2>
              {state.loading ? <Skel className="h-14 w-full rounded-[10px]" /> : <AttentionList issues={d?.issues ?? []} mobile />}
            </section>
          </div>
        )}
        {d && openSection && (
          <Sheet open onClose={() => setSheet(null)} title={t(openSection.title)} subtitle={summaryOf(d, openSection, t) ?? undefined}>
            <div className="pb-2">
              {countOf(d, openSection.key) === 0 ? <EmptyRow icon={openSection.icon} text={openSection.empty} /> : <SectionBody d={d} k={openSection.key} />}
              <div className="mt-4 flex justify-end">
                <AllLink href={reportOf(d, openSection.key)} label={openSection.key === "orders" ? "Все задания" : "Открыть отчёт"} />
              </div>
            </div>
          </Sheet>
        )}
      </div>

      {/* ---------- Компьютер и планшет ---------- */}
      <div className="mx-auto flex max-w-[1800px] flex-col gap-4 px-6 pb-10 pt-5 @container max-md:hidden max-lg:px-4">
        <div className="flex flex-wrap items-center gap-3">
          <PeriodSwitch value={chosen} onChange={choose} />
          <nav
            aria-label={t("Разделы технолога")}
            className="ml-auto flex flex-wrap justify-end gap-2.5 @max-[1000px]:grid @max-[1000px]:w-full @max-[1000px]:grid-cols-3"
          >
            {links.map((l) => (
              <QuickLinkCard key={l.key} link={l} loading={state.loading} unknown={!d && !state.loading} />
            ))}
          </nav>
        </div>
        {errorBanner}
        {showBlocks && (
          <div aria-busy={busy} className={`flex flex-col gap-4 ${dim}`}>
            <div className="grid grid-cols-3 gap-3 @min-[1080px]:grid-cols-6">
              {kpiItems.map((k) => (
                <Kpi key={k.key} icon={k.icon} label={k.label} value={k.value} unit={k.unit} note={k.note} extra={k.extra} tone={k.tone} loading={state.loading} />
              ))}
            </div>
            <div className="-mt-1 flex flex-wrap items-center gap-x-3 gap-y-2">
              <CounterChips items={counterItems} loading={state.loading} />
              {d && d.pieceOutputs > 0 && <p className="text-[12px] text-ink-3">{t("смен по продуктам в штуках: {n} — в килограммы не складываются", { n: d.pieceOutputs })}</p>}
            </div>
            <DailyChart days={d?.days ?? []} loading={state.loading} />
            <div className="grid grid-cols-2 gap-3 @min-[1080px]:grid-cols-3">
              {SECTIONS.filter((s) => s.key !== "orders").map((s) => (
                <section key={s.key} className="flex flex-col gap-3.5 rounded-2xl border border-line bg-surface px-[18px] py-4">
                  <div className="flex items-center justify-between gap-3">
                    <h2 className="text-[14px] font-bold text-ink">{t(s.title)}</h2>
                    {d && countOf(d, s.key) > 0 && <AllLink href={reportOf(d, s.key)} />}
                  </div>
                  {state.loading ? <Skel className="h-8 w-3/4" /> : d && countOf(d, s.key) > 0 ? <SectionBody d={d} k={s.key} limit={5} /> : <EmptyRow icon={s.icon} text={s.empty} />}
                </section>
              ))}
            </div>
            <div className="grid gap-3 @min-[1000px]:grid-cols-[minmax(0,1fr)_400px]">
              <section className="flex flex-col gap-3.5 rounded-2xl border border-line bg-surface px-[18px] py-4">
                <div className="flex items-center justify-between gap-3">
                  <h2 className="text-[14px] font-bold text-ink">{t("Задания в работе")}</h2>
                  <AllLink href="/production/orders" label="Все задания" />
                </div>
                {state.loading ? <Skel className="h-8 w-3/4" /> : d && d.orders.length > 0 ? <OrdersList d={d} /> : <EmptyRow icon={Hammer} text="Выпущенных заданий нет" />}
              </section>
              <section className="flex flex-col gap-3.5 rounded-2xl border border-line bg-surface px-[18px] py-4">
                <h2 className="text-[14px] font-bold text-ink">{t("Требует внимания")}</h2>
                {state.loading ? <Skel className="h-14 w-full rounded-[10px]" /> : <AttentionList issues={d?.issues ?? []} />}
              </section>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
