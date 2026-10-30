"use client";

import { usePathname } from "next/navigation";

/**
 * Обёртка глобальной шапки: на телефоне в AI-консультанте, «Уведомлениях», канбане и карточке задачи шапки нет — вместо неё своя шапка
 * экрана (ТЗ ai-consultant, notifications, tasks-kanban-detail). Путь известен и при отрисовке на сервере, поэтому шапка не мелькает.
 * На компьютере шапка всегда на месте.
 */
export function TopbarFrame({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const own = ["/consultant", "/notifications", "/tasks/board", "/tasks/task"].some((p) => pathname === p || pathname.startsWith(`${p}/`));
  return <div className={own ? "contents max-lg:hidden" : "contents"}>{children}</div>;
}
