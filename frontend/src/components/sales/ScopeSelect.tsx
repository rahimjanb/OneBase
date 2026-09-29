"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";

/**
 * Выбор области страницы (республика / РМ / регион) — меняет параметры адреса, сервер пересчитывает страницу.
 * Значение «kind:id»: «region:…», «direction:…»; пустое — вся республика.
 */
export function ScopeSelect({
  options,
  label = "Область",
  keys = ["region", "direction"],
}: {
  options: { value: string; label: string; group?: string }[];
  label?: string;
  keys?: string[];
}) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const current = keys.map((k) => (params.get(k) ? `${k}:${params.get(k)}` : null)).find(Boolean) ?? "";
  const groups = [...new Set(options.map((o) => o.group ?? ""))];

  return (
    <label className="inline-flex items-center gap-2 text-sm text-ink-2">
      {label}
      <select
        value={current}
        onChange={(e) => {
          const next = new URLSearchParams(params);
          keys.forEach((k) => next.delete(k));
          const [kind, ...rest] = e.target.value.split(":");
          if (kind) next.set(kind, rest.join(":"));
          router.push(`${pathname}?${next}`);
        }}
        className="h-9 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink"
      >
        {groups.map((g) =>
          g ? (
            <optgroup key={g} label={g}>
              {options.filter((o) => o.group === g).map((o) => (
                <option key={o.value} value={o.value}>
                  {o.label}
                </option>
              ))}
            </optgroup>
          ) : (
            options.filter((o) => !o.group).map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))
          ),
        )}
      </select>
    </label>
  );
}
