"use client";

import { useSyncExternalStore } from "react";
import { tNow } from "@/i18n/client";
import {
  applyRead,
  applyReadAll,
  applyRemove,
  freshIds,
  type Filter,
  type NotificationCounts,
  type NotificationItem,
  type NotificationPage,
} from "./notifications";

/**
 * Общее состояние уведомлений: колокольчик, панель на компьютере и экран на телефоне видят одни и те же список и счётчики — «Прочитать
 * все», чтение по нажатию и жесты сразу меняют их везде. Список загружается страницами по курсору; счётчики — вместе со списком или
 * отдельно (колокольчик). Сервер — /api/notifications (NotificationsController).
 */
type State = {
  filter: Filter;
  page: NotificationPage | null;
  counts: NotificationCounts | null;
  loading: boolean;
  loadingMore: boolean;
  error: string | null;
  /** Только что пришедшие — мягкая подсветка сверху. */
  fresh: string[];
};

let state: State = { filter: "all", page: null, counts: null, loading: false, loadingMore: false, error: null, fresh: [] };
const listeners = new Set<() => void>();
const set = (patch: Partial<State>) => {
  state = { ...state, ...patch };
  listeners.forEach((l) => l());
};

const subscribe = (l: () => void) => {
  listeners.add(l);
  return () => listeners.delete(l);
};

const server: State = { filter: "all", page: null, counts: null, loading: false, loadingMore: false, error: null, fresh: [] };

export function useNotifications(): State {
  return useSyncExternalStore(subscribe, () => state, () => server);
}

async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/bff/api/notifications${path}`, { cache: "no-store", ...init, headers: { "Content-Type": "application/json", ...init?.headers } });
  if (!response.ok) {
    let message = "";
    try {
      const json = await response.json();
      message = json.error ?? json.detail ?? json.title ?? "";
    } catch {
      // не JSON
    }
    throw new Error(message ? tNow(message) : tNow("Ошибка {status}", { status: response.status }));
  }
  return (response.status === 204 ? undefined : await response.json()) as T;
}

const pageSize = 30;
let loadRun = 0;

/** Загрузить первую страницу вкладки. silent — без индикатора (опрос, возврат в приложение); новые сверху подсвечиваются. */
export async function loadNotifications(filter: Filter = state.filter, silent = false) {
  const run = ++loadRun;
  const same = filter === state.filter && state.page !== null;
  set({ filter, loading: !silent || !same, error: null, ...(same ? {} : { page: null }) });
  try {
    const page = await api<NotificationPage>(`?filter=${filter}&take=${pageSize}`);
    if (run !== loadRun) return;
    const old = same ? state.page : null;
    const before = old?.items ?? [];
    // Подгруженные дальше страницы при тихом обновлении не теряются.
    const extra = old && old.items.length > pageSize ? old.items.slice(pageSize).filter((n) => !page.items.some((x) => x.id === n.id)) : [];
    set({
      page: { ...page, items: [...page.items, ...extra], nextCursor: extra.length > 0 ? old!.nextCursor : page.nextCursor },
      counts: page.counts,
      loading: false,
      fresh: silent ? freshIds(before, page.items) : [],
    });
  } catch (e) {
    if (run === loadRun) set({ loading: false, error: (e as Error).message });
  }
}

/** Следующая страница (прокрутка до конца). */
export async function loadMore() {
  const page = state.page;
  if (!page?.nextCursor || state.loadingMore) return;
  set({ loadingMore: true });
  try {
    const next = await api<NotificationPage>(`?filter=${state.filter}&take=${pageSize}&cursor=${encodeURIComponent(page.nextCursor)}`);
    const known = new Set(state.page?.items.map((n) => n.id));
    set({
      page: { ...next, items: [...(state.page?.items ?? []), ...next.items.filter((n) => !known.has(n.id))] },
      counts: next.counts,
      loadingMore: false,
    });
  } catch (e) {
    set({ loadingMore: false, error: (e as Error).message });
  }
}

/** Только счётчики — для колокольчика, пока список не открыт. */
export async function refreshCounts() {
  try {
    const counts = await api<NotificationCounts>("/counts");
    set({ counts });
  } catch {
    // нет связи — покажем, что было
  }
}

/** Свежие непрочитанные для всплывающего окна (не меняют открытый список). */
export async function peekUnread(take = 10): Promise<NotificationItem[]> {
  const page = await api<NotificationPage>(`?filter=unread&take=${take}`);
  set({ counts: page.counts });
  return page.items;
}

const updateCounts = (counts: NotificationCounts | null, fn: (c: NotificationCounts) => NotificationCounts) => (counts ? fn(counts) : counts);

export async function markRead(id: string, read = true) {
  const page = state.page;
  const item = page?.items.find((n) => n.id === id);
  if (page && item && item.read !== read) {
    const next = applyRead(page, id, read);
    set({ page: next, counts: updateCounts(state.counts, (c) => ({ ...c, unread: next.counts.unread })) });
  } else if (!page || !item) {
    set({ counts: updateCounts(state.counts, (c) => ({ ...c, unread: Math.max(0, c.unread + (read ? -1 : 1)) })) });
  }
  try {
    await api(`/${id}/${read ? "read" : "unread"}`, { method: "POST" });
    if (state.filter === "unread" && read) set({ page: state.page ? applyRemove(state.page, id) : null });
  } catch (e) {
    set({ error: (e as Error).message });
  }
}

export async function markAllRead() {
  if (state.page) set({ page: applyReadAll(state.page) });
  set({ counts: updateCounts(state.counts, (c) => ({ ...c, unread: 0 })) });
  try {
    await api("/read-all", { method: "POST" });
    if (state.filter === "unread") await loadNotifications("unread", true);
  } catch (e) {
    set({ error: (e as Error).message });
  }
}

/** Удаление с отменой: строка исчезает сразу, запрос уходит через 5 секунд, если не нажали «Отменить». */
export function removeWithUndo(id: string): () => void {
  const page = state.page;
  if (!page) return () => {};
  const before = page;
  const countsBefore = state.counts;
  const next = applyRemove(page, id);
  set({ page: next, counts: updateCounts(state.counts, (c) => ({ ...c, all: next.counts.all, unread: next.counts.unread })) });
  let undone = false;
  const timer = setTimeout(() => {
    if (undone) return;
    void api(`/${id}`, { method: "DELETE" }).catch((e) => set({ error: (e as Error).message }));
  }, 5000);
  return () => {
    undone = true;
    clearTimeout(timer);
    set({ page: before, counts: countsBefore });
  };
}

/** Действие без перехода (extend_due — новый срок задачи). Ошибка сервера — текстом для человека. */
export async function act(id: string, action: string, due?: string) {
  await api(`/${id}/action`, { method: "POST", body: JSON.stringify({ action, due: due ?? null }) });
  if (state.page) {
    const next = applyRead(state.page, id, true);
    set({ page: next, counts: updateCounts(state.counts, (c) => ({ ...c, unread: next.counts.unread })) });
  }
}

export const clearError = () => set({ error: null });

/** Что присылать: отключённые разделы. */
export type Preferences = { muted: string[]; categories: string[] };
export const loadPreferences = () => api<Preferences>("/preferences");
export async function savePreferences(muted: string[]) {
  const prefs = await api<Preferences>("/preferences", { method: "PUT", body: JSON.stringify({ muted }) });
  await loadNotifications(state.filter, true);
  return prefs;
}
