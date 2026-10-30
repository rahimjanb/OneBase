"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "next/navigation";
import { Building2, CalendarDays, Check, ChevronDown, Loader2, Search, Users, type LucideIcon } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { filtersOf, PERIODS, type HomeFilters, type PeriodKey } from "@/lib/dashboard-mobile";
import type { HrEmployeeRow } from "@/lib/hr";
import type { PageResult } from "@/lib/tasks";
import { focusRing, press } from "./ui";

/**
 * Фильтры главной — в адресе (?period=7d&dept=finance,sales&emp=…), чтобы пережить обновление и ссылку. Меняются через history.replaceState:
 * Next.js синхронизирует его с useSearchParams без запроса к серверу — блоки ниже пересчитываются сразу, история не засоряется.
 */
export function useHomeFilters() {
  const params = useSearchParams();
  const filters = useMemo(() => filtersOf(params), [params]);
  const update = useCallback((next: Partial<HomeFilters>) => {
    const url = new URL(window.location.href);
    const f = { ...filtersOf(url.searchParams), ...next };
    const put = (key: string, value: string | null) => (value ? url.searchParams.set(key, value) : url.searchParams.delete(key));
    put("period", f.period === "30d" ? null : f.period);
    put("from", f.period === "custom" ? f.from : null);
    put("to", f.period === "custom" ? f.to : null);
    put("dept", f.departments.join(",") || null);
    put("emp", f.employees.join(",") || null);
    window.history.replaceState(null, "", url);
  }, []);
  return [filters, update] as const;
}

/** Имена выбранных сотрудников — для подписи чипа (в адресе только id). */
const NAMES_KEY = "onebase-home-employees";
const readNames = (): Record<string, string> => {
  try {
    return JSON.parse(sessionStorage.getItem(NAMES_KEY) ?? "{}");
  } catch {
    return {};
  }
};

const ddmm = (iso: string | null) => (iso ? `${iso.slice(8, 10)}.${iso.slice(5, 7)}` : "…");

export type DepartmentOption = { code: string; name: string };

/** Чипы фильтров в одну строку с горизонтальной прокруткой; тап — шторка выбора. */
export function Filters({ departments }: { departments: DepartmentOption[] }) {
  const t = useT();
  const [filters, update] = useHomeFilters();
  const [open, setOpen] = useState<"period" | "dept" | "emp" | null>(null);
  const [names, setNames] = useState<Record<string, string>>({});
  useEffect(() => setNames(readNames()), []);

  const periodLabel =
    filters.period === "custom" ? `${ddmm(filters.from)} – ${ddmm(filters.to)}` : t(PERIODS.find((p) => p.key === filters.period)?.label ?? "30 дней");
  const deptLabel =
    filters.departments.length === 0
      ? t("Все отделы")
      : filters.departments.length === 1
        ? t(departments.find((d) => d.code === filters.departments[0])?.name ?? filters.departments[0])
        : t("Отделы: {n}", { n: filters.departments.length });
  const empLabel =
    filters.employees.length === 0
      ? t("Все сотрудники")
      : filters.employees.length === 1 && names[filters.employees[0]]
        ? names[filters.employees[0]]
        : t("Сотрудники: {n}", { n: filters.employees.length });

  return (
    <>
      <div role="toolbar" aria-label={t("Фильтры")} className="no-scrollbar -mx-[16px] flex gap-[8px] overflow-x-auto px-[16px] py-[2px]">
        <Chip icon={CalendarDays} label={periodLabel} active={filters.period !== "30d"} onClick={() => setOpen("period")} hint={t("Период")} />
        <Chip icon={Building2} label={deptLabel} active={filters.departments.length > 0} onClick={() => setOpen("dept")} hint={t("Отделы")} />
        <Chip icon={Users} label={empLabel} active={filters.employees.length > 0} onClick={() => setOpen("emp")} hint={t("Сотрудники")} />
      </div>

      <PeriodSheet open={open === "period"} onClose={() => setOpen(null)} filters={filters} onApply={(f) => update(f)} />
      <PickSheet
        open={open === "dept"}
        onClose={() => setOpen(null)}
        title={t("Отделы")}
        selected={filters.departments}
        onApply={(codes) => update({ departments: codes })}
        load={async (q) =>
          departments.filter((d) => t(d.name).toLowerCase().includes(q.toLowerCase())).map((d) => ({ id: d.code, name: t(d.name) }))
        }
      />
      <PickSheet
        open={open === "emp"}
        onClose={() => setOpen(null)}
        title={t("Сотрудники")}
        selected={filters.employees}
        known={names}
        remote
        onApply={(ids, picked) => {
          const next = { ...readNames(), ...picked };
          try {
            sessionStorage.setItem(NAMES_KEY, JSON.stringify(next));
          } catch {
            // хранилище недоступно — в чипе будет число
          }
          setNames(next);
          update({ employees: ids });
        }}
        load={async (q) => {
          const page = await bff<PageResult<HrEmployeeRow>>(`hr/employees?search=${encodeURIComponent(q)}&status=W&pageSize=30`);
          return page.items.map((e) => ({ id: e.id, name: e.fullName, hint: e.job ?? e.division ?? undefined }));
        }}
      />
    </>
  );
}

function Chip({ icon: Icon, label, active, onClick, hint }: { icon: LucideIcon; label: string; active: boolean; onClick: () => void; hint: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-haspopup="dialog"
      aria-label={`${hint}: ${label}`}
      className={`inline-flex h-[34px] shrink-0 items-center gap-[6px] rounded-[20px] border px-[12px] text-[13px] font-medium ${press} ${focusRing} ${
        active
          ? "border-[color-mix(in_srgb,var(--ts-accent)_35%,transparent)] bg-(--ts-accent-soft) text-(--ts-link)"
          : "border-(--ts-border) bg-(--ts-surface) text-(--ts-text-2)"
      }`}
    >
      <Icon aria-hidden className="size-[14px] shrink-0" strokeWidth={2} />
      <span className="max-w-[180px] truncate">{label}</span>
      <ChevronDown aria-hidden className="size-[13px] shrink-0 opacity-70" strokeWidth={2} />
    </button>
  );
}

const rowClass = `flex min-h-[48px] w-full items-center gap-3 rounded-[12px] px-3 text-left text-[15px] text-ink ${press} hover:bg-muted`;

/** Период: 7 дней · 30 дней · Квартал · Год — сразу; «Свой период» — две даты и «Применить». */
function PeriodSheet({ open, onClose, filters, onApply }: { open: boolean; onClose: () => void; filters: HomeFilters; onApply: (f: Partial<HomeFilters>) => void }) {
  const t = useT();
  const [custom, setCustom] = useState(filters.period === "custom");
  const [from, setFrom] = useState(filters.from ?? "");
  const [to, setTo] = useState(filters.to ?? "");
  useEffect(() => {
    if (!open) return;
    setCustom(filters.period === "custom");
    setFrom(filters.from ?? "");
    setTo(filters.to ?? "");
  }, [open, filters]);

  const pick = (period: PeriodKey) => {
    onApply({ period });
    onClose();
  };
  const valid = !!from && !!to && from <= to;

  return (
    <Sheet open={open} onClose={onClose} title={t("Период")}>
      <div role="radiogroup" aria-label={t("Период")} className="flex flex-col gap-0.5">
        {PERIODS.map((p) => {
          const active = !custom && filters.period === p.key;
          return (
            <button key={p.key} type="button" role="radio" aria-checked={active} onClick={() => pick(p.key)} className={rowClass}>
              <span className="flex-1">{t(p.label)}</span>
              {active && <Check aria-hidden className="size-5 text-accent" />}
            </button>
          );
        })}
        <button type="button" role="radio" aria-checked={custom} onClick={() => setCustom(true)} className={rowClass}>
          <span className="flex-1">{t("Свой период")}</span>
          {custom && <Check aria-hidden className="size-5 text-accent" />}
        </button>
      </div>
      {custom && (
        <div className="mt-3 flex flex-col gap-3 rounded-[14px] border border-line p-3">
          <div className="grid grid-cols-2 gap-2">
            <label className="flex flex-col gap-1 text-xs text-ink-3">
              {t("С")}
              <input type="date" value={from} max={to || undefined} onChange={(e) => setFrom(e.target.value)} className="h-11 rounded-[10px] border border-line bg-surface px-2 text-[15px] text-ink" />
            </label>
            <label className="flex flex-col gap-1 text-xs text-ink-3">
              {t("По")}
              <input type="date" value={to} min={from || undefined} onChange={(e) => setTo(e.target.value)} className="h-11 rounded-[10px] border border-line bg-surface px-2 text-[15px] text-ink" />
            </label>
          </div>
          <button
            type="button"
            disabled={!valid}
            onClick={() => {
              onApply({ period: "custom", from, to });
              onClose();
            }}
            className="h-11 rounded-[12px] bg-accent text-[15px] font-semibold text-white disabled:opacity-50"
          >
            {t("Применить")}
          </button>
        </div>
      )}
    </Sheet>
  );
}

type Option = { id: string; name: string; hint?: string };

/**
 * Отделы и сотрудники: поиск и мультивыбор, «Применить» — сразу ко всем блокам. remote — список с сервера по мере ввода (сотрудники);
 * нет доступа — пояснение вместо списка. Выбранные — всегда вверху списка.
 */
function PickSheet({
  open,
  onClose,
  title,
  selected,
  known = {},
  remote = false,
  load,
  onApply,
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  selected: string[];
  known?: Record<string, string>;
  remote?: boolean;
  load: (q: string) => Promise<Option[]>;
  onApply: (ids: string[], picked: Record<string, string>) => void;
}) {
  const t = useT();
  const [query, setQuery] = useState("");
  const [items, setItems] = useState<Option[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [picked, setPicked] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!open) return;
    setQuery("");
    setPicked(Object.fromEntries(selected.map((id) => [id, known[id] ?? id])));
    // Открытие — один раз на показ шторки; known и selected берём на этот момент.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  useEffect(() => {
    if (!open) return;
    let alive = true;
    setLoading(true);
    const timer = window.setTimeout(
      () =>
        load(query.trim())
          .then((list) => alive && (setItems(list), setError(null)))
          .catch((e: Error) => alive && (setItems([]), setError(e.message)))
          .finally(() => alive && setLoading(false)),
      remote && query ? 250 : 0,
    );
    return () => {
      alive = false;
      window.clearTimeout(timer);
    };
    // load — новая функция при каждой отрисовке родителя; список зависит только от строки поиска.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, query, remote]);

  // Выбранные вверху, затем найденные (без повторов).
  const rows = useMemo(() => {
    const chosen = Object.entries(picked).map(([id, name]) => items?.find((i) => i.id === id) ?? { id, name });
    const q = query.trim().toLowerCase();
    const visibleChosen = q ? chosen.filter((c) => c.name.toLowerCase().includes(q)) : chosen;
    return [...visibleChosen, ...(items ?? []).filter((i) => !(i.id in picked))];
  }, [items, picked, query]);

  const toggle = (o: Option) =>
    setPicked((p) => {
      const next = { ...p };
      if (o.id in next) delete next[o.id];
      else next[o.id] = o.name;
      return next;
    });

  const count = Object.keys(picked).length;
  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={title}
      footer={
        <div className="flex gap-2">
          <button type="button" onClick={() => setPicked({})} disabled={count === 0} className="h-11 flex-1 rounded-[12px] border border-line text-[15px] font-medium text-ink disabled:opacity-50">
            {t("Сбросить")}
          </button>
          <button
            type="button"
            onClick={() => {
              onApply(Object.keys(picked), picked);
              onClose();
            }}
            className="h-11 flex-[2] rounded-[12px] bg-accent text-[15px] font-semibold text-white"
          >
            {count > 0 ? t("Применить · {n}", { n: count }) : t("Показать все")}
          </button>
        </div>
      }
    >
      <label className="sticky top-0 z-10 flex h-11 items-center gap-2 rounded-[12px] border border-line bg-surface-solid px-3">
        <Search aria-hidden className="size-4 shrink-0 text-ink-3" />
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={t("Поиск")}
          aria-label={t("Поиск")}
          className="min-w-0 flex-1 bg-transparent text-[15px] text-ink outline-none placeholder:text-ink-3"
        />
        {loading && <Loader2 aria-hidden className="size-4 shrink-0 animate-spin text-ink-3" />}
      </label>
      {error ? (
        <p className="px-1 py-6 text-center text-sm text-ink-3">{error}</p>
      ) : rows.length === 0 && !loading ? (
        <p className="px-1 py-6 text-center text-sm text-ink-3">{t("Ничего не найдено")}</p>
      ) : (
        <ul className="mt-2 flex flex-col gap-0.5" aria-busy={loading}>
          {rows.map((o) => {
            const on = o.id in picked;
            return (
              <li key={o.id}>
                <button type="button" role="checkbox" aria-checked={on} onClick={() => toggle(o)} className={rowClass}>
                  <span
                    aria-hidden
                    className={`grid size-[22px] shrink-0 place-items-center rounded-[7px] border ${on ? "border-accent bg-accent text-white" : "border-line-strong"}`}
                  >
                    {on && <Check className="size-[15px]" strokeWidth={3} />}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block truncate">{o.name}</span>
                    {o.hint && <span className="block truncate text-xs text-ink-3">{o.hint}</span>}
                  </span>
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </Sheet>
  );
}
