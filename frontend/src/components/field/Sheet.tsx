"use client";

import { useEffect } from "react";
import { createPortal } from "react-dom";
import { X } from "lucide-react";

/**
 * Нижняя шторка на телефоне и диалог по центру на большом экране. Закрывается крестиком, Esc и тапом по фону.
 * Отступ снизу — под жест «домой» (safe area). Рисуется в корне оболочки приложения (портал): открытая из шапки
 * шторка иначе оказалась бы под нижней панелью; корень несёт тему продукта (data-product), цвета сохраняются.
 */
export function Sheet({ open, onClose, title, children, footer }: { open: boolean; onClose: () => void; title: string; children: React.ReactNode; footer?: React.ReactNode }) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    // Фон не прокручивается: в оболочке приложения прокручивается [data-app-scroll], на остальных страницах — документ.
    const target = document.querySelector<HTMLElement>("[data-app-scroll]") ?? document.body;
    const overflow = target.style.overflow;
    target.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      target.style.overflow = overflow;
    };
  }, [open, onClose]);

  if (!open) return null;
  const host = typeof document === "undefined" ? null : (document.querySelector<HTMLElement>("[data-app-shell]") ?? document.body);
  const sheet = (
    <div className="fixed inset-0 z-50 flex items-end justify-center overscroll-contain bg-black/40 sm:items-center sm:p-4" onClick={onClose} role="dialog" aria-modal="true" aria-label={title}>
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
        <div className="flex-1 overflow-y-auto overscroll-contain px-4 pb-4">{children}</div>
        {footer && <div className="border-t border-line px-4 pb-[calc(0.75rem+env(safe-area-inset-bottom))] pt-3">{footer}</div>}
      </div>
    </div>
  );
  return host ? createPortal(sheet, host) : sheet;
}
