"use client";

import { useEffect } from "react";
import { X } from "lucide-react";

/**
 * Нижняя шторка на телефоне и диалог по центру на большом экране. Закрывается крестиком, Esc и тапом по фону.
 * Отступ снизу — под жест «домой» (safe area).
 */
export function Sheet({ open, onClose, title, children, footer }: { open: boolean; onClose: () => void; title: string; children: React.ReactNode; footer?: React.ReactNode }) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = overflow;
    };
  }, [open, onClose]);

  if (!open) return null;
  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 sm:items-center sm:p-4" onClick={onClose} role="dialog" aria-modal="true" aria-label={title}>
      <div
        className="field-sheet flex max-h-[92dvh] w-full flex-col rounded-t-2xl bg-surface shadow-xl sm:max-w-lg sm:rounded-2xl"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between gap-3 border-b border-line px-4 py-3">
          <div className="mx-auto h-1 w-10 rounded-full bg-line sm:hidden" aria-hidden />
        </div>
        <div className="-mt-3 flex items-center justify-between gap-3 px-4 pb-2">
          <h2 className="text-base font-semibold text-ink">{title}</h2>
          <button type="button" onClick={onClose} aria-label="Закрыть" className="grid size-10 place-items-center rounded-full text-ink-3 hover:bg-muted">
            <X className="size-5" />
          </button>
        </div>
        <div className="flex-1 overflow-y-auto px-4 pb-4">{children}</div>
        {footer && <div className="border-t border-line px-4 pb-[calc(0.75rem+env(safe-area-inset-bottom))] pt-3">{footer}</div>}
      </div>
    </div>
  );
}
