"use client";

import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { ArrowDown, ArrowUp, GripVertical, Search, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { move, search } from "@/lib/module-nav";
import { Badge, portalHost, sectionsCount, type NavItem } from "./ModuleNavMenu";

/**
 * Шторка «Все разделы» (телефон): поиск и сетка разделов в три колонки, открытый — подсвечен. Тап — переход и закрытие. Закрывается
 * крестиком, Esc, тапом по фону и свайпом вниз за ручку или шапку. Высота — по содержимому, до 85% экрана; снизу — отступ под жест «домой».
 */
export function AllSectionsSheet({ title, items, activeKey, onClose }: { title: string; items: NavItem[]; activeKey: string | null; onClose: () => void }) {
  const t = useT();
  const panel = useRef<HTMLDivElement>(null);
  const [query, setQuery] = useState("");
  const [drag, setDrag] = useState<{ start: number; dy: number } | null>(null);
  const list = useMemo(() => search(items, query), [items, query]);

  useEffect(() => {
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    panel.current?.focus({ preventScroll: true });
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    // Фон не прокручивается (в оболочке прокручивается [data-app-scroll]).
    const target = document.querySelector<HTMLElement>("[data-app-scroll]") ?? document.body;
    const overflow = target.style.overflow;
    target.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      target.style.overflow = overflow;
      if (opener?.isConnected) opener.focus({ preventScroll: true });
    };
  }, [onClose]);

  // Свайп вниз за ручку или шапку: тянется за пальцем, дальше 80 px — закрыть.
  const swipe = {
    onPointerDown: (e: React.PointerEvent) => {
      if ((e.target as HTMLElement).closest("button")) return;
      e.currentTarget.setPointerCapture(e.pointerId);
      setDrag({ start: e.clientY, dy: 0 });
    },
    onPointerMove: (e: React.PointerEvent) => drag && setDrag({ ...drag, dy: Math.max(0, e.clientY - drag.start) }),
    onPointerUp: () => {
      if (drag && drag.dy > 80) onClose();
      setDrag(null);
    },
    onPointerCancel: () => setDrag(null),
  };

  const host = portalHost();
  if (!host) return null;
  return createPortal(
    <div className="fixed inset-0 z-50 flex items-end bg-black/70 lg:hidden" onClick={onClose} role="dialog" aria-modal="true" aria-label={t("Все разделы")}>
      <div
        ref={panel}
        tabIndex={-1}
        onClick={(e) => e.stopPropagation()}
        style={drag ? { transform: `translateY(${drag.dy}px)` } : undefined}
        className={`max-h-[85dvh] w-full overflow-y-auto overscroll-contain rounded-t-[24px] bg-surface-solid px-4 pb-[calc(env(safe-area-inset-bottom)+28px)] text-ink outline-none ${drag ? "" : "transition-transform"}`}
      >
        <div {...swipe} className="touch-none">
          <div className="flex justify-center pb-2 pt-2.5" aria-hidden>
            <div className="h-[5px] w-10 rounded-full bg-line-strong" />
          </div>
          <div className="flex items-start justify-between gap-3 pb-3">
            <div className="min-w-0">
              <h2 className="text-lg font-bold leading-tight">{t("Все разделы")}</h2>
              <p className="mt-0.5 text-xs text-ink-3">
                {title} · {sectionsCount(items.length, t)}
              </p>
            </div>
            <button type="button" onClick={onClose} aria-label={t("Закрыть")} className="grid size-[34px] shrink-0 place-items-center rounded-full bg-muted text-ink-2">
              <X aria-hidden className="size-4" />
            </button>
          </div>
        </div>
        <label className="flex h-11 items-center gap-2 rounded-xl border border-line bg-bg px-3 focus-within:border-accent">
          <Search aria-hidden className="size-4 shrink-0 text-ink-4" />
          <input
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("Найти раздел")}
            aria-label={t("Найти раздел")}
            className="min-w-0 flex-1 bg-transparent text-[15px] text-ink outline-none placeholder:text-ink-4 [&::-webkit-search-cancel-button]:hidden"
          />
        </label>
        {list.length === 0 ? (
          <p className="py-8 text-center text-sm text-ink-3">{t("Ничего не найдено")}</p>
        ) : (
          <ul className="mt-3 grid grid-cols-3 gap-2">
            {list.map((item) => {
              const on = item.key === activeKey;
              return (
                <li key={item.key}>
                  <Link
                    href={item.href}
                    onClick={onClose}
                    aria-current={on ? "page" : undefined}
                    className={`relative flex h-full min-h-[96px] flex-col items-center gap-2 rounded-[14px] border px-1.5 py-3 text-center ${
                      on ? "border-accent/50 bg-accent-soft" : "border-line bg-bg active:bg-muted"
                    }`}
                  >
                    <span className={`grid size-9 shrink-0 place-items-center rounded-[10px] ${on ? "bg-accent text-white" : "bg-muted text-ink-2"}`}>
                      <item.icon aria-hidden className="size-[18px]" strokeWidth={1.9} />
                    </span>
                    <span className={`line-clamp-2 text-xs leading-tight ${on ? "font-semibold text-ink" : "text-ink"}`}>{item.label}</span>
                    {item.badge ? (
                      <span className="absolute right-1.5 top-1.5">
                        <Badge value={item.badge} tone={item.badgeTone} />
                      </span>
                    ) : null}
                  </Link>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>,
    host,
  );
}

/**
 * «Настроить панель»: порядок вкладок — перетаскиванием (или «выше» / «ниже» с клавиатуры и на телефоне) и закреплённые. Закреплённые
 * всегда первыми; изменения сохраняются сразу. «Сбросить» — порядок по умолчанию, без закреплённых.
 */
export function PanelSettings({
  open,
  title,
  items,
  pins,
  onOrder,
  onPin,
  onReset,
  onClose,
}: {
  open: boolean;
  title: string;
  items: NavItem[];
  pins: string[];
  onOrder: (keys: string[]) => void;
  onPin: (key: string, pinned: boolean) => void;
  onReset: () => void;
  onClose: () => void;
}) {
  const t = useT();
  const [dragging, setDragging] = useState<number | null>(null);
  const [over, setOver] = useState<number | null>(null);
  const keys = items.map((i) => i.key);
  const button = "grid size-8 place-items-center rounded-md text-ink-3 hover:bg-muted hover:text-ink disabled:opacity-30 disabled:hover:bg-transparent";
  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={t("Настроить панель")}
      subtitle={title}
      footer={
        <div className="flex items-center justify-between gap-2">
          <button type="button" onClick={onReset} className="inline-flex h-10 items-center rounded-lg px-3 text-sm font-medium text-ink-2 hover:bg-muted hover:text-ink">
            {t("Сбросить")}
          </button>
          <button type="button" onClick={onClose} className="inline-flex h-10 items-center rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong">
            {t("Готово")}
          </button>
        </div>
      }
    >
      <p className="mb-3 text-xs text-ink-3">{t("Перетащите разделы, чтобы задать порядок вкладок. Закреплённые всегда видны первыми; остальные, если не помещаются, уходят в «Ещё».")}</p>
      <ul className="space-y-1">
        {items.map((item, i) => {
          const pinned = pins.includes(item.key);
          return (
            <li
              key={item.key}
              draggable
              onDragStart={(e) => {
                setDragging(i);
                e.dataTransfer.effectAllowed = "move";
              }}
              onDragOver={(e) => {
                e.preventDefault();
                setOver(i);
              }}
              onDrop={(e) => {
                e.preventDefault();
                if (dragging !== null) onOrder(move(keys, dragging, i));
                setDragging(null);
                setOver(null);
              }}
              onDragEnd={() => {
                setDragging(null);
                setOver(null);
              }}
              className={`flex items-center gap-2 rounded-xl border px-2 py-1.5 ${over === i && dragging !== null && dragging !== i ? "border-accent/60 bg-accent-soft" : "border-line bg-surface"} ${dragging === i ? "opacity-50" : ""}`}
            >
              <GripVertical aria-hidden className="size-4 shrink-0 cursor-grab text-ink-4" />
              <span className="grid size-7 shrink-0 place-items-center rounded-md bg-muted text-ink-2">
                <item.icon aria-hidden className="size-[15px]" strokeWidth={1.9} />
              </span>
              <span className="min-w-0 flex-1 truncate text-sm text-ink">{item.label}</span>
              <button type="button" className={button} disabled={i === 0} onClick={() => onOrder(move(keys, i, i - 1))} aria-label={t("Выше: {label}", { label: item.label })}>
                <ArrowUp aria-hidden className="size-4" />
              </button>
              <button type="button" className={button} disabled={i === items.length - 1} onClick={() => onOrder(move(keys, i, i + 1))} aria-label={t("Ниже: {label}", { label: item.label })}>
                <ArrowDown aria-hidden className="size-4" />
              </button>
              <label className="flex shrink-0 cursor-pointer items-center gap-1.5 pl-1 text-xs text-ink-2">
                <input type="checkbox" checked={pinned} onChange={(e) => onPin(item.key, e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
                {t("Закреплено")}
              </label>
            </li>
          );
        })}
      </ul>
    </Sheet>
  );
}
