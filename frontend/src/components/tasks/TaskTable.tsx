"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { useEffect } from "react";
import { markOpenedFromList, rememberTaskList } from "@/lib/task-list";
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight } from "lucide-react";
import { useT } from "@/i18n/client";
import { shortDate, type PageResult, type WorkTask } from "@/lib/tasks";
import { PriorityBadge, ProgressBar, StatusBadge, TaskFlags } from "./TaskBadges";

/** Фильтры таблицы и канбана — общие (TaskFilterBar); старый импорт отсюда работает. */
export { TaskFilters, type FilterSet } from "./TaskFilterBar";

const columns: { key: string; label: string; sort?: string; className?: string }[] = [
  { key: "title", label: "Задача", sort: "title" },
  { key: "department", label: "Отдел · проект", sort: "department" },
  { key: "author", label: "Постановщик" },
  { key: "assignee", label: "Исполнитель", sort: "assignee" },
  { key: "priority", label: "Приоритет", sort: "priority" },
  { key: "status", label: "Статус", sort: "status" },
  { key: "created", label: "Создана", sort: "created" },
  { key: "due", label: "Срок", sort: "due" },
  { key: "progress", label: "Выполнение", sort: "progress" },
];

/**
 * Таблица задач: на компьютере — колонки с сортировкой по клику на заголовок, на телефоне — карточки. Строка открывает карточку задачи.
 * Просрочка — красный срок и значок, блокировка — значок с причиной в подсказке.
 */
export function TaskTable({ data, empty }: { data: PageResult<WorkTask>; empty: string }) {
  const t = useT();
  const pathname = usePathname();
  const params = useSearchParams();
  const sort = params.get("sort");
  const desc = params.get("desc") === "true";
  const href = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    return `${pathname}${next.size ? `?${next}` : ""}`;
  };
  const pages = Math.max(1, Math.ceil(data.total / data.pageSize));
  // Порядок строк — для «1 из N» и листания в карточке задачи; «Назад» из карточки вернёт на эту страницу с фильтрами.
  const query = params.toString();
  const ids = data.items.map((x) => x.id).join(",");
  useEffect(() => rememberTaskList(`${pathname}${query ? `?${query}` : ""}`, ids ? ids.split(",") : []), [pathname, query, ids]);

  if (data.items.length === 0) {
    return <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">{t(empty)}</p>;
  }

  return (
    <>
      <div className="hidden overflow-x-auto rounded-xl border border-line bg-surface lg:block">
        <table className="w-full min-w-[1100px] text-sm">
          <thead>
            <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
              {columns.map((c) => (
                <th key={c.key} className="px-3 py-2.5 font-semibold">
                  {c.sort ? (
                    <Link
                      href={href({ sort: c.sort, desc: sort === c.sort && !desc ? "true" : null, page: null })}
                      className="inline-flex items-center gap-1 hover:text-ink"
                      aria-sort={sort === c.sort ? (desc ? "descending" : "ascending") : undefined}
                    >
                      {t(c.label)}
                      {sort === c.sort && (desc ? <ArrowDown className="size-3" /> : <ArrowUp className="size-3" />)}
                    </Link>
                  ) : (
                    t(c.label)
                  )}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.items.map((task) => (
              <tr key={task.id} className="border-b border-line align-top last:border-0 hover:bg-muted/50">
                <td className="max-w-80 px-3 py-2.5">
                  <Link href={`/tasks/task/${task.id}`} onClick={() => markOpenedFromList(task.id)} className="font-medium text-ink hover:text-accent-strong">
                    {task.title}
                  </Link>
                  <div className="mt-1">
                    <TaskFlags task={task} />
                  </div>
                </td>
                <td className="px-3 py-2.5 text-ink-2">
                  {task.departmentName}
                  {task.projectName && <div className="text-xs text-ink-3">{task.projectName}</div>}
                </td>
                <td className="px-3 py-2.5 text-ink-2">{task.createdByName ?? "—"}</td>
                <td className="px-3 py-2.5 text-ink-2">{task.assigneeName ?? <span className="text-ink-3">{t("не назначен")}</span>}</td>
                <td className="px-3 py-2.5">
                  <PriorityBadge priority={task.priority} />
                </td>
                <td className="px-3 py-2.5">
                  <StatusBadge status={task.status} />
                </td>
                <td className="px-3 py-2.5 tabular-nums text-ink-2">{shortDate(task.createdAt)}</td>
                <td className={`px-3 py-2.5 tabular-nums ${task.overdue ? "font-semibold text-bad" : "text-ink-2"}`}>{task.dueDate ? shortDate(task.dueDate) : "—"}</td>
                <td className="px-3 py-2.5">
                  <ProgressBar value={task.progress} compact />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <ul className="space-y-2 lg:hidden">
        {data.items.map((task) => (
          <li key={task.id}>
            <Link href={`/tasks/task/${task.id}`} onClick={() => markOpenedFromList(task.id)} className="block rounded-xl border border-line bg-surface p-3.5 hover:border-accent/40">
              <div className="flex items-start justify-between gap-2">
                <span className="font-medium text-ink">{task.title}</span>
                <StatusBadge status={task.status} />
              </div>
              <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-3">
                <PriorityBadge priority={task.priority} />
                <span>{task.departmentName}</span>
                <span>{task.assigneeName ?? t("не назначен")}</span>
                {task.dueDate && <span className={task.overdue ? "font-semibold text-bad" : ""}>{t("срок {date}", { date: shortDate(task.dueDate) })}</span>}
              </div>
              <div className="mt-2 flex items-center justify-between gap-3">
                <TaskFlags task={task} />
                <ProgressBar value={task.progress} compact />
              </div>
            </Link>
          </li>
        ))}
      </ul>

      <div className="mt-3 flex flex-wrap items-center justify-between gap-3 text-sm text-ink-2">
        <span>{t("Задач: {n}", { n: data.total })}</span>
        {pages > 1 && (
          <div className="flex items-center gap-2">
            {data.page > 1 && (
              <Link href={href({ page: String(data.page - 1) })} className="inline-flex h-9 items-center gap-1 rounded-lg border border-line bg-surface px-3 hover:bg-muted">
                <ChevronLeft className="size-4" /> {t("Назад")}
              </Link>
            )}
            <span>{t("{page} из {pages}", { page: data.page, pages })}</span>
            {data.page < pages && (
              <Link href={href({ page: String(data.page + 1) })} className="inline-flex h-9 items-center gap-1 rounded-lg border border-line bg-surface px-3 hover:bg-muted">
                {t("Дальше")} <ChevronRight className="size-4" />
              </Link>
            )}
          </div>
        )}
      </div>
    </>
  );
}
