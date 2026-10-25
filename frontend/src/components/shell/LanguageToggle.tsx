"use client";

import { useEffect, useRef, useState } from "react";
import { Check, Languages } from "lucide-react";
import { LANGS } from "@/i18n/core";
import { setLang, useLang, useT } from "@/i18n/client";

/**
 * Кнопка языка рядом с уведомлениями: код текущего языка (RU / UZ), по нажатию — список «Русский» и «O'zbekcha».
 * Выбор хранится в cookie на год и действует на всех страницах OneBase и Sales Base.
 */
export function LanguageToggle({ align = "right" }: { align?: "left" | "right" }) {
  const lang = useLang();
  const t = useT();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const current = LANGS.find((l) => l.code === lang) ?? LANGS[0];

  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => {
      if (!ref.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={t("Язык интерфейса")}
        className="flex h-10 items-center gap-1 rounded-full px-2.5 text-xs font-semibold text-current transition-colors duration-150 hover:bg-white/10 hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent lg:h-9 lg:rounded-[9px] lg:text-[12.5px] lg:text-ink-3 lg:hover:bg-muted lg:hover:text-ink"
      >
        <Languages className="size-4" strokeWidth={1.75} />
        {current.short}
      </button>
      {open && (
        <div
          role="menu"
          className={`absolute z-40 mt-2 w-44 rounded-lg border border-line bg-surface-solid p-1.5 text-ink shadow-lg ${align === "right" ? "right-0" : "left-0"}`}
        >
          {LANGS.map((l) => (
            <button
              key={l.code}
              type="button"
              role="menuitemradio"
              aria-checked={l.code === lang}
              onClick={() => (l.code === lang ? setOpen(false) : setLang(l.code))}
              className="flex w-full items-center gap-2 rounded-md px-2.5 py-2 text-left text-sm hover:bg-muted"
            >
              <span className="w-6 text-xs font-semibold text-ink-3">{l.short}</span>
              <span className="flex-1">{l.label}</span>
              {l.code === lang && <Check className="size-4 text-accent" />}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
