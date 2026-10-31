import Link from "next/link";
import { ChevronRight, type LucideIcon } from "lucide-react";
import type { Tone } from "@/lib/my-day";

/**
 * Общие части «Моего дня» (ТЗ employee-dashboard): карточка (surface, рамка 1px, радиус 16, на телефоне 20, без теней), шапка блока,
 * цвета статусов и плашки-заготовки. Цвета — токены экрана .me-ui (globals.css): тёмная и светлая темы.
 */

export const card = "rounded-[20px] border border-line bg-surface p-4 lg:rounded-2xl lg:p-5";

export const focusRing = "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2 focus-visible:ring-offset-page";

export const toneText: Record<Tone, string> = {
  success: "text-ok",
  warning: "text-warn",
  danger: "text-bad",
  accent: "text-link",
  violet: "text-violet",
  muted: "text-ink-3",
};

export const toneSoft: Record<Tone, string> = {
  success: "bg-ok-soft",
  warning: "bg-warn-soft",
  danger: "bg-bad-soft",
  accent: "bg-accent-soft",
  violet: "bg-violet-soft",
  muted: "bg-surface-2",
};

export const toneDot: Record<Tone, string> = {
  success: "bg-ok",
  warning: "bg-warn",
  danger: "bg-bad",
  accent: "bg-accent",
  violet: "bg-violet",
  muted: "bg-ink-4",
};

/** Шапка блока: значок и заголовок 17/600, справа — подпись или ссылка «Все задачи →». */
export function BlockHeader({
  icon: Icon,
  title,
  short,
  aside,
  href,
  action,
  children,
}: {
  icon: LucideIcon;
  title: string;
  /** Короткий заголовок для телефона («Эффективность»). */
  short?: string;
  aside?: React.ReactNode;
  href?: string;
  action?: string;
  children?: React.ReactNode;
}) {
  return (
    <div className="flex min-h-[28px] items-center justify-between gap-3">
      <h2 className="flex min-w-0 items-center gap-2.5 text-[16px] font-semibold leading-tight text-ink lg:text-[17px]">
        <Icon aria-hidden className="size-[18px] shrink-0 text-ink-3" strokeWidth={2} />
        {short ? (
          <>
            <span className="truncate lg:hidden">{short}</span>
            <span className="hidden truncate lg:inline">{title}</span>
          </>
        ) : (
          <span className="truncate">{title}</span>
        )}
        {children}
      </h2>
      {href && action ? (
        <Link
          href={href}
          className={`-my-2 inline-flex min-h-[44px] shrink-0 items-center gap-1 rounded-lg text-[13px] font-semibold text-link lg:min-h-0 ${focusRing}`}
        >
          {action}
          <ChevronRight aria-hidden className="size-4" />
        </Link>
      ) : (
        aside && <span className="shrink-0 text-right text-[12.5px] text-ink-3">{aside}</span>
      )}
    </div>
  );
}

/** Серая плашка заготовки (общий класс skel из globals.css). */
export function Sk({ className = "" }: { className?: string }) {
  return <span aria-hidden className={`skel ${className}`} />;
}

/** Заготовка блока: заголовок и несколько строк — пока идут данные. */
export function BlockSkeleton({ rows = 3, className = "", label }: { rows?: number; className?: string; label: string }) {
  return (
    <div role="status" aria-busy="true" aria-label={label} className={`${card} ${className}`}>
      <Sk className="h-5 w-40" />
      <div className="mt-4 space-y-3">
        {Array.from({ length: rows }, (_, i) => (
          <Sk key={i} className="h-10 w-full" />
        ))}
      </div>
    </div>
  );
}

/** Пустое состояние внутри блока. */
export function Empty({ children, action }: { children: React.ReactNode; action?: React.ReactNode }) {
  return (
    <div className="flex flex-col items-start gap-2 rounded-xl bg-surface-2 px-4 py-4 text-[13.5px] text-ink-3">
      <p>{children}</p>
      {action}
    </div>
  );
}
