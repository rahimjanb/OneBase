"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { ArrowDown, ArrowUp, ChevronLeft, ChevronRight, Search, X } from "lucide-react";
import { field } from "@/components/ai/form";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  shortDate,
  workPriorities,
  workPriorityLabel,
  workStatuses,
  workStatusLabel,
  type DepartmentTasks,
  type PageResult,
  type WorkAssignee,
  type WorkTask,
} from "@/lib/tasks";
import { PriorityBadge, ProgressBar, StatusBadge, TaskFlags } from "./TaskBadges";

/** Компактные поля фильтра: в один ряд на компьютере, переносятся на телефоне (у общего класса field — ширина 100%). */
const control =
  "h-10 rounded-lg border border-line bg-surface px-3 text-sm text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const select = `${control} max-w-full pr-8`;

/** Поля фильтра, которые показывает страница (у «Моих задач» нет фильтра по исполнителю, у отдела — по отделу). */
export type FilterSet = { department?: boolean; assignee?: boolean; status?: "all" | "archive" };

/**
 * Фильтры таблицы: поиск, статус, приоритет, отдел, исполнитель (сотрудники выбранного отдела), период по дате создания или сроку.
 * Состояние — в адресе страницы: ссылкой можно поделиться, «назад» возвращает прежний фильтр. Поиск применяется с задержкой.
 */
export function TaskFilters({ departments, show, fixedDepartment }: { departments: DepartmentTasks[]; show: FilterSet; fixedDepartment?: string }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("search") ?? "");
  const [assignees, setAssignees] = useState<WorkAssignee[]>([]);
  const department = fixedDepartment ?? params.get("department") ?? "";

  const apply = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    next.delete("page");
    router.push(`${pathname}${next.size ? `?${next}` : ""}`);
  };
  // Сброс фильтров сохраняет вкладку страницы (у страницы отдела — tab).
  const reset = () => {
    const tab = params.get("tab");
    router.push(`${pathname}${tab ? `?tab=${tab}` : ""}`);
  };

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if ((params.get("search") ?? "") !== search.trim()) apply({ search: search.trim() || null });
    }, 400);
    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  useEffect(() => {
    if (!show.assignee || !department) {
      setAssignees([]);
      return;
    }
    let alive = true;
    bff<WorkAssignee[]>(`work/assignees?department=${encodeURIComponent(department)}`)
      .then((list) => alive && setAssignees(list))
      .catch(() => alive && setAssignees([]));
    return () => {
      alive = false;
    };
  }, [department, show.assignee]);

  const statusOptions =
    show.status === "archive"
      ? [
          { value: "", label: "Все в архиве и завершённые" },
          { value: "Done", label: workStatusLabel.Done },
          { value: "Cancelled", label: workStatusLabel.Cancelled },
        ]
      : [
          { value: "", label: "Все статусы" },
          { value: "open", label: "Открытые" },
          { value: "overdue", label: "Просроченные" },
          { value: "blocked", label: "Заблокированные" },
          ...workStatuses.map((s) => ({ value: s, label: workStatusLabel[s] })),
        ];
  const hasFilters = ["search", "status", "priority", "department", "assigneeId", "from", "to"].some((k) => params.get(k));

  return (
    <div className="mb-4 flex flex-wrap items-center gap-2">
      <label className="relative block min-w-56 flex-1 sm:max-w-80">
        <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" />
        <input className={`${field} pl-9`} value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Поиск: название, описание, исполнитель")} aria-label={t("Поиск задач")} />
      </label>
      <select className={select} value={params.get("status") ?? ""} onChange={(e) => apply({ status: e.target.value || null })} aria-label={t("Статус")}>
        {statusOptions.map((o) => (
          <option key={o.value} value={o.value}>
            {t(o.label)}
          </option>
        ))}
      </select>
      <select className={select} value={params.get("priority") ?? ""} onChange={(e) => apply({ priority: e.target.value || null })} aria-label={t("Приоритет")}>
        <option value="">{t("Любой приоритет")}</option>
        {workPriorities.map((p) => (
          <option key={p} value={p}>
            {t(workPriorityLabel[p])}
          </option>
        ))}
      </select>
      {show.department && (
        <select className={select} value={department} onChange={(e) => apply({ department: e.target.value || null, assigneeId: null })} aria-label={t("Отдел")}>
          <option value="">{t("Все отделы")}</option>
          {departments
            .filter((d) => d.hasTasks)
            .map((d) => (
              <option key={d.code} value={d.code}>
                {d.name}
              </option>
            ))}
        </select>
      )}
      {show.assignee && department && (
        <select className={select} value={params.get("assigneeId") ?? ""} onChange={(e) => apply({ assigneeId: e.target.value || null })} aria-label={t("Исполнитель")}>
          <option value="">{t("Любой исполнитель")}</option>
          {[
            { label: "Сотрудники отдела", list: assignees.filter((a) => a.inDepartment) },
            { label: "Другие пользователи OneBase", list: assignees.filter((a) => !a.inDepartment) },
          ]
            .filter((g) => g.list.length > 0)
            .map((g) => (
              <optgroup key={g.label} label={t(g.label)}>
                {g.list.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name}
                  </option>
                ))}
              </optgroup>
            ))}
        </select>
      )}
      <select className={select} value={params.get("dateField") ?? "created"} onChange={(e) => apply({ dateField: e.target.value === "due" ? "due" : null })} aria-label={t("Период по")}>
        <option value="created">{t("Создана")}</option>
        <option value="due">{t("Срок")}</option>
      </select>
      <input type="date" className={control} value={params.get("from") ?? ""} onChange={(e) => apply({ from: e.target.value || null })} aria-label={t("С даты")} />
      <input type="date" className={control} value={params.get("to") ?? ""} onChange={(e) => apply({ to: e.target.value || null })} aria-label={t("По дату")} />
      {hasFilters && (
        <button
          type="button"
          onClick={() => {
            setSearch("");
            reset();
          }}
          className="inline-flex h-10 items-center gap-1 rounded-lg px-2 text-sm text-ink-3 hover:text-ink"
        >
          <X className="size-4" /> {t("Сбросить")}
        </button>
      )}
    </div>
  );
}

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
                  <Link href={`/tasks/task/${task.id}`} className="font-medium text-ink hover:text-accent-strong">
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
            <Link href={`/tasks/task/${task.id}`} className="block rounded-xl border border-line bg-surface p-3.5 hover:border-accent/40">
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
