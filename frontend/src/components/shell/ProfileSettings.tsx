"use client";

import { useEffect, useState } from "react";
import { Languages, Moon, Sun } from "lucide-react";
import { LANGS } from "@/i18n/core";
import { setLang, useLang, useT } from "@/i18n/client";
import { applyTheme, currentTheme } from "./ThemeToggle";

const segment = "h-9 flex-1 rounded-[9px] text-[13px] font-semibold transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent";
const on = "bg-surface-solid text-ink shadow-sm";
const off = "text-ink-3";

/** Тема и язык в «Профиле» на телефоне (из шапки телефона они убраны): две строки с переключателями. */
export function ProfileSettings() {
  const t = useT();
  const lang = useLang();
  const [theme, setTheme] = useState<"light" | "dark" | null>(null);
  useEffect(() => setTheme(currentTheme()), []);

  const pick = (next: "light" | "dark") => {
    applyTheme(next);
    setTheme(next);
  };

  return (
    <div className="mt-3 flex flex-col gap-3 rounded-xl border border-line p-3 text-ink">
      <div className="flex items-center gap-3">
        <span className="flex min-w-0 flex-1 items-center gap-2 text-sm">
          {theme === "light" ? <Sun aria-hidden className="size-4 text-ink-3" /> : <Moon aria-hidden className="size-4 text-ink-3" />}
          {t("Тема")}
        </span>
        <div role="radiogroup" aria-label={t("Тема")} className="flex w-[180px] gap-0.5 rounded-[11px] bg-muted p-[3px]">
          <button type="button" role="radio" aria-checked={theme === "dark"} onClick={() => pick("dark")} className={`${segment} ${theme === "dark" ? on : off}`}>
            {t("Тёмная")}
          </button>
          <button type="button" role="radio" aria-checked={theme === "light"} onClick={() => pick("light")} className={`${segment} ${theme === "light" ? on : off}`}>
            {t("Светлая")}
          </button>
        </div>
      </div>
      <div className="flex items-center gap-3">
        <span className="flex min-w-0 flex-1 items-center gap-2 text-sm">
          <Languages aria-hidden className="size-4 text-ink-3" />
          {t("Язык")}
        </span>
        <div role="radiogroup" aria-label={t("Язык интерфейса")} className="flex w-[180px] gap-0.5 rounded-[11px] bg-muted p-[3px]">
          {LANGS.map((l) => (
            <button
              key={l.code}
              type="button"
              role="radio"
              aria-checked={l.code === lang}
              aria-label={l.label}
              onClick={() => l.code !== lang && setLang(l.code)}
              className={`${segment} ${l.code === lang ? on : off}`}
            >
              {l.short}
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
