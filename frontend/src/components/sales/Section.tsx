"use client";

import { createContext, useContext, useState } from "react";
import { ChevronRight } from "lucide-react";

/** true — секции внутри свёрнуты, пока их не раскроют. */
const CollapsedContext = createContext(false);

/**
 * Блоки под карточками категорий: свёрнуты, страница начинается с главного.
 * Клик по заголовку раскрывает блок, повторный — сворачивает. Секции внутри раскрытой сами не сворачиваются.
 */
export function CollapsedSections({ children }: { children: React.ReactNode }) {
  return <CollapsedContext.Provider value={true}>{children}</CollapsedContext.Provider>;
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
  const collapsible = useContext(CollapsedContext);
  const [open, setOpen] = useState(!collapsible);

  if (!collapsible) {
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

  return (
    <section className={`mt-6 rounded-xl border border-line bg-surface p-4 sm:p-5 ${className}`}>
      <div className={`flex flex-wrap items-center justify-between gap-3 ${open ? "mb-3" : ""}`}>
        <h2 className="min-w-0 flex-1 text-base font-semibold text-ink">
          <button type="button" onClick={() => setOpen((o) => !o)} aria-expanded={open} className="group flex w-full items-center gap-2 text-left max-lg:min-h-11">
            <ChevronRight className={`size-4 shrink-0 text-ink-3 transition-transform ${open ? "rotate-90" : ""}`} />
            <span className="flex min-w-0 flex-wrap items-baseline gap-x-3 gap-y-1">
              <span>{title}</span>
              {hint && <span className="text-xs font-normal text-ink-3">{hint}</span>}
            </span>
            <span className="ml-auto shrink-0 pl-3 text-xs font-medium text-accent group-hover:underline">{open ? "Свернуть" : "Развернуть"}</span>
          </button>
        </h2>
        {open && actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {open && <CollapsedContext.Provider value={false}>{children}</CollapsedContext.Provider>}
    </section>
  );
}
