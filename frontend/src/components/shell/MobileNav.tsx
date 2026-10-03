"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { isActive, settings, workspace } from "./Sidebar";

/**
 * Нижняя панель навигации на телефоне — главные разделы, как в нативном приложении.
 * Разделы «скоро» не показываем; «Настройки» — только при правах. Отступ снизу — под жест «домой» (safe area).
 */
export function MobileNav({ canOpenSettings }: { canOpenSettings: boolean }) {
  const pathname = usePathname();
  const items = [...workspace.filter((item) => !item.soon), ...(canOpenSettings ? [settings] : [])];

  return (
    <nav aria-label="Основные разделы" className="fixed inset-x-0 bottom-0 z-30 border-t border-line bg-surface/95 pb-[env(safe-area-inset-bottom)] backdrop-blur lg:hidden">
      <ul className="flex">
        {items.map((item) => {
          const active = isActive(pathname, item);
          return (
            <li key={item.href} className="min-w-0 flex-1">
              <Link
                href={item.href}
                aria-current={active ? "page" : undefined}
                className={`flex h-14 flex-col items-center justify-center gap-1 text-[11px] font-medium active:bg-muted ${active ? "text-accent-strong" : "text-ink-3"}`}
              >
                <item.icon className="size-5" strokeWidth={active ? 2.25 : 1.75} />
                <span className="max-w-full truncate px-1">{item.short ?? item.label}</span>
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
