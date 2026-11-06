"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import {
  AlertTriangle,
  ArrowDownToLine,
  ArrowLeftRight,
  ArrowUpFromLine,
  CalendarClock,
  ChevronDown,
  ClipboardList,
  Ellipsis,
  FlaskConical,
  Loader2,
  PackageMinus,
  Pencil,
  Plus,
  ScanBarcode,
  X,
} from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { BarcodeScanner } from "@/components/scan/BarcodeScanner";
import { QualitySpecSheet } from "@/components/quality/QualitySpecSheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  activeWarehouses,
  batchQualityLabel,
  batchQualityTone,
  batchStateLabel,
  batchTone,
  qualityControlLabel,
  dateRu,
  money,
  movementKindLabel,
  parseNumber,
  priceSourceLabel,
  quantity,
  stampRu,
  type ItemCard,
  type ItemPrices,
  type ItemUnit,
  type Page,
  type RequestCategory,
  type RequestDepartment,
  type RequestUnit,
  type StockRow,
  type StockSummary,
  type Warehouse,
  type BatchRow,
  type CatalogOption,
  type QualityControl,
} from "@/lib/procurement";
import { allColumns, nextSort, readColumns, type StockColumn } from "@/lib/stock-view";
import { BatchEntrySheet } from "./BatchEntrySheet";
import { CatalogImport } from "./CatalogImport";
import { BatchAlerts, Dropdown, MenuItem, MinimumHint, SelectionBar, StockCards, StockFooter, StockTable, StockToolbar, toolButton } from "./StockBoard";
import { MinimumsSheet } from "./StockMinimums";
import { StockChips, StockFiltersSheet, StockMobileList, StockSearch, StockTiles } from "./StockMobile";
import { StockOperation, type OperationKind } from "./StockOperation";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 disabled:opacity-60";
const secondary = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted";

/**
 * Кнопки операций склада, как на макете: кладовщику — «Приход», «Выдача» и меню «Операции» (списание, инвентаризация, перемещение — когда
 * складов больше одного; по складам, где он проводит операции); тем, кто ведёт каталог, — загрузка каталога из Excel и «Новая позиция».
 */
export function StockActions({
  warehouse,
  catalog,
  departments,
  categories,
  units,
  warehouses,
  warehouseId,
}: {
  warehouse: boolean;
  catalog: boolean;
  departments: RequestDepartment[];
  categories: RequestCategory[];
  units: RequestUnit[];
  warehouses: Warehouse[];
  warehouseId?: string | null;
}) {
  const t = useT();
  const [op, setOp] = useState<OperationKind | null>(null);
  const [creating, setCreating] = useState(false);
  const camera = useCamera();
  const posts = warehouse && warehouses.some((w) => w.isActive && w.posts);
  const several = activeWarehouses(warehouses).length > 1;
  return (
    <div className="flex items-center gap-2">
      {camera && (
        <button
          type="button"
          onClick={() => window.dispatchEvent(new Event(scanEvent))}
          aria-label={t("Сканировать штрихкод")}
          className="grid size-12 place-items-center rounded-full border border-line-strong bg-surface text-ink hover:bg-muted lg:hidden"
        >
          <ScanBarcode className="size-6" aria-hidden />
        </button>
      )}
      {posts && (
        <div className="hidden items-center gap-2 lg:flex">
          <button type="button" onClick={() => setOp("receipt")} className={toolButton}>
            <ArrowDownToLine className="size-4" aria-hidden /> <span className="max-sm:sr-only">{t("Приход")}</span>
          </button>
          <button type="button" onClick={() => setOp("issue")} className={toolButton}>
            <ArrowUpFromLine className="size-4" aria-hidden /> <span className="max-sm:sr-only">{t("Выдача")}</span>
          </button>
          <Dropdown
            label={t("Операции")}
            align="right"
            button={(open, toggle) => (
              <button type="button" onClick={toggle} aria-expanded={open} aria-haspopup="menu" className={toolButton}>
                <Ellipsis className="size-4" aria-hidden /> <span className="max-sm:sr-only">{t("Операции")}</span>
                <ChevronDown className="size-4 text-ink-3 max-sm:hidden" aria-hidden />
              </button>
            )}
          >
            {(close) => (
              <>
                <MenuItem icon={PackageMinus} onClick={() => (close(), setOp("writeoff"))}>
                  {t("Списание")}
                </MenuItem>
                <MenuItem icon={ClipboardList} onClick={() => (close(), setOp("inventory"))}>
                  {t("Инвентаризация")}
                </MenuItem>
                {several && (
                  <MenuItem icon={ArrowLeftRight} onClick={() => (close(), setOp("transfer"))}>
                    {t("Перемещение")}
                  </MenuItem>
                )}
              </>
            )}
          </Dropdown>
        </div>
      )}
      {posts && catalog && <span aria-hidden className="mx-1 hidden h-7 w-px bg-line-strong lg:block" />}
      {/* Загрузка каталога из Excel — на компьютере; в шапке телефона, как на макете, — сканер и «+». */}
      {catalog && (
        <span className="hidden lg:contents">
          <CatalogImport compact />
        </span>
      )}
      {catalog && (
        <button
          type="button"
          onClick={() => setCreating(true)}
          className="grid size-12 place-items-center rounded-full bg-accent text-white shadow-lg shadow-accent/30 hover:bg-accent-strong lg:inline-flex lg:h-10 lg:w-auto lg:items-center lg:gap-2 lg:rounded-xl lg:px-4 lg:text-sm lg:font-semibold lg:shadow-none"
        >
          <Plus className="size-6 lg:size-4" aria-hidden /> <span className="max-lg:sr-only">{t("Новая позиция")}</span>
        </button>
      )}
      {op && <StockOperation kind={op} open onClose={() => setOp(null)} departments={departments} warehouses={warehouses} warehouseId={warehouseId} />}
      <ItemFormSheet open={creating} onClose={() => setCreating(false)} categories={categories} units={units} />
    </div>
  );
}

/** Кнопка сканера в шапке телефона открывает сканер экрана склада. */
const scanEvent = "onebase:stock-scan";

/** Камера есть, и браузер её даёт (https или localhost) — иначе кнопок сканера нет. Проверяется в браузере после загрузки. */
function useCamera() {
  const [camera, setCamera] = useState(false);
  useEffect(() => setCamera(typeof navigator !== "undefined" && Boolean(navigator.mediaDevices?.getUserMedia)), []);
  return camera;
}

/** Позиция строки для формы операции: остаток — выбранного склада или всех (склад формы подтянет свой). */
const asOption = (r: StockRow): CatalogOption => ({
  id: r.itemId,
  sku: r.sku,
  name: r.name,
  categoryId: r.categoryId,
  categoryName: r.categoryName,
  unitCode: r.unitCode,
  onHand: r.onHand,
  available: r.available,
  tracksBatches: r.tracksBatches,
  shelfLifeDays: r.shelfLifeDays,
});

const columnsKey = "onebase:stock:columns";

/**
 * Склад по макету: карточки-фильтры (все позиции, ниже минимума, нет на складе, в резерве; «К выдаче» — переход), сроки партий, подсказка о
 * минимумах; таблица в одной карточке с панелью фильтров (поиск, склад, категория, «Ещё фильтры», «Скрыть нулевые», Excel, колонки),
 * сортировкой по заголовкам, галочками для прихода и выдачи, быстрыми действиями и страницами. Строка открывает карточку позиции.
 * Фильтр, поиск, склад, сортировка и страница — в адресе.
 */
export function StockView({
  data,
  summary,
  categories,
  units,
  departments,
  warehouse,
  catalog,
  warehouses,
  quality = false,
  prices = false,
}: {
  data: Page<StockRow>;
  summary: StockSummary;
  categories: RequestCategory[];
  units: RequestUnit[];
  departments: RequestDepartment[];
  warehouse: boolean;
  catalog: boolean;
  warehouses: Warehouse[];
  /** Лаборатория: правит спецификации материалов. */
  quality?: boolean;
  /** История цен в карточке позиции — снабжению и директору. */
  prices?: boolean;
}) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [search, setSearch] = useState(params.get("q") ?? "");
  const [open, setOpen] = useState<string | null>(null);
  const [op, setOp] = useState<{ kind: OperationKind; options: CatalogOption[] } | null>(null);
  const [minimum, setMinimum] = useState<{ open: boolean; item: { id: string; name: string } | null }>({ open: false, item: null });
  const [selected, setSelected] = useState<Set<string>>(() => new Set());
  const [columns, setColumnsState] = useState<Set<StockColumn>>(() => new Set(allColumns));
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [extra, setExtra] = useState<StockRow[]>([]);
  const [loaded, setLoaded] = useState(data.page);
  const [loadingMore, setLoadingMore] = useState(false);
  const [moreError, setMoreError] = useState<string | null>(null);
  const [now] = useState(() => new Date());
  const camera = useCamera();
  const [scanning, setScanning] = useState(false);
  const [missing, setMissing] = useState<string | null>(null);
  const [newBarcode, setNewBarcode] = useState<string | null>(null);
  const filter = params.get("filter") ?? "";
  const category = params.get("category") ?? "";
  const store = params.get("warehouse") ?? "";
  const sort = params.get("sort") ?? "";
  const desc = params.get("desc") === "1";
  const hideEmpty = params.get("hideEmpty") === "1";
  const stores = activeWarehouses(warehouses);
  const posts = warehouse && warehouses.some((w) => w.isActive && w.posts);

  // Кнопка сканера в шапке (телефон) — событием: шапка и экран — соседние части страницы.
  useEffect(() => {
    const open = () => setScanning(true);
    window.addEventListener(scanEvent, open);
    return () => window.removeEventListener(scanEvent, open);
  }, []);

  /**
   * Код со сканера: в поиск; одна позиция с таким штрихкодом или артикулом (в том числе отключённая) — сразу её карточка; нет в каталоге —
   * сообщение и «Новая позиция с этим штрихкодом» тем, кто ведёт каталог.
   */
  const scanned = async (code: string) => {
    setScanning(false);
    setMissing(null);
    setSearch(code);
    try {
      const found = await bff<Page<StockRow>>(`procurement/stock?${new URLSearchParams({ q: code, filter: "all", pageSize: "5" })}`);
      const exact = found.items.filter((r) => r.barcode === code || r.sku === code);
      if (exact.length === 1) setOpen(exact[0].itemId);
      else if (found.items.length === 0) setMissing(code);
    } catch {
      // поиск по коду уже в строке поиска — список покажет, что нашлось
    }
  };

  // Колонки — из браузера (на сервере их нет: сначала все, потом сохранённые).
  useEffect(() => {
    setColumnsState(new Set(readColumns(window.localStorage.getItem(columnsKey))));
  }, []);
  const setColumns = (next: Set<StockColumn>) => {
    setColumnsState(next);
    window.localStorage.setItem(columnsKey, JSON.stringify([...next]));
  };

  // Другая страница или фильтр — выбор сбрасывается (галочки только у видимых строк), «Загрузить ещё» — начинается заново.
  useEffect(() => {
    setSelected(new Set());
    setExtra([]);
    setLoaded(data.page);
    setMoreError(null);
  }, [data]);

  const apply = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    if (!("page" in patch)) next.delete("page");
    router.push(`${pathname}${next.size ? `?${next}` : ""}`, { scroll: false });
  };

  useEffect(() => {
    const timer = window.setTimeout(() => {
      if ((params.get("q") ?? "") !== search.trim()) apply({ q: search.trim() || null });
    }, 400);
    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const exportQuery = new URLSearchParams();
  for (const key of ["q", "category", "filter", "sort", "desc", "warehouse", "hideEmpty"]) {
    const value = params.get(key);
    if (value) exportQuery.set(key, key === "desc" || key === "hideEmpty" ? "true" : value);
  }
  // Телефон: следующие 50 строк с теми же фильтром, поиском, складом и порядком — к уже показанным.
  const loadMore = async () => {
    const query = new URLSearchParams(exportQuery);
    query.set("page", String(loaded + 1));
    query.set("pageSize", String(data.pageSize));
    setLoadingMore(true);
    setMoreError(null);
    try {
      const next = await bff<Page<StockRow>>(`procurement/stock?${query}`);
      setExtra((rows) => {
        const seen = new Set([...data.items, ...rows].map((r) => r.itemId));
        return [...rows, ...next.items.filter((r) => !seen.has(r.itemId))];
      });
      setLoaded(next.page);
    } catch (e) {
      setMoreError((e as Error).message);
    } finally {
      setLoadingMore(false);
    }
  };
  // Фильтры шторки (кроме чипов): склад, категория, редкие фильтры, «скрыть нулевые», порядок.
  const sheetFilters = [store, category, ["", "low", "empty", "reserved"].includes(filter) ? "" : filter, hideEmpty ? "1" : "", sort].filter(Boolean).length;

  const exportHref = `/bff/api/procurement/stock/xlsx${exportQuery.size ? `?${exportQuery}` : ""}`;
  const operate = (kind: OperationKind, rows: StockRow[]) => {
    const list = kind === "issue" || kind === "writeoff" ? rows.filter((r) => r.available > 0) : rows;
    if (list.length > 0) setOp({ kind, options: list.map(asOption) });
  };
  const chosen = data.items.filter((r) => selected.has(r.itemId));

  return (
    <div className="space-y-4 lg:space-y-5">
      {/* Телефон (макет): плитки операций, поиск с «Фильтрами», чипы. */}
      {posts && <StockTiles several={stores.length > 1} onOperation={(kind) => setOp({ kind, options: [] })} />}
      <StockSearch search={search} onSearch={setSearch} active={sheetFilters} onFilters={() => setFiltersOpen(true)} />
      {missing && (
        <div role="status" className="flex flex-wrap items-center gap-x-4 gap-y-2 rounded-2xl border border-line bg-surface px-4 py-3">
          <ScanBarcode className="size-5 shrink-0 text-ink-3" aria-hidden />
          <p className="min-w-0 flex-1 text-sm text-ink">{t("Штрихкода {code} нет в каталоге.", { code: missing })}</p>
          {catalog && (
            <button type="button" onClick={() => (setNewBarcode(missing), setMissing(null))} className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong">
              <Plus className="size-4" aria-hidden /> {t("Новая позиция с этим штрихкодом")}
            </button>
          )}
          <button type="button" onClick={() => setMissing(null)} aria-label={t("Закрыть")} className="grid size-9 place-items-center rounded-lg text-ink-3 hover:bg-muted hover:text-ink">
            <X className="size-4" aria-hidden />
          </button>
        </div>
      )}
      <StockChips summary={summary} filter={filter} onFilter={(f) => apply({ filter: f || null })} />
      {/* Компьютер: карточки-фильтры. */}
      <StockCards summary={summary} filter={filter} onFilter={(f) => apply({ filter: f || null })} />
      <BatchAlerts summary={summary} filter={filter} onFilter={(f) => apply({ filter: f || null })} />
      {catalog && <MinimumHint summary={summary} onOpen={() => setMinimum({ open: true, item: null })} />}

      <section className="overflow-hidden rounded-2xl border border-line bg-surface">
        <StockToolbar
          search={search}
          onSearch={setSearch}
          stores={stores}
          store={store}
          categories={categories}
          category={category}
          filter={filter}
          summary={summary}
          hideEmpty={hideEmpty}
          exportHref={exportHref}
          columns={columns}
          onColumns={setColumns}
          apply={apply}
          onScan={camera ? () => setScanning(true) : undefined}
        />
        {posts && chosen.length > 0 && <SelectionBar count={chosen.length} onOperation={(kind) => operate(kind, chosen)} onClear={() => setSelected(new Set())} />}
        {data.items.length === 0 ? (
          <p className="px-4 py-12 text-center text-sm text-ink-3">
            {summary.items === 0 ? t("Каталог склада пуст — добавьте позиции кнопкой «Новая позиция», затем проведите приход.") : t("По этому фильтру позиций нет.")}
          </p>
        ) : (
          <>
            <StockTable
              rows={data.items}
              columns={columns}
              sort={sort}
              desc={desc}
              onSort={(key) => {
                const next = nextSort(sort, desc, key);
                apply({ sort: next.sort || null, desc: next.desc ? "1" : null });
              }}
              posts={posts}
              catalog={catalog}
              selected={selected}
              onSelect={setSelected}
              onOpen={setOpen}
              onOperation={operate}
              onMinimum={(r) => setMinimum({ open: true, item: { id: r.itemId, name: r.name } })}
            />
            <StockMobileList
              rows={extra.length ? [...data.items, ...extra] : data.items}
              total={data.total}
              sort={sort}
              now={now}
              onSort={() => setFiltersOpen(true)}
              onOpen={setOpen}
              more={loaded * data.pageSize < data.total}
              loading={loadingMore}
              onMore={() => void loadMore()}
            />
            {moreError && (
              <div className="px-4 pb-4 lg:hidden">
                <ErrorNote text={moreError} />
              </div>
            )}
          </>
        )}
        <StockFooter page={data.page} pageSize={data.pageSize} total={data.total} sort={sort} onPage={(page) => apply({ page: page > 1 ? String(page) : null })} />
      </section>

      <ItemSheet
        id={open}
        onClose={() => setOpen(null)}
        categories={categories}
        units={units}
        departments={departments}
        warehouse={warehouse}
        catalog={catalog}
        warehouses={warehouses}
        warehouseId={store || null}
        quality={quality}
        prices={prices}
      />
      {op && <StockOperation kind={op.kind} open onClose={() => setOp(null)} departments={departments} warehouses={warehouses} warehouseId={store || null} presets={op.options} />}
      <MinimumsSheet open={minimum.open} item={minimum.item} onClose={() => setMinimum({ open: false, item: null })} />
      <BarcodeScanner open={scanning} onClose={() => setScanning(false)} onCode={(code) => void scanned(code)} />
      <ItemFormSheet open={newBarcode !== null} onClose={() => setNewBarcode(null)} categories={categories} units={units} barcode={newBarcode ?? undefined} />
      <StockFiltersSheet
        open={filtersOpen}
        onClose={() => setFiltersOpen(false)}
        stores={stores}
        store={store}
        categories={categories}
        category={category}
        filter={filter}
        summary={summary}
        hideEmpty={hideEmpty}
        sort={sort}
        desc={desc}
        exportHref={exportHref}
        apply={apply}
      />
    </div>
  );
}

/** Карточка товара: остатки (выбранного склада или всех), по складам, резервы по заявкам, последние движения; правка каталога и быстрые операции. */
function ItemSheet({
  id,
  onClose,
  categories,
  units,
  departments,
  warehouse,
  catalog,
  warehouses,
  warehouseId,
  quality,
  prices = false,
}: {
  id: string | null;
  onClose: () => void;
  quality: boolean;
  /** История цен — снабжению и директору. */
  prices?: boolean;
  categories: RequestCategory[];
  units: RequestUnit[];
  departments: RequestDepartment[];
  warehouse: boolean;
  catalog: boolean;
  warehouses: Warehouse[];
  warehouseId: string | null;
}) {
  const t = useT();
  const [card, setCard] = useState<ItemCard | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const [op, setOp] = useState<OperationKind | null>(null);

  useEffect(() => {
    if (!id) return;
    let alive = true;
    setCard(null);
    setError(null);
    bff<ItemCard>(`procurement/items/${id}${warehouseId ? `?warehouse=${warehouseId}` : ""}`)
      .then((c) => alive && setCard(c))
      .catch((e: Error) => alive && setError(e.message));
    return () => {
      alive = false;
    };
  }, [id, op, editing, warehouseId]);

  const several = activeWarehouses(warehouses).length > 1;
  const posts = warehouse && warehouses.some((w) => w.isActive && w.posts);
  const storeName = warehouseId ? warehouses.find((w) => w.id === warehouseId)?.name : null;

  const item = card?.item;
  const option = item
    ? {
        id: item.itemId,
        sku: item.sku,
        name: item.name,
        categoryId: item.categoryId,
        categoryName: item.categoryName,
        unitCode: item.unitCode,
        onHand: item.onHand,
        available: item.available,
        tracksBatches: item.tracksBatches,
        shelfLifeDays: item.shelfLifeDays,
        units: card?.units ?? [],
      }
    : null;
  const [history, setHistory] = useState<ItemPrices | null>(null);
  useEffect(() => {
    if (!id || !prices) {
      setHistory(null);
      return;
    }
    let alive = true;
    bff<ItemPrices>(`procurement/items/${id}/prices`)
      .then((p) => alive && setHistory(p))
      .catch(() => alive && setHistory(null));
    return () => {
      alive = false;
    };
  }, [id, prices, op]);
  const [batches, setBatches] = useState<BatchRow[] | null>(null);
  const [entering, setEntering] = useState(false);
  const [spec, setSpec] = useState(false);
  const tracks = !!item?.tracksBatches;
  useEffect(() => {
    if (!id || !tracks) {
      setBatches(null);
      return;
    }
    let alive = true;
    bff<Page<BatchRow>>(`procurement/batches?item=${id}${warehouseId ? `&warehouse=${warehouseId}` : ""}&pageSize=50`)
      .then((p) => alive && setBatches(p.items))
      .catch(() => alive && setBatches([]));
    return () => {
      alive = false;
    };
  }, [id, tracks, warehouseId, op]);

  return (
    <>
      <Sheet open={!!id && !editing && !op} onClose={onClose} title={item?.name ?? t("Позиция склада")} subtitle={item ? `${item.sku} · ${item.categoryName}` : undefined} size="xl">
        {error && <ErrorNote text={error} />}
        {!card && !error && (
          <p className="flex items-center gap-2 py-8 text-sm text-ink-3">
            <Loader2 className="size-4 animate-spin" aria-hidden /> {t("Загрузка")}
          </p>
        )}
        {item && (
          <div className="space-y-5">
            <div className="grid grid-cols-3 gap-2">
              {[
                { label: "На складе", value: item.onHand },
                { label: "В резерве", value: item.reserved },
                { label: "Доступно", value: item.available, tone: item.low ? "text-warn" : "text-ok" },
              ].map((x) => (
                <div key={x.label} className="rounded-xl bg-muted px-3 py-2.5">
                  <div className={`text-xl font-semibold tabular-nums text-ink ${x.tone ?? ""}`}>
                    {quantity(x.value)} <span className="text-sm font-normal text-ink-3">{item.unitCode}</span>
                  </div>
                  <div className="text-xs text-ink-3">{t(x.label)}</div>
                </div>
              ))}
            </div>
            {several && <p className="-mt-3 text-xs text-ink-3">{storeName ? t("Остаток склада «{name}».", { name: storeName }) : t("Остаток по всем складам.")}</p>}
            {item.low && (
              <p className="flex items-center gap-2 rounded-xl bg-warn-soft px-3 py-2 text-sm text-ink">
                <AlertTriangle className="size-4 text-warn" aria-hidden /> {t("Ниже минимального остатка ({n}) — пора пополнить.", { n: `${quantity(item.minQty)} ${item.unitCode}` })}
              </p>
            )}
            <div className="flex flex-wrap gap-2">
              {posts && (
                <>
                  <button type="button" onClick={() => setOp("receipt")} className={secondary}>
                    <ArrowDownToLine className="size-4" aria-hidden /> {t("Приход")}
                  </button>
                  <button type="button" onClick={() => setOp("issue")} className={secondary}>
                    <ArrowUpFromLine className="size-4" aria-hidden /> {t("Выдача")}
                  </button>
                  <button type="button" onClick={() => setOp("writeoff")} className={secondary}>
                    <PackageMinus className="size-4" aria-hidden /> {t("Списание")}
                  </button>
                  <button type="button" onClick={() => setOp("inventory")} className={secondary}>
                    <ClipboardList className="size-4" aria-hidden /> {t("Инвентаризация")}
                  </button>
                  {several && (
                    <button type="button" onClick={() => setOp("transfer")} className={secondary}>
                      <ArrowLeftRight className="size-4" aria-hidden /> {t("Перемещение")}
                    </button>
                  )}
                </>
              )}
              {catalog && (
                <button type="button" onClick={() => setEditing(true)} className={secondary}>
                  <Pencil className="size-4" aria-hidden /> {t("Изменить")}
                </button>
              )}
            </div>
            {item.description && <p className="whitespace-pre-line text-sm text-ink-2">{item.description}</p>}
            {(card.units?.length ?? 0) > 0 && (
              <p className="flex flex-wrap items-center gap-1.5 text-sm text-ink-2">
                <span className="text-ink-3">{t("Единицы закупки:")}</span>
                {card.units!.map((u) => (
                  <span key={u.unitCode} className="rounded-md bg-muted px-1.5 py-0.5 text-xs tabular-nums text-ink">
                    1 {u.unitCode} = {quantity(u.factor)} {item.unitCode}
                  </span>
                ))}
              </p>
            )}

            {several && card.warehouses && card.warehouses.length > 0 && (
              <section>
                <h3 className="mb-2 text-sm font-semibold text-ink">{t("По складам")}</h3>
                <ul className="divide-y divide-line rounded-xl border border-line">
                  {card.warehouses.map((w) => (
                    <li key={w.warehouseId} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                      <span className="min-w-0 truncate text-ink">{w.name}</span>
                      <span className="shrink-0 text-right tabular-nums text-ink">
                        {quantity(w.onHand)} {item.unitCode}
                        {w.reserved > 0 && <span className="block text-[11px] text-ink-3">{t("в резерве {n}", { n: quantity(w.reserved) })}</span>}
                      </span>
                    </li>
                  ))}
                </ul>
              </section>
            )}

            {tracks && (
              <p className="flex flex-wrap items-center gap-2 rounded-xl border border-line px-3 py-2 text-sm text-ink-2">
                <FlaskConical className="size-4 text-ink-3" aria-hidden />
                <span className="min-w-0 flex-1">
                  {t("Контроль качества при приходе: {mode}", { mode: t(qualityControlLabel[item.qualityControl]).toLowerCase() })}
                  {item.qualityControl !== "None" && <span className="block text-xs text-ink-3">{t("Новая партия — «на контроле», пока назначенный по качеству её не допустит.")}</span>}
                </span>
                <button type="button" onClick={() => setSpec(true)} className={secondary}>
                  {t(quality ? "Спецификация" : "Что проверяют")}
                </button>
              </p>
            )}
            {tracks && (
              <section>
                <h3 className="mb-2 text-sm font-semibold text-ink">{t("Партии")}</h3>
                {batches === null ? (
                  <p className="text-sm text-ink-3">{t("Загрузка")}</p>
                ) : batches.length === 0 ? (
                  <p className="text-sm text-ink-3">{t("Партий с остатком нет.")}</p>
                ) : (
                  <ul className="divide-y divide-line rounded-xl border border-line">
                    {batches.map((b) => (
                      <li key={b.id}>
                        <Link href={`/procurement/batches/${b.id}`} className="flex items-center justify-between gap-3 px-3 py-2 text-sm hover:bg-muted/50">
                          <span className="min-w-0">
                            <span className="block truncate text-ink">
                              {[b.number, b.supplierBatchNo].filter(Boolean).join(" · ")}
                              {b.quality !== "Approved" && (
                                <span className={`ml-1.5 rounded-md px-1.5 py-0.5 text-[11px] font-semibold ${batchQualityTone[b.quality]}`}>{t(batchQualityLabel[b.quality])}</span>
                              )}
                            </span>
                            <span className={`text-xs ${batchTone(b.state)}`}>
                              {b.expiresOn ? t("годна до {date}", { date: dateRu(b.expiresOn) }) : t("срок не указан")} · {t(batchStateLabel[b.state])}
                            </span>
                          </span>
                          <span className="shrink-0 tabular-nums text-ink">
                            {quantity(b.onHand)} {item.unitCode}
                          </span>
                        </Link>
                      </li>
                    ))}
                  </ul>
                )}
              </section>
            )}
            {!tracks && posts && item.onHand > 0 && (
              <p className="flex flex-wrap items-center gap-2 rounded-xl bg-muted px-3 py-2 text-sm text-ink-2">
                {t("Сырьё со сроком годности? Разложите остаток по партиям — дальше позиция будет вестись по партиям.")}
                <button type="button" onClick={() => setEntering(true)} className={secondary}>
                  <CalendarClock className="size-4" aria-hidden /> {t("Ввести партии")}
                </button>
              </p>
            )}

            {card.reservations.length > 0 && (
              <section>
                <h3 className="mb-2 text-sm font-semibold text-ink">{t("В резерве по заявкам")}</h3>
                <ul className="divide-y divide-line rounded-xl border border-line">
                  {card.reservations.map((r) => (
                    <li key={r.requestId}>
                      <Link href={`/requests/${r.requestId}`} className="flex items-center justify-between gap-3 px-3 py-2 text-sm hover:bg-muted/50">
                        <span className="min-w-0">
                          <span className="block truncate text-ink">
                            {r.requestNumber} · {r.requestTitle}
                          </span>
                          <span className="text-xs text-ink-3">{r.departmentName}</span>
                        </span>
                        <span className="shrink-0 tabular-nums text-ink">
                          {quantity(r.quantity)} {item.unitCode}
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              </section>
            )}

            {history && history.points.length > 0 && (
              <section>
                <h3 className="mb-2 text-sm font-semibold text-ink">
                  {t("Цены за год")} <span className="font-normal text-ink-3">· {t("за {unit}", { unit: history.unitCode })}</span>
                </h3>
                <div className="mb-2 grid grid-cols-3 gap-2">
                  {[
                    { label: "Последняя", value: history.last },
                    { label: "Лучшая", value: history.min, tone: "text-ok" },
                    { label: "Худшая", value: history.max, tone: "text-bad" },
                  ].map((x) => (
                    <div key={x.label} className="rounded-xl bg-muted px-3 py-2">
                      <div className={`text-base font-semibold tabular-nums text-ink ${x.tone ?? ""}`}>{money(x.value)}</div>
                      <div className="text-xs text-ink-3">{t(x.label)}</div>
                    </div>
                  ))}
                </div>
                <ul className="divide-y divide-line rounded-xl border border-line">
                  {[...history.points].reverse().slice(0, 10).map((p, i) => (
                    <li key={`${p.orderNumber}-${i}`} className="flex items-center justify-between gap-3 px-3 py-2 text-sm">
                      <span className="min-w-0">
                        <span className="block truncate text-ink">{t(p.supplierName)}</span>
                        <span className="block truncate text-xs text-ink-3">
                          {dateRu(p.date)} · {t(priceSourceLabel[p.source] ?? "покупка")} <span className="tabular-nums">{p.orderNumber}</span> · {quantity(p.quantity)} {history.unitCode}
                        </span>
                      </span>
                      <span className="shrink-0 tabular-nums text-ink">{money(p.price)}</span>
                    </li>
                  ))}
                </ul>
              </section>
            )}

            <section>
              <h3 className="mb-2 text-sm font-semibold text-ink">{t("Движения")}</h3>
              {card.movements.length === 0 ? (
                <p className="text-sm text-ink-3">{t("Движений ещё не было.")}</p>
              ) : (
                <ul className="divide-y divide-line rounded-xl border border-line">
                  {card.movements.map((m) => (
                    <li key={m.id} className="flex items-center gap-3 px-3 py-2 text-sm">
                      <span className="min-w-0 flex-1">
                        <span className="block text-ink">
                          {t(movementKindLabel[m.kind])} · <span className="tabular-nums text-ink-3">{m.documentNumber}</span>
                        </span>
                        <span className="block truncate text-xs text-ink-3">
                          {stampRu(m.createdAt)}
                          {several && m.warehouseName && ` · ${m.warehouseName}`}
                          {m.counterpartName && ` · ${m.kind === "TransferOut" ? "→" : "←"} ${m.counterpartName}`}
                          {m.actorName && ` · ${m.actorName}`}
                          {m.departmentName && ` · ${m.departmentName}`}
                          {m.requestNumber && ` · ${m.requestNumber}`}
                          {m.comment && ` · ${m.comment}`}
                        </span>
                      </span>
                      <span className={`shrink-0 text-right tabular-nums ${m.quantity > 0 ? "text-ok" : m.quantity < 0 ? "text-bad" : "text-ink-3"}`}>
                        {m.quantity > 0 ? "+" : ""}
                        {quantity(m.quantity)}
                        <span className="block text-[11px] text-ink-3">→ {quantity(m.balanceAfter)}</span>
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </div>
        )}
      </Sheet>
      {item && <ItemFormSheet open={editing} onClose={() => setEditing(false)} categories={categories} units={units} item={item} itemUnits={card?.units ?? []} />}
      {spec && item && <QualitySpecSheet itemId={item.itemId} onClose={() => setSpec(false)} />}
      {op && option && <StockOperation kind={op} open onClose={() => setOp(null)} departments={departments} warehouses={warehouses} warehouseId={warehouseId} preset={option} />}
      {entering && card && item && (
        <BatchEntrySheet
          item={item}
          stock={(card.warehouses ?? []).filter((w) => w.onHand > 0)}
          onClose={() => {
            setEntering(false);
            setOp(null);
          }}
        />
      )}

    </>
  );
}

const emptyForm = {
  sku: "",
  name: "",
  categoryId: "",
  unitCode: "шт",
  barcode: "",
  minQty: "",
  description: "",
  isActive: true,
  tracksBatches: false,
  shelfLifeDays: "",
  expiryWarningDays: "",
  storageConditions: "",
  qualityControl: "None" as QualityControl,
  purchaseUnits: [] as { key: string; unitCode: string; factor: string }[],
};

let unitSeq = 0;
const unitRow = (unitCode = "", factor = "") => ({ key: `u-${Date.now()}-${unitSeq++}`, unitCode, factor });

/**
 * Новая позиция каталога или правка: артикул, название, категория, единица, штрихкод, минимальный остаток, описание, активна; сырьё — учёт
 * по партиям (включается сразу только без остатка — иначе «Ввести партии»), срок хранения, за сколько дней напоминать о сроке, условия хранения;
 * единицы закупки — в чём приходит товар (мешок = 25 кг): приход и тендер ведут в них, на складе — в единице карточки.
 */
function ItemFormSheet({
  open,
  onClose,
  categories,
  units,
  item,
  itemUnits = [],
  barcode,
}: {
  open: boolean;
  onClose: () => void;
  categories: RequestCategory[];
  units: RequestUnit[];
  item?: StockRow;
  itemUnits?: ItemUnit[];
  /** Новая позиция со штрихкодом со сканера. */
  barcode?: string;
}) {
  const t = useT();
  const router = useRouter();
  const [form, setForm] = useState(emptyForm);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setError(null);
    setForm(
      item
        ? {
            sku: item.sku,
            name: item.name,
            categoryId: item.categoryId,
            unitCode: item.unitCode,
            barcode: item.barcode ?? "",
            minQty: item.minQty ? String(item.minQty).replace(".", ",") : "",
            description: item.description ?? "",
            isActive: item.isActive,
            tracksBatches: item.tracksBatches,
            shelfLifeDays: item.shelfLifeDays ? String(item.shelfLifeDays) : "",
            expiryWarningDays: item.expiryWarningDays !== null ? String(item.expiryWarningDays) : "",
            storageConditions: item.storageConditions ?? "",
            qualityControl: item.qualityControl,
            purchaseUnits: itemUnits.map((u) => unitRow(u.unitCode, String(u.factor).replace(".", ","))),
          }
        : { ...emptyForm, barcode: barcode ?? "" },
    );
    // itemUnits — из карточки, меняется вместе с item.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, item, barcode]);

  const save = async () => {
    setError(null);
    if (!form.sku.trim() || !form.name.trim() || !form.categoryId) {
      setError(t("Укажите артикул, название и категорию."));
      return;
    }
    const purchase = form.purchaseUnits.filter((u) => u.unitCode || u.factor.trim());
    for (const u of purchase) {
      const factor = parseNumber(u.factor);
      if (!u.unitCode || factor === null || factor <= 0) {
        setError(t("Единица закупки: выберите единицу и укажите, сколько {unit} в одной.", { unit: form.unitCode }));
        return;
      }
      if (u.unitCode === form.unitCode) {
        setError(t("«{code}» — единица карточки: единица закупки должна быть другой.", { code: u.unitCode }));
        return;
      }
    }
    setBusy(true);
    try {
      const days = (text: string) => (text.trim() ? Number(text.trim()) : null);
      const { purchaseUnits: _drop, ...rest } = form;
      void _drop;
      const body = JSON.stringify({
        ...rest,
        units: purchase.map((u) => ({ unitCode: u.unitCode, factor: parseNumber(u.factor) })),
        minQty: parseNumber(form.minQty) ?? 0,
        shelfLifeDays: days(form.shelfLifeDays),
        expiryWarningDays: days(form.expiryWarningDays),
        storageConditions: form.storageConditions.trim() || null,
        qualityControl: form.tracksBatches ? form.qualityControl : "None",
      });
      if (item) await bff(`procurement/items/${item.itemId}`, { method: "PUT", body });
      else await bff("procurement/items", { method: "POST", body });
      onClose();
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const set = (patch: Partial<typeof form>) => setForm((f) => ({ ...f, ...patch }));
  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={item ? t("Изменить позицию") : t("Новая позиция каталога")}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted">
            {t("Закрыть")}
          </button>
          <button type="button" onClick={() => void save()} disabled={busy} className="inline-flex h-10 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50">
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("Сохранить")}
          </button>
        </div>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Артикул (SKU)")}</span>
          <input value={form.sku} onChange={(e) => set({ sku: e.target.value })} maxLength={64} className={field} autoFocus={!item} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Штрихкод")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <input value={form.barcode} onChange={(e) => set({ barcode: e.target.value })} maxLength={64} className={field} />
        </label>
        <label className="block sm:col-span-2">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Название")}</span>
          <input value={form.name} onChange={(e) => set({ name: e.target.value })} maxLength={300} className={field} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Категория")}</span>
          <select value={form.categoryId} onChange={(e) => set({ categoryId: e.target.value })} className={field}>
            <option value="">{t("Выберите категорию")}</option>
            {categories.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </label>
        <div className="grid grid-cols-2 gap-3">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Ед.")}</span>
            <select value={form.unitCode} onChange={(e) => set({ unitCode: e.target.value })} className={field}>
              {units.map((u) => (
                <option key={u.code} value={u.code}>
                  {u.code}
                </option>
              ))}
            </select>
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Минимум")}</span>
            <input value={form.minQty} onChange={(e) => set({ minQty: e.target.value })} inputMode="decimal" placeholder="0" className={`${field} tabular-nums`} />
          </label>
        </div>
        <fieldset className="rounded-xl border border-line p-3 sm:col-span-2">
          <legend className="px-1 text-sm font-medium text-ink">{t("Единицы закупки")}</legend>
          <p className="text-xs text-ink-3">
            {t("В чём приходит товар, если не в {unit}: например, мешок = 25 {unit}. Приход и тендер можно вести в них — на склад встанет в {unit}.", { unit: form.unitCode })}
          </p>
          {form.purchaseUnits.length > 0 && (
            <ul className="mt-2 space-y-2">
              {form.purchaseUnits.map((u, i) => (
                <li key={u.key} className="flex items-center gap-2">
                  <span className="shrink-0 text-sm text-ink-3">1</span>
                  <select
                    value={u.unitCode}
                    onChange={(e) => set({ purchaseUnits: form.purchaseUnits.map((x) => (x.key === u.key ? { ...x, unitCode: e.target.value } : x)) })}
                    aria-label={t("Единица закупки {n}", { n: i + 1 })}
                    className={`${field} w-28`}
                  >
                    <option value="">{t("ед.")}</option>
                    {units
                      .filter((x) => x.code !== form.unitCode)
                      .map((x) => (
                        <option key={x.code} value={x.code}>
                          {x.code}
                        </option>
                      ))}
                  </select>
                  <span className="shrink-0 text-sm text-ink-3">=</span>
                  <input
                    value={u.factor}
                    onChange={(e) => set({ purchaseUnits: form.purchaseUnits.map((x) => (x.key === u.key ? { ...x, factor: e.target.value } : x)) })}
                    inputMode="decimal"
                    placeholder="25"
                    aria-label={t("Сколько {unit} в единице закупки {n}", { unit: form.unitCode, n: i + 1 })}
                    className={`${field} w-28 tabular-nums`}
                  />
                  <span className="shrink-0 text-sm text-ink-3">{form.unitCode}</span>
                  <button
                    type="button"
                    onClick={() => set({ purchaseUnits: form.purchaseUnits.filter((x) => x.key !== u.key) })}
                    aria-label={t("Убрать единицу закупки {n}", { n: i + 1 })}
                    className="grid size-10 shrink-0 place-items-center rounded-md text-ink-3 hover:text-bad"
                  >
                    <X className="size-4" aria-hidden />
                  </button>
                </li>
              ))}
            </ul>
          )}
          {form.purchaseUnits.length < 10 && (
            <button
              type="button"
              onClick={() => set({ purchaseUnits: [...form.purchaseUnits, unitRow()] })}
              className="mt-2 inline-flex h-8 items-center gap-1.5 rounded-lg px-2 text-xs font-semibold text-accent-strong hover:bg-accent-soft"
            >
              <Plus className="size-3.5" aria-hidden /> {t("Добавить единицу закупки")}
            </button>
          )}
        </fieldset>
        <label className="block sm:col-span-2">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Описание")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <textarea value={form.description} onChange={(e) => set({ description: e.target.value })} maxLength={1000} rows={2} className={`${field} h-auto py-2.5`} />
        </label>
        <fieldset className="rounded-xl border border-line p-3 sm:col-span-2">
          <legend className="px-1 text-sm font-medium text-ink">{t("Сырьё: партии и срок годности")}</legend>
          <label className="flex items-start gap-2 text-sm text-ink">
            <input
              type="checkbox"
              checked={form.tracksBatches}
              onChange={(e) => set({ tracksBatches: e.target.checked })}
              disabled={!!item && !item.tracksBatches && item.onHand > 0}
              className="mt-0.5 size-4 accent-[var(--color-accent)]"
            />
            <span>
              {t("Вести по партиям — приход с номером партии и сроком годности, выдача только годного (сначала ближайший срок)")}
              {item && !item.tracksBatches && item.onHand > 0 && (
                <span className="block text-xs text-ink-3">{t("У позиции есть остаток — разложите его по партиям кнопкой «Ввести партии» в карточке.")}</span>
              )}
            </span>
          </label>
          <div className="mt-3 grid gap-3 sm:grid-cols-3">
            <label className="block">
              <span className="mb-1.5 block text-xs font-medium text-ink-2">{t("Срок хранения, дней")}</span>
              <input value={form.shelfLifeDays} onChange={(e) => set({ shelfLifeDays: e.target.value })} inputMode="numeric" placeholder="180" className={`${field} tabular-nums`} />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-xs font-medium text-ink-2">{t("Напоминать за, дней")}</span>
              <input value={form.expiryWarningDays} onChange={(e) => set({ expiryWarningDays: e.target.value })} inputMode="numeric" placeholder="30" className={`${field} tabular-nums`} />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-xs font-medium text-ink-2">{t("Условия хранения")}</span>
              <input value={form.storageConditions} onChange={(e) => set({ storageConditions: e.target.value })} maxLength={300} placeholder={t("сухо, до +25 °C")} className={field} />
            </label>
          </div>
          <p className="mt-2 text-xs text-ink-3">{t("Срок хранения подставляет срок годности по дате производства, если в накладной его нет.")}</p>
          <label className="mt-3 block">
            <span className="mb-1.5 block text-xs font-medium text-ink-2">{t("Контроль качества при приходе")}</span>
            <select
              value={form.tracksBatches ? form.qualityControl : "None"}
              onChange={(e) => set({ qualityControl: e.target.value as QualityControl })}
              disabled={!form.tracksBatches}
              className={field}
            >
              {(["None", "Documents", "Lab"] as const).map((c) => (
                <option key={c} value={c}>
                  {t(qualityControlLabel[c])}
                </option>
              ))}
            </select>
            <span className="mt-1 block text-xs text-ink-3">
              {t(
                form.tracksBatches
                  ? "С контролем новая партия не выдаётся, пока назначенный по качеству её не допустит. Что проверяют — в спецификации материала."
                  : "Контроль качества — у позиции, которая ведётся по партиям.",
              )}
            </span>
          </label>
        </fieldset>

        {item && (
          <label className="flex items-center gap-2 text-sm text-ink sm:col-span-2">
            <input type="checkbox" checked={form.isActive} onChange={(e) => set({ isActive: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
            {t("Позиция активна — её можно выбрать в заявке и принять на склад")}
          </label>
        )}
      </div>
      <div className="mt-3">
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
