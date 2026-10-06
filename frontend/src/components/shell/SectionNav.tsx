"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { useState } from "react";
import { activeSalesTab, salesTabs, type SalesTab } from "@/components/sales/tabs";
import { periodQuery } from "@/lib/sales/query";

/**
 * Кнопки разделов в верхней панели — только внутри «Продаж»; в других разделах панель без них.
 * На телефоне — отдельной строкой под логотипом, прокручивается по горизонтали без полосы прокрутки.
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

  // Период и план переносятся между разделами — тем же правилом, что ссылки внутри страниц (plan — только factory).
  const query = periodQuery(Object.fromEntries(params));

  return (
    <nav className="no-scrollbar order-last flex basis-full gap-2 overflow-x-auto pb-3 lg:order-none lg:min-w-0 lg:shrink lg:basis-auto lg:pb-0" aria-label="Разделы продаж">
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
            className={`inline-flex h-9 shrink-0 items-center gap-1.5 whitespace-nowrap rounded-lg border px-2.5 text-sm font-medium shadow-sm transition-colors max-lg:h-10 max-lg:px-3 ${
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
