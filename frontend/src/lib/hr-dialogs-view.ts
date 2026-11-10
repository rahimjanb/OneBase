// «Диалоги Telegram» — чистые функции экрана (без запросов): время в списке, порядок и фильтры, цвет аватара, этапы, оценка.
// Тест: node --test src/lib/hr-dialogs.test.mjs.

export type DialogMode = "ai" | "hr" | "need_hr";
export type DialogFilter = "all" | "ai" | "need_hr" | "new";
export type Tone = "accent" | "violet" | "ok" | "warn" | "bad";

export type DialogRowLike = { id: string; name: string; username: string | null; mode: string; unread: number; lastAt: string | null };

const ZONE = "Asia/Tashkent";

/** Дата по Ташкенту: «2026-11-10». */
function dayKey(date: Date): string {
  return date.toLocaleDateString("en-CA", { timeZone: ZONE });
}

/** Дней между датами (по Ташкенту): 0 — сегодня, 1 — вчера. */
function daysAgo(at: Date, now: Date): number {
  const a = Date.parse(`${dayKey(at)}T00:00:00Z`);
  const b = Date.parse(`${dayKey(now)}T00:00:00Z`);
  return Math.round((b - a) / 86_400_000);
}

const shortDays = ["Вс", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб"];

/** Время в списке: сегодня — «10:15», вчера — «Вчера», на этой неделе — день («Пн»), раньше — «07.11». */
export function listTime(iso: string | null, now: Date = new Date()): string {
  if (!iso) return "";
  const at = new Date(iso);
  const days = daysAgo(at, now);
  if (days <= 0) return at.toLocaleTimeString("ru-RU", { timeZone: ZONE, hour: "2-digit", minute: "2-digit" });
  if (days === 1) return "Вчера";
  const weekday = new Date(`${dayKey(at)}T12:00:00Z`).getUTCDay();
  const today = new Date(`${dayKey(now)}T12:00:00Z`).getUTCDay();
  const monday = (today + 6) % 7; // сколько дней прошло с понедельника этой недели
  if (days <= monday) return shortDays[weekday];
  return at.toLocaleDateString("ru-RU", { timeZone: ZONE, day: "2-digit", month: "2-digit" });
}

/** Разделитель дня в ленте: «Сегодня», «Вчера», «8 ноября». */
export function dayLabel(iso: string, now: Date = new Date()): string {
  const at = new Date(iso);
  const days = daysAgo(at, now);
  if (days <= 0) return "Сегодня";
  if (days === 1) return "Вчера";
  return at.toLocaleDateString("ru-RU", { timeZone: ZONE, day: "numeric", month: "long" });
}

export const sameDay = (a: string, b: string) => dayKey(new Date(a)) === dayKey(new Date(b));

/** Время сообщения: «10:04». */
export const clock = (iso: string) => new Date(iso).toLocaleTimeString("ru-RU", { timeZone: ZONE, hour: "2-digit", minute: "2-digit" });

/**
 * «писал(а) 2 мин назад» — по последнему входящему (статус «в сети» Telegram в OneBase не приходит). Ключ перевода и подстановки: n — число,
 * when — «Вчера», «Пн» или «07.11» (тоже переводится).
 */
export type LastSeen = { key: string; n?: number; when?: string };

export function lastSeen(iso: string | null, now: Date = new Date()): LastSeen | null {
  if (!iso) return null;
  const minutes = Math.max(0, Math.round((now.getTime() - new Date(iso).getTime()) / 60_000));
  if (minutes < 1) return { key: "писал(а) только что" };
  if (minutes < 60) return { key: "писал(а) {n} мин назад", n: minutes };
  const hours = Math.round(minutes / 60);
  if (hours < 24) return { key: "писал(а) {n} ч назад", n: hours };
  return { key: "писал(а) {when}", when: listTime(iso, now) };
}

/** Порядок списка: сначала «Нужен HR», затем по последнему сообщению (новые выше). */
export function sortDialogs<T extends DialogRowLike>(rows: T[]): T[] {
  return [...rows].sort((a, b) => Number(b.mode === "need_hr") - Number(a.mode === "need_hr") || (Date.parse(b.lastAt ?? "") || 0) - (Date.parse(a.lastAt ?? "") || 0));
}

export function matchesFilter(row: DialogRowLike, filter: DialogFilter): boolean {
  if (filter === "ai") return row.mode === "ai";
  if (filter === "need_hr") return row.mode === "need_hr";
  if (filter === "new") return row.unread > 0;
  return true;
}

/** Поиск по имени и @username (без учёта регистра и «@»). */
export function matchesSearch(row: DialogRowLike, search: string): boolean {
  const q = search.trim().toLowerCase().replace(/^@/, "");
  if (!q) return true;
  return row.name.toLowerCase().includes(q) || (row.username ?? "").toLowerCase().includes(q);
}

export function filterCounts(rows: DialogRowLike[]): Record<DialogFilter, number> {
  return {
    all: rows.length,
    ai: rows.filter((r) => r.mode === "ai").length,
    need_hr: rows.filter((r) => r.mode === "need_hr").length,
    new: rows.filter((r) => r.unread > 0).length,
  };
}

const tones: Tone[] = ["accent", "violet", "ok", "warn", "bad"];

/** Цвет аватара без фото — по хэшу id (у одного человека всегда один). */
export function avatarTone(id: string): Tone {
  let hash = 0;
  for (let i = 0; i < id.length; i++) hash = (hash * 31 + id.charCodeAt(i)) | 0;
  return tones[Math.abs(hash) % tones.length];
}

/** Инициалы: «Жасур Тошматов» → «ЖТ», «@jasur_t» → «J». */
export function initials(name: string): string {
  const words = name.replace(/^@/, "").split(/\s+/).filter((w) => /\p{L}/u.test(w));
  return words.slice(0, 2).map((w) => w.match(/\p{L}/u)![0].toUpperCase()).join("") || "?";
}

/** Префикс последнего сообщения в списке: «AI: », «Вы: ». */
export const authorPrefix = (author: string | null): string => (author === "ai" || author === "auto" ? "AI: " : author === "hr" ? "Вы: " : "");

/** Этапы в карточке: Отклик, Анкета, Собес., Оффер; индекс текущего (−1 — закрыт отказом). */
export const STAGE_STEPS = ["New", "Screening", "Interview", "Offer"] as const;

export function stageIndex(stage: string | null): number {
  if (!stage) return 0;
  if (stage === "Hired") return STAGE_STEPS.length;
  if (stage === "Rejected" || stage === "Withdrawn") return -1;
  return Math.max(0, STAGE_STEPS.indexOf(stage as (typeof STAGE_STEPS)[number]));
}

/** Цвет оценки AI: от 70 — зелёный, 40–69 — жёлтый, ниже — красный. */
export const scoreTone = (value: number): "ok" | "warn" | "bad" => (value >= 70 ? "ok" : value >= 40 ? "warn" : "bad");

/** Размер файла: «2 КБ», «1,4 МБ»; units — переведённые «КБ» и «МБ». */
export function fileSize(bytes: number | null, units: [kb: string, mb: string] = ["КБ", "МБ"]): string {
  if (bytes == null) return "";
  if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} ${units[0]}`;
  return `${(bytes / 1024 / 1024).toFixed(1).replace(".", ",")} ${units[1]}`;
}
