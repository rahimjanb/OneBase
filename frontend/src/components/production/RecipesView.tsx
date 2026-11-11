"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState, useTransition } from "react";
import {
  ArrowDownUp,
  Archive,
  BadgeCheck,
  CalendarCheck,
  Check,
  ChevronLeft,
  FilePen,
  FileSpreadsheet,
  FlaskConical,
  Hourglass,
  ImagePlus,
  LayoutGrid,
  Layers,
  List,
  Package,
  Plus,
  Search,
  SearchX,
  Upload,
  type LucideIcon,
} from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import { recipeStatusLabel, type RecipeList, type RecipeRow, type RecipeStatus } from "@/lib/production";
import {
  RECIPE_FILTERS,
  RECIPE_SORTS,
  SORT_KEY,
  VIEW_KEY,
  effectiveDate,
  filterCounts,
  normBase,
  parseFilter,
  parseSort,
  parseView,
  recipeTone,
  sortRecipes,
  type RecipeFilter,
  type RecipeSort,
  type RecipeTone,
  type RecipeView,
} from "@/lib/recipe-cards";
import { PhotoImage, PhotoProgress, usePhotoUpload, usePicker } from "./RecipePhoto";

type T = ReturnType<typeof useT>;

const statusIcon: Record<RecipeStatus, LucideIcon> = { Active: BadgeCheck, Draft: FilePen, PendingApproval: Hourglass, Archived: Archive };
const filterIcon: Record<RecipeFilter, LucideIcon> = { "": Layers, active: BadgeCheck, draft: FilePen, pending: Hourglass, archived: Archive };
const filterTone: Record<RecipeFilter, RecipeTone | null> = { "": null, active: "ok", draft: "warn", pending: "link", archived: "subtle" };

/** Цвет текста и мягкий фон значка состояния (токены палитры страницы). */
const toneText: Record<RecipeTone, string> = { ok: "text-ok", warn: "text-warn", link: "text-accent-strong", subtle: "text-ink-4" };
const toneSoft: Record<RecipeTone, string> = { ok: "bg-ok-soft", warn: "bg-warn-soft", link: "bg-accent-soft", subtle: "bg-muted" };

/**
 * Значки поверх фото: подложка одна в обеих темах (#0C1221 85 % + размытие), поэтому и цвета текста — светлые значения палитры, иначе в
 * светлой теме тёмный текст лёг бы на тёмную подложку.
 */
const onPhoto = "bg-[#0C1221D9] backdrop-blur-[8px]";
const onPhotoText: Record<RecipeTone, string> = { ok: "text-[#2BD47D]", warn: "text-[#F6A93B]", link: "text-[#7FA6FF]", subtle: "text-[#8D97AE]" };

const scrollRow = "overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden";

/** «Новая версия: черновик» / «на утверждении» — если у кода есть неутверждённая версия, а показана другая. */
const newVersion = (r: RecipeRow, t: T) =>
  r.draftId && r.draftId !== r.id && r.draftStatus ? t("Новая версия: {status}", { status: t(recipeStatusLabel[r.draftStatus]).toLowerCase() }) : null;

/** Значения браузера (вид, сортировка): localStorage бывает недоступен (приватное окно) — тогда просто не запоминаем. */
function useStored<V extends string>(key: string, parse: (v: string | null) => V): [V, (v: V) => void] {
  const [value, setValue] = useState<V>(() => parse(null));
  useEffect(() => {
    try {
      setValue(parse(window.localStorage.getItem(key)));
    } catch {
      // нет доступа к хранилищу
    }
  }, [key, parse]);
  const set = (v: V) => {
    setValue(v);
    try {
      window.localStorage.setItem(key, v);
    } catch {
      // нет доступа к хранилищу
    }
  };
  return [value, set];
}

/**
 * «Производство → Технолог → Техкарты» (макеты sTGwX и I0Ht2): карточки продуктов по коду — утверждённая версия (а нет — последняя), с фото
 * продукта, состоянием, версией, кодом, базой норм, числом этапов и компонентов, датой начала действия. Поиск (?q=) по коду, названию,
 * выходу и номеру карты; состояние (?status=) — кнопки со счётчиками; вид «Сетка / Список» и сортировка — в браузере. Компьютер — сетка
 * (3 колонки, на широком — 4; узко — 2) или список; телефон — список с квадратным фото слева. Без фото — заглушка, загрузить можно прямо
 * отсюда (кто ведёт техкарты). data = null — скелетоны (loading.tsx).
 */
export function RecipesView({ data }: { data: RecipeList | null }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, start] = useTransition();
  const [search, setSearch] = useState(params.get("q") ?? "");
  const [chosen, setChosen] = useState<RecipeFilter>(parseFilter(params.get("status")));
  const [view, setView] = useStored<RecipeView>(VIEW_KEY, parseView);
  const [sort, setSort] = useStored<RecipeSort>(SORT_KEY, parseSort);
  const [sortOpen, setSortOpen] = useState(false);
  const q = params.get("q") ?? "";

  // Адрес изменился снаружи (назад/вперёд) — подтягиваем поле и фильтр.
  useEffect(() => setChosen(parseFilter(params.get("status"))), [params]);

  const go = (patch: Record<string, string | null>, replace = false) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    const href = `${pathname}${next.size ? `?${next}` : ""}`;
    start(() => (replace ? router.replace(href, { scroll: false }) : router.push(href, { scroll: false })));
  };
  useEffect(() => {
    const timer = window.setTimeout(() => {
      if (q !== search.trim()) go({ q: search.trim() || null }, true);
    }, 300);
    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const choose = (f: RecipeFilter) => {
    setChosen(f);
    go({ status: f || null });
  };
  const reset = () => {
    setSearch("");
    setChosen("");
    go({ q: null, status: null });
  };

  const counts = data ? filterCounts(data.counts, data.items) : null;
  const rows = data ? sortRecipes(data.items, sort) : [];
  const canEdit = !!data?.canCreate;
  const nothingYet = !!data && (counts?.all ?? 0) === 0 && !q && !chosen;
  const dim = pending ? "opacity-60 transition-opacity" : "";
  const sortLabel = RECIPE_SORTS.find((s) => s.key === sort)?.label ?? "";

  const importLabel = t("Импортировать технологические карты из Excel");

  return (
    <div data-palette="sales-secondary" data-glow="both" className="min-h-full">
      {/* ---------- Телефон и планшет: своя шапка вместо общей ---------- */}
      <header className="flex h-14 items-center gap-2 px-3 lg:hidden">
        <Link href="/production/technologist" aria-label={t("Назад")} className="grid size-10 shrink-0 place-items-center rounded-full bg-surface text-ink">
          <ChevronLeft aria-hidden className="size-5" />
        </Link>
        <div className="min-w-0 flex-1 text-center">
          <h1 className="truncate text-[15px] font-bold text-ink">{t("Техкарты")}</h1>
          <p className="truncate text-[11px] font-medium text-ink-3">{t("Производство · Технолог")}</p>
        </div>
        {canEdit ? (
          <>
            <Link href="/production/recipes/import" aria-label={t("Импорт из Excel")} title={importLabel}
              className="grid size-10 shrink-0 place-items-center rounded-full border border-line-strong bg-surface">
              <FileSpreadsheet aria-hidden className="size-[18px] text-ok" />
            </Link>
            <Link href="/production/recipes/new" aria-label={t("Новая техкарта")} className="grid size-10 shrink-0 place-items-center rounded-full bg-accent text-white">
              <Plus aria-hidden className="size-5" />
            </Link>
          </>
        ) : (
          <span className="size-10 shrink-0" aria-hidden />
        )}
      </header>

      {/* ---------- Компьютер: крошки, название, описание, кнопки ---------- */}
      <header className="border-b border-line max-lg:hidden">
        <div className="mx-auto flex max-w-[1800px] items-end justify-between gap-6 px-6 pb-5 pt-[18px]">
          <div className="min-w-0">
            <nav aria-label={t("Навигация")} className="flex flex-wrap gap-1.5 text-[12px] font-medium text-ink-4">
              {[["OneBase", "/"], ["Отделы", "/departments"], ["Производство", "/production"], ["Технолог", "/production/technologist"]].map(([label, href]) => (
                <span key={href} className="flex gap-1.5">
                  <Link href={href} className="hover:text-ink">{t(label)}</Link>
                  <span aria-hidden>/</span>
                </span>
              ))}
              <span className="text-ink-3">{t("Техкарты")}</span>
            </nav>
            <h1 className="mt-2 text-[28px] font-bold leading-tight tracking-[-0.4px] text-ink">{t("Техкарты")}</h1>
            <p className="mt-2 text-[14px] font-medium text-ink-3">
              {t("Технологические карты продуктов: этапы, рецептура замеса, нормы, брак и упаковка — по версиям с утверждением")}
            </p>
          </div>
          {canEdit && (
            <div className="flex shrink-0 gap-2.5">
              <Link href="/production/recipes/import" title={importLabel} aria-label={importLabel}
                className="inline-flex h-[42px] items-center gap-2 rounded-[10px] border border-line-strong bg-surface px-4 text-[13px] font-semibold text-ink hover:bg-muted max-xl:px-3">
                <FileSpreadsheet aria-hidden className="size-[17px] text-ok" />
                <span className="max-xl:hidden">{t("Импорт из Excel")}</span>
              </Link>
              <Link href="/production/recipes/new"
                className="inline-flex h-[42px] items-center gap-2 rounded-[10px] bg-accent px-4 text-[13px] font-semibold text-white shadow-[0_4px_14px_#3D7BFF55] hover:brightness-110">
                <Plus aria-hidden className="size-[17px]" /> {t("Новая техкарта")}
              </Link>
            </div>
          )}
        </div>
      </header>

      {nothingYet ? (
        <EmptyAll canEdit={canEdit} />
      ) : (
        <>
          {/* ---------- Телефон (уже 768) ---------- */}
          <div className="flex flex-col gap-3 px-4 pb-6 pt-1 md:hidden">
            <SearchField value={search} onChange={setSearch} placeholder={t("Код, название или номер карты")} mobile />
            <div className={`-mx-4 px-4 ${scrollRow}`}>
              <Filters value={chosen} counts={counts} onChange={choose} mobile />
            </div>
            <div className="flex items-center justify-between gap-3 pt-1">
              <h2 className="text-[17px] font-bold text-ink">
                {data ? `${rows.length} ${t(plural(rows.length, ["техкарта", "техкарты", "техкарт"]))}` : <span className="inline-block h-5 w-24 animate-pulse rounded-md bg-muted align-middle" />}
              </h2>
              <button type="button" onClick={() => setSortOpen(true)} className="flex items-center gap-1.5 text-[12px] font-medium text-ink-3" aria-haspopup="dialog">
                <ArrowDownUp aria-hidden className="size-[13px]" /> {t(sortLabel)}
              </button>
            </div>
            <div aria-busy={pending || !data} className={dim}>
              {!data ? (
                <ul className="flex flex-col gap-2.5">{[0, 1, 2, 3].map((i) => <RowSkeleton key={i} />)}</ul>
              ) : rows.length === 0 ? (
                <NothingFound onReset={reset} />
              ) : (
                <ul className="flex flex-col gap-2.5">
                  {rows.map((r) => <RecipeRowCard key={r.code} r={r} canEdit={canEdit} />)}
                </ul>
              )}
            </div>
            <Sheet open={sortOpen} onClose={() => setSortOpen(false)} title={t("Сортировка")}>
              <ul role="radiogroup" aria-label={t("Сортировка")} className="-mx-1 pb-2">
                {RECIPE_SORTS.map((s) => (
                  <li key={s.key}>
                    <button type="button" role="radio" aria-checked={sort === s.key}
                      onClick={() => { setSort(s.key); setSortOpen(false); }}
                      className="flex w-full items-center justify-between rounded-xl px-3 py-3 text-left text-[14px] font-medium text-ink active:bg-muted">
                      {t(s.label)}
                      {sort === s.key && <Check aria-hidden className="size-4 text-accent-strong" />}
                    </button>
                  </li>
                ))}
              </ul>
            </Sheet>
          </div>

          {/* ---------- Компьютер и планшет ---------- */}
          <div className="mx-auto flex max-w-[1800px] flex-col gap-[18px] px-6 pb-10 pt-5 @container max-md:hidden max-lg:px-4">
            <div className="flex flex-wrap items-center gap-2.5">
              <div className="order-1 min-w-0 flex-1">
                <SearchField value={search} onChange={setSearch} placeholder={t("Код, название, выход или номер карты")} />
              </div>
              <div className={`order-3 w-full ${scrollRow} @min-[1080px]:order-2 @min-[1080px]:w-auto`}>
                <Filters value={chosen} counts={counts} onChange={choose} />
              </div>
              <div role="group" aria-label={t("Вид списка")} className="order-2 flex shrink-0 gap-1 rounded-xl border border-line bg-surface p-1 @min-[1080px]:order-3">
                {([["grid", LayoutGrid, "Сетка"], ["list", List, "Список"]] as const).map(([key, Icon, label]) => (
                  <button key={key} type="button" onClick={() => setView(key)} aria-pressed={view === key} aria-label={t(label)} title={t(label)}
                    className={`grid size-[34px] place-items-center rounded-lg border ${view === key ? "border-line-strong bg-muted text-ink" : "border-transparent text-ink-3 hover:text-ink"}`}>
                    <Icon aria-hidden className="size-[17px]" />
                  </button>
                ))}
              </div>
            </div>

            <div aria-busy={pending || !data} className={dim}>
              {!data ? (
                <ul className="grid grid-cols-2 gap-4 @min-[960px]:grid-cols-3 @min-[1600px]:grid-cols-4">{[0, 1, 2, 3, 4, 5].map((i) => <CardSkeleton key={i} />)}</ul>
              ) : rows.length === 0 ? (
                <NothingFound onReset={reset} />
              ) : view === "list" ? (
                <ul className="flex flex-col gap-2.5">
                  {rows.map((r) => <RecipeRowCard key={r.code} r={r} canEdit={canEdit} wide />)}
                </ul>
              ) : (
                <ul className="grid grid-cols-2 gap-4 @min-[960px]:grid-cols-3 @min-[1600px]:grid-cols-4">
                  {rows.map((r) => <RecipeGridCard key={r.code} r={r} canEdit={canEdit} />)}
                </ul>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}

function SearchField({ value, onChange, placeholder, mobile = false }: { value: string; onChange: (v: string) => void; placeholder: string; mobile?: boolean }) {
  const t = useT();
  return (
    <label className="relative block">
      <Search aria-hidden className="pointer-events-none absolute left-3.5 top-1/2 size-4 -translate-y-1/2 text-ink-4" />
      <input
        type="search"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        aria-label={t("Поиск техкарты")}
        className={`w-full border border-line bg-surface pl-10 pr-3 text-ink placeholder:text-ink-4 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20 ${
          mobile ? "h-11 rounded-xl text-[14px]" : "h-[42px] rounded-[10px] text-[13px]"
        }`}
      />
    </label>
  );
}

/** Кнопки состояния со счётчиками — radiogroup: стрелки переключают, Tab — к выбранной. */
function Filters({ value, counts, onChange, mobile = false }: { value: RecipeFilter; counts: ReturnType<typeof filterCounts> | null; onChange: (f: RecipeFilter) => void; mobile?: boolean }) {
  const t = useT();
  const refs = useRef<(HTMLButtonElement | null)[]>([]);
  const keys = (e: React.KeyboardEvent, i: number) => {
    const step = e.key === "ArrowRight" || e.key === "ArrowDown" ? 1 : e.key === "ArrowLeft" || e.key === "ArrowUp" ? -1 : 0;
    if (!step) return;
    e.preventDefault();
    const next = (i + step + RECIPE_FILTERS.length) % RECIPE_FILTERS.length;
    refs.current[next]?.focus();
    onChange(RECIPE_FILTERS[next].key);
  };
  return (
    <div role="radiogroup" aria-label={t("Статус техкарты")}
      className={mobile ? "flex w-max gap-2" : "flex w-max gap-1 rounded-xl border border-line bg-surface p-1"}>
      {RECIPE_FILTERS.map((f, i) => {
        const Icon = filterIcon[f.key];
        const on = value === f.key;
        const tone = filterTone[f.key];
        const n = counts?.[f.count];
        return (
          <button
            key={f.key || "all"}
            ref={(el) => { refs.current[i] = el; }}
            type="button"
            role="radio"
            aria-checked={on}
            tabIndex={on ? 0 : -1}
            onClick={() => onChange(f.key)}
            onKeyDown={(e) => keys(e, i)}
            className={`flex shrink-0 items-center whitespace-nowrap ${
              mobile
                ? `gap-1.5 rounded-[10px] border px-2.5 py-[7px] text-[12px] font-semibold ${on ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink-2"}`
                : `gap-1.5 rounded-[9px] px-[11px] py-[7px] text-[13px] ${on ? "bg-accent font-semibold text-white" : "font-medium text-ink-2 hover:bg-muted"}`
            }`}
          >
            <Icon aria-hidden className={`${mobile ? "size-[13px]" : "size-[14px]"} ${on ? "text-white" : tone ? toneText[tone] : "text-ink-3"}`} />
            {t(f.label)}
            <span className={`text-[11px] font-semibold tabular-nums ${on ? "text-white/70" : "text-ink-4"}`}>{n ?? "·"}</span>
          </button>
        );
      })}
    </div>
  );
}

/** Значок состояния поверх фото (сетка). */
function PhotoBadge({ status }: { status: RecipeStatus }) {
  const t = useT();
  const Icon = statusIcon[status];
  const tone = recipeTone[status];
  return (
    <span className={`inline-flex items-center gap-1.5 rounded-lg px-[9px] py-1 text-[11px] font-bold ${onPhoto} ${onPhotoText[tone]}`}>
      <Icon aria-hidden className="size-[13px]" /> {t(recipeStatusLabel[status])}
    </span>
  );
}

/** Пилюля состояния (строка списка и телефон): иконка, текст, мягкий фон. */
function StatusPill({ status }: { status: RecipeStatus }) {
  const t = useT();
  const Icon = statusIcon[status];
  const tone = recipeTone[status];
  return (
    <span className={`inline-flex items-center gap-1 rounded-md px-[7px] py-0.5 text-[10px] font-bold ${toneSoft[tone]} ${toneText[tone]}`}>
      <Icon aria-hidden className="size-[11px]" /> {t(recipeStatusLabel[status])}
    </span>
  );
}

function CodeChip({ code }: { code: string }) {
  return <span className="shrink-0 rounded-[5px] bg-muted px-1.5 py-px text-[11px] font-semibold tabular-nums text-ink-2">{code}</span>;
}

/** Карточка в сетке: фото 210 (узко — 180) с состоянием и версией, название ровно в две строки, код и база норм, три мини-блока. */
function RecipeGridCard({ r, canEdit }: { r: RecipeRow; canEdit: boolean }) {
  const t = useT();
  const date = r.status === "Active" ? effectiveDate(r.effectiveFrom) : null;
  const extra = newVersion(r, t);
  const facts = miniFacts(r, date, t);
  return (
    <li className="group relative flex flex-col overflow-hidden rounded-[18px] border border-line bg-surface transition-colors @container/card hover:border-line-strong">
      <div className="relative h-[180px] shrink-0 @min-[1232px]:h-[210px]">
        {r.photo ? (
          <PhotoImage photo={r.photo} alt={r.name} sizes="(min-width: 1600px) 25vw, (min-width: 1024px) 33vw, 50vw" zoom className="size-full" />
        ) : (
          <NoPhotoCard r={r} canEdit={canEdit} />
        )}
        <span className="pointer-events-none absolute left-3 top-3 z-10 flex flex-col items-start gap-1.5">
          <PhotoBadge status={r.status} />
          {extra && <span className={`rounded-lg px-[9px] py-1 text-[11px] font-semibold text-[#C9CFDC] ${onPhoto}`}>{extra}</span>}
        </span>
        <span className={`pointer-events-none absolute right-3 top-3 z-10 w-[72px] rounded-lg py-1 text-center text-[11px] font-semibold text-[#C9CFDC] ${onPhoto}`}>
          {t("версия {n}", { n: r.versionNo })}
        </span>
      </div>
      <div className="flex flex-1 flex-col gap-3 px-4 pb-4 pt-3.5">
        <h3 className="line-clamp-2 h-10 text-[15px] font-semibold leading-[1.3] text-ink" title={r.name}>
          {/* Вся карточка — ссылка (растянута на карточку); кнопка «Загрузить фото» лежит выше неё. */}
          <Link href={`/production/recipes/${r.id}`} className="outline-none after:absolute after:inset-0 after:z-0 after:rounded-[18px] focus-visible:after:ring-2 focus-visible:after:ring-accent">
            {r.name}
          </Link>
        </h3>
        <p className="flex min-w-0 items-center gap-1.5 text-[12px] text-ink-3">
          <CodeChip code={r.code} />
          <span className="truncate">{t("Выход: нормы на {q}", { q: normBase(r.outputQuantity, r.outputUnit) })}</span>
        </p>
        <MiniFacts facts={facts} className="mt-auto" />
      </div>
    </li>
  );
}

type MiniFact = { key: string; icon: LucideIcon; label: string; value: string; tone: string };

/** Три мини-блока карточки: этапы (0 — «нет»), компоненты, дата начала действия (только у утверждённой). */
const miniFacts = (r: RecipeRow, date: string | null, t: T): MiniFact[] => [
  { key: "stages", icon: Layers, label: "Этапы", value: r.stages > 0 ? String(r.stages) : t("нет"), tone: r.stages > 0 ? "text-ink" : "text-ink-4" },
  { key: "lines", icon: Package, label: "Компоненты", value: String(r.lines), tone: "text-ink" },
  { key: "from", icon: CalendarCheck, label: "Действует", value: date ? t("с {date}", { date }) : "—", tone: date ? "text-ok" : "text-ink-4" },
];

/** Мини-блоки: в узкой карточке (меньше 430 px) — без иконок и с меньшими отступами, чтобы «КОМПОНЕНТЫ» и дата не обрезались. */
function MiniFacts({ facts, className = "" }: { facts: MiniFact[]; className?: string }) {
  const t = useT();
  return (
    <dl className={`grid grid-cols-3 gap-2 ${className}`}>
      {facts.map((f) => (
        <div key={f.key} className="flex min-w-0 items-center gap-2 rounded-[10px] bg-muted px-2.5 py-2 @max-[430px]/card:px-2">
          <f.icon aria-hidden className="size-[15px] shrink-0 text-ink-4 @max-[430px]/card:hidden" />
          <div className="min-w-0">
            <dt className="truncate text-[10px] font-semibold uppercase tracking-[0.02em] text-ink-4">{t(f.label)}</dt>
            <dd className={`truncate text-[13px] font-semibold tabular-nums ${f.tone}`}>{f.value}</dd>
          </div>
        </div>
      ))}
    </dl>
  );
}

/** Нет фото (сетка): иконка, «Фото не добавлено» и — кто ведёт техкарты — «Загрузить фото» (сразу выбор файла, карточку не открывает). */
function NoPhotoCard({ r, canEdit }: { r: RecipeRow; canEdit: boolean }) {
  const t = useT();
  const up = usePhotoUpload(canEdit ? r.id : null);
  const picker = usePicker((file) => void up.upload(file));
  return (
    <div className="relative flex size-full flex-col items-center justify-center gap-2.5 bg-muted bg-[radial-gradient(closest-side,#3D7BFF14,transparent)] px-4 text-center">
      <span className="grid size-[52px] place-items-center rounded-2xl border border-line-strong bg-surface" role="img" aria-label={t("Фото не добавлено")}>
        <ImagePlus aria-hidden className="size-[22px] text-ink-3" />
      </span>
      <p className="text-[13px] font-semibold text-ink-2">{t("Фото не добавлено")}</p>
      {canEdit && (
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            picker.open();
          }}
          disabled={up.busy}
          aria-label={t("Загрузить фото для «{name}»", { name: r.name })}
          className="relative z-10 inline-flex items-center gap-1.5 rounded-lg bg-accent-soft px-3 py-1.5 text-[12px] font-semibold text-accent-strong hover:brightness-125 disabled:opacity-60"
        >
          <Upload aria-hidden className="size-[13px]" /> {up.busy ? t("Загружаем…") : t("Загрузить фото")}
        </button>
      )}
      {up.error && <p role="alert" className="relative z-10 max-w-full text-[12px] text-bad">{up.error}</p>}
      {up.progress !== null && <PhotoProgress value={up.progress} />}
      {picker.inputs}
    </div>
  );
}

/** Строка: телефон и вид «Список» на компьютере — квадратное фото слева, состояние и версия, название, код и база норм, факты. */
function RecipeRowCard({ r, canEdit, wide = false }: { r: RecipeRow; canEdit: boolean; wide?: boolean }) {
  const t = useT();
  const date = r.status === "Active" ? effectiveDate(r.effectiveFrom, !wide) : null;
  const extra = newVersion(r, t);
  return (
    <li className={`relative flex gap-3 ${wide ? "items-center" : ""} rounded-2xl border border-line bg-surface p-2.5 transition-colors @container/card hover:border-line-strong`}>
      <RowPhoto r={r} canEdit={canEdit} />
      <div className="flex min-w-0 flex-1 flex-col gap-1.5 py-0.5">
        <div className="flex items-center justify-between gap-2">
          <StatusPill status={r.status} />
          <span className="text-[11px] font-semibold text-ink-4">{wide ? t("версия {n}", { n: r.versionNo }) : `v${r.versionNo}`}</span>
        </div>
        <h3 className="line-clamp-3 text-[14px] font-semibold leading-[1.3] text-ink" title={r.name}>
          <Link href={`/production/recipes/${r.id}`} className="outline-none after:absolute after:inset-0 after:z-0 after:rounded-2xl focus-visible:after:ring-2 focus-visible:after:ring-accent">
            {r.name}
          </Link>
        </h3>
        <p className="flex min-w-0 items-center gap-1.5 text-[11px] text-ink-3">
          <CodeChip code={r.code} />
          <span className="truncate">{t("на {q}", { q: normBase(r.outputQuantity, r.outputUnit) })}</span>
        </p>
        <p className={`flex flex-wrap items-center gap-x-2.5 gap-y-1 text-[11px] font-medium ${wide ? "@min-[860px]/card:hidden" : ""}`}>
          <span className="inline-flex items-center gap-1 text-ink-2">
            <Package aria-hidden className="size-3 text-ink-4" /> {t("{n} комп.", { n: r.lines })}
          </span>
          <span className={`inline-flex items-center gap-1 ${r.stages > 0 ? "text-ink-2" : "text-ink-4"}`}>
            <Layers aria-hidden className="size-3 text-ink-4" />
            {r.stages > 0 ? `${r.stages} ${t(plural(r.stages, ["этап", "этапа", "этапов"]))}` : t("без этапов")}
          </span>
          {date && (
            <span className="inline-flex items-center gap-1 text-ok">
              <CalendarCheck aria-hidden className="size-3" /> {t("с {date}", { date })}
            </span>
          )}
          {extra && (
            <span className="inline-flex items-center gap-1 text-ink-3">
              <FilePen aria-hidden className="size-3 text-ink-4" /> {extra}
            </span>
          )}
        </p>
        {wide && extra && <p className="hidden items-center gap-1 text-[11px] font-medium text-ink-3 @min-[860px]/card:flex"><FilePen aria-hidden className="size-3 text-ink-4" /> {extra}</p>}
      </div>
      {wide && (
        <div className="hidden w-[380px] shrink-0 pr-1.5 @min-[860px]/card:block">
          <MiniFacts facts={miniFacts(r, effectiveDate(r.status === "Active" ? r.effectiveFrom : null), t)} />
        </div>
      )}
    </li>
  );
}

/** Квадрат 104 × 104: фото или «Добавить фото» (выбор файла — камера или галерея телефона). */
function RowPhoto({ r, canEdit }: { r: RecipeRow; canEdit: boolean }) {
  const t = useT();
  const up = usePhotoUpload(canEdit && !r.photo ? r.id : null);
  const picker = usePicker((file) => void up.upload(file));
  if (r.photo) return <PhotoImage photo={r.photo} alt={r.name} sizes="104px" compact className="size-[104px] shrink-0 rounded-xl" />;
  const inner = (
    <>
      <ImagePlus aria-hidden className="size-[22px] text-ink-3" />
      {canEdit && <span className="px-1 text-[10px] font-semibold leading-tight text-accent-strong">{up.busy ? t("Загружаем…") : t("Добавить фото")}</span>}
      {up.progress !== null && <PhotoProgress value={up.progress} />}
    </>
  );
  const box = "relative flex size-[104px] shrink-0 flex-col items-center justify-center gap-1.5 overflow-hidden rounded-xl border border-line-strong bg-muted text-center";
  return canEdit ? (
    <div className="relative z-10 shrink-0">
      <button type="button" onClick={picker.open} disabled={up.busy} aria-label={t("Загрузить фото для «{name}»", { name: r.name })} className={box}>
        {inner}
      </button>
      {up.error && <p role="alert" className="mt-1 w-[104px] text-[10px] leading-tight text-bad">{up.error}</p>}
      {picker.inputs}
    </div>
  ) : (
    <span className={box} role="img" aria-label={t("Фото не добавлено")}>
      {inner}
    </span>
  );
}

function CardSkeleton() {
  return (
    <li aria-hidden className="overflow-hidden rounded-[18px] border border-line bg-surface">
      <span className="block h-[180px] animate-pulse bg-muted @min-[1232px]:h-[210px]" />
      <div className="flex flex-col gap-3 px-4 pb-4 pt-3.5">
        <span className="block h-4 w-4/5 animate-pulse rounded-md bg-muted" />
        <span className="block h-4 w-1/2 animate-pulse rounded-md bg-muted" />
        <span className="grid grid-cols-3 gap-2">
          {[0, 1, 2].map((i) => <span key={i} className="block h-[46px] animate-pulse rounded-[10px] bg-muted" />)}
        </span>
      </div>
    </li>
  );
}

function RowSkeleton() {
  return (
    <li aria-hidden className="flex gap-3 rounded-2xl border border-line bg-surface p-2.5">
      <span className="block size-[104px] shrink-0 animate-pulse rounded-xl bg-muted" />
      <span className="flex flex-1 flex-col gap-2 py-1">
        <span className="block h-4 w-20 animate-pulse rounded-md bg-muted" />
        <span className="block h-4 w-4/5 animate-pulse rounded-md bg-muted" />
        <span className="block h-3 w-1/2 animate-pulse rounded-md bg-muted" />
        <span className="block h-3 w-2/3 animate-pulse rounded-md bg-muted" />
      </span>
    </li>
  );
}

function NothingFound({ onReset }: { onReset: () => void }) {
  const t = useT();
  return (
    <div className="flex flex-col items-center rounded-2xl border border-line bg-surface px-6 py-12 text-center">
      <span className="grid size-12 place-items-center rounded-2xl bg-muted">
        <SearchX aria-hidden className="size-6 text-ink-3" />
      </span>
      <p className="mt-3 text-[15px] font-semibold text-ink">{t("Ничего не найдено")}</p>
      <p className="mt-1 text-[13px] text-ink-3">{t("Измените поиск или состояние техкарты.")}</p>
      <button type="button" onClick={onReset} className="mt-4 inline-flex h-9 items-center rounded-lg border border-line-strong bg-surface px-3.5 text-[13px] font-semibold text-ink hover:bg-muted">
        {t("Сбросить фильтры")}
      </button>
    </div>
  );
}

function EmptyAll({ canEdit }: { canEdit: boolean }) {
  const t = useT();
  return (
    <div className="mx-auto max-w-[1800px] px-4 pb-10 pt-3 md:px-6 md:pt-5">
      <section className="flex flex-col items-center rounded-2xl border border-line bg-surface px-6 py-14 text-center">
        <span className="grid size-12 place-items-center rounded-2xl bg-accent-soft text-accent-strong">
          <FlaskConical aria-hidden className="size-6" />
        </span>
        <h2 className="mt-4 text-lg font-semibold text-ink">{t("Техкарт пока нет")}</h2>
        <p className="mt-1 max-w-md text-sm text-ink-3">
          {t("Карта продукта — этапы изготовления (замес, выпечка, начинка, глазурь, упаковка…), рецептура замеса, нормы на 1 000 кг, выход и брак каждого этапа. По утверждённой версии создаются задания на выпуск, и система считает сырьё по цепочке.")}
        </p>
        {canEdit && (
          <div className="mt-5 flex flex-wrap justify-center gap-2">
            <Link href="/production/recipes/new" className="inline-flex h-10 items-center gap-2 rounded-xl bg-accent px-4 text-sm font-semibold text-white hover:brightness-110">
              <Plus aria-hidden className="size-4" /> {t("Новая техкарта")}
            </Link>
            <Link href="/production/recipes/import" className="inline-flex h-10 items-center gap-2 rounded-xl border border-line-strong bg-surface px-4 text-sm font-semibold text-ink hover:bg-muted">
              <FileSpreadsheet aria-hidden className="size-4 text-ok" /> {t("Импорт из Excel")}
            </Link>
          </div>
        )}
      </section>
    </div>
  );
}
