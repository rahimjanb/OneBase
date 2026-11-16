// «Настройки → Telegram → Автоматические отчёты → Карточки категорий» (отчёт 10): выбор категорий по умолчанию и у получателя, кто подписан,
// какие категории ему уйдут. Без интерфейса — проверяется тестами (telegram-category-cards.test.mjs).
import type { ReportJob, ReportRecipient } from "./telegramReports";

export const CARD_REPORT = "category-cards";

/** Категория для выбора: название из «Настроек продаж» и второе название («Помадка» — «Могуль»). */
export type CategoryCardOption = { name: string; alias: string | null };

/** Выбор получателя: categories = null — по умолчанию. */
export type CategoryCardRecipient = { id: string; categories: string[] | null };

export type CategoryCardsView = {
  categories: CategoryCardOption[];
  /** Выбор по умолчанию; пусто — все категории. */
  defaults: string[];
  recipients: CategoryCardRecipient[];
  /** Сколько категорий в одной отправке (альбом Telegram). */
  max: number;
  updatedAt: string | null;
};

/** «Помадка · Могуль». */
export const categoryLabel = (c: CategoryCardOption) => (c.alias ? `${c.name} · ${c.alias}` : c.name);

/** Запуски карточек категорий (утро и вечер) в порядке расписания. */
export const cardJobs = (jobs: ReportJob[]) => jobs.filter((j) => j.report === CARD_REPORT);

/** Получатели, подписанные хотя бы на один запуск карточек. */
export const cardRecipients = (recipients: ReportRecipient[]) => recipients.filter((r) => r.reports.some((code) => code.startsWith(`${CARD_REPORT}.`)));

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

/** Одинаковый выбор (порядок важен — он же порядок картинок). */
export const sameChoice = (a: string[], b: string[]) => a.length === b.length && a.every((x, i) => x === b[i]);

/** «Помадка, Придзел» или «все категории». */
export const choiceText = (names: string[], total: number) => (names.length === 0 || names.length === total ? "все категории" : names.join(", "));
