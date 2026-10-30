"use client";

import Link from "next/link";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { ArrowRight, CheckCheck, Loader2, Settings2, SlidersHorizontal } from "lucide-react";
import { useT } from "@/i18n/client";
import { filterCount, filterLabel, filtersFor, groupByDay, plural, type Filter, type NotificationItem } from "@/lib/notifications";
import { loadMore, loadNotifications, markAllRead, useNotifications } from "@/lib/notifications-store";
import { EmptyNotifications, NotificationRow, PushPrompt, usePushState } from "./parts";

/**
 * Панель уведомлений на компьютере (ТЗ notifications): 420px под колокольчиком, затемнение под шапкой, шапка и футер закреплены, список
 * прокручивается внутри и подгружает следующие страницы. Esc и нажатие вне панели закрывают её, фокус возвращается на колокольчик (Bell).
 */
export function NotificationsPanel({
  anchor,
  permissions,
  onClose,
  onOpenItem,
  onSettings,
}: {
  anchor: DOMRect;
  permissions: readonly string[] | null;
  onClose: () => void;
  onOpenItem: (n: NotificationItem) => void;
  onSettings: () => void;
}) {
  const t = useT();
  const { filter, page, counts, loading, loadingMore, error, fresh } = useNotifications();
  const push = usePushState();
  const panel = useRef<HTMLDivElement>(null);
  const sentinel = useRef<HTMLDivElement>(null);
  const [top, setTop] = useState(64);
  const filters = filtersFor(permissions);

  // Затемнение — под шапкой; панель — у правого края колокольчика, не ближе 24px к краю окна.
  useLayoutEffect(() => {
    setTop(document.querySelector("[data-topbar]")?.getBoundingClientRect().bottom ?? anchor.bottom);
  }, [anchor]);

  useEffect(() => {
    void loadNotifications(filters.includes(filter) ? filter : "all");
    panel.current?.focus({ preventScroll: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Esc закрывает; Tab не уходит из панели (фокус-ловушка).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.preventDefault();
        onClose();
        return;
      }
      if (e.key !== "Tab" || !panel.current) return;
      const focusable = [...panel.current.querySelectorAll<HTMLElement>('a[href], button:not([disabled]), [tabindex="0"], input')].filter((el) => el.offsetParent !== null);
      if (focusable.length === 0) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (e.shiftKey && (document.activeElement === first || document.activeElement === panel.current)) {
        e.preventDefault();
        last.focus();
      } else if (!e.shiftKey && document.activeElement === last) {
        e.preventDefault();
        first.focus();
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  // Следующая страница — когда низ списка показался.
  useEffect(() => {
    const el = sentinel.current;
    if (!el) return;
    const observer = new IntersectionObserver((entries) => entries.some((e) => e.isIntersecting) && void loadMore(), { root: el.parentElement, rootMargin: "120px" });
    observer.observe(el);
    return () => observer.disconnect();
  }, [page?.nextCursor]);

  const unread = counts?.unread ?? 0;
  const groups = page ? groupByDay(page.items) : [];
  const right = Math.max(24, window.innerWidth - anchor.right);

  return (
    <>
      <button type="button" aria-label={t("Закрыть уведомления")} tabIndex={-1} onClick={onClose} className="fixed inset-x-0 bottom-0 z-40 cursor-default bg-[#03060d]/70" style={{ top }} />
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-label={t("Уведомления")}
        tabIndex={-1}
        className="notif-ui notif-panel-in fixed z-50 flex w-[420px] flex-col overflow-hidden rounded-[18px] border border-line-strong bg-surface text-ink shadow-[0_24px_60px_rgb(0_0_0/0.7)] outline-none"
        style={{ top: anchor.bottom + 8, right, maxHeight: "min(640px, calc(100vh - 100px))" }}
      >
        <div className="flex shrink-0 items-center gap-2 pb-3 pl-[18px] pr-4 pt-4">
          <h2 className="text-base font-bold">{t("Уведомления")}</h2>
          {unread > 0 && <span className="rounded-full bg-accent-fill px-2 py-0.5 text-[11px] font-bold tabular-nums text-white">{unread}</span>}
          <span className="flex-1" />
          {unread > 0 && (
            <button type="button" onClick={() => void markAllRead()} className="inline-flex items-center gap-1.5 rounded-lg px-2 py-1 text-[12px] font-semibold text-link hover:bg-surface-2">
              <CheckCheck className="size-4" aria-hidden />
              {t("Прочитать все")}
            </button>
          )}
          <button type="button" onClick={onSettings} aria-label={t("Настройки уведомлений")} className="grid size-[30px] place-items-center rounded-lg text-ink-2 hover:bg-surface-2 hover:text-ink">
            <Settings2 className="size-4" aria-hidden />
          </button>
        </div>

        <div role="tablist" aria-label={t("Фильтр уведомлений")} className="flex shrink-0 gap-1 overflow-x-auto border-b border-line px-3.5 pb-2.5 no-scrollbar">
          {filters.map((f: Filter) => {
            const n = filterCount(f, counts);
            return (
              <button
                key={f}
                type="button"
                role="tab"
                aria-selected={filter === f}
                onClick={() => void loadNotifications(f)}
                className={`inline-flex shrink-0 items-center gap-1.5 rounded-[9px] border px-2.5 py-1.5 text-[13px] font-medium ${
                  filter === f ? "border-line-strong bg-surface-2 text-ink" : "border-transparent text-ink-3 hover:text-ink"
                }`}
              >
                {t(filterLabel[f])}
                {n !== null && n > 0 && <span className="text-[12px] tabular-nums text-subtle">{n}</span>}
              </button>
            );
          })}
        </div>

        {push.visible(true) && push.state !== "ios-install" && <PushPrompt push={push} variant="row" dismissible={false} onHelp={onSettings} />}

        <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
          {loading && !page && (
            <div className="grid place-items-center py-12 text-ink-3" role="status">
              <Loader2 className="size-5 animate-spin" aria-label={t("Загрузка")} />
            </div>
          )}
          {error && <p className="px-[18px] py-3 text-[13px] text-bad">{t(error)}</p>}
          {page && page.items.length === 0 && !loading && (filter === "all" ? <EmptyNotifications compact /> : <p className="px-6 py-10 text-center text-[13px] text-ink-3">{t("Здесь пока пусто")}</p>)}
          {groups.map((g) => (
            <section key={g.label} aria-label={t(g.label)}>
              <h3 className="px-[18px] pb-1 pt-3 text-[10px] font-bold uppercase tracking-[0.9px] text-subtle">{t(g.label)}</h3>
              <ul>
                {g.items.map((n) => (
                  <li key={n.id}>
                    <NotificationRow n={n} variant="panel" fresh={fresh.includes(n.id)} onOpen={onOpenItem} />
                  </li>
                ))}
              </ul>
            </section>
          ))}
          <div ref={sentinel} className="h-1" />
          {loadingMore && (
            <div className="grid place-items-center py-3 text-ink-3">
              <Loader2 className="size-4 animate-spin" aria-label={t("Загрузка")} />
            </div>
          )}
        </div>

        <div className="flex shrink-0 items-center justify-between gap-3 border-t border-line bg-surface-2 px-[18px] py-3 text-[13px]">
          <button type="button" onClick={onSettings} className="inline-flex items-center gap-2 text-ink-2 hover:text-ink">
            <SlidersHorizontal className="size-4" aria-hidden />
            {t("Уведомления и геолокация")}
          </button>
          <Link href="/notifications" onClick={onClose} className="inline-flex items-center gap-1.5 font-semibold text-link">
            {t("Все уведомления")}
            <ArrowRight className="size-4" aria-hidden />
          </Link>
        </div>
        <span className="sr-only" aria-live="polite">
          {t(plural(unread, "{n} непрочитанное", "{n} непрочитанных", "{n} непрочитанных"), { n: unread })}
        </span>
      </div>
    </>
  );
}
