"use client";

import { useEffect, useRef } from "react";
import { createPortal } from "react-dom";
import { X } from "lucide-react";
import { useT } from "@/i18n/client";

/**
 * Нижняя шторка на телефоне и диалог по центру на большом экране. Закрывается крестиком, Esc и тапом по фону.
 * Фон шторки — непрозрачный (surface-solid): в стеклянной теме OneBase сквозь полупрозрачный проступали страница и нижняя панель.
 * Открытая шторка забирает фокус (если в ней ещё ничего не в фокусе — поле с autoFocus остаётся), закрытая возвращает его туда,
 * откуда открыли: кнопка, открывшая шторку, не остаётся в фокусе под ней. Отступ снизу — под жест «домой» (safe area). Рисуется в корне оболочки приложения (портал): открытая из шапки
 * шторка иначе оказалась бы под нижней панелью; корень несёт тему продукта (data-product), цвета сохраняются.
 */
export function Sheet({
  open,
  onClose,
  title,
  subtitle,
  children,
  footer,
  size = "md",
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  /** Строка под заголовком (кого редактируем). */
  subtitle?: string;
  children: React.ReactNode;
  footer?: React.ReactNode;
  /** md — узкий диалог (по умолчанию), xl — широкий для форм в две колонки. */
  size?: "md" | "xl";
}) {
  const panel = useRef<HTMLDivElement>(null);
  const t = useT();

  useEffect(() => {
    if (!open) return;
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    if (panel.current && !panel.current.contains(document.activeElement)) panel.current.focus({ preventScroll: true });
    return () => {
      if (opener?.isConnected) opener.focus({ preventScroll: true });
    };
  }, [open]);

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
        ref={panel}
        tabIndex={-1}
        className={`field-sheet flex max-h-[92dvh] w-full flex-col rounded-t-2xl bg-surface-solid shadow-xl outline-none sm:rounded-2xl ${size === "xl" ? "sm:max-w-6xl" : "sm:max-w-lg"}`}
        onClick={(e) => e.stopPropagation()}
      >
        <div className="flex justify-center pt-2.5 sm:hidden" aria-hidden>
          <div className="h-1 w-10 rounded-full bg-line" />
        </div>
        <div className="flex items-center justify-between gap-3 border-b border-line py-1 pl-4 pr-2 sm:py-2">
          <div className="min-w-0">
            <h2 className="truncate text-base font-semibold text-ink">{title}</h2>
            {subtitle && <p className="truncate text-xs text-ink-3">{subtitle}</p>}
          </div>
          <button type="button" onClick={onClose} aria-label={t("Закрыть")} className="grid size-10 place-items-center rounded-full text-ink-3 hover:bg-muted">
            <X className="size-5" />
          </button>
        </div>
        <div className={`flex-1 overflow-y-auto overscroll-contain px-4 pt-3 ${footer ? "pb-4" : "pb-[calc(1rem+env(safe-area-inset-bottom))]"}`}>{children}</div>
        {footer && <div className="border-t border-line px-4 pb-[calc(0.75rem+env(safe-area-inset-bottom))] pt-3">{footer}</div>}
      </div>
    </div>
  );
  return host ? createPortal(sheet, host) : sheet;
}
