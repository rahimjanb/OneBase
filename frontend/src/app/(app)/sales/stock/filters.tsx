"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { num } from "@/lib/sales/format";
import type { StockView } from "@/lib/sales/types";

const control = "h-9 rounded-lg border border-line bg-surface px-3 text-sm text-ink shadow-sm max-lg:h-11";

/** Меняет параметры адреса страницы остатка — сервер пересчитывает строки, плитки и итоги (DOC-filters §4). */
function useSetParams() {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  return (changes: Record<string, string | null>, replace = false) => {
    const next = new URLSearchParams(params);
    for (const [key, value] of Object.entries(changes)) {
      if (value) next.set(key, value);
      else next.delete(key);
    }
    const query = next.toString();
    const url = query ? `${pathname}?${query}` : pathname;
    if (replace) router.replace(url);
    else router.push(url);
  };
}

/**
 * Поиск: код («002» или «2» — точный код) или название (подстрока). Применяется через 300 мс после ввода, без кнопки; текст живёт
 * в адресе (q), поэтому переживает смену охвата, категории, ТОПа и статуса (DOC-filters §12).
 */
export function StockSearch({ value }: { value: string }) {
  const set = useSetParams();
  const [text, setText] = useState(value);
  const pushed = useRef(value);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  // Адрес сменили снаружи («Сбросить фильтры») — поле подстраивается; собственный отложенный переход поле не трогает.
  useEffect(() => {
    if (value !== pushed.current) {
      pushed.current = value;
      setText(value);
    }
  }, [value]);
  return (
    <input
      type="search"
      value={text}
      placeholder="Поиск: код или название"
      aria-label="Поиск по товарам"
      autoComplete="off"
      onChange={(e) => {
        const v = e.target.value;
        setText(v);
        if (timer.current) clearTimeout(timer.current);
        timer.current = setTimeout(() => {
          pushed.current = v.trim();
          set({ q: v.trim() || null }, true);
        }, 300);
      }}
      className={`${control} min-w-[220px] max-lg:w-full`}
    />
  );
}

/** Охват: вся страна (склады дилеров), РМ, регион, завод, экспорт — параметр scope. */
export function StockScopeSelect({ data }: { data: StockView }) {
  const set = useSetParams();
  return (
    <label className="inline-flex items-center gap-2 text-sm text-ink-2 max-lg:w-full">
      Охват
      <select
        value={data.scope === "country" ? "" : data.scope}
        onChange={(e) => set({ scope: e.target.value || null, region: null })}
        className={`${control} font-medium max-lg:min-w-0 max-lg:flex-1`}
      >
        <option value="">Вся страна (склады дилеров)</option>
        {data.directions.length > 0 && (
          <optgroup label="РМ">
            {data.directions.map((d) => (
              <option key={d.id} value={`rm:${d.id}`}>
                {d.name}
              </option>
            ))}
          </optgroup>
        )}
        <optgroup label="Регионы">
          {data.regions.map((r) => (
            <option key={r.id} value={`region:${r.id}`}>
              {r.name}
            </option>
          ))}
        </optgroup>
        {(data.factory || data.export) && (
          <optgroup label="Отдельные склады">
            {data.factory && <option value="plant">{data.factory.name}</option>}
            {data.export && <option value="export">{data.export.name}</option>}
          </optgroup>
        )}
      </select>
    </label>
  );
}

/** Фасовка из названия — несколько значений (параметр pack); список весов — по всем товарам охвата, не по отфильтрованным. */
export function PackFilter({ packs, selected }: { packs: number[]; selected: number[] }) {
  const set = useSetParams();
  const label = (p: number) => `${num(p, p < 0.1 ? 3 : 2)} кг`;
  if (packs.length === 0) return null;
  const toggle = (p: number) => set({ pack: (selected.includes(p) ? selected.filter((x) => x !== p) : [...selected, p]).join(",") || null });
  return (
    <details className="relative">
      <summary className={`${control} inline-flex cursor-pointer select-none list-none items-center gap-1.5 [&::-webkit-details-marker]:hidden`}>
        Фасовка: <span className="font-medium">{selected.length ? selected.map(label).join(", ") : "все веса"}</span>
      </summary>
      <div className="absolute left-0 top-full z-20 mt-1 max-h-72 min-w-[190px] overflow-y-auto rounded-lg border border-line bg-surface p-2 shadow-lg">
        {selected.length > 0 && (
          <button type="button" onClick={() => set({ pack: null })} className="mb-1 block w-full rounded px-2 py-1 text-left text-xs font-medium text-accent hover:bg-muted">
            Все веса
          </button>
        )}
        {packs.map((p) => (
          <label key={p} className="flex cursor-pointer items-center gap-2 rounded px-2 py-1 text-sm text-ink hover:bg-muted">
            <input type="checkbox" checked={selected.includes(p)} onChange={() => toggle(p)} className="size-4 accent-[var(--color-accent)]" />
            {label(p)}
          </label>
        ))}
      </div>
    </details>
  );
}
