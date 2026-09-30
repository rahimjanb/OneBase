import { Suspense } from "react";
import { Bell, ChevronDown } from "lucide-react";
import { initials, type Me } from "@/lib/users";
import { SectionNav } from "./SectionNav";
import { ThemeToggle } from "./ThemeToggle";

/**
 * Верхняя панель — одна на все страницы (живёт в layout, поэтому не перерисовывается и не пропадает при переходах).
 * Слева — кнопки разделов открытого раздела (сейчас — только «Продажи»). На больших экранах прилипает к верху.
 */
export function Topbar({ me }: { me: Me | null }) {
  return (
    <div className="z-30 border-b border-line bg-page/90 backdrop-blur lg:sticky lg:top-0">
      <div className="mx-auto flex h-16 max-w-[1800px] items-center gap-3 px-4 sm:gap-4 sm:px-6">
        <div className="min-w-0 flex-1">
          <Suspense fallback={null}>
            <SectionNav />
          </Suspense>
        </div>
        <ThemeToggle />
        <button
          type="button"
          aria-label="Уведомления"
          className="relative grid size-9 place-items-center rounded-full border border-line bg-surface text-ink-2 hover:text-ink"
        >
          <Bell className="size-4" strokeWidth={1.75} />
          <span className="absolute right-1.5 top-1.5 size-2 rounded-full bg-accent ring-2 ring-surface" />
        </button>
        {me && (
          <details className="relative">
            <summary className="flex cursor-pointer list-none items-center gap-3 text-left [&::-webkit-details-marker]:hidden">
              <span className="grid size-10 place-items-center rounded-full bg-accent-soft text-sm font-semibold text-accent-strong">{initials(me)}</span>
              <span className="hidden sm:block">
                <span className="block max-w-[220px] truncate text-sm font-semibold text-ink">{me.name || me.login}</span>
                <span className="block max-w-[220px] truncate text-xs text-ink-3">{me.roles[0] ?? me.position ?? me.login}</span>
              </span>
              <ChevronDown className="ml-3 hidden size-4 text-ink-3 sm:block" />
            </summary>
            <div className="absolute right-0 z-40 mt-2 w-56 rounded-lg border border-line bg-surface p-1.5 shadow-lg">
              <div className="px-2.5 py-2 text-xs text-ink-3">
                <span className="block truncate text-ink-2">{me.login}</span>
                {me.position && <span className="block truncate">{me.position}</span>}
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
  );
}
