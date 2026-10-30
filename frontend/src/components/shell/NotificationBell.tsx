"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { Bell, BellRing, X } from "lucide-react";
import { NotificationsPanel } from "@/components/notifications/NotificationsPanel";
import { useT } from "@/i18n/client";
import { tParts } from "@/lib/field/labels";
import { bellBadge, plural, type NotificationItem } from "@/lib/notifications";
import { loadNotifications, markRead, peekUnread, useNotifications } from "@/lib/notifications-store";
import { PermissionsSheet } from "./PermissionsSheet";

/** Как часто проверять новые уведомления, пока страница открыта: на устройствах без push новая задача видна не позже чем через полминуты. */
const POLL_MS = 30_000;
/** Пока панель открыта, список обновляется чаще. */
const OPEN_POLL_MS = 15_000;

const desktop = () => window.matchMedia("(min-width: 1024px)").matches;

/**
 * Колокольчик OneBase (ТЗ notifications). Компьютер — панель уведомлений под колокольчиком; телефон — экран «Уведомления». Число
 * непрочитанных — общее с панелью и экраном (notifications-store): обновляется при открытии, возвращении в приложение, после push и раз
 * в 30 секунд. Новое уведомление на любом устройстве, где открыт OneBase, всплывает окном (кроме пришедших push — их уже показал
 * телефон или компьютер). Бейдж: до 9 непрочитанных — красная точка, больше — «9+».
 */
export function NotificationBell({ permissions = null }: { permissions?: readonly string[] | null }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const { counts } = useNotifications();
  const [anchor, setAnchor] = useState<DOMRect | null>(null);
  const [settings, setSettings] = useState(false);
  const buttonRef = useRef<HTMLButtonElement>(null);
  // Уже известные непрочитанные: новое среди них — повод показать окно. До первой загрузки — null (старые не всплывают).
  const known = useRef<Set<string> | null>(null);
  const [toast, setToast] = useState<{ row: NotificationItem; more: number } | null>(null);
  const open = anchor !== null;
  const anchorRef = useRef<DOMRect | null>(null);
  anchorRef.current = anchor;

  // silent — без всплывающего окна (уведомление уже показано push-ом).
  const check = useCallback(async (silent = false) => {
    try {
      const items = await peekUnread();
      const seen = known.current;
      const fresh = seen ? items.filter((n) => !seen.has(n.id)) : [];
      known.current = new Set([...(seen ?? []), ...items.map((n) => n.id)]);
      if (fresh.length > 0 && !silent) setToast({ row: fresh[0], more: fresh.length - 1 });
      if (fresh.length > 0 && anchorRef.current) void loadNotifications(undefined, true);
    } catch {
      // нет связи — покажем, что было
    }
  }, []);

  useEffect(() => {
    void check();
    const onVisible = () => document.visibilityState === "visible" && void check();
    document.addEventListener("visibilitychange", onVisible);
    const onMessage = (event: MessageEvent) => (event.data as { type?: string } | null)?.type === "push-received" && void check(true);
    navigator.serviceWorker?.addEventListener("message", onMessage);
    const timer = setInterval(() => document.visibilityState === "visible" && void check(), POLL_MS);
    return () => {
      document.removeEventListener("visibilitychange", onVisible);
      navigator.serviceWorker?.removeEventListener("message", onMessage);
      clearInterval(timer);
    };
  }, [check]);

  // Открытая панель: список обновляется чаще; новое — сверху с подсветкой.
  useEffect(() => {
    if (!open) return;
    const timer = setInterval(() => document.visibilityState === "visible" && void loadNotifications(undefined, true), OPEN_POLL_MS);
    const onResize = () => setAnchor(buttonRef.current?.getBoundingClientRect() ?? null);
    window.addEventListener("resize", onResize);
    return () => {
      clearInterval(timer);
      window.removeEventListener("resize", onResize);
    };
  }, [open]);

  // Окно о новом уведомлении само закрывается через 12 секунд.
  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(null), 12_000);
    return () => clearTimeout(timer);
  }, [toast]);

  const close = useCallback(() => {
    setAnchor(null);
    buttonRef.current?.focus({ preventScroll: true });
  }, []);

  useEffect(() => setAnchor(null), [pathname]);

  const toggle = () => {
    if (!desktop()) {
      router.push("/notifications");
      return;
    }
    if (open) close();
    else setAnchor(buttonRef.current?.getBoundingClientRect() ?? null);
  };

  const openItem = (n: NotificationItem) => {
    setToast((x) => (x?.row.id === n.id ? null : x));
    if (!n.read) void markRead(n.id, true);
    setAnchor(null);
    if (n.href) router.push(n.href);
  };

  const unread = counts?.unread ?? 0;
  const badge = bellBadge(unread);
  const label = unread > 0 ? t(plural(unread, "Уведомления, {n} непрочитанное", "Уведомления, {n} непрочитанных", "Уведомления, {n} непрочитанных"), { n: unread }) : t("Уведомления");

  return (
    <div className="relative">
      <button
        ref={buttonRef}
        type="button"
        onClick={toggle}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-label={label}
        className={`relative grid size-[40px] place-items-center rounded-full border border-white/10 bg-white/[0.05] text-current transition-colors duration-150 hover:bg-white/10 hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent lg:size-9 lg:border ${
          open ? "lg:border-accent/50 lg:bg-accent-soft lg:text-accent-strong" : "lg:border-transparent lg:bg-transparent lg:text-ink-3 lg:hover:bg-muted lg:hover:text-ink"
        }`}
      >
        <Bell className="size-[18px] lg:size-4" strokeWidth={1.75} />
        {badge === "dot" && <span aria-hidden className="absolute right-[9px] top-[9px] size-[8px] rounded-full bg-bad ring-2 ring-[#060a14] lg:right-[7px] lg:top-[7px] lg:ring-[var(--color-page,#060a14)]" />}
        {badge === "9+" && (
          <span aria-hidden className="absolute -right-1 -top-1 grid h-[18px] min-w-[18px] place-items-center rounded-full bg-bad px-1 text-[10px] font-bold leading-none text-white ring-2 ring-[#060a14]">
            9+
          </span>
        )}
      </button>
      {anchor && (
        <NotificationsPanel
          anchor={anchor}
          permissions={permissions}
          onClose={close}
          onOpenItem={openItem}
          onSettings={() => {
            setAnchor(null);
            setSettings(true);
          }}
        />
      )}
      {toast && (
        <div role="status" aria-live="polite" className="fixed right-4 top-20 z-50 w-[min(360px,calc(100vw-32px))] rounded-xl border border-line bg-surface-solid p-3 text-ink shadow-xl">
          <div className="flex items-start gap-3">
            <span className="grid size-8 shrink-0 place-items-center rounded-full bg-accent-soft text-accent-strong">
              <BellRing className="size-4" />
            </span>
            <button type="button" onClick={() => openItem(toast.row)} className="min-w-0 flex-1 text-left">
              <span className="block text-sm font-semibold text-ink">{t(toast.row.title)}</span>
              {toast.row.body && <span className="mt-0.5 block line-clamp-2 text-xs text-ink-2">{tParts(t, toast.row.body, "; ")}</span>}
              {toast.more > 0 && <span className="mt-1 block text-[11px] text-ink-3">{t("и ещё новых: {n}", { n: toast.more })}</span>}
            </button>
            <button type="button" onClick={() => setToast(null)} aria-label={t("Закрыть")} className="grid size-7 shrink-0 place-items-center rounded-md text-ink-3 hover:bg-muted hover:text-ink">
              <X className="size-4" />
            </button>
          </div>
        </div>
      )}
      <PermissionsSheet open={settings} onClose={() => setSettings(false)} appName="OneBase" preferences />
    </div>
  );
}
