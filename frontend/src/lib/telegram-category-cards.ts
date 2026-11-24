// «Настройки → Telegram → Автоматические отчёты → Карточки категорий» (отчёт 11 «Итоги дня по категориям»): выбор категорий (и, если нужно,
// их товаров) по умолчанию и у получателя, кто подписан, какие категории ему уйдут. Без интерфейса — проверяется тестами
// (telegram-category-cards.test.mjs).
import type { ReportJob, ReportRecipient } from "./telegramReports";

/** «11. Итоги дня по категориям» — продажи только за день (прежний отчёт 10 «Карточки категорий» с месяцем убран). */
export const DAY_REPORT = "category-day";

/** Товар категории из справочника Linko. */
export type CategoryCardProduct = { id: number; name: string; code: string | null };

/** Категория для выбора: название из «Настроек продаж», второе название («Помадка» — «Могуль») и её товары. */
export type CategoryCardOption = { name: string; alias: string | null; products?: CategoryCardProduct[] };

/** Выбранные товары: категория → id товаров; категории нет — вся категория (и новые товары в ней тоже). */
export type ProductChoice = Record<string, number[]>;

/** Выбор получателя: categories = null — по умолчанию; products — выбранные товары его категорий. */
export type CategoryCardRecipient = { id: string; categories: string[] | null; products?: ProductChoice | null };

export type CategoryCardsView = {
  categories: CategoryCardOption[];
  /** Выбор по умолчанию; пусто — все категории. */
  defaults: string[];
  /** Выбранные товары категорий по умолчанию. */
  defaultProducts?: ProductChoice | null;
  recipients: CategoryCardRecipient[];
  /** Сколько категорий в одной отправке (альбом Telegram). */
  max: number;
  updatedAt: string | null;
};

/** «Помадка · Могуль». */
export const categoryLabel = (c: CategoryCardOption) => (c.alias ? `${c.name} · ${c.alias}` : c.name);

/** Запуски итогов дня по категориям (утро и вечер) в порядке каталога. */
export const cardJobs = (jobs: ReportJob[]) => jobs.filter((j) => j.report === DAY_REPORT);

/** Получатели, подписанные хотя бы на один запуск итогов дня по категориям. */
export const cardRecipients = (recipients: ReportRecipient[]) => recipients.filter((r) => r.reports.some((code) => code.startsWith(`${DAY_REPORT}.`)));

/**
 * Включить или выключить категорию: порядок — как в «Настройках продаж» (так и на картинках), больше max — не добавляется (вернётся тот же
 * список).
 */
export function toggleCategory(selected: string[], name: string, options: CategoryCardOption[], max: number): string[] {
  const has = selected.includes(name);
  if (!has && selected.length >= max) return selected;
  const next = has ? selected.filter((x) => x !== name) : [...selected, name];
  return options.map((o) => o.name).filter((x) => next.includes(x));
}

/** Какие категории уйдут: свой выбор получателя, иначе — по умолчанию, иначе — все (не больше max). */
export function effectiveCategories(own: string[] | null | undefined, view: Pick<CategoryCardsView, "defaults" | "categories" | "max">): string[] {
  if (own && own.length > 0) return own;
  if (view.defaults.length > 0) return view.defaults;
  return view.categories.map((c) => c.name).slice(0, view.max);
}

/** Какие товары уйдут вместе с effectiveCategories: свои (если свой выбор категорий), иначе — по умолчанию; «все категории» — без выбора товаров. */
export function effectiveProducts(
  own: string[] | null | undefined,
  ownProducts: ProductChoice | null | undefined,
  view: Pick<CategoryCardsView, "defaults" | "defaultProducts">,
): ProductChoice {
  if (own && own.length > 0) return ownProducts ?? {};
  if (view.defaults.length > 0) return view.defaultProducts ?? {};
  return {};
}

/** Одинаковый выбор (порядок важен — он же порядок картинок). */
export const sameChoice = (a: string[], b: string[]) => a.length === b.length && a.every((x, i) => x === b[i]);

/** Одинаковый выбор товаров (порядок товаров не важен). */
export function sameProducts(a: ProductChoice, b: ProductChoice): boolean {
  const keys = Object.keys(a);
  if (keys.length !== Object.keys(b).length) return false;
  return keys.every((k) => {
    const x = [...a[k]].sort((m, n) => m - n);
    const y = [...(b[k] ?? [])].sort((m, n) => m - n);
    return x.length === y.length && x.every((v, i) => v === y[i]);
  });
}

/** Товары только выбранных категорий (у снятой категории выбор товаров забывается). */
export const keepProducts = (products: ProductChoice, names: string[]): ProductChoice =>
  Object.fromEntries(Object.entries(products).filter(([k, ids]) => names.includes(k) && ids.length > 0));

/** Выбранные товары категории: свой выбор или все товары категории. */
export const pickedProducts = (products: ProductChoice, option: CategoryCardOption): number[] =>
  products[option.name] ?? (option.products ?? []).map((p) => p.id);

/**
 * Отметить или снять товар категории: отмечены все — снова «вся категория» (ключ убирается, новые товары тоже попадут); последний
 * отмеченный товар снять нельзя (вернётся тот же выбор).
 */
export function toggleProduct(products: ProductChoice, option: CategoryCardOption, id: number): ProductChoice {
  const all = (option.products ?? []).map((p) => p.id);
  const current = pickedProducts(products, option);
  const next = current.includes(id) ? current.filter((x) => x !== id) : [...current, id];
  if (next.length === 0) return products;
  const rest = Object.fromEntries(Object.entries(products).filter(([k]) => k !== option.name));
  return all.every((x) => next.includes(x)) ? rest : { ...rest, [option.name]: all.filter((x) => next.includes(x)) };
}

/** Снова вся категория. */
export const allProducts = (products: ProductChoice, name: string): ProductChoice => Object.fromEntries(Object.entries(products).filter(([k]) => k !== name));

/**
 * «Помадка, Придзел» или «все категории»; у категории с выбранными товарами — «Придзел (товаров: 3)» (подпись — productsLabel, чтобы её
 * можно было перевести).
 */
export const choiceText = (names: string[], total: number, products: ProductChoice = {}, productsLabel: (n: number) => string = (n) => `товаров: ${n}`) =>
  (names.length === 0 || names.length === total) && Object.keys(products).length === 0
    ? "все категории"
    : names.map((n) => (products[n]?.length ? `${n} (${productsLabel(products[n].length)})` : n)).join(", ");
