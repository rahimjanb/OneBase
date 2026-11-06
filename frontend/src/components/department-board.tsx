"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import {
  ArrowUpRight,
  BadgeCheck,
  CalendarDays,
  Check,
  ChevronDown,
  CircleCheck,
  CircleDashed,
  CircleX,
  Factory,
  Info,
  LayoutGrid,
  List,
  Lock,
  Megaphone,
  Package,
  RefreshCw,
  ShoppingBag,
  ShoppingCart,
  TriangleAlert,
  UserCheck,
  Users,
  Wallet,
  Building2,
  type LucideIcon,
} from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { barAriaLabel, cardAriaLabel, type DeptCard, type DeptDetailIcon, type DeptStatus } from "@/lib/departments-view";

export type BoardCard = DeptCard & { href: string };

/** Иконки отделов (ТЗ); неизвестный отдел — здание. */
const icons: Record<string, LucideIcon> = { production: Factory, finance: Wallet, sales: ShoppingCart, marketing: Megaphone, supply: Package, hr: Users };
const detailIcons: Record<DeptDetailIcon, LucideIcon> = { cart: ShoppingCart, attendance: UserCheck, info: Info, lock: Lock };

type Tone = "ok" | "warn" | "bad" | "none";

/** Подложка строки деталей: темнее карточки, в светлой теме — цвет страницы (макет). */
const strip = "bg-black/25 [[data-theme=light]_&]:bg-page";
/** Статус: метка с иконкой (на телефоне — короче), цвет. */
export const statusMeta: Record<DeptStatus, { label: string; short: string; icon: LucideIcon; tone: Tone }> = {
  done: { label: "План выполнен", short: "План выполнен", icon: BadgeCheck, tone: "ok" },
  ok: { label: "В норме", short: "В норме", icon: CircleCheck, tone: "ok" },
  attention: { label: "Требует внимания", short: "Внимание", icon: TriangleAlert, tone: "warn" },
  none: { label: "Нет данных", short: "Нет данных", icon: CircleDashed, tone: "none" },
  error: { label: "Не удалось загрузить", short: "Ошибка", icon: CircleX, tone: "bad" },
};
const tones: Record<Tone, { badge: string; fill: string; soft: string; tile: string; border: string }> = {
  ok: { badge: "bg-ok-soft text-ok", fill: "bg-ok", soft: "bg-ok/30", tile: "bg-muted text-ink", border: "border-line" },
  warn: { badge: "bg-warn-soft text-warn", fill: "bg-warn", soft: "bg-warn/30", tile: "bg-warn-soft text-warn", border: "border-warn/25" },
  bad: { badge: "bg-bad-soft text-bad", fill: "bg-bad", soft: "bg-bad/30", tile: "bg-bad-soft text-bad", border: "border-bad/25" },
  none: { badge: "bg-muted text-ink-3", fill: "bg-ink-3", soft: "bg-ink-3/30", tile: "bg-muted text-ink-3", border: "border-line" },
};

export function DeptIcon({ code, status, size = "size-10" }: { code: string; status: DeptStatus; size?: string }) {
  const Icon = icons[code] ?? Building2;
  return (
    <span className={`grid ${size} shrink-0 place-items-center rounded-xl ${tones[statusMeta[status].tone].tile}`}>
      <Icon className="size-[19px]" aria-hidden />
    </span>
  );
}

/** Метка статуса с иконкой; при наведении — порог отдела («Норма ≥ 80%, сейчас 82%»). */
export function StatusBadge({ card, short = false, small = false }: { card: DeptCard; short?: boolean; small?: boolean }) {
  const t = useT();
  const meta = statusMeta[card.status];
  return (
    <span className="group/badge relative inline-flex">
      <span className={`inline-flex items-center gap-1 whitespace-nowrap rounded-[7px] px-2 py-0.5 font-bold ${small ? "text-[10px]" : "text-[11px]"} ${tones[meta.tone].badge}`}>
        <meta.icon className="size-3" aria-hidden /> {t(short ? meta.short : meta.label)}
      </span>
      {card.reason && (
        <span
          role="tooltip"
          className="pointer-events-none absolute left-0 top-full z-20 mt-1.5 hidden whitespace-nowrap rounded-lg border border-line-strong bg-surface-solid px-2.5 py-1.5 text-xs font-medium text-ink shadow-lg group-hover/badge:block"
        >
          {card.reason}
        </span>
      )}
    </span>
  );
}

/** Полоса цветом статуса; у продаж — два слоя: прогноз бледно, факт ярко. */
export function DeptBar({ card, height = "h-2" }: { card: DeptCard; height?: string }) {
  const t = useT();
  const tone = tones[statusMeta[card.status].tone];
  const bar = card.bar;
  return (
    <div
      role="progressbar"
      aria-label={barAriaLabel(card, t)}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={bar?.value ?? undefined}
      className={`relative ${height} w-full overflow-hidden rounded-full bg-muted`}
    >
      {bar?.forecast != null && <span className={`absolute inset-y-0 left-0 rounded-full ${tone.soft}`} style={{ width: `${bar.forecast}%` }} />}
      {bar && <span className={`absolute inset-y-0 left-0 rounded-full ${tone.fill}`} style={{ width: `${bar.value}%` }} />}
    </div>
  );
}

/** Строка деталей на подложке: у продаж — легенда полосы, у остальных — иконка и текст. */
export function DeptDetail({ card, className = "" }: { card: DeptCard; className?: string }) {
  const tone = tones[statusMeta[card.status].tone];
  if (card.legend) {
    return (
      <div className={`flex flex-wrap items-center gap-x-5 gap-y-1.5 rounded-[11px] ${strip} px-3 py-2.5 text-xs font-medium text-ink-2 ${className}`}>
        <span className="inline-flex items-center gap-2">
          <span className={`h-1.5 w-3 rounded-full ${tone.fill}`} aria-hidden /> {card.legend.fact}
        </span>
        {card.legend.forecast && (
          <span className="inline-flex items-center gap-2">
            <span className={`h-1.5 w-3 rounded-full ${tone.soft}`} aria-hidden /> {card.legend.forecast}
          </span>
        )}
      </div>
    );
  }
  if (!card.detail) return null;
  const Icon = detailIcons[card.detail.icon];
  return (
    <div className={`flex items-center gap-2 rounded-[11px] ${strip} px-3 py-2.5 text-xs font-medium text-ink-2 ${className}`}>
      <Icon className="size-3.5 shrink-0 text-ink-3" aria-hidden />
      <span className="min-w-0">{card.detail.text}</span>
    </div>
  );
}

/** Карточка, которая не загрузилась: метка, «Не удалось загрузить показатели» и «Повторить» (страница перезапрашивает данные). */
function ErrorCard({ card }: { card: BoardCard }) {
  const t = useT();
  const router = useRouter();
  return (
    <div className={`flex h-full flex-col gap-4 rounded-[18px] border bg-surface p-5 ${tones.bad.border}`}>
      <div className="flex items-start gap-3">
        <DeptIcon code={card.code} status={card.status} />
        <div className="min-w-0 flex-1">
          <div className="text-[17px] font-bold text-ink">{card.name}</div>
          <StatusBadge card={card} />
        </div>
        <Link href={card.href} aria-label={t("Открыть отдел «{name}»", { name: card.name })} className="grid size-8 place-items-center rounded-full border border-line-strong text-ink-2 hover:text-ink">
          <ArrowUpRight className="size-4" aria-hidden />
        </Link>
      </div>
      <p className="text-sm text-ink-2">{t("Не удалось загрузить показатели отдела.")}</p>
      <button type="button" onClick={() => router.refresh()} className="mt-auto inline-flex h-10 w-fit items-center gap-2 rounded-xl border border-line-strong bg-surface px-4 text-sm font-semibold text-ink hover:bg-muted">
        <RefreshCw className="size-4" aria-hidden /> {t("Повторить")}
      </button>
    </div>
  );
}

/**
 * Карточка отдела (сетка): целиком — ссылка в отдел. Шапка — иконка, название, метка статуса, стрелка; значение с периодом и название
 * показателя; полоса; строка деталей на подложке. «Требует внимания» — рамка и плитка цветом предупреждения.
 */
export function DepartmentCard({ card }: { card: BoardCard }) {
  const t = useT();
  if (card.status === "error") return <ErrorCard card={card} />;
  const meta = statusMeta[card.status];
  return (
    <Link
      href={card.href}
      aria-label={cardAriaLabel(card, t(meta.label))}
      className={`group flex h-full flex-col gap-4 rounded-[18px] border bg-surface p-5 transition hover:-translate-y-px hover:border-line-strong focus-visible:border-line-strong focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent/40 ${tones[meta.tone].border}`}
    >
      <div className="flex items-start gap-3">
        <DeptIcon code={card.code} status={card.status} />
        <div className="min-w-0 flex-1">
          <div className="truncate text-[17px] font-bold text-ink">{card.name}</div>
          <div className="mt-1">
            <StatusBadge card={card} />
          </div>
        </div>
        <span className="grid size-8 shrink-0 place-items-center rounded-full border border-line-strong text-ink-2 transition-colors group-hover:text-ink" aria-hidden>
          <ArrowUpRight className="size-4" />
        </span>
      </div>
      <div>
        <div className="flex items-baseline gap-2.5">
          <span className="text-[36px] font-bold leading-none tracking-[-1px] tabular-nums text-ink">{card.value == null ? "—" : `${card.value}%`}</span>
          {card.period && card.value != null && <span className="text-xs text-ink-3">{card.period}</span>}
        </div>
        <div className="mt-2 text-[13px] font-medium text-ink-2">{card.label}</div>
      </div>
      <DeptBar card={card} />
      <DeptDetail card={card} className="mt-auto" />
    </Link>
  );
}

/**
 * Карточка на телефоне (макет «Отделы — Mobile»): плитка, название и показатель, справа значение и короткая метка («Внимание»); полоса 6;
 * строка деталей с иконкой (у продаж — «Факт 62,3 из 437,4 т (14%) · прогноз 52%»). Нажатие — в отдел.
 */
function MobileDepartmentCard({ card }: { card: BoardCard }) {
  const t = useT();
  const router = useRouter();
  const meta = statusMeta[card.status];
  const tone = tones[meta.tone];
  const detail = card.legend
    ? { icon: ShoppingBag, text: card.legend.short }
    : card.detail
      ? { icon: detailIcons[card.detail.icon], text: card.detail.text }
      : null;
  const head = (
    <div className="flex items-center gap-3">
      <DeptIcon code={card.code} status={card.status} size="size-[38px]" />
      <div className="min-w-0 flex-1">
        <div className="truncate text-base font-bold text-ink">{card.name}</div>
        <div className="truncate text-xs text-ink-3">{card.status === "error" ? t("Не удалось загрузить") : card.label}</div>
      </div>
      <div className="shrink-0 text-right">
        <div className="text-[22px] font-bold leading-none tabular-nums text-ink">{card.value == null ? "—" : `${card.value}%`}</div>
        <div className="mt-1.5">
          <StatusBadge card={card} short small />
        </div>
      </div>
    </div>
  );
  if (card.status === "error") {
    return (
      <div className={`flex flex-col gap-3 rounded-[20px] border bg-surface p-4 ${tone.border}`}>
        {head}
        <button type="button" onClick={() => router.refresh()} className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-line-strong text-sm font-semibold text-ink active:bg-muted">
          <RefreshCw className="size-4" aria-hidden /> {t("Повторить")}
        </button>
      </div>
    );
  }
  return (
    <Link href={card.href} aria-label={cardAriaLabel(card, t(meta.label))} className={`flex flex-col gap-3 rounded-[20px] border bg-surface p-4 active:bg-muted/40 ${tone.border}`}>
      {head}
      <DeptBar card={card} height="h-1.5" />
      {detail && (
        <div className="flex items-center gap-2 text-xs font-medium text-ink-2">
          <detail.icon className="size-3.5 shrink-0 text-ink-3" aria-hidden />
          <span className="min-w-0">{detail.text}</span>
        </div>
      )}
    </Link>
  );
}

/** Строка списка: иконка · отдел · показатель · значение · полоса · статус · стрелка. */
function DepartmentRow({ card }: { card: BoardCard }) {
  const t = useT();
  const meta = statusMeta[card.status];
  const body = (
    <>
      <DeptIcon code={card.code} status={card.status} />
      <span className="min-w-0">
        <span className="block truncate text-[15px] font-bold text-ink">{card.name}</span>
        {card.detail && <span className="block truncate text-xs text-ink-3">{card.detail.text}</span>}
        {card.legend && <span className="block truncate text-xs text-ink-3">{card.legend.fact}</span>}
      </span>
      <span className="min-w-0 truncate text-sm text-ink-2">
        {card.label}
        {card.period && card.value != null && <span className="text-ink-3"> · {card.period}</span>}
      </span>
      <span className="text-right text-xl font-bold tabular-nums text-ink">{card.value == null ? "—" : `${card.value}%`}</span>
      <DeptBar card={card} />
      <span>
        <StatusBadge card={card} />
      </span>
      <span className="grid size-8 place-items-center rounded-full border border-line-strong text-ink-2 group-hover:text-ink" aria-hidden>
        <ArrowUpRight className="size-4" />
      </span>
    </>
  );
  const row = "group grid grid-cols-[auto_minmax(0,1.1fr)_minmax(0,1.3fr)_64px_200px_170px_auto] items-center gap-4 px-5 py-4";
  if (card.status === "error") return <ErrorRow card={card} className={row} />;
  return (
    <Link href={card.href} aria-label={cardAriaLabel(card, t(meta.label))} className={`${row} transition-colors hover:bg-muted/60 focus-visible:bg-muted/60 focus-visible:outline-none`}>
      {body}
    </Link>
  );
}

function ErrorRow({ card, className }: { card: BoardCard; className: string }) {
  const t = useT();
  const router = useRouter();
  return (
    <div className={className}>
      <DeptIcon code={card.code} status={card.status} />
      <span className="truncate text-[15px] font-bold text-ink">{card.name}</span>
      <span className="text-sm text-ink-2">{t("Не удалось загрузить показатели отдела.")}</span>
      <span />
      <button type="button" onClick={() => router.refresh()} className="inline-flex h-9 w-fit items-center gap-2 rounded-lg border border-line-strong px-3 text-sm font-semibold text-ink hover:bg-muted">
        <RefreshCw className="size-4" aria-hidden /> {t("Повторить")}
      </button>
      <span>
        <StatusBadge card={card} />
      </span>
      <Link href={card.href} aria-label={t("Открыть отдел «{name}»", { name: card.name })} className="grid size-8 place-items-center rounded-full border border-line-strong text-ink-2 hover:text-ink">
        <ArrowUpRight className="size-4" aria-hidden />
      </Link>
    </div>
  );
}

/** Выбор месяца (все карточки пересчитываются, месяц — в адресе) и вида «сетка / список». */
export function DepartmentsControls({ month, months, view }: { month: string; months: { key: string; label: string }[]; view: "grid" | "list" }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const apply = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    router.push(`${pathname}${next.size ? `?${next}` : ""}`, { scroll: false });
  };
  const toggle = "grid size-9 place-items-center rounded-lg";
  return (
    <div className="hidden items-center gap-2.5 md:flex">
      <label className="relative inline-flex items-center">
        <span className="sr-only">{t("Месяц")}</span>
        <CalendarDays className="pointer-events-none absolute left-3.5 size-4 text-ink-2" aria-hidden />
        <select
          value={month}
          onChange={(e) => apply({ month: e.target.value === months[0]?.key ? null : e.target.value })}
          className="h-11 cursor-pointer appearance-none rounded-xl border border-line-strong bg-surface pl-10 pr-10 text-sm font-semibold text-ink hover:bg-muted focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
        >
          {months.map((m) => (
            <option key={m.key} value={m.key}>
              {m.label}
            </option>
          ))}
        </select>
        <ChevronDown className="pointer-events-none absolute right-3.5 size-4 text-ink-3" aria-hidden />
      </label>
      <div className="flex h-11 items-center gap-1 rounded-xl border border-line-strong bg-surface p-1" role="group" aria-label={t("Вид")}>
        <button type="button" onClick={() => apply({ view: null })} aria-pressed={view === "grid"} title={t("Сеткой")} className={`${toggle} ${view === "grid" ? "bg-muted text-ink" : "text-ink-3 hover:text-ink"}`}>
          <LayoutGrid className="size-4" aria-hidden />
          <span className="sr-only">{t("Сеткой")}</span>
        </button>
        <button type="button" onClick={() => apply({ view: "list" })} aria-pressed={view === "list"} title={t("Списком")} className={`${toggle} ${view === "list" ? "bg-muted text-ink" : "text-ink-3 hover:text-ink"}`}>
          <List className="size-4" aria-hidden />
          <span className="sr-only">{t("Списком")}</span>
        </button>
      </div>
    </div>
  );
}

/**
 * Подзаголовок «Показатели, планы и состояние подразделений · ноябрь 2026»; на телефоне короче и нажимается — шторка с месяцами (ТЗ: период —
 * по тапу).
 */
export function DepartmentsSubtitle({ month, monthText, months }: { month: string; monthText: string; months: { key: string; label: string }[] }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [open, setOpen] = useState(false);
  const choose = (key: string) => {
    const next = new URLSearchParams(params.toString());
    if (key === months[0]?.key) next.delete("month");
    else next.set("month", key);
    setOpen(false);
    router.push(`${pathname}${next.size ? `?${next}` : ""}`, { scroll: false });
  };
  return (
    <>
      <span className="max-md:hidden">
        {t("Показатели, планы и состояние подразделений")} · {monthText}
      </span>
      <button type="button" onClick={() => setOpen(true)} className="inline-flex min-h-[44px] items-center gap-1 text-left md:hidden" aria-haspopup="dialog">
        {t("Показатели, планы и состояние")} · <span className="font-semibold text-ink">{monthText}</span>
        <ChevronDown className="size-4 shrink-0 text-ink-3" aria-hidden />
      </button>
      <Sheet open={open} onClose={() => setOpen(false)} title={t("Период")}>
        <div role="radiogroup" aria-label={t("Месяц")} className="space-y-0.5 pb-2">
          {months.map((m) => (
            <button
              key={m.key}
              type="button"
              role="radio"
              aria-checked={m.key === month}
              onClick={() => choose(m.key)}
              className="flex h-12 w-full items-center gap-3 rounded-xl px-3 text-left text-[15px] text-ink active:bg-muted"
            >
              <span className="grid size-5 shrink-0 place-items-center">{m.key === month && <Check className="size-5 text-accent" aria-hidden />}</span>
              {m.label}
            </button>
          ))}
        </div>
      </Sheet>
    </>
  );
}

/**
 * «Отделы»: одна сетка в прежнем порядке (3 колонки, уже 1200 — 2) или список; на телефоне — компактные карточки (макет «Отделы —
 * Mobile»). Под сеткой — как считается статус.
 */
export function DepartmentsBoard({ cards, view }: { cards: BoardCard[]; view: "grid" | "list" }) {
  const t = useT();
  return (
    <>
      <div className="flex flex-col gap-3.5 md:hidden">
        {cards.map((c) => (
          <MobileDepartmentCard key={c.code} card={c} />
        ))}
      </div>
      {view === "grid" && (
        <div className="hidden gap-4 md:grid md:grid-cols-2 min-[75rem]:grid-cols-3">
          {cards.map((c) => (
            <DepartmentCard key={c.code} card={c} />
          ))}
        </div>
      )}
      {view === "list" && (
        <div className="hidden overflow-x-auto rounded-2xl border border-line bg-surface md:block">
          <div className="min-w-[980px] divide-y divide-line">
            {cards.map((c) => (
              <DepartmentRow key={c.code} card={c} />
            ))}
          </div>
        </div>
      )}
      <p className="mt-5 hidden items-start gap-2 text-xs text-ink-3 md:flex">
        <Info className="mt-px size-3.5 shrink-0" aria-hidden />
        {t("Статус считается по порогам отдела: ниже цели — «Требует внимания», цель достигнута — «В норме», план уже выполнен — «План выполнен». Наведите на метку, чтобы увидеть порог; нажмите на карточку, чтобы открыть отдел.")}
      </p>
    </>
  );
}

/** Скелетон страницы: шапка и 6 карточек. */
export function DepartmentsSkeleton() {
  const block = "animate-pulse rounded-lg bg-muted";
  return (
    <div className="grid gap-4 md:grid-cols-2 min-[75rem]:grid-cols-3" aria-busy="true">
      {Array.from({ length: 6 }, (_, i) => (
        <div key={i} className="flex h-[132px] flex-col gap-4 rounded-[18px] border border-line bg-surface p-5 md:h-[248px]">
          <div className="flex items-center gap-3">
            <span className={`size-10 ${block} rounded-xl`} />
            <span className="flex-1 space-y-2">
              <span className={`block h-4 w-32 ${block}`} />
              <span className={`block h-4 w-24 ${block}`} />
            </span>
          </div>
          <span className={`h-9 w-24 ${block}`} />
          <span className={`h-3.5 w-40 ${block}`} />
          <span className={`h-2 w-full ${block} rounded-full`} />
        </div>
      ))}
    </div>
  );
}
