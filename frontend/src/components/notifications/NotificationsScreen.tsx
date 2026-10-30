"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { CheckCheck, ChevronLeft, ChevronRight, Loader2, Settings2, SlidersHorizontal, Undo2 } from "lucide-react";
import { PermissionsSheet } from "@/components/shell/PermissionsSheet";
import { useT } from "@/i18n/client";
import { filterCount, filterLabel, filtersFor, groupByDay, type Filter, type NotificationItem } from "@/lib/notifications";
import { loadMore, loadNotifications, markAllRead, markRead, removeWithUndo, useNotifications } from "@/lib/notifications-store";
import { DeviceRows, EmptyNotifications, NotificationRow, PushPrompt, useGeoState, usePushState } from "./parts";

/** Пока экран открыт, список обновляется чаще, чем счётчик колокольчика. */
const POLL_MS = 15_000;

/**
 * Экран «Уведомления» (ТЗ notifications): на телефоне — вместо глобальной шапки, по колокольчику; на компьютере — «Все уведомления»
 * из панели (та же разметка). Карточка push, вкладки-чипы, группы «Сегодня / Вчера / Ранее» карточками, жесты (влево — прочитано, вправо
 * — удалить с отменой), подгрузка при прокрутке, обновление жестом вниз; пусто — колокольчик, карточка push и «Это устройство».
 */
export function NotificationsScreen({ permissions }: { permissions: readonly string[] }) {
  const t = useT();
  const router = useRouter();
  const { filter, page, counts, loading, loadingMore, error, fresh } = useNotifications();
  const push = usePushState();
  const geo = useGeoState();
  const [settings, setSettings] = useState(false);
  const [undo, setUndo] = useState<{ cancel: () => void } | null>(null);
  const sentinel = useRef<HTMLDivElement>(null);
  const filters = filtersFor(permissions);

  useEffect(() => {
    void loadNotifications(filters.includes(filter) ? filter : "all");
    const reload = () => void loadNotifications(undefined, true);
    const timer = setInterval(() => document.visibilityState === "visible" && reload(), POLL_MS);
    const onVisible = () => document.visibilityState === "visible" && reload();
    // Обновление жестом вниз (PullToRefresh) и push, пришедший, пока экран открыт.
    const onMessage = (e: MessageEvent) => (e.data as { type?: string } | null)?.type === "push-received" && reload();
    window.addEventListener("onebase:refresh", reload);
    document.addEventListener("visibilitychange", onVisible);
    navigator.serviceWorker?.addEventListener("message", onMessage);
    return () => {
      clearInterval(timer);
      window.removeEventListener("onebase:refresh", reload);
      document.removeEventListener("visibilitychange", onVisible);
      navigator.serviceWorker?.removeEventListener("message", onMessage);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const el = sentinel.current;
    if (!el) return;
    const observer = new IntersectionObserver((entries) => entries.some((e) => e.isIntersecting) && void loadMore(), { rootMargin: "200px" });
    observer.observe(el);
    return () => observer.disconnect();
  }, [page?.nextCursor]);

  useEffect(() => {
    if (!undo) return;
    const timer = setTimeout(() => setUndo(null), 5000);
    return () => clearTimeout(timer);
  }, [undo]);

  const open = (n: NotificationItem) => {
    if (!n.read) void markRead(n.id, true);
    if (n.href) router.push(n.href);
  };

  const remove = (n: NotificationItem) => setUndo({ cancel: removeWithUndo(n.id) });

  const back = () => (window.history.length > 1 ? router.back() : router.push("/"));
  const unread = counts?.unread ?? 0;
  const empty = page !== null && page.items.length === 0 && filter === "all" && !loading;
  const groups = page ? groupByDay(page.items) : [];

  return (
    <div className="notif-ui min-h-full bg-ai-bg text-ink">
      <header className="sticky top-0 z-10 flex items-center gap-2.5 border-b border-line bg-ai-bg/90 px-3 pb-2 pt-[calc(env(safe-area-inset-top)+8px)] backdrop-blur-xl lg:static lg:mx-auto lg:max-w-[760px] lg:border-0 lg:bg-transparent lg:px-4 lg:pt-6 lg:backdrop-blur-none">
        <button type="button" onClick={back} aria-label={t("Назад")} className="grid size-10 shrink-0 place-items-center rounded-full border border-line bg-surface text-ink-2">
          <ChevronLeft className="size-5" aria-hidden />
        </button>
        <h1 className="flex min-w-0 flex-1 items-center gap-2 text-xl font-bold">
          <span className="truncate">{t("Уведомления")}</span>
          {unread > 0 && <span className="shrink-0 rounded-full bg-accent-fill px-2 py-0.5 text-[12px] font-bold tabular-nums text-white">{unread}</span>}
        </h1>
        {unread > 0 && (
          <button type="button" onClick={() => void markAllRead()} aria-label={t("Прочитать все")} className="grid size-10 shrink-0 place-items-center rounded-full border border-line bg-surface text-link">
            <CheckCheck className="size-[18px]" aria-hidden />
          </button>
        )}
        <button type="button" onClick={() => setSettings(true)} aria-label={t("Настройки уведомлений")} className="grid size-10 shrink-0 place-items-center rounded-full border border-line bg-surface text-ink-2">
          <Settings2 className="size-[18px]" aria-hidden />
        </button>
      </header>

      <div className="mx-auto flex max-w-[760px] flex-col gap-4 px-4 pb-8 pt-2">
        {empty ? (
          <>
            <EmptyNotifications />
            {push.visible() && <PushPrompt push={push} variant="card" dismissible={false} snoozable onHelp={() => setSettings(true)} />}
            <section>
              <h2 className="mb-2.5 px-1 text-[11px] font-bold uppercase tracking-[0.9px] text-subtle">{t("Это устройство")}</h2>
              <DeviceRows push={push.state} geo={geo} onOpen={() => setSettings(true)} />
            </section>
          </>
        ) : (
          <>
            {push.visible() && <PushPrompt push={push} variant="card" dismissible onHelp={() => setSettings(true)} />}

            <div role="tablist" aria-label={t("Фильтр уведомлений")} className="-mx-4 flex gap-2 overflow-x-auto px-4 no-scrollbar">
              {filters.map((f: Filter) => {
                const n = filterCount(f, counts);
                const active = filter === f;
                return (
                  <button
                    key={f}
                    type="button"
                    role="tab"
                    aria-selected={active}
                    onClick={() => void loadNotifications(f)}
                    className={`inline-flex min-h-[40px] shrink-0 items-center gap-1.5 rounded-full border px-4 text-sm font-medium ${
                      active ? "border-ink bg-ink text-ai-bg" : "border-line bg-surface text-ink-2"
                    }`}
                  >
                    {t(filterLabel[f])}
                    {n !== null && n > 0 && <span className={`text-[13px] tabular-nums ${active ? "opacity-60" : "text-subtle"}`}>{n}</span>}
                  </button>
                );
              })}
            </div>

            {loading && !page && (
              <div className="grid place-items-center py-16 text-ink-3" role="status">
                <Loader2 className="size-6 animate-spin" aria-label={t("Загрузка")} />
              </div>
            )}
            {error && <p className="rounded-xl bg-bad-soft px-3 py-2 text-[13px] text-bad">{t(error)}</p>}
            {page && page.items.length === 0 && !loading && <p className="py-10 text-center text-sm text-ink-3">{t("Здесь пока пусто")}</p>}

            {groups.map((g) => (
              <section key={g.label} aria-label={t(g.label)}>
                <h2 className="mb-2.5 px-1 text-[11px] font-bold uppercase tracking-[0.9px] text-subtle">{t(g.label)}</h2>
                <ul className="divide-y divide-line overflow-hidden rounded-[18px] border border-line bg-surface">
                  {g.items.map((n) => (
                    <li key={n.id}>
                      <NotificationRow n={n} variant="screen" fresh={fresh.includes(n.id)} onOpen={open} onRemove={remove} />
                    </li>
                  ))}
                </ul>
              </section>
            ))}
            <div ref={sentinel} className="h-1" />
            {loadingMore && (
              <div className="grid place-items-center py-3 text-ink-3">
                <Loader2 className="size-5 animate-spin" aria-label={t("Загрузка")} />
              </div>
            )}

            <button type="button" onClick={() => setSettings(true)} className="flex min-h-[64px] items-center gap-3 rounded-[18px] border border-line bg-surface px-4 py-3 text-left">
              <span className="grid size-[34px] shrink-0 place-items-center rounded-[10px] bg-surface-2 text-ink-2">
                <SlidersHorizontal className="size-4" aria-hidden />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block text-sm font-semibold">{t("Уведомления и геолокация")}</span>
                <span className="block text-[12px] text-ink-3">{t("Настройки для этого устройства")}</span>
              </span>
              <ChevronRight className="size-4 text-ink-3" aria-hidden />
            </button>
          </>
        )}
      </div>

      {undo && (
        <div role="status" aria-live="polite" className="fixed inset-x-4 bottom-[calc(env(safe-area-inset-bottom)+110px)] z-40 mx-auto flex max-w-[420px] items-center gap-3 rounded-2xl border border-line-strong bg-surface px-4 py-3 shadow-xl lg:bottom-6">
          <span className="flex-1 text-sm">{t("Уведомление удалено")}</span>
          <button
            type="button"
            onClick={() => {
              undo.cancel();
              setUndo(null);
            }}
            className="inline-flex items-center gap-1.5 text-sm font-semibold text-link"
          >
            <Undo2 className="size-4" aria-hidden />
            {t("Отменить")}
          </button>
        </div>
      )}

      <PermissionsSheet open={settings} onClose={() => setSettings(false)} appName="OneBase" preferences />
    </div>
  );
}
