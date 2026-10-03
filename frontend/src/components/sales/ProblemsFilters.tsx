"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import type { FlagKind } from "@/lib/sales/types";

const criteria: { key: FlagKind; label: string }[] = [
  { key: "LowConversion", label: "Низкая конверсия" },
  { key: "VisitsNoSales", label: "Ходит, но не продаёт" },
  { key: "SmallCheck", label: "Мелкий чек" },
  { key: "NarrowAssortment", label: "Узкий ассортимент" },
  { key: "TempoDrop", label: "Падение темпа" },
  { key: "DataMismatch", label: "Данные не сходятся" },
  { key: "LowData", label: "Мало данных (< 20 визитов)" },
];

const control = "h-9 rounded-lg border border-line bg-surface px-3 text-sm text-ink shadow-sm max-lg:h-11";

/** Фильтры «Проблемных агентов»: РМ, критерий, вакансии — применяются сразу, без кнопки. */
export function ProblemsFilters({ directions, vacancies, found }: { directions: { id: string; name: string }[]; vacancies: number; found: number }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();

  const set = (key: string, value: string | null) => {
    const next = new URLSearchParams(params);
    if (value) next.set(key, value);
    else next.delete(key);
    router.push(`${pathname}?${next}`);
  };

  // Неуправляемые поля: выбранное значение остаётся сразу, пока грузится страница; key сбрасывает их, когда адрес сменился.
  const key = params.toString();

  return (
    <div className="flex flex-wrap items-center gap-2">
      {directions.length > 0 && (
        <select key={`d${key}`} aria-label="РМ" defaultValue={params.get("direction") ?? ""} onChange={(e) => set("direction", e.target.value || null)} className={control}>
          <option value="">Все РМ</option>
          {directions.map((d) => (
            <option key={d.id} value={d.id}>
              {d.name}
            </option>
          ))}
        </select>
      )}
      <select key={`c${key}`} aria-label="Критерий" defaultValue={params.get("criterion") ?? ""} onChange={(e) => set("criterion", e.target.value || null)} className={control}>
        <option value="">Любой критерий</option>
        {criteria.map((c) => (
          <option key={c.key} value={c.key}>
            {c.label}
          </option>
        ))}
      </select>
      <label className={`${control} inline-flex cursor-pointer items-center gap-2`}>
        <input
          key={`v${key}`}
          type="checkbox"
          defaultChecked={params.get("vacancies") === "true"}
          onChange={(e) => set("vacancies", e.target.checked ? "true" : null)}
          className="size-4 accent-[var(--color-accent)]"
        />
        Показывать вакансии ({vacancies})
      </label>
      <span className="rounded-full border border-line bg-surface px-3 py-1 text-xs tabular-nums text-ink-2 max-lg:py-2">{found} ТП с замечаниями</span>
    </div>
  );
}
