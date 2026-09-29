import { Bell, ChevronDown } from "lucide-react";
import { currentUser } from "@/lib/demo-data";
import { ThemeToggle } from "./ThemeToggle";

/**
 * Верхняя панель — одна на все страницы (живёт в layout, поэтому не перерисовывается и не пропадает при переходах).
 * На больших экранах прилипает к верху при прокрутке.
 */
export function Topbar() {
  return (
    <div className="z-30 border-b border-line bg-page/90 backdrop-blur lg:sticky lg:top-0">
      <div className="mx-auto flex h-16 max-w-[1240px] items-center justify-end gap-3 px-4 sm:gap-4 sm:px-8">
        <ThemeToggle />
        <button
          type="button"
          aria-label="Уведомления"
          className="relative grid size-9 place-items-center rounded-full border border-line bg-surface text-ink-2 hover:text-ink"
        >
          <Bell className="size-4" strokeWidth={1.75} />
          <span className="absolute right-1.5 top-1.5 size-2 rounded-full bg-accent ring-2 ring-surface" />
        </button>
        <button type="button" className="flex items-center gap-3 text-left">
          <span className="grid size-10 place-items-center rounded-full bg-accent-soft text-sm font-semibold text-accent-strong">
            {currentUser.initials}
          </span>
          <span className="hidden sm:block">
            <span className="block text-sm font-semibold text-ink">{currentUser.login}</span>
            <span className="block text-xs text-ink-3">{currentUser.role}</span>
          </span>
          <ChevronDown className="ml-3 hidden size-4 text-ink-3 sm:block" />
        </button>
      </div>
    </div>
  );
}
