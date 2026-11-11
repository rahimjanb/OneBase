// «Технолог → Техкарты» — чистые функции списка (без запросов): фильтры состояния и их счётчики, подписи карточки (база норм, этапы,
// компоненты, дата начала действия), сортировка, вид «Сетка / Список», адреса фото продукта и проверка файла фото перед загрузкой.
// Тест: node --test src/lib/recipe-cards.test.mjs.
import { quantity } from "./procurement.ts";
import type { RecipeCounts, RecipePhoto, RecipeRow, RecipeStatus } from "./production.ts";

/** Фильтр состояния в адресе (?status=…): как его понимает сервер. */
export type RecipeFilter = "" | "active" | "draft" | "pending" | "archived";

export const RECIPE_FILTERS: { key: RecipeFilter; label: string; count: keyof RecipeCounts }[] = [
  { key: "", label: "Все", count: "all" },
  { key: "active", label: "Утверждённые", count: "active" },
  { key: "draft", label: "Черновики", count: "draft" },
  { key: "pending", label: "На утверждении", count: "pending" },
  { key: "archived", label: "Архив", count: "archived" },
];

export const parseFilter = (value: string | null | undefined): RecipeFilter =>
  RECIPE_FILTERS.some((f) => f.key === value) ? (value as RecipeFilter) : "";

/** Счётчики фильтров: от сервера; старый ответ без них — по видимым карточкам (только «Все» точно). */
export function filterCounts(counts: RecipeCounts | null | undefined, rows: RecipeRow[]): RecipeCounts {
  if (counts) return counts;
  const n = (s: RecipeStatus) => rows.filter((r) => r.status === s).length;
  return {
    all: rows.length,
    active: n("Active"),
    draft: rows.filter((r) => r.draftStatus === "Draft").length,
    pending: rows.filter((r) => r.draftStatus === "PendingApproval").length,
    archived: n("Archived"),
  };
}

/** Тон значка состояния: утверждена — зелёный, черновик — жёлтый, на утверждении — синий (link), архив — приглушённый. */
export type RecipeTone = "ok" | "warn" | "link" | "subtle";

export const recipeTone: Record<RecipeStatus, RecipeTone> = { Active: "ok", Draft: "warn", PendingApproval: "link", Archived: "subtle" };

/** Число с неразрывными пробелами между разрядами: «1 000». */
export const nbsp = (value: number) => quantity(value).replace(/ /g, " ");

/** База норм: «1 000 кг» (неразрывные пробелы — строка не рвётся посередине числа). */
export const normBase = (q: number, unit: string) => `${nbsp(q)} ${unit}`;

/** Дата начала действия: компьютер — «09.11.2026», телефон — «09.11»; нет — null. */
export function effectiveDate(iso: string | null | undefined, short = false): string | null {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso ?? "");
  if (!m) return null;
  return short ? `${m[3]}.${m[2]}` : `${m[3]}.${m[2]}.${m[1]}`;
}

/** Сортировка списка: сначала новые (последнее изменение любой версии), по названию, по коду (числа в коде — по значению). */
export type RecipeSort = "new" | "name" | "code";

export const RECIPE_SORTS: { key: RecipeSort; label: string }[] = [
  { key: "new", label: "Сначала новые" },
  { key: "name", label: "По названию" },
  { key: "code", label: "По коду" },
];

export const parseSort = (value: string | null | undefined): RecipeSort => (RECIPE_SORTS.some((s) => s.key === value) ? (value as RecipeSort) : "new");

const collator = new Intl.Collator("ru", { numeric: true, sensitivity: "base" });

export function sortRecipes(rows: RecipeRow[], sort: RecipeSort): RecipeRow[] {
  const byName = (a: RecipeRow, b: RecipeRow) => collator.compare(a.name, b.name) || collator.compare(a.code, b.code);
  const list = [...rows];
  if (sort === "name") return list.sort(byName);
  if (sort === "code") return list.sort((a, b) => collator.compare(a.code, b.code));
  const time = (r: RecipeRow) => (r.updatedAt ? Date.parse(r.updatedAt) : 0);
  return list.sort((a, b) => time(b) - time(a) || byName(a, b));
}

/** Вид списка на компьютере: сетка карточек с фото или компактный список; выбор помнит браузер. */
export type RecipeView = "grid" | "list";

export const parseView = (value: string | null | undefined): RecipeView => (value === "list" ? "list" : "grid");

export const VIEW_KEY = "onebase.recipes.view";
export const SORT_KEY = "onebase.recipes.sort";

/** Адрес картинки фото продукта нужной ширины: 480 и 960 — превью для списка, 1600 — оригинал (форма техкарты). */
export const photoSrc = (photo: Pick<RecipePhoto, "id">, width: 480 | 960 | 1600) => `/bff/api/production/recipe-photos/${photo.id}/${width}`;

/** srcset превью: браузер сам берёт 480 или 960 по ширине места и плотности экрана. */
export const photoSrcSet = (photo: Pick<RecipePhoto, "id">) => `${photoSrc(photo, 480)} 480w, ${photoSrc(photo, 960)} 960w`;

export const PHOTO_MAX_BYTES = 10 * 1024 * 1024;

/** Что принимает поле выбора файла: JPEG, PNG, WebP, HEIC (у HEIC браузер иногда не знает тип — по расширению). */
export const PHOTO_ACCEPT = "image/jpeg,image/png,image/webp,image/heic,image/heif,.heic,.heif";

const PHOTO_TYPES = ["image/jpeg", "image/png", "image/webp", "image/heic", "image/heif"];

/** Проверка файла до загрузки: ошибка — текст для человека (переводится в интерфейсе), всё хорошо — null. */
export function photoFileError(file: { name: string; type: string; size: number }): string | null {
  const type = file.type.toLowerCase();
  const heic = /\.(heic|heif)$/i.test(file.name);
  if (!(PHOTO_TYPES.includes(type) || (heic && (type === "" || type === "application/octet-stream")))) return "Фото — JPEG, PNG, WebP или HEIC.";
  if (file.size <= 0) return "Файл пустой — выберите фото.";
  if (file.size > PHOTO_MAX_BYTES) return "Фото — до 10 МБ.";
  return null;
}
