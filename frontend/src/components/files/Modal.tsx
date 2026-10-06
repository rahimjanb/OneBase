"use client";

import { useEffect, useRef } from "react";
import { X } from "lucide-react";

/** Модальное окно раздела «Файлы»: Escape и клик по фону закрывают, фокус — на окне. */
export function Modal({
  title,
  onClose,
  children,
  wide = false,
}: {
  title: string;
  onClose: () => void;
  children: React.ReactNode;
  wide?: boolean;
}) {
  const panel = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    panel.current?.focus();
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 p-4 sm:p-8" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
      <div
        ref={panel}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className={`w-full ${wide ? "max-w-5xl" : "max-w-lg"} rounded-xl border border-line bg-surface shadow-xl outline-none`}
      >
        <div className="flex items-center gap-3 border-b border-line px-5 py-4">
          <h2 className="min-w-0 flex-1 truncate text-base font-semibold">{title}</h2>
          <button type="button" onClick={onClose} aria-label="Закрыть" className="rounded-md p-1 text-ink-3 hover:bg-muted hover:text-ink">
            <X className="size-4" />
          </button>
        </div>
        <div className="px-5 py-4">{children}</div>
      </div>
    </div>
  );
}
