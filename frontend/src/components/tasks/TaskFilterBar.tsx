"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useMemo, useRef, useState } from "react";
import { Building2, CalendarDays, Check, ChevronDown, CircleDot, Flag, RotateCcw, Search, SlidersHorizontal, UserRound, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useLang, useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { monthsRu, monthsUz } from "@/lib/notifications";
import { activeFilterCount, listOf, presetOf, presetRange, toggleIn, type DatePreset } from "@/lib/task-board";
import { todayInTashkent, workPriorityLabel, workStatuses, workStatusLabel, type DepartmentTasks, type WorkAssignee, type WorkTaskPriority } from "@/lib/tasks";
import { PriorityPill } from "./TaskKit";

/** Какие фильтры показывает страница: у «Моих задач» нет исполнителя и «Только мои», у отдела — отдела; статус — только у таблицы. */
export type FilterSet = { department?: boolean; assignee?: boolean; status?: "all" | "archive"; mine?: boolean; dateField?: boolean };

const priorities: WorkTaskPriority[] = ["Urgent", "High", "Medium", "Low"];
const norm = (s: string) => s.toLocaleLowerCase("ru").replace(/ё/g, "е");

type Option = { value: string; label: string; hint?: string | null };

/**
 * Фильтры задач (ТЗ tasks-kanban-detail) — общие для канбана и таблицы, в адресе страницы: ссылкой можно поделиться, переключение
 * «Таблица · Канбан» и «назад» сохраняют их. Компьютер — одна строка: поиск, кнопки-фильтры с мультивыбором в поповере, «Создана» с
 * пресетами, «Только мои», «Сбросить». Телефон — поиск и «Фильтры» со счётчиком (шторка), под ними — чипы включённых фильтров.
 * Приоритет, отдел и исполнитель — через запятую (API принимает список). Поиск применяется с задержкой.
 */
export function TaskFilters({ departments, show, fixedDepartment }: { departments: DepartmentTasks[]; show: FilterSet; fixedDepartment?: string }) {
  const t = useT();
  const lang = useLang();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("search") ?? "");
  const [assignees, setAssignees] = useState<WorkAssignee[]>([]);
  const [sheet, setSheet] = useState(false);
  const today = todayInTashkent();
  const months = lang === "uz" ? monthsUz : monthsRu;

  const priority = listOf(params.get("priority"));
  const department = fixedDepartment ? [] : listOf(params.get("department"));
  const assignee = listOf(params.get("assigneeId"));
  const status = params.get("status") ?? "";
  const from = params.get("from");
  const to = params.get("to");
  const due = params.get("dateField") === "due";
  const mine = params.get("mine") === "true";
  const preset = presetOf(from, to, today);
  const count = activeFilterCount((k) => (k === "department" && fixedDepartment ? null : params.get(k)), fixedDepartment);
  const hasFilters = count > 0 || !!params.get("search");

  const apply = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    next.delete("page");
    router.push(`${pathname}${next.size ? `?${next}` : ""}`, { scroll: false });
  };
  // Сброс сохраняет вкладку страницы отдела (tab).
  const reset = () => {
    setSearch("");
    const tab = params.get("tab");
    router.push(`${pathname}${tab ? `?tab=${tab}` : ""}`, { scroll: false });
  };

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if ((params.get("search") ?? "") !== search.trim()) apply({ search: search.trim() || null });
    }, 400);
    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  // Исполнители — все действующие пользователи с разделом «Задачи»; отдел запроса задаёт только порядок (сотрудники отдела — первыми).
  const visible = departments.filter((d) => d.hasTasks);
  const listDepartment = fixedDepartment ?? department[0] ?? visible.find((d) => d.isMine)?.code ?? visible[0]?.code;
  useEffect(() => {
    if (!show.assignee || !listDepartment) return;
    let alive = true;
    bff<WorkAssignee[]>(`work/assignees?department=${encodeURIComponent(listDepartment)}`)
      .then((list) => alive && setAssignees(list))
      .catch(() => alive && setAssignees([]));
    return () => {
      alive = false;
    };
  }, [show.assignee, listDepartment]);

  const day = (iso: string) => `${Number(iso.slice(8, 10))} ${months[Number(iso.slice(5, 7)) - 1]}`;
  const dateName = due ? t("Срок") : t("Создана");
  const dateValue =
    preset === "today" ? day(today) : preset === "7d" ? t("7 дней") : preset === "30d" ? t("30 дней") : from && to && from !== to ? `${day(from)} – ${day(to)}` : from ? `${t("с")} ${day(from)}` : to ? `${t("по")} ${day(to)}` : "";

  const priorityOptions: Option[] = priorities.map((p) => ({ value: p, label: t(workPriorityLabel[p]) }));
  const departmentOptions: Option[] = visible.map((d) => ({ value: d.code, label: d.name }));
  const assigneeOptions: Option[] = assignees.map((a) => ({ value: a.id, label: a.name, hint: a.position ?? a.department }));
  const statusOptions: Option[] =
    show.status === "archive"
      ? [
          { value: "Done", label: t(workStatusLabel.Done) },
          { value: "Cancelled", label: t(workStatusLabel.Cancelled) },
        ]
      : [
          { value: "open", label: t("Открытые") },
          { value: "overdue", label: t("Просроченные") },
          { value: "blocked", label: t("Заблокированные") },
          ...workStatuses.map((s) => ({ value: s, label: t(workStatusLabel[s]) })),
        ];
  const names = (values: string[], options: Option[]) => {
    const found = values.map((v) => options.find((o) => o.value === v)?.label).filter(Boolean) as string[];
    return found.length === 0 ? String(values.length) : found.length <= 2 ? found.join(", ") : `${found[0]} +${found.length - 1}`;
  };

  // Включённые фильтры — чипы на телефоне (и подписи кнопок на компьютере).
  const chips: { key: string; label: string; clear: Record<string, null> }[] = [
    ...(priority.length ? [{ key: "priority", label: `${t("Приоритет")}: ${names(priority, priorityOptions)}`, clear: { priority: null } }] : []),
    ...(department.length ? [{ key: "department", label: `${t("Отдел")}: ${names(department, departmentOptions)}`, clear: { department: null } }] : []),
    ...(assignee.length ? [{ key: "assignee", label: `${t("Исполнитель")}: ${names(assignee, assigneeOptions)}`, clear: { assigneeId: null } }] : []),
    ...(status ? [{ key: "status", label: `${t("Статус")}: ${statusOptions.find((o) => o.value === status)?.label ?? status}`, clear: { status: null } }] : []),
    ...(from || to ? [{ key: "date", label: `${dateName}: ${dateValue}`, clear: { from: null, to: null, dateField: null } }] : []),
    ...(mine ? [{ key: "mine", label: t("Только мои"), clear: { mine: null } }] : []),
  ];

  const datePanel = (
    <DatePanel
      due={due}
      showField={!!show.dateField}
      preset={preset}
      from={from}
      to={to}
      onField={(isDue) => apply({ dateField: isDue ? "due" : null })}
      onPreset={(p) => {
        if (p === "custom") apply({ from: from ?? today, to: to ?? today });
        else apply(presetRange(p, today));
      }}
      onRange={(f, tTo) => apply({ from: f || null, to: tTo || null })}
      onClear={() => apply({ from: null, to: null })}
    />
  );

  return (
    <div className="flex flex-col gap-2.5">
      <div className="flex items-center gap-2">
        <label className="relative block min-w-0 max-lg:flex-1 lg:w-[300px] lg:shrink-0">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-subtle" aria-hidden />
          <input
            id="task-search"
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("Поиск по задачам и исполнителям")}
            aria-label={t("Поиск задач")}
            className="h-11 w-full rounded-xl border border-line bg-surface pl-9 pr-3 text-sm text-ink placeholder:text-subtle focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/25 lg:h-10 lg:rounded-[10px]"
          />
        </label>

        <button
          type="button"
          onClick={() => setSheet(true)}
          aria-label={count > 0 ? t("Фильтры, включено: {n}", { n: count }) : t("Фильтры")}
          className={`inline-flex h-11 shrink-0 items-center gap-1.5 rounded-xl border px-3.5 text-sm font-semibold lg:hidden ${
            count > 0 ? "border-accent/35 bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink-2"
          }`}
        >
          <SlidersHorizontal className="size-[18px]" aria-hidden />
          {count > 0 && <span className="tabular-nums">{count}</span>}
        </button>

        <div className="hidden min-w-0 flex-1 flex-wrap items-center gap-2 lg:flex">
          <MultiFilter
            icon={Flag}
            name={t("Приоритет")}
            value={priority.length ? names(priority, priorityOptions) : null}
            options={priorityOptions}
            selected={priority}
            render={(o) => <PriorityPill priority={o.value as WorkTaskPriority} />}
            onToggle={(v) => apply({ priority: toggleIn(priority, v).join(",") || null })}
            onClear={() => apply({ priority: null })}
          />
          {show.department && !fixedDepartment && departmentOptions.length > 1 && (
            <MultiFilter
              icon={Building2}
              name={t("Отдел")}
              value={department.length ? names(department, departmentOptions) : null}
              options={departmentOptions}
              selected={department}
              searchable={departmentOptions.length > 6}
              onToggle={(v) => apply({ department: toggleIn(department, v).join(",") || null })}
              onClear={() => apply({ department: null })}
            />
          )}
          {show.assignee && listDepartment && (
            <MultiFilter
              icon={UserRound}
              name={t("Исполнитель")}
              value={assignee.length ? names(assignee, assigneeOptions) : null}
              options={assigneeOptions}
              selected={assignee}
              searchable
              onToggle={(v) => apply({ assigneeId: toggleIn(assignee, v).join(",") || null })}
              onClear={() => apply({ assigneeId: null })}
            />
          )}
          {show.status && (
            <MultiFilter
              icon={CircleDot}
              name={t("Статус")}
              value={status ? (statusOptions.find((o) => o.value === status)?.label ?? status) : null}
              options={statusOptions}
              selected={status ? [status] : []}
              single
              onToggle={(v) => apply({ status: v === status ? null : v })}
              onClear={() => apply({ status: null })}
            />
          )}
          <FilterPopover icon={CalendarDays} name={dateName} value={from || to ? dateValue : null} onClear={() => apply({ from: null, to: null, dateField: null })} wide>
            {datePanel}
          </FilterPopover>
          {show.mine && <MineSwitch on={mine} onChange={(on) => apply({ mine: on ? "true" : null })} />}
        </div>

        {hasFilters && (
          <button type="button" onClick={reset} className="ml-auto hidden h-10 shrink-0 items-center gap-1.5 px-1 text-sm font-semibold text-link hover:underline lg:inline-flex">
            <RotateCcw className="size-4" aria-hidden />
            {t("Сбросить")}
          </button>
        )}
      </div>

      {chips.length > 0 && (
        <div className="flex flex-wrap items-center gap-2 lg:hidden">
          {chips.map((c) => (
            <span key={c.key} className="inline-flex h-9 max-w-full items-center gap-1.5 rounded-full border border-line bg-surface pl-3 pr-1 text-[13px] text-ink-2">
              {c.key === "date" && <CalendarDays className="size-4 shrink-0 text-ink-3" aria-hidden />}
              <span className="truncate">{c.label}</span>
              <button type="button" onClick={() => apply(c.clear)} aria-label={t("Убрать фильтр «{name}»", { name: c.label })} className="grid size-7 shrink-0 place-items-center rounded-full text-ink-3 hover:bg-surface-2 hover:text-ink">
                <X className="size-3.5" aria-hidden />
              </button>
            </span>
          ))}
          <button type="button" onClick={reset} className="h-9 px-1 text-sm font-semibold text-link">
            {t("Сбросить")}
          </button>
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
          <SheetSection title={t("Приоритет")}>
            <div className="flex flex-wrap gap-2">
              {priorities.map((p) => (
                <button
                  key={p}
                  type="button"
                  aria-pressed={priority.includes(p)}
                  onClick={() => apply({ priority: toggleIn(priority, p).join(",") || null })}
                  className={`inline-flex h-10 items-center gap-2 rounded-xl border px-2.5 ${priority.includes(p) ? "border-accent bg-accent-soft" : "border-line"}`}
                >
                  <PriorityPill priority={p} />
                  {priority.includes(p) && <Check className="size-4 text-accent-strong" aria-hidden />}
                </button>
              ))}
            </div>
          </SheetSection>
          {show.status && (
            <SheetSection title={t("Статус")}>
              <ChoiceList options={statusOptions} selected={status ? [status] : []} onToggle={(v) => apply({ status: v === status ? null : v })} />
            </SheetSection>
          )}
          {show.department && !fixedDepartment && departmentOptions.length > 1 && (
            <SheetSection title={t("Отдел")}>
              <ChoiceList options={departmentOptions} selected={department} onToggle={(v) => apply({ department: toggleIn(department, v).join(",") || null })} />
            </SheetSection>
          )}
          {show.assignee && listDepartment && (
            <SheetSection title={t("Исполнитель")}>
              <ChoiceList options={assigneeOptions} selected={assignee} searchable onToggle={(v) => apply({ assigneeId: toggleIn(assignee, v).join(",") || null })} />
            </SheetSection>
          )}
          <SheetSection title={dateName}>{datePanel}</SheetSection>
          {show.mine && (
            <div className="flex items-center justify-between gap-3">
              <span className="text-sm font-semibold text-ink">{t("Только мои")}</span>
              <MineSwitch on={mine} onChange={(on) => apply({ mine: on ? "true" : null })} bare />
            </div>
          )}
        </div>
      </Sheet>
    </div>
  );
}

function SheetSection({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section>
      <h3 className="mb-2 text-[11px] font-bold uppercase tracking-[0.8px] text-subtle">{title}</h3>
      {children}
    </section>
  );
}

/** Кнопка-фильтр с поповером: выключен — «Приоритет ▾», включён — «Приоритет: Высокий ×» на мягком акценте. Esc и нажатие мимо закрывают. */
function FilterPopover({
  icon: Icon,
  name,
  value,
  onClear,
  wide = false,
  children,
}: {
  icon: React.ComponentType<{ className?: string }>;
  name: string;
  value: string | null;
  onClear: () => void;
  wide?: boolean;
  children: React.ReactNode;
}) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => !box.current?.contains(e.target as Node) && setOpen(false);
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      setOpen(false);
      box.current?.querySelector<HTMLButtonElement>("button")?.focus();
    };
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const active = value !== null;
  return (
    <div ref={box} className="relative">
      <div
        className={`inline-flex h-10 items-center rounded-[10px] border text-sm transition-colors ${
          active ? "border-accent/35 bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink-2 hover:border-line-strong hover:text-ink"
        }`}
      >
        <button type="button" onClick={() => setOpen((o) => !o)} aria-expanded={open} aria-haspopup="dialog" className="inline-flex h-full max-w-[260px] items-center gap-2 pl-3 pr-2.5 font-medium">
          <Icon className="size-4 shrink-0" />
          <span className="truncate">{active ? `${name}: ${value}` : name}</span>
          {!active && <ChevronDown className="size-4 shrink-0 text-subtle" aria-hidden />}
        </button>
        {active && (
          <button type="button" onClick={onClear} aria-label={t("Сбросить фильтр «{name}»", { name })} className="grid h-full w-8 place-items-center rounded-r-[10px] hover:bg-accent/10">
            <X className="size-4" aria-hidden />
          </button>
        )}
      </div>
      {open && (
        <div role="dialog" aria-label={name} className={`absolute left-0 top-full z-30 mt-1.5 rounded-xl border border-line-strong bg-surface-solid p-1.5 text-ink shadow-[0_16px_40px_rgb(0_0_0/0.35)] ${wide ? "w-72" : "w-64"}`}>
          {children}
        </div>
      )}
    </div>
  );
}

function MultiFilter({
  icon,
  name,
  value,
  options,
  selected,
  searchable = false,
  single = false,
  render,
  onToggle,
  onClear,
}: {
  icon: React.ComponentType<{ className?: string }>;
  name: string;
  value: string | null;
  options: Option[];
  selected: string[];
  searchable?: boolean;
  single?: boolean;
  render?: (o: Option) => React.ReactNode;
  onToggle: (value: string) => void;
  onClear: () => void;
}) {
  return (
    <FilterPopover icon={icon} name={name} value={value} onClear={onClear}>
      <ChoiceList options={options} selected={selected} searchable={searchable} single={single} render={render} onToggle={onToggle} />
    </FilterPopover>
  );
}

/** Список вариантов с галочками и поиском; single — один вариант (статус). */
function ChoiceList({
  options,
  selected,
  searchable = false,
  single = false,
  render,
  onToggle,
}: {
  options: Option[];
  selected: string[];
  searchable?: boolean;
  single?: boolean;
  render?: (o: Option) => React.ReactNode;
  onToggle: (value: string) => void;
}) {
  const t = useT();
  const [query, setQuery] = useState("");
  const shown = useMemo(() => {
    const q = norm(query.trim());
    const list = q ? options.filter((o) => norm(`${o.label} ${o.hint ?? ""}`).includes(q)) : options;
    // Выбранные — сверху, чтобы их было видно в длинном списке.
    return [...list.filter((o) => selected.includes(o.value)), ...list.filter((o) => !selected.includes(o.value))];
  }, [options, query, selected]);

  return (
    <div>
      {searchable && (
        <label className="relative mb-1.5 block">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-subtle" aria-hidden />
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("Найти")}
            aria-label={t("Найти")}
            className="h-10 w-full rounded-lg border border-line bg-surface pl-8 pr-2 text-sm text-ink placeholder:text-subtle focus:border-accent focus:outline-none"
          />
        </label>
      )}
      <ul className="max-h-72 overflow-y-auto overscroll-contain" role="listbox" aria-multiselectable={!single}>
        {shown.map((o) => {
          const on = selected.includes(o.value);
          return (
            <li key={o.value}>
              <button
                type="button"
                role="option"
                aria-selected={on}
                onClick={() => onToggle(o.value)}
                className="flex min-h-10 w-full items-center gap-2.5 rounded-lg px-2 py-1.5 text-left text-sm text-ink hover:bg-surface-2"
              >
                <span className={`grid size-[18px] shrink-0 place-items-center border ${single ? "rounded-full" : "rounded-[5px]"} ${on ? "border-accent bg-accent text-white" : "border-line-strong"}`}>
                  {on && <Check className="size-3" strokeWidth={3} aria-hidden />}
                </span>
                <span className="min-w-0 flex-1">
                  {render ? render(o) : <span className="block truncate">{o.label}</span>}
                  {o.hint && <span className="block truncate text-[11px] text-subtle">{o.hint}</span>}
                </span>
              </button>
            </li>
          );
        })}
        {shown.length === 0 && <li className="px-2 py-3 text-center text-[13px] text-subtle">{t("Ничего не найдено")}</li>}
      </ul>
    </div>
  );
}

/** Период: «Сегодня, 7 дней, 30 дней, Свой» одним контролом; у таблицы — ещё выбор «по дате создания / по сроку». */
function DatePanel({
  due,
  showField,
  preset,
  from,
  to,
  onField,
  onPreset,
  onRange,
  onClear,
}: {
  due: boolean;
  showField: boolean;
  preset: DatePreset | null;
  from: string | null;
  to: string | null;
  onField: (due: boolean) => void;
  onPreset: (p: DatePreset) => void;
  onRange: (from: string, to: string) => void;
  onClear: () => void;
}) {
  const t = useT();
  const presets: { key: DatePreset; label: string }[] = [
    { key: "today", label: t("Сегодня") },
    { key: "7d", label: t("7 дней") },
    { key: "30d", label: t("30 дней") },
    { key: "custom", label: t("Свой") },
  ];
  const input = "h-10 w-full rounded-lg border border-line bg-surface px-2 text-sm text-ink focus:border-accent focus:outline-none";
  return (
    <div className="space-y-2 p-0.5">
      {showField && (
        <div className="grid grid-cols-2 gap-1 rounded-lg bg-surface-2 p-1" role="radiogroup" aria-label={t("Период по")}>
          {[false, true].map((isDue) => (
            <button
              key={String(isDue)}
              type="button"
              role="radio"
              aria-checked={due === isDue}
              onClick={() => onField(isDue)}
              className={`h-8 rounded-md text-[13px] font-medium ${due === isDue ? "bg-surface text-ink shadow-sm" : "text-ink-3"}`}
            >
              {isDue ? t("По сроку") : t("По дате создания")}
            </button>
          ))}
        </div>
      )}
      <div className="grid grid-cols-4 gap-1" role="radiogroup" aria-label={t("Период")}>
        {presets.map((p) => (
          <button
            key={p.key}
            type="button"
            role="radio"
            aria-checked={preset === p.key}
            onClick={() => onPreset(p.key)}
            className={`h-9 rounded-lg border text-[13px] font-medium ${preset === p.key ? "border-accent bg-accent-soft text-accent-strong" : "border-line text-ink-2 hover:bg-surface-2"}`}
          >
            {p.label}
          </button>
        ))}
      </div>
      {preset === "custom" && (
        <div className="grid grid-cols-2 gap-2">
          <label className="block text-[11px] text-subtle">
            {t("С")}
            <input type="date" className={input} value={from ?? ""} max={to ?? undefined} onChange={(e) => onRange(e.target.value, to ?? "")} />
          </label>
          <label className="block text-[11px] text-subtle">
            {t("По")}
            <input type="date" className={input} value={to ?? ""} min={from ?? undefined} onChange={(e) => onRange(from ?? "", e.target.value)} />
          </label>
        </div>
      )}
      {preset && (
        <button type="button" onClick={onClear} className="h-9 w-full rounded-lg text-[13px] font-medium text-ink-3 hover:bg-surface-2 hover:text-ink">
          {t("Любая дата")}
        </button>
      )}
    </div>
  );
}

function MineSwitch({ on, onChange, bare = false }: { on: boolean; onChange: (on: boolean) => void; bare?: boolean }) {
  const t = useT();
  return (
    <button
      type="button"
      role="switch"
      aria-checked={on}
      aria-label={bare ? t("Только мои") : undefined}
      onClick={() => onChange(!on)}
      className={bare ? "" : `inline-flex h-10 items-center gap-2.5 rounded-[10px] border px-3 text-sm font-medium ${on ? "border-accent/35 bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink-2 hover:text-ink"}`}
    >
      <span className={`relative inline-block h-5 w-9 shrink-0 rounded-full transition-colors ${on ? "bg-accent" : "bg-line-strong"}`}>
        <span className={`absolute top-0.5 size-4 rounded-full bg-white shadow transition-[left] ${on ? "left-[18px]" : "left-0.5"}`} />
      </span>
      {!bare && t("Только мои")}
    </button>
  );
}
