"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { useState } from "react";
import { activeSalesTab, salesTabs, type SalesTab } from "@/components/sales/tabs";

/**
 * Кнопки разделов в верхней панели — только внутри «Продаж»; в других разделах панель без них.
 * Нажатая кнопка подсвечивается сразу, а не после загрузки страницы: иначе ещё секунду горит прежний раздел.
 */
export function SectionNav() {
  const pathname = usePathname();
  const params = useSearchParams();
  // Нажатая кнопка — пока адрес не сменился; после перехода подсветку определяет адрес.
  const [pending, setPending] = useState<{ tab: SalesTab; from: string } | null>(null);

  const current = activeSalesTab(pathname);
  if (!pathname.startsWith("/sales")) return null;
  const active = pending && pending.from === pathname ? pending.tab : current;

  // Период и план переносятся между разделами.
  const period = new URLSearchParams();
  for (const key of ["year", "month", "plan"]) {
    const value = params.get(key);
    if (value) period.set(key, value);
  }
  const query = period.toString();

  return (
    <nav className="flex min-w-0 gap-2 overflow-x-auto" aria-label="Разделы продаж">
      {salesTabs.map((t) => {
        const on = active === t.key;
        return (
          <Link
            key={t.key}
            href={query ? `${t.href}?${query}` : t.href}
            aria-current={current === t.key ? "page" : undefined}
            onClick={(e) => {
              if (e.button === 0 && !e.metaKey && !e.ctrlKey && !e.shiftKey && !e.altKey) setPending({ tab: t.key, from: pathname });
            }}
            className={`inline-flex h-9 shrink-0 items-center gap-1.5 whitespace-nowrap rounded-lg border px-2.5 text-sm font-medium shadow-sm transition-colors ${
              on ? "border-accent bg-accent text-white hover:bg-accent-strong" : "border-line bg-surface text-ink hover:border-accent/40 hover:bg-muted"
            }`}
          >
            <t.icon className="hidden size-4 2xl:block" />
            {t.label}
          </Link>
        );
      })}
    </nav>
  );
}
