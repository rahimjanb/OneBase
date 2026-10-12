"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useKeyboardOpen } from "./AppScroll";
import { isActive, settings, visibleWorkspace } from "./Sidebar";

/**
 * Нижняя панель навигации на телефоне — главные разделы, как в нативном приложении.
 * Разделы «скоро» не показываем; «Настройки» — только при правах. Отступ снизу — под жест «домой» (safe area).
 * Панель в потоке под областью прокрутки — не двигается при прокрутке; при открытой клавиатуре прячется.
 * Вид — плавающая скруглённая плашка (отступ 10px от краёв, радиус 24px), полупрозрачная, с размытием; активный раздел — светлый.
 */
export function MobileNav({ canOpenSettings, permissions }: { canOpenSettings: boolean; permissions: readonly string[] | null }) {
  const pathname = usePathname();
  const items = [...visibleWorkspace(permissions).filter((item) => !item.soon), ...(canOpenSettings ? [settings] : [])];
  const keyboardOpen = useKeyboardOpen();

  return (
    <nav
      aria-label="Основные разделы"
      className={`relative z-30 shrink-0 px-2.5 pb-[calc(env(safe-area-inset-bottom)+10px)] pt-1.5 lg:hidden ${keyboardOpen ? "hidden" : ""}`}
    >
      <ul className="flex rounded-3xl border border-line bg-surface-solid/75 px-1 shadow-[0_12px_40px_-12px_rgb(0_0_0/0.55)] backdrop-blur-xl">
        {items.map((item) => {
          const active = isActive(pathname, item);
          return (
            <li key={item.href} className="min-w-0 flex-1">
              <Link
                href={item.href}
                aria-current={active ? "page" : undefined}
                className={`flex h-14 flex-col items-center justify-center gap-1 rounded-2xl text-[10px] font-medium transition-colors active:bg-muted ${
                  active ? "text-ink" : "text-ink-3 hover:text-ink-2"
                }`}
              >
                <item.icon className="size-5" strokeWidth={active ? 2.25 : 1.75} />
                <span className="max-w-full truncate px-0.5">{item.short ?? item.label}</span>
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
