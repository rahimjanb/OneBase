"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { Check, ChevronDown, Search, UserRound, X } from "lucide-react";
import { field } from "@/components/ai/form";
import { useT } from "@/i18n/client";
import type { WorkAssignee } from "@/lib/tasks";

const norm = (s: string) => s.toLocaleLowerCase("ru").replace(/ё/g, "е");

/**
 * Выбор исполнителя среди всех действующих пользователей OneBase: поиск по имени, должности и отделу; сначала — сотрудники отдела задачи
 * и участники проекта, ниже — остальные. Пустое значение — «Любой сотрудник отдела» (задачу возьмёт тот, кто начнёт).
 * Список раскрывается в самой форме (а не поверх неё) — на телефоне в шторке он не обрезается.
 */
export function AssigneePicker({
  value,
  onChange,
  assignees,
  current,
}: {
  value: string;
  onChange: (id: string) => void;
  assignees: WorkAssignee[];
  /** Нынешний исполнитель задачи — если его уже нет в списке (отключён). */
  current?: { id: string; name: string | null } | null;
}) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [cursor, setCursor] = useState(0);
  const box = useRef<HTMLDivElement>(null);
  const search = useRef<HTMLInputElement>(null);

  const selected = assignees.find((a) => a.id === value) ?? (current && current.id === value ? { id: current.id, name: current.name ?? "—", position: null, department: null, inDepartment: false } : null);

  const filtered = useMemo(() => {
    const q = norm(query.trim());
    return q ? assignees.filter((a) => norm(`${a.name} ${a.position ?? ""} ${a.department ?? ""}`).includes(q)) : assignees;
  }, [assignees, query]);
  // Строки списка: «Любой сотрудник отдела» (без поиска), затем свои, затем остальные — по ним ходят стрелки.
  const rows = useMemo(() => [...(query.trim() ? [] : [null]), ...filtered.filter((a) => a.inDepartment), ...filtered.filter((a) => !a.inDepartment)], [filtered, query]);

  useEffect(() => {
    if (!open) return;
    search.current?.focus();
    const onDown = (e: PointerEvent) => !box.current?.contains(e.target as Node) && setOpen(false);
    document.addEventListener("pointerdown", onDown);
    return () => document.removeEventListener("pointerdown", onDown);
  }, [open]);

  useEffect(() => setCursor(0), [query]);

  const choose = (id: string) => {
    onChange(id);
    setOpen(false);
    setQuery("");
  };

  const onKey = (e: React.KeyboardEvent) => {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setCursor((c) => Math.min(rows.length - 1, c + 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setCursor((c) => Math.max(0, c - 1));
    } else if (e.key === "Enter") {
      e.preventDefault();
      const row = rows[cursor];
      if (row !== undefined) choose(row?.id ?? "");
    } else if (e.key === "Escape") {
      e.stopPropagation();
      setOpen(false);
    }
  };

  const own = filtered.filter((a) => a.inDepartment);
  const others = filtered.filter((a) => !a.inDepartment);
  const item = (a: WorkAssignee | null, index: number) => {
    const id = a?.id ?? "";
    const active = rows[cursor] === a;
    return (
      <li key={id || "any"}>
        <button
          type="button"
          onMouseEnter={() => setCursor(index)}
          onClick={() => choose(id)}
          className={`flex w-full items-center gap-2.5 rounded-md px-2.5 py-2 text-left text-sm ${active ? "bg-muted" : "hover:bg-muted/60"}`}
        >
          <UserRound className="size-4 shrink-0 text-ink-3" />
          <span className="min-w-0 flex-1">
            <span className="block truncate text-ink">{a ? a.name : t("Любой сотрудник отдела")}</span>
            {a && (a.position || a.department) && <span className="block truncate text-xs text-ink-3">{[a.position, a.department].filter(Boolean).join(" · ")}</span>}
          </span>
          {value === id && <Check className="size-4 shrink-0 text-accent-strong" />}
        </button>
      </li>
    );
  };

  return (
    <div ref={box}>
      <button type="button" className={`${field} flex items-center gap-2 text-left`} onClick={() => setOpen((o) => !o)} aria-expanded={open}>
        <span className={`min-w-0 flex-1 truncate ${selected ? "text-ink" : "text-ink-2"}`}>
          {selected ? (selected.position ? `${selected.name} — ${selected.position}` : selected.name) : t("Любой сотрудник отдела")}
        </span>
        {selected && (
          <span
            role="button"
            tabIndex={0}
            aria-label={t("Убрать исполнителя")}
            onClick={(e) => {
              e.stopPropagation();
              onChange("");
            }}
            onKeyDown={(e) => e.key === "Enter" && (e.stopPropagation(), onChange(""))}
            className="grid size-6 place-items-center rounded text-ink-3 hover:bg-muted hover:text-ink"
          >
            <X className="size-3.5" />
          </span>
        )}
        <ChevronDown className="size-4 shrink-0 text-ink-3" />
      </button>

      {open && (
        <div className="mt-1.5 rounded-lg border border-line bg-surface-solid p-1.5 shadow-lg">
          <label className="flex items-center gap-2 rounded-md border border-line px-2.5">
            <Search className="size-4 shrink-0 text-ink-3" />
            <input
              ref={search}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              onKeyDown={onKey}
              placeholder={t("Имя, должность или отдел")}
              className="h-9 min-w-0 flex-1 bg-transparent text-sm text-ink outline-none placeholder:text-ink-3"
            />
          </label>
          <ul className="mt-1 max-h-64 overflow-y-auto">
            {!query.trim() && item(null, 0)}
            {own.length > 0 && <li className="px-2.5 pb-1 pt-2 text-[11px] font-semibold uppercase tracking-wide text-ink-3">{t("Сотрудники отдела и проекта")}</li>}
            {own.map((a) => item(a, rows.indexOf(a)))}
            {others.length > 0 && <li className="px-2.5 pb-1 pt-2 text-[11px] font-semibold uppercase tracking-wide text-ink-3">{t("Другие пользователи OneBase")}</li>}
            {others.map((a) => item(a, rows.indexOf(a)))}
            {filtered.length === 0 && <li className="px-2.5 py-3 text-sm text-ink-3">{t("Никого не нашлось.")}</li>}
          </ul>
        </div>
      )}
    </div>
  );
}
