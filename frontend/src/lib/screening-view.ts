// HR → «Подбор» → «AI-скрининг» — чистые функции экрана (без запросов): подписи и цвета статусов и категорий, стаж словами, фильтры в адресе,
// выбор для сравнения. Тест: node --test src/lib/screening-view.test.mjs.

export type ScreeningStatus = "new" | "analyzing" | "suitable" | "recommended" | "interview" | "rejected" | "hired" | "reserve";
export type ScreeningCategory = "excellent" | "very_good" | "good" | "partial" | "low";
export type Tone = "ok" | "accent" | "violet" | "warn" | "bad" | "neutral";

export const STATUSES: ScreeningStatus[] = ["new", "analyzing", "suitable", "recommended", "interview", "rejected", "hired", "reserve"];

/** Статусы, которые выбирает HR («анализируется» ставит очередь). */
export const SETTABLE: ScreeningStatus[] = ["new", "suitable", "recommended", "interview", "reserve", "hired", "rejected"];

export const statusLabel: Record<ScreeningStatus, string> = {
  new: "Новый",
  analyzing: "Анализируется",
  suitable: "Подходит",
  recommended: "Рекомендован",
  interview: "На собеседовании",
  rejected: "Отказ",
  hired: "Принят на работу",
  reserve: "В резерве",
};

export const statusTone: Record<ScreeningStatus, Tone> = {
  new: "neutral",
  analyzing: "accent",
  suitable: "ok",
  recommended: "violet",
  interview: "accent",
  rejected: "bad",
  hired: "ok",
  reserve: "warn",
};

export const categoryLabel: Record<ScreeningCategory, string> = {
  excellent: "Отличный кандидат",
  very_good: "Очень хороший кандидат",
  good: "Подходит",
  partial: "Частично подходит",
  low: "Низкое соответствие",
};

/** Категория по итогу — как на сервере: 90 / 80 / 70 / 50. */
export function category(score: number): ScreeningCategory {
  return score >= 90 ? "excellent" : score >= 80 ? "very_good" : score >= 70 ? "good" : score >= 50 ? "partial" : "low";
}

export function scoreTone(score: number | null | undefined): Tone {
  if (score == null) return "neutral";
  return score >= 80 ? "ok" : score >= 70 ? "accent" : score >= 50 ? "warn" : "bad";
}

export const questionTypeLabel: Record<string, string> = {
  technical: "Технический",
  practical: "Практический",
  experience: "Опыт",
  achievement: "Проверка достижений",
  gap: "Пробел в резюме",
};

/** Стаж: «6 лет», «3 года 4 мес.», «8 мес.»; нет — null. */
export function experienceText(months: number | null | undefined): string | null {
  if (months == null || months <= 0) return null;
  const y = Math.floor(months / 12);
  const m = months % 12;
  if (y === 0) return `${m} мес.`;
  const word = y % 10 === 1 && y % 100 !== 11 ? "год" : [2, 3, 4].includes(y % 10) && ![12, 13, 14].includes(y % 100) ? "года" : "лет";
  return m === 0 ? `${y} ${word}` : `${y} ${word} ${m} мес.`;
}

/** Баллы критерия: «12,5 / 25». */
export function pointsText(points: number, max: number): string {
  const p = Number.isInteger(points) ? String(points) : points.toFixed(1).replace(".", ",");
  return `${p} / ${max}`;
}

/** Доля 0–1 → «45%». */
export const percent = (x: number | null | undefined) => (x == null ? null : `${Math.round(x * 100)}%`);

// ---------- Фильтры в адресе ----------

export type ScreeningFilters = {
  vacancyId: string;
  status: string;
  minScore: string;
  city: string;
  skill: string;
  minExperience: string;
  position: string;
  search: string;
  sort: string;
  view: "cards" | "table";
};

export const EMPTY_FILTERS: ScreeningFilters = {
  vacancyId: "", status: "", minScore: "", city: "", skill: "", minExperience: "", position: "", search: "", sort: "score", view: "cards",
};

/** Из параметров адреса (?vacancy=…&status=…): чужие значения — по умолчанию. */
export function parseFilters(params: { get(name: string): string | null }): ScreeningFilters {
  const status = params.get("status") ?? "";
  const sort = params.get("sort") ?? "score";
  const num = (v: string | null) => (v && /^\d{1,3}$/.test(v) ? v : "");
  return {
    vacancyId: params.get("vacancy") ?? "",
    status: (STATUSES as string[]).includes(status) ? status : "",
    minScore: num(params.get("score")),
    city: params.get("city") ?? "",
    skill: params.get("skill") ?? "",
    minExperience: num(params.get("exp")),
    position: params.get("position") ?? "",
    search: params.get("q") ?? "",
    sort: ["score", "recent", "name", "experience"].includes(sort) ? sort : "score",
    view: params.get("view") === "table" ? "table" : "cards",
  };
}

/** Фильтры → строка адреса (пустые и значения по умолчанию не пишутся). */
export function filtersQuery(f: ScreeningFilters): string {
  const pairs: [string, string][] = [
    ["vacancy", f.vacancyId], ["status", f.status], ["score", f.minScore], ["city", f.city], ["skill", f.skill], ["exp", f.minExperience],
    ["position", f.position], ["q", f.search], ["sort", f.sort === "score" ? "" : f.sort], ["view", f.view === "cards" ? "" : f.view],
  ];
  const s = pairs.filter(([, v]) => v.trim() !== "").map(([k, v]) => `${k}=${encodeURIComponent(v.trim())}`).join("&");
  return s ? `?${s}` : "";
}

/** Фильтры → запрос к API (минимальный стаж — в месяцах из лет). */
export function apiQuery(f: ScreeningFilters): Record<string, string> {
  const q: Record<string, string> = {};
  if (f.vacancyId) q.vacancyId = f.vacancyId;
  if (f.status) q.status = f.status;
  if (f.minScore) q.minScore = f.minScore;
  if (f.city) q.city = f.city;
  if (f.skill.trim()) q.skill = f.skill.trim();
  if (f.minExperience) q.minExperience = String(Number(f.minExperience) * 12);
  if (f.position.trim()) q.position = f.position.trim();
  if (f.search.trim()) q.search = f.search.trim();
  if (f.sort && f.sort !== "score") q.sort = f.sort;
  return q;
}

/** Сколько фильтров задано (кроме вакансии, порядка и вида). */
export function activeFilters(f: ScreeningFilters): number {
  return [f.status, f.minScore, f.city, f.skill, f.minExperience, f.position, f.search].filter((v) => v.trim() !== "").length;
}

// ---------- Сравнение ----------

export const MAX_COMPARE = 5;

/**
 * Выбор для сравнения: до пяти кандидатов одной вакансии. Отмеченный снова — снимается; другой вакансии — выбор начинается заново с него;
 * шестой — не добавляется.
 */
export function toggleCompare(
  selected: { applicationId: string; vacancyId: string }[],
  row: { applicationId: string; vacancyId: string },
): { applicationId: string; vacancyId: string }[] {
  if (selected.some((s) => s.applicationId === row.applicationId)) return selected.filter((s) => s.applicationId !== row.applicationId);
  if (selected.length > 0 && selected[0].vacancyId !== row.vacancyId) return [row];
  if (selected.length >= MAX_COMPARE) return selected;
  return [...selected, row];
}

/** Можно ли сравнить: 2–5 кандидатов, у всех есть анализ. */
export function canCompare(selected: { analyzed: boolean }[]): boolean {
  return selected.length >= 2 && selected.length <= MAX_COMPARE && selected.every((s) => s.analyzed);
}
