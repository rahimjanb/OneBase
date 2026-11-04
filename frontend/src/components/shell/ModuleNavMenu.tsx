"use client";

import Link from "next/link";
import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { ArrowRight, Check, ChevronDown, ChevronUp, Ellipsis, Pin, PinOff, Search, SlidersHorizontal, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import { search } from "@/lib/module-nav";

/** Раздел модуля для навигации: уже с адресом (период продаж переносится), переведённой подписью и счётчиком. */
export type NavItem = { key: string; label: string; short?: string; description?: string; href: string; icon: LucideIcon; badge?: number };

const useIsoLayoutEffect = typeof window === "undefined" ? useEffect : useLayoutEffect;

/** Корень оболочки приложения (несёт тему продукта) — туда рисуются меню и шторки, чтобы не прятались под панелями. */
export const portalHost = () => (typeof document === "undefined" ? null : (document.querySelector<HTMLElement>("[data-app-shell]") ?? document.body));

/** «Все 13 разделов», «2 раздела» — слово по-русски склоняется, переводится каждое слово. */
export function sectionsCount(n: number, t: (s: string) => string): string {
  return `${n} ${t(plural(n, ["раздел", "раздела", "разделов"]))}`;
}

/**
 * Кнопка «Ещё»: обычная — «… Ещё 6 ⌄» (бейдж — сколько скрыто, точка — у скрытых есть счётчик); если открыт раздел из меню — она
 * подсвечена как активная вкладка и показывает его: «DSO | Ещё 5 ⌄».
 */
export function MoreButton({
  count,
  active,
  open,
  dot,
  onClick,
  buttonRef,
  measuring,
}: {
  count: number;
  active?: NavItem | null;
  open?: boolean;
  dot?: boolean;
  onClick?: (keyboard: boolean) => void;
  buttonRef?: React.Ref<HTMLButtonElement>;
  measuring?: boolean;
}) {
  const t = useT();
  const label = active ? t("{label}, ещё разделов: {n}", { label: active.label, n: count }) : t("Ещё разделов: {n}", { n: count });
  const Icon = active?.icon;
  return (
    <button
      ref={buttonRef}
      type="button"
      tabIndex={measuring ? -1 : undefined}
      aria-haspopup="menu"
      aria-expanded={open ?? false}
      aria-label={label}
      onClick={(e) => onClick?.(e.detail === 0)}
      className={`relative inline-flex h-[34px] shrink-0 items-center gap-1.5 whitespace-nowrap rounded-[9px] px-[11px] text-[13px] font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent/60 ${
        active
          ? "bg-accent text-white shadow-[0_4px_12px_rgb(61_123_255/0.3)] hover:bg-accent-strong"
          : open
            ? "bg-muted text-ink ring-1 ring-accent/50"
            : "text-ink-2 hover:bg-muted hover:text-ink"
      }`}
    >
      {Icon ? (
        <>
          <Icon aria-hidden className="size-[15px] shrink-0" strokeWidth={1.9} />
          {active.label}
          <span aria-hidden className="h-3.5 w-px bg-white/30" />
          <span className="font-medium text-white/80">{t("Ещё {n}", { n: count })}</span>
        </>
      ) : (
        <>
          <Ellipsis aria-hidden className="size-[15px] shrink-0" strokeWidth={1.9} />
          {t("Ещё")}
          <span className="rounded-full bg-accent-soft px-1.5 text-[11px] font-bold leading-[18px] text-accent-strong">{count}</span>
        </>
      )}
      {open ? <ChevronUp aria-hidden className="size-3.5 shrink-0" /> : <ChevronDown aria-hidden className="size-3.5 shrink-0" />}
      {dot && !active && <span aria-hidden className="absolute right-1 top-1 size-1.5 rounded-full bg-bad" />}
    </button>
  );
}

/** Счётчик раздела (ждут решения, к выдаче…): на активном — белый, иначе мягкий акцент. */
export function Badge({ value, on }: { value: number; on?: boolean }) {
  return <span className={`rounded-full px-1.5 text-[11px] font-bold leading-[18px] ${on ? "bg-white/25 text-white" : "bg-accent-soft text-accent-strong"}`}>{value}</span>;
}

/**
 * Меню «Ещё»: поиск по всем разделам модуля, скрытые разделы (или все — «Все N разделов»), у строки — «Закрепить» / «Открепить»,
 * внизу «Настроить панель». Ширина 340, под кнопкой, правым краем к ней, не ближе 24 px к краю окна; высота — до min(560, окно − 120).
 * Клавиатура: ↑/↓ — по пунктам, Enter — открыть, буквы — в поиск, «/» — поиск, Esc — закрыть (фокус — на кнопку); Tab не уходит из меню.
 */
export function MoreMenu({
  anchor,
  hidden,
  all,
  activeKey,
  pins,
  focusSearch,
  onPin,
  onSettings,
  onClose,
}: {
  anchor: HTMLElement;
  hidden: NavItem[];
  all: NavItem[];
  activeKey: string | null;
  pins: string[];
  focusSearch: boolean;
  onPin: (key: string, pinned: boolean) => void;
  onSettings: () => void;
  onClose: (returnFocus: boolean) => void;
}) {
  const t = useT();
  const panel = useRef<HTMLDivElement>(null);
  const input = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState("");
  const [showAll, setShowAll] = useState(false);
  const [place, setPlace] = useState<{ top: number; left: number; width: number; maxHeight: number } | null>(null);

  const list = useMemo(() => (query.trim() ? search(all, query) : showAll ? all : hidden), [query, showAll, all, hidden]);
  const group = query.trim() ? t("Найдено · {n}", { n: list.length }) : showAll ? t("Все разделы · {n}", { n: all.length }) : t("Ещё разделы · {n}", { n: hidden.length });

  // Место — от кнопки: при открытии, изменении окна и прокрутке.
  useIsoLayoutEffect(() => {
    const update = () => {
      const r = anchor.getBoundingClientRect();
      const width = Math.min(340, window.innerWidth - 48);
      const left = Math.max(24, Math.min(r.right - width, window.innerWidth - 24 - width));
      setPlace({ top: r.bottom + 8, left, width, maxHeight: Math.min(560, window.innerHeight - 120) });
    };
    update();
    window.addEventListener("resize", update);
    window.addEventListener("scroll", update, true);
    return () => {
      window.removeEventListener("resize", update);
      window.removeEventListener("scroll", update, true);
    };
  }, [anchor]);

  useEffect(() => {
    if (!place) return;
    (focusSearch ? input.current : panel.current)?.focus({ preventScroll: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [place !== null]);

  // Клик вне меню и вне кнопки — закрыть.
  useEffect(() => {
    const down = (e: PointerEvent) => {
      const target = e.target as Node;
      if (!panel.current?.contains(target) && !anchor.contains(target)) onClose(false);
    };
    document.addEventListener("pointerdown", down);
    return () => document.removeEventListener("pointerdown", down);
  }, [anchor, onClose]);

  const onKey = (e: React.KeyboardEvent) => {
    const root = panel.current;
    if (!root) return;
    const items = Array.from(root.querySelectorAll<HTMLElement>('[role="menuitem"]'));
    const at = items.indexOf(document.activeElement as HTMLElement);
    const inSearch = document.activeElement === input.current;
    if (e.key === "Escape") {
      e.preventDefault();
      onClose(true);
    } else if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      if (items.length === 0) return;
      const next = e.key === "ArrowDown" ? (inSearch || at < 0 ? 0 : Math.min(at + 1, items.length - 1)) : at <= 0 ? -1 : at - 1;
      (next < 0 ? input.current : items[next])?.focus();
    } else if (e.key === "Home" && !inSearch && items.length) {
      e.preventDefault();
      items[0].focus();
    } else if (e.key === "End" && !inSearch && items.length) {
      e.preventDefault();
      items[items.length - 1].focus();
    } else if (e.key === "Tab") {
      // Фокус не уходит из меню: по кругу между полем поиска, пунктами и кнопками внизу.
      const focusable = Array.from(root.querySelectorAll<HTMLElement>("input, a[href], button:not([tabindex='-1'])"));
      const i = focusable.indexOf(document.activeElement as HTMLElement);
      if (focusable.length && (e.shiftKey ? i <= 0 : i === focusable.length - 1)) {
        e.preventDefault();
        focusable[e.shiftKey ? focusable.length - 1 : 0].focus();
      }
    } else if (!inSearch && (e.key === "/" || (e.key.length === 1 && !e.ctrlKey && !e.metaKey && !e.altKey && e.key !== " "))) {
      // Ввод букв — в поиск.
      e.preventDefault();
      if (e.key !== "/") setQuery((q) => q + e.key);
      input.current?.focus();
    }
  };

  const host = portalHost();
  if (!host || !place) return null;
  return createPortal(
    <div
      ref={panel}
      tabIndex={-1}
      onKeyDown={onKey}
      style={{ top: place.top, left: place.left, width: place.width, maxHeight: place.maxHeight }}
      className="fixed z-50 flex flex-col overflow-hidden rounded-2xl border border-line-strong bg-surface-solid text-ink shadow-[0_20px_50px_rgb(0_0_0/0.45)] outline-none"
    >
      <div className="p-2.5">
        <label className="flex h-9 items-center gap-2 rounded-[10px] border border-line bg-bg px-3 focus-within:border-accent">
          <Search aria-hidden className="size-4 shrink-0 text-ink-4" />
          <input
            ref={input}
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("Найти раздел…")}
            aria-label={t("Найти раздел")}
            className="min-w-0 flex-1 bg-transparent text-[13px] text-ink outline-none placeholder:text-ink-4 [&::-webkit-search-cancel-button]:hidden"
          />
          <kbd className="rounded border border-line px-1.5 text-[11px] text-ink-4">/</kbd>
        </label>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto overscroll-contain px-1.5 pb-1.5">
        <div className="px-2.5 pb-1 text-[10px] font-bold uppercase tracking-[0.09em] text-ink-4">{group}</div>
        {list.length === 0 ? (
          <p className="px-2.5 py-6 text-center text-[13px] text-ink-3">{t("Ничего не найдено")}</p>
        ) : (
          <ul role="menu" aria-label={group}>
            {list.map((item) => {
              const on = item.key === activeKey;
              const pinned = pins.includes(item.key);
              return (
                <li key={item.key} role="none" className="group relative">
                  <Link
                    role="menuitem"
                    href={item.href}
                    aria-current={on ? "page" : undefined}
                    onClick={() => onClose(false)}
                    className="flex items-center gap-3 rounded-[10px] px-2.5 py-2 outline-none hover:bg-muted focus-visible:bg-muted"
                  >
                    <span
                      className={`grid size-8 shrink-0 place-items-center rounded-lg ${
                        on ? "bg-accent text-white" : "bg-muted text-ink-2 group-hover:bg-accent-soft group-hover:text-accent-strong group-focus-within:bg-accent-soft group-focus-within:text-accent-strong"
                      }`}
                    >
                      <item.icon aria-hidden className="size-4" strokeWidth={1.9} />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-[13px] font-semibold text-ink">{item.label}</span>
                      {item.description && <span className="block truncate text-[11px] text-ink-4">{item.description}</span>}
                    </span>
                    <span className="flex shrink-0 items-center gap-1.5 group-hover:invisible">
                      {item.badge ? <Badge value={item.badge} /> : null}
                      {on && <Check aria-hidden className="size-4 text-accent-strong" />}
                    </span>
                  </Link>
                  <button
                    type="button"
                    tabIndex={-1}
                    onClick={() => onPin(item.key, !pinned)}
                    className="absolute right-2.5 top-1/2 hidden -translate-y-1/2 items-center gap-1 rounded-md border border-line bg-bg px-2 py-1 text-[11px] font-medium text-ink-2 hover:text-ink group-hover:inline-flex"
                  >
                    {pinned ? <PinOff aria-hidden className="size-3" /> : <Pin aria-hidden className="size-3" />}
                    {pinned ? t("Открепить") : t("Закрепить")}
                  </button>
                </li>
              );
            })}
          </ul>
        )}
      </div>
      <div className="flex items-center justify-between gap-2 border-t border-line bg-muted px-3 py-2.5 text-[12.5px]">
        <button type="button" onClick={onSettings} className="inline-flex items-center gap-1.5 rounded-md px-1 py-0.5 font-medium text-ink-2 hover:text-ink">
          <SlidersHorizontal aria-hidden className="size-3.5" />
          {t("Настроить панель")}
        </button>
        {!query.trim() && (
          <button type="button" onClick={() => setShowAll((v) => !v)} className="inline-flex items-center gap-1 rounded-md px-1 py-0.5 font-semibold text-accent-strong hover:underline">
            {showAll ? t("Только скрытые") : t("Все {list}", { list: sectionsCount(all.length, t) })}
            {!showAll && <ArrowRight aria-hidden className="size-3.5" />}
          </button>
        )}
      </div>
    </div>,
    host,
  );
}

/** Контекстное меню вкладки (правая кнопка): закрепить или открепить, настроить панель. */
export function TabContextMenu({
  x,
  y,
  pinned,
  onPin,
  onSettings,
  onClose,
}: {
  x: number;
  y: number;
  pinned: boolean;
  onPin: () => void;
  onSettings: () => void;
  onClose: () => void;
}) {
  const t = useT();
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    ref.current?.querySelector<HTMLElement>("button")?.focus();
    const down = (e: PointerEvent) => !ref.current?.contains(e.target as Node) && onClose();
    const key = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("pointerdown", down);
    document.addEventListener("keydown", key);
    return () => {
      document.removeEventListener("pointerdown", down);
      document.removeEventListener("keydown", key);
    };
  }, [onClose]);
  const host = portalHost();
  if (!host) return null;
  const item = "flex w-full items-center gap-2 rounded-md px-2.5 py-2 text-left text-[13px] text-ink hover:bg-muted focus-visible:bg-muted focus-visible:outline-none";
  return createPortal(
    <div
      ref={ref}
      role="menu"
      style={{ top: y, left: Math.min(x, window.innerWidth - 220) }}
      className="fixed z-50 w-52 rounded-xl border border-line-strong bg-surface-solid p-1 shadow-[0_12px_32px_rgb(0_0_0/0.35)]"
    >
      <button type="button" role="menuitem" className={item} onClick={() => (onPin(), onClose())}>
        {pinned ? <PinOff aria-hidden className="size-4 text-ink-3" /> : <Pin aria-hidden className="size-4 text-ink-3" />}
        {pinned ? t("Открепить") : t("Закрепить")}
      </button>
      <button type="button" role="menuitem" className={item} onClick={() => (onSettings(), onClose())}>
        <SlidersHorizontal aria-hidden className="size-4 text-ink-3" />
        {t("Настроить панель")}
      </button>
    </div>,
    host,
  );
}
