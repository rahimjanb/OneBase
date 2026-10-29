import type { Delta } from "@/lib/hr-overview";

/** Карточка: surface, рамка border, радиус 16 на телефоне и 14 на ноутбуке, без теней. */
export const card = "rounded-[16px] border border-(--ts-border) bg-(--ts-surface) lg:rounded-[14px]";

export const focus = "focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent) focus-visible:ring-offset-2 focus-visible:ring-offset-(--ts-bg)";

const deltaTone = { success: "text-(--ts-success)", danger: "text-(--ts-danger)", muted: "text-(--ts-muted)" };

/** Строка динамики под числом; нет данных — строки нет (не «0» и не «—»). */
export function DeltaLine({ delta, className = "" }: { delta: Delta; className?: string }) {
  if (!delta) return null;
  return <p className={`truncate font-medium ${deltaTone[delta.tone]} ${className}`}>{delta.text}</p>;
}

/**
 * Подсказка над элементом при наведении (и фокусе внутри): тёмная плашка в обеих темах. Родитель — `group/tip relative`.
 * Только дополняет видимые подписи — сами значения доступны и без неё.
 */
export function Tip({ children, align = "center" }: { children: React.ReactNode; align?: "center" | "start" | "end" }) {
  const position = align === "start" ? "left-0" : align === "end" ? "right-0" : "left-1/2 -translate-x-1/2";
  return (
    <span
      role="tooltip"
      className={`pointer-events-none absolute bottom-[calc(100%+8px)] z-30 hidden whitespace-nowrap rounded-[8px] border border-(--ts-pop-border) bg-(--ts-pop-bg) px-2.5 py-1.5 text-xs font-medium text-(--ts-pop-text) shadow-(--ts-pop-shadow) group-hover/tip:block group-focus-within/tip:block ${position}`}
    >
      {children}
    </span>
  );
}
