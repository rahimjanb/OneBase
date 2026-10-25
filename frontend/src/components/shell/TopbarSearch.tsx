"use client";

import { useEffect, useRef } from "react";
import { Search } from "lucide-react";
import { usePathname } from "next/navigation";
import { useT } from "@/i18n/client";
import { sectionOf } from "./sections";
import { isActive, settings, workspace } from "./Sidebar";

/**
 * Поиск в шапке (ТЗ dashboard): 420×38, подсказка «⌘K»; Ctrl/⌘+K ставит курсор в поле, Esc убирает. Сам поиск по данным — следующий шаг.
 * В «Продажах», «HR» и «Задачах» шапку занимают кнопки разделов — поиска там нет (и SectionNav меряет тесноту по блоку справа, как раньше).
 */
export function TopbarSearch() {
  const t = useT();
  const pathname = usePathname();
  const input = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        input.current?.focus();
        input.current?.select();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  if (sectionOf(pathname)) return null;
  return (
    <div className="hidden min-w-0 flex-1 justify-center lg:flex">
      <label className="flex h-[38px] w-full min-w-[200px] max-w-[420px] items-center gap-2 rounded-[10px] border border-line bg-surface px-3 transition-colors duration-150 focus-within:border-accent">
        <Search className="size-4 shrink-0 text-ink-4" strokeWidth={1.75} />
        <input
          ref={input}
          type="search"
          placeholder={t("Поиск по задачам, отделам, сотрудникам…")}
          aria-label={t("Поиск")}
          onKeyDown={(e) => e.key === "Escape" && e.currentTarget.blur()}
          className="min-w-0 flex-1 bg-transparent text-[13px] text-ink outline-none placeholder:text-ink-4 [&::-webkit-search-cancel-button]:hidden"
        />
        <kbd className="rounded-[5px] border border-line-strong px-1.5 py-0.5 font-sans text-[11px] leading-4 text-ink-3">
          ⌘K
        </kbd>
      </label>
    </div>
  );
}

/** Крошки шапки: раздел, которому принадлежит адрес (компании в OneBase пока нет — только раздел). */
export function Breadcrumbs() {
  const t = useT();
  const pathname = usePathname();
  const item =
    workspace.find((i) => isActive(pathname, i)) ??
    (isActive(pathname, settings) ? settings : null);
  return (
    <nav
      aria-label={t("Навигация")}
      className="flex items-center gap-1.5 whitespace-nowrap text-[13px]"
    >
      <span className="font-medium text-ink">{t(item?.label ?? "Обзор")}</span>
    </nav>
  );
}
