"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { Bell, Settings2 } from "lucide-react";
import { PermissionsSheet } from "./PermissionsSheet";

type Row = { id: string; kind: string; title: string; body: string | null; link: string | null; createdAt: string; read: boolean };

const timeOf = (iso: string) => new Date(iso).toLocaleString("ru-RU", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });

/**
 * Колокольчик OneBase: уведомления пользователя (новые задачи отдела, выполненные задачи агентов). Число непрочитанных
 * обновляется при открытии, возвращении в приложение и после push; «Уведомления и геолокация» — настройки устройства.
 */
export function NotificationBell() {
  const router = useRouter();
  const pathname = usePathname();
  const [open, setOpen] = useState(false);
  const [items, setItems] = useState<Row[]>([]);
  const [unread, setUnread] = useState(0);
  const [settings, setSettings] = useState(false);
  const [pos, setPos] = useState<{ top: number; left: number; width: number; maxHeight: number } | null>(null);
  const ref = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  // Список — под колокольчиком, но в пределах экрана: на телефоне справа от колокольчика ещё аватар, и привязанный
  // к нему список шириной 320 px уходил за левый край.
  const place = useCallback(() => {
    const r = buttonRef.current?.getBoundingClientRect();
    if (!r) return;
    const width = Math.min(320, window.innerWidth - 16);
    const left = Math.max(8, Math.min(r.right - width, window.innerWidth - width - 8));
    setPos({ top: r.bottom + 8, left, width, maxHeight: Math.max(160, window.innerHeight - r.bottom - 16) });
  }, []);

  const load = useCallback(async () => {
    try {
      const response = await fetch("/bff/api/notifications?take=20", { cache: "no-store" });
      if (!response.ok) return;
      const data = (await response.json()) as { items: Row[]; unread: number };
      setItems(data.items);
      setUnread(data.unread);
    } catch {
      // нет связи — покажем, что было
    }
  }, []);

  useEffect(() => {
    void load();
    const onVisible = () => document.visibilityState === "visible" && void load();
    document.addEventListener("visibilitychange", onVisible);
    const onMessage = (event: MessageEvent) => (event.data as { type?: string } | null)?.type === "push-received" && void load();
    navigator.serviceWorker?.addEventListener("message", onMessage);
    return () => {
      document.removeEventListener("visibilitychange", onVisible);
      navigator.serviceWorker?.removeEventListener("message", onMessage);
    };
  }, [load]);

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

  const openItem = async (n: Row) => {
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
        aria-label={unread > 0 ? `Уведомления: ${unread} новых` : "Уведомления"}
        className="relative grid size-10 place-items-center rounded-full text-current transition-colors hover:bg-white/10 hover:text-white lg:size-9 lg:hover:bg-muted lg:hover:text-ink"
      >
        <Bell className="size-4" strokeWidth={1.75} />
        {unread > 0 && (
          <span className="absolute right-0.5 top-0.5 grid min-w-[18px] place-items-center rounded-full bg-bad px-1 text-[10px] font-semibold leading-[18px] text-white">
            {unread > 99 ? "99+" : unread}
          </span>
        )}
      </button>
      {open && pos && (
        <div
          role="menu"
          style={{ top: pos.top, left: pos.left, width: pos.width, maxHeight: pos.maxHeight }}
          className="fixed z-50 flex flex-col overflow-hidden rounded-xl border border-line bg-surface text-ink shadow-xl"
        >
          <div className="flex shrink-0 items-center justify-between gap-2 border-b border-line px-4 py-2.5">
            <span className="text-sm font-semibold">Уведомления</span>
            {unread > 0 && (
              <button type="button" onClick={readAll} className="text-xs text-accent-strong hover:underline">
                Прочитать все
              </button>
            )}
          </div>
          {items.length === 0 ? (
            <p className="px-4 py-6 text-center text-sm text-ink-3">Уведомлений пока нет</p>
          ) : (
            <ul className="max-h-[60vh] min-h-0 divide-y divide-line overflow-y-auto overscroll-contain">
              {items.map((n) => (
                <li key={n.id}>
                  <button type="button" role="menuitem" onClick={() => openItem(n)} className={`block w-full px-4 py-2.5 text-left hover:bg-muted ${n.read ? "" : "bg-accent-soft/50"}`}>
                    <span className="flex items-start gap-2">
                      {!n.read && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-accent" aria-hidden />}
                      <span className="min-w-0 flex-1">
                        <span className="block text-sm font-medium text-ink">{n.title}</span>
                        {n.body && <span className="mt-0.5 block line-clamp-2 text-xs text-ink-2">{n.body}</span>}
                        <span className="mt-0.5 block text-[11px] text-ink-3">{timeOf(n.createdAt)}</span>
                      </span>
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
          <button
            type="button"
            onClick={() => {
              setOpen(false);
              setSettings(true);
            }}
            className="flex w-full shrink-0 items-center gap-2 border-t border-line px-4 py-2.5 text-left text-sm text-ink-2 hover:bg-muted"
          >
            <Settings2 className="size-4" /> Уведомления и геолокация на этом устройстве
          </button>
        </div>
      )}
      <PermissionsSheet open={settings} onClose={() => setSettings(false)} appName="OneBase" />
    </div>
  );
}
