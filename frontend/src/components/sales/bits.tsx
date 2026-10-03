import Link from "next/link";
import { ArrowUpRight, CircleAlert, CircleCheck, Info, TriangleAlert, type LucideIcon } from "lucide-react";
import type { AgentFlag, FlagCounts, TargetLevel, TargetValue } from "@/lib/sales/types";
import { delta } from "@/lib/sales/format";

/** Цвет значения: обычный, акцент (план), зелёный, оранжевый, красный, серый. */
export type Tone = "ink" | "accent" | "ok" | "warn" | "bad" | "muted";

export const toneClass: Record<Tone, string> = {
  ink: "text-ink",
  accent: "text-accent-strong",
  ok: "text-ok",
  warn: "text-warn",
  bad: "text-bad",
  muted: "text-ink-3",
};

/** Цвет выполнения в таблицах: ≥100% зелёный, 90–99% обычный, 70–89% оранжевый, ниже — красный. */
export function execClass(value: number | null): string {
  if (value == null) return "text-ink-3";
  if (value >= 1) return "text-ok font-semibold";
  if (value >= 0.9) return "text-ink";
  if (value >= 0.7) return "text-warn";
  return "text-bad";
}

const levelTone: Record<TargetLevel, Tone> = { Good: "ok", Warning: "warn", Bad: "bad" };

/** Тон по уровню цели (конверсия, выручка на ТТ, АКБ на агента); без цели — обычный. */
export const targetTone = (t: TargetValue): Tone => (t.level ? levelTone[t.level] : "ink");

/** Тон доли плана: ≥100% зелёный, 70–99% оранжевый, меньше — красный; без плана — серый. */
export const planTone = (share: number | null | undefined): Tone => (share == null ? "muted" : share >= 1 ? "ok" : share >= 0.7 ? "warn" : "bad");

/** Тон выполнения плана, как execClass в таблицах: ≥100% зелёный, 90–99% обычный, 70–89% оранжевый, ниже — красный. */
export const execTone = (value: number | null | undefined): Tone => (value == null ? "muted" : value >= 1 ? "ok" : value >= 0.9 ? "ink" : value >= 0.7 ? "warn" : "bad");

/** Тон изменения к прошлому периоду: рост зелёный, падение до −10% оранжевый, сильнее — красный. */
export const deltaTone = (value: number | null | undefined): Tone => (value == null ? "muted" : value >= 0 ? "ok" : value > -0.1 ? "warn" : "bad");

/** Плитка KPI: подпись капителью, крупное значение (цвет — tone) и одна строка пояснения; подробности — в title. */
export function KpiTile({
  label,
  value,
  unit,
  tone = "ink",
  title,
  children,
}: {
  label: string;
  value: string;
  unit?: string;
  tone?: Tone;
  /** Всплывающая подсказка с подробностями, которые не влезают в строку пояснения. */
  title?: string;
  children?: React.ReactNode;
}) {
  return (
    <div className="flex min-w-0 flex-col rounded-xl border border-line bg-surface px-4 py-3.5 shadow-sm max-lg:px-3.5 max-lg:py-3" title={title}>
      <div className="text-[11px] font-medium uppercase tracking-[0.08em] text-ink-3">{label}</div>
      <div className="mt-1.5 flex flex-wrap items-baseline gap-x-1.5">
        <span className={`text-[24px] font-semibold leading-none tracking-tight tabular-nums max-lg:text-xl ${toneClass[tone]}`}>{value}</span>
        {unit && <span className="text-sm text-ink-3">{unit}</span>}
      </div>
      {children && <div className="mt-1.5 text-xs leading-relaxed text-ink-2">{children}</div>}
    </div>
  );
}

/** Показатель внутри карточки: подпись капителью и значение; значения ряда — на одной линии, даже если подпись в две строки. */
export function Stat({ label, value, tone = "ink" }: { label: string; value: React.ReactNode; tone?: Tone }) {
  return (
    <div className="flex min-w-0 flex-col justify-between">
      <dt className="text-[10px] font-medium uppercase leading-tight tracking-[0.08em] text-ink-3">{label}</dt>
      <dd className={`mt-1 whitespace-nowrap text-sm font-semibold tabular-nums ${toneClass[tone]}`}>{value}</dd>
    </div>
  );
}

/**
 * Карточка-сводка: заголовок, подзаголовок, стрелка (если есть ссылка) и содержимое.
 * aside — крупное значение справа от заголовка (первичка); тогда стрелка стоит рядом с названием.
 */
export function SummaryCard({
  title,
  subtitle,
  href,
  aside,
  children,
}: {
  title: string;
  subtitle?: React.ReactNode;
  href?: string | null;
  aside?: React.ReactNode;
  children: React.ReactNode;
}) {
  const arrow = href ? <ArrowUpRight className="size-4 shrink-0 text-ink-3 group-hover:text-accent-strong" aria-hidden /> : null;
  const body = (
    <section className={`flex h-full flex-col rounded-xl border border-line bg-surface p-5 shadow-sm ${href ? "transition-colors group-hover:border-accent/40" : ""}`}>
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h2 className="flex items-center gap-2 text-base font-semibold text-ink">
            {title}
            {aside && arrow}
          </h2>
          {subtitle && <p className="mt-0.5 text-xs text-ink-3">{subtitle}</p>}
        </div>
        {aside ?? arrow}
      </div>
      {children}
    </section>
  );
  return href ? (
    <Link href={href} aria-label={`Открыть «${title}»`} className="group block max-lg:active:opacity-75">
      {body}
    </Link>
  ) : (
    body
  );
}

/** Цветная пилюля изменения: рост — зелёная, падение до −10% — оранжевая, сильнее — красная. */
export function DeltaPill({ value }: { value: number | null }) {
  if (value == null) return <span className="text-ink-3">—</span>;
  const tone = value >= 0 ? "bg-ok-soft text-ok" : value > -0.1 ? "bg-warn-soft text-warn" : "bg-bad-soft text-bad";
  return <span className={`inline-block whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-semibold tabular-nums ${tone}`}>{delta(value)}</span>;
}

const severityStyles = {
  Critical: "border-bad/25 bg-bad-soft text-bad",
  Risk: "border-warn/25 bg-warn-soft text-warn",
  Info: "border-line bg-muted text-ink-2",
} as const;

/** Флаги агента: «● Конверсия», «● Темп» — красные критичные, оранжевые риски. */
export function FlagPills({ flags, empty = "Замечаний нет" }: { flags: AgentFlag[]; empty?: string | null }) {
  if (flags.length === 0) {
    return empty ? <span className="inline-block rounded-full bg-ok-soft px-2 py-0.5 text-xs font-medium text-ok">{empty}</span> : null;
  }
  return (
    <span className="inline-flex flex-wrap gap-1.5">
      {flags.map((f) => (
        <span
          key={f.kind}
          title={f.title}
          className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border px-2 py-0.5 text-xs font-semibold ${severityStyles[f.severity]}`}
        >
          <span className="size-1.5 rounded-full bg-current" />
          {f.label}
        </span>
      ))}
    </span>
  );
}

export function FlagCountPills({ flags }: { flags: FlagCounts }) {
  if (flags.critical === 0 && flags.risk === 0) {
    return <span className="inline-block whitespace-nowrap rounded-full bg-ok-soft px-2 py-0.5 text-xs font-medium text-ok">Замечаний нет</span>;
  }
  return (
    <span className="inline-flex flex-wrap gap-1.5">
      {flags.critical > 0 && (
        <span className="inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border border-bad/25 bg-bad-soft px-2 py-0.5 text-xs font-semibold text-bad">
          <span className="size-1.5 rounded-full bg-current" />
          {flags.critical} критично
        </span>
      )}
      {flags.risk > 0 && (
        <span className="inline-flex items-center gap-1.5 whitespace-nowrap rounded-full border border-warn/25 bg-warn-soft px-2 py-0.5 text-xs font-semibold text-warn">
          <span className="size-1.5 rounded-full bg-current" />
          {flags.risk} риск
        </span>
      )}
    </span>
  );
}

/** Серая сноска «как считается». */
export function Note({ children }: { children: React.ReactNode }) {
  return <p className="mt-3 border-l-2 border-line pl-3 text-xs leading-relaxed text-ink-3">{children}</p>;
}

// Секция живёт в клиентском модуле: под карточками категорий она сворачивается (CollapsedSections).
export { CollapsedSections, Section } from "./Section";

const alertStyles: Record<"warn" | "bad" | "info" | "ok", { icon: LucideIcon; className: string }> = {
  warn: { icon: TriangleAlert, className: "text-warn" },
  bad: { icon: CircleAlert, className: "text-bad" },
  info: { icon: Info, className: "text-ink-3" },
  ok: { icon: CircleCheck, className: "text-ok" },
};

/** Полоса-уведомление: белая карточка со значком по тону; выделенное жирным — тёмным. */
export function Alert({ tone = "warn", className = "mt-4", children }: { tone?: keyof typeof alertStyles; className?: string; children: React.ReactNode }) {
  const { icon: Icon, className: iconClass } = alertStyles[tone];
  return (
    <div className={`flex items-start gap-3 rounded-xl border border-line bg-surface px-4 py-3 text-sm text-ink-2 shadow-sm [&_b]:font-semibold [&_b]:text-ink ${className}`}>
      <Icon className={`mt-0.5 size-4 shrink-0 ${iconClass}`} aria-hidden />
      <div className="min-w-0 flex-1">{children}</div>
    </div>
  );
}

/** Шкала выполнения: без плана — пустая штриховка, а не красная. */
export function ExecutionBar({ value, tone }: { value: number | null; tone?: "accent" | "warn" | "bad" }) {
  if (value == null) return <div className="hatched h-1.5 w-full rounded-full" />;
  const color = tone === "bad" ? "bg-bad" : tone === "warn" ? "bg-warn" : "bg-accent";
  return (
    <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
      <div className={`h-full rounded-full ${color}`} style={{ width: `${Math.min(100, Math.max(0, value * 100))}%` }} />
    </div>
  );
}
