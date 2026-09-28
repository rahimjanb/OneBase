import type { AgentFlag, FlagCounts, TargetLevel, TargetValue } from "@/lib/sales/types";
import { delta, pct } from "@/lib/sales/format";

/** Цвет выполнения: ≥100% зелёный, 90–99% обычный, 70–89% оранжевый, ниже — красный. */
export function execClass(value: number | null): string {
  if (value == null) return "text-ink-3";
  if (value >= 1) return "text-ok font-semibold";
  if (value >= 0.9) return "text-ink";
  if (value >= 0.7) return "text-warn";
  return "text-bad";
}

const levelStyles: Record<TargetLevel, string> = {
  Good: "bg-ok-soft text-ok",
  Warning: "bg-warn-soft text-warn",
  Bad: "bg-bad-soft text-bad",
};

/** Плашка «X% от цели»: ≥100% зелёная, 70–99% оранжевая, &lt;70% красная. */
export function TargetBadge({ target }: { target: TargetValue }) {
  if (target.ratio == null || target.level == null) return null;
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-semibold ${levelStyles[target.level]}`}>
      {pct(target.ratio)} от цели
    </span>
  );
}

export function KpiTile({
  label,
  value,
  unit,
  children,
  badge,
}: {
  label: string;
  value: string;
  unit?: string;
  children?: React.ReactNode;
  badge?: React.ReactNode;
}) {
  return (
    <div className="flex min-w-0 flex-col rounded-xl border border-line bg-surface p-4">
      <div className="text-xs text-ink-2">{label}</div>
      <div className="mt-2 flex items-baseline gap-1.5">
        <span className="text-[26px] font-semibold leading-none tracking-tight tabular-nums text-ink">{value}</span>
        {unit && <span className="text-sm text-ink-3">{unit}</span>}
      </div>
      {badge && <div className="mt-2">{badge}</div>}
      {children && <div className="mt-2 text-xs leading-relaxed text-ink-2">{children}</div>}
    </div>
  );
}

/** Цветная пилюля изменения: рост — зелёная, падение до −10% — оранжевая, сильнее — красная. */
export function DeltaPill({ value }: { value: number | null }) {
  if (value == null) return <span className="text-ink-3">—</span>;
  const tone = value >= 0 ? "bg-ok-soft text-ok" : value > -0.1 ? "bg-warn-soft text-warn" : "bg-bad-soft text-bad";
  return <span className={`inline-block whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-semibold tabular-nums ${tone}`}>{delta(value)}</span>;
}

const severityStyles = {
  Critical: "bg-bad-soft text-bad",
  Risk: "bg-warn-soft text-warn",
  Info: "bg-muted text-ink-2",
} as const;

export function FlagPills({ flags, empty = "Замечаний нет" }: { flags: AgentFlag[]; empty?: string | null }) {
  if (flags.length === 0) {
    return empty ? <span className="inline-block rounded-full bg-ok-soft px-2 py-0.5 text-xs font-medium text-ok">{empty}</span> : null;
  }
  return (
    <span className="inline-flex flex-wrap gap-1">
      {flags.map((f) => (
        <span key={f.kind} title={f.title} className={`whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ${severityStyles[f.severity]}`}>
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
    <span className="inline-flex flex-wrap gap-1">
      {flags.critical > 0 && (
        <span className="whitespace-nowrap rounded-full bg-bad-soft px-2 py-0.5 text-xs font-medium text-bad">{flags.critical} критично</span>
      )}
      {flags.risk > 0 && (
        <span className="whitespace-nowrap rounded-full bg-warn-soft px-2 py-0.5 text-xs font-medium text-warn">{flags.risk} риск</span>
      )}
    </span>
  );
}

/** Серая сноска «как считается». */
export function Note({ children }: { children: React.ReactNode }) {
  return <p className="mt-3 border-l-2 border-line pl-3 text-xs leading-relaxed text-ink-3">{children}</p>;
}

/** Секция-карточка с заголовком, пояснением и действиями справа. */
export function Section({
  title,
  hint,
  actions,
  children,
  className = "",
}: {
  title: string;
  hint?: React.ReactNode;
  actions?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <section className={`mt-6 rounded-xl border border-line bg-surface p-4 sm:p-5 ${className}`}>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
          <h2 className="text-base font-semibold text-ink">{title}</h2>
          {hint && <span className="text-xs text-ink-3">{hint}</span>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {children}
    </section>
  );
}

export function Alert({ tone = "warn", children }: { tone?: "warn" | "bad" | "info"; children: React.ReactNode }) {
  const styles = { warn: "border-warn/30 bg-warn-soft text-ink", bad: "border-bad/30 bg-bad-soft text-ink", info: "border-line bg-muted text-ink-2" };
  return <div className={`mt-4 rounded-lg border px-4 py-3 text-sm ${styles[tone]}`}>{children}</div>;
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
