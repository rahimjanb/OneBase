"use client";

import { usePathname } from "next/navigation";

/**
 * Обёртка глобальной шапки: на телефоне в AI-консультанте шапки нет — вместо неё шапка чата (ТЗ ai-consultant). Путь известен и при
 * отрисовке на сервере, поэтому шапка не мелькает. На компьютере шапка всегда на месте.
 */
export function TopbarFrame({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const consultant = pathname === "/consultant" || pathname.startsWith("/consultant/");
  return <div className={consultant ? "contents max-lg:hidden" : "contents"}>{children}</div>;
}
