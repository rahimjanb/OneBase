"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { monthLabel } from "@/lib/sales/format";
import type { SalesMonth } from "@/lib/sales/types";

/** Период (месяц). Меняет параметры адреса — сервер пересчитывает страницу. План — только из Linko, переключателя нет. */
export function SalesToolbar({
  months,
  year,
  month,
}: {
  months: SalesMonth[];
  year: number;
  month: number;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();

  const go = (changes: Record<string, string | null>) => {
    const next = new URLSearchParams(params);
    for (const [key, value] of Object.entries(changes)) {
      if (value == null) next.delete(key);
      else next.set(key, value);
    }
    router.push(`${pathname}?${next}`);
  };

  const options = months.some((m) => m.year === year && m.month === month) ? months : [{ year, month }, ...months];

  return (
    <div className="flex flex-wrap items-center gap-2 max-lg:w-full">
      <select
        aria-label="Период"
        value={`${year}-${month}`}
        onChange={(e) => {
          const [y, m] = e.target.value.split("-");
          go({ year: y, month: m, from: null, to: null });
        }}
        className="h-9 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink max-lg:h-11 max-lg:flex-1"
      >
        {options.map((m) => (
          <option key={`${m.year}-${m.month}`} value={`${m.year}-${m.month}`}>
            {monthLabel(m.year, m.month)}
          </option>
        ))}
      </select>
    </div>
  );
}
