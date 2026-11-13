"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { ArrowDown, ArrowUp, Copy, Check, Expand, Star } from "lucide-react";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { toTsv, type Column } from "@/components/sales/DataTable";
import { kg, money, num, outletsLabel, pct, plural } from "@/lib/sales/format";
import { matchesSku, skuName, sortSkus } from "@/lib/sales/assortment-view";
import { withQuery } from "@/lib/sales/query";
import type { SkuRow, SkuStatus } from "@/lib/sales/types";
import { ProductImage, type ViewerItem } from "./photo";
import { usePhotoPicker, useProductPhotos } from "./PhotosContext";

/*
 * Артикулы категории (ТЗ «Продажи · Ассортимент · Категория», разделы 4.3 и 5.3; «— фото», 6.4–6.5): таблица с фото (компьютер),
 * карточки с фото (компьютер «Карточки» и телефон «Сетка»), строки списка (телефон «Список»). Нули — subtle, пропавший — приглушён.
 */

export const statusView: Record<SkuStatus, { label: string; pill: string; overlay: string }> = {
  selling: { label: "Продаётся", pill: "bg-ok-soft text-ok", overlay: "text-[#2BD47D]" },
  lost: { label: "Пропал", pill: "bg-bad-soft text-bad", overlay: "text-[#FF5A6A]" },
  elsewhere: { label: "Не возят", pill: "bg-warn-soft text-warn", overlay: "text-[#F6A93B]" },
  silent: { label: "Нет продаж", pill: "bg-muted text-ink-3", overlay: "text-[#C9CFDC]" },
};

export { matchesSku, skuName, sortSkus };

export const skuHref = (r: SkuRow, query: string) => withQuery(`/sales/assortment/product/${r.productId}`, query);

const minus = (s: string) => s.replace(/^-/, "−");

const isZero = (v: number | null | undefined) => v == null || v === 0;
/** Доля 0..1: меньше 10% — с десятыми; ноль — «0%». */
const share = (v: number | null) => (isZero(v) ? "0%" : pct(v, 1));

export function StatusPill({ status, small = false }: { status: SkuStatus; small?: boolean }) {
  const t = useT();
  const v = statusView[status];
  return (
    <span className={`inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-[7px] font-semibold ${v.pill} ${small ? "px-1.5 py-0.5 text-[10px]" : "px-2 py-[3px] text-[11px]"}`}>
      <span aria-hidden className="size-1.5 rounded-full bg-current" />
      {t(v.label)}
    </span>
  );
}

export function TopBadge() {
  const t = useT();
  return (
    <span className="inline-flex shrink-0 items-center gap-[3px] rounded-[5px] bg-warn-soft px-1.5 py-px text-[10px] font-bold leading-4 text-warn">
      <Star aria-hidden className="size-2.5" strokeWidth={2.4} />
      {t("ТОП")}
    </span>
  );
}

/** Доля по весу: полоса и процент; role="meter". */
export function ShareBar({ value, thin = false, className = "" }: { value: number | null; thin?: boolean; className?: string }) {
  const t = useT();
  const v = Math.max(0, Math.min(1, value ?? 0));
  return (
    <span className={`flex items-center ${thin ? "gap-1.5" : "gap-2"} ${className}`}>
      <span
        role="meter"
        aria-label={t("Доля по весу")}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.round(v * 1000) / 10}
        className={`relative flex-1 overflow-hidden bg-muted ${thin ? "h-1 rounded-sm" : "h-1.5 rounded-[3px]"}`}
      >
        {v > 0 && <span className="absolute inset-y-0 left-0 rounded-[inherit] bg-accent" style={{ width: `${Math.max(v * 100, 1.5)}%` }} />}
      </span>
      <span className={`shrink-0 text-right font-semibold tabular-nums ${thin ? "min-w-9 text-[10px]" : "min-w-12 text-[12px]"} ${isZero(value) ? "text-ink-4" : thin ? "text-ink-3" : "text-ink-2"}`}>
        {share(value)}
      </span>
    </span>
  );
}

/** «Только он»: подсказка с числом точек и долей. */
function soloTitle(r: SkuRow, t: T) {
  return t(
    plural(r.solo, ["{outlets} купила только этот SKU — {share} его ТТ", "{outlets} купили только этот SKU — {share} его ТТ", "{outlets} купили только этот SKU — {share} его ТТ"]),
    { outlets: outletsLabel(r.solo, t), share: pct(r.soloShare, 1) },
  );
}

const viewerOf = (r: SkuRow, t: T): ViewerItem => ({ productId: r.productId, photos: [], title: skuName(r), sub: r.code ? t("Артикул {code}", { code: r.code }) : null });

/** Фото в строке таблицы: есть — нажатие открывает просмотр; нет — заглушка, у кого есть право — «Добавить фото». */
function PhotoCell({ r, lost, onPhoto }: { r: SkuRow; lost: boolean; onPhoto: (item: ViewerItem) => void }) {
  const t = useT();
  const { photos, canEdit, busy, upload } = useProductPhotos(r.productId);
  const picker = usePhotoPicker((file) => void upload(file));
  if (photos.length) {
    return (
      <button
        type="button"
        onClick={() => onPhoto(viewerOf(r, t))}
        aria-label={t("Фото: {name}", { name: skuName(r) })}
        className="size-14 shrink-0 cursor-zoom-in overflow-hidden rounded-xl border border-line-strong outline-none focus-visible:ring-2 focus-visible:ring-accent"
      >
        <ProductImage photo={photos[0]} alt="" sizes="56px" pad="p-1" emptyIcon={18} dim={lost} busy={busy === "upload"} />
      </button>
    );
  }
  return (
    <span className="size-14 shrink-0 overflow-hidden rounded-xl border border-line">
      {picker.element}
      <ProductImage photo={null} alt="" sizes="56px" emptyIcon={18} onAdd={canEdit ? picker.open : undefined} busy={busy === "upload"} />
    </span>
  );
}

// ---------- Таблица (компьютер) ----------

type Sort = { key: string; dir: "asc" | "desc" } | null;

type Col = Column<SkuRow> & { width?: number };

function columns(t: T, prevLabel: string): Col[] {
  return [
    { key: "name", label: "Продукт", value: (r) => skuName(r) },
    { key: "code", label: "Артикул", value: (r) => r.code },
    { key: "status", label: "Статус", value: (r) => t(statusView[r.status].label), width: 112 },
    { key: "fact", label: "Факт, кг", align: "right", value: (r) => r.factKg, width: 84 },
    { key: "revenue", label: "Выручка", align: "right", value: (r) => r.revenue, width: 96 },
    { key: "share", label: "Доля по весу", value: (r) => r.weightShare, width: 130 },
    { key: "akb", label: "ТТ", align: "right", value: (r) => r.akb, width: 64 },
    { key: "dist", label: "Дистриб.", align: "right", value: (r) => r.distribution, width: 76 },
    { key: "solo", label: "Только он", align: "right", value: (r) => r.solo, width: 90 },
    { key: "prev", label: `${prevLabel}, кг`, align: "right", value: (r) => r.prevMonthKg, width: 96 },
  ];
}

/** TSV для Excel: все строки в текущем порядке, артикул — отдельной колонкой. */
export function CopySkus({ rows, prevLabel }: { rows: SkuRow[]; prevLabel: string }) {
  const t = useT();
  const [copied, setCopied] = useState(false);
  return (
    <button
      type="button"
      aria-label={t(copied ? "Скопировано" : "Копировать таблицу")}
      title={t(copied ? "Скопировано" : "Копировать таблицу — вставляется в Excel")}
      onClick={async () => {
        try {
          await navigator.clipboard.writeText(toTsv(columns(t, prevLabel), rows, t));
          setCopied(true);
          setTimeout(() => setCopied(false), 1500);
        } catch {
          setCopied(false);
        }
      }}
      className="grid size-9 shrink-0 place-items-center rounded-[10px] border border-line bg-surface text-ink-2 outline-none hover:bg-muted focus-visible:ring-2 focus-visible:ring-accent"
    >
      {copied ? <Check aria-hidden className="size-4 text-ok" /> : <Copy aria-hidden className="size-4" />}
    </button>
  );
}

export function SkuTable({
  rows,
  query,
  prevLabel,
  onPhoto,
}: {
  rows: SkuRow[];
  query: string;
  prevLabel: string;
  onPhoto: (item: ViewerItem) => void;
}) {
  const t = useT();
  const router = useRouter();
  const [sort, setSort] = useState<Sort>(null);
  const cols = columns(t, prevLabel).filter((c) => c.key !== "code");
  const sorted = (() => {
    if (!sort) return rows;
    const col = cols.find((c) => c.key === sort.key)!;
    const f = sort.dir === "asc" ? 1 : -1;
    return [...rows].sort((a, b) => {
      const x = col.value(a);
      const y = col.value(b);
      if (x == null) return 1;
      if (y == null) return -1;
      return (typeof x === "number" && typeof y === "number" ? x - y : String(x).localeCompare(String(y), "ru")) * f;
    });
  })();
  const toggle = (key: string) => setSort((s) => (s?.key !== key ? { key, dir: "desc" } : s.dir === "desc" ? { key, dir: "asc" } : null));
  const td = "px-[7px] py-2.5 text-[13px] tabular-nums";

  return (
    <div className="overflow-x-auto border-t border-line">
      <table className="w-full min-w-[1100px] border-collapse">
        <colgroup>
          <col />
          {cols.slice(1).map((c) => (
            <col key={c.key} style={{ width: (c.width ?? 80) + 14 }} />
          ))}
        </colgroup>
        <thead>
          <tr className="bg-muted">
            {cols.map((c, i) => {
              const active = sort?.key === c.key;
              return (
                <th
                  key={c.key}
                  scope="col"
                  aria-sort={active ? (sort!.dir === "asc" ? "ascending" : "descending") : undefined}
                  className={`whitespace-nowrap px-[7px] py-[9px] ${c.align === "right" ? "text-right" : "text-left"} ${i === 0 ? "sticky left-0 z-[1] bg-muted pl-[18px]" : ""} ${i === cols.length - 1 ? "pr-[18px]" : ""}`}
                >
                  <button
                    type="button"
                    onClick={() => toggle(c.key)}
                    className={`inline-flex items-center gap-1 text-[11px] font-semibold uppercase tracking-[0.02em] outline-none hover:text-ink focus-visible:underline ${active ? "text-ink" : "text-ink-4"}`}
                  >
                    {t(c.label)}
                    {active && (sort!.dir === "asc" ? <ArrowUp aria-hidden className="size-3" /> : <ArrowDown aria-hidden className="size-3" />)}
                  </button>
                </th>
              );
            })}
          </tr>
        </thead>
        <tbody>
          {sorted.map((r) => {
            const lost = r.status === "lost";
            const href = skuHref(r, query);
            const zero = (v: number | null | undefined, text: string, tone = "text-ink-2") => <span className={isZero(v) ? "text-ink-4" : tone}>{text}</span>;
            return (
              <tr
                key={r.productId}
                onClick={(e) => !(e.target as HTMLElement).closest("a,button") && router.push(href)}
                className={`group cursor-pointer border-t border-line ${lost ? "bg-bad/[0.03]" : ""} hover:bg-muted`}
              >
                <td className={`sticky left-0 z-[1] py-2.5 pl-[18px] pr-[7px] ${lost ? "bg-[color-mix(in_srgb,var(--color-bad)_3%,var(--color-surface))]" : "bg-surface"} group-hover:bg-muted`}>
                  <div className="flex min-w-[320px] items-center gap-3">
                    <PhotoCell r={r} lost={lost} onPhoto={onPhoto} />
                    <div className="min-w-0">
                      <Link href={href} className={`line-clamp-2 text-[13px] font-medium leading-snug outline-none hover:underline focus-visible:underline ${lost ? "text-ink-2" : "text-ink"}`}>
                        {skuName(r)}
                      </Link>
                      <div className="mt-1 flex items-center gap-2">
                        {r.isTop && <TopBadge />}
                        {r.code && <span className="text-[11px] font-medium text-ink-4">{t("Артикул {code}", { code: r.code })}</span>}
                      </div>
                    </div>
                  </div>
                </td>
                <td className="px-[7px] py-2.5">
                  <StatusPill status={r.status} />
                </td>
                <td className={`${td} text-right font-semibold`}>{zero(r.factKg, kg(r.factKg), "text-ink")}</td>
                <td className={`${td} text-right font-medium`}>{zero(r.revenue, money(r.revenue, t))}</td>
                <td className="px-[7px] py-2.5">
                  <ShareBar value={r.weightShare} />
                </td>
                <td className={`${td} text-right font-medium`}>{zero(r.akb, num(r.akb))}</td>
                <td className={`${td} text-right font-medium`}>{zero(r.distribution, isZero(r.distribution) ? "0%" : pct(r.distribution))}</td>
                <td className={`${td} text-right font-medium`}>
                  {r.solo > 0 ? (
                    <span title={soloTitle(r, t)} className="text-ink-2">
                      {num(r.solo)} · {pct(r.soloShare)}
                    </span>
                  ) : (
                    <span className="text-ink-4">—</span>
                  )}
                </td>
                <td className={`${td} pr-[18px] text-right font-medium ${r.prevMonthKg < 0 ? "text-bad" : r.prevMonthKg === 0 ? "text-ink-4" : "text-ink-3"}`}>{minus(kg(r.prevMonthKg))}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

// ---------- Карточка с фото (компьютер «Карточки», телефон «Сетка») ----------

/** Долгое нажатие по фото — просмотр на весь экран; клик после него страницу не открывает. */
function useLongPress(onLong: () => void) {
  const timer = useRef<number | null>(null);
  const fired = useRef(false);
  const origin = useRef<{ x: number; y: number } | null>(null);
  const cancel = () => {
    if (timer.current) window.clearTimeout(timer.current);
    timer.current = null;
  };
  return {
    fired,
    handlers: {
      onPointerDown: (e: React.PointerEvent) => {
        if (e.pointerType === "mouse") return;
        fired.current = false;
        origin.current = { x: e.clientX, y: e.clientY };
        timer.current = window.setTimeout(() => {
          fired.current = true;
          onLong();
        }, 500);
      },
      onPointerMove: (e: React.PointerEvent) => {
        if (origin.current && Math.hypot(e.clientX - origin.current.x, e.clientY - origin.current.y) > 10) cancel();
      },
      onPointerUp: cancel,
      onPointerCancel: cancel,
      onContextMenu: (e: React.MouseEvent) => e.preventDefault(),
    },
  };
}

export function SkuCard({
  r,
  query,
  priority,
  sizes,
  onPhoto,
}: {
  r: SkuRow;
  query: string;
  priority: boolean;
  sizes: string;
  onPhoto: (item: ViewerItem) => void;
}) {
  const t = useT();
  const { photos, canEdit, busy, upload } = useProductPhotos(r.productId);
  const picker = usePhotoPicker((file) => void upload(file));
  const lost = r.status === "lost";
  const v = statusView[r.status];
  const open = () => photos.length && onPhoto(viewerOf(r, t));
  const press = useLongPress(open);
  const label = [skuName(r), r.isTop ? t("ТОП") : null, t(v.label).toLocaleLowerCase("ru"), `${kg(r.factKg)} ${t("кг")}`].filter(Boolean).join(", ");
  const overlay = "absolute left-2 inline-flex items-center rounded-[7px] bg-[#0C1221D9] px-[7px] py-0.5 text-[10px] font-bold leading-4 backdrop-blur-[6px]";

  // Поле выбора файла — рядом со ссылкой, не внутри: нажатие по нему внутри <a> открыло бы товар.
  return (
    <>
    {picker.element}
    <Link
      href={skuHref(r, query)}
      aria-label={label}
      onClick={(e) => {
        if (press.fired.current) {
          e.preventDefault();
          press.fired.current = false;
        }
      }}
      className="group flex min-w-0 flex-col overflow-hidden rounded-[18px] border border-line bg-surface outline-none transition-[transform,background-color] duration-100 focus-visible:ring-2 focus-visible:ring-accent active:scale-[0.98] active:bg-muted"
    >
      <span className="relative block aspect-[174/150] overflow-hidden [-webkit-touch-callout:none]" {...press.handlers}>
        <ProductImage
          photo={photos[0]}
          alt={skuName(r)}
          sizes={sizes}
          priority={priority}
          emptyIcon={24}
          emptyLabel
          dim={lost}
          onAdd={canEdit ? picker.open : undefined}
          busy={busy === "upload"}
        />
        {r.isTop && (
          <span className={`${overlay} top-2 gap-[3px] text-[#F6A93B]`}>
            <Star aria-hidden className="size-2.5" strokeWidth={2.4} />
            {t("ТОП")}
          </span>
        )}
        <span className={`${overlay} bottom-[13px] gap-1 ${v.overlay}`}>
          <span aria-hidden className="size-1.5 rounded-full bg-current" />
          {t(v.label)}
        </span>
        {photos.length > 0 && (
          <button
            type="button"
            tabIndex={-1}
            aria-hidden
            onClick={(e) => {
              e.preventDefault();
              e.stopPropagation();
              open();
            }}
            className="absolute right-2 top-2 grid size-7 place-items-center rounded-lg bg-[#0C1221D9] text-white/90 opacity-0 transition-opacity group-hover:opacity-100 max-lg:hidden"
          >
            <Expand className="size-3.5" />
          </button>
        )}
      </span>
      <span className="flex flex-1 flex-col gap-2 px-3 pb-3 pt-2.5">
        <span className={`line-clamp-3 min-h-[3.9em] text-[12px] font-semibold leading-[1.3] ${lost ? "text-ink-2" : "text-ink"}`}>{skuName(r)}</span>
        <span className="mt-auto flex items-end justify-between gap-2">
          <span className="flex items-baseline gap-0.5 whitespace-nowrap">
            <span className={`text-[16px] font-bold leading-none tabular-nums ${isZero(r.factKg) ? "text-ink-4" : "text-ink"}`}>{kg(r.factKg)}</span>
            <span className="text-[10px] font-medium text-ink-4">{t("кг")}</span>
          </span>
          <span className={`whitespace-nowrap text-[11px] font-semibold tabular-nums ${isZero(r.revenue) ? "text-ink-4" : "text-ink-3"}`}>{money(r.revenue, t)}</span>
        </span>
        <ShareBar value={r.weightShare} thin />
        <span className="text-[10px] font-medium text-ink-4 tabular-nums">
          {t("ТТ {n} · дистр. {pct}", { n: num(r.akb), pct: isZero(r.distribution) ? "0%" : pct(r.distribution) })}
        </span>
      </span>
    </Link>
    </>
  );
}

// ---------- Строка списка (телефон «Список») ----------

export function SkuListRow({ r, query, onPhoto }: { r: SkuRow; query: string; onPhoto: (item: ViewerItem) => void }) {
  const t = useT();
  const { photos, canEdit, busy, upload } = useProductPhotos(r.productId);
  const picker = usePhotoPicker((file) => void upload(file));
  const lost = r.status === "lost";
  const press = useLongPress(() => photos.length && onPhoto(viewerOf(r, t)));
  return (
    <>
    {picker.element}
    <Link
      href={skuHref(r, query)}
      onClick={(e) => {
        if (press.fired.current) {
          e.preventDefault();
          press.fired.current = false;
        }
      }}
      className="flex items-center gap-3 rounded-2xl border border-line bg-surface p-2.5 outline-none transition-[transform,background-color] duration-100 focus-visible:ring-2 focus-visible:ring-accent active:scale-[0.98] active:bg-muted"
    >
      <span className="size-16 shrink-0 overflow-hidden rounded-xl border border-line [-webkit-touch-callout:none]" {...press.handlers}>
        <ProductImage photo={photos[0]} alt={skuName(r)} sizes="64px" pad="p-1" emptyIcon={16} dim={lost} onAdd={canEdit ? picker.open : undefined} busy={busy === "upload"} />
      </span>
      <span className="min-w-0 flex-1">
        <span className="flex items-start gap-1.5">
          <span className={`line-clamp-2 text-[13px] font-semibold leading-snug ${lost ? "text-ink-2" : "text-ink"}`}>{skuName(r)}</span>
          {r.isTop && <Star aria-label={t("ТОП")} className="mt-0.5 size-3.5 shrink-0 text-warn" strokeWidth={2.4} />}
        </span>
        <span className={`mt-0.5 block text-[12px] font-medium tabular-nums ${isZero(r.factKg) ? "text-ink-4" : "text-ink-3"}`}>
          {kg(r.factKg)} {t("кг")} · {money(r.revenue, t)}
        </span>
        <span className="mt-1.5 flex items-center gap-2">
          <ShareBar value={r.weightShare} thin className="min-w-0 flex-1" />
          <StatusPill status={r.status} small />
        </span>
      </span>
    </Link>
    </>
  );
}
