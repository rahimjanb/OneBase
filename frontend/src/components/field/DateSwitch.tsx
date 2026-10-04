"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { ChevronLeft, ChevronRight } from "lucide-react";

const shift = (iso: string, days: number) => {
  const d = new Date(`${iso}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

/** Выбор дня: стрелки и календарь; пишет ?date= в адрес, сервер пересчитывает страницу. */
export function DateSwitch({ value, today, param = "date" }: { value: string; today: string; param?: string }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const go = (date: string) => {
    const next = new URLSearchParams(params);
    if (date === today) next.delete(param);
    else next.set(param, date);
    const q = next.toString();
    router.push(q ? `${pathname}?${q}` : pathname);
  };
  const label = new Date(`${value}T00:00:00Z`).toLocaleDateString("ru-RU", { weekday: "short", day: "numeric", month: "long", timeZone: "UTC" });
  return (
    <div className="inline-flex items-center rounded-lg border border-line bg-surface">
      <button type="button" aria-label="Предыдущий день" onClick={() => go(shift(value, -1))} className="grid size-11 place-items-center text-ink-2 hover:bg-muted lg:size-10">
        <ChevronLeft className="size-4" />
      </button>
      <label className="relative flex h-11 items-center px-2 text-sm font-medium text-ink lg:h-10">
        {value === today ? `Сегодня, ${label}` : label}
        <input type="date" value={value} onChange={(e) => e.target.value && go(e.target.value)} className="absolute inset-0 cursor-pointer opacity-0" aria-label="Выбрать дату" />
      </label>
      <button type="button" aria-label="Следующий день" onClick={() => go(shift(value, 1))} className="grid size-11 place-items-center text-ink-2 hover:bg-muted lg:size-10">
        <ChevronRight className="size-4" />
      </button>
    </div>
  );
}

/** Выпадающий фильтр, пишет параметр в адрес. */
export function ParamSelect({ param, value, options, label, className = "" }: { param: string; value: string; options: { value: string; label: string }[]; label: string; className?: string }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  return (
    <label className={`inline-flex items-center gap-2 text-sm text-ink-2 ${className}`}>
      <span className="sr-only lg:not-sr-only">{label}</span>
      <select
        value={value}
        onChange={(e) => {
          const next = new URLSearchParams(params);
          if (e.target.value) next.set(param, e.target.value);
          else next.delete(param);
          next.delete("page");
          const q = next.toString();
          router.push(q ? `${pathname}?${q}` : pathname);
        }}
        className="h-11 min-w-0 max-w-[260px] rounded-lg border border-line bg-surface px-3 text-sm text-ink lg:h-10"
      >
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    </label>
  );
}

/** Поиск с отправкой по Enter. */
export function SearchBox({ param = "search", value, placeholder }: { param?: string; value: string; placeholder: string }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  return (
    <form
      className="min-w-0 flex-1 sm:max-w-sm"
      onSubmit={(e) => {
        e.preventDefault();
        const text = String(new FormData(e.currentTarget).get(param) ?? "").trim();
        const next = new URLSearchParams(params);
        if (text) next.set(param, text);
        else next.delete(param);
        next.delete("page");
        const q = next.toString();
        router.push(q ? `${pathname}?${q}` : pathname);
      }}
    >
      <input
        name={param}
        type="search"
        defaultValue={value}
        placeholder={placeholder}
        enterKeyHint="search"
        className="h-11 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 lg:h-10"
      />
    </form>
  );
}
