"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from "react";
import { ArrowLeft, Banknote, Boxes, ChevronLeft, LayoutGrid, List, Package, Search, Store, Table2, Weight, X, type LucideIcon } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { ReportPlans } from "@/components/sales/PlanAvailability";
import { useT } from "@/i18n/client";
import { dayMonth, kg, money, monthGenitive, monthLabel, monthName, num, pct, plural } from "@/lib/sales/format";
import { planOf } from "@/lib/sales/query";
import type { ProductPhotos } from "@/lib/sales/product-photos";
import type { CategoryView, SalesMonth, SkuRow } from "@/lib/sales/types";
import { PhotoViewer, ProductImage, type ViewerItem } from "./photo";
import { ProductPhotosProvider, useAllProductPhotos } from "./PhotosContext";
import { CopySkus, matchesSku, SkuCard, SkuListRow, SkuTable, skuName, sortSkus } from "./sku-views";

/*
 * «Продажи · Ассортимент · Категория» (макеты ua4iR — компьютер, F4LXf — телефон; ТЗ prompt-prodazhi-assortiment и «— фото»): шапка с
 * обложкой (фото самого продаваемого артикула с фото), четыре KPI, артикулы — таблица или карточки с фото на компьютере, сетка с фото или
 * список на телефоне. Телефон и планшет (уже 1024) — своя шапка: «назад», месяц (шторка выбора), поиск по названию и артикулу; общая
 * шапка и полоса раздела на этом адресе скрыты (TopbarFrame, globals.css — data-sales-own-header). Палитра — макета (data-palette).
 * Выбранный вид запоминается в браузере: отдельно для компьютера (таблица / карточки) и телефона (сетка / список).
 */

type DesktopView = "table" | "cards";
type MobileView = "grid" | "list";

const VIEW_KEY = { desktop: "onebase.sales.assortment.view.desktop", mobile: "onebase.sales.assortment.view.mobile" } as const;

function readView<T extends string>(key: string, allowed: readonly T[]): T | null {
  try {
    const v = localStorage.getItem(key);
    return allowed.includes(v as T) ? (v as T) : null;
  } catch {
    return null;
  }
}

function saveView(key: string, value: string) {
  try {
    localStorage.setItem(key, value);
  } catch {
    // приватный режим — без запоминания
  }
}

/** Планшет (уже 1024): по умолчанию «Карточки» — таблица там только с прокруткой вбок. */
function useNarrow() {
  return useSyncExternalStore(
    (notify) => {
      const m = window.matchMedia("(max-width: 1023.98px)");
      m.addEventListener("change", notify);
      return () => m.removeEventListener("change", notify);
    },
    () => window.matchMedia("(max-width: 1023.98px)").matches,
    () => false,
  );
}

type ScreenProps = {
  data: CategoryView;
  months: SalesMonth[];
  /** «Все категории» в том же охвате. */
  allHref: string;
  /** Период и охват — для ссылок на артикулы. */
  query: string;
  /** Прошлый месяц с заглавной: «Октябрь». */
  prevLabel: string;
  /** «По регионам» (у республики и направления) — под артикулами. */
  children?: React.ReactNode;
};

/** Экран категории с фото товаров с сервера (photos; не пришли — без фото). */
export function CategoryScreen({ photos, ...props }: ScreenProps & { photos: ProductPhotos | null }) {
  return (
    <ProductPhotosProvider initial={photos}>
      <CategoryBody {...props} />
    </ProductPhotosProvider>
  );
}

function CategoryBody({
  data,
  months,
  allHref,
  query,
  prevLabel,
  children,
}: {
  data: CategoryView;
  months: SalesMonth[];
  /** «Все категории» в том же охвате. */
  allHref: string;
  /** Период и охват — для ссылок на артикулы. */
  query: string;
  /** Прошлый месяц с заглавной: «Октябрь». */
  prevLabel: string;
  /** «По регионам» (у республики и направления) — под артикулами. */
  children?: React.ReactNode;
}) {
  const t = useT();
  const card = data.card;
  const period = data.period;
  const name = data.categoryId === "none" ? t(data.name) : data.name;
  const scopeName = t(data.scopeName);
  const skus = useMemo(() => sortSkus(card?.skus ?? []), [card]);
  const [search, setSearch] = useState("");
  const visible = useMemo(() => skus.filter((r) => matchesSku(r, search)), [skus, search]);
  const [viewer, setViewer] = useState<ViewerItem | null>(null);
  const [monthsOpen, setMonthsOpen] = useState(false);
  const closeViewer = useCallback(() => setViewer(null), []);

  // Обложка категории — фото самого продаваемого артикула, у которого фото есть (своё раньше похожего).
  const all = useAllProductPhotos();
  const photosOf = (r: SkuRow) => all[String(r.productId)] ?? [];
  const coverSku = skus.find((r) => photosOf(r).length > 0 && !photosOf(r)[0].similar) ?? skus.find((r) => photosOf(r).length > 0);
  const cover = coverSku ? photosOf(coverSku) : [];
  const openCover = () =>
    coverSku && setViewer({ productId: coverSku.productId, photos: cover, title: skuName(coverSku), sub: coverSku.code ? t("Артикул {code}", { code: coverSku.code }) : null });

  const open = !period.closed;
  const monthText = monthLabel(period.year, period.month, t);
  const through = open ? t("по {date}", { date: dayMonth(period.dataThrough) }) : null;
  const day = Number(period.dataThrough.slice(8, 10));
  const span = day > 1 ? `1–${day} ${monthGenitive(period.month, t)}` : `1 ${monthGenitive(period.month, t)}`;

  return (
    <div data-palette="sales-secondary" data-sales-own-header className="min-h-full">
      <ReportPlans year={period.year} month={period.month} plans={period.availablePlans} />
      <MobileHeader
        allHref={allHref}
        title={t("Ассортимент")}
        sub={[monthText, through].filter(Boolean).join(" · ")}
        search={search}
        onSearch={setSearch}
        onMonths={() => setMonthsOpen(true)}
        canSearch={skus.length > 0}
      />

      <div className="mx-auto flex max-w-[1800px] flex-col gap-3.5 px-4 pb-10 pt-2 md:gap-5 md:px-6 lg:pt-5">
        {/* Компьютер: «Назад» и крошки. */}
        <div className="flex flex-wrap items-center gap-3 max-lg:hidden">
          <Link
            href={allHref}
            className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-line bg-surface px-2.5 text-[12px] font-semibold text-ink-2 outline-none hover:bg-muted focus-visible:ring-2 focus-visible:ring-accent"
          >
            <ArrowLeft aria-hidden className="size-3.5" />
            {t("Назад")}
          </Link>
          <nav aria-label={t("Навигация")} className="flex flex-wrap items-center gap-1.5 text-[12px] font-medium text-ink-4">
            {[
              ["OneBase", "/"],
              [t("Продажи"), "/sales"],
              [t("Ассортимент"), allHref],
              [t("Все категории"), allHref],
            ].map(([label, href], i) => (
              <span key={i} className="flex items-center gap-1.5">
                {i > 0 && <span aria-hidden>/</span>}
                <Link href={href} className="hover:text-ink">
                  {label}
                </Link>
              </span>
            ))}
            <span aria-hidden>/</span>
            <span className="text-ink-3">{name}</span>
          </nav>
        </div>

        {/* Шапка категории. */}
        <div className="flex items-center gap-3.5 md:gap-5">
          <button
            type="button"
            onClick={openCover}
            disabled={!coverSku}
            aria-label={coverSku ? t("Фото: {name}", { name: skuName(coverSku) }) : t("Нет фото")}
            className="size-[72px] shrink-0 cursor-zoom-in overflow-hidden rounded-[18px] border border-line-strong outline-none focus-visible:ring-2 focus-visible:ring-accent disabled:cursor-default md:size-24 md:rounded-[20px]"
          >
            {coverSku ? <ProductImage photo={cover[0]} alt="" sizes="96px" priority pad="p-1.5" /> : <CoverEmpty />}
          </button>
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
              <h1 className="text-[24px] font-bold leading-tight tracking-[-0.4px] text-ink md:text-[30px]">{name}</h1>
              {through && (
                <span className="rounded-lg bg-muted px-2 py-[3px] text-[11px] font-semibold text-ink-3 tabular-nums max-md:hidden">
                  {t("данные по {date}", { date: dayMonth(period.dataThrough) })}
                </span>
              )}
            </div>
            <p className="mt-1 text-[12px] font-medium text-ink-3 md:text-[14px]">
              {t("Категория")} · {scopeName}
              {card ? ` · ${t("{n} SKU", { n: num(card.skuTotal) })}` : ""}
            </p>
          </div>
        </div>

        {card ? (
          <>
            <div className="grid grid-cols-2 gap-2 md:gap-3 lg:grid-cols-4">
              <Kpi icon={Weight} label={["Факт, кг", "Факт"]} value={kg(card.factKg)} unit="кг" mobileUnit>
                {card.prevMonthKg ? (
                  <>
                    <span className="max-md:hidden">{t("прошлый месяц целиком: {kg} кг", { kg: kg(card.prevMonthKg) })}</span>
                    <span className="md:hidden">{t("прошлый месяц: {kg}", { kg: kg(card.prevMonthKg) })}</span>
                  </>
                ) : (
                  t("в прошлом месяце продаж не было")
                )}
              </Kpi>
              <Kpi icon={Banknote} label={["Выручка", "Выручка"]} value={money(card.revenue, t)} unit="сум">
                <span className="max-md:hidden">{open ? t("за {span}", { span }) : t("за {month}", { month: monthName(period.month, t) })}</span>
                <span className="md:hidden">{open ? t("сум, {span}", { span }) : t("сум, {month}", { month: monthName(period.month, t) })}</span>
              </Kpi>
              <Kpi icon={Boxes} label={["Продаётся SKU", "SKU"]} value={num(card.skuSold)} unit={`/ ${num(card.skuTotal)}`} mobileUnit>
                {card.lost > 0 ? (
                  <span className="font-semibold text-bad">
                    <span className="max-md:hidden">{t(plural(card.lost, ["пропал {n} SKU", "пропало {n} SKU", "пропало {n} SKU"]), { n: num(card.lost) })}</span>
                    <span className="md:hidden">{t(plural(card.lost, ["пропал {n}", "пропало {n}", "пропало {n}"]), { n: num(card.lost) })}</span>
                  </span>
                ) : card.silent > 0 ? (
                  t("молчат {n}", { n: num(card.silent) })
                ) : (
                  t("все в продаже")
                )}
              </Kpi>
              <Kpi icon={Store} label={["Торговые точки", "ТТ"]} value={num(card.akb)} unit="ТТ">
                {t("дистрибуция {pct}", { pct: pct(card.distribution) })}
              </Kpi>
            </div>

            <Skus
              all={skus}
              rows={visible}
              search={search}
              onResetSearch={() => setSearch("")}
              query={query}
              prevLabel={prevLabel}
              mono={data.mono}
              onPhoto={setViewer}
              onMonths={() => setMonthsOpen(true)}
              monthWord={monthName(period.month, t)}
            />
          </>
        ) : (
          <Empty
            text={t("За {month} продаж по категории нет", { month: monthName(period.month, t) })}
            hint={t("В охвате «{scope}» продаж категории «{name}» ни в этом, ни в прошлом месяце нет.", { scope: scopeName, name })}
            onMonths={() => setMonthsOpen(true)}
          />
        )}

        {children}
      </div>

      <MonthSheet open={monthsOpen} onClose={() => setMonthsOpen(false)} months={months} year={period.year} month={period.month} />
      {viewer && <PhotoViewer item={viewer} onClose={closeViewer} />}
    </div>
  );
}

function CoverEmpty() {
  return (
    <span className="grid size-full place-items-center bg-muted text-ink-4">
      <LayoutGrid aria-hidden className="size-7" strokeWidth={1.6} />
    </span>
  );
}

/** KPI: на компьютере — иконка в квадрате, подпись, значение 22/700 с единицей; на телефоне — без иконки, 18/700, короткие подписи. */
function Kpi({
  icon: Icon,
  label,
  value,
  unit,
  mobileUnit = false,
  children,
}: {
  icon: LucideIcon;
  /** Подпись на компьютере и на телефоне. */
  label: [string, string];
  value: string;
  unit: string;
  /** Единица и на телефоне (кг, «/ 10»); иначе — только на компьютере. */
  mobileUnit?: boolean;
  children: React.ReactNode;
}) {
  const t = useT();
  return (
    <div className="flex min-w-0 items-center gap-3.5 rounded-2xl border border-line bg-surface px-3.5 py-3 md:px-[18px] md:py-4">
      <span aria-hidden className="grid size-10 shrink-0 place-items-center rounded-xl bg-muted text-ink-2 max-md:hidden">
        <Icon className="size-[18px]" />
      </span>
      <div className="min-w-0">
        <div className="text-[11px] font-medium text-ink-3 md:text-[12px]">
          <span className="max-md:hidden">{t(label[0])}</span>
          <span className="md:hidden">{t(label[1])}</span>
        </div>
        <div className="mt-[3px] flex items-baseline gap-1 whitespace-nowrap">
          <span className="text-[18px] font-bold leading-tight tracking-tight text-ink tabular-nums md:text-[22px]">{value}</span>
          <span className={`text-[12px] font-medium text-ink-3 md:text-[12px] ${mobileUnit ? "max-md:text-[18px] max-md:font-bold max-md:text-ink" : "max-md:hidden"}`}>
            {t(unit)}
          </span>
        </div>
        <div className="mt-0.5 text-[11px] font-medium leading-snug text-ink-4">{children}</div>
      </div>
    </div>
  );
}

/** Переключатель вида (radiogroup): на компьютере — подписи с иконками, на телефоне — иконки 30×26. */
function ViewSwitch<V extends string>({
  value,
  options,
  onChange,
  label,
  compact = false,
}: {
  value: V;
  options: { key: V; label: string; icon: LucideIcon }[];
  onChange: (v: V) => void;
  label: string;
  compact?: boolean;
}) {
  const t = useT();
  return (
    <div role="radiogroup" aria-label={t(label)} className={`flex shrink-0 rounded-[10px] border border-line p-[3px] ${compact ? "gap-0.5 bg-surface" : "bg-page"}`}>
      {options.map((o) => {
        const on = o.key === value;
        const Icon = o.icon;
        return (
          <button
            key={o.key}
            type="button"
            role="radio"
            aria-checked={on}
            aria-label={compact ? t(o.label) : undefined}
            onClick={() => onChange(o.key)}
            onKeyDown={(e) => {
              if (e.key !== "ArrowRight" && e.key !== "ArrowLeft") return;
              e.preventDefault();
              const i = options.findIndex((x) => x.key === value);
              const next = options[(i + (e.key === "ArrowRight" ? 1 : options.length - 1)) % options.length];
              onChange(next.key);
              (e.currentTarget.parentElement?.querySelector(`[aria-checked="true"]`) as HTMLElement | null)?.focus();
            }}
            tabIndex={on ? 0 : -1}
            className={`flex items-center justify-center rounded-[7px] outline-none focus-visible:ring-2 focus-visible:ring-accent ${
              compact ? "h-[26px] w-[30px]" : "h-[30px] gap-1.5 px-3 text-[12px]"
            } ${on ? "bg-muted font-semibold text-ink" : "font-medium text-ink-3 hover:text-ink"}`}
          >
            <Icon aria-hidden className={compact ? "size-[15px]" : "size-3.5"} />
            {!compact && t(o.label)}
          </button>
        );
      })}
    </div>
  );
}

function Skus({
  all,
  rows,
  search,
  onResetSearch,
  query,
  prevLabel,
  mono,
  onPhoto,
  onMonths,
  monthWord,
}: {
  all: SkuRow[];
  rows: SkuRow[];
  search: string;
  onResetSearch: () => void;
  query: string;
  prevLabel: string;
  mono: number;
  onPhoto: (item: ViewerItem) => void;
  onMonths: () => void;
  monthWord: string;
}) {
  const t = useT();
  const narrow = useNarrow();
  const [desktop, setDesktop] = useState<DesktopView | null>(null);
  const [mobile, setMobile] = useState<MobileView>("grid");
  useEffect(() => {
    setDesktop(readView(VIEW_KEY.desktop, ["table", "cards"] as const));
    setMobile(readView(VIEW_KEY.mobile, ["grid", "list"] as const) ?? "grid");
  }, []);
  const desktopView: DesktopView = desktop ?? (narrow ? "cards" : "table");

  const empty =
    all.length === 0 ? (
      <Empty text={t("За {month} продаж по категории нет", { month: monthWord })} onMonths={onMonths} />
    ) : rows.length === 0 ? (
      <Empty text={t("Ничего не найдено")} hint={t("По «{q}» артикулов нет.", { q: search.trim() })} onReset={onResetSearch} />
    ) : null;

  return (
    <>
      {/* Компьютер и планшет. */}
      <section className="overflow-hidden rounded-2xl border border-line bg-surface max-md:hidden" aria-labelledby="skus-title">
        <div className="flex flex-wrap items-start justify-between gap-3 px-[18px] py-3.5">
          <div className="min-w-0">
            <h2 id="skus-title" className="flex items-baseline gap-2 text-[16px] font-bold text-ink">
              {t("Артикулы")}
              <span className="text-[12px] font-semibold text-ink-4 tabular-nums">{rows.length}</span>
            </h2>
            <p className="mt-0.5 text-[12px] font-medium text-ink-3">{t("Нажмите на товар — увидите, где он продаётся, а где нет")}</p>
          </div>
          <div className="flex items-center gap-2">
            <ViewSwitch
              label="Вид артикулов"
              value={desktopView}
              onChange={(v) => {
                setDesktop(v);
                saveView(VIEW_KEY.desktop, v);
              }}
              options={[
                { key: "table", label: "Таблица", icon: Table2 },
                { key: "cards", label: "Карточки", icon: LayoutGrid },
              ]}
            />
            <CopySkus rows={rows} prevLabel={prevLabel} />
          </div>
        </div>
        {empty ? (
          <div className="border-t border-line p-[18px]">{empty}</div>
        ) : desktopView === "table" ? (
          <SkuTable rows={rows} query={query} prevLabel={prevLabel} onPhoto={onPhoto} />
        ) : (
          <div className="grid grid-cols-[repeat(auto-fill,minmax(220px,1fr))] gap-3 border-t border-line p-[18px]">
            {rows.map((r, i) => (
              <SkuCard key={r.productId} r={r} query={query} priority={i < 4} sizes="(max-width: 1279px) 30vw, 240px" onPhoto={onPhoto} />
            ))}
          </div>
        )}
      </section>

      {/* Телефон. */}
      <section className="flex flex-col gap-3.5 md:hidden" aria-labelledby="skus-title-m">
        <div className="flex items-center justify-between px-1 pt-1">
          <h2 id="skus-title-m" className="flex items-baseline gap-2 text-[17px] font-bold text-ink">
            {t("Артикулы")}
            <span className="text-[12px] font-semibold text-ink-4 tabular-nums">{rows.length}</span>
          </h2>
          <ViewSwitch
            compact
            label="Вид артикулов"
            value={mobile}
            onChange={(v) => {
              setMobile(v);
              saveView(VIEW_KEY.mobile, v);
            }}
            options={[
              { key: "grid", label: "Сетка с фото", icon: LayoutGrid },
              { key: "list", label: "Список", icon: List },
            ]}
          />
        </div>
        {empty ??
          (mobile === "grid" ? (
            <div className="grid grid-cols-2 gap-2.5">
              {rows.map((r, i) => (
                <SkuCard key={r.productId} r={r} query={query} priority={i < 4} sizes="(max-width: 767px) 50vw, 220px" onPhoto={onPhoto} />
              ))}
            </div>
          ) : (
            <div className="flex flex-col gap-2">
              {rows.map((r) => (
                <SkuListRow key={r.productId} r={r} query={query} onPhoto={onPhoto} />
              ))}
            </div>
          ))}
      </section>

      <p className="border-l-2 border-line pl-3 text-xs leading-relaxed text-ink-3 max-md:hidden">
        {t(
          "«Продаётся» — есть точка с положительной строкой артикула (кг или сумма больше нуля). «Пропал» — в прошлом месяце здесь продавался, в этом пока ни одной продажи. «Не возят» — здесь ноль, а по республике товар идёт. «Нет продаж» — есть в ассортименте (продавался с начала года), но в этом месяце не продаётся нигде. ТТ — точки с положительной строкой артикула; дистрибуция — их доля от ТТ с покупкой. «Только он» — точки, которые из всех SKU купили только этот, и их доля от ТТ артикула{mono}. «ТОП» — товар из списка ТОП. «{prev}» — прошлый месяц целиком: текущий ещё не закончен, килограммы в лоб не сравнивать.",
          { mono: t("; всего таких точек {n}", { n: num(mono) }), prev: t(prevLabel) },
        )}
      </p>
    </>
  );
}

function Empty({ text, hint, onMonths, onReset }: { text: string; hint?: string; onMonths?: () => void; onReset?: () => void }) {
  const t = useT();
  return (
    <div className="flex flex-col items-center gap-2 rounded-2xl border border-line bg-surface px-6 py-10 text-center md:border-0 md:bg-transparent md:py-8">
      <span aria-hidden className="grid size-12 place-items-center rounded-[14px] bg-muted text-ink-4">
        {onReset ? <Search className="size-5" /> : <Package className="size-6" strokeWidth={1.6} />}
      </span>
      <p className="mt-1 text-[15px] font-semibold text-ink">{text}</p>
      {hint && <p className="max-w-md text-[13px] text-ink-3">{hint}</p>}
      {(onMonths || onReset) && (
        <button
          type="button"
          onClick={onReset ?? onMonths}
          className="mt-2 h-9 rounded-[10px] border border-line-strong bg-surface px-3.5 text-[13px] font-semibold text-ink outline-none hover:bg-muted focus-visible:ring-2 focus-visible:ring-accent"
        >
          {t(onReset ? "Сбросить поиск" : "Другой месяц")}
        </button>
      )}
    </div>
  );
}

/** Своя шапка телефона и планшета: закреплена сверху, после 8 px прокрутки — фон с размытием и линия снизу. */
function MobileHeader({
  allHref,
  title,
  sub,
  search,
  onSearch,
  onMonths,
  canSearch,
}: {
  allHref: string;
  title: string;
  sub: string;
  search: string;
  onSearch: (q: string) => void;
  onMonths: () => void;
  canSearch: boolean;
}) {
  const t = useT();
  const [searching, setSearching] = useState(false);
  const [scrolled, setScrolled] = useState(false);
  const header = useRef<HTMLElement>(null);
  useEffect(() => {
    const scroller = header.current?.closest("[data-app-scroll]") ?? null;
    if (!scroller) return;
    const update = () => setScrolled(scroller.scrollTop > 8);
    update();
    scroller.addEventListener("scroll", update, { passive: true });
    return () => scroller.removeEventListener("scroll", update);
  }, []);
  const round = "grid size-10 shrink-0 place-items-center rounded-full bg-surface text-ink-2 outline-none focus-visible:ring-2 focus-visible:ring-accent";

  return (
    <header
      ref={header}
      className={`sticky top-0 z-20 border-b transition-colors lg:hidden ${scrolled ? "border-line bg-page/80 backdrop-blur-xl" : "border-transparent bg-transparent"}`}
    >
      <div className="flex h-14 items-center gap-2.5 px-3">
        {searching ? (
          <>
            <label className="relative flex h-10 min-w-0 flex-1 items-center">
              <span className="sr-only">{t("Поиск по названию и артикулу")}</span>
              <Search aria-hidden className="pointer-events-none absolute left-3.5 size-4 text-ink-4" />
              <input
                autoFocus
                type="search"
                value={search}
                onChange={(e) => onSearch(e.target.value)}
                placeholder={t("Название или артикул")}
                className="h-full w-full rounded-full border border-line bg-surface pl-10 pr-4 text-[14px] text-ink outline-none placeholder:text-ink-4 focus:border-accent"
              />
            </label>
            <button
              type="button"
              aria-label={t("Закрыть поиск")}
              onClick={() => {
                setSearching(false);
                onSearch("");
              }}
              className={round}
            >
              <X aria-hidden className="size-[18px]" />
            </button>
          </>
        ) : (
          <>
            <Link href={allHref} aria-label={t("Назад")} className={round}>
              <ChevronLeft aria-hidden className="size-[18px]" />
            </Link>
            <button type="button" onClick={onMonths} aria-label={t("Выбрать месяц: {month}", { month: sub })} className="min-w-0 flex-1 rounded-lg text-center outline-none focus-visible:ring-2 focus-visible:ring-accent">
              <span className="block truncate text-[15px] font-bold leading-tight text-ink">{title}</span>
              <span className="block truncate text-[11px] font-medium text-ink-3">{sub}</span>
            </button>
            <button type="button" aria-label={t("Поиск")} onClick={() => setSearching(true)} disabled={!canSearch} className={`${round} disabled:opacity-50`}>
              <Search aria-hidden className="size-[18px]" />
            </button>
          </>
        )}
      </div>
    </header>
  );
}

/** Выбор месяца: шторка на телефоне, окно на компьютере. Меняет адрес — сервер пересчитывает страницу (как переключатель в полосе раздела). */
function MonthSheet({ open, onClose, months, year, month }: { open: boolean; onClose: () => void; months: SalesMonth[]; year: number; month: number }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const options = months.some((m) => m.year === year && m.month === month) ? months : [{ year, month }, ...months];
  const choose = (y: number, m: number) => {
    onClose();
    if (y === year && m === month) return;
    const next = new URLSearchParams(params);
    next.set("year", String(y));
    next.set("month", String(m));
    for (const key of ["from", "to", "day", "dealer"]) next.delete(key);
    if (planOf(next.get("plan")) !== "factory") next.delete("plan");
    router.push(`${pathname}?${next}`);
  };
  return (
    <Sheet open={open} onClose={onClose} title={t("Месяц")}>
      <div role="radiogroup" aria-label={t("Месяц")} className="flex flex-col pb-2">
        {options.map((m) => {
          const on = m.year === year && m.month === month;
          return (
            <button
              key={`${m.year}-${m.month}`}
              type="button"
              role="radio"
              aria-checked={on}
              onClick={() => choose(m.year, m.month)}
              className={`flex items-center justify-between rounded-xl px-3 py-3 text-left text-[14px] ${on ? "bg-accent-soft font-semibold text-accent-strong" : "text-ink hover:bg-muted"}`}
            >
              {monthLabel(m.year, m.month, t)}
              {on && <span aria-hidden>✓</span>}
            </button>
          );
        })}
      </div>
    </Sheet>
  );
}

/** Заготовка экрана категории на время загрузки: шапка, обложка 72, 4 KPI, 4 карточки (телефон) или 6 строк (компьютер). */
export function CategorySkeleton() {
  const t = useT();
  const bar = "block animate-pulse rounded-md bg-muted";
  return (
    <div data-palette="sales-secondary" data-sales-own-header className="min-h-full" aria-busy aria-label={t("Загрузка…")}>
      <div className="flex h-14 items-center gap-2.5 px-3 lg:hidden">
        <span className={`${bar} size-10 rounded-full`} />
        <span className="flex flex-1 flex-col items-center gap-1.5">
          <span className="text-[15px] font-bold text-ink">{t("Ассортимент")}</span>
          <span className={`${bar} h-2.5 w-28`} />
        </span>
        <span className={`${bar} size-10 rounded-full`} />
      </div>
      <div className="mx-auto flex max-w-[1800px] flex-col gap-3.5 px-4 pb-10 pt-2 md:gap-5 md:px-6 lg:pt-5">
        <span className={`${bar} h-8 w-80 max-lg:hidden`} />
        <div className="flex items-center gap-3.5 md:gap-5">
          <span className={`${bar} size-[72px] rounded-[18px] md:size-24 md:rounded-[20px]`} />
          <span className="flex flex-col gap-2">
            <span className={`${bar} h-7 w-40 md:h-8 md:w-56`} />
            <span className={`${bar} h-3 w-48`} />
          </span>
        </div>
        <div className="grid grid-cols-2 gap-2 md:gap-3 lg:grid-cols-4">
          {Array.from({ length: 4 }, (_, i) => (
            <span key={i} className={`${bar} h-[76px] rounded-2xl md:h-[86px]`} />
          ))}
        </div>
        <div className="overflow-hidden rounded-2xl border border-line bg-surface max-md:hidden">
          <span className="block h-[74px]" />
          {Array.from({ length: 6 }, (_, i) => (
            <div key={i} className="flex items-center gap-4 border-t border-line px-[18px] py-2.5">
              <span className={`${bar} size-14 rounded-xl`} />
              <span className="flex flex-1 flex-col gap-1.5">
                <span className={`${bar} h-3.5 w-72`} />
                <span className={`${bar} h-2.5 w-24`} />
              </span>
              <span className={`${bar} h-5 w-20 rounded-[7px]`} />
              <span className={`${bar} h-3 w-16`} />
              <span className={`${bar} h-3 w-32`} />
              <span className={`${bar} h-3 w-24`} />
            </div>
          ))}
        </div>
        <div className="grid grid-cols-2 gap-2.5 md:hidden">
          {Array.from({ length: 4 }, (_, i) => (
            <div key={i} className="overflow-hidden rounded-[18px] border border-line bg-surface">
              <span className="block aspect-[174/150] animate-pulse bg-muted" />
              <span className="flex flex-col gap-2 p-3">
                <span className={`${bar} h-3 w-full`} />
                <span className={`${bar} h-3 w-2/3`} />
                <span className={`${bar} h-4 w-1/2`} />
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

