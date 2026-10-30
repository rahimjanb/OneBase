"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { Bell, BellRing, Settings2, X } from "lucide-react";
import { useT } from "@/i18n/client";
import { tParts } from "@/lib/field/labels";
import { enablePush, notificationPermission, pushSupport } from "@/lib/push";
import { PermissionsSheet } from "./PermissionsSheet";

type Row = { id: string; kind: string; title: string; body: string | null; link: string | null; createdAt: string; read: boolean };

const timeOf = (iso: string) => new Date(iso).toLocaleString("ru-RU", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });

/** Как часто проверять новые уведомления, пока страница открыта: на устройствах без push новая задача видна не позже чем через полминуты. */
const POLL_MS = 30_000;

/**
 * Колокольчик OneBase: уведомления пользователя (новые задачи, назначения, сроки, выполненные задачи агентов). Число непрочитанных
 * обновляется при открытии, возвращении в приложение, после push и раз в 30 секунд, пока страница открыта. Новое уведомление на любом
 * устройстве, где открыт OneBase, всплывает окном (кроме пришедших push — их уже показал телефон или компьютер). Push на закрытое
 * приложение приходит на все устройства пользователя, где он вошёл и разрешил уведомления; на устройстве без разрешения список
 * предлагает включить. «Уведомления и геолокация» — настройки устройства.
 */
export function NotificationBell() {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<Row[]>([]);
  const [unread, setUnread] = useState(0);
  const [settings, setSettings] = useState(false);
  const [pos, setPos] = useState<{ top: number; left: number; width: number; maxHeight: number } | null>(null);
  const ref = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  // Уже известные уведомления: новое среди них — повод показать окно. До первой загрузки — null (старые не всплывают).
  const known = useRef<Set<string> | null>(null);
  const [toast, setToast] = useState<{ row: Row; more: number } | null>(null);
  const [push, setPush] = useState<"supported" | "ios-install" | "unsupported" | "granted" | "denied">("unsupported");

  // Можно ли включить push на этом устройстве — для строки в списке.
  const checkPush = useCallback(() => {
    const support = pushSupport();
    const permission = notificationPermission();
    setPush(support !== "supported" ? support : permission === "granted" ? "granted" : permission === "denied" ? "denied" : "supported");
  }, []);

  // Список — под колокольчиком, но в пределах экрана: на телефоне справа от колокольчика ещё аватар, и привязанный
  // к нему список шириной 320 px уходил за левый край.
  const place = useCallback(() => {
    const r = buttonRef.current?.getBoundingClientRect();
    if (!r) return;
    const width = Math.min(320, window.innerWidth - 16);
    const left = Math.max(8, Math.min(r.right - width, window.innerWidth - width - 8));
    setPos({ top: r.bottom + 8, left, width, maxHeight: Math.max(160, window.innerHeight - r.bottom - 16) });
  }, []);

  // silent — без всплывающего окна (уведомление уже показано push-ом).
  const load = useCallback(async (silent = false) => {
    try {
      const response = await fetch("/bff/api/notifications?take=20", { cache: "no-store" });
      if (!response.ok) return;
      const data = (await response.json()) as { items: Row[]; unread: number };
      setItems(data.items);
      setUnread(data.unread);
      const seen = known.current;
      const fresh = seen ? data.items.filter((n) => !n.read && !seen.has(n.id)) : [];
      known.current = new Set([...(seen ?? []), ...data.items.map((n) => n.id)]);
      if (fresh.length > 0 && !silent) setToast({ row: fresh[0], more: fresh.length - 1 });
    } catch {
      // нет связи — покажем, что было
    }
  }, []);

  useEffect(() => {
    void load();
    checkPush();
    const onVisible = () => {
      if (document.visibilityState !== "visible") return;
      void load();
      checkPush();
    };
    document.addEventListener("visibilitychange", onVisible);
    const onMessage = (event: MessageEvent) => (event.data as { type?: string } | null)?.type === "push-received" && void load(true);
    navigator.serviceWorker?.addEventListener("message", onMessage);
    const timer = setInterval(() => document.visibilityState === "visible" && void load(), POLL_MS);
    return () => {
      document.removeEventListener("visibilitychange", onVisible);
      navigator.serviceWorker?.removeEventListener("message", onMessage);
      clearInterval(timer);
    };
  }, [load, checkPush]);

  // Окно о новом уведомлении само закрывается через 12 секунд.
  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(null), 12_000);
    return () => clearTimeout(timer);
  }, [toast]);

  useEffect(() => setOpen(false), [pathname]);

  useEffect(() => {
    if (!open) return;
    void load();
    place();
    window.addEventListener("resize", place);
    const onDown = (e: PointerEvent) => {
      if (!ref.current?.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("resize", place);
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open, load, place]);

  const turnOnPush = async () => {
    await enablePush();
    checkPush();
  };

  const openItem = async (n: Row) => {
    setToast((x) => (x?.row.id === n.id ? null : x));
    if (!n.read) {
      setItems((list) => list.map((x) => (x.id === n.id ? { ...x, read: true } : x)));
      setUnread((u) => Math.max(0, u - 1));
      void fetch(`/bff/api/notifications/${n.id}/read`, { method: "POST" });
    }
    setOpen(false);
    if (n.link) router.push(n.link);
  };

  const readAll = async () => {
    setItems((list) => list.map((x) => ({ ...x, read: true })));
    setUnread(0);
    await fetch("/bff/api/notifications/read-all", { method: "POST" });
  };

  return (
    <div ref={ref} className="relative">
      <button
        ref={buttonRef}
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={unread > 0 ? t("Уведомления: {n} новых", { n: unread }) : t("Уведомления")}
        className="relative grid size-[40px] place-items-center rounded-full border border-white/10 bg-white/[0.05] text-current transition-colors duration-150 hover:bg-white/10 hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent lg:size-9 lg:rounded-[9px] lg:border-0 lg:bg-transparent lg:text-ink-3 lg:hover:bg-muted lg:hover:text-ink"
      >
        <Bell className="size-[18px] lg:size-4" strokeWidth={1.75} />
        {/* Телефон — красная точка с обводкой цветом шапки; компьютер — число непрочитанных. */}
        {unread > 0 && <span aria-hidden className="absolute right-[9px] top-[9px] size-[8px] rounded-full bg-bad ring-2 ring-[#060a14] lg:hidden" />}
        {unread > 0 && (
          <span className="absolute right-0.5 top-0.5 hidden min-w-[18px] place-items-center rounded-full bg-bad px-1 text-[10px] font-semibold leading-[18px] text-white lg:grid">
            {unread > 99 ? "99+" : unread}
          </span>
        )}
      </button>
      {open && pos && (
        <div
          role="menu"
          style={{ top: pos.top, left: pos.left, width: pos.width, maxHeight: pos.maxHeight }}
          className="fixed z-50 flex flex-col overflow-hidden rounded-xl border border-line bg-surface-solid text-ink shadow-xl"
        >
          <div className="flex shrink-0 items-center justify-between gap-2 border-b border-line px-4 py-2.5">
            <span className="text-sm font-semibold">{t("Уведомления")}</span>
            {unread > 0 && (
              <button type="button" onClick={readAll} className="text-xs text-accent-strong hover:underline">
                {t("Прочитать все")}
              </button>
            )}
          </div>
          {items.length === 0 ? (
            <p className="px-4 py-6 text-center text-sm text-ink-3">{t("Уведомлений пока нет")}</p>
          ) : (
            <ul className="max-h-[60vh] min-h-0 divide-y divide-line overflow-y-auto overscroll-contain">
              {items.map((n) => (
                <li key={n.id}>
                  <button type="button" role="menuitem" onClick={() => openItem(n)} className={`block w-full px-4 py-2.5 text-left hover:bg-muted ${n.read ? "" : "bg-accent-soft/50"}`}>
                    <span className="flex items-start gap-2">
                      {!n.read && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-accent" aria-hidden />}
                      <span className="min-w-0 flex-1">
                        <span className="block text-sm font-medium text-ink">{t(n.title)}</span>
                        {/* Текст сервера: несколько названий через «; » (AI-рекомендации) — переводится по частям. */}
                        {n.body && <span className="mt-0.5 block line-clamp-2 text-xs text-ink-2">{tParts(t, n.body, "; ")}</span>}
                        <span className="mt-0.5 block text-[11px] text-ink-3">{timeOf(n.createdAt)}</span>
                      </span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
          {push !== "granted" && push !== "unsupported" && (
            <div className="shrink-0 border-t border-line bg-accent-soft/40 px-4 py-2.5 text-xs text-ink-2">
              {push === "supported" ? (
                <button type="button" onClick={turnOnPush} className="flex items-center gap-2 text-left text-sm font-medium text-accent-strong hover:underline">
                  <BellRing className="size-4 shrink-0" /> {t("Включить push на этом устройстве")}
                </button>
              ) : push === "denied" ? (
                t("Уведомления для OneBase запрещены в браузере — разрешите их в настройках сайта, чтобы новые задачи приходили на это устройство.")
              ) : (
                t("На iPhone push приходят только в приложении: добавьте OneBase на экран «Домой».")
              )}
            </div>
          )}
          <button
            type="button"
            onClick={() => {
              setOpen(false);
              setSettings(true);
            }}
            className="flex w-full shrink-0 items-center gap-2 border-t border-line px-4 py-2.5 text-left text-sm text-ink-2 hover:bg-muted"
          >
            <Settings2 className="size-4" /> {t("Уведомления и геолокация на этом устройстве")}
          </button>
        </div>
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
      <PermissionsSheet open={settings} onClose={() => setSettings(false)} appName="OneBase" />
    </div>
  );
}
