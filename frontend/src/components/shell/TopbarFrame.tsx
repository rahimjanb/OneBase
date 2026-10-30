"use client";

import { usePathname } from "next/navigation";

/**
 * Обёртка глобальной шапки: на телефоне в AI-консультанте и «Уведомлениях» шапки нет — вместо неё своя шапка экрана (ТЗ ai-consultant,
 * notifications). Путь известен и при
 * отрисовке на сервере, поэтому шапка не мелькает. На компьютере шапка всегда на месте.
 */
export function TopbarFrame({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const own = ["/consultant", "/notifications"].some((p) => pathname === p || pathname.startsWith(`${p}/`));
  return <div className={own ? "contents max-lg:hidden" : "contents"}>{children}</div>;
}
