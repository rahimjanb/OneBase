"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState, useTransition } from "react";
import {
  AlarmClock,
  ArrowUp,
  ArrowUpRight,
  Building,
  Calendar,
  CalendarClock,
  ChartColumn,
  Check,
  ChevronDown,
  ChevronRight,
  CircleDollarSign,
  ExternalLink,
  Gauge,
  GitMerge,
  Newspaper,
  Package,
  PackageCheck,
  Plus,
  RefreshCw,
  ShieldCheck,
  ShoppingBag,
  Sparkles,
  Stamp,
  Star,
  Tags,
  TrendingUp,
  Truck,
  UserPlus,
  Users,
  Workflow,
  X,
} from "lucide-react";
import { formatDate } from "@/i18n/core";
import { useLocale, useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { initialsOf } from "@/lib/dashboard";
import { plural } from "@/lib/format";
import { compactMoney, dateRu, money, periodOf, ratingText, type Amount, type Board } from "@/lib/procurement";
import { todayInTashkent } from "@/lib/tasks";
import { useUnits } from "./Charts";

type T = ReturnType<typeof useT>;
type Preset = "month" | "prev" | "quarter" | "year";

const card = "rounded-2xl border border-line bg-surface max-lg:rounded-[20px]";
const presets: { key: Preset; label: string }[] = [
  { key: "month", label: "Этот месяц" },
  { key: "prev", label: "Прошлый" },
  { key: "quarter", label: "3 месяца" },
  { key: "year", label: "Год" },
];
const questions = [
  { icon: TrendingUp, text: "Где цены выросли >10% за 3 месяца?" },
  { icon: CircleDollarSign, text: "Где мы переплачиваем?" },
  { icon: CalendarClock, text: "Какие заявки дольше всего ждут закупки?" },
  { icon: Package, text: "Что ниже минимума на складе?" },
  { icon: GitMerge, text: "Какие заявки объединить?" },
];

/** Русское склонение через перевод: каждая форма — свой ключ словаря с {n}. */
const pl = (t: T, n: number, forms: [string, string, string]) => t(plural(n, forms), { n });

/** «1–30 ноября», «1 октября – 15 ноября» — на языке интерфейса. */
function rangeLabel(from: string, to: string, locale: string): string {
  const day = (iso: string) => new Date(`${iso}T12:00:00Z`);
  const long = (iso: string, year = false) => formatDate(day(iso), locale, { day: "numeric", month: "long", ...(year ? { year: "numeric" } : {}) });
  if (from === to) return long(to);
  if (from.slice(0, 7) === to.slice(0, 7)) return `${Number(from.slice(8, 10))}–${long(to)}`;
  return from.slice(0, 4) === to.slice(0, 4) ? `${long(from)} – ${long(to)}` : `${long(from, true)} – ${long(to, true)}`;
}

/**
 * «Снабжение → Обзор»: что требует действия (согласование, просрочки), путь закупки одним блоком, показатели периода в одной карточке,
 * аналитика с вкладками и честным пустым состоянием, сотрудники снабжения. Данные — GET /api/procurement/board (по правам роли), период —
 * в адресе. Согласование из обзора — тем же действием, что в карточке заявки, с подтверждением; счётчики обновляются сразу.
 */
export function OverviewView({ board: initial, ask, aiReady }: { board: Board; ask: boolean; aiReady: boolean }) {
  const t = useT();
  const locale = useLocale();
  const units = useUnits();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, start] = useTransition();
  const [board, setBoard] = useState(initial);
  useEffect(() => setBoard(initial), [initial]);
  const today = todayInTashkent();
  const sum = (v: number) => `${compactMoney(v, units)} ${t("сум")}`;
  const full = (v: number) => `${money(v)} ${t("сум")}`;
  const range = rangeLabel(board.from, board.to, locale);

  const go = (r: { from: string; to: string }) => {
    const next = new URLSearchParams(params.toString());
    next.set("from", r.from);
    next.set("to", r.to);
    start(() => router.push(`${pathname}?${next}`, { scroll: false }));
  };
  const retry = () => start(() => router.refresh());
  const active = presets.find((p) => {
    const r = periodOf(p.key, today);
    return r.from === board.from && r.to === board.to;
  })?.key;

  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const approve = async (id: string) => {
    const a = board.approvals?.find((x) => x.id === id);
    if (!a || !window.confirm(t("Согласовать заявку {n} на {sum}?", { n: a.number, sum: full(a.amount) }))) return;
    setBusy(id);
    setError(null);
    try {
      await bff(`procurement/requests/${id}/approve`, { method: "POST", body: JSON.stringify({ comment: null }) });
      // Согласованная заявка уходит со «Ждёт согласования» сразу; куда она пошла дальше (следующий шаг или к закупке) — покажет обновление.
      setBoard((b) => ({ ...b, approvals: (b.approvals ?? []).filter((x) => x.id !== id), myApprovalCount: Math.max(0, b.myApprovalCount - 1) }));
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(null);
    }
  };

  const askAi = (question: string) => {
    const q = question.trim();
    if (!q || !aiReady) return;
    router.push(`/consultant?ask=${encodeURIComponent(q)}&dept=supply&period=${board.to.slice(0, 7)}&send=1`);
  };

  const showApprovals = board.access.canApprove && (board.approvals?.length ?? 0) > 0;
  const showUpcoming = !showApprovals && (board.upcoming?.length ?? 0) > 0;
  // Просрочки: заявки к закупке с прошедшей желаемой датой (кто видит заявки), иначе — поставки по прежним заказам.
  const overdue = board.pipeline.overdueToBuy ?? board.summary.overdueDeliveries;

  return (
    <>
      <header className="border-b border-line">
        <div className="mx-auto flex max-w-[1800px] items-start justify-between gap-6 px-4 pb-5 pt-5 max-lg:items-center max-lg:pb-4 max-lg:pt-3 sm:px-6">
          <div className="min-w-0 flex-1">
            <nav aria-label={t("Навигация")} className="mb-2 flex items-center gap-1.5 text-xs text-ink-3 max-lg:hidden">
              <Link href="/" className="hover:text-ink">
                OneBase
              </Link>
              <span aria-hidden>/</span>
              <span className="text-ink-2">{t("Снабжение")}</span>
            </nav>
            <h1 className="text-[1.375rem] font-semibold leading-tight tracking-tight text-ink sm:text-[1.625rem] lg:text-[1.875rem] lg:font-bold">{t("Снабжение")}</h1>
            <div className="mt-1.5 text-sm text-ink-3">
              <SummaryLine board={board} sum={sum} full={full} />
            </div>
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <div className="flex items-center gap-2 max-lg:hidden">
              <div className="flex rounded-xl border border-line bg-surface p-1">
                {presets.map((p) => (
                  <button
                    key={p.key}
                    type="button"
                    onClick={() => go(periodOf(p.key, today))}
                    aria-pressed={active === p.key}
                    className={`h-8 rounded-lg px-3 text-sm font-medium ${active === p.key ? "border border-line-strong bg-muted text-ink" : "text-ink-3 hover:text-ink"}`}
                  >
                    {t(p.label)}
                  </button>
                ))}
              </div>
              <RangePicker from={board.from} to={board.to} today={today} label={range} onApply={go} />
            </div>
            {board.access.canCreateRequest && (
              <Link
                href="/requests/new"
                aria-label={t("Новая заявка")}
                className="inline-flex h-10 items-center gap-2 rounded-xl bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong max-lg:size-10 max-lg:justify-center max-lg:rounded-full max-lg:px-0"
              >
                <Plus className="size-4" aria-hidden />
                <span className="max-lg:hidden">{t("Новая заявка")}</span>
              </Link>
            )}
          </div>
        </div>
      </header>
      <div className={`mx-auto flex max-w-[1800px] flex-col gap-4 px-4 py-6 transition-opacity max-lg:gap-4 max-lg:pb-[calc(150px+env(safe-area-inset-bottom))] max-lg:pt-4 sm:px-6 ${pending ? "opacity-60" : ""}`}>
        {/* Телефон: период и просрочки */}
        <div className="flex items-center justify-between gap-3 lg:hidden">
          <MobilePeriod active={active} label={range} from={board.from} to={board.to} today={today} onApply={go} />
          {overdue !== null && (
            <span className={`inline-flex items-center gap-1.5 text-sm font-semibold ${overdue > 0 ? "text-bad" : "text-ok"}`}>
              <ShieldCheck className="size-4" aria-hidden />
              {overdue > 0 ? pl(t, overdue, ["{n} просрочена", "{n} просрочено", "{n} просрочено"]) : t("Просрочек нет")}
            </span>
          )}
        </div>

        {ask && (
          <div className="max-lg:order-3">
            <SupplyAi ready={aiReady} onAsk={askAi} />
          </div>
        )}

        {board.empty && (
          <div className={`${card} flex flex-wrap items-center justify-between gap-4 p-5 max-lg:order-1`}>
            <div>
              <h2 className="text-base font-bold text-ink">{t("Начните с первой заявки")}</h2>
              <p className="mt-1 text-sm text-ink-3">{t("Заявки и покупки появятся здесь, как только начнётся работа.")}</p>
            </div>
            <div className="flex flex-wrap gap-2">
              {board.access.canCreateRequest && (
                <Link href="/requests/new" className="inline-flex h-10 items-center gap-2 rounded-xl bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong">
                  <Plus className="size-4" aria-hidden /> {t("Новая заявка")}
                </Link>
              )}
              {board.access.canManageSuppliers && (
                <Link href="/procurement/suppliers" className="inline-flex h-10 items-center gap-2 rounded-xl border border-line bg-surface px-4 text-sm font-semibold text-ink hover:bg-muted">
                  <UserPlus className="size-4" aria-hidden /> {t("Добавить поставщика")}
                </Link>
              )}
            </div>
          </div>
        )}

        <div className="max-lg:order-2">
          <Pipeline board={board} sum={sum} />
        </div>

        <div className={`grid gap-4 max-lg:contents ${showApprovals || showUpcoming ? "lg:grid-cols-[380px_minmax(0,1fr)] 2xl:grid-cols-[440px_minmax(0,1fr)]" : ""}`}>
          {showApprovals && (
            <div className="min-w-0 max-lg:order-1">
              <ApprovalCard board={board} sum={sum} full={full} busy={busy} error={error} onApprove={approve} />
            </div>
          )}
          {showUpcoming && (
            <div className="min-w-0 max-lg:order-4">
              <UpcomingCard board={board} />
            </div>
          )}
          {board.metrics ? (
            <div className="min-w-0 max-lg:order-4">
              <Metrics board={board} sum={sum} full={full} range={range} />
            </div>
          ) : board.errors.includes("metrics") ? (
            <div className="max-lg:order-4">
              <BlockError title="Показатели" onRetry={retry} />
            </div>
          ) : null}
        </div>

        {board.analytics ? (
          <div className="max-lg:order-5">
            <Analytics board={board} sum={sum} full={full} />
          </div>
        ) : board.errors.includes("analytics") ? (
          <div className="max-lg:order-5">
            <BlockError title="Аналитика закупок" onRetry={retry} />
          </div>
        ) : null}

        {board.team && board.team.length > 0 ? (
          <div className="max-lg:order-6">
            <Team board={board} />
          </div>
        ) : board.errors.includes("team") ? (
          <div className="max-lg:order-6">
            <BlockError title="Сотрудники снабжения" onRetry={retry} />
          </div>
        ) : null}

        {board.digests && board.digests.length > 0 && (
          <Link href="/procurement/digests" className={`${card} flex items-center gap-3 px-5 py-3.5 text-sm hover:border-line-strong max-lg:order-7`}>
            <Newspaper className="size-4 shrink-0 text-ink-3" aria-hidden />
            <span className="min-w-0 flex-1 truncate text-ink-2">
              <span className="font-semibold text-ink">{t("Ежедневные сводки")}</span> · {t("Сводка за {date}", { date: dateRu(board.digests[0].day) })} — {t(board.digests[0].summary)}
            </span>
            <span className="shrink-0 font-medium text-link">{t("Все сводки")}</span>
            <ChevronRight className="size-4 shrink-0 text-link" aria-hidden />
          </Link>
        )}
      </div>
    </>
  );
}

/** Итог под заголовком: заявки на согласовании и их сумма, просроченные поставки. */
function SummaryLine({ board, sum, full }: { board: Board; sum: (v: number) => string; full: (v: number) => string }) {
  const t = useT();
  const parts: React.ReactNode[] = [];
  const n = board.summary.pendingApprovalCount ?? 0;
  if (n > 0) {
    parts.push(
      <span key="ap" className={board.access.canApprove ? "font-semibold text-warn" : "text-ink-2"}>
        {board.access.canApprove
          ? pl(t, n, ["{n} заявка ждёт согласования", "{n} заявки ждут согласования", "{n} заявок ждут согласования"])
          : pl(t, n, ["{n} заявка на согласовании", "{n} заявки на согласовании", "{n} заявок на согласовании"])}
      </span>,
    );
    if (board.summary.pendingApprovalAmount)
      parts.push(
        <span key="sum" className="font-semibold tabular-nums text-ink-2" title={full(board.summary.pendingApprovalAmount)}>
          {sum(board.summary.pendingApprovalAmount)}
        </span>,
      );
  }

  const late = board.pipeline.overdueToBuy;
  if (late !== null)
    parts.push(
      <span key="late" className={`font-semibold ${late > 0 ? "text-bad" : "text-ok"}`}>
        {late > 0 ? pl(t, late, ["{n} заявка просрочена к закупке", "{n} заявки просрочены к закупке", "{n} заявок просрочено к закупке"]) : t("просроченных закупок нет")}
      </span>,
    );
  // Поставки по прежним заказам поставщикам — только если какая-то просрочена.
  const overdue = board.summary.overdueDeliveries;
  if (overdue)
    parts.push(
      <span key="od" className="font-semibold text-bad">
        {pl(t, overdue, ["{n} поставка просрочена", "{n} поставки просрочены", "{n} поставок просрочено"])}
      </span>,
    );
  if (parts.length === 0) return <span className="max-lg:hidden">{t("Обзор закупок и склада — по вашей роли")}</span>;
  return (
    <span className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm max-lg:hidden">
      {parts.map((p, i) => (
        <span key={i} className="inline-flex items-center gap-2">
          {i > 0 && <span className="text-subtle" aria-hidden>·</span>}
          {p}
        </span>
      ))}
    </span>
  );
}

/** Кнопка диапазона дат «📅 1–30 ноября ▾» — выбор двух дат во всплывающем окне. */
function RangePicker({ from, to, today, label, onApply }: { from: string; to: string; today: string; label: string; onApply: (r: { from: string; to: string }) => void }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [a, setA] = useState(from);
  const [b, setB] = useState(to);
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    setA(from);
    setB(to);
  }, [from, to]);
  useEffect(() => {
    if (!open) return;
    const close = (e: MouseEvent) => !box.current?.contains(e.target as Node) && setOpen(false);
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [open]);
  const field = "h-9 w-full rounded-lg border border-line bg-surface px-2.5 text-sm text-ink focus:border-accent focus:outline-none";
  return (
    <div ref={box} className="relative">
      <button type="button" onClick={() => setOpen(!open)} aria-expanded={open} className="inline-flex h-10 items-center gap-2 rounded-xl border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted">
        <Calendar className="size-4 text-ink-3" aria-hidden />
        <span className="tabular-nums">{label}</span>
        <ChevronDown className="size-4 text-ink-3" aria-hidden />
      </button>
      {open && (
        <div className="absolute right-0 top-12 z-30 w-72 rounded-2xl border border-line bg-surface-solid p-4 shadow-xl">
          <div className="grid grid-cols-2 gap-2">
            <label className="text-xs text-ink-3">
              {t("С")}
              <input type="date" value={a} max={b || today} onChange={(e) => setA(e.target.value)} className={`${field} mt-1`} />
            </label>
            <label className="text-xs text-ink-3">
              {t("По")}
              <input type="date" value={b} min={a} max={today} onChange={(e) => setB(e.target.value)} className={`${field} mt-1`} />
            </label>
          </div>
          <button
            type="button"
            disabled={!a || !b || a > b}
            onClick={() => {
              setOpen(false);
              onApply({ from: a, to: b });
            }}
            className="mt-3 h-9 w-full rounded-lg bg-accent text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50"
          >
            {t("Показать")}
          </button>
        </div>
      )}
    </div>
  );
}

/** Телефон: чип периода и нижний лист с выбором (пресеты и даты). */
function MobilePeriod({ active, label, from, to, today, onApply }: { active?: Preset; label: string; from: string; to: string; today: string; onApply: (r: { from: string; to: string }) => void }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [a, setA] = useState(from);
  const [b, setB] = useState(to);
  useEffect(() => {
    setA(from);
    setB(to);
  }, [from, to]);
  const field = "h-11 w-full rounded-xl border border-line bg-surface px-3 text-base text-ink";
  const pick = (r: { from: string; to: string }) => {
    setOpen(false);
    onApply(r);
  };
  return (
    <>
      <button type="button" onClick={() => setOpen(true)} className="inline-flex h-10 items-center gap-2 rounded-full border border-accent/30 bg-accent-soft px-4 text-sm font-semibold text-link">
        <Calendar className="size-4" aria-hidden />
        {active ? t(active === "prev" ? "Прошлый месяц" : presets.find((p) => p.key === active)!.label) : label}
        <ChevronDown className="size-4" aria-hidden />
      </button>
      {open && (
        <div className="fixed inset-0 z-50 flex items-end bg-black/40" onClick={() => setOpen(false)}>
          <div className="w-full rounded-t-3xl border-t border-line bg-surface-solid p-5 pb-[calc(20px+env(safe-area-inset-bottom))]" onClick={(e) => e.stopPropagation()}>
            <div className="mb-4 flex items-center justify-between">
              <h2 className="text-lg font-bold text-ink">{t("Период")}</h2>
              <button type="button" onClick={() => setOpen(false)} aria-label={t("Закрыть")} className="flex size-11 items-center justify-center rounded-full text-ink-3 hover:bg-muted">
                <X className="size-5" />
              </button>
            </div>
            <div className="grid grid-cols-2 gap-2">
              {presets.map((p) => (
                <button
                  key={p.key}
                  type="button"
                  onClick={() => pick(periodOf(p.key, today))}
                  className={`h-12 rounded-xl border text-sm font-semibold ${active === p.key ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink"}`}
                >
                  {t(p.key === "prev" ? "Прошлый месяц" : p.label)}
                </button>
              ))}
            </div>
            <div className="mt-4 grid grid-cols-2 gap-2">
              <label className="text-xs text-ink-3">
                {t("С")}
                <input type="date" value={a} max={b || today} onChange={(e) => setA(e.target.value)} className={`${field} mt-1`} />
              </label>
              <label className="text-xs text-ink-3">
                {t("По")}
                <input type="date" value={b} min={a} max={today} onChange={(e) => setB(e.target.value)} className={`${field} mt-1`} />
              </label>
            </div>
            <button type="button" disabled={!a || !b || a > b} onClick={() => pick({ from: a, to: b })} className="mt-4 h-12 w-full rounded-xl bg-accent text-base font-semibold text-white disabled:opacity-50">
              {t("Показать")}
            </button>
          </div>
        </div>
      )}
    </>
  );
}

/** Supply AI: поле вопроса и короткие вопросы-чипы. Вопрос уходит в существующий AI-консультант (сотрудник «Снабжение») с периодом. */
function SupplyAi({ ready, onAsk }: { ready: boolean; onAsk: (q: string) => void }) {
  const t = useT();
  const [text, setText] = useState("");
  const tile = "flex size-9 shrink-0 items-center justify-center rounded-xl bg-[linear-gradient(135deg,#7c5cff,#3d7bff)] text-white";
  return (
    <section className="rounded-2xl border border-violet/25 bg-[linear-gradient(120deg,var(--color-violet-soft),var(--color-accent-soft))] p-4 max-lg:rounded-[20px]">
      {/* Компьютер: строка вопроса и чипы */}
      <div className="max-lg:hidden">
        <form
          onSubmit={(e) => {
            e.preventDefault();
            onAsk(text);
          }}
          className="flex items-center gap-3"
        >
          <span className={tile}>
            <Sparkles className="size-5" aria-hidden />
          </span>
          <div className="relative flex-1">
            <input
              value={text}
              onChange={(e) => setText(e.target.value)}
              disabled={!ready}
              placeholder={t("Спросите Supply AI о закупках, поставщиках или складе…")}
              aria-label={t("Вопрос Supply AI")}
              className="h-11 w-full rounded-xl border border-line-strong bg-surface pl-4 pr-12 text-sm text-ink placeholder:text-ink-3 focus:border-violet focus:outline-none disabled:opacity-60"
            />
            <button type="submit" disabled={!ready || !text.trim()} aria-label={t("Спросить")} className="absolute right-1.5 top-1.5 flex size-8 items-center justify-center rounded-full bg-accent text-white disabled:opacity-40">
              <ArrowUp className="size-4" aria-hidden />
            </button>
          </div>
        </form>
        <div className="mt-3 flex h-8 flex-wrap items-center gap-2 overflow-hidden">
          <span className="text-xs font-medium text-ink-3">{ready ? t("Например:") : t("Supply AI временно недоступен")}</span>
          {questions.map((q) => (
            <button
              key={q.text}
              type="button"
              disabled={!ready}
              onClick={() => onAsk(t(q.text))}
              className="inline-flex h-8 items-center gap-1.5 rounded-2xl border border-line bg-surface px-3 text-xs font-medium text-ink hover:border-violet/40 disabled:opacity-50"
            >
              <q.icon className="size-3.5 text-violet" aria-hidden /> {t(q.text)}
            </button>
          ))}
        </div>
      </div>
      {/* Телефон: заголовок и три вопроса строками */}
      <div className="lg:hidden">
        <div className="flex items-center gap-3">
          <span className={tile}>
            <Sparkles className="size-5" aria-hidden />
          </span>
          <h2 className="text-base font-bold text-ink">{t("Спросить Supply AI")}</h2>
        </div>
        {!ready && <p className="mt-2 text-sm text-ink-3">{t("Supply AI временно недоступен")}</p>}
        <div className="mt-3 grid gap-2">
          {[questions[2], questions[3], questions[1]].map((q) => (
            <button
              key={q.text}
              type="button"
              disabled={!ready}
              onClick={() => onAsk(t(q.text))}
              className="flex min-h-11 items-center gap-3 rounded-xl border border-line bg-surface px-3 text-left text-sm font-medium text-ink disabled:opacity-50"
            >
              <q.icon className="size-4 shrink-0 text-violet" aria-hidden />
              <span className="flex-1">{t(q.text)}</span>
              <ArrowUpRight className="size-4 shrink-0 text-ink-3" aria-hidden />
            </button>
          ))}
        </div>
      </div>
    </section>
  );
}

type Stage = { key: string; icon: typeof Stamp; count: number; title: string; hint: string; href: string; action: boolean; amount?: number | null };

const Icon = ({ icon: I }: { icon: typeof Truck }) => <I className="size-3.5" aria-hidden />;

/** Путь закупки: на согласовании → к закупке → на складе (кладовщику — только склад) и пилюли рисков. */
function Pipeline({ board, sum }: { board: Board; sum: (v: number) => string }) {
  const t = useT();
  const p = board.pipeline;
  // Три этапа: заявки на согласовании → к закупке (снабжение покупает) → на складе (ждут выдачи сотруднику).
  const stages: Stage[] = [
    ...(p.onApprovalCount !== null
      ? [{
          key: "appr",
          icon: Stamp,
          count: p.onApprovalCount,
          title: "Заявки",
          hint: "на согласовании",
          href: board.access.canApprove ? "/requests/approvals" : "/requests/all?status=HeadApproval,FinanceApproval,SupplyApproval,DirectorApproval,Submitted",
          action: board.access.canApprove,
          amount: p.onApprovalAmount,
        }]
      : []),
    ...(p.requestsAwaitingOrder !== null
      ? [{
          key: "buy",
          icon: ShoppingBag,
          count: p.requestsAwaitingOrder,
          title: "К закупке",
          hint: "снабжение покупает",
          href: "/procurement/buy",
          action: board.access.canCreateOrder,
          amount: p.toBuyAmount,
        }]
      : []),
    { key: "stock", icon: PackageCheck, count: p.toIssue, title: "На складе", hint: "ждут выдачи", href: "/procurement/issue", action: false },
  ];
  const mobileTitle: Record<string, string> = { appr: "На согласовании", buy: "К закупке", stock: "На складе, ждут выдачи" };
  const risk = (n: number | null, icon: typeof Truck, label: string, href: string) =>
    n === null ? null : (
      <Link href={href} className={`inline-flex h-8 items-center gap-1.5 rounded-lg px-3 text-xs font-semibold ${n > 0 ? "bg-bad-soft text-bad" : "bg-ok-soft text-ok"}`}>
        <Icon icon={icon} />
        {t(label)} {n}
      </Link>
    );
  return (
    <>
    <h2 className="mb-2 px-1 text-[15px] font-bold text-ink-3 lg:hidden">{t("Путь закупки")}</h2>
    <section className={`${card} p-5 max-lg:p-0`}>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3 max-lg:hidden">
        <h2 className="flex items-center gap-2 text-base font-bold text-ink">
          <Workflow className="size-4 text-ink-3" aria-hidden /> {t("Путь закупки")} <span className="text-sm font-normal text-subtle">{t("сейчас в работе")}</span>
        </h2>
        <div className="flex flex-wrap gap-2">
          {risk(p.overdueToBuy, CalendarClock, "Просрочено к закупке", "/procurement/buy")}
          {board.summary.overdueDeliveries ? risk(board.summary.overdueDeliveries, Truck, "Просрочено поставок", "/procurement/orders?status=overdue") : null}
          {risk(board.summary.overdueTasks, AlarmClock, "Просрочено задач", "/tasks/supply")}
        </div>
      </div>
      {/* Компьютер: этапы в ряд */}
      <div className="flex items-stretch gap-2 max-lg:hidden">
        {stages.map((s, i) => {
          const tone = s.count > 0 ? (s.action ? "warn" : "accent") : "zero";
          return (
            <div key={s.key} className="flex min-w-0 flex-1 items-center gap-1.5 2xl:gap-2">
              {i > 0 && <ChevronRight className="size-4 shrink-0 text-subtle max-2xl:hidden" aria-hidden />}
              <Link
                href={s.href}
                className={`flex min-w-0 flex-1 items-center gap-2.5 rounded-xl border px-3 py-3 2xl:gap-3 2xl:px-3.5 ${
                  tone === "warn" ? "border-warn/30 bg-warn-soft" : tone === "accent" ? "border-accent/20 bg-accent-soft" : "border-line bg-page hover:border-line-strong"
                }`}
              >
                <span className={`flex size-[30px] shrink-0 items-center justify-center rounded-lg 2xl:size-[34px] ${tone === "warn" ? "bg-warn/15 text-warn" : tone === "accent" ? "bg-accent/15 text-link" : "bg-muted text-subtle"}`}>
                  <s.icon className="size-[18px]" aria-hidden />
                </span>
                <span className="min-w-0">
                  <span className="flex items-baseline gap-2">
                    <b className={`text-[22px] font-bold leading-none tabular-nums ${tone === "zero" ? "text-subtle" : "text-ink"}`}>{s.count}</b>
                    <span className={`truncate text-[13px] font-semibold ${tone === "zero" ? "text-ink-3" : "text-ink"}`}>{t(s.title)}</span>
                  </span>
                  <span className={`mt-1 block truncate text-[11px] ${tone === "warn" ? "font-medium text-warn" : "text-subtle"}`}>
                    {s.amount ? sum(s.amount) : t(s.hint)}
                  </span>
                </span>
              </Link>
            </div>
          );
        })}
      </div>
      {/* Телефон: этапы списком */}
      <div className="lg:hidden">
        <ul className="divide-y divide-line">
          {stages.map((s) => {
            const action = s.action && s.count > 0;
            return (
              <li key={s.key}>
                <Link href={s.href} className={`flex min-h-[60px] items-center gap-3 px-4 ${action ? "bg-warn-soft" : ""}`}>
                  <span className={`flex size-8 shrink-0 items-center justify-center rounded-lg ${action ? "bg-warn/15 text-warn" : "bg-muted text-ink-3"}`}>
                    <s.icon className="size-4" aria-hidden />
                  </span>
                  <span className={`flex-1 text-[15px] font-medium ${action ? "text-ink" : "text-ink-2"}`}>{t(mobileTitle[s.key])}</span>
                  <b className={`text-[17px] font-bold tabular-nums ${action ? "text-warn" : s.count > 0 ? "text-ink" : "text-subtle"}`}>{s.count}</b>
                  <ChevronRight className="size-4 text-subtle" aria-hidden />
                </Link>
              </li>
            );
          })}
        </ul>
      </div>
    </section>
    </>
  );
}

/** «Ждёт согласования»: до трёх заявок директора; одна — «Согласовать» прямо здесь (с подтверждением), несколько — «Все N →». */
function ApprovalCard({ board, sum, full, busy, error, onApprove }: { board: Board; sum: (v: number) => string; full: (v: number) => string; busy: string | null; error: string | null; onApprove: (id: string) => void }) {
  const t = useT();
  const list = board.approvals ?? [];
  const total = Math.max(board.myApprovalCount, list.length);
  const one = total === 1 && list.length === 1 ? list[0] : null;
  const orders = board.ordersOnApproval > 0 ? pl(t, board.ordersOnApproval, ["{n} заказ на согласовании", "{n} заказа на согласовании", "{n} заказов на согласовании"]) : null;
  return (
    <section className="flex h-full flex-col gap-4 rounded-2xl border border-warn/25 bg-[linear-gradient(160deg,var(--color-warn-soft),transparent_70%)] bg-surface p-5 max-lg:rounded-[20px] max-lg:p-4">
      <div className="flex items-center gap-3">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-warn/15 text-warn">
          <Stamp className="size-5" aria-hidden />
        </span>
        <div className="min-w-0 flex-1">
          <h2 className="text-[15px] font-bold text-ink">{t("Ждёт согласования")}</h2>
          <p className="text-xs text-ink-3">
            {pl(t, total, ["{n} заявка", "{n} заявки", "{n} заявок"])}
            {orders && ` · ${orders}`}
          </p>
        </div>
        {one && (
          <b className="text-base font-bold tabular-nums text-ink lg:hidden" title={full(one.amount)}>
            {compactMoneyShort(one.amount, sum)}
          </b>
        )}
      </div>
      <ul className="grid grid-cols-1 gap-2 max-lg:hidden">
        {list.map((a) => (
          <li key={a.id}>
            <Link href={`/requests/${a.id}`} className="block rounded-xl border border-line bg-page px-4 py-3 hover:border-line-strong">
              <span className="flex items-baseline justify-between gap-3">
                <span className="truncate text-sm font-semibold text-ink">{a.title || a.number}</span>
                <b className="shrink-0 text-[15px] font-bold tabular-nums text-ink" title={full(a.amount)}>
                  {sum(a.amount)}
                </b>
              </span>
              <span className="mt-1.5 flex items-center gap-2 text-xs text-ink-3">
                <span className="inline-flex shrink-0 items-center gap-1.5 rounded-md bg-warn-soft px-2 py-0.5 font-semibold text-warn">
                  <span className="size-1.5 rounded-full bg-warn" aria-hidden />
                  {t("на согласовании")}
                </span>
                <span className="min-w-0 truncate">
                  <span className="tabular-nums">{a.number}</span> · {a.department} · {a.initiator}
                </span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
      {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      <div className="mt-auto grid grid-cols-2 gap-2">
        {one ? (
          <>
            <button
              type="button"
              onClick={() => onApprove(one.id)}
              disabled={busy !== null}
              className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-accent text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-60"
            >
              {busy === one.id ? <RefreshCw className="size-4 animate-spin" aria-hidden /> : <Check className="size-4" aria-hidden />} {t("Согласовать")}
            </button>
            <Link href={`/requests/${one.id}`} className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-line bg-surface text-sm font-semibold text-ink hover:bg-muted">
              <ExternalLink className="size-4 max-lg:hidden" aria-hidden /> <span className="max-lg:hidden">{t("Открыть заявку")}</span>
              <span className="lg:hidden">{t("Открыть")}</span>
            </Link>
          </>
        ) : (
          <Link href="/requests/approvals" className="col-span-2 inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-accent text-sm font-semibold text-white hover:bg-accent-strong">
            {t("Все {n}", { n: total })} <ChevronRight className="size-4" aria-hidden />
          </Link>
        )}
      </div>
    </section>
  );
}

const compactMoneyShort = (v: number, sum: (v: number) => string) => sum(v).replace(/\s\S+$/, "");

/** На месте согласования, когда согласовывать нечего: ближайшие поставки со сроком. */
function UpcomingCard({ board }: { board: Board }) {
  const t = useT();
  return (
    <section className={`${card} flex h-full flex-col gap-3 p-5`}>
      <h2 className="flex items-center gap-2 text-[15px] font-bold text-ink">
        <Truck className="size-4 text-ink-3" aria-hidden /> {t("Ближайшие поставки")}
      </h2>
      <ul className="grid grid-cols-1 gap-2">
        {(board.upcoming ?? []).map((u) => (
          <li key={u.id}>
            <Link href={`/procurement/orders/${u.id}`} className="flex items-center justify-between gap-3 rounded-xl border border-line bg-page px-4 py-3 hover:border-line-strong">
              <span className="min-w-0">
                <span className="block truncate text-sm font-semibold text-ink">{u.supplierName}</span>
                <span className="text-xs tabular-nums text-ink-3">{u.number}</span>
              </span>
              <span className="shrink-0 text-sm font-semibold tabular-nums text-ink-2">{dateRu(u.expectedDate)}</span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}

type Cell = { label: string; value: string | null; hint: string; title?: string };

/** Показатели периода 4×2: пустые значения приглушены и объяснены. */
function Metrics({ board, sum, full, range }: { board: Board; sum: (v: number) => string; full: (v: number) => string; range: string }) {
  const t = useT();
  const locale = useLocale();
  // Месяц целиком или с начала месяца — «за ноябрь», иначе — диапазон.
  const periodTitle =
    board.from.endsWith("-01") && board.from.slice(0, 7) === board.to.slice(0, 7)
      ? formatDate(new Date(`${board.from}T12:00:00Z`), locale, { month: "long" })
      : range;
  const m = board.metrics!;
  const k = m.kpis;
  const days = (v: number) => `${v.toString().replace(".", ",")} ${t("дн.")}`;
  const cells: Cell[] = [
    { label: "Закуплено", value: sum(m.purchasedAmount), title: full(m.purchasedAmount), hint: t("покупок: {n}", { n: m.ordersCount }) },
    { label: "Поступило на склад", value: sum(m.receivedAmount), title: full(m.receivedAmount), hint: t("по ценам покупки") },
    k.savingsLines > 0
      ? { label: "Экономия", value: sum(k.savings), title: full(k.savings), hint: k.savingsPercent !== null ? t("{p}% к ценам заявок", { p: k.savingsPercent.toString().replace(".", ",") }) : t("к ценам заявок") }
      : { label: "Экономия", value: null, hint: t("в заявках не было цен") },
    { label: "Выполнено заявок", value: String(k.fulfilledRequests), hint: t("перешли в «Выполнена»") },
    k.onTimePercent !== null
      ? { label: "Поставки в срок", value: `${k.onTimePercent.toString().replace(".", ",")}%`, hint: t("поставок со сроком: {n}", { n: k.deliveriesMeasured }) }
      : { label: "Поставки в срок", value: null, hint: t("нет заказов со сроком") },
    { label: "Среднее время закупки", value: k.avgPurchaseDays !== null ? days(k.avgPurchaseDays) : null, hint: t("от утверждения до покупки") },
    k.supplierQuality !== null
      ? { label: "Качество поставщиков", value: `${Math.round(k.supplierQuality)} / 100`, hint: t("поставщиков: {n}", { n: k.suppliersRated }) }
      : { label: "Качество поставщиков", value: null, hint: t("нет оценок") },
    { label: "Претензии", value: String(k.claims), hint: t("брак или недостача") },
  ];
  const zero = (c: Cell) => c.value === null || /^0(\s|$)/.test(c.value);
  const allEmpty = cells.every(zero);
  return (
    <section className={`${card} @container h-full p-5 max-lg:p-4`}>
      <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="flex items-center gap-2 text-[15px] font-bold text-ink">
          <Gauge className="size-4 text-ink-3" aria-hidden /> {t("Показатели за {period}", { period: periodTitle })}
        </h2>
        <span className="text-xs text-subtle max-lg:hidden">
          {range}
          {allEmpty && ` · ${t("данные копятся")}`}
        </span>
      </div>
      {/* Ширина карточки решает сетку: узкая (рядом с согласованием, телефон) — 2×4, широкая — 4×2. */}
      <div className="grid grid-cols-2 @[580px]:grid-cols-4">
        {cells.map((c, i) => (
          <div
            key={c.label}
            className={`border-line px-4 py-3 ${i % 2 === 1 ? "border-l" : "pl-0"} ${i >= 2 ? "border-t" : ""} ${
              i % 4 === 0 ? "@[580px]:border-l-0 @[580px]:pl-0" : "@[580px]:border-l @[580px]:pl-4"
            } ${i < 4 ? "@[580px]:border-t-0" : "@[580px]:border-t"}`}
          >
            <p className="text-xs text-ink-3">{t(c.label)}</p>
            <p className={`mt-1 whitespace-nowrap text-[18px] font-bold tabular-nums @[580px]:text-[17px] @[760px]:text-[20px] ${zero(c) ? "text-subtle" : "text-ink"}`} title={c.title}>
              {c.value ?? "—"}
            </p>
            <p className="mt-0.5 text-[11px] text-subtle max-lg:hidden">{c.hint}</p>
          </div>
        ))}
      </div>
      {allEmpty && <p className="mt-3 text-xs text-ink-3 lg:hidden">{t("Значения появятся после первых покупок")}</p>}
    </section>
  );
}

type Tab = "months" | "departments" | "categories" | "suppliers";

/** Аналитика: вкладки «по месяцам», «по отделам», «по категориям», «поставщики»; без данных — одно пустое состояние на весь блок. */
function Analytics({ board, sum, full }: { board: Board; sum: (v: number) => string; full: (v: number) => string }) {
  const t = useT();
  const locale = useLocale();
  const a = board.analytics!;
  const [tab, setTab] = useState<Tab>("months");
  const [table, setTable] = useState(false);
  const hasData = a.monthly.some((m) => m.amount > 0);
  const tabs: { key: Tab; label: string }[] = [
    { key: "months", label: "По месяцам" },
    { key: "departments", label: "По отделам" },
    { key: "categories", label: "По категориям" },
    { key: "suppliers", label: "Поставщики" },
  ];
  const monthName = (key: string) => formatDate(new Date(`${key}-15T12:00:00Z`), locale, { month: "short" }).replace(".", "").slice(0, 3);
  const ghost = [42, 58, 36, 62, 50, 72, 54, 68, 46, 60, 64, 40];
  return (
    <section className={`${card} p-5 max-lg:p-4`}>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
        <h2 className="flex items-center gap-2 text-[15px] font-bold text-ink">
          <ChartColumn className="size-4 text-ink-3" aria-hidden /> {t("Аналитика закупок")}
        </h2>
        {hasData && (
          <div className="flex max-w-full overflow-x-auto rounded-xl border border-line bg-surface p-1">
            {tabs.map((x) => (
              <button
                key={x.key}
                type="button"
                onClick={() => setTab(x.key)}
                aria-pressed={tab === x.key}
                className={`h-8 shrink-0 rounded-lg px-3 text-sm font-medium ${tab === x.key ? "border border-line-strong bg-muted text-ink" : "text-ink-3 hover:text-ink"}`}
              >
                {t(x.label)}
              </button>
            ))}
          </div>
        )}
      </div>

      {!hasData ? (
        <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_380px]">
          <div className="relative rounded-xl border border-line bg-page p-5 max-lg:p-3">
            <p className="mb-4 flex items-center justify-center gap-2 text-sm font-medium text-ink-2 max-lg:hidden">
              <ChartColumn className="size-4 text-subtle" aria-hidden /> {t("За последние 12 месяцев покупок нет")}
            </p>
            <div className="flex h-32 items-end gap-2 opacity-35 max-lg:h-16" aria-hidden>
              {ghost.map((h, i) => (
                <span key={i} className="flex-1 rounded-t-md border border-line-strong bg-muted" style={{ height: `${h}%` }} />
              ))}
            </div>
            <div className="mt-2 flex gap-2 max-lg:hidden" aria-hidden>
              {a.monthly.map((m) => (
                <span key={m.key} className="flex-1 text-center text-[11px] text-subtle">
                  {monthName(m.key)}
                </span>
              ))}
            </div>
          </div>
          <div className="flex flex-col">
            <h3 className="text-sm font-bold text-ink max-lg:hidden">{t("Появится после первых покупок")}</h3>
            <p className="text-sm text-ink-2 lg:hidden">{t("Динамика по месяцам, расходы по отделам и категориям, рейтинг поставщиков появятся после первых покупок")}</p>
            <ul className="mt-3 grid gap-3 max-lg:hidden">
              {[
                { icon: ChartColumn, title: "Динамика закупок по месяцам", hint: "покупки по заявкам, сум" },
                { icon: Building, title: "Расходы по отделам", hint: "по купленным позициям" },
                { icon: Tags, title: "Расходы по категориям", hint: "по купленным позициям" },
                { icon: Star, title: "Рейтинг поставщиков", hint: "сроки, качество, претензии" },
              ].map((x) => (
                <li key={x.title} className="flex items-start gap-3">
                  <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-muted text-ink-3">
                    <x.icon className="size-4" aria-hidden />
                  </span>
                  <span>
                    <b className="block text-sm font-semibold text-ink">{t(x.title)}</b>
                    <span className="text-xs text-subtle">{t(x.hint)}</span>
                  </span>
                </li>
              ))}
            </ul>
            <div className="mt-4 grid grid-cols-2 gap-2 lg:mt-auto lg:pt-4">
              {board.access.canCreateOrder && (
                <Link href="/procurement/buy" className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-accent/30 bg-accent-soft text-sm font-semibold text-link">
                  <ShoppingBag className="size-4" aria-hidden /> {t("К закупке")}
                </Link>
              )}
              {board.access.canManageSuppliers && (
                <Link href="/procurement/suppliers" className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-line bg-surface text-sm font-semibold text-ink hover:bg-muted">
                  <UserPlus className="size-4" aria-hidden /> <span className="max-lg:hidden">{t("Добавить поставщика")}</span>
                  <span className="lg:hidden">{t("Поставщик")}</span>
                </Link>
              )}
            </div>
          </div>
        </div>
      ) : tab === "months" ? (
        <MonthBars data={a.monthly} sum={sum} full={full} monthName={monthName} table={table} />
      ) : tab === "departments" ? (
        <ShareBars data={a.byDepartment} sum={sum} full={full} table={table} />
      ) : tab === "categories" ? (
        <ShareBars data={a.byCategory} sum={sum} full={full} table={table} />
      ) : (
        <SupplierTable rows={a.suppliers} sum={sum} full={full} />
      )}
      {hasData && tab !== "suppliers" && (
        <button type="button" onClick={() => setTable(!table)} className="mt-3 text-xs font-medium text-link hover:underline">
          {table ? t("Показать график") : t("Таблица")}
        </button>
      )}
    </section>
  );
}

/** Столбики за 12 месяцев: текущий — акцентом, остальные — приглушённым акцентом; подсказка по наведению и фокусу. */
function MonthBars({ data, sum, full, monthName, table }: { data: Amount[]; sum: (v: number) => string; full: (v: number) => string; monthName: (key: string) => string; table: boolean }) {
  const t = useT();
  const [hover, setHover] = useState<number | null>(null);
  const max = Math.max(1, ...data.map((d) => d.amount));
  const last = data.length - 1;
  if (table)
    return (
      <AmountTable rows={data.map((d) => ({ key: d.key, label: monthName(d.key) + " " + d.key.slice(0, 4), amount: d.amount, count: d.count }))} full={full} countLabel="покупок" />
    );
  return (
    <div className="relative">
      <div className="flex h-44 items-end gap-2" role="img" aria-label={t("Закупки по месяцам")}>
        {data.map((d, i) => (
          <button
            key={d.key}
            type="button"
            onPointerEnter={() => setHover(i)}
            onPointerLeave={() => setHover(null)}
            onFocus={() => setHover(i)}
            onBlur={() => setHover(null)}
            aria-label={`${monthName(d.key)}: ${full(d.amount)}`}
            className="flex h-full flex-1 items-end justify-center focus:outline-none"
          >
            <span className={`block w-full max-w-7 rounded-t-md ${i === last ? "bg-accent" : "bg-accent/30"}`} style={{ height: `${Math.max(d.amount > 0 ? 2 : 0, (d.amount / max) * 100)}%` }} />
          </button>
        ))}
      </div>
      <div className="mt-2 flex gap-2">
        {data.map((d, i) => (
          <span key={d.key} className={`flex-1 text-center text-[11px] ${i === last ? "font-semibold text-ink-2" : "text-subtle"}`}>
            {monthName(d.key)}
          </span>
        ))}
      </div>
      {hover !== null && (
        <div
          role="tooltip"
          className="pointer-events-none absolute top-0 z-10 -translate-x-1/2 rounded-lg border border-line bg-surface-solid px-3 py-2 text-xs shadow-lg"
          style={{ left: `${((hover + 0.5) / data.length) * 100}%` }}
        >
          <b className="block tabular-nums text-ink">{sum(data[hover].amount)}</b>
          <span className="text-ink-3">
            {monthName(data[hover].key)} · {t("покупок: {n}", { n: data[hover].count })}
          </span>
        </div>
      )}
    </div>
  );
}

/** Горизонтальные полосы с суммой и долей; больше восьми — остальное в «Прочее». */
function ShareBars({ data, sum, full, table }: { data: Amount[]; sum: (v: number) => string; full: (v: number) => string; table: boolean }) {
  const t = useT();
  const total = data.reduce((s, d) => s + d.amount, 0);
  const shown = data.length > 8 ? [...data.slice(0, 7), { key: "other", label: t("Прочее"), amount: data.slice(7).reduce((s, d) => s + d.amount, 0), count: data.slice(7).reduce((s, d) => s + d.count, 0) }] : data;
  const max = Math.max(1, ...shown.map((d) => d.amount));
  const share = (v: number) => (total > 0 ? `${((v / total) * 100).toFixed(1).replace(".", ",")}%` : "—");
  if (data.length === 0) return <p className="py-8 text-center text-sm text-ink-3">{t("За этот период закупок не было.")}</p>;
  if (table) return <AmountTable rows={shown} full={full} countLabel="позиций" />;
  return (
    <ul className="grid gap-3">
      {shown.map((d) => (
        <li key={d.key} title={full(d.amount)}>
          <div className="flex items-baseline justify-between gap-3 text-sm">
            <span className="truncate text-ink-2">{d.label}</span>
            <span className="shrink-0 tabular-nums">
              <b className="font-semibold text-ink">{sum(d.amount)}</b> <span className="text-xs text-subtle">{share(d.amount)}</span>
            </span>
          </div>
          <div className="mt-1.5 h-2 rounded-full bg-muted">
            <div className="h-2 rounded-full bg-accent" style={{ width: `${Math.max(1, (d.amount / max) * 100)}%` }} />
          </div>
        </li>
      ))}
    </ul>
  );
}

function AmountTable({ rows, full, countLabel }: { rows: Amount[]; full: (v: number) => string; countLabel: string }) {
  const t = useT();
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
            <th className="py-2 pr-3 font-semibold">{t("Название")}</th>
            <th className="py-2 pr-3 text-right font-semibold">{t("Сумма")}</th>
            <th className="py-2 text-right font-semibold">{t(countLabel)}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.key} className="border-b border-line last:border-0">
              <td className="py-2 pr-3 text-ink-2">{r.label}</td>
              <td className="py-2 pr-3 text-right tabular-nums text-ink">{full(r.amount)}</td>
              <td className="py-2 text-right tabular-nums text-ink-2">{r.count}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function SupplierTable({ rows, sum, full }: { rows: NonNullable<Board["analytics"]>["suppliers"]; sum: (v: number) => string; full: (v: number) => string }) {
  const t = useT();
  if (rows.length === 0) return <p className="py-8 text-center text-sm text-ink-3">{t("За этот период закупок не было.")}</p>;
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[560px] text-sm">
        <thead>
          <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
            <th className="py-2 pr-3 font-semibold">{t("Поставщик")}</th>
            <th className="py-2 pr-3 text-right font-semibold">{t("Заказов")}</th>
            <th className="py-2 pr-3 text-right font-semibold">{t("Сумма")}</th>
            <th className="py-2 pr-3 text-right font-semibold">{t("В срок")}</th>
            <th className="py-2 pr-3 text-right font-semibold">{t("Претензии")}</th>
            <th className="py-2 text-right font-semibold">{t("Оценка")}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.id} className="border-b border-line last:border-0">
              <td className="py-2.5 pr-3">
                <Link href={`/procurement/suppliers/${r.id}`} className="font-medium text-ink hover:text-link">
                  {r.name}
                </Link>
              </td>
              <td className="py-2.5 pr-3 text-right tabular-nums text-ink-2">{r.orders}</td>
              <td className="py-2.5 pr-3 text-right tabular-nums text-ink" title={full(r.total)}>
                {sum(r.total)}
              </td>
              <td className={`py-2.5 pr-3 text-right tabular-nums ${r.onTime === null ? "text-subtle" : "text-ink-2"}`}>{r.onTime === null ? "—" : `${r.onTime.toString().replace(".", ",")}%`}</td>
              <td className={`py-2.5 pr-3 text-right tabular-nums ${r.claims > 0 ? "font-semibold text-bad" : "text-subtle"}`}>{r.claims}</td>
              <td className={`py-2.5 text-right tabular-nums ${r.rating === null ? "text-subtle" : "font-semibold text-ink"}`}>{ratingText(r.rating)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

const avatarTones = ["bg-violet", "bg-teal", "bg-pink", "bg-accent", "bg-cyan"];
const toneOf = (id: string) => avatarTones[[...id].reduce((s, c) => s + c.charCodeAt(0), 0) % avatarTones.length];

/** Сотрудники снабжения: задачи, куплено за период, просрочки и нагрузка. */
function Team({ board }: { board: Board }) {
  const t = useT();
  const rows = board.team ?? [];
  const [all, setAll] = useState(false);
  const shown = all ? rows : rows.slice(0, 6);
  const level = { free: "свободен", low: "низкая", high: "высокая" } as const;
  return (
    <section className={`${card} overflow-hidden`}>
      <div className="flex items-center justify-between gap-3 px-5 py-4">
        <h2 className="flex items-center gap-2 text-[15px] font-bold text-ink">
          <Users className="size-4 text-ink-3" aria-hidden /> {t(board.access.canSeeTeam ? "Сотрудники снабжения" : "Моя работа")}
          {board.access.canSeeTeam && <span className="text-sm font-normal text-subtle">{rows.length}</span>}
        </h2>
        {rows.length > 6 && (
          <button type="button" onClick={() => setAll(!all)} className="inline-flex items-center gap-1 text-sm font-medium text-link">
            {all ? t("Свернуть") : t("Все")} <ChevronRight className={`size-4 ${all ? "-rotate-90" : ""}`} aria-hidden />
          </button>
        )}
      </div>
      {/* Компьютер: таблица */}
      <table className="w-full text-sm max-lg:hidden">
        <thead>
          <tr className="bg-muted text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3">
            <th className="px-5 py-2.5 font-semibold">{t("Сотрудник")}</th>
            <th className="px-3 py-2.5 font-semibold">{t("Роль")}</th>
            <th className="px-3 py-2.5 text-right font-semibold">{t("Задачи")}</th>
            <th className="px-3 py-2.5 text-right font-semibold" title={t("Покупки за выбранный период")}>
              {t("Куплено")}
            </th>
            <th className="px-3 py-2.5 text-right font-semibold">{t("Просрочено")}</th>
            <th className="w-64 px-5 py-2.5 font-semibold">{t("Нагрузка")}</th>
          </tr>
        </thead>
        <tbody>
          {shown.map((r) => (
            <tr key={r.userId} className="border-t border-line">
              <td className="px-5 py-3">
                <span className="flex items-center gap-3">
                  <span className={`flex size-7 shrink-0 items-center justify-center rounded-full text-[11px] font-bold text-white ${toneOf(r.userId)}`}>{initialsOf(r.name)}</span>
                  <span className="font-medium text-ink">{r.name}</span>
                </span>
              </td>
              <td className="px-3 py-3 text-ink-2">{t(r.role)}</td>
              <td className={`px-3 py-3 text-right tabular-nums ${r.tasks > 0 ? "font-semibold text-ink" : "text-subtle"}`}>{r.tasks}</td>
              <td className={`px-3 py-3 text-right tabular-nums ${r.bought > 0 ? "font-semibold text-ink" : "text-subtle"}`}>{r.bought}</td>
              <td className={`px-3 py-3 text-right tabular-nums ${r.overdue > 0 ? "font-semibold text-bad" : "text-subtle"}`}>{r.overdue}</td>
              <td className="px-5 py-3">
                <span className="flex items-center gap-3">
                  <span className="h-[5px] flex-1 rounded-full bg-muted">
                    <span
                      className={`block h-[5px] rounded-full ${r.loadLevel === "high" ? "bg-bad" : "bg-accent"}`}
                      style={{ width: `${Math.min(100, (r.load / 6) * 100)}%` }}
                    />
                  </span>
                  <span className={`w-20 text-right text-xs ${r.loadLevel === "free" ? "text-subtle" : r.loadLevel === "high" ? "font-semibold text-bad" : "font-medium text-ink-2"}`}>
                    {t(level[r.loadLevel])}
                  </span>
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {/* Телефон: список */}
      <ul className="divide-y divide-line border-t border-line lg:hidden">
        {shown.map((r) => (
          <li key={r.userId} className="flex items-center gap-3 px-4 py-3">
            <span className={`flex size-[34px] shrink-0 items-center justify-center rounded-full text-xs font-bold text-white ${toneOf(r.userId)}`}>{initialsOf(r.name)}</span>
            <span className="min-w-0 flex-1">
              <b className="block truncate text-[15px] font-semibold text-ink">{r.name}</b>
              <span className="text-xs text-ink-3">{t(r.role)}</span>
            </span>
            <span
              className={`shrink-0 rounded-md px-2 py-1 text-xs font-semibold ${
                r.load === 0 ? "bg-muted text-subtle" : r.overdue > 0 || r.loadLevel === "high" ? "bg-bad-soft text-bad" : "bg-warn-soft text-warn"
              }`}
            >
              {r.load === 0 ? t("свободен") : t("в работе: {n}", { n: r.load })}
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}

function BlockError({ title, onRetry }: { title: string; onRetry: () => void }) {
  const t = useT();
  return (
    <section className={`${card} flex flex-wrap items-center justify-between gap-3 p-5`}>
      <span className="text-sm text-ink-2">
        <b className="text-ink">{t(title)}</b> — {t("не удалось загрузить")}
      </span>
      <button type="button" onClick={onRetry} className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted">
        <RefreshCw className="size-4" aria-hidden /> {t("Повторить")}
      </button>
    </section>
  );
}

