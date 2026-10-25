import type { Tone } from "@/lib/dashboard.mock";
import { ReloadButton } from "./ReloadButton";

/** Классы по тону — только токены темы (globals.css), без hex в компонентах. */
export const toneText: Record<Tone, string> = {
  accent: "text-accent",
  ok: "text-ok",
  warn: "text-warn",
  bad: "text-bad",
  violet: "text-violet-strong",
  pink: "text-pink",
  cyan: "text-cyan",
  muted: "text-ink-3",
};

export const toneBg: Record<Tone, string> = {
  accent: "bg-accent",
  ok: "bg-ok",
  warn: "bg-warn",
  bad: "bg-bad",
  violet: "bg-violet",
  pink: "bg-pink",
  cyan: "bg-cyan",
  muted: "bg-ink-3",
};

/** Мягкая заливка (альфа ≈ 1A/1F): чипы, плитки иконок, аватары. */
export const toneSoft: Record<Tone, string> = {
  accent: "bg-accent/12",
  ok: "bg-ok/10",
  warn: "bg-warn/10",
  bad: "bg-bad/10",
  violet: "bg-violet/12",
  pink: "bg-pink/12",
  cyan: "bg-cyan/12",
  muted: "bg-muted",
};

/** Столбики мини-графиков (альфа 40 ≈ 25 %). */
export const toneBar: Record<Tone, string> = {
  accent: "bg-accent/25",
  ok: "bg-ok/25",
  warn: "bg-warn/25",
  bad: "bg-bad/25",
  violet: "bg-violet/25",
  pink: "bg-pink/25",
  cyan: "bg-cyan/25",
  muted: "bg-ink-3/25",
};

/** Аватар с инициалами (альфа 33 ≈ 20 %). */
export const toneAvatar: Record<Tone, string> = {
  accent: "bg-accent/20 text-accent",
  ok: "bg-ok/20 text-ok",
  warn: "bg-warn/20 text-warn",
  bad: "bg-bad/20 text-bad",
  violet: "bg-violet/20 text-violet-strong",
  pink: "bg-pink/20 text-pink",
  cyan: "bg-cyan/20 text-cyan",
  muted: "bg-muted text-ink-3",
};

export function DashCard({ className = "", children, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={`rounded-[14px] border border-line bg-surface ${className}`} {...props}>
      {children}
    </div>
  );
}

/** Шапка карточки: заголовок 15px и подзаголовок 12.5px, справа — действие. */
export function CardHeader({ title, subtitle, action, className = "" }: { title: string; subtitle?: string; action?: React.ReactNode; className?: string }) {
  return (
    <div className={`flex flex-wrap items-start justify-between gap-3 ${className}`}>
      <div className="min-w-0">
        <h2 className="text-[15px] font-semibold leading-tight text-ink">{title}</h2>
        {subtitle && <p className="mt-0.5 text-[12.5px] text-ink-4">{subtitle}</p>}
      </div>
      {action && <div className="flex shrink-0 items-center gap-2">{action}</div>}
    </div>
  );
}

export function Avatar({ initials, tone, size = 26, className = "" }: { initials: string; tone: Tone; size?: number; className?: string }) {
  return (
    <span
      className={`grid shrink-0 place-items-center rounded-full font-semibold ${toneAvatar[tone]} ${className}`}
      style={{ width: size, height: size, fontSize: Math.round(size * 0.38) }}
      aria-hidden
    >
      {initials}
    </span>
  );
}

/** Ссылка-действие «… →» в шапках карточек. */
export const linkClass =
  "inline-flex items-center gap-1 rounded-md text-[12.5px] font-semibold text-accent-strong transition-colors duration-150 hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent";

/** Вторичная кнопка: обводка border-strong, скругление 8–10. */
export const outlineButton =
  "inline-flex items-center justify-center gap-2 rounded-[10px] border border-line-strong bg-transparent text-[13px] font-semibold text-ink transition-colors duration-150 hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent disabled:opacity-50";

export type BlockStatus = "ready" | "loading" | "empty" | "error";

export function Skeleton({ className = "" }: { className?: string }) {
  return <div aria-hidden className={`animate-pulse rounded-md bg-muted ${className}`} />;
}

/**
 * Состояние блока с данными: загрузка — скелет в форме карточки, пусто — подпись, ошибка — текст и кнопка обновления.
 * Тексты приходят уже переведёнными (страница — серверная, компоненты — любые).
 */
export function BlockState({
  status,
  skeleton,
  emptyText,
  errorText,
  retryText,
  children,
}: {
  status: BlockStatus;
  skeleton: React.ReactNode;
  emptyText: string;
  errorText: string;
  retryText: string;
  children: React.ReactNode;
}) {
  if (status === "loading") return <>{skeleton}</>;
  if (status === "empty") return <div className="flex min-h-[120px] items-center justify-center px-5 py-8 text-center text-[13px] text-ink-4">{emptyText}</div>;
  if (status === "error") {
    return (
      <div className="flex min-h-[120px] flex-col items-center justify-center gap-3 px-5 py-8 text-center">
        <span className="text-[13px] text-bad">{errorText}</span>
        <ReloadButton className={`${outlineButton} h-8 px-3 text-xs`}>{retryText}</ReloadButton>
      </div>
    );
  }
  return <>{children}</>;
}

/** Скелет карточки: заголовок и несколько строк. */
export function CardSkeleton({ rows = 4, className = "" }: { rows?: number; className?: string }) {
  return (
    <div className={`flex flex-col gap-3 p-5 ${className}`}>
      <Skeleton className="h-4 w-40" />
      <Skeleton className="h-3 w-56" />
      {Array.from({ length: rows }, (_, i) => (
        <Skeleton key={i} className="h-10 w-full" />
      ))}
    </div>
  );
}
