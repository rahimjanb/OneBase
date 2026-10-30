import Link from "next/link";
import { ChevronRight } from "lucide-react";
import m from "./mobile.module.css";

/** Карточка: surface, рамка 1px border, радиус 20, без теней. */
export const card = "rounded-[20px] border border-(--ts-border) bg-(--ts-surface)";

export const focusRing = "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent) focus-visible:ring-offset-2 focus-visible:ring-offset-(--ts-bg)";

/** Нажимаемое: сжатие при нажатии (globals.css) — плавно. */
export const press = "transition-transform duration-150";

/** Заголовок секции 17/700, рядом — счётчики, справа — ссылка «Подробнее ›» 13/600 (зона нажатия 44 px). */
export function SectionHeader({ title, href, action, children }: { title: string; href?: string; action?: string; children?: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-[12px] px-[4px]">
      <div className="flex min-w-0 flex-wrap items-center gap-x-[10px] gap-y-[2px]">
        <h2 className="text-[17px] font-bold leading-tight tracking-[-0.2px] text-(--ts-text)">{title}</h2>
        {children}
      </div>
      {href && action && (
        <Link
          href={href}
          className={`-my-[12px] inline-flex min-h-[44px] shrink-0 items-center gap-[2px] rounded-[8px] text-[13px] font-semibold text-(--ts-link) ${press} ${focusRing}`}
        >
          {action}
          <ChevronRight aria-hidden className="size-[15px]" strokeWidth={2.2} />
        </Link>
      )}
    </div>
  );
}

/** Серая плашка заготовки. */
export function Sk({ className = "", style }: { className?: string; style?: React.CSSProperties }) {
  return <span aria-hidden className={`block ${m.sk} ${className}`} style={style} />;
}
