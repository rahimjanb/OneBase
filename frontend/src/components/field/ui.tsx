import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { chipTone, type Tone } from "@/lib/field/labels";

/** Страница Sales Base: заголовок, подзаголовок, действия справа; на телефоне — компактно. */
export function FieldPage({
  title,
  subtitle,
  actions,
  back,
  children,
}: {
  title: string;
  subtitle?: React.ReactNode;
  actions?: React.ReactNode;
  back?: { href: string; label: string };
  children: React.ReactNode;
}) {
  return (
    <div className="mx-auto w-full max-w-[1400px] px-4 pb-8 pt-4 sm:px-6 lg:pt-6">
      {back && (
        <Link href={back.href} className="mb-2 inline-flex items-center gap-1 text-sm text-ink-3 hover:text-ink max-lg:min-h-9">
          ← {back.label}
        </Link>
      )}
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-xl font-semibold tracking-tight text-ink sm:text-2xl">{title}</h1>
          {subtitle && <div className="mt-1 text-sm text-ink-2">{subtitle}</div>}
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
      <div className="mt-4 space-y-4 lg:mt-6">{children}</div>
    </div>
  );
}

export function Card({ className = "", children, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={`min-w-0 rounded-xl border border-line bg-surface ${className}`} {...props}>
      {children}
    </div>
  );
}

/** Секция-карточка с заголовком и ссылкой/действием справа. */
export function Panel({ title, hint, action, children, className = "" }: { title: string; hint?: React.ReactNode; action?: React.ReactNode; children: React.ReactNode; className?: string }) {
  return (
    <section className={`min-w-0 rounded-xl border border-line bg-surface p-4 sm:p-5 ${className}`}>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <div className="flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-0.5">
          <h2 className="text-base font-semibold text-ink">{title}</h2>
          {hint && <span className="text-xs text-ink-3">{hint}</span>}
        </div>
        {action}
      </div>
      {children}
    </section>
  );
}

/** Показатель: подпись, крупное значение, пояснение и полоса выполнения. */
export function Stat({
  label,
  value,
  unit,
  hint,
  share,
  tone = "ink",
  href,
}: {
  label: string;
  value: React.ReactNode;
  unit?: string;
  hint?: React.ReactNode;
  share?: number | null;
  tone?: Tone;
  href?: string;
}) {
  const color = { ok: "text-ok", warn: "text-warn", bad: "text-bad", accent: "text-accent-strong", muted: "text-ink-3", ink: "text-ink" }[tone];
  const body = (
    <>
      <div className="text-[11px] font-medium uppercase tracking-[0.08em] text-ink-3">{label}</div>
      <div className="mt-1.5 flex flex-wrap items-baseline gap-x-1.5">
        <span className={`text-2xl font-semibold leading-none tracking-tight tabular-nums max-sm:text-xl ${color}`}>{value}</span>
        {unit && <span className="text-sm text-ink-3">{unit}</span>}
      </div>
      {share != null && <Bar share={share} className="mt-2.5" />}
      {hint && <div className="mt-1.5 text-xs leading-relaxed text-ink-2">{hint}</div>}
    </>
  );
  const cls = "block min-w-0 rounded-xl border border-line bg-surface px-4 py-3.5 max-sm:px-3.5 max-sm:py-3";
  return href ? (
    <Link href={href} className={`${cls} transition-colors hover:border-ink-3`}>
      {body}
    </Link>
  ) : (
    <div className={cls}>{body}</div>
  );
}

/** Полоса выполнения: зелёная от 100%, синяя-бирюзовая до, оранжевая ниже 70%. */
export function Bar({ share, expected, className = "" }: { share: number; expected?: number; className?: string }) {
  const pct = Math.max(0, Math.min(100, share * 100));
  const color = share >= 1 ? "bg-ok" : share >= 0.7 ? "bg-accent" : "bg-warn";
  return (
    <div className={`relative h-1.5 w-full overflow-hidden rounded-full bg-muted ${className}`}>
      <div className={`h-full rounded-full ${color}`} style={{ width: `${pct}%` }} />
      {expected != null && <div className="absolute top-0 h-full w-0.5 bg-ink-3" style={{ left: `${Math.min(100, expected * 100)}%` }} title="Ожидаемый темп" />}
    </div>
  );
}

export function Chip({ tone = "ink", children, className = "" }: { tone?: Tone; children: React.ReactNode; className?: string }) {
  return <span className={`inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-full px-2 py-0.5 text-xs font-medium ${chipTone[tone]} ${className}`}>{children}</span>;
}

export function Empty({ children, action }: { children: React.ReactNode; action?: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-dashed border-line bg-surface/60 px-5 py-8 text-center text-sm text-ink-3">
      <div>{children}</div>
      {action && <div className="mt-3 flex justify-center">{action}</div>}
    </div>
  );
}

/** Строка-ссылка для мобильных списков: заголовок, подпись, справа — значение и стрелка. */
export function ListLink({ href, title, subtitle, right, leading }: { href: string; title: React.ReactNode; subtitle?: React.ReactNode; right?: React.ReactNode; leading?: React.ReactNode }) {
  return (
    <Link href={href} prefetch={false} className="flex min-h-14 items-center gap-3 px-4 py-3 transition-colors hover:bg-muted active:bg-muted">
      {leading}
      <div className="min-w-0 flex-1">
        <div className="truncate text-sm font-medium text-ink">{title}</div>
        {subtitle && <div className="mt-0.5 truncate text-xs text-ink-3">{subtitle}</div>}
      </div>
      {right && <div className="shrink-0 text-right text-sm">{right}</div>}
      <ChevronRight className="size-4 shrink-0 text-ink-3" />
    </Link>
  );
}

/** Ссылки-переключатели (табы/фильтры) — работают без JavaScript. */
export function Tabs({ items, current }: { items: { key: string; label: React.ReactNode; href: string }[]; current: string }) {
  return (
    <div className="no-scrollbar -mx-4 flex gap-1 overflow-x-auto px-4 sm:mx-0 sm:px-0">
      {items.map((i) => (
        <Link
          key={i.key}
          href={i.href}
          className={`shrink-0 whitespace-nowrap rounded-full px-3.5 py-1.5 text-sm max-lg:py-2 ${current === i.key ? "bg-ink text-surface" : "bg-surface text-ink-2 ring-1 ring-line hover:bg-muted"}`}
        >
          {i.label}
        </Link>
      ))}
    </div>
  );
}

/** Пагинация ссылками. */
export function Pager({ page, pageSize, total, href }: { page: number; pageSize: number; total: number; href: (page: number) => string }) {
  const pages = Math.max(1, Math.ceil(total / pageSize));
  if (pages <= 1) return null;
  return (
    <div className="flex items-center justify-between gap-3 text-sm text-ink-2">
      <span>
        {(page - 1) * pageSize + 1}–{Math.min(page * pageSize, total)} из {total.toLocaleString("ru-RU")}
      </span>
      <div className="flex gap-2">
        {page > 1 && (
          <Link href={href(page - 1)} className="rounded-lg border border-line bg-surface px-3 py-1.5 hover:bg-muted max-lg:py-2.5">
            Назад
          </Link>
        )}
        {page < pages && (
          <Link href={href(page + 1)} className="rounded-lg border border-line bg-surface px-3 py-1.5 hover:bg-muted max-lg:py-2.5">
            Дальше
          </Link>
        )}
      </div>
    </div>
  );
}

export const inputClass =
  "h-11 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 lg:h-10";

export const buttonClass = {
  primary: "inline-flex h-11 items-center justify-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50 lg:h-10",
  outline: "inline-flex h-11 items-center justify-center gap-2 rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50 lg:h-10",
  danger: "inline-flex h-11 items-center justify-center gap-2 rounded-lg border border-bad/30 bg-bad-soft px-4 text-sm font-medium text-bad hover:bg-bad/10 disabled:opacity-50 lg:h-10",
  ghost: "inline-flex h-11 items-center justify-center gap-2 rounded-lg px-3 text-sm font-medium text-ink-2 hover:bg-muted disabled:opacity-50 lg:h-10",
};
