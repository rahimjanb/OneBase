"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import {
  BadgeCheck,
  CalendarClock,
  CalendarDays,
  Check,
  ChevronDown,
  Crown,
  Database,
  Info,
  Minus,
  Plus,
  SlidersHorizontal,
  Target,
  TrendingDown,
  TrendingUp,
  TriangleAlert,
  type LucideIcon,
} from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { num } from "@/lib/analytik";
import {
  SEGMENT_TONES,
  WAPE_GOOD,
  WHAT_IF,
  accuracyTone,
  amount,
  factorText,
  localizeUnits,
  methodName,
  methodTone,
  pct,
  wapeLevel,
  type BacktestView,
  type CalendarEffect,
  type Scenario,
  type ScenarioCard,
} from "@/lib/forecasts";

// Детали экрана «Аналитик → Прогнозы» по макету (UVDBc, CBnGo). Цвета — токены палитры страницы (data-palette="sales-secondary"):
// surface-2 → muted, subtle → ink-4, link → accent-strong.

export const card = "rounded-[18px] border border-line bg-surface max-md:rounded-[14px]";

const toneText = { ok: "text-ok", warn: "text-warn", bad: "text-bad", muted: "text-ink-3" } as const;
const toneBg = { ok: "bg-ok", warn: "bg-warn", bad: "bg-bad", muted: "bg-ink-4" } as const;
const toneSoft = { ok: "bg-ok/15 text-ok", warn: "bg-warn/15 text-warn", bad: "bg-bad/15 text-bad", muted: "bg-muted text-ink-3" } as const;

/** Подпись поля панели управления: 11/600, верхний регистр (компьютер). */
export function FieldLabel({ children }: { children: React.ReactNode }) {
  return <span className="mb-1.5 block text-[11px] font-semibold uppercase tracking-[0.04em] text-ink-4">{children}</span>;
}

/** Сегменты (radiogroup): компьютер — контейнер page, телефон — во всю ширину равными долями. */
export function Segments<K extends string | number>({
  label,
  items,
  value,
  onChange,
  full = false,
  size = "md",
}: {
  label: string;
  items: { key: K; label: string; icon?: LucideIcon; aria?: string }[];
  value: K;
  onChange: (k: K) => void;
  full?: boolean;
  size?: "md" | "sm";
}) {
  const ref = useRef<HTMLDivElement>(null);
  const move = (dir: 1 | -1) => {
    const i = items.findIndex((x) => x.key === value);
    const next = items[(i + dir + items.length) % items.length];
    onChange(next.key);
    requestAnimationFrame(() => ref.current?.querySelector<HTMLButtonElement>('[aria-checked="true"]')?.focus());
  };
  return (
    <div
      ref={ref}
      role="radiogroup"
      aria-label={label}
      onKeyDown={(e) => {
        if (e.key === "ArrowRight" || e.key === "ArrowDown") (e.preventDefault(), move(1));
        if (e.key === "ArrowLeft" || e.key === "ArrowUp") (e.preventDefault(), move(-1));
      }}
      className={`flex gap-0.5 rounded-xl border border-line p-[3px] ${full ? "w-full bg-surface" : "w-fit bg-page"}`}
    >
      {items.map((it) => {
        const on = it.key === value;
        const Icon = it.icon;
        return (
          <button
            key={String(it.key)}
            type="button"
            role="radio"
            aria-checked={on}
            aria-label={it.aria}
            tabIndex={on ? 0 : -1}
            onClick={() => onChange(it.key)}
            className={`inline-flex items-center justify-center gap-1.5 whitespace-nowrap rounded-[9px] transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
              full ? "h-[34px] min-w-0 flex-1 px-1 text-[12px]" : size === "sm" ? "h-[30px] px-3 text-[13px]" : "h-[30px] px-2.5 text-[13px]"
            } ${on ? "bg-accent font-semibold text-white" : "font-medium text-ink-2 hover:text-ink"}`}
          >
            {Icon && <Icon aria-hidden className={`size-[14px] shrink-0 max-md:size-[13px] ${full ? "max-[399px]:hidden" : ""} ${on ? "" : "text-ink-3"}`} />}
            <span className="truncate">{it.label}</span>
          </button>
        );
      })}
    </div>
  );
}

/** Выбор из списка: на компьютере — меню под кнопкой, на телефоне — шторка снизу с переключателями. */
export function Picker<K extends string>({
  label,
  icon: Icon,
  items,
  value,
  onChange,
  onOpen,
  className = "",
  compact = false,
}: {
  label: string;
  icon: LucideIcon;
  items: { key: K; label: string; hint?: string }[];
  value: K;
  onChange: (k: K) => void;
  onOpen?: () => void;
  className?: string;
  /** Телефон: поле 36, текст 12, шторка вместо меню. */
  compact?: boolean;
}) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const current = items.find((x) => x.key === value) ?? items[0];
  useEffect(() => {
    if (!open || compact) return;
    const close = (e: MouseEvent) => !ref.current?.contains(e.target as Node) && setOpen(false);
    const esc = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", esc);
    return () => (document.removeEventListener("mousedown", close), document.removeEventListener("keydown", esc));
  }, [open, compact]);
  const choose = (k: K) => (onChange(k), setOpen(false));
  return (
    <div ref={ref} className={`relative ${className}`}>
      <button
        type="button"
        aria-haspopup={compact ? "dialog" : "listbox"}
        aria-expanded={open}
        aria-label={`${label}: ${current?.label ?? ""}`}
        onClick={() => (onOpen?.(), setOpen((o) => !o))}
        className={`flex w-full items-center gap-1.5 rounded-[10px] border border-line bg-surface text-left text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
          compact ? "h-9 px-2.5 text-[12px] font-semibold" : "h-[34px] min-w-[150px] px-3 text-[13px] font-semibold"
        }`}
      >
        <Icon aria-hidden className="size-[14px] shrink-0 text-ink-3 max-md:size-[13px]" />
        <span className="min-w-0 flex-1 truncate">{current?.label}</span>
        <ChevronDown aria-hidden className="size-[14px] shrink-0 text-ink-4 max-md:size-[13px]" />
      </button>
      {open && !compact && (
        <ul role="listbox" aria-label={label} className="absolute left-0 z-30 mt-1.5 max-h-72 min-w-full overflow-y-auto rounded-xl border border-line bg-surface-solid p-1.5 shadow-lg">
          {items.map((it) => (
            <li key={it.key} role="option" aria-selected={it.key === value}>
              <button type="button" onClick={() => choose(it.key)} className="flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-[13px] text-ink hover:bg-muted">
                <span className="min-w-0 flex-1">
                  <span className="block truncate">{it.label}</span>
                  {it.hint && <span className="block truncate text-[11px] text-ink-4">{it.hint}</span>}
                </span>
                {it.key === value && <Check aria-hidden className="size-4 text-accent" />}
              </button>
            </li>
          ))}
        </ul>
      )}
      {compact && (
        <Sheet open={open} onClose={() => setOpen(false)} title={label}>
          <div role="radiogroup" aria-label={label} className="flex flex-col gap-1 pb-2">
            {items.map((it) => (
              <button
                key={it.key}
                type="button"
                role="radio"
                aria-checked={it.key === value}
                onClick={() => choose(it.key)}
                className={`flex min-h-12 items-center gap-3 rounded-xl px-3 text-left ${it.key === value ? "bg-accent/15" : "hover:bg-muted"}`}
              >
                <span className={`grid size-5 shrink-0 place-items-center rounded-full border-2 ${it.key === value ? "border-accent" : "border-line-strong"}`} aria-hidden>
                  {it.key === value && <span className="size-2.5 rounded-full bg-accent" />}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block text-[14px] font-medium text-ink">{it.label}</span>
                  {it.hint && <span className="block text-[12px] text-ink-3">{it.hint}</span>}
                </span>
              </button>
            ))}
          </div>
          <p className="sr-only">{t("Выберите вариант")}</p>
        </Sheet>
      )}
    </div>
  );
}

/** «Что если»: степпер (−50…+50 %, шаг 1, долгое нажатие повторяет) и быстрые −10 / +10 / +20. */
export function WhatIf({ value, onChange, compact = false }: { value: number; onChange: (v: number) => void; compact?: boolean }) {
  const t = useT();
  const timer = useRef<number | null>(null);
  const current = useRef(value);
  current.current = value;
  const clamp = (v: number) => Math.max(WHAT_IF.min, Math.min(WHAT_IF.max, v));
  const stop = () => {
    if (timer.current != null) window.clearInterval(timer.current), window.clearTimeout(timer.current);
    timer.current = null;
  };
  useEffect(() => stop, []);
  const press = (dir: 1 | -1) => {
    onChange(clamp(current.current + dir));
    stop();
    timer.current = window.setTimeout(() => {
      timer.current = window.setInterval(() => onChange((current.current = clamp(current.current + dir))), 90);
    }, 400);
  };
  const btn = `grid shrink-0 place-items-center text-ink-2 hover:text-ink disabled:opacity-30 ${compact ? "h-9 w-[34px]" : "h-[34px] w-9"}`;
  return (
    <div className={compact ? "flex flex-col gap-2" : "flex items-center gap-2"}>
      <div
        role="spinbutton"
        aria-label={t("Сценарий «что если», %")}
        aria-valuenow={value}
        aria-valuemin={WHAT_IF.min}
        aria-valuemax={WHAT_IF.max}
        aria-valuetext={`${value > 0 ? "+" : ""}${value}%`}
        tabIndex={0}
        onKeyDown={(e) => {
          if (e.key === "ArrowUp" || e.key === "ArrowRight") (e.preventDefault(), onChange(clamp(value + 1)));
          if (e.key === "ArrowDown" || e.key === "ArrowLeft") (e.preventDefault(), onChange(clamp(value - 1)));
          if (e.key === "Home") onChange(WHAT_IF.min);
          if (e.key === "End") onChange(WHAT_IF.max);
          if (e.key === "Delete" || e.key === "Backspace" || e.key === "0") onChange(0);
        }}
        className={`flex items-center rounded-[10px] border border-line focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${compact ? "w-full bg-page" : "bg-page"}`}
      >
        <button type="button" tabIndex={-1} aria-label={t("Меньше")} disabled={value <= WHAT_IF.min} onPointerDown={() => press(-1)} onPointerUp={stop} onPointerLeave={stop} className={btn}>
          <Minus aria-hidden className="size-4" />
        </button>
        <span className={`min-w-[52px] flex-1 text-center font-bold tabular-nums ${compact ? "text-[15px]" : "text-[14px]"} ${value ? "text-accent-strong" : "text-ink"}`}>
          {value > 0 ? "+" : value < 0 ? "−" : ""}
          {Math.abs(value)}
          <span className="ml-0.5 text-[11px] font-medium text-ink-3">%</span>
        </span>
        <button type="button" tabIndex={-1} aria-label={t("Больше")} disabled={value >= WHAT_IF.max} onPointerDown={() => press(1)} onPointerUp={stop} onPointerLeave={stop} className={btn}>
          <Plus aria-hidden className="size-4" />
        </button>
      </div>
      <div className={`flex gap-1.5 ${compact ? "w-full" : ""}`}>
        {WHAT_IF.quick.map((q) => (
          <button
            key={q}
            type="button"
            aria-pressed={value === q}
            onClick={() => onChange(value === q ? 0 : q)}
            className={`rounded-[9px] font-semibold tabular-nums transition-colors ${compact ? "h-6 flex-1 text-[11px]" : "h-[34px] px-3 text-[13px] border border-line"} ${
              value === q ? "bg-accent/15 text-accent-strong" : "bg-muted text-ink-2 hover:text-ink"
            }`}
          >
            {q > 0 ? "+" : "−"}
            {Math.abs(q)}
            {compact ? "" : "%"}
          </button>
        ))}
      </div>
    </div>
  );
}

const scenarioMeta: Record<Scenario, { icon: LucideIcon; title: string; short: string; tag: string; iconTone: string }> = {
  pessimistic: { icon: TrendingDown, title: "Пессимистичный", short: "Пессим.", tag: "нижняя граница · P10", iconTone: "bg-bad/15 text-bad" },
  realistic: { icon: Target, title: "Реалистичный", short: "Реалист.", tag: "медианный прогноз · P50", iconTone: "bg-accent/15 text-accent-strong" },
  optimistic: { icon: TrendingUp, title: "Оптимистичный", short: "Оптим.", tag: "верхняя граница · P90", iconTone: "bg-ok/15 text-ok" },
};

/** Карточки сценариев (radiogroup): выбранная — линия на графике. */
export function ScenarioCards({
  cards,
  value,
  onChange,
  unit,
  prevText,
  compact = false,
  loading = false,
}: {
  cards: ScenarioCard[];
  value: Scenario;
  onChange: (s: Scenario) => void;
  unit: string;
  prevText: string;
  compact?: boolean;
  loading?: boolean;
}) {
  const t = useT();
  return (
    <div role="radiogroup" aria-label={t("Сценарий на графике")} className={`grid grid-cols-3 ${compact ? "gap-1.5" : "gap-3"}`}>
      {cards.map((c) => {
        const m = scenarioMeta[c.key];
        const on = c.key === value;
        const raw = amount(c.value, unit);
        const a = { head: raw.head, tail: localizeUnits(raw.tail, t) };
        const deltaTone = c.delta == null ? "text-ink-4" : c.delta > 0.005 ? "text-ok" : c.delta < -0.005 ? "text-bad" : "text-ink-3";
        const aria = `${t(`${m.title} сценарий`)}, ${a.head} ${a.tail}${c.delta != null ? `, ${pct(c.delta)} ${t("к прошлым {period}", { period: prevText })}` : ""}`;
        return (
          <button
            key={c.key}
            type="button"
            role="radio"
            aria-checked={on}
            aria-label={aria}
            onClick={() => onChange(c.key)}
            className={`@container relative min-w-0 border text-left transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
              compact ? "rounded-[14px] p-2.5" : "rounded-2xl px-[18px] py-4"
            } ${on ? "border-accent bg-[linear-gradient(180deg,rgb(61_123_255/0.12),rgb(61_123_255/0.02))] [box-shadow:inset_0_0_0_0.5px_var(--color-accent)]" : "border-line bg-surface hover:border-line-strong"} ${
              loading ? "opacity-50" : ""
            }`}
          >
            {compact ? (
              <span className={`block text-[11px] font-semibold ${on ? "text-accent-strong" : "text-ink-3"}`}>{t(m.short)}</span>
            ) : (
              <span className="flex items-center gap-2.5">
                <span className={`grid size-7 shrink-0 place-items-center rounded-lg ${m.iconTone}`} aria-hidden>
                  <m.icon className="size-4" />
                </span>
                <span className="min-w-0 flex-1 truncate text-[13px] font-semibold text-ink-2">{t(m.title)}</span>
                {on ? (
                  <span className="inline-flex shrink-0 items-center gap-1 rounded-md bg-accent px-1.5 py-0.5 text-[10px] font-bold text-white">
                    <Check aria-hidden className="size-3" />
                    <span className="hidden @[330px]:inline">{t("на графике")}</span>
                  </span>
                ) : (
                  <span aria-hidden className="size-4 shrink-0 rounded-full border-2 border-line-strong" />
                )}
              </span>
            )}
            <span className={`flex items-baseline gap-1.5 ${compact ? "mt-1" : "mt-3"}`}>
              <span className={`font-bold tabular-nums text-ink ${compact ? "text-[19px] tracking-[-0.3px]" : "text-[28px] leading-none tracking-[-0.4px]"}`}>{a.head}</span>
              <span className={`truncate text-ink-3 ${compact ? "text-[10px]" : "text-[13px]"}`}>{compact ? a.tail.split(" ")[0] : a.tail}</span>
            </span>
            {/* Узкая карточка (компьютер с боковым меню): изменение и граница — двумя строками. */}
            <span className={`flex gap-x-2 gap-y-0.5 ${compact ? "mt-1 items-baseline justify-between" : "mt-3 flex-col @[380px]:flex-row @[380px]:items-baseline @[380px]:justify-between"}`}>
              <span className={`font-semibold tabular-nums ${deltaTone} ${compact ? "text-[10px]" : "text-[12px]"}`}>
                {c.delta == null ? "—" : `${pct(c.delta)} ${compact ? t("к пред.") : t("к прошлым {period}", { period: prevText })}`}
              </span>
              {!compact && <span className="text-[11px] text-ink-4">{t(m.tag)}</span>}
            </span>
          </button>
        );
      })}
    </div>
  );
}

/** Шкала ошибки: 4 деления, заполнено по уровню; цвет делений — по месту (2 зелёных, жёлтое, красное). */
function WapeScale({ wape, height = 5 }: { wape: number | null; height?: number }) {
  const level = wapeLevel(wape);
  return (
    <span className="flex gap-[3px]" aria-hidden>
      {SEGMENT_TONES.map((tone, i) => (
        <span key={i} className={`flex-1 rounded-full ${level && i < level.filled ? toneBg[tone] : "bg-muted"}`} style={{ height }} />
      ))}
    </span>
  );
}

/** «Точность на истории» (компьютер): WAPE, шкала, уровень текстом. */
export function AccuracyCard({ wape, holdout, onInfo }: { wape: number | null; holdout: number; onInfo?: () => void }) {
  const t = useT();
  const level = wapeLevel(wape);
  return (
    <div className={`${card} flex flex-col gap-3 p-4`}>
      <div className="flex items-center gap-2.5">
        <span className="grid size-7 shrink-0 place-items-center rounded-lg bg-ok/15 text-ok" aria-hidden>
          <BadgeCheck className="size-4" />
        </span>
        <span className="flex-1 text-[13px] font-semibold text-ink-2">{t("Точность на истории")}</span>
        <span className="group relative">
          <button type="button" onClick={onInfo} aria-label={t("Что такое WAPE")} className="grid size-6 place-items-center rounded-full text-ink-4 hover:text-ink-2">
            <Info className="size-4" aria-hidden />
          </button>
          <span role="tooltip" className="pointer-events-none absolute right-0 top-7 z-20 hidden w-64 rounded-xl border border-line-strong bg-muted p-3 text-[11.5px] leading-snug text-ink-2 shadow-lg group-hover:block group-focus-within:block">
            {t("WAPE — средняя ошибка прогноза: сумма отклонений от факта, делённая на сумму факта, на последних {n} точках истории, которые метод не видел.", { n: holdout })}
          </span>
        </span>
      </div>
      <p className="flex items-baseline gap-1.5">
        <span className="text-[28px] font-bold leading-none tabular-nums text-ink">{wape == null ? "—" : num(wape * 100, 0)}</span>
        <span className="text-[13px] text-ink-3">{t("% ошибка (WAPE)")}</span>
      </p>
      <WapeScale wape={wape} />
      <p className="flex items-baseline justify-between gap-2">
        <span className={`text-[12px] font-semibold ${level ? toneText[level.tone] : "text-ink-3"}`}>{level ? t(`Прогноз ${level.label}`) : t("Истории мало — ошибку не проверить")}</span>
        <span className="text-[11px] text-ink-4">{t("< {n}% — хорошо", { n: WAPE_GOOD * 100 })}</span>
      </p>
    </div>
  );
}

/** Плитка ошибки (телефон, планшет): нажатие — шторка с объяснением и методами. */
export function AccuracyTile({ wape, onOpen }: { wape: number | null; onOpen: () => void }) {
  const t = useT();
  const level = wapeLevel(wape);
  return (
    <button type="button" onClick={onOpen} className={`${card} flex flex-col gap-2 p-3 text-left`}>
      <span className="flex items-center gap-1.5 text-[11px] text-ink-3">
        <BadgeCheck aria-hidden className="size-[13px] text-ok" />
        {t("Ошибка (WAPE)")}
      </span>
      <span className="text-[20px] font-bold leading-none tabular-nums text-ink">{wape == null ? "—" : `${num(wape * 100, 0)}%`}</span>
      <WapeScale wape={wape} height={4} />
      <span className={`text-[11px] font-semibold ${level ? toneText[level.tone] : "text-ink-3"}`}>{level ? t(level.label) : t("мало истории")}</span>
    </button>
  );
}

/** Проверенные методы: лучший — зелёной строкой с короной; полоса — ошибка на шкале до 40 %. */
export function Methods({
  methods,
  chosen,
  holdout,
  limit,
  compact = false,
}: {
  methods: { method: string; wape: number | null }[];
  chosen: string;
  holdout: number;
  /** Телефон: сколько показать (остальные — «Все методы»). */
  limit?: number;
  compact?: boolean;
}) {
  const t = useT();
  const [all, setAll] = useState(false);
  const sorted = [...methods].sort((a, b) => (a.method === chosen ? -1 : b.method === chosen ? 1 : (a.wape ?? 9) - (b.wape ?? 9)));
  const shown = limit && !all ? sorted.slice(0, limit) : sorted;
  return (
    <div className="flex flex-col gap-1.5">
      {!compact && (
        <p className="mb-1 text-[11px] text-ink-4">{t("ошибка на отложенной части истории · {n} точек · меньше — лучше", { n: holdout })}</p>
      )}
      {shown.length === 0 && <p className="text-[12px] text-ink-3">{t("Истории мало — методы на отложенной части не проверялись.")}</p>}
      {shown.map((m) => {
        const best = m.method === chosen;
        const tone = methodTone(m.wape, best);
        return (
          <div
            key={m.method}
            className={`flex items-center gap-3 rounded-[10px] ${compact ? "px-2 py-[7px]" : "px-3 py-2.5"} ${best ? "border border-ok/20 bg-ok/10" : "border border-transparent"}`}
          >
            {best && <Crown aria-label={t("лучший метод")} className="size-3.5 shrink-0 text-ok" />}
            <span className={`min-w-0 truncate ${compact ? "flex-1 text-[12px]" : "w-[170px] shrink-0 text-[13px]"} ${best ? "font-bold text-ink" : "text-ink-2"}`}>
              {t(methodName(m.method))}
            </span>
            {!compact && (
              <span className="relative h-1.5 min-w-0 flex-1 rounded-full bg-muted" aria-hidden>
                <span className={`absolute inset-y-0 left-0 rounded-full ${toneBg[tone]}`} style={{ width: `${Math.min(100, ((m.wape ?? 0) / 0.4) * 100)}%` }} />
              </span>
            )}
            <span className={`shrink-0 text-right text-[12px] font-bold tabular-nums ${compact ? toneText[tone] : best ? "text-ok" : "text-ink"} ${compact ? "" : "w-12"}`}>
              {m.wape == null ? "—" : `${num(m.wape * 100, 1)}%`}
            </span>
          </div>
        );
      })}
      {limit && sorted.length > limit && (
        <button type="button" onClick={() => setAll((a) => !a)} className="self-start px-2 py-1 text-[12px] font-semibold text-accent-strong">
          {all ? t("Свернуть") : `${t("Все методы")} ›`}
        </button>
      )}
    </div>
  );
}

const monthName = (iso: string, t: (s: string) => string) =>
  t(["Январь", "Февраль", "Март", "Апрель", "Май", "Июнь", "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"][Number(iso.slice(5, 7)) - 1]);

/** Точность прошлых прогнозов: месяц, «точность N%», прогноз и факт; источник — проверка на истории или сохранённый прогноз. */
export function PastAccuracy({ data, unit, compact = false, loading = false }: { data: BacktestView | null; unit: string; compact?: boolean; loading?: boolean }) {
  const t = useT();
  if (loading && !data) return <div className="h-40 animate-pulse rounded-xl bg-muted" aria-busy="true" />;
  if (!data || data.months.length === 0)
    return <p className="text-[12px] leading-snug text-ink-3">{t("Прошлых месяцев для проверки пока нет: нужна история хотя бы за две недели до начала месяца.")}</p>;
  const months = compact ? data.months.slice(-3) : data.months;
  return (
    <div className="flex flex-col gap-2">
      {months.map((m) => {
        const tone = accuracyTone(m.accuracy);
        const f = { ...amount(m.forecast, unit), tail: localizeUnits(amount(m.forecast, unit).tail, t) };
        const a = { ...amount(m.fact, unit), tail: localizeUnits(amount(m.fact, unit).tail, t) };
        return compact ? (
          <div key={m.month} className="flex items-center justify-between gap-3 rounded-[10px] bg-muted px-3 py-2">
            <span className="text-[12px] font-semibold text-ink">{monthName(m.month, t)}</span>
            <span className="text-[11px] text-ink-3">
              {f.head} / {a.head} {a.tail}
            </span>
            <span className={`rounded-md px-1.5 py-0.5 text-[11px] font-bold tabular-nums ${toneSoft[tone]}`}>{m.accuracy == null ? "—" : `${num(m.accuracy * 100, 0)}%`}</span>
          </div>
        ) : (
          <div key={m.month} className="rounded-xl bg-muted px-4 py-3">
            <div className="flex items-center justify-between gap-2">
              <span className="text-[12.5px] font-semibold text-ink">
                {monthName(m.month, t)}
                {m.saved && <span className="ml-1.5 text-[10.5px] font-medium text-ink-4">· {t("сохранённый прогноз")}</span>}
              </span>
              <span className={`rounded-md px-2 py-0.5 text-[11px] font-bold ${toneSoft[tone]}`}>
                {t("точность {n}%", { n: m.accuracy == null ? "—" : num(m.accuracy * 100, 0) })}
              </span>
            </div>
            <div className="mt-1.5 flex flex-wrap gap-x-4 gap-y-1 text-[12px] text-ink-2">
              <span className="inline-flex items-center gap-1.5">
                <span aria-hidden className="size-2 rounded-sm bg-violet" />
                {t("прогноз {v}", { v: `${f.head} ${f.tail}` })}
              </span>
              <span className="inline-flex items-center gap-1.5">
                <span aria-hidden className="size-2 rounded-sm bg-accent" />
                {t("факт {v}", { v: `${a.head} ${a.tail}` })}
              </span>
            </div>
          </div>
        );
      })}
      <p className="text-[10.5px] leading-snug text-ink-4">
        {data.months.some((m) => !m.saved)
          ? t("Проверка на истории: прогноз на 1-е число месяца по данным до этого дня, тем же методом выбора.")
          : t("Сохранённые прогнозы, сверенные с фактом.")}
      </p>
    </div>
  );
}

/** Допущения: история, сезонность, что не учтено, переход в Симулятор. */
export function Assumptions({
  days,
  from,
  to,
  stepWord,
  seasonality,
  calendar = [],
  simulatorHref,
  compact = false,
}: {
  days: number;
  from: string | null;
  to: string | null;
  stepWord: string;
  seasonality: number;
  /** Учтённый край месяца (прогноз по дням). */
  calendar?: CalendarEffect[];
  simulatorHref: string;
  compact?: boolean;
}) {
  const t = useT();
  const d = (iso: string | null) => (iso ? (compact ? `${iso.slice(8, 10)}.${iso.slice(5, 7)}` : `${iso.slice(8, 10)}.${iso.slice(5, 7)}.${iso.slice(0, 4)}`) : "—");
  const season = seasonality === 7 ? t("по дню недели") : seasonality === 12 ? t("по месяцу года") : null;
  const rows: { icon: LucideIcon; tone: string; title: string; text?: string }[] = [
    { icon: Database, tone: "bg-ok/15 text-ok", title: t("История {n} {unit}", { n: days, unit: stepWord }), text: `${d(from)} — ${d(to)}` },
    season
      ? { icon: CalendarDays, tone: "bg-ok/15 text-ok", title: t("Учтена сезонность"), text: season }
      : { icon: CalendarDays, tone: "bg-warn/15 text-warn", title: t("Сезонность не учтена"), text: t("для неё мало истории или шаг без сезонности") },
    ...(calendar.length
      ? [
          {
            icon: CalendarClock,
            tone: "bg-ok/15 text-ok",
            title: t("Учтён край месяца"),
            text: [
              ...calendar.map((c) => (c.kind === "last" ? t("последний день {f}", { f: factorText(c.factor) }) : t("1-е число {f}", { f: factorText(c.factor) }))),
              t("по {n} мес.", { n: Math.max(...calendar.map((c) => c.months)) }),
            ].join(" · "),
          },
        ]
      : []),
    { icon: TriangleAlert, tone: "bg-warn/15 text-warn", title: t("Не учтены"), text: t("новые акции, цены, ассортимент, форс-мажор") },
  ];
  return (
    <div className={`flex flex-col ${compact ? "gap-2.5" : "gap-3.5"}`}>
      {rows.map((r) => (
        <div key={r.title} className="flex items-start gap-3">
          <span className={`grid shrink-0 place-items-center rounded-lg ${r.tone} ${compact ? "size-[26px]" : "size-7"}`} aria-hidden>
            <r.icon className="size-3.5" />
          </span>
          {compact ? (
            <span className="pt-1 text-[12px] leading-snug text-ink-2">
              {r.title}
              {r.text ? ` · ${r.text}` : ""}
            </span>
          ) : (
            <span className="min-w-0">
              <span className="block text-[13px] font-semibold text-ink">{r.title}</span>
              {r.text && <span className="block text-[12px] text-ink-3">{r.text}</span>}
            </span>
          )}
        </div>
      ))}
      {compact ? (
        <Link href={simulatorHref} className="mt-1 inline-flex h-[38px] items-center justify-center gap-2 rounded-[10px] bg-accent/15 text-[12px] font-semibold text-accent-strong">
          <SlidersHorizontal aria-hidden className="size-4" />
          {t("Учесть изменения в Симуляторе")}
        </Link>
      ) : (
        <Link href={simulatorHref} className="group flex items-start gap-3 rounded-xl">
          <span className="grid size-7 shrink-0 place-items-center rounded-lg bg-accent/15 text-accent-strong" aria-hidden>
            <SlidersHorizontal className="size-3.5" />
          </span>
          <span className="min-w-0">
            <span className="block text-[13px] font-semibold text-accent-strong group-hover:underline">{t("Хотите учесть изменения?")}</span>
            <span className="block text-[12px] text-ink-3">{t("Передайте прогноз в Симулятор сценариев")}</span>
          </span>
        </Link>
      )}
    </div>
  );
}
