"use client";

import { useEffect, useId, useRef, useState } from "react";
import { Loader2, PackageSearch, X } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { quantity, type CatalogOption } from "@/lib/procurement";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 disabled:opacity-60";

/**
 * Поле товара с подсказками из каталога склада (SKU, название, штрихкод) и доступным остатком. free — можно оставить своё наименование
 * (форма заявки: товара нет на складе — всё к закупке); иначе нужно выбрать из каталога (складские операции). Выбранный товар —
 * с артикулом и крестиком «отвязать»; locked — по позиции уже есть выдача или резерв, товар не меняется. warehouseId — остаток этого
 * склада (складские операции); без него — склада по умолчанию, на котором резервируют заявки.
 */
export function CatalogPicker({
  name,
  sku,
  linked,
  free = false,
  locked = false,
  placeholder,
  label,
  warehouseId,
  freeHint,
  onText,
  onPick,
  onUnlink,
}: {
  name: string;
  sku?: string | null;
  linked: boolean;
  free?: boolean;
  locked?: boolean;
  placeholder?: string;
  label: string;
  warehouseId?: string | null;
  /** Подсказка под списком в режиме free (по умолчанию — про заявку). */
  freeHint?: string;
  onText?: (text: string) => void;
  onPick: (option: CatalogOption) => void;
  onUnlink?: () => void;
}) {
  const t = useT();
  const list = useId();
  const box = useRef<HTMLDivElement>(null);
  const [text, setText] = useState(name);
  const [open, setOpen] = useState(false);
  const [options, setOptions] = useState<CatalogOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [active, setActive] = useState(0);

  useEffect(() => setText(name), [name]);

  useEffect(() => {
    if (!open || linked) return;
    let alive = true;
    setLoading(true);
    const timer = window.setTimeout(() => {
      bff<CatalogOption[]>(`procurement/catalog?q=${encodeURIComponent(text.trim())}${warehouseId ? `&warehouse=${warehouseId}` : ""}`)
        .then((found) => alive && (setOptions(found), setActive(0)))
        .catch(() => alive && setOptions([]))
        .finally(() => alive && setLoading(false));
    }, 250);
    return () => {
      alive = false;
      window.clearTimeout(timer);
    };
  }, [text, open, linked, warehouseId]);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => !box.current?.contains(e.target as Node) && setOpen(false);
    document.addEventListener("pointerdown", onDown);
    return () => document.removeEventListener("pointerdown", onDown);
  }, [open]);

  const pick = (option: CatalogOption) => {
    onPick(option);
    setText(option.name);
    setOpen(false);
  };

  if (linked) {
    return (
      <div className="flex h-10 min-w-0 items-center gap-2 rounded-lg border border-accent/30 bg-accent-soft/50 pl-3 pr-1 text-sm">
        <PackageSearch className="size-4 shrink-0 text-accent-strong" aria-hidden />
        <span className="min-w-0 flex-1 truncate text-ink" title={name}>
          {name}
        </span>
        {sku && !free && <span className="shrink-0 rounded bg-surface px-1.5 py-0.5 text-[11px] font-medium tabular-nums text-ink-3">{sku}</span>}
        {!locked && onUnlink && (
          <button type="button" onClick={onUnlink} aria-label={t("Отвязать от каталога: {name}", { name })} className="grid size-8 shrink-0 place-items-center rounded-md text-ink-3 hover:text-ink">
            <X className="size-4" aria-hidden />
          </button>
        )}
      </div>
    );
  }

  return (
    <div ref={box} className="relative min-w-0">
      <input
        value={text}
        role="combobox"
        aria-expanded={open}
        aria-controls={list}
        aria-autocomplete="list"
        aria-label={label}
        placeholder={placeholder ?? (free ? t("Что купить — или выберите со склада") : t("Найдите товар: артикул или название"))}
        maxLength={300}
        onFocus={() => setOpen(true)}
        onChange={(e) => {
          setText(e.target.value);
          onText?.(e.target.value);
          setOpen(true);
        }}
        onKeyDown={(e) => {
          if (!open || options.length === 0) return;
          if (e.key === "ArrowDown") {
            e.preventDefault();
            setActive((i) => Math.min(options.length - 1, i + 1));
          } else if (e.key === "ArrowUp") {
            e.preventDefault();
            setActive((i) => Math.max(0, i - 1));
          } else if (e.key === "Enter") {
            e.preventDefault();
            pick(options[active]);
          } else if (e.key === "Escape") {
            setOpen(false);
          }
        }}
        className={field}
      />
      {open && (loading || options.length > 0 || !free) && (
        <ul id={list} role="listbox" className="absolute left-0 right-0 top-full z-30 mt-1 max-h-72 overflow-y-auto rounded-xl border border-line-strong bg-surface-solid p-1 shadow-[0_16px_40px_rgb(0_0_0/0.18)]">
          {loading && options.length === 0 && (
            <li className="flex items-center gap-2 px-3 py-2 text-sm text-ink-3">
              <Loader2 className="size-4 animate-spin" aria-hidden /> {t("Ищем на складе…")}
            </li>
          )}
          {!loading && options.length === 0 && <li className="px-3 py-2 text-sm text-ink-3">{t("В каталоге склада такого нет")}</li>}
          {options.map((o, i) => (
            <li key={o.id} role="option" aria-selected={i === active}>
              <button
                type="button"
                onMouseEnter={() => setActive(i)}
                onClick={() => pick(o)}
                className={`flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left ${i === active ? "bg-muted" : ""}`}
              >
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm text-ink">{o.name}</span>
                  <span className="block truncate text-[11px] text-ink-3">
                    {o.sku} · {o.categoryName}
                  </span>
                </span>
                <span className={`shrink-0 text-xs tabular-nums ${o.available > 0 ? "text-ok" : "text-ink-3"}`}>
                  {o.available > 0 ? t("доступно {n}", { n: `${quantity(o.available)} ${o.unitCode}` }) : t("нет на складе")}
                </span>
              </button>
            </li>
          ))}
          {free && options.length > 0 && (
            <li className="px-3 pb-1 pt-1.5 text-[11px] text-ink-3">{freeHint ?? t("Нет в списке — оставьте своё наименование, всё пойдёт к закупке")}</li>
          )}
        </ul>
      )}
    </div>
  );
}
