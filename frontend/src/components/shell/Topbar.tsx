import { Suspense } from "react";
import { ChevronDown } from "lucide-react";
import { LinkTelegramButton } from "@/components/telegram/LinkTelegramButton";
import { Tr } from "@/i18n/client";
import { initials, type Me } from "@/lib/users";
import { LogoutLink } from "./AppBridge";
import { LanguageToggle } from "./LanguageToggle";
import { MobileSearch } from "./MobileSearch";
import { NotificationBell } from "./NotificationBell";
import { SectionNav } from "./SectionNav";
import { Logo } from "./Sidebar";
import { ThemeToggle } from "./ThemeToggle";
import { Breadcrumbs, TopbarSearch } from "./TopbarSearch";

/**
 * Верхняя панель — одна на все страницы (живёт в layout, поэтому не перерисовывается и не пропадает при переходах).
 * На компьютере (ТЗ dashboard): высота 64, крошки «Компания › Раздел», кнопки разделов (в «Продажах», «HR» и «Задачах»), поиск по центру,
 * справа тема, язык, уведомления, разделитель и меню пользователя с градиентным аватаром.
 * На телефоне это тёмная шапка приложения (ТЗ 1base-mobile-home): логотип и справа три круглые кнопки 40×40 — поиск, уведомления, аватар;
 * тема и язык — в «Профиле» нижней панели; кнопки разделов — второй строкой.
 * Отступ сверху — под вырез экрана и строку состояния в установленном приложении (safe area). Панель стоит над областью прокрутки.
 * Слой выше нижней панели (z-40 против z-30): уведомления и меню аватара, открытые вниз, не прячутся под неё.
 */
export function Topbar({ me, permissions }: { me: Me | null; permissions: readonly string[] | null }) {
  return (
    <div data-topbar className="relative z-40 shrink-0 bg-sidebar pt-[env(safe-area-inset-top)] text-sidebar-text lg:border-b lg:border-line lg:bg-transparent lg:pt-0 lg:text-ink-2">
      {/* На компьютере — одна строка: не помещающиеся кнопки разделов прокручиваются, а не уезжают отдельной строкой над меню. */}
      <div className="flex flex-wrap items-center gap-1 pl-[20px] pr-[16px] sm:px-6 lg:h-16 lg:flex-nowrap lg:gap-5 lg:px-8">
        <div className="flex h-[52px] items-center lg:hidden">
          <Logo />
        </div>
        <div className="hidden shrink-0 lg:block">
          <Breadcrumbs />
        </div>
        <Suspense fallback={null}>
          <SectionNav permissions={me?.permissions ?? null} />
        </Suspense>
        <TopbarSearch />
        {/* Справа всегда у края: в разделах с кнопками в шапке (Продажи, HR, Задачи) поиска нет, и без ml-auto блок вставал сразу за кнопками. */}
        <div className="ml-auto flex h-[52px] shrink-0 items-center gap-[6px] lg:h-16 lg:gap-1.5">
          <MobileSearch permissions={permissions} />
          <span className="hidden lg:contents">
            <ThemeToggle />
            <LanguageToggle />
          </span>
          {me && <NotificationBell permissions={permissions} />}
          {me && (
            <>
              <span aria-hidden className="mx-1.5 hidden h-6 w-px bg-line lg:block" />
              <details className="relative lg:ml-0">
                <summary className="flex cursor-pointer list-none items-center gap-2.5 rounded-full text-left lg:rounded-lg lg:p-1 transition-colors duration-150 hover:bg-white/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent lg:rounded-[9px] lg:hover:bg-muted [&::-webkit-details-marker]:hidden">
                  <span className="grid size-[40px] place-items-center rounded-full border border-white/10 bg-white/10 text-xs font-semibold text-white lg:size-[34px] lg:border-0 lg:bg-gradient-to-br lg:from-brand-from lg:to-violet lg:font-bold">
                    {initials(me)}
                  </span>
                  {/* Тесно (кнопки разделов продаж не помещаются вместе с именем, data-crowded ставит SectionNav) — только аватар. */}
                  <span data-user-name className="hidden lg:block [[data-crowded]_&]:hidden">
                    <span className="block max-w-[220px] truncate text-[13px] font-semibold leading-tight text-ink">{me.name || me.login}</span>
                    <span className="block max-w-[220px] truncate text-[11.5px] leading-tight text-ink-4">{me.roles[0] ?? me.position ?? me.login}</span>
                  </span>
                  <ChevronDown className="mr-1 hidden size-[15px] text-ink-3 lg:block" />
                </summary>
                <div className="absolute right-0 z-40 mt-2 w-56 rounded-lg border border-line bg-surface-solid p-1.5 text-ink shadow-lg">
                  <div className="px-2.5 py-2 text-xs text-ink-3">
                    <span className="block truncate text-ink-2">{me.name || me.login}</span>
                    <span className="block truncate">{me.position ?? me.roles[0] ?? me.login}</span>
                  </div>
                  <LinkTelegramButton />
                  {/* Обычная ссылка: /logout при загрузке удаляет сессию, заранее его загружать нельзя. */}
                  <LogoutLink href="/logout" className="block rounded-md px-2.5 py-2 text-sm text-ink hover:bg-muted">
                    <Tr>Выйти</Tr>
                  </LogoutLink>
                </div>
              </details>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
