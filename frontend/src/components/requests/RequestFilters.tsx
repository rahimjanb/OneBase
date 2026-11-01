"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { Building2, CalendarDays, CircleDot, Flag, RotateCcw, Search, SlidersHorizontal, Tags, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ChoiceList, FilterPopover, MultiFilter, SheetSection, type Option } from "@/components/tasks/TaskFilterBar";
import { useT } from "@/i18n/client";
import { listOf, toggleIn } from "@/lib/task-board";
import { requestPriorities, requestPriorityLabel, requestStatuses, requestStatusLabel, type RequestCategory, type RequestDepartment, type RequestPriority } from "@/lib/procurement";
import { RequestPriorityPill } from "./RequestKit";

const input =
  "h-10 w-full rounded-[10px] border border-line bg-surface px-3 text-sm text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/25";

/**
 * Фильтры заявок — в адресе страницы (ссылкой можно поделиться): поиск по номеру, названию, позиции и инициатору (с задержкой), статус,
 * приоритет, категория, отдел (у тех, кто видит заявки нескольких отделов), дата создания. Компьютер — одна строка, телефон — поиск и
 * «Фильтры» в шторке с чипами включённых.
 */
export function RequestFilters({ categories, departments }: { categories: RequestCategory[]; departments: RequestDepartment[] }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("q") ?? "");
  const [sheet, setSheet] = useState(false);

  const status = params.get("status") ?? "";
  const priority = listOf(params.get("priority"));
  const category = params.get("category") ?? "";
  const department = listOf(params.get("department"));
  const from = params.get("from") ?? "";
  const to = params.get("to") ?? "";

  const apply = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    next.delete("page");
    router.push(`${pathname}${next.size ? `?${next}` : ""}`, { scroll: false });
  };
  const reset = () => {
    setSearch("");
    router.push(pathname, { scroll: false });
  };

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if ((params.get("q") ?? "") !== search.trim()) apply({ q: search.trim() || null });
    }, 400);
    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const statusOptions: Option[] = [
    { value: "open", label: t("В работе") },
    { value: "draft", label: t("Черновики") },
    { value: "closed", label: t("Закрытые") },
    ...requestStatuses.filter((s) => s !== "Draft").map((s) => ({ value: s, label: t(requestStatusLabel[s]) })),
  ];
  const priorityOptions: Option[] = [...requestPriorities].reverse().map((p) => ({ value: p, label: t(requestPriorityLabel[p]) }));
  const categoryOptions: Option[] = categories.map((c) => ({ value: c.id, label: c.name }));
  const departmentOptions: Option[] = departments.map((d) => ({ value: d.code, label: d.name }));
  const names = (values: string[], options: Option[]) => {
    const found = values.map((v) => options.find((o) => o.value === v)?.label).filter(Boolean) as string[];
    return found.length === 0 ? String(values.length) : found.length <= 2 ? found.join(", ") : `${found[0]} +${found.length - 1}`;
  };
  const dateValue = from && to ? `${from.split("-").reverse().join(".")} – ${to.split("-").reverse().join(".")}` : from ? `${t("с")} ${from.split("-").reverse().join(".")}` : to ? `${t("по")} ${to.split("-").reverse().join(".")}` : null;

  const chips: { key: string; label: string; clear: Record<string, null> }[] = [
    ...(status ? [{ key: "status", label: `${t("Статус")}: ${names([status], statusOptions)}`, clear: { status: null } }] : []),
    ...(priority.length ? [{ key: "priority", label: `${t("Приоритет")}: ${names(priority, priorityOptions)}`, clear: { priority: null } }] : []),
    ...(category ? [{ key: "category", label: `${t("Категория")}: ${names([category], categoryOptions)}`, clear: { category: null } }] : []),
    ...(department.length ? [{ key: "department", label: `${t("Отдел")}: ${names(department, departmentOptions)}`, clear: { department: null } }] : []),
    ...(dateValue ? [{ key: "date", label: `${t("Создана")}: ${dateValue}`, clear: { from: null, to: null } }] : []),
  ];
  const hasFilters = chips.length > 0 || !!params.get("q");

  const dates = (
    <div className="grid gap-2 p-2">
      <label className="grid gap-1 text-xs text-ink-3">
        {t("С")}
        <input type="date" value={from} max={to || undefined} onChange={(e) => apply({ from: e.target.value || null })} className={input} />
      </label>
      <label className="grid gap-1 text-xs text-ink-3">
        {t("По")}
        <input type="date" value={to} min={from || undefined} onChange={(e) => apply({ to: e.target.value || null })} className={input} />
      </label>
    </div>
  );

  return (
    <div className="mb-4 flex flex-col gap-2.5">
      <div className="flex items-center gap-2">
        <label className="relative block min-w-0 max-lg:flex-1 lg:w-[300px] lg:shrink-0">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
          <span className="sr-only">{t("Поиск")}</span>
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("Номер, название, товар, инициатор")}
            className="h-11 w-full rounded-xl border border-line bg-surface pl-9 pr-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/25 lg:h-10 lg:rounded-[10px]"
          />
        </label>
        <button
          type="button"
          onClick={() => setSheet(true)}
          className="inline-flex h-11 shrink-0 items-center gap-2 rounded-xl border border-line bg-surface px-3.5 text-sm font-semibold text-ink lg:hidden"
        >
          <SlidersHorizontal className="size-[18px]" aria-hidden />
          {chips.length > 0 && <span className="tabular-nums">{chips.length}</span>}
          <span className="sr-only">{t("Фильтры")}</span>
        </button>

        <div className="hidden min-w-0 flex-1 flex-wrap items-center gap-2 lg:flex">
          <MultiFilter
            icon={CircleDot}
            name={t("Статус")}
            value={status ? names([status], statusOptions) : null}
            options={statusOptions}
            selected={status ? [status] : []}
            single
            onToggle={(v) => apply({ status: v === status ? null : v })}
            onClear={() => apply({ status: null })}
          />
          <MultiFilter
            icon={Flag}
            name={t("Приоритет")}
            value={priority.length ? names(priority, priorityOptions) : null}
            options={priorityOptions}
            selected={priority}
            render={(o) => <RequestPriorityPill priority={o.value as RequestPriority} />}
            onToggle={(v) => apply({ priority: toggleIn(priority, v).join(",") || null })}
            onClear={() => apply({ priority: null })}
          />
          <MultiFilter
            icon={Tags}
            name={t("Категория")}
            value={category ? names([category], categoryOptions) : null}
            options={categoryOptions}
            selected={category ? [category] : []}
            single
            searchable={categoryOptions.length > 6}
            onToggle={(v) => apply({ category: v === category ? null : v })}
            onClear={() => apply({ category: null })}
          />
          {departmentOptions.length > 1 && (
            <MultiFilter
              icon={Building2}
              name={t("Отдел")}
              value={department.length ? names(department, departmentOptions) : null}
              options={departmentOptions}
              selected={department}
              onToggle={(v) => apply({ department: toggleIn(department, v).join(",") || null })}
              onClear={() => apply({ department: null })}
            />
          )}
          <FilterPopover icon={CalendarDays} name={t("Создана")} value={dateValue} onClear={() => apply({ from: null, to: null })}>
            {dates}
          </FilterPopover>
        </div>

        {hasFilters && (
          <button type="button" onClick={reset} className="ml-auto hidden h-10 shrink-0 items-center gap-1.5 px-1 text-sm font-semibold text-accent-strong hover:underline lg:inline-flex">
            <RotateCcw className="size-4" aria-hidden />
            {t("Сбросить")}
          </button>
        )}
      </div>

      {chips.length > 0 && (
        <div className="flex flex-wrap items-center gap-2 lg:hidden">
          {chips.map((c) => (
            <span key={c.key} className="inline-flex h-9 max-w-full items-center gap-1.5 rounded-full border border-line bg-surface pl-3 pr-1 text-[13px] text-ink-2">
              <span className="truncate">{c.label}</span>
              <button type="button" onClick={() => apply(c.clear)} aria-label={t("Убрать фильтр «{name}»", { name: c.label })} className="grid size-7 shrink-0 place-items-center rounded-full text-ink-3 hover:bg-muted hover:text-ink">
                <X className="size-3.5" aria-hidden />
              </button>
            </span>
          ))}
        </div>
      )}

      <Sheet
        open={sheet}
        onClose={() => setSheet(false)}
        title={t("Фильтры")}
        footer={
          <div className="flex gap-2">
            <button type="button" onClick={reset} disabled={!hasFilters} className="h-11 flex-1 rounded-xl border border-line bg-surface text-sm font-semibold text-ink-2 disabled:opacity-50">
              {t("Сбросить")}
            </button>
            <button type="button" onClick={() => setSheet(false)} className="h-11 flex-1 rounded-xl bg-accent text-sm font-semibold text-white">
              {t("Готово")}
            </button>
          </div>
        }
      >
        <div className="space-y-5">
          <SheetSection title={t("Статус")}>
            <ChoiceList options={statusOptions} selected={status ? [status] : []} single onToggle={(v) => apply({ status: v === status ? null : v })} />
          </SheetSection>
          <SheetSection title={t("Приоритет")}>
            <ChoiceList
              options={priorityOptions}
              selected={priority}
              render={(o) => <RequestPriorityPill priority={o.value as RequestPriority} />}
              onToggle={(v) => apply({ priority: toggleIn(priority, v).join(",") || null })}
            />
          </SheetSection>
          <SheetSection title={t("Категория")}>
            <ChoiceList options={categoryOptions} selected={category ? [category] : []} single onToggle={(v) => apply({ category: v === category ? null : v })} />
          </SheetSection>
          {departmentOptions.length > 1 && (
            <SheetSection title={t("Отдел")}>
              <ChoiceList options={departmentOptions} selected={department} onToggle={(v) => apply({ department: toggleIn(department, v).join(",") || null })} />
            </SheetSection>
          )}
          <SheetSection title={t("Создана")}>{dates}</SheetSection>
        </div>
      </Sheet>
    </div>
  );
}
