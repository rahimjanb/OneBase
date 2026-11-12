"use client";

import { usePathname } from "next/navigation";

/**
 * Обёртка глобальной шапки: на телефоне в AI-консультанте, «Уведомлениях», канбане и карточке задачи шапки нет — вместо неё своя шапка
 * экрана (ТЗ ai-consultant, notifications, tasks-kanban-detail, все страницы «Подбора», «Технолог», список техкарт, симулятор Аналитика). Путь известен и при отрисовке на сервере, поэтому шапка не мелькает.
 * На компьютере шапка всегда на месте.
 */
export function TopbarFrame({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const own =
    ["/consultant", "/analytik/chat", "/notifications", "/tasks/board", "/tasks/task", "/hr/recruitment", "/production/technologist"].some((p) => pathname === p || pathname.startsWith(`${p}/`)) ||
    // Список техкарт — своя шапка; карточка и форма техкарты — с общей. Симулятор Аналитика — своя шапка (сравнение — с общей).
    pathname === "/production/recipes" ||
    pathname === "/analytik/simulator";
  return <div className={own ? "contents max-lg:hidden" : "contents"}>{children}</div>;
}
