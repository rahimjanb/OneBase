"use client";

import Link from "next/link";
import { ArrowDownToLine, ArrowLeftRight, ArrowUpDown, ArrowUpFromLine, Check, ClipboardList, Ellipsis, FileDown, Loader2, PackageMinus, Search, SlidersHorizontal } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import { quantity, type RequestCategory, type StockRow, type StockSummary, type Warehouse } from "@/lib/procurement";
import { availableTone, movedWhen, nextSort } from "@/lib/stock-view";
import { Dropdown, ItemIcon, MenuItem, moreFilters } from "./StockBoard";
import type { OperationKind } from "./StockOperation";

/** Порядок списка на телефоне: ключ сервера (пусто — по названию). */
const mobileSorts: { key: string; label: string }[] = [
  { key: "", label: "По названию" },
  { key: "sku", label: "По артикулу" },
  { key: "available", label: "По доступному" },
  { key: "onHand", label: "По остатку" },
  { key: "reserved", label: "По резерву" },
  { key: "category", label: "По категории" },
  { key: "moved", label: "По дате движения" },
];

/** Плитки операций кладовщика (макет): «Приход» — главная, «Выдача», «Инвент.», «Ещё» — списание и перемещение. */
export function StockTiles({ several, onOperation }: { several: boolean; onOperation: (kind: OperationKind) => void }) {
  const t = useT();
  const tile = "flex h-[60px] w-full flex-col items-center justify-center gap-1 rounded-2xl text-sm font-semibold";
  const plain = `${tile} border border-line bg-surface text-ink active:bg-muted`;
  return (
    <div className="grid grid-cols-4 gap-2.5 lg:hidden">
      <button type="button" onClick={() => onOperation("receipt")} className={`${tile} bg-accent text-white active:bg-accent-strong`}>
        <ArrowDownToLine className="size-5" aria-hidden /> {t("Приход")}
      </button>
      <button type="button" onClick={() => onOperation("issue")} className={plain}>
        <ArrowUpFromLine className="size-5" aria-hidden /> {t("Выдача")}
      </button>
      <button type="button" onClick={() => onOperation("inventory")} className={plain}>
        <ClipboardList className="size-5" aria-hidden /> {t("Инвент.")}
        <span className="sr-only">{t("Инвентаризация")}</span>
      </button>
      <Dropdown
        label={t("Ещё")}
        align="right"
        width="w-60"
        button={(open, toggle) => (
          <button type="button" onClick={toggle} aria-expanded={open} aria-haspopup="menu" className={plain}>
            <Ellipsis className="size-5" aria-hidden /> {t("Ещё")}
          </button>
        )}
      >
        {(close) => (
          <>
            <MenuItem icon={PackageMinus} onClick={() => (close(), onOperation("writeoff"))}>
              {t("Списание")}
            </MenuItem>
            {several && (
              <MenuItem icon={ArrowLeftRight} onClick={() => (close(), onOperation("transfer"))}>
                {t("Перемещение")}
              </MenuItem>
            )}
          </>
        )}
      </Dropdown>
    </div>
  );
}

/** Поиск и «Фильтры» (шторка) — на телефоне; число на кнопке — сколько фильтров выбрано кроме чипов. */
export function StockSearch({ search, onSearch, active, onFilters }: { search: string; onSearch: (value: string) => void; active: number; onFilters: () => void }) {
  const t = useT();
  return (
    <div className="relative lg:hidden">
      <Search className="pointer-events-none absolute left-4 top-1/2 size-5 -translate-y-1/2 text-ink-3" aria-hidden />
      <label>
        <span className="sr-only">{t("Поиск")}</span>
        <input
          type="search"
          value={search}
          onChange={(e) => onSearch(e.target.value)}
          placeholder={t("Артикул, название")}
          className="h-12 w-full rounded-2xl border border-line-strong bg-surface pl-12 pr-32 text-base text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
        />
      </label>
      <button
        type="button"
        onClick={onFilters}
        className={`absolute right-2 top-1/2 inline-flex h-8 -translate-y-1/2 items-center gap-1.5 rounded-lg px-2.5 text-sm font-semibold ${active > 0 ? "bg-accent-soft text-accent-strong" : "bg-muted text-ink"}`}
      >
        <SlidersHorizontal className="size-4" aria-hidden /> {t("Фильтры")}
        {active > 0 && <span className="grid size-5 place-items-center rounded-full bg-accent text-xs text-white">{active}</span>}
      </button>
    </div>
  );
}

/** Чипы состояния (телефон): все, ниже минимума, нет, резерв — с числами; «К выдаче» — переход к заявкам. Выбранный — светлый. */
export function StockChips({ summary, filter, onFilter }: { summary: StockSummary; filter: string; onFilter: (filter: string) => void }) {
  const t = useT();
  const chips = [
    { key: "", label: "Все", value: summary.items, dot: null },
    { key: "low", label: "Ниже мин.", value: summary.low, dot: "bg-warn" },
    { key: "empty", label: "Нет", value: summary.empty, dot: "bg-bad" },
    { key: "reserved", label: "Резерв", value: summary.withReserve, dot: "bg-violet" },
  ];
  const chip = "inline-flex h-9 shrink-0 items-center gap-2 rounded-full border px-3.5 text-sm font-semibold";
  return (
    <div className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-0.5 [scrollbar-width:none] lg:hidden">
      {chips.map((c) => {
        const active = filter === c.key;
        return (
          <button key={c.key || "all"} type="button" onClick={() => onFilter(c.key)} aria-pressed={active} className={`${chip} ${active ? "border-ink bg-ink text-surface-solid" : "border-line bg-surface text-ink"}`}>
            {c.dot && <span className={`size-2 rounded-full ${c.dot}`} aria-hidden />}
            {t(c.label)}
            <span className={`tabular-nums font-medium ${active ? "opacity-60" : "text-ink-3"}`}>{c.value.toLocaleString("ru-RU")}</span>
          </button>
        );
      })}
      <Link href="/procurement/issue" className={`${chip} border-line bg-surface text-ink`}>
        <span className="size-2 rounded-full bg-accent" aria-hidden />
        {t("К выдаче")}
        <span className="tabular-nums font-medium text-ink-3">{summary.toIssueRequests}</span>
      </Link>
    </div>
  );
}

/**
 * Шторка «Фильтры» (телефон): склад (когда складов больше одного), категория, остальные фильтры (сроки, качество, отключённые), «Скрыть
 * нулевые», порядок списка и выгрузка в Excel. Каждый выбор сразу применяется — список за шторкой обновляется.
 */
export function StockFiltersSheet({
  open,
  onClose,
  stores,
  store,
  categories,
  category,
  filter,
  summary,
  hideEmpty,
  sort,
  desc,
  exportHref,
  apply,
}: {
  open: boolean;
  onClose: () => void;
  stores: Warehouse[];
  store: string;
  categories: RequestCategory[];
  category: string;
  filter: string;
  summary: StockSummary;
  hideEmpty: boolean;
  sort: string;
  desc: boolean;
  exportHref: string;
  apply: (patch: Record<string, string | null>) => void;
}) {
  const t = useT();
  const select =
    "h-12 w-full rounded-xl border border-line-strong bg-surface px-3 text-base text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
  const option = "flex h-12 w-full items-center gap-3 rounded-xl px-3 text-left text-[15px] text-ink active:bg-muted";
  const more = moreFilters.filter((f) => !f.batches || summary.tracked > 0);
  const caption = "mb-1.5 block text-xs font-semibold uppercase tracking-wide text-ink-3";
  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={t("Фильтры")}
      footer={
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => apply({ warehouse: null, category: null, filter: null, hideEmpty: null, sort: null, desc: null })}
            className="inline-flex h-12 flex-1 items-center justify-center rounded-xl border border-line bg-surface text-sm font-semibold text-ink"
          >
            {t("Сбросить")}
          </button>
          <button type="button" onClick={onClose} className="inline-flex h-12 flex-1 items-center justify-center rounded-xl bg-accent text-sm font-semibold text-white">
            {t("Готово")}
          </button>
        </div>
      }
    >
      <div className="space-y-5 pb-2">
        {stores.length > 1 && (
          <label className="block">
            <span className={caption}>{t("Склад")}</span>
            <select value={store} onChange={(e) => apply({ warehouse: e.target.value || null })} className={select}>
              <option value="">{t("Все склады")}</option>
              {stores.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </select>
          </label>
        )}
        <label className="block">
          <span className={caption}>{t("Категория")}</span>
          <select value={category} onChange={(e) => apply({ category: e.target.value || null })} className={select}>
            <option value="">{t("Все категории")}</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
        <button
          type="button"
          role="switch"
          aria-checked={hideEmpty}
          disabled={filter === "empty"}
          onClick={() => apply({ hideEmpty: hideEmpty ? null : "1" })}
          className="flex h-12 w-full items-center justify-between rounded-xl border border-line px-3 text-[15px] font-medium text-ink disabled:opacity-50"
        >
          {t("Скрыть нулевые")}
          <span className={`relative h-7 w-12 shrink-0 rounded-full transition-colors ${hideEmpty ? "bg-accent" : "bg-muted ring-1 ring-inset ring-line-strong"}`} aria-hidden>
            <span className={`absolute top-1 size-5 rounded-full bg-white shadow transition-[left] ${hideEmpty ? "left-6" : "left-1"}`} />
          </span>
        </button>
        <div>
          <span className={caption}>{t("Ещё фильтры")}</span>
          <div className="space-y-0.5">
            {more.map((f) => (
              <button key={f.value} type="button" role="radio" aria-checked={filter === f.value} onClick={() => apply({ filter: filter === f.value ? null : f.value })} className={option}>
                <span className="grid size-5 shrink-0 place-items-center">{filter === f.value && <Check className="size-5 text-accent" aria-hidden />}</span>
                <span className="min-w-0 flex-1">{t(f.label)}</span>
                {f.count && <span className="tabular-nums text-ink-3">{f.count(summary)}</span>}
              </button>
            ))}
          </div>
        </div>
        <div>
          <span className={caption}>{t("Порядок")}</span>
          <div className="space-y-0.5">
            {mobileSorts.map((s) => (
              <button
                key={s.key || "name"}
                type="button"
                role="radio"
                aria-checked={sort === s.key}
                onClick={() => {
                  const next = nextSort(sort, desc, s.key);
                  apply({ sort: next.sort || null, desc: next.desc ? "1" : null });
                }}
                className={option}
              >
                <span className="grid size-5 shrink-0 place-items-center">{sort === s.key && <Check className="size-5 text-accent" aria-hidden />}</span>
                <span className="min-w-0 flex-1">{t(s.label)}</span>
                {sort === s.key && <span className="text-sm text-ink-3">{desc ? t("по убыванию") : t("по возрастанию")}</span>}
              </button>
            ))}
          </div>
        </div>
        <a href={exportHref} download className="flex h-12 w-full items-center justify-center gap-2 rounded-xl border border-line text-sm font-semibold text-ink">
          <FileDown className="size-4" aria-hidden /> {t("Выгрузить в Excel")}
        </a>
      </div>
    </Sheet>
  );
}

/** Строка списка на телефоне (макет): значок, название, «артикул · категория · время движения»; справа — доступно или «Нет». */
function MobileRow({ row, now, onOpen }: { row: StockRow; now: Date; onOpen: (id: string) => void }) {
  const t = useT();
  const tone = availableTone(row);
  const moved = row.lastMovementAt ? movedWhen(row.lastMovementAt, now) : null;
  const when = !moved
    ? t("нет движений")
    : moved.when === "today"
      ? t("сегодня {time}", { time: moved.time })
      : moved.when === "yesterday"
        ? t("вчера {time}", { time: moved.time })
        : moved.date;
  const notes = [
    row.expired > 0 && { text: t("просрочено {n} {unit}", { n: quantity(row.expired), unit: row.unitCode }), tone: "text-bad" },
    row.onControl > 0 && { text: t("на контроле {n} {unit}", { n: quantity(row.onControl), unit: row.unitCode }), tone: "text-warn" },
    row.blocked > 0 && { text: t("забраковано {n} {unit}", { n: quantity(row.blocked), unit: row.unitCode }), tone: "text-bad" },
  ].filter(Boolean) as { text: string; tone: string }[];
  return (
    <li>
      <button type="button" onClick={() => onOpen(row.itemId)} className={`flex w-full items-center gap-3.5 px-4 py-3.5 text-left active:bg-muted ${row.isActive ? "" : "opacity-60"}`}>
        <ItemIcon row={row} size="size-11" />
        <span className="min-w-0 flex-1">
          <span className="line-clamp-2 text-base font-semibold leading-snug text-ink">{row.name}</span>
          <span className="mt-0.5 block truncate text-sm text-ink-3">
            <span className="tabular-nums">{row.sku}</span> · {row.categoryName} · {when}
          </span>
          {notes.length > 0 && (
            <span className="mt-0.5 flex flex-wrap gap-x-2 text-xs">
              {notes.map((n) => (
                <span key={n.text} className={n.tone}>
                  {n.text}
                </span>
              ))}
            </span>
          )}
        </span>
        <span className="shrink-0 text-right">
          {tone === "empty" ? (
            <>
              <span className="inline-flex h-7 items-center rounded-lg bg-bad-soft px-2.5 text-sm font-semibold text-bad">{t("Нет")}</span>
              <span className="mt-1 block text-sm tabular-nums text-ink-3">
                0 {row.unitCode}
              </span>
            </>
          ) : (
            <>
              <span className={`block text-[20px] font-semibold leading-none tabular-nums ${tone === "low" ? "text-warn" : tone === "none" ? "text-ink-2" : "text-ok"}`}>
                {quantity(row.available)} <span className="text-sm font-medium text-ink-3">{row.unitCode}</span>
              </span>
              <span className="mt-1 block text-sm text-ink-3">{tone === "low" ? t("ниже мин.") : t("доступно")}</span>
            </>
          )}
        </span>
      </button>
    </li>
  );
}

/**
 * Список на телефоне: «544 позиции» и порядок (тот же выбор, что в шторке), строки, «Загрузить ещё» — добирает следующие 50 к показанным.
 */
export function StockMobileList({
  rows,
  total,
  sort,
  now,
  onSort,
  onOpen,
  more,
  loading,
  onMore,
}: {
  rows: StockRow[];
  total: number;
  sort: string;
  now: Date;
  onSort: () => void;
  onOpen: (id: string) => void;
  more: boolean;
  loading: boolean;
  onMore: () => void;
}) {
  const t = useT();
  const label = mobileSorts.find((s) => s.key === sort)?.label ?? mobileSorts[0].label;
  return (
    <div className="lg:hidden">
      <div className="flex items-center justify-between gap-3 border-b border-line px-4 py-3.5">
        <span className="text-base font-semibold text-ink">
          {total.toLocaleString("ru-RU")} {t(plural(total, ["позиция", "позиции", "позиций"]))}
        </span>
        <button type="button" onClick={onSort} className="inline-flex items-center gap-1.5 text-sm text-ink-2">
          <ArrowUpDown className="size-4" aria-hidden /> {t(label)}
        </button>
      </div>
      <ul className="divide-y divide-line">
        {rows.map((r) => (
          <MobileRow key={r.itemId} row={r} now={now} onOpen={onOpen} />
        ))}
      </ul>
      {more && (
        <div className="border-t border-line">
          <button type="button" onClick={onMore} disabled={loading} className="flex h-14 w-full items-center justify-center gap-2 text-[15px] font-semibold text-accent-strong disabled:opacity-60">
            {loading && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("Загрузить ещё")}
          </button>
        </div>
      )}
    </div>
  );
}
