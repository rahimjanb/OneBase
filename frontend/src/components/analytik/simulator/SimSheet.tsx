"use client";

import { useEffect, useRef, useState } from "react";
import { X } from "lucide-react";
import { useT } from "@/i18n/client";

/**
 * Панель поверх экрана симулятора (role="dialog"): на телефоне — снизу (bottom sheet, от 84 px сверху, свайп вниз закрывает), на планшете
 * и компьютере — сбоку (side: left — параметры, right — сценарии). Фокус остаётся внутри, Esc закрывает, фон затемнён и не прокручивается.
 */
export function Sheet({
  open,
  onClose,
  label,
  side = "right",
  header,
  footer,
  children,
  width = "md:w-[420px]",
}: {
  open: boolean;
  onClose: () => void;
  label: string;
  side?: "left" | "right";
  header: React.ReactNode;
  footer?: React.ReactNode;
  children: React.ReactNode;
  width?: string;
}) {
  const t = useT();
  const panel = useRef<HTMLDivElement>(null);
  const [drag, setDrag] = useState(0);
  const start = useRef<number | null>(null);
  // Колбэк — в ref: родитель пересчитывается на каждое изменение параметра, а эффект (фокус, блокировка прокрутки) — только при открытии.
  const close = useRef(onClose);
  useEffect(() => {
    close.current = onClose;
  });

  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement as HTMLElement | null;
    const scroller = document.querySelector<HTMLElement>("[data-app-scroll]");
    const overflow = scroller?.style.overflow ?? "";
    if (scroller) scroller.style.overflow = "hidden";
    const focusables = () => [...(panel.current?.querySelectorAll<HTMLElement>("button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])") ?? [])].filter((el) => !el.hasAttribute("disabled") && el.offsetParent !== null);
    const frame = requestAnimationFrame(() => (panel.current?.querySelector<HTMLElement>("[data-autofocus]") ?? focusables()[0])?.focus({ preventScroll: true }));
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.stopPropagation();
        close.current();
      } else if (e.key === "Tab") {
        const list = focusables();
        if (list.length === 0) return;
        const first = list[0];
        const last = list[list.length - 1];
        if (e.shiftKey && document.activeElement === first) {
          e.preventDefault();
          last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
          e.preventDefault();
          first.focus();
        }
      }
    };
    document.addEventListener("keydown", onKey);
    return () => {
      cancelAnimationFrame(frame);
      document.removeEventListener("keydown", onKey);
      if (scroller) scroller.style.overflow = overflow;
      previous?.focus?.({ preventScroll: true });
    };
  }, [open]);

  if (!open) return null;

  // Свайп вниз по шапке (телефон): больше 80 px — закрыть, меньше — вернуть на место.
  const touch = {
    onTouchStart: (e: React.TouchEvent) => (start.current = e.touches[0].clientY),
    onTouchMove: (e: React.TouchEvent) => start.current != null && setDrag(Math.max(0, e.touches[0].clientY - start.current)),
    onTouchEnd: () => {
      if (drag > 80) onClose();
      setDrag(0);
      start.current = null;
    },
  };

  const sideCls =
    side === "left"
      ? `md:inset-y-0 md:left-0 md:right-auto md:top-0 md:rounded-none md:rounded-r-3xl md:border-r md:border-t-0 ${width}`
      : `md:inset-y-0 md:left-auto md:right-0 md:top-0 md:rounded-none md:rounded-l-3xl md:border-l md:border-t-0 ${width}`;

  return (
    <div className="fixed inset-0 z-50">
      <div className="absolute inset-0 bg-black/70 backdrop-blur-[2px]" onClick={onClose} aria-hidden />
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-label={t(label)}
        style={drag ? { transform: `translateY(${drag}px)` } : undefined}
        className={`absolute inset-x-0 bottom-0 top-[84px] flex flex-col overflow-hidden rounded-t-3xl border-t border-line-strong bg-surface shadow-2xl ${sideCls}`}
      >
        <div {...touch} className="shrink-0 border-b border-line px-4 pb-3 pt-2 md:px-5 md:pt-4">
          <div className="mx-auto mb-2 h-1 w-10 rounded-full bg-line-strong md:hidden" aria-hidden />
          <div className="flex items-center gap-2.5">
            <div className="min-w-0 flex-1">{header}</div>
            <button type="button" onClick={onClose} aria-label={t("Закрыть")} className="grid size-8 shrink-0 place-items-center rounded-full bg-muted text-ink-2 hover:text-ink">
              <X className="size-4" />
            </button>
          </div>
        </div>
        <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-4 md:px-5">{children}</div>
        {footer && <div className="shrink-0 border-t border-line px-4 pb-[calc(16px+env(safe-area-inset-bottom))] pt-3 md:px-5 md:pb-4">{footer}</div>}
      </div>
    </div>
  );
}
