// HR → «Подбор» — чистые функции экрана (без запросов): подписи и значки кнопок разделов, фильтр и порядок вакансий, короткая и полная
// зарплата, инициалы и цвет аватара рекрутера. Тест: node --test src/lib/recruitment-view.test.mjs.

/** Строка списка вакансий — поля, нужные экрану (полный тип — VacancyRow в lib/recruitment.ts). */
export type VacancyLike = {
  title: string;
  city: string | null;
  departmentId: string;
  status: "Draft" | "Open" | "Paused" | "Closed";
  updatedAt: string;
};

/** Подписи кнопок разделов: GET /api/hr/recruitment/nav. */
export type NavCounters = {
  openVacancies: number;
  candidatesInWork: number;
  telegramNew: number;
  activePublications: number;
  interviewsToday: number;
  openExceptions: number;
  hhConnected: boolean;
};

export type NavKey = "vacancies" | "candidates" | "telegram" | "publications" | "interviews" | "exceptions" | "analytics" | "hh" | "settings";

/** Подпись под названием кнопки: текст для перевода и подстановка; tone — цвет (zero — «нет новых» и т. п. цветом subtle, warn — «не подключён»). */
export type NavCaption = { key: string; vars?: Record<string, number>; tone?: "zero" | "warn" };

const counted: Partial<Record<NavKey, [keyof NavCounters, string, string]>> = {
  vacancies: ["openVacancies", "{n} открыто", "нет открытых"],
  candidates: ["candidatesInWork", "{n} в работе", "нет в работе"],
  telegram: ["telegramNew", "{n} новых", "нет новых"],
  publications: ["activePublications", "{n} активны", "нет активных"],
  interviews: ["interviewsToday", "{n} сегодня", "нет сегодня"],
  exceptions: ["openExceptions", "{n} в списке", "список пуст"],
};

/** Подпись кнопки раздела. null — счётчики ещё не пришли (у разделов со счётчиком на это время — скелетон). */
export function navCaption(key: NavKey, c: NavCounters | null): NavCaption | null {
  if (key === "analytics") return { key: "воронка" };
  if (key === "settings") return { key: "этапы" };
  if (!c) return null;
  if (key === "hh") return c.hhConnected ? { key: "подключён" } : { key: "не подключён", tone: "warn" };
  const [field, some, none] = counted[key]!;
  const n = c[field] as number;
  return n > 0 ? { key: some, vars: { n } } : { key: none, tone: "zero" };
}

/** Значок-счётчик на плитке телефона — только «Telegram» (непрочитанные) и «Собеседования» (сегодня); больше 99 — «99+», ноль — нет значка. */
export function navBadge(key: NavKey, c: NavCounters | null): string | null {
  const n = !c ? 0 : key === "telegram" ? c.telegramNew : key === "interviews" ? c.interviewsToday : 0;
  return n <= 0 ? null : n > 99 ? "99+" : String(n);
}

// ---------- Вакансии: статус в адресе, порядок, фильтр ----------

/** Значение ?status= в адресе ↔ статус вакансии. */
export type StatusFilter = "all" | "active" | "pause" | "closed" | "draft";

export const STATUS_OF: Record<Exclude<StatusFilter, "all">, VacancyLike["status"]> = { active: "Open", pause: "Paused", closed: "Closed", draft: "Draft" };

export const parseStatus = (value: string | null | undefined): StatusFilter =>
  value === "active" || value === "pause" || value === "closed" || value === "draft" ? value : "all";

/** Активные → пауза → черновики → закрытые; внутри — недавно изменённые выше. */
const rank: Record<VacancyLike["status"], number> = { Open: 0, Paused: 1, Draft: 2, Closed: 3 };

export function sortVacancies<T extends VacancyLike>(rows: T[]): T[] {
  return [...rows].sort((a, b) => rank[a.status] - rank[b.status] || b.updatedAt.localeCompare(a.updatedAt));
}

/** Поиск — по должности и городу без учёта регистра; отдел — по id (пусто — все). */
export function matchesQuery(v: VacancyLike, q: string, department: string): boolean {
  if (department && v.departmentId !== department) return false;
  const s = q.trim().toLocaleLowerCase("ru");
  return !s || v.title.toLocaleLowerCase("ru").includes(s) || (v.city ?? "").toLocaleLowerCase("ru").includes(s);
}

/** Счётчики кнопок статуса — по уже применённым поиску и отделу. */
export function statusCounts(rows: VacancyLike[]): Record<StatusFilter, number> {
  const counts: Record<StatusFilter, number> = { all: rows.length, active: 0, pause: 0, closed: 0, draft: 0 };
  for (const r of rows) {
    if (r.status === "Open") counts.active++;
    else if (r.status === "Paused") counts.pause++;
    else if (r.status === "Closed") counts.closed++;
    else counts.draft++;
  }
  return counts;
}

export const matchesStatus = (v: VacancyLike, status: StatusFilter) => status === "all" || v.status === STATUS_OF[status];

// ---------- Зарплата ----------

const grouped = (n: number) => Math.round(n).toLocaleString("ru-RU").replace(/ | /g, " ");
const short = (n: number, unit: number) => (Math.round((n / unit) * 10) / 10).toLocaleString("ru-RU", { maximumFractionDigits: 1 });

/** Зарплата для перевода: key с подстановками (t(key, vars)). Нет вилки — «—». */
export type SalaryText = { key: string; vars?: Record<string, string> };

/** Полностью, для таблицы: «6 000 000 – 8 000 000», «от 6 000 000», «до 8 000 000». */
export function salaryFull(from: number | null, to: number | null): SalaryText {
  if (from == null && to == null) return { key: "—" };
  if (from != null && to != null) return { key: from === to ? grouped(from) : `${grouped(from)} – ${grouped(to)}` };
  return from != null ? { key: "от {sum}", vars: { sum: grouped(from) } } : { key: "до {sum}", vars: { sum: grouped(to!) } };
}

/**
 * Коротко, для карточки телефона: «6–8 млн», «4,5–5,5 млн», «100–200 тыс», «от 6 млн», «до 8 млн». Единица — по большему краю: от миллиона —
 * «млн» (500 000 – 1 500 000 → «0,5–1,5 млн»), от тысячи — «тыс», меньше — в сумах.
 */
export function salaryShort(from: number | null, to: number | null): SalaryText {
  if (from == null && to == null) return { key: "—" };
  const top = Math.max(from ?? 0, to ?? 0);
  const [unit, word] = top >= 1_000_000 ? [1_000_000, "млн"] : top >= 1_000 ? [1_000, "тыс"] : [1, "сум"];
  const fmt = (n: number) => (unit === 1 ? grouped(n) : short(n, unit));
  if (from != null && to != null) {
    const range = from === to ? fmt(from) : `${fmt(from)}–${fmt(to)}`;
    return { key: `{v} ${word}`, vars: { v: range } };
  }
  return from != null ? { key: `от {v} ${word}`, vars: { v: fmt(from) } } : { key: `до {v} ${word}`, vars: { v: fmt(to!) } };
}

// ---------- Рекрутер ----------

/** «Дилноза Юсупова» → «ДЮ», «Admin» → «AD»; пусто — «?». */
export function initials(name: string | null): string {
  const words = (name ?? "").trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return "?";
  if (words.length === 1) return words[0].slice(0, 2).toLocaleUpperCase("ru");
  return (words[0][0] + words[1][0]).toLocaleUpperCase("ru");
}

/** «Дилноза Юсупова» → «Дилноза Ю.» (на телефоне); одно слово — как есть. */
export function shortName(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean);
  return words.length < 2 ? name.trim() : `${words[0]} ${words[1][0]}.`;
}

export const AVATAR_TONES = ["violet", "ok", "accent", "warn", "pink", "cyan"] as const;
export type AvatarTone = (typeof AVATAR_TONES)[number];

/** Цвет аватара — по хэшу имени: у одного человека всегда один цвет. */
export function avatarTone(name: string | null): AvatarTone {
  let h = 0;
  for (const ch of name ?? "") h = (h * 31 + ch.codePointAt(0)!) >>> 0;
  return AVATAR_TONES[h % AVATAR_TONES.length];
}

/** «Принято»: доля плана (0…100) и выполнен ли план. */
export function hiredProgress(hired: number, planned: number): { pct: number; done: boolean } {
  if (planned <= 0) return { pct: 0, done: false };
  return { pct: Math.min(100, Math.round((hired / planned) * 100)), done: hired >= planned };
}
