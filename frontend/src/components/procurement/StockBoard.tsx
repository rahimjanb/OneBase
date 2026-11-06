"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import {
  ArrowDown,
  ArrowDownToLine,
  ArrowRight,
  ArrowUp,
  ArrowUpFromLine,
  Boxes,
  CalendarClock,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ClipboardList,
  Columns3,
  EllipsisVertical,
  FileDown,
  FlaskConical,
  Lightbulb,
  ListFilter,
  Lock,
  Minus,
  Package,
  PackageOpen,
  PackageMinus,
  PackageX,
  ScanBarcode,
  Search,
  ShieldAlert,
  SlidersHorizontal,
  Tag,
  TrendingDown,
  Warehouse as WarehouseIcon,
  X,
} from "lucide-react";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import { dateRu, movementKindLabel, quantity, type RequestCategory, type StockRow, type StockSummary, type Warehouse } from "@/lib/procurement";
import { availableTone, minimumHint, movedWhen, pageList, type StockColumn } from "@/lib/stock-view";
import type { OperationKind } from "./StockOperation";

/** Необязательные колонки таблицы: выбор хранится в браузере. */
export const stockColumns: { key: StockColumn; label: string }[] = [
  { key: "reserved", label: "В резерве" },
  { key: "min", label: "Минимум" },
  { key: "moved", label: "Последнее движение" },
];

/** Порядок списка: ключ сервера (пусто — по названию) и подпись под таблицей. */
export const stockSorts: Record<string, string> = {
  "": "сортировка по названию",
  sku: "сортировка по артикулу",
  onHand: "сортировка по остатку",
  available: "сортировка по доступному",
  reserved: "сортировка по резерву",
  category: "сортировка по категории",
  moved: "сортировка по дате движения",
};

/** Фильтры «Ещё фильтры» — те, что не на карточках; сроки и качество — когда есть позиции по партиям. */
export const moreFilters: { value: string; label: string; batches?: boolean; count?: (s: StockSummary) => number }[] = [
  { value: "expiring", label: "Истекает срок", batches: true, count: (s) => s.expiring },
  { value: "expired", label: "Просрочено", batches: true, count: (s) => s.expired },
  { value: "control", label: "На контроле", batches: true, count: (s) => s.onControl },
  { value: "blocked", label: "Забраковано и заблокировано", batches: true, count: (s) => s.blocked },
  { value: "inactive", label: "Отключённые" },
  { value: "all", label: "Все, включая отключённые" },
];

export const toolButton =
  "inline-flex h-10 items-center gap-2 rounded-xl border border-line-strong bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const iconButton = "grid size-10 shrink-0 place-items-center rounded-xl border border-line-strong bg-surface text-ink-2 hover:bg-muted hover:text-ink";
const menuPanel = "absolute top-full z-30 mt-1.5 rounded-xl border border-line-strong bg-surface-solid p-1.5 shadow-[0_16px_40px_rgb(0_0_0/0.35)]";
const menuItem = "flex h-10 w-full items-center gap-2.5 rounded-lg px-2.5 text-left text-sm font-medium text-ink hover:bg-muted";

/** Выпадающее меню: закрывается щелчком мимо и Esc. */
export function Dropdown({
  button,
  children,
  align = "left",
  width = "w-60",
  label,
}: {
  button: (open: boolean, toggle: () => void) => React.ReactNode;
  children: (close: () => void) => React.ReactNode;
  align?: "left" | "right";
  width?: string;
  label?: string;
}) {
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => !box.current?.contains(e.target as Node) && setOpen(false);
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);
  return (
    <div ref={box} className="relative">
      {button(open, () => setOpen((o) => !o))}
      {open && (
        <div role="menu" aria-label={label} className={`${menuPanel} ${width} ${align === "right" ? "right-0" : "left-0"}`}>
          {children(() => setOpen(false))}
        </div>
      )}
    </div>
  );
}

export function MenuItem({ icon: Icon, children, onClick, tone }: { icon: typeof Package; children: React.ReactNode; onClick: () => void; tone?: string }) {
  return (
    <button type="button" role="menuitem" onClick={onClick} className={`${menuItem} ${tone ?? ""}`}>
      <Icon className="size-4 shrink-0 text-ink-3" aria-hidden />
      {children}
    </button>
  );
}

// ---------- Карточки-фильтры ----------

const tones = {
  accent: { tile: "bg-accent-soft text-accent-strong", value: "text-accent-strong" },
  warn: { tile: "bg-warn-soft text-warn", value: "text-warn" },
  bad: { tile: "bg-bad-soft text-bad", value: "text-bad" },
  violet: { tile: "bg-violet-soft text-violet", value: "text-violet" },
  plain: { tile: "bg-muted text-ink", value: "text-ink" },
} as const;

/**
 * Пять карточек склада, как на макете: все позиции, ниже минимума, нет на складе (доля каталога), в резерве — фильтры таблицы (выбранная —
 * с галочкой); «К выдаче» — переход к заявкам, которые ждут выдачи. Цифра цветная, только когда не ноль.
 */
export function StockCards({ summary, filter, onFilter }: { summary: StockSummary; filter: string; onFilter: (filter: string) => void }) {
  const t = useT();
  const share = summary.items > 0 ? Math.round((summary.empty / summary.items) * 100) : 0;
  const cards = [
    { key: "", label: "Все позиции", value: summary.items, caption: t("в каталоге"), icon: Boxes, tone: "plain" as const },
    { key: "low", label: "Ниже минимума", value: summary.low, caption: t("нужна закупка"), icon: TrendingDown, tone: "warn" as const },
    { key: "empty", label: "Нет на складе", value: summary.empty, caption: t("{n}% каталога", { n: share }), icon: PackageX, tone: "bad" as const },
    { key: "reserved", label: "В резерве", value: summary.withReserve, caption: t("под заявки"), icon: Lock, tone: "violet" as const },
  ];
  const waiting = summary.toIssueRequests;
  return (
    <div className="hidden gap-4 lg:grid lg:grid-cols-5">
      {cards.map((c) => {
        const active = filter === c.key;
        const tone = tones[c.tone];
        return (
          <button
            key={c.key || "all"}
            type="button"
            onClick={() => onFilter(c.key)}
            aria-pressed={active}
            className={`flex items-center gap-3 rounded-2xl border bg-surface p-4 text-left transition-colors hover:border-accent/40 ${active ? "border-accent ring-1 ring-accent/30" : "border-line"}`}
          >
            <span className={`grid size-11 shrink-0 place-items-center rounded-xl ${tone.tile}`}>
              <c.icon className="size-5" aria-hidden />
            </span>
            <span className="min-w-0 flex-1">
              <span className="block truncate text-sm text-ink-2">{t(c.label)}</span>
              <span className="mt-0.5 flex items-baseline gap-2">
                <span className={`text-2xl font-semibold tabular-nums ${c.value > 0 ? tone.value : "text-ink"}`}>{c.value.toLocaleString("ru-RU")}</span>
                <span className="truncate text-xs text-ink-3">{c.caption}</span>
              </span>
            </span>
            {active && <Check className="size-5 shrink-0 text-accent" aria-hidden />}
          </button>
        );
      })}
      <Link href="/procurement/issue" className="flex items-center gap-3 rounded-2xl border border-line bg-surface p-4 transition-colors hover:border-accent/40">
        <span className={`grid size-11 shrink-0 place-items-center rounded-xl ${tones.accent.tile}`}>
          <PackageOpen className="size-5" aria-hidden />
        </span>
        <span className="min-w-0 flex-1">
          <span className="block truncate text-sm text-ink-2">{t("К выдаче")}</span>
          <span className="mt-0.5 flex items-baseline gap-2">
            <span className={`text-2xl font-semibold tabular-nums ${waiting > 0 ? tones.accent.value : "text-ink"}`}>{waiting.toLocaleString("ru-RU")}</span>
            <span className="truncate text-xs text-ink-3">{waiting > 0 ? t(plural(waiting, ["заявка ждёт", "заявки ждут", "заявок ждут"])) : t("заявок нет")}</span>
          </span>
        </span>
      </Link>
    </div>
  );
}

/** Сроки и качество партий — строкой под карточками, только когда что-то есть: просроченное и на контроле не должно теряться. */
export function BatchAlerts({ summary, filter, onFilter }: { summary: StockSummary; filter: string; onFilter: (filter: string) => void }) {
  const t = useT();
  const list = moreFilters.filter((f) => f.batches && (f.count?.(summary) ?? 0) > 0);
  if (summary.tracked === 0 || list.length === 0) return null;
  return (
    <div className="flex flex-wrap items-center gap-2 text-sm">
      <span className="inline-flex items-center gap-1.5 text-ink-3">
        <CalendarClock className="size-4" aria-hidden /> {t("Партии:")}
      </span>
      {list.map((f) => (
        <button
          key={f.value}
          type="button"
          onClick={() => onFilter(filter === f.value ? "" : f.value)}
          aria-pressed={filter === f.value}
          className={`inline-flex h-8 items-center gap-2 rounded-full border px-3 ${filter === f.value ? "border-accent bg-accent-soft text-ink" : "border-line bg-surface text-ink-2 hover:text-ink"}`}
        >
          <span className={`size-2 rounded-full ${f.value === "expiring" || f.value === "control" ? "bg-warn" : "bg-bad"}`} aria-hidden />
          {t(f.label)}
          <span className="tabular-nums text-ink-3">{f.count!(summary)}</span>
        </button>
      ))}
    </div>
  );
}

/**
 * Подсказка о минимумах (тем, кто ведёт каталог): без минимума система не подскажет, что пора закупать. Большинство позиций без минимума —
 * текст макета; иначе — сколько ходовых без минимума. Нечего задавать — подсказки нет.
 */
export function MinimumHint({ summary, onOpen }: { summary: StockSummary; onOpen: () => void }) {
  const t = useT();
  const hint = minimumHint(summary);
  if (!hint) return null;
  const moving = t("Ходовых позиций без минимума: {n} — система не подскажет, что их пора закупать.", { n: summary.noMinMoving });
  const long = hint === "most" ? t("У большинства позиций не задан минимум — система не сможет подсказать, что пора закупать. Задайте минимумы для ходовых позиций.") : moving;
  const short = hint === "most" ? t("У большинства позиций не задан минимум — подсказки о закупке не работают") : moving;
  return (
    <div className="flex items-start gap-3 rounded-2xl border border-warn/30 bg-warn-soft px-4 py-3.5 lg:items-center lg:gap-4 lg:px-5 lg:py-3">
      <Lightbulb className="mt-0.5 size-5 shrink-0 text-warn lg:mt-0" aria-hidden />
      <div className="min-w-0 flex-1 lg:flex lg:items-center lg:gap-4">
        <p className="min-w-0 text-sm font-medium text-ink lg:flex-1">
          <span className="lg:hidden">{short}</span>
          <span className="max-lg:hidden">{long}</span>
        </p>
        <button
          type="button"
          onClick={onOpen}
          className="mt-1.5 inline-flex items-center gap-1 text-sm font-semibold text-warn hover:underline lg:mt-0 lg:h-9 lg:shrink-0 lg:gap-2 lg:rounded-lg lg:bg-warn/15 lg:px-3.5 lg:hover:bg-warn/25 lg:hover:no-underline"
        >
          {t("Задать минимумы")}
          <ChevronRight className="size-4 lg:hidden" aria-hidden />
          <ArrowRight className="size-4 max-lg:hidden" aria-hidden />
        </button>
      </div>
    </div>
  );
}

// ---------- Панель фильтров ----------

function SelectBox({
  icon: Icon,
  value,
  onChange,
  label,
  children,
}: {
  icon: typeof Package;
  value: string;
  onChange: (value: string) => void;
  label: string;
  children: React.ReactNode;
}) {
  return (
    <label className="relative inline-flex min-w-0 items-center">
      <span className="sr-only">{label}</span>
      <Icon className="pointer-events-none absolute left-3 size-4 text-ink-3" aria-hidden />
      <select
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="h-10 max-w-[260px] cursor-pointer appearance-none truncate rounded-xl border border-line-strong bg-surface pl-9 pr-9 text-sm font-medium text-ink hover:bg-muted focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20"
      >
        {children}
      </select>
      <ChevronDown className="pointer-events-none absolute right-3 size-4 text-ink-3" aria-hidden />
    </label>
  );
}

/**
 * Панель над таблицей: поиск (артикул, название, штрихкод — ручной сканер вводит код как клавиатура), склад (когда складов больше одного),
 * категория, «Ещё фильтры» (сроки, качество, отключённые), «Скрыть нулевые», выгрузка в Excel по текущему фильтру, выбор колонок.
 */
export function StockToolbar({
  search,
  onSearch,
  stores,
  store,
  categories,
  category,
  filter,
  summary,
  hideEmpty,
  exportHref,
  columns,
  onColumns,
  apply,
  onScan,
}: {
  search: string;
  onSearch: (value: string) => void;
  stores: Warehouse[];
  store: string;
  categories: RequestCategory[];
  category: string;
  filter: string;
  summary: StockSummary;
  hideEmpty: boolean;
  exportHref: string;
  columns: Set<StockColumn>;
  onColumns: (columns: Set<StockColumn>) => void;
  apply: (patch: Record<string, string | null>) => void;
  /** Сканер камерой (значок в поиске, макет) — когда браузер даёт камеру. */
  onScan?: () => void;
}) {
  const t = useT();
  const more = moreFilters.filter((f) => !f.batches || summary.tracked > 0);
  const chosen = more.find((f) => f.value === filter);
  const emptyOnly = filter === "empty";
  return (
    <div className="hidden flex-wrap items-center gap-2.5 border-b border-line p-4 lg:flex">
      <label className="relative block min-w-[220px] flex-1 lg:max-w-[440px]">
        <Search className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
        <span className="sr-only">{t("Поиск")}</span>
        <input
          type="search"
          value={search}
          onChange={(e) => onSearch(e.target.value)}
          placeholder={t("Артикул, название, штрихкод")}
          className={`h-10 w-full rounded-xl border border-line-strong bg-surface pl-10 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 ${onScan ? "pr-11" : "pr-3"}`}
        />
        {onScan && (
          <button
            type="button"
            onClick={onScan}
            title={t("Сканировать штрихкод")}
            className="absolute right-1.5 top-1/2 grid size-8 -translate-y-1/2 place-items-center rounded-lg text-ink-3 hover:bg-muted hover:text-ink"
          >
            <ScanBarcode className="size-[18px]" aria-hidden />
            <span className="sr-only">{t("Сканировать штрихкод")}</span>
          </button>
        )}
      </label>
      {stores.length > 1 && (
        <SelectBox icon={WarehouseIcon} value={store} onChange={(v) => apply({ warehouse: v || null })} label={t("Склад")}>
          <option value="">{t("Все склады")}</option>
          {stores.map((w) => (
            <option key={w.id} value={w.id}>
              {w.name}
            </option>
          ))}
        </SelectBox>
      )}
      <SelectBox icon={Tag} value={category} onChange={(v) => apply({ category: v || null })} label={t("Категория")}>
        <option value="">{t("Все категории")}</option>
        {categories.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </SelectBox>
      <Dropdown
        label={t("Ещё фильтры")}
        width="w-72"
        button={(open, toggle) => (
          <button type="button" onClick={toggle} aria-expanded={open} aria-haspopup="menu" className={`${toolButton} ${chosen ? "border-accent text-accent-strong" : ""}`}>
            <ListFilter className="size-4" aria-hidden />
            <span className="max-w-[180px] truncate">{chosen ? t(chosen.label) : t("Ещё фильтры")}</span>
            <ChevronDown className="size-4 text-ink-3" aria-hidden />
          </button>
        )}
      >
        {(close) => (
          <>
            {more.map((f) => (
              <button
                key={f.value}
                type="button"
                role="menuitemradio"
                aria-checked={filter === f.value}
                onClick={() => {
                  close();
                  apply({ filter: filter === f.value ? null : f.value });
                }}
                className={menuItem}
              >
                <span className="grid size-4 shrink-0 place-items-center">{filter === f.value && <Check className="size-4 text-accent" aria-hidden />}</span>
                <span className="min-w-0 flex-1 truncate">{t(f.label)}</span>
                {f.count && <span className="tabular-nums text-ink-3">{f.count(summary)}</span>}
              </button>
            ))}
            {chosen && (
              <>
                <div role="separator" className="my-1 h-px bg-line" />
                <MenuItem
                  icon={X}
                  onClick={() => {
                    close();
                    apply({ filter: null });
                  }}
                >
                  {t("Сбросить фильтр")}
                </MenuItem>
              </>
            )}
          </>
        )}
      </Dropdown>

      <div className="ml-auto flex items-center gap-2.5">
        <button
          type="button"
          role="switch"
          aria-checked={hideEmpty}
          disabled={emptyOnly}
          title={emptyOnly ? t("В фильтре «Нет на складе» только нулевые позиции") : undefined}
          onClick={() => apply({ hideEmpty: hideEmpty ? null : "1" })}
          className="inline-flex h-10 items-center gap-2.5 rounded-xl px-1.5 text-sm font-medium text-ink disabled:opacity-50"
        >
          <span className={`relative h-6 w-10 shrink-0 rounded-full transition-colors ${hideEmpty ? "bg-accent" : "bg-muted ring-1 ring-inset ring-line-strong"}`} aria-hidden>
            <span className={`absolute top-1 size-4 rounded-full bg-white shadow transition-[left] ${hideEmpty ? "left-5" : "left-1"}`} />
          </span>
          {t("Скрыть нулевые")}
        </button>
        <a href={exportHref} download title={t("Выгрузить в Excel")} className={iconButton}>
          <FileDown className="size-4" aria-hidden />
          <span className="sr-only">{t("Выгрузить в Excel")}</span>
        </a>
        <Dropdown
          label={t("Колонки")}
          align="right"
          width="w-64"
          button={(open, toggle) => (
            <button type="button" onClick={toggle} aria-expanded={open} aria-haspopup="menu" title={t("Колонки")} className={iconButton}>
              <Columns3 className="size-4" aria-hidden />
              <span className="sr-only">{t("Колонки")}</span>
            </button>
          )}
        >
          {() => (
            <>
              <p className="px-2.5 pb-1 pt-1.5 text-xs text-ink-3">{t("Позиция, на складе и доступно — всегда")}</p>
              {stockColumns.map((c) => (
                <button
                  key={c.key}
                  type="button"
                  role="menuitemcheckbox"
                  aria-checked={columns.has(c.key)}
                  onClick={() => {
                    const next = new Set(columns);
                    if (next.has(c.key)) next.delete(c.key);
                    else next.add(c.key);
                    onColumns(next);
                  }}
                  className={menuItem}
                >
                  <span className={`grid size-4 shrink-0 place-items-center rounded border ${columns.has(c.key) ? "border-accent bg-accent text-white" : "border-line-strong"}`}>
                    {columns.has(c.key) && <Check className="size-3" aria-hidden />}
                  </span>
                  {t(c.label)}
                </button>
              ))}
            </>
          )}
        </Dropdown>
      </div>
    </div>
  );
}

// ---------- Таблица ----------

/** «Доступно» плашкой: нет на складе — красная, ниже минимума — жёлтая, всё в резерве или не выдаётся — серая, иначе зелёная. */
export function AvailablePill({ row }: { row: StockRow }) {
  const t = useT();
  const base = "inline-flex h-7 items-center gap-1.5 whitespace-nowrap rounded-lg px-2.5 text-sm font-semibold tabular-nums";
  const tone = availableTone(row);
  if (tone === "empty")
    return (
      <span className={`${base} bg-bad-soft text-bad`}>
        <PackageX className="size-3.5" aria-hidden /> {t("Нет на складе")}
      </span>
    );
  if (tone === "low")
    return (
      <span className={`${base} bg-warn-soft text-warn`} title={t("Ниже минимума")}>
        <TrendingDown className="size-3.5" aria-label={t("Ниже минимума")} /> {quantity(row.available)} <span className="font-medium">{row.unitCode}</span>
      </span>
    );
  if (tone === "none")
    return (
      <span className={`${base} bg-muted text-ink-2`} title={t("Всё в резерве или не выдаётся")}>
        0 <span className="font-medium">{row.unitCode}</span>
      </span>
    );
  return (
    <span className={`${base} bg-ok-soft text-ok`}>
      <span className="size-1.5 rounded-full bg-ok" aria-hidden /> {quantity(row.available)} <span className="font-medium">{row.unitCode}</span>
    </span>
  );
}

/** Последнее движение: «вчера, 12:36» и вид («приход»); движений нет — «движений не было». */
export function MovedCell({ row, now }: { row: StockRow; now: Date }) {
  const t = useT();
  if (!row.lastMovementAt) return <span className="whitespace-nowrap text-ink-3">{t("движений не было")}</span>;
  const m = movedWhen(row.lastMovementAt, now);
  const when = m.when === "today" ? t("сегодня, {time}", { time: m.time }) : m.when === "yesterday" ? t("вчера, {time}", { time: m.time }) : m.date;
  return (
    <span className="block whitespace-nowrap leading-tight">
      <span className="block text-ink">{when}</span>
      {row.lastMovementKind && <span className="mt-0.5 block text-xs text-ink-3">{t(movementKindLabel[row.lastMovementKind]).toLowerCase()}</span>}
    </span>
  );
}

/** Значок позиции: колба — ведётся по партиям (сырьё), коробка — без партий; остатка нет — серый. */
export function ItemIcon({ row, size = "size-10" }: { row: StockRow; size?: string }) {
  const Icon = row.tracksBatches ? FlaskConical : Package;
  return (
    <span className={`grid ${size} shrink-0 place-items-center rounded-xl ${row.onHand > 0 ? "bg-accent-soft text-accent-strong" : "bg-muted text-ink-3"}`}>
      <Icon className="size-[18px]" aria-hidden />
    </span>
  );
}

/** Галочка как на макете: скруглённый контур; отмечено и «частично» — заливка акцентом. */
function Box({ checked, onChange, label, inputRef }: { checked: boolean; onChange: () => void; label: string; inputRef?: React.Ref<HTMLInputElement> }) {
  return (
    <span className="relative inline-grid size-[18px] place-items-center align-middle">
      <input
        ref={inputRef}
        type="checkbox"
        checked={checked}
        onChange={onChange}
        aria-label={label}
        className="peer size-[18px] cursor-pointer appearance-none rounded-[5px] border-[1.5px] border-line-strong bg-transparent transition-colors checked:border-accent checked:bg-accent indeterminate:border-accent indeterminate:bg-accent hover:border-ink-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent/40"
      />
      <Check className="pointer-events-none absolute size-3 text-white opacity-0 peer-checked:opacity-100" strokeWidth={3} aria-hidden />
      <Minus className="pointer-events-none absolute size-3 text-white opacity-0 peer-indeterminate:opacity-100" strokeWidth={3} aria-hidden />
    </span>
  );
}

/** Пометки строки, которые не видны в колонках: просрочено, на контроле, забраковано, отключена. */
function RowNotes({ row }: { row: StockRow }) {
  const t = useT();
  const notes: { text: string; tone: string; icon: typeof Package }[] = [];
  if (row.expired > 0) notes.push({ text: t("просрочено {n} {unit}", { n: quantity(row.expired), unit: row.unitCode }), tone: "text-bad", icon: CalendarClock });
  if (row.onControl > 0) notes.push({ text: t("на контроле {n} {unit}", { n: quantity(row.onControl), unit: row.unitCode }), tone: "text-warn", icon: ShieldAlert });
  if (row.blocked > 0) notes.push({ text: t("забраковано {n} {unit}", { n: quantity(row.blocked), unit: row.unitCode }), tone: "text-bad", icon: ShieldAlert });
  if (row.nearestExpiry) notes.push({ text: t("ближайший срок {date}", { date: dateRu(row.nearestExpiry) }), tone: "text-ink-3", icon: CalendarClock });
  if (notes.length === 0) return null;
  return (
    <span className="mt-0.5 flex flex-wrap gap-x-3 text-xs">
      {notes.map((n) => (
        <span key={n.text} className={`inline-flex items-center gap-1 ${n.tone}`}>
          <n.icon className="size-3.5" aria-hidden /> {n.text}
        </span>
      ))}
    </span>
  );
}

function SortHead({
  label,
  sortKey,
  sort,
  desc,
  onSort,
  align = "left",
  className = "",
}: {
  label: string;
  sortKey: string;
  sort: string;
  desc: boolean;
  onSort: (key: string) => void;
  align?: "left" | "right";
  className?: string;
}) {
  const t = useT();
  const active = sort === sortKey;
  const Arrow = desc ? ArrowDown : ArrowUp;
  return (
    <th className={`px-3 py-3 font-semibold ${align === "right" ? "text-right" : "text-left"} ${className}`} aria-sort={active ? (desc ? "descending" : "ascending") : "none"}>
      <button type="button" onClick={() => onSort(sortKey)} className={`inline-flex items-center gap-1 whitespace-nowrap uppercase tracking-wide hover:text-ink ${active ? "text-ink" : ""}`}>
        {t(label)}
        {active && <Arrow className="size-3.5 text-accent" aria-hidden />}
      </button>
    </th>
  );
}

/**
 * Таблица остатков (компьютер): галочки — для прихода и выдачи по выбранным (кладовщику), позиция со значком, «артикул • категория» и
 * пометками сроков, на складе, в резерве, плашка «Доступно», минимум, последнее движение с видом; при наведении — «Приход», «Выдать»
 * (кладовщику) и «Мин.» (тем, кто ведёт каталог), меню ⋮. Строка открывает карточку позиции.
 */
export function StockTable({
  rows,
  columns,
  sort,
  desc,
  onSort,
  posts,
  catalog,
  selected,
  onSelect,
  onOpen,
  onOperation,
  onMinimum,
}: {
  rows: StockRow[];
  columns: Set<StockColumn>;
  sort: string;
  desc: boolean;
  onSort: (key: string) => void;
  /** Кладовщик: операции и галочки. */
  posts: boolean;
  /** Ведёт каталог: «Мин.». */
  catalog: boolean;
  selected: Set<string>;
  onSelect: (next: Set<string>) => void;
  onOpen: (id: string) => void;
  onOperation: (kind: OperationKind, rows: StockRow[]) => void;
  onMinimum: (row: StockRow) => void;
}) {
  const t = useT();
  const [now] = useState(() => new Date());
  const all = rows.length > 0 && rows.every((r) => selected.has(r.itemId));
  const some = rows.some((r) => selected.has(r.itemId));
  const head = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (head.current) head.current.indeterminate = some && !all;
  }, [some, all]);
  const quick = posts || catalog;
  const stop = (e: React.SyntheticEvent) => e.stopPropagation();
  const action = "pointer-events-auto inline-flex h-8 items-center gap-1.5 rounded-lg border border-line-strong bg-surface-solid px-2.5 text-xs font-semibold text-ink hover:bg-muted-solid";

  return (
    <div className="hidden overflow-x-auto lg:block">
      {/* Ширины колонок заданы, «Позиция» — остаток: длинное название обрезается, а не раздвигает таблицу. */}
      <table className="w-full min-w-[980px] table-fixed text-sm">
        <thead>
          <tr className="border-b border-line bg-muted/40 text-[11px] text-ink-3">
            {posts && (
              <th className="w-12 py-3 pl-5 pr-1">
                <Box
                  inputRef={head}
                  checked={all}
                  onChange={() => {
                    const next = new Set(selected);
                    for (const r of rows) if (all) next.delete(r.itemId);
                    else next.add(r.itemId);
                    onSelect(next);
                  }}
                  label={t("Выбрать все на странице")}
                />
              </th>
            )}
            <SortHead label="Позиция" sortKey="" sort={sort} desc={desc} onSort={onSort} className={posts ? "" : "pl-5"} />
            <SortHead label="На складе" sortKey="onHand" sort={sort} desc={desc} onSort={onSort} align="right" className="w-[140px]" />
            {columns.has("reserved") && <SortHead label="В резерве" sortKey="reserved" sort={sort} desc={desc} onSort={onSort} align="right" className="w-[120px]" />}
            <SortHead label="Доступно" sortKey="available" sort={sort} desc={desc} onSort={onSort} className="w-[190px] pl-6" />
            {columns.has("min") && <th className="w-[120px] whitespace-nowrap px-3 py-3 text-right font-semibold uppercase tracking-wide">{t("Минимум")}</th>}
            {columns.has("moved") && <SortHead label="Последнее движение" sortKey="moved" sort={sort} desc={desc} onSort={onSort} className="w-[200px] pl-6" />}
            <th className="w-16 py-3 pr-4">
              <span className="sr-only">{t("Действия")}</span>
            </th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => {
            const checked = selected.has(r.itemId);
            return (
              <tr
                key={r.itemId}
                onClick={() => onOpen(r.itemId)}
                className={`group cursor-pointer border-b border-line last:border-0 hover:bg-muted/60 ${checked ? "bg-accent-soft/40" : ""} ${r.isActive ? "" : "opacity-60"}`}
              >
                {posts && (
                  <td className="py-3 pl-5 pr-1" onClick={stop}>
                    <Box
                      checked={checked}
                      onChange={() => {
                        const next = new Set(selected);
                        if (checked) next.delete(r.itemId);
                        else next.add(r.itemId);
                        onSelect(next);
                      }}
                      label={t("Выбрать «{name}»", { name: r.name })}
                    />
                  </td>
                )}
                <td className={`relative py-3 pr-3 ${posts ? "pl-3" : "pl-5"}`}>
                  <div className="flex min-w-0 items-center gap-3.5">
                    <ItemIcon row={r} />
                    <div className={`min-w-0 flex-1 ${quick ? "group-hover:pr-[268px] group-has-[:focus-visible]:pr-[268px]" : ""}`}>
                      <span className="block truncate text-[15px] font-medium text-ink" title={r.name}>
                        {r.name}
                      </span>
                      <span className="mt-0.5 block truncate text-xs text-ink-3">
                        <span className="tabular-nums">{r.sku}</span> • {r.categoryName}
                        {!r.isActive && ` • ${t("отключена")}`}
                      </span>
                      <RowNotes row={r} />
                    </div>
                  </div>
                  {quick && (
                    <div className="pointer-events-none absolute inset-y-0 right-3 hidden items-center gap-1.5 group-hover:flex group-has-[:focus-visible]:flex" onClick={stop}>
                      {posts && r.isActive && (
                        <>
                          <button type="button" onClick={() => onOperation("receipt", [r])} className={action}>
                            <ArrowDownToLine className="size-3.5" aria-hidden /> {t("Приход")}
                          </button>
                          <button type="button" onClick={() => onOperation("issue", [r])} disabled={r.available <= 0} className={`${action} disabled:opacity-50`}>
                            <ArrowUpFromLine className="size-3.5" aria-hidden /> {t("Выдать")}
                          </button>
                        </>
                      )}
                      {catalog && (
                        <button type="button" onClick={() => onMinimum(r)} className={action}>
                          <SlidersHorizontal className="size-3.5" aria-hidden /> {t("Мин.")}
                        </button>
                      )}
                    </div>
                  )}
                </td>
                <td className="px-3 py-3 text-right tabular-nums">
                  <span className="text-[15px] font-semibold text-ink">{quantity(r.onHand)}</span> <span className="text-xs text-ink-3">{r.unitCode}</span>
                </td>
                {columns.has("reserved") && <td className="px-3 py-3 text-right tabular-nums text-ink-2">{r.reserved > 0 ? quantity(r.reserved) : "—"}</td>}
                <td className="py-3 pl-6 pr-3">
                  <AvailablePill row={r} />
                </td>
                {columns.has("min") && (
                  <td className="px-3 py-3 text-right tabular-nums">
                    {r.minQty > 0 ? (
                      <>
                        <span className="text-ink">{quantity(r.minQty)}</span> <span className="text-xs text-ink-3">{r.unitCode}</span>
                      </>
                    ) : (
                      <span className="text-ink-3">{t("не задан")}</span>
                    )}
                  </td>
                )}
                {columns.has("moved") && (
                  <td className="py-3 pl-6 pr-3">
                    <MovedCell row={r} now={now} />
                  </td>
                )}
                <td className="py-3 pr-4 text-right" onClick={stop}>
                  <RowMenu row={r} posts={posts} catalog={catalog} onOpen={onOpen} onOperation={onOperation} onMinimum={onMinimum} />
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

/** Меню ⋮ строки: карточка, операции кладовщика, минимум. */
export function RowMenu({
  row,
  posts,
  catalog,
  onOpen,
  onOperation,
  onMinimum,
}: {
  row: StockRow;
  posts: boolean;
  catalog: boolean;
  onOpen: (id: string) => void;
  onOperation: (kind: OperationKind, rows: StockRow[]) => void;
  onMinimum: (row: StockRow) => void;
}) {
  const t = useT();
  return (
    <Dropdown
      label={t("Действия с позицией")}
      align="right"
      button={(open, toggle) => (
        <button
          type="button"
          onClick={toggle}
          aria-expanded={open}
          aria-haspopup="menu"
          className={`ml-auto grid size-9 place-items-center rounded-lg text-ink-3 hover:bg-muted hover:text-ink ${open ? "bg-muted text-ink" : ""}`}
        >
          <EllipsisVertical className="size-4" aria-hidden />
          <span className="sr-only">{t("Действия с позицией «{name}»", { name: row.name })}</span>
        </button>
      )}
    >
      {(close) => {
        const run = (fn: () => void) => () => {
          close();
          fn();
        };
        return (
          <>
            <MenuItem icon={Package} onClick={run(() => onOpen(row.itemId))}>
              {t("Открыть карточку")}
            </MenuItem>
            {posts && row.isActive && (
              <>
                <div role="separator" className="my-1 h-px bg-line" />
                <MenuItem icon={ArrowDownToLine} onClick={run(() => onOperation("receipt", [row]))}>
                  {t("Приход")}
                </MenuItem>
                {row.available > 0 && (
                  <MenuItem icon={ArrowUpFromLine} onClick={run(() => onOperation("issue", [row]))}>
                    {t("Выдать")}
                  </MenuItem>
                )}
                {row.available > 0 && (
                  <MenuItem icon={PackageMinus} onClick={run(() => onOperation("writeoff", [row]))}>
                    {t("Списать")}
                  </MenuItem>
                )}
                <MenuItem icon={ClipboardList} onClick={run(() => onOperation("inventory", [row]))}>
                  {t("Инвентаризация")}
                </MenuItem>
              </>
            )}
            {catalog && (
              <>
                <div role="separator" className="my-1 h-px bg-line" />
                <MenuItem icon={SlidersHorizontal} onClick={run(() => onMinimum(row))}>
                  {row.minQty > 0 ? t("Изменить минимум") : t("Задать минимум")}
                </MenuItem>
              </>
            )}
          </>
        );
      }}
    </Dropdown>
  );
}

/** Выбрано галочками: приход и выдача по выбранным одним документом. */
export function SelectionBar({ count, onOperation, onClear }: { count: number; onOperation: (kind: OperationKind) => void; onClear: () => void }) {
  const t = useT();
  return (
    <div className="hidden items-center gap-3 border-b border-line bg-accent-soft/50 px-5 py-2.5 text-sm lg:flex">
      <span className="font-semibold text-ink">{t("Выбрано: {n}", { n: count })}</span>
      <button type="button" onClick={() => onOperation("receipt")} className={toolButton.replace("h-10", "h-9")}>
        <ArrowDownToLine className="size-4" aria-hidden /> {t("Приход по выбранным")}
      </button>
      <button type="button" onClick={() => onOperation("issue")} className={toolButton.replace("h-10", "h-9")}>
        <ArrowUpFromLine className="size-4" aria-hidden /> {t("Выдача по выбранным")}
      </button>
      <button type="button" onClick={onClear} className="ml-auto inline-flex h-9 items-center gap-1.5 rounded-lg px-2.5 text-ink-2 hover:bg-muted hover:text-ink">
        <X className="size-4" aria-hidden /> {t("Снять выбор")}
      </button>
    </div>
  );
}

// ---------- Страницы ----------

/** Подвал таблицы: «Показано 1–50 из 544 · сортировка по названию» и страницы. */
export function StockFooter({ page, pageSize, total, sort, onPage }: { page: number; pageSize: number; total: number; sort: string; onPage: (page: number) => void }) {
  const t = useT();
  const pages = Math.max(1, Math.ceil(total / pageSize));
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, total);
  const box = "grid size-10 place-items-center rounded-xl border text-sm font-semibold tabular-nums";
  return (
    <div className="hidden flex-wrap items-center justify-between gap-3 border-t border-line px-5 py-4 text-sm text-ink-2 lg:flex">
      <span>
        {t("Показано {from}–{to} из {total}", { from, to, total: total.toLocaleString("ru-RU") })} · {t(stockSorts[sort] ?? stockSorts[""])}
      </span>
      {pages > 1 && (
        <nav aria-label={t("Страницы")} className="flex items-center gap-1.5">
          <button type="button" onClick={() => onPage(page - 1)} disabled={page <= 1} className={`${box} border-line-strong bg-surface text-ink-2 hover:bg-muted disabled:opacity-40`}>
            <ChevronLeft className="size-4" aria-hidden />
            <span className="sr-only">{t("Назад")}</span>
          </button>
          {pageList(page, pages).map((n, i) =>
            n === "gap" ? (
              <span key={`g${i}`} className={`${box} border-transparent text-ink-3`} aria-hidden>
                …
              </span>
            ) : (
              <button
                key={n}
                type="button"
                onClick={() => onPage(n)}
                aria-current={n === page ? "page" : undefined}
                className={`${box} ${n === page ? "border-accent bg-accent text-white" : "border-line-strong bg-surface text-ink hover:bg-muted"}`}
              >
                {n}
              </button>
            ),
          )}
          <button type="button" onClick={() => onPage(page + 1)} disabled={page >= pages} className={`${box} border-line-strong bg-surface text-ink-2 hover:bg-muted disabled:opacity-40`}>
            <ChevronRight className="size-4" aria-hidden />
            <span className="sr-only">{t("Дальше")}</span>
          </button>
        </nav>
      )}
    </div>
  );
}
