"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { monthLabel } from "@/lib/sales/format";
import type { SalesMonth } from "@/lib/sales/types";

/** Период (месяц) и план (РОП / Завод). Меняют параметры адреса — сервер пересчитывает страницу. */
export function SalesToolbar({
  months,
  year,
  month,
  plan,
  showPlan = true,
}: {
  months: SalesMonth[];
  year: number;
  month: number;
  plan: string;
  /** План РОП / «Завод» меняет только вторичку — на первичке и в ассортименте переключатель не нужен. */
  showPlan?: boolean;
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
    <div className="flex flex-wrap items-center gap-2">
      <select
        aria-label="Период"
        value={`${year}-${month}`}
        onChange={(e) => {
          const [y, m] = e.target.value.split("-");
          go({ year: y, month: m, from: null, to: null });
        }}
        className="h-9 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink"
      >
        {options.map((m) => (
          <option key={`${m.year}-${m.month}`} value={`${m.year}-${m.month}`}>
            {monthLabel(m.year, m.month)}
          </option>
        ))}
      </select>
      {showPlan && (
        <div className="flex overflow-hidden rounded-lg border border-line text-sm" role="group" aria-label="План">
          {[
            { key: "Rop", label: "План РОП" },
            { key: "Factory", label: "Завод" },
          ].map((p) => (
            <button
              key={p.key}
              type="button"
              onClick={() => go({ plan: p.key === "Rop" ? null : p.key })}
              className={`px-3 py-1.5 ${plan === p.key ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}
            >
              {p.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
