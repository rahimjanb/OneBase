"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { KanbanSquare, Plus, Search, Table2 } from "lucide-react";
import { taskTabs } from "@/components/shell/sections";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/notifications";
import type { DepartmentTasks, WorkProject } from "@/lib/tasks";
import { TaskForm } from "./TaskForm";

/** Переключатель вида: «Таблица · Канбан» (календарь появится, когда будет сделан вид). Фильтры переносятся. */
export function ViewSwitch({ current, tableHref, boardHref }: { current: "table" | "board"; tableHref: string; boardHref: string }) {
  const t = useT();
  const items = [
    { key: "table", label: t("Таблица"), href: tableHref, icon: Table2 },
    { key: "board", label: t("Канбан"), href: boardHref, icon: KanbanSquare },
  ] as const;
  return (
    <nav aria-label={t("Вид")} className="inline-flex h-10 items-center gap-0.5 rounded-xl border border-line bg-surface p-1">
      {items.map((i) => (
        <Link
          key={i.key}
          href={i.href}
          aria-current={current === i.key ? "page" : undefined}
          className={`inline-flex h-full items-center gap-2 rounded-lg px-3 text-sm font-medium ${
            current === i.key ? "border border-line-strong bg-surface-2 text-ink" : "text-ink-3 hover:text-ink"
          }`}
        >
          <i.icon className="size-4" aria-hidden />
          {i.label}
        </Link>
      ))}
    </nav>
  );
}

/**
 * Шапка канбана (ТЗ tasks-kanban-detail). Компьютер: крошки, «Канбан» и число задач с учётом фильтров, подсказка; справа вид и
 * «Новая задача». Телефон — вместо глобальной шапки: «Задачи», поиск (фокус в поле поиска задач) и «+», ниже — вкладки раздела чипами.
 */
export function BoardHeader({
  total,
  tableHref,
  boardHref,
  departments,
  projects,
  defaultDepartment,
}: {
  total: number;
  tableHref: string;
  boardHref: string;
  departments: DepartmentTasks[];
  projects: WorkProject[];
  defaultDepartment?: string;
}) {
  const t = useT();
  const [creating, setCreating] = useState(false);
  const canCreate = departments.some((d) => d.hasTasks) || projects.length > 0;

  return (
    <>
      <header className="hidden items-start justify-between gap-6 lg:flex">
        <div className="min-w-0">
          <nav aria-label={t("Навигация")} className="flex items-center gap-1.5 text-xs text-subtle">
            <Link href="/" className="hover:text-ink">
              OneBase
            </Link>
            <span aria-hidden>/</span>
            <Link href="/tasks" className="hover:text-ink">
              {t("Задачи")}
            </Link>
          </nav>
          <div className="mt-1.5 flex items-center gap-3">
            <h1 className="text-[28px] font-bold leading-tight tracking-tight text-ink">{t("Канбан")}</h1>
            <span className="rounded-full bg-surface-2 px-2.5 py-0.5 text-xs font-semibold tabular-nums text-ink-3">
              {t(plural(total, "{n} задача", "{n} задачи", "{n} задач"), { n: total })}
            </span>
          </div>
          <p className="mt-1 text-[13px] text-ink-3">{t("Перетащите карточку в другую колонку, чтобы сменить статус")}</p>
        </div>
        <div className="flex shrink-0 items-center gap-2.5 pt-5">
          <ViewSwitch current="board" tableHref={tableHref} boardHref={boardHref} />
          {canCreate && (
            <button type="button" onClick={() => setCreating(true)} className="inline-flex h-10 items-center gap-2 rounded-xl bg-accent px-4 text-sm font-semibold text-white hover:brightness-110">
              <Plus className="size-4" aria-hidden />
              {t("Новая задача")}
            </button>
          )}
        </div>
      </header>

      <MobileTasksHeader onCreate={canCreate ? () => setCreating(true) : null} />

      {creating && <TaskForm open onClose={() => setCreating(false)} departments={departments} projects={projects} defaultDepartment={defaultDepartment} />}
    </>
  );
}

/** Шапка раздела «Задачи» на телефоне: заголовок, поиск, «+» и вкладки раздела чипами (активная — светлая). */
function MobileTasksHeader({ onCreate }: { onCreate: (() => void) | null }) {
  const t = useT();
  const [work, setWork] = useState<{ heads: boolean; pendingSuggestions: number } | null>(null);
  useEffect(() => {
    let live = true;
    fetch("/bff/api/work/nav")
      .then((r) => (r.ok ? r.json() : null))
      .then((data) => live && data && setWork(data))
      .catch(() => undefined);
    return () => {
      live = false;
    };
  }, []);
  const tabs = taskTabs.filter((tab) => !tab.headsOnly || work?.heads);
  const round = "grid size-11 shrink-0 place-items-center rounded-full";

  return (
    <div className="lg:hidden">
      <div className="flex items-center gap-2.5 px-4 pb-3 pt-[calc(env(safe-area-inset-top)+14px)]">
        <h1 className="min-w-0 flex-1 truncate text-[28px] font-bold leading-tight tracking-tight text-ink">{t("Задачи")}</h1>
        <button type="button" onClick={() => document.getElementById("task-search")?.focus()} aria-label={t("Поиск задач")} className={`${round} border border-line bg-surface text-ink-2`}>
          <Search className="size-5" aria-hidden />
        </button>
        {onCreate && (
          <button type="button" onClick={onCreate} aria-label={t("Новая задача")} className={`${round} bg-accent text-white shadow-[0_8px_20px_rgb(61_123_255/0.35)]`}>
            <Plus className="size-6" aria-hidden />
          </button>
        )}
      </div>
      <nav aria-label={t("Разделы задач")} className="no-scrollbar flex gap-2 overflow-x-auto px-4 pb-3">
        {tabs.map((tab) => {
          const on = tab.key === "board";
          return (
            <Link
              key={tab.key}
              href={tab.href}
              aria-current={on ? "page" : undefined}
              className={`inline-flex h-10 shrink-0 items-center gap-1.5 rounded-full border px-4 text-sm font-medium ${on ? "border-ink bg-ink text-ai-bg" : "border-line bg-surface text-ink-2"}`}
            >
              {t(tab.short ?? tab.label)}
              {tab.key === "suggestions" && (work?.pendingSuggestions ?? 0) > 0 && (
                <span className="rounded-full bg-violet-soft px-1.5 text-[12px] font-semibold tabular-nums text-violet">{work?.pendingSuggestions}</span>
              )}
            </Link>
          );
        })}
      </nav>
    </div>
  );
}
