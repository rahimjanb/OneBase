import { Suspense } from "react";
import { Bell, ChevronDown } from "lucide-react";
import { initials, type Me } from "@/lib/users";
import { SectionNav } from "./SectionNav";
import { Logo } from "./Sidebar";
import { ThemeToggle } from "./ThemeToggle";

/**
 * Верхняя панель — одна на все страницы (живёт в layout, поэтому не перерисовывается и не пропадает при переходах).
 * На телефоне это тёмная шапка приложения: логотип, тема, уведомления, аватар; кнопки разделов продаж — второй строкой.
 * Отступ сверху — под вырез экрана и строку состояния в установленном приложении (safe area).
 * На больших экранах — светлая, прилипает к верху, без нижней линии: её даёт заголовок страницы.
 */
export function Topbar({ me }: { me: Me | null }) {
  return (
    <div className="z-30 bg-sidebar pt-[env(safe-area-inset-top)] text-sidebar-text lg:sticky lg:top-0 lg:bg-page/90 lg:pt-0 lg:text-ink-2 lg:backdrop-blur">
      <div className="mx-auto flex max-w-[1800px] flex-wrap items-center gap-1 px-4 sm:px-6">
        <div className="flex h-14 items-center lg:hidden">
          <Logo />
        </div>
        <Suspense fallback={null}>
          <SectionNav />
        </Suspense>
        <div className="ml-auto flex h-14 items-center gap-1 lg:h-16">
          <ThemeToggle />
          <button
            type="button"
            aria-label="Уведомления"
            className="relative grid size-10 place-items-center rounded-full text-current transition-colors hover:bg-white/10 hover:text-white lg:size-9 lg:hover:bg-muted lg:hover:text-ink"
          >
            <Bell className="size-4" strokeWidth={1.75} />
            <span className="absolute right-2.5 top-2.5 size-1.5 rounded-full bg-accent ring-2 ring-sidebar lg:right-2 lg:top-2 lg:ring-page" />
          </button>
          {me && (
            <details className="relative ml-1">
              <summary className="flex cursor-pointer list-none items-center gap-3 rounded-lg p-1 text-left transition-colors hover:bg-white/10 lg:hover:bg-muted [&::-webkit-details-marker]:hidden">
                <span className="grid size-9 place-items-center rounded-full bg-white/10 text-xs font-semibold text-white lg:bg-muted lg:text-ink-2">{initials(me)}</span>
                <span className="hidden lg:block">
                  <span className="block max-w-[220px] truncate text-sm font-semibold text-ink">{me.name || me.login}</span>
                  <span className="block max-w-[220px] truncate text-xs text-ink-3">{me.roles[0] ?? me.position ?? me.login}</span>
                </span>
                <ChevronDown className="mr-1 hidden size-4 text-ink-3 lg:block" />
              </summary>
              <div className="absolute right-0 z-40 mt-2 w-56 rounded-lg border border-line bg-surface p-1.5 text-ink shadow-lg">
                <div className="px-2.5 py-2 text-xs text-ink-3">
                  <span className="block truncate text-ink-2">{me.name || me.login}</span>
                  <span className="block truncate">{me.position ?? me.roles[0] ?? me.login}</span>
                </div>
                {/* Обычная ссылка: /logout при загрузке удаляет сессию, заранее его загружать нельзя. */}
                <a href="/logout" className="block rounded-md px-2.5 py-2 text-sm text-ink hover:bg-muted">
                  Выйти
                </a>
              </div>
            </details>
          )}
        </div>
      </div>
    </div>
  );
}
