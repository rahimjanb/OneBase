"use client";

/**
 * Список, из которого открыли задачу (ТЗ tasks-kanban-detail): канбан или таблица запоминают адрес с фильтрами, вкладку раздела и порядок
 * задач. Карточка по нему показывает «1 из 5», листает соседние задачи (↑↓, J/K), подсвечивает вкладку, откуда пришли, а «Назад»
 * возвращает в тот же список с фильтрами и прокруткой. Хранится в sessionStorage — у каждой вкладки браузера свой.
 */
export type TaskListContext = { href: string; tab: string; ids: string[] };

const LIST = "onebase-task-list";
const OPENED = "onebase-task-opened";

/** Вкладка раздела «Задачи» по адресу списка. */
export function tabOfPath(pathname: string): string {
  if (pathname.startsWith("/tasks/board")) return "board";
  if (pathname.startsWith("/tasks/my")) return "my";
  if (pathname.startsWith("/tasks/archive")) return "archive";
  if (pathname.startsWith("/tasks/all")) return "all";
  return "department";
}

export function rememberTaskList(href: string, ids: string[]) {
  try {
    sessionStorage.setItem(LIST, JSON.stringify({ href, tab: tabOfPath(href), ids } satisfies TaskListContext));
  } catch {
    // приватный режим — без «1 из 5»
  }
}

export function readTaskList(): TaskListContext | null {
  try {
    const raw = sessionStorage.getItem(LIST);
    const value = raw ? (JSON.parse(raw) as TaskListContext) : null;
    return value && typeof value.href === "string" && Array.isArray(value.ids) ? value : null;
  } catch {
    return null;
  }
}

/** Задачу открыли из списка (а не по ссылке из уведомления): «Назад» — шаг назад по истории, прокрутка списка восстановится. */
export function markOpenedFromList(id: string) {
  try {
    sessionStorage.setItem(OPENED, id);
  } catch {
    // без отметки «Назад» ведёт на адрес списка
  }
}

export function openedFromList(): string | null {
  try {
    return sessionStorage.getItem(OPENED);
  } catch {
    return null;
  }
}
