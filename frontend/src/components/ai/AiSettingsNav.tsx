"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { aiSettingsTabs } from "@/lib/ai";

/** Вкладки «Настройки → AI». */
export function AiSettingsNav() {
  const pathname = usePathname();
  return (
    <nav aria-label="Разделы настроек AI" className="-mx-4 mb-6 overflow-x-auto border-b border-line px-4 sm:mx-0 sm:px-0">
      <div className="flex min-w-max gap-1">
        {aiSettingsTabs.map((tab) => {
          const active = tab.href === "/settings/ai" ? pathname === tab.href : pathname.startsWith(tab.href);
          return (
            <Link
              key={tab.href}
              href={tab.href}
              aria-current={active ? "page" : undefined}
              className={`-mb-px border-b-2 px-3 py-2.5 text-sm font-medium ${active ? "border-accent text-ink" : "border-transparent text-ink-2 hover:text-ink"}`}
            >
              {tab.label}
            </Link>
          );
        })}
      </div>
    </nav>
  );
}
