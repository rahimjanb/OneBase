"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import {
  ArrowRight,
  Building2,
  ChartLine,
  Check,
  ChevronRight,
  CircleCheck,
  CircleDot,
  ClipboardPlus,
  Factory,
  FileText,
  Gauge,
  Info,
  ListChecks,
  ListPlus,
  OctagonAlert,
  Package,
  PackageX,
  Sparkles,
  TriangleAlert,
  Users,
  Wallet,
  type LucideIcon,
} from "lucide-react";
import { TaskForm } from "@/components/tasks/TaskForm";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  compact,
  date,
  moduleLabel,
  num,
  overviewRecommendations,
  planDays,
  planTone,
  plural,
  purchaseOf,
  rawRequestHref,
  shortDate,
  shortWhen,
  stockTone,
  type Dashboard,
  type Insight,
  type OverviewRecommendation,
  type RiskCounts,
  type SupplyRecommendation,
} from "@/lib/analytik";
import type { DepartmentTasks, WorkProject } from "@/lib/tasks";
import { AiMark, MoreLink, OCard, toneSoft, toneText } from "./parts";

const moduleIcon: Record<string, LucideIcon> = {
  sales: ChartLine,
  finance: Wallet,
  procurement: Building2,
  production: Factory,
  hr: Users,
  work: ListChecks,
};

// ---------- Критические риски ----------

const severityBox: Record<string, { cls: string; icon: LucideIcon }> = {
  critical: { cls: "bg-bad-soft text-bad", icon: OctagonAlert },
  warning: { cls: "bg-warn-soft text-warn", icon: TriangleAlert },
  info: { cls: "bg-muted text-ink-3", icon: Info },
};

function RiskRow({ risk, now }: { risk: Insight; now: Date }) {
  const t = useT();
  const box = severityBox[risk.severity] ?? severityBox.info;
  const Icon = box.icon;
  const ModuleIcon = moduleIcon[risk.module] ?? CircleDot;
  const when = shortWhen(risk.detectedAt, now);
  const whenText = when.day ? `${t(when.day === "today" ? "сегодня" : "вчера")} ${when.text}` : when.text;
  return (
    <li>
      <Link
        href={`/analytik/risks?risk=${encodeURIComponent(risk.id)}`}
        className="flex items-center gap-3 rounded-xl border border-line bg-muted px-3 py-2 transition-colors hover:border-line-strong max-md:py-2.5"
      >
        <span className={`grid size-[34px] shrink-0 place-items-center rounded-[10px] max-md:size-8 ${box.cls}`}>
          <Icon className="size-[17px]" aria-hidden />
        </span>
        <span className="min-w-0 flex-1">
          <span className="line-clamp-2 text-[13px] font-semibold leading-snug text-ink">{risk.title}</span>
          <span className="mt-0.5 flex items-center gap-1.5 text-[11px] text-ink-4">
            <ModuleIcon className="size-3 shrink-0" aria-hidden />
            <span className="truncate">
              {t(moduleLabel[risk.module] ?? risk.module)} · {whenText}
            </span>
          </span>
        </span>
        <ChevronRight className="size-4 shrink-0 text-ink-4" aria-hidden />
      </Link>
    </li>
  );
}

/**
 * «Критические риски»: фильтр «Все / Критично / Внимание» со счётчиками по всем активным находкам, первые 4 (телефон — 3), нажатие —
 * находка на странице рисков (раскрыта); «Разобрать риски с AI-аналитиком» — вопрос со списком рисков. Рисков нет — зелёная плашка.
 */
export function RisksCard({ risks, counts, at }: { risks: Insight[]; counts: RiskCounts | null; at: string }) {
  const t = useT();
  const router = useRouter();
  const [filter, setFilter] = useState<"all" | "critical" | "warning">("all");
  const [now, setNow] = useState(() => new Date(at));
  useEffect(() => setNow(new Date()), []);
  const c = counts ?? {
    critical: risks.filter((r) => r.severity === "critical").length,
    warning: risks.filter((r) => r.severity === "warning").length,
    info: risks.filter((r) => r.severity === "info").length,
    total: risks.length,
  };
  const list = risks.filter((r) => filter === "all" || r.severity === filter);
  const time = shortWhen(at, now).text;
  const chip = (key: typeof filter, label: string, n: number, dot?: string) => (
    <button
      key={key}
      type="button"
      role="radio"
      aria-checked={filter === key}
      onClick={() => setFilter(key)}
      className={`inline-flex items-center gap-1.5 rounded-lg px-2.5 py-1 text-[12px] font-semibold transition-colors ${
        filter === key ? "bg-accent text-white" : "border border-line bg-muted text-ink-2 hover:text-ink"
      }`}
    >
      {dot && <span aria-hidden className={`size-1.5 rounded-full ${dot}`} />}
      {t(label)}
      <span className={filter === key ? "text-white/75" : "text-ink-4"}>{n}</span>
    </button>
  );
  const analyse = () => {
    const top = risks
      .filter((r) => r.severity !== "info")
      .slice(0, 6)
      .map((r, i) => `${i + 1}) ${r.title}`)
      .join("; ");
    const q = top ? `${t("Разбери критические риски и скажи, что делать в первую очередь")}: ${top}` : t("Какие проблемы сейчас требуют вмешательства директора?");
    router.push(`/analytik/chat?q=${encodeURIComponent(q)}&send=1`);
  };
  return (
    <OCard
      title={t("Критические риски")}
      subtitle={`${c.total} ${t(plural(c.total, "активный", "активных", "активных"))} · ${t("обновлено")} ${time}`}
      action={
        <>
          <span className="max-md:hidden">
            <MoreLink href="/analytik/risks" label="Все риски" />
          </span>
          <Link href="/analytik/risks" className="shrink-0 text-[13px] font-semibold text-accent-strong md:hidden">
            {t("Все")} ›
          </Link>
        </>
      }
      className="h-full"
    >
      {c.total === 0 ? (
        <div className="flex items-center gap-2.5 rounded-xl bg-ok-soft px-3.5 py-3 text-[13px] font-semibold text-ok">
          <CircleCheck className="size-4 shrink-0" aria-hidden />
          {t("Критических рисков нет")}
        </div>
      ) : (
        <>
          <div role="radiogroup" aria-label={t("Фильтр рисков")} className="mb-3 flex flex-wrap gap-1.5 max-md:hidden">
            {chip("all", "Все", c.total)}
            {chip("critical", "Критично", c.critical, filter === "critical" ? "bg-white" : "bg-bad")}
            {chip("warning", "Внимание", c.warning, filter === "warning" ? "bg-white" : "bg-warn")}
          </div>
          {list.length === 0 ? (
            <p className="rounded-xl border border-dashed border-line px-3 py-6 text-center text-[12px] text-ink-3">{t("В этом фильтре рисков нет")}</p>
          ) : (
            <ul className="space-y-2">
              {list.slice(0, 4).map((r, i) => (
                <div key={r.id} className={i === 3 ? "max-md:hidden" : undefined}>
                  <RiskRow risk={r} now={now} />
                </div>
              ))}
            </ul>
          )}
          <div className="mt-auto pt-3.5 max-md:hidden">
            <button
              type="button"
              onClick={analyse}
              className="inline-flex h-[38px] w-full items-center justify-center gap-2 rounded-xl border border-violet/25 bg-violet-soft text-[12px] font-semibold text-violet transition-colors hover:border-violet/50"
            >
              <Sparkles className="size-4" aria-hidden />
              {t("Разобрать риски с AI-аналитиком")}
            </button>
          </div>
        </>
      )}
    </OCard>
  );
}

// ---------- План продаж ----------

const months = ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];

/**
 * «План продаж: ноябрь 2026»: выполнено и прогноз к концу месяца, полоса (прогноз под фактом, метка «прошло времени»), регионы — самые
 * крупные по плану: факт полосой и чип прогноза (цвет — по тем же порогам, что снижают оценку компании).
 */
export function PlanCard({ data }: { data: Dashboard }) {
  const t = useT();
  const plan = data.sales?.plan;
  if (!plan) return null;
  const days = planDays(plan);
  const fact = plan.execution;
  const forecast = plan.forecastExecution;
  const lastDay = shortDate(new Date(Date.UTC(plan.year, plan.month, 0)).toISOString());
  const regions = plan.regions
    .filter((r) => r.planKg)
    .sort((a, b) => (b.planKg ?? 0) - (a.planKg ?? 0))
    .slice(0, 4);
  const pct = (v: number | null) => (v == null ? "—" : `${num(v, 1)}`);
  return (
    <OCard
      title={`${t("План продаж")}: ${t(months[plan.month - 1])} ${plan.year}`}
      subtitle={`${t("данные по")} ${date(plan.dataThrough)} · ${t("прошло {day} из {days} дней", { day: days.day, days: days.days })}`}
      action={<MoreLink href="/analytik/sales" />}
      className="h-full"
    >
      <div className="flex items-end gap-8">
        <div>
          <p className="text-[10px] font-bold uppercase tracking-wider text-ink-4">{t("Выполнено")}</p>
          <p className="mt-1 text-[30px] font-bold leading-none tabular-nums text-ink">
            {pct(fact)}
            <span className="ml-1 text-[13px] font-semibold text-ink-3">%</span>
          </p>
        </div>
        <div>
          <p className="text-[10px] font-bold uppercase tracking-wider text-ink-4">{t("Прогноз к {date}", { date: lastDay })}</p>
          <p className="mt-1 text-[30px] font-bold leading-none tabular-nums text-violet">
            {pct(forecast)}
            <span className="ml-1 text-[13px] font-semibold text-ink-3">%</span>
          </p>
        </div>
      </div>
      <div
        className="relative mt-5 h-2.5 rounded-[5px] bg-muted"
        role="img"
        aria-label={t("Выполнено {fact}%, прошло времени {time}%, прогноз {forecast}%", { fact: pct(fact), time: num(days.percent, 0), forecast: pct(forecast) })}
      >
        <div className="absolute inset-y-0 left-0 rounded-[5px] bg-violet/25" style={{ width: `${Math.min(100, forecast ?? 0)}%` }} />
        <div className="absolute inset-y-0 left-0 rounded-[5px] bg-accent" style={{ width: `${Math.min(100, fact ?? 0)}%` }} />
        <div className="absolute -top-1.5 h-[22px] w-0.5 rounded-full bg-ink-2" style={{ left: `calc(${Math.min(100, days.percent)}% - 1px)` }} />
      </div>
      <div className="mt-3 flex flex-wrap gap-x-4 gap-y-1 text-[11px] text-ink-3">
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2 rounded-sm bg-accent" />
          {t("факт")} {pct(fact)}%
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2 rounded-sm bg-ink-2" />
          {t("прошло времени")} {num(days.percent, 0)}%
        </span>
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2 rounded-sm bg-violet/60" />
          {t("прогноз")} {pct(forecast)}%
        </span>
      </div>
      {regions.length > 0 && (
        <ul className="mt-4 space-y-2.5 border-t border-line pt-4">
          {regions.map((r) => {
            const tone = planTone(r.forecastExecution);
            return (
              <li key={r.id} className="grid grid-cols-[84px_minmax(0,1fr)_40px_auto] items-center gap-3">
                <span className="truncate text-[12px] text-ink-2" title={r.name}>
                  {r.name}
                </span>
                <span className="h-1.5 overflow-hidden rounded-full bg-muted">
                  <span className="block h-full rounded-full bg-accent" style={{ width: `${Math.min(100, r.execution ?? 0)}%` }} />
                </span>
                <span className="text-right text-[12px] font-semibold tabular-nums text-ink">{r.execution == null ? "—" : `${num(r.execution, 0)}%`}</span>
                <span
                  title={t("прогноз выполнения к концу месяца")}
                  className={`inline-flex items-center gap-0.5 rounded-md px-1.5 py-px text-[11px] font-bold tabular-nums ${toneSoft[tone]}`}
                >
                  <ArrowRight className="size-3" aria-hidden />
                  {r.forecastExecution == null ? "—" : `${num(r.forecastExecution, 0)}%`}
                </span>
              </li>
            );
          })}
        </ul>
      )}
    </OCard>
  );
}

// ---------- Что закупить ----------

function daysText(days: number | null, t: (s: string) => string): string {
  if (days == null) return "—";
  if (days <= 0) return t("закончилось");
  const n = Math.floor(days);
  return `${num(n)} ${t(plural(n, "день", "дня", "дней"))}`;
}

function CriticalItem({ item, canRequest }: { item: SupplyRecommendation; canRequest: boolean }) {
  const t = useT();
  const { qty, sum, days } = purchaseOf(item);
  const reason = item.shortageDate
    ? t("Аналитик: запас «{name}» закончится {date}, поставка занимает {days} дн.", { name: item.name, date: date(item.shortageDate), days: item.leadTimeDays })
    : t("Аналитик: «{name}» нет в наличии, поставка занимает {days} дн.", { name: item.name, days: item.leadTimeDays });
  const mini = (label: string, value: string) => (
    <div className="min-w-0 rounded-[9px] bg-surface px-3 py-2">
      <p className="text-[10px] font-bold uppercase tracking-wider text-ink-4">{t(label)}</p>
      <p className="mt-0.5 truncate text-[13px] font-bold tabular-nums text-ink">{value}</p>
    </div>
  );
  return (
    <div className="rounded-[14px] border border-bad/25 bg-bad-soft p-3.5">
      <div className="flex items-start gap-3">
        <span className="grid size-9 shrink-0 place-items-center rounded-[10px] bg-surface text-bad">
          <PackageX className="size-[18px]" aria-hidden />
        </span>
        <div className="min-w-0">
          <p className="truncate text-[14px] font-bold text-ink">{item.name}</p>
          <p className="mt-0.5 text-[11px] font-medium text-bad">
            {item.shortageDate ? t("закончится {date}", { date: shortDate(item.shortageDate) }) : t("нет в наличии")} ·{" "}
            {t("поставка занимает {n}", { n: `${item.leadTimeDays} ${t(plural(item.leadTimeDays, "день", "дня", "дней"))}` })}
          </p>
        </div>
      </div>
      <div className="mt-3 grid grid-cols-3 gap-2">
        {mini("Остаток", daysText(days, t))}
        {mini("Купить", `${num(qty, qty < 10 ? 1 : 0)} ${item.unit}`)}
        {mini("Сумма", sum == null ? t("нет цены") : compact(sum))}
      </div>
      {canRequest && (
        <Link
          href={rawRequestHref(item, reason)}
          className="mt-3 inline-flex h-9 w-full items-center justify-center gap-2 rounded-[10px] bg-accent text-[12px] font-semibold text-white"
        >
          <ClipboardPlus className="size-4" aria-hidden />
          {t("Создать заявку в снабжение")}
        </Link>
      )}
    </div>
  );
}

/**
 * «Что закупить в первую очередь»: критичная позиция — карточкой (остаток, сколько купить на 30 дней, сумма, заявка в снабжение с
 * заполненной позицией); ещё критичные (до 3) и «На контроле» — строками с запасом в днях. Срочного нет — «обеспечены минимум на 14 дней».
 */
export function PurchaseCard({ data, canRequest }: { data: Dashboard; canRequest: boolean }) {
  const t = useT();
  const supply = data.supply;
  if (!supply) return null;
  const critical = supply.top.filter((r) => r.risk === "critical");
  const watch = supply.top.filter((r) => r.risk !== "critical");
  const extraCritical = critical.slice(1, 3);
  const rows = [...extraCritical, ...watch].slice(0, 4);
  const more = critical.length + watch.length - 1 - rows.length;
  return (
    <OCard title={t("Что закупить в первую очередь")} subtitle={t("по остаткам, скорости расхода и срокам поставки")} action={<MoreLink href="/analytik/procurement" />} className="h-full">
      {critical[0] ? (
        <CriticalItem item={critical[0]} canRequest={canRequest} />
      ) : (
        <div className="flex items-center gap-2.5 rounded-xl bg-ok-soft px-3.5 py-3 text-[13px] font-semibold text-ok">
          <CircleCheck className="size-4 shrink-0" aria-hidden />
          {t("Все позиции обеспечены минимум на 14 дней")}
        </div>
      )}
      {rows.length > 0 && (
        <div className="mt-4">
          <p className="mb-1 text-[10px] font-bold uppercase tracking-wider text-ink-4">{t("На контроле")}</p>
          <ul className="divide-y divide-line">
            {rows.map((r) => {
              const days = r.daysOfCover;
              const tone = r.risk === "critical" ? "bad" : stockTone(days);
              const share = days == null ? 0 : Math.max(0.04, Math.min(1, days / 30));
              return (
                <li key={r.itemId} className="flex items-center gap-3 py-2.5">
                  <Package className="size-4 shrink-0 text-ink-4" aria-hidden />
                  <span className="min-w-0 flex-1 truncate text-[13px] text-ink-2" title={r.name}>
                    {r.name}
                  </span>
                  <span className="h-1 w-[60px] shrink-0 overflow-hidden rounded-full bg-muted">
                    <span className={`block h-full rounded-full ${tone === "bad" ? "bg-bad" : tone === "warn" ? "bg-warn" : tone === "ok" ? "bg-ok" : "bg-ink-4"}`} style={{ width: `${share * 100}%` }} />
                  </span>
                  <span className={`w-14 shrink-0 text-right text-[12px] font-semibold tabular-nums ${toneText[tone]}`}>
                    {days == null ? "—" : days <= 0 ? t("нет") : `${num(Math.floor(days))} ${t("дн.")}`}
                  </span>
                </li>
              );
            })}
          </ul>
          {more > 0 && (
            <Link href="/analytik/procurement" className="mt-1 inline-flex text-[12px] font-semibold text-accent-strong hover:underline">
              {t("Ещё {n}", { n: more })}
            </Link>
          )}
        </div>
      )}
    </OCard>
  );
}

/** Телефон: «Срочно закупить» — первая критичная позиция и заявка; срочного нет — блока нет. */
export function UrgentPurchase({ data, canRequest }: { data: Dashboard; canRequest: boolean }) {
  const t = useT();
  const item = data.supply?.top.find((r) => r.risk === "critical");
  if (!item) return null;
  const { qty, sum, days } = purchaseOf(item);
  const reason = item.shortageDate
    ? t("Аналитик: запас «{name}» закончится {date}, поставка занимает {days} дн.", { name: item.name, date: date(item.shortageDate), days: item.leadTimeDays })
    : t("Аналитик: «{name}» нет в наличии, поставка занимает {days} дн.", { name: item.name, days: item.leadTimeDays });
  return (
    <section className="rounded-[18px] border border-bad/25 bg-bad-soft p-4">
      <div className="flex items-start gap-3">
        <span className="grid size-9 shrink-0 place-items-center rounded-[10px] bg-surface text-bad">
          <PackageX className="size-[18px]" aria-hidden />
        </span>
        <div className="min-w-0">
          <p className="text-[11px] font-bold text-bad">{t("Срочно закупить")}</p>
          <p className="mt-0.5 text-[14px] font-bold text-ink">
            {item.name} — {num(qty, qty < 10 ? 1 : 0)} {item.unit}
          </p>
          <p className="mt-0.5 text-[12px] text-ink-3">
            {days != null && days > 0 ? t("остатка на {n}", { n: daysText(days, t) }) : t("нет в наличии")}
            {sum != null ? ` · ${compact(sum)} ${t("сум")}` : ""}
          </p>
        </div>
      </div>
      {canRequest && (
        <Link href={rawRequestHref(item, reason)} className="mt-3 inline-flex h-10 w-full items-center justify-center gap-2 rounded-xl bg-accent text-[13px] font-semibold text-white">
          <ClipboardPlus className="size-4" aria-hidden />
          {t("Создать заявку в снабжение")}
        </Link>
      )}
    </section>
  );
}

// ---------- Рекомендации AI ----------

const doneKey = "analytik:recommendations:done";

function readDone(): Record<string, string> {
  try {
    const raw = JSON.parse(localStorage.getItem(doneKey) ?? "{}") as Record<string, string>;
    return raw && typeof raw === "object" ? raw : {};
  } catch {
    return {};
  }
}

/** Отдел задачи по модулю находки (если такой отдел у пользователя есть — подставится в форму). */
const departmentOf: Record<string, string> = { sales: "sales", procurement: "procurement", production: "production", hr: "hr", finance: "finance" };

/**
 * «Рекомендации AI-аналитика»: из найденных рисков, подтверждённых данными. Действие: сырьё с дефицитом — заявка в снабжение с позицией;
 * персонал — отчёт; иначе — задача с заполненным названием и описанием (право «Задачи»), без права — данные находки. Созданная задача —
 * пункт помечен «Сделано».
 */
export function RecommendationsCard({ data, canRequest, canTasks }: { data: Dashboard; canRequest: boolean; canTasks: boolean }) {
  const t = useT();
  const items = overviewRecommendations(data.risks, 3);
  const [done, setDone] = useState<Record<string, string>>({});
  useEffect(() => setDone(readDone()), []);
  const [work, setWork] = useState<{ departments: DepartmentTasks[]; projects: WorkProject[] } | null>(null);
  const [form, setForm] = useState<OverviewRecommendation | null>(null);
  const [error, setError] = useState<string | null>(null);
  const openTask = useCallback(
    async (rec: OverviewRecommendation) => {
      setError(null);
      try {
        const loaded = work ?? { departments: await bff<DepartmentTasks[]>("work/departments"), projects: await bff<WorkProject[]>("work/projects") };
        setWork(loaded);
        setForm(rec);
      } catch (e) {
        setError((e as Error).message);
      }
    },
    [work],
  );
  const markDone = (id: string, taskId: string) => {
    const next = { ...readDone(), [id]: taskId };
    try {
      localStorage.setItem(doneKey, JSON.stringify(next));
    } catch {
      // Хранилище недоступно — отметка живёт до перезагрузки.
    }
    setDone(next);
  };

  const action = (rec: OverviewRecommendation) => {
    const linkCls = "inline-flex shrink-0 items-center gap-1 text-[11px] font-semibold text-accent-strong hover:underline";
    if (done[rec.id]) {
      return (
        <Link href={`/tasks/task/${done[rec.id]}`} className="inline-flex shrink-0 items-center gap-1 text-[11px] font-semibold text-ok">
          <Check className="size-3.5" aria-hidden />
          {t("Сделано")}
        </Link>
      );
    }
    const item = rec.sku ? data.supply?.top.find((r) => r.sku === rec.sku) : undefined;
    if (item && canRequest) {
      const reason = item.shortageDate
        ? t("Аналитик: запас «{name}» закончится {date}, поставка занимает {days} дн.", { name: item.name, date: date(item.shortageDate), days: item.leadTimeDays })
        : t("Аналитик: «{name}» нет в наличии, поставка занимает {days} дн.", { name: item.name, days: item.leadTimeDays });
      return (
        <Link href={rawRequestHref(item, reason)} className={linkCls}>
          <ClipboardPlus className="size-3.5" aria-hidden />
          {t("Создать заявку")}
        </Link>
      );
    }
    if (rec.module === "hr") {
      return (
        <Link href={rec.href ?? "/analytik/hr"} className={linkCls}>
          <FileText className="size-3.5" aria-hidden />
          {t("Открыть отчёт")}
        </Link>
      );
    }
    if (canTasks) {
      return (
        <button type="button" onClick={() => void openTask(rec)} className={linkCls}>
          <ListPlus className="size-3.5" aria-hidden />
          {t("Создать задачу")}
        </button>
      );
    }
    return rec.href ? (
      <Link href={rec.href} className={linkCls}>
        {t("Открыть данные")} ›
      </Link>
    ) : null;
  };

  return (
    <section className="relative flex h-full min-w-0 flex-col overflow-hidden rounded-[18px] border border-line bg-surface p-5 max-md:p-4">
      <div aria-hidden className="pointer-events-none absolute inset-0 bg-[radial-gradient(70%_60%_at_100%_0%,rgb(139_92_246/0.12),transparent)]" />
      <header className="relative mb-3.5 flex items-center gap-3">
        <AiMark size={32} radius={9} iconSize={16} />
        <div className="min-w-0">
          <h2 className="text-[15px] font-bold leading-tight text-ink">
            <span className="max-md:hidden">{t("Рекомендации AI-аналитика")}</span>
            <span className="md:hidden">{t("Рекомендации AI")}</span>
          </h2>
          <p className="mt-0.5 text-[11px] font-medium text-ink-4 max-md:hidden">{t("из найденных рисков — подтверждены данными")}</p>
        </div>
      </header>
      {items.length === 0 ? (
        <p className="relative rounded-xl border border-dashed border-line px-4 py-8 text-center text-[13px] text-ink-3">{t("Срочных рекомендаций нет")}</p>
      ) : (
        <ol className="relative space-y-2.5">
          {items.map((rec, i) => (
            <li key={rec.id} className={`rounded-xl border border-line bg-muted p-3 ${i === 2 ? "max-md:hidden" : ""}`}>
              <div className="flex gap-3">
                <span className="grid size-6 shrink-0 place-items-center rounded-full bg-violet-soft text-[11px] font-bold text-violet">{i + 1}</span>
                <div className="min-w-0 flex-1">
                  <p className="line-clamp-2 text-[13px] font-semibold leading-snug text-ink">{rec.title}</p>
                  <p className="mt-1 line-clamp-2 text-[11px] text-ink-3 max-md:hidden">{rec.reason}</p>
                  <div className="mt-2 flex flex-wrap items-center justify-between gap-2">
                    {rec.impact ? (
                      <span title={t("Масштаб находки по данным")} className="inline-flex items-center gap-1 rounded-md bg-ok-soft px-1.5 py-0.5 text-[10px] font-bold text-ok">
                        <Gauge className="size-3" aria-hidden />
                        {t("масштаб")} {rec.impact}
                      </span>
                    ) : (
                      <span />
                    )}
                    {action(rec)}
                  </div>
                </div>
              </div>
            </li>
          ))}
        </ol>
      )}
      {error && <p className="relative mt-2 text-[11px] text-bad">{error}</p>}
      {work && form && (
        <TaskForm
          open
          onClose={() => setForm(null)}
          departments={work.departments}
          projects={work.projects}
          prefill={{
            title: form.title.slice(0, 200),
            description: [form.title, `${t("Причина")}: ${form.reason}`, `${t("Из рекомендаций AI-аналитика")}: ${window.location.origin}/analytik/risks?risk=${form.id}`].join("\n"),
            department: departmentOf[form.module] ?? null,
          }}
          onCreated={(id) => {
            markDone(form.id, id);
            setForm(null);
          }}
        />
      )}
    </section>
  );
}
