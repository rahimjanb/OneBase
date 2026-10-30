"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import {
  AlarmClock,
  AtSign,
  Bell,
  BellOff,
  BellRing,
  Check,
  CheckCheck,
  ChevronRight,
  CircleCheckBig,
  Clock,
  FileChartColumn,
  Gauge,
  Loader2,
  MapPin,
  MessageSquareText,
  MoreHorizontal,
  RefreshCw,
  SlidersHorizontal,
  Sparkles,
  SquareCheckBig,
  Trash2,
  TrendingDown,
  TrendingUp,
  Users,
  X,
  type LucideIcon,
} from "lucide-react";
import { useLocale, useT } from "@/i18n/client";
import { tParts } from "@/lib/field/labels";
import { geoPermission, type GeoState } from "@/lib/geo";
import {
  monthsRu,
  monthsUz,
  pushStateOf,
  showPushPrompt,
  timeText,
  typeOf,
  type NotificationItem,
  type PushState,
  type Tone,
} from "@/lib/notifications";
import { act, loadPreferences, markRead, savePreferences } from "@/lib/notifications-store";
import { enablePush, notificationPermission, prefetchPushKey, pushSupport } from "@/lib/push";

const icons: Record<string, LucideIcon> = {
  sparkles: Sparkles,
  "file-chart-column": FileChartColumn,
  gauge: Gauge,
  "alarm-clock": AlarmClock,
  clock: Clock,
  "circle-check-big": CircleCheckBig,
  "message-square-text": MessageSquareText,
  "square-check-big": SquareCheckBig,
  "at-sign": AtSign,
  users: Users,
  "trending-down": TrendingDown,
  "trending-up": TrendingUp,
  "refresh-cw": RefreshCw,
};

/** Цвет типа: значок и подпись раздела — цветом, плитка — его «-soft». */
const toneText: Record<Tone, string> = { violet: "text-violet", danger: "text-bad", accent: "text-link", teal: "text-teal", warning: "text-warn", success: "text-ok" };
const toneTile: Record<Tone, string> = {
  violet: "bg-violet-soft text-violet",
  danger: "bg-bad-soft text-bad",
  accent: "bg-accent-soft text-link",
  teal: "bg-teal-soft text-teal",
  warning: "bg-warn-soft text-warn",
  success: "bg-ok-soft text-ok",
};

/** Разделы «Что присылать»: код сервера → подпись. */
export const categoryLabel: Record<string, string> = { tasks: "Задачи", ai: "AI", hr: "HR", sales: "Продажи", system: "Система" };

export function TypeTile({ n, size }: { n: Pick<NotificationItem, "type" | "severity">; size: "sm" | "md" }) {
  const view = typeOf(n);
  const Icon = icons[view.icon] ?? RefreshCw;
  return (
    <span className={`grid shrink-0 place-items-center ${size === "md" ? "size-[38px] rounded-xl" : "size-[34px] rounded-[10px]"} ${toneTile[view.tone]}`} aria-hidden>
      <Icon className={size === "md" ? "size-[18px]" : "size-4"} />
    </span>
  );
}

/**
 * Время по языку интерфейса: «14:12», «вчера 18:00», «28 окт». grouped — в списке с группами «Сегодня / Вчера» день уже в заголовке
 * группы: «18:00» без «вчера» (как в макете).
 */
export function useTimeText(grouped = false) {
  const t = useT();
  const locale = useLocale();
  const months = locale.startsWith("uz") ? monthsUz : monthsRu;
  return (iso: string) => {
    const r = timeText(iso, new Date(), months);
    return r.yesterday && !grouped ? t("вчера {time}", { time: r.text }) : r.text;
  };
}

/** Мини-выбор нового срока: завтра, через 3 дня, через неделю или своя дата. */
function ExtendDue({ onPick, onClose, busy, error }: { onPick: (date: string) => void; onClose: () => void; busy: boolean; error: string | null }) {
  const t = useT();
  const ref = useRef<HTMLDivElement>(null);
  const day = (add: number) => {
    const d = new Date();
    d.setDate(d.getDate() + add);
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
  };
  const [custom, setCustom] = useState(day(1));
  useEffect(() => {
    const close = (e: Event) => {
      if (e instanceof KeyboardEvent ? e.key === "Escape" : !ref.current?.contains(e.target as Node)) onClose();
    };
    document.addEventListener("pointerdown", close);
    document.addEventListener("keydown", close);
    return () => {
      document.removeEventListener("pointerdown", close);
      document.removeEventListener("keydown", close);
    };
  }, [onClose]);
  return (
    <div ref={ref} role="dialog" aria-label={t("Новый срок задачи")} className="mt-2 rounded-xl border border-line-strong bg-surface p-2.5 shadow-lg" onClick={(e) => e.stopPropagation()}>
      <div className="flex flex-wrap gap-1.5">
        {[
          [1, "Завтра"],
          [3, "Через 3 дня"],
          [7, "Через неделю"],
        ].map(([add, label]) => (
          <button key={label} type="button" disabled={busy} onClick={() => onPick(day(add as number))} className="rounded-lg border border-line bg-surface-2 px-2.5 py-1.5 text-[12px] font-medium text-ink hover:border-accent">
            {t(label as string)}
          </button>
        ))}
      </div>
      <div className="mt-2 flex items-center gap-1.5">
        <input type="date" value={custom} min={day(0)} onChange={(e) => setCustom(e.target.value)} aria-label={t("Своя дата")} className="h-8 min-w-0 flex-1 rounded-lg border border-line bg-surface-2 px-2 text-[12px] text-ink" />
        <button type="button" disabled={busy || !custom} onClick={() => onPick(custom)} className="inline-flex h-8 items-center gap-1 rounded-lg bg-accent-fill px-3 text-[12px] font-semibold text-white disabled:opacity-50">
          {busy && <Loader2 className="size-3.5 animate-spin" aria-hidden />}
          {t("Сохранить")}
        </button>
      </div>
      {error && <p className="mt-1.5 text-[12px] text-bad">{t(error)}</p>}
    </div>
  );
}

/**
 * Уведомление — общий компонент панели и экрана. Нажатие — прочитано и переход; действия — без перехода («Продлить срок» — выбор даты,
 * после — «✓ Готово»). На компьютере при наведении — меню «⋯» (прочитано / не прочитано, отключить такие); на телефоне — жесты: влево —
 * «Прочитано», вправо — «Удалить» (с отменой).
 */
export function NotificationRow({
  n,
  variant,
  fresh,
  onOpen,
  onRemove,
  onMuted,
}: {
  n: NotificationItem;
  variant: "panel" | "screen";
  fresh: boolean;
  onOpen: (n: NotificationItem) => void;
  onRemove?: (n: NotificationItem) => void;
  onMuted?: (category: string) => void;
}) {
  const t = useT();
  const time = useTimeText(true);
  const view = typeOf(n);
  const [menu, setMenu] = useState(false);
  const [picking, setPicking] = useState(false);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const panel = variant === "panel";

  useEffect(() => {
    if (!menu) return;
    const close = (e: Event) => {
      if (e instanceof KeyboardEvent ? e.key === "Escape" : !menuRef.current?.contains(e.target as Node)) setMenu(false);
    };
    document.addEventListener("pointerdown", close);
    document.addEventListener("keydown", close);
    return () => {
      document.removeEventListener("pointerdown", close);
      document.removeEventListener("keydown", close);
    };
  }, [menu]);

  const run = async (action: NotificationItem["actions"][number], date?: string) => {
    if (action.action === "open") {
      onOpen(n);
      return;
    }
    if (action.action === "extend_due" && !date) {
      setPicking(true);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await act(n.id, action.action, date);
      setDone(action.id);
      setPicking(false);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const mute = async () => {
    setMenu(false);
    try {
      const prefs = await loadPreferences();
      await savePreferences([...new Set([...prefs.muted, n.category])]);
      onMuted?.(n.category);
    } catch (e) {
      setError((e as Error).message);
    }
  };

  // Жесты на телефоне: горизонтальный сдвиг строки; вертикальная прокрутка — как обычно (touch-action: pan-y).
  const [dx, setDx] = useState(0);
  const touch = useRef<{ x: number; y: number; locked: boolean | null } | null>(null);
  const swipe = !panel && !!onRemove;
  const onTouchStart = (e: React.TouchEvent) => {
    if (!swipe) return;
    touch.current = { x: e.touches[0].clientX, y: e.touches[0].clientY, locked: null };
  };
  const onTouchMove = (e: React.TouchEvent) => {
    const s = touch.current;
    if (!s) return;
    const ddx = e.touches[0].clientX - s.x;
    const ddy = e.touches[0].clientY - s.y;
    if (s.locked === null && (Math.abs(ddx) > 8 || Math.abs(ddy) > 8)) s.locked = Math.abs(ddx) > Math.abs(ddy);
    if (s.locked) setDx(Math.max(-120, Math.min(120, ddx)));
  };
  const onTouchEnd = () => {
    const s = touch.current;
    touch.current = null;
    if (!s?.locked) return;
    if (dx <= -72 && !n.read) void markRead(n.id, true);
    if (dx >= 72) onRemove?.(n);
    setDx(0);
  };

  const label = `${n.read ? "" : `${t("не прочитано")}. `}${t(n.title)}`;
  const actions = n.actions.slice(0, 2);
  const meta = (
    <span className="flex flex-wrap items-center gap-x-1.5 gap-y-1 text-[11px]">
      <span className="text-subtle tabular-nums">{time(n.createdAt)}</span>
      <span aria-hidden className="size-[3px] rounded-full bg-subtle" />
      <span className={`font-semibold ${toneText[view.tone]}`}>{t(view.section)}</span>
      {panel &&
        actions.map((a) =>
          done === a.id ? (
            <span key={a.id} className="ml-1 inline-flex items-center gap-1 font-semibold text-ok">
              <Check className="size-3" aria-hidden />
              {t("Готово")}
            </span>
          ) : (
            <button
              key={a.id}
              type="button"
              disabled={busy}
              onClick={(e) => {
                e.stopPropagation();
                void run(a);
              }}
              className="ml-1 rounded-md border border-line-strong bg-surface-2 px-2 py-0.5 text-[11px] font-semibold text-ink-2 hover:border-accent hover:text-ink"
            >
              {t(a.id === "extend_due" ? "Продлить" : a.label)}
            </button>
          ),
        )}
    </span>
  );

  return (
    <div className="relative overflow-hidden">
      {swipe && dx !== 0 && (
        <div aria-hidden className="absolute inset-0 flex items-center justify-between px-5 text-[12px] font-semibold">
          <span className={`flex items-center gap-1.5 text-bad ${dx > 0 ? "opacity-100" : "opacity-0"}`}>
            <Trash2 className="size-4" /> {t("Удалить")}
          </span>
          <span className={`flex items-center gap-1.5 text-link ${dx < 0 ? "opacity-100" : "opacity-0"}`}>
            {t("Прочитано")} <CheckCheck className="size-4" />
          </span>
        </div>
      )}
      <div
        role="button"
        tabIndex={0}
        aria-label={label}
        onClick={() => onOpen(n)}
        onKeyDown={(e) => {
          if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            onOpen(n);
          }
        }}
        onTouchStart={onTouchStart}
        onTouchMove={onTouchMove}
        onTouchEnd={onTouchEnd}
        style={{ transform: dx ? `translateX(${dx}px)` : undefined, transition: dx ? "none" : "transform 180ms ease-out", touchAction: swipe ? "pan-y" : undefined }}
        className={`group relative flex cursor-pointer gap-3 text-left outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-accent ${
          panel ? "py-2.5 pl-[18px] pr-4 hover:bg-surface-2" : "bg-surface p-3.5"
        } ${n.read ? "" : "bg-[var(--notif-unread)]"} ${fresh ? "notif-fresh" : ""}`}
      >
        <TypeTile n={n} size={panel ? "sm" : "md"} />
        <div className="flex min-w-0 flex-1 flex-col gap-[3px]">
          <span className={`line-clamp-2 text-ink ${panel ? "text-[13px]" : "text-sm"} ${n.read ? "font-medium" : "font-semibold"}`}>{t(n.title)}</span>
          {/* Текст сервера: несколько названий через «; » (AI-рекомендации) — переводится по частям. */}
          {n.body && <span className={`line-clamp-2 text-ink-3 ${panel ? "text-[12px]" : "text-[13px]"}`}>{tParts(t, n.body, "; ")}</span>}
          {meta}
          {!panel && actions.length > 0 && (
            <span className="mt-2 flex flex-wrap gap-2">
              {actions.map((a) =>
                done === a.id ? (
                  <span key={a.id} className="inline-flex items-center gap-1 rounded-[9px] bg-ok-soft px-3 py-1.5 text-[12px] font-semibold text-ok">
                    <Check className="size-3.5" aria-hidden />
                    {t("Готово")}
                  </span>
                ) : (
                  <button
                    key={a.id}
                    type="button"
                    disabled={busy}
                    onClick={(e) => {
                      e.stopPropagation();
                      void run(a);
                    }}
                    className={`min-h-[32px] rounded-[9px] px-3 py-1.5 text-[12px] font-semibold disabled:opacity-60 ${
                      a.kind === "primary" ? "bg-accent-fill text-white" : "border border-line-strong bg-surface-2 text-ink"
                    }`}
                  >
                    {t(a.label)}
                  </button>
                ),
              )}
            </span>
          )}
          {picking && (
            <ExtendDue
              busy={busy}
              error={error}
              onClose={() => setPicking(false)}
              onPick={(date) => void run(n.actions.find((a) => a.action === "extend_due")!, date)}
            />
          )}
          {!picking && error && <span className="text-[12px] text-bad">{t(error)}</span>}
        </div>
        {!n.read && <span aria-hidden className={`mt-1.5 size-2 shrink-0 rounded-full bg-accent ${panel ? "group-hover:opacity-0" : ""}`} />}
        {panel && (
          <div ref={menuRef} className="absolute right-2.5 top-2" onClick={(e) => e.stopPropagation()}>
            <button
              type="button"
              aria-label={t("Действия с уведомлением")}
              aria-haspopup="menu"
              aria-expanded={menu}
              onClick={() => setMenu((v) => !v)}
              className={`size-7 place-items-center rounded-md text-ink-3 hover:bg-surface hover:text-ink ${menu ? "grid" : "hidden group-hover:grid group-focus-within:grid"}`}
            >
              <MoreHorizontal className="size-4" aria-hidden />
            </button>
            {menu && (
              <div role="menu" className="absolute right-0 top-8 z-10 w-60 rounded-xl border border-line-strong bg-surface p-1 shadow-xl">
                <button
                  type="button"
                  role="menuitem"
                  onClick={() => {
                    setMenu(false);
                    void markRead(n.id, !n.read);
                  }}
                  className="flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-left text-[13px] text-ink hover:bg-surface-2"
                >
                  <CheckCheck className="size-4" aria-hidden />
                  {n.read ? t("Отметить непрочитанным") : t("Отметить прочитанным")}
                </button>
                <button type="button" role="menuitem" onClick={() => void mute()} className="flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-left text-[13px] text-ink hover:bg-surface-2">
                  <BellOff className="size-4" aria-hidden />
                  {t("Отключить уведомления «{section}»", { section: t(categoryLabel[n.category] ?? view.section) })}
                </button>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

// ---------- Push и геолокация этого устройства ----------

const snoozeKey = "onebase-push-snooze";

/** Состояние push этого устройства: поддержка, разрешение браузера, подписка; включить — по нажатию. */
export function usePushState() {
  const [state, setState] = useState<PushState>("unsupported");
  const [snoozedAt, setSnoozedAt] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const refresh = useCallback(async () => {
    let subscribed = false;
    try {
      const registration = "serviceWorker" in navigator ? await navigator.serviceWorker.getRegistration() : undefined;
      subscribed = !!(await registration?.pushManager?.getSubscription());
    } catch {
      subscribed = false;
    }
    setState(pushStateOf(pushSupport(), notificationPermission(), subscribed));
  }, []);
  useEffect(() => {
    void refresh();
    try {
      const raw = localStorage.getItem(snoozeKey);
      setSnoozedAt(raw ? Number(raw) : null);
    } catch {
      setSnoozedAt(null);
    }
    if (pushSupport() === "supported") void prefetchPushKey().catch(() => undefined);
    const onVisible = () => document.visibilityState === "visible" && void refresh();
    document.addEventListener("visibilitychange", onVisible);
    return () => document.removeEventListener("visibilitychange", onVisible);
  }, [refresh]);
  const enable = async () => {
    setBusy(true);
    try {
      await enablePush();
    } finally {
      setBusy(false);
      await refresh();
    }
  };
  const snooze = () => {
    const now = Date.now();
    setSnoozedAt(now);
    try {
      localStorage.setItem(snoozeKey, String(now));
    } catch {
      // хранилище недоступно — скроется до перезагрузки
    }
  };
  return { state, snoozedAt, busy, enable, snooze, refresh, visible: (ignoreSnooze = false) => showPushPrompt(state, snoozedAt, Date.now(), ignoreSnooze) };
}

export function useGeoState() {
  const [geo, setGeo] = useState<GeoState>("prompt");
  useEffect(() => {
    void geoPermission().then(setGeo);
  }, []);
  return geo;
}

/**
 * Карточка (телефон) или строка (панель) «Включите push на этом устройстве»: default — «Включить» (разрешение и подписка), denied —
 * «Как включить» (настройки устройства с инструкцией), iPhone вне приложения — подсказка добавить OneBase на экран «Домой».
 */
export function PushPrompt({
  push,
  variant,
  dismissible,
  snoozable = dismissible,
  onHelp,
}: {
  push: ReturnType<typeof usePushState>;
  variant: "row" | "card";
  /** Крестик: скрыть на 7 дней (в пустом состоянии крестика нет). */
  dismissible: boolean;
  /** «Не сейчас»: скрыть на 7 дней. */
  snoozable?: boolean;
  onHelp: () => void;
}) {
  const t = useT();
  const text =
    push.state === "ios-install"
      ? t("На iPhone push приходят только в приложении: добавьте OneBase на экран «Домой».")
      : push.state === "denied"
        ? t("Уведомления заблокированы в браузере — их можно разрешить в настройках.")
        : t("Чтобы сразу узнавать о просрочках, упоминаниях и критичных инсайтах AI");
  const primary =
    push.state === "default" ? (
      <button type="button" disabled={push.busy} onClick={() => void push.enable()} className={`inline-flex items-center justify-center gap-2 rounded-xl bg-accent-fill font-semibold text-white disabled:opacity-60 ${variant === "card" ? "h-11 flex-1 text-sm" : "h-8 px-3.5 text-[12px]"}`}>
        {push.busy ? <Loader2 className="size-4 animate-spin" aria-hidden /> : variant === "card" && <Bell className="size-4" aria-hidden />}
        {t("Включить")}
      </button>
    ) : (
      <button type="button" onClick={onHelp} className={`inline-flex items-center justify-center rounded-xl bg-accent-fill font-semibold text-white ${variant === "card" ? "h-11 flex-1 text-sm" : "h-8 px-3.5 text-[12px]"}`}>
        {t("Как включить")}
      </button>
    );

  if (variant === "row") {
    return (
      <div className="flex items-center gap-3 border-b border-line bg-accent-soft px-[18px] py-3">
        <BellRing className="size-[18px] shrink-0 text-link" aria-hidden />
        <span className="min-w-0 flex-1 text-[13px] font-medium text-ink">
          {t("Включите push на этом устройстве")}
          {push.state !== "default" && <span className="block text-[12px] font-normal text-ink-3">{text}</span>}
        </span>
        {primary}
      </div>
    );
  }

  return (
    <section
      className="relative rounded-[18px] border border-accent/25 p-4"
      style={{ background: "linear-gradient(135deg, rgb(61 123 255 / 0.2), rgb(124 92 255 / 0.1)), var(--notif-push-bg, var(--color-surface))" }}
    >
      {dismissible && (
        <button type="button" onClick={push.snooze} aria-label={t("Скрыть на 7 дней")} className="absolute right-2 top-2 grid size-9 place-items-center rounded-full text-ink-3 hover:text-ink">
          <X className="size-4" aria-hidden />
        </button>
      )}
      <div className="flex gap-3 pr-6">
        <span className="grid size-10 shrink-0 place-items-center rounded-xl border border-accent/30 bg-accent-soft text-link">
          <BellRing className="size-5" aria-hidden />
        </span>
        <div className="min-w-0">
          <h2 className="text-sm font-bold text-ink">{t("Включите push на этом устройстве")}</h2>
          <p className="mt-0.5 text-[13px] leading-snug text-ink-2">{text}</p>
        </div>
      </div>
      <div className="mt-4 flex gap-2.5">
        {primary}
        {snoozable && (
          <button type="button" onClick={push.snooze} className="h-11 flex-1 rounded-xl border border-line-strong bg-white/[0.06] text-sm font-semibold text-ink-2">
            {t("Не сейчас")}
          </button>
        )}
      </div>
    </section>
  );
}

/** «Это устройство»: push, геолокация, что присылать — реальные статусы; нажатие — настройки устройства. */
export function DeviceRows({ push, geo, onOpen }: { push: PushState; geo: GeoState; onOpen: () => void }) {
  const t = useT();
  const [muted, setMuted] = useState<string[] | null>(null);
  useEffect(() => {
    void loadPreferences()
      .then((p) => setMuted(p.muted))
      .catch(() => setMuted([]));
  }, []);
  const pushStatus: Record<PushState, [string, string]> = {
    granted: ["Включены", "text-ok"],
    default: ["Выключены", "text-warn"],
    denied: ["Заблокированы в браузере", "text-bad"],
    "ios-install": ["Нужна установка на экран «Домой»", "text-warn"],
    unsupported: ["Не поддерживаются этим браузером", "text-ink-3"],
  };
  const geoStatus: [string, string] =
    geo === "granted" ? ["Разрешена · для отметок", "text-ok"] : geo === "unsupported" ? ["Недоступна на этом устройстве", "text-ink-3"] : ["Не разрешена", "text-warn"];
  const sending = muted === null ? "…" : Object.keys(categoryLabel).filter((c) => !muted.includes(c)).map((c) => t(categoryLabel[c])).join(", ") || t("Ничего");
  const rows: { icon: LucideIcon; title: string; status: string; tone: string }[] = [
    { icon: push === "granted" ? Bell : BellOff, title: "Push-уведомления", status: t(pushStatus[push][0]), tone: pushStatus[push][1] },
    { icon: MapPin, title: "Геолокация", status: t(geoStatus[0]), tone: geoStatus[1] },
    { icon: SlidersHorizontal, title: "Что присылать", status: sending, tone: "text-ink-3" },
  ];
  return (
    <ul className="divide-y divide-line overflow-hidden rounded-[18px] border border-line bg-surface">
      {rows.map((r) => (
        <li key={r.title}>
          <button type="button" onClick={onOpen} className="flex min-h-[64px] w-full items-center gap-3 px-3.5 py-3 text-left hover:bg-surface-2">
            <span className="grid size-[34px] shrink-0 place-items-center rounded-[10px] bg-surface-2 text-ink-2">
              <r.icon className="size-4" aria-hidden />
            </span>
            <span className="min-w-0 flex-1">
              <span className="block text-sm font-semibold text-ink">{t(r.title)}</span>
              <span className={`block truncate text-[12px] ${r.tone}`}>{r.status}</span>
            </span>
            <ChevronRight className="size-4 shrink-0 text-ink-3" aria-hidden />
          </button>
        </li>
      ))}
    </ul>
  );
}

/** Пустой список: колокольчик в двойном круге, «Уведомлений пока нет» и подсказка. */
export function EmptyNotifications({ compact }: { compact?: boolean }) {
  const t = useT();
  return (
    <div className={`flex flex-col items-center text-center ${compact ? "px-6 py-10" : "pb-2 pt-6"}`}>
      <span className="grid size-20 place-items-center rounded-full border border-accent/15 bg-accent/[0.08]">
        <span className="grid size-14 place-items-center rounded-full border border-accent/30 bg-accent-soft text-link">
          <Bell className="size-6" aria-hidden />
        </span>
      </span>
      <h2 className={`mt-5 font-bold text-ink ${compact ? "text-base" : "text-xl"}`}>{t("Уведомлений пока нет")}</h2>
      <p className={`mt-2 max-w-[300px] text-ink-3 ${compact ? "text-[13px]" : "text-sm"}`}>{t("Здесь появятся просрочки задач, упоминания, инсайты AI и события HR")}</p>
    </div>
  );
}

