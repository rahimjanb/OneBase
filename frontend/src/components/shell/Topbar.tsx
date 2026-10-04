import { Suspense } from "react";
import { ChevronDown } from "lucide-react";
import { initials, type Me } from "@/lib/users";
import { LogoutLink } from "./AppBridge";
import { NotificationBell } from "./NotificationBell";
import { SectionNav } from "./SectionNav";
import { Logo } from "./Sidebar";
import { ThemeToggle } from "./ThemeToggle";

/**
 * Верхняя панель — одна на все страницы (живёт в layout, поэтому не перерисовывается и не пропадает при переходах).
 * На телефоне это тёмная шапка приложения: логотип, тема, уведомления, аватар; кнопки разделов продаж — второй строкой.
 * Отступ сверху — под вырез экрана и строку состояния в установленном приложении (safe area).
 * На больших экранах — светлая, без нижней линии: её даёт заголовок страницы. Панель стоит над областью прокрутки и не двигается.
 * Слой выше нижней панели (z-40 против z-30): уведомления и меню аватара, открытые вниз, не прячутся под неё.
 */
export function Topbar({ me }: { me: Me | null }) {
  return (
    <div className="relative z-40 shrink-0 bg-sidebar pt-[env(safe-area-inset-top)] text-sidebar-text lg:bg-page lg:pt-0 lg:text-ink-2">
      <div className="mx-auto flex max-w-[1800px] flex-wrap items-center gap-1 px-4 sm:px-6">
        <div className="flex h-14 items-center lg:hidden">
          <Logo />
        </div>
        <Suspense fallback={null}>
          <SectionNav />
        </Suspense>
        <div className="ml-auto flex h-14 items-center gap-1 lg:h-16">
          <ThemeToggle />
          {me && <NotificationBell />}
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
                <LogoutLink href="/logout" className="block rounded-md px-2.5 py-2 text-sm text-ink hover:bg-muted">
                  Выйти
                </LogoutLink>
              </div>
            </details>
          )}
        </div>
      </div>
    </div>
  );
}
