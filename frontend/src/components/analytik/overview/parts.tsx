"use client";

import Link from "next/link";
import { ArrowDownRight, ArrowUpRight, Calculator, CircleCheck, Sparkles, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import type { KpiKind } from "@/lib/analytik";

/**
 * Детали «Обзора» Аналитика по макету (oy7PN / PIFPr): карточка, знак AI-аналитика, значок источника показателя, чип изменения, ссылка
 * «Подробнее». Цвета — токены палитры страницы (data-palette="sales-secondary": surface-2 → muted, subtle → ink-4, link → accent-strong).
 */

/** Фирменный AI-градиент знака модуля и иконок AI-аналитика. */
export const aiGradient = "bg-[linear-gradient(135deg,#5b97ff,#8b5cf6)] text-white";

export function AiMark({ icon: Icon = Sparkles, size = 36, radius = 10, iconSize = 18 }: { icon?: LucideIcon; size?: number; radius?: number; iconSize?: number }) {
  return (
    <span aria-hidden className={`grid shrink-0 place-items-center ${aiGradient}`} style={{ width: size, height: size, borderRadius: radius }}>
      <Icon style={{ width: iconSize, height: iconSize }} strokeWidth={2} />
    </span>
  );
}

/** Карточка блока: radius 18, padding 20 (телефон — 16), заголовок 15/700, подзаголовок 11 subtle, действие справа. */
export function OCard({
  title,
  subtitle,
  action,
  children,
  className = "",
  id,
}: {
  title?: React.ReactNode;
  subtitle?: React.ReactNode;
  action?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
  id?: string;
}) {
  return (
    <section id={id} className={`flex min-w-0 flex-col rounded-[18px] border border-line bg-surface p-5 max-md:p-4 ${className}`}>
      {(title || action) && (
        <header className="mb-3.5 flex items-start justify-between gap-3">
          <div className="min-w-0">
            {title && <h2 className="text-[15px] font-bold leading-tight text-ink">{title}</h2>}
            {subtitle && <p className="mt-1 text-[11px] font-medium text-ink-4">{subtitle}</p>}
          </div>
          {action}
        </header>
      )}
      {children}
    </section>
  );
}

/** «Подробнее ↗» — 12/600 link. */
export function MoreLink({ href, label = "Подробнее" }: { href: string; label?: string }) {
  const t = useT();
  return (
    <Link href={href} className="inline-flex shrink-0 items-center gap-1 text-[12px] font-semibold text-accent-strong hover:underline">
      {t(label)}
      <ArrowUpRight className="size-3.5" aria-hidden />
    </Link>
  );
}

const sourceStyle: Record<KpiKind, { icon: LucideIcon; cls: string; label: string; hint: string }> = {
  fact: { icon: CircleCheck, cls: "bg-ok-soft text-ok", label: "Факт", hint: "Факт — данные как есть из источника" },
  calculated: { icon: Calculator, cls: "bg-accent-soft text-accent-strong", label: "Расчёт", hint: "Расчёт — посчитано по фактическим данным" },
  forecast: { icon: Sparkles, cls: "bg-violet-soft text-violet", label: "Прогноз", hint: "Прогноз — оценка AI-аналитика при текущем темпе" },
};

/** Значок источника показателя 18×18 (телефон — 16): факт, расчёт, прогноз; подсказка и озвучка — словами. */
export function SourceIcon({ kind, source, small = false }: { kind: KpiKind; source?: string; small?: boolean }) {
  const t = useT();
  const s = sourceStyle[kind] ?? sourceStyle.fact;
  const Icon = s.icon;
  const label = `${t("источник")}: ${t(s.label).toLowerCase()}`;
  return (
    <span
      role="img"
      aria-label={label}
      title={`${t(s.hint)}${source ? ` · ${source}` : ""}`}
      className={`grid shrink-0 place-items-center rounded-[5px] ${s.cls} ${small ? "size-4" : "size-[18px]"}`}
    >
      <Icon className={small ? "size-2.5" : "size-[11px]"} strokeWidth={2.2} aria-hidden />
    </span>
  );
}

/** Легенда источников: ФАКТ / РАСЧЁТ / ПРОГНОЗ — пилюли 10/700. */
export function SourceLegend() {
  const t = useT();
  return (
    <div className="flex flex-wrap items-center gap-1.5">
      {(["fact", "calculated", "forecast"] as const).map((k) => {
        const s = sourceStyle[k];
        const Icon = s.icon;
        return (
          <span key={k} title={t(s.hint)} className={`inline-flex items-center gap-1 rounded-md px-2 py-[3px] text-[10px] font-bold uppercase tracking-wide ${s.cls}`}>
            <Icon className="size-3" strokeWidth={2.2} aria-hidden />
            {t(s.label)}
          </span>
        );
      })}
    </div>
  );
}

/** Чип изменения: стрелка и знак (не только цвет), фон — мягкий цвет «хорошо» или «плохо». */
export function DeltaChip({ text, tone, up }: { text: string; tone: "ok" | "bad" | "muted"; up: boolean }) {
  const Icon = up ? ArrowUpRight : ArrowDownRight;
  const cls = tone === "ok" ? "bg-ok-soft text-ok" : tone === "bad" ? "bg-bad-soft text-bad" : "bg-muted text-ink-3";
  return (
    <span className={`inline-flex items-center gap-0.5 rounded-md px-1.5 py-px text-[11px] font-bold tabular-nums ${cls}`}>
      <Icon className="size-3" strokeWidth={2.4} aria-hidden />
      {text}
    </span>
  );
}

export const toneText = { ok: "text-ok", warn: "text-warn", bad: "text-bad", muted: "text-ink-4" } as const;
export const toneSoft = { ok: "bg-ok-soft text-ok", warn: "bg-warn-soft text-warn", bad: "bg-bad-soft text-bad", muted: "bg-muted text-ink-3" } as const;

/** Плашка ошибки блока с «Повторить»: ошибка одного блока не ломает страницу. */
export function BlockError({ text, onRetry }: { text: string; onRetry?: () => void }) {
  const t = useT();
  return (
    <div role="alert" className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-bad/25 bg-bad-soft px-3 py-2.5 text-[12px] text-bad">
      <span className="min-w-0 break-words">{t(text)}</span>
      {onRetry && (
        <button type="button" onClick={onRetry} className="shrink-0 rounded-lg border border-bad/30 px-2.5 py-1 font-semibold hover:bg-bad/10">
          {t("Повторить")}
        </button>
      )}
    </div>
  );
}

/** Серый прямоугольник скелетона. */
export function Bone({ className = "" }: { className?: string }) {
  return <div aria-hidden className={`animate-pulse rounded-lg bg-muted ${className}`} />;
}
