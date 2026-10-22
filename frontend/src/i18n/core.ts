/**
 * Языки интерфейса: русский (исходный) и узбекский латиницей. Ключ перевода — сам русский текст: t("Задачи") → «Vazifalar».
 * Нет перевода — остаётся русский, поэтому непереведённое место видно, но ничего не ломается. Подстановки — {имя}:
 * t("Осталось {n} дн.", { n: 3 }). Язык хранится в cookie onebase_lang (выбор кнопкой RU/UZ), по умолчанию — русский.
 * Модуль общий для сервера и браузера (без server-only).
 */

export type Lang = "ru" | "uz";
export type Dict = Record<string, string>;
export type Vars = Record<string, string | number>;
export type T = (text: string, vars?: Vars) => string;

export const LANG_COOKIE = "onebase_lang";

export const LANGS: { code: Lang; label: string; short: string }[] = [
  { code: "ru", label: "Русский", short: "RU" },
  { code: "uz", label: "O'zbekcha", short: "UZ" },
];

export const parseLang = (value: string | null | undefined): Lang => (value === "uz" ? "uz" : "ru");

/** Локаль для дат и чисел: месяцы и дни недели — на языке интерфейса. */
export const localeOf = (lang: Lang) => (lang === "uz" ? "uz-Latn-UZ" : "ru-RU");

/**
 * Контекст — когда одно русское слово в разных местах значит разное: t("файл::Имя") → по-русски «Имя», в словаре — свой перевод
 * («Nomi», а у человека «Имя» — «Ism»). По-русски выводится часть после «::».
 */
export function translate(dict: Dict | null, text: string, vars?: Vars): string {
  const context = text.indexOf("::");
  const value = (dict && dict[text]) || (dict && !vars && byPattern(dict, text)) || (context >= 0 ? text.slice(context + 2) : text);
  return vars ? value.replace(/\{(\w+)\}/g, (match, key: string) => (key in vars ? String(vars[key]) : match)) : value;
}

type Pattern = { re: RegExp; names: string[]; value: string; anchor: string };
const patterns = new WeakMap<Dict, Pattern[]>();

/**
 * Готовый текст с подставленными значениями — так приходят тексты сервера («данные по 19.10», «Вопрос — от 1 до 4000 символов.»):
 * ищется ключ-шаблон («данные по {date}»), значения переносятся в перевод; значение, которое само есть в словаре, тоже переводится.
 * Шаблон без длинного постоянного куска («{n} {unit}») не используется — иначе он узнавал бы что угодно, в том числе данные.
 */
function byPattern(dict: Dict, text: string): string | null {
  if (text.length > 600 || !/[А-Яа-яЁё]/.test(text)) return null;
  let list = patterns.get(dict);
  if (!list) {
    list = [];
    for (const [key, value] of Object.entries(dict)) {
      if (!key.includes("{")) continue;
      const parts = key.split(/\{(\w+)\}/);
      if (parts.length < 3) continue;
      const names: string[] = [];
      let source = "^";
      let anchor = "";
      parts.forEach((part, i) => {
        if (i % 2) {
          names.push(part);
          source += "(.+?)";
        } else {
          source += part.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
          if (part.trim().length > anchor.trim().length) anchor = part;
        }
      });
      if (anchor.trim().length >= 4) list.push({ re: new RegExp(`${source}$`, "s"), names, value, anchor });
    }
    patterns.set(dict, list);
  }
  for (const p of list) {
    if (!text.includes(p.anchor)) continue;
    const match = p.re.exec(text);
    if (!match) continue;
    const found: Record<string, string> = {};
    p.names.forEach((name, i) => (found[name] = dict[match[i + 1]] ?? match[i + 1]));
    return p.value.replace(/\{(\w+)\}/g, (all, key: string) => found[key] ?? all);
  }
  return null;
}

export const makeT = (dict: Dict | null): T => (text, vars) => translate(dict, text, vars);

const uzMonths = ["yanvar", "fevral", "mart", "aprel", "may", "iyun", "iyul", "avgust", "sentabr", "oktabr", "noyabr", "dekabr"];
const uzMonthsShort = ["yan", "fev", "mar", "apr", "may", "iyn", "iyl", "avg", "sen", "okt", "noy", "dek"];
const uzWeekdays = ["yakshanba", "dushanba", "seshanba", "chorshanba", "payshanba", "juma", "shanba"];
const uzWeekdaysShort = ["Ya", "Du", "Se", "Ch", "Pa", "Ju", "Sh"];
const weekdayIndex: Record<string, number> = { Sun: 0, Mon: 1, Tue: 2, Wed: 3, Thu: 4, Fri: 5, Sat: 6 };

/**
 * Дата на языке интерфейса — вместо toLocaleDateString / Intl.DateTimeFormat с локалью из useLocale / getLocale.
 * Узбекский собирается здесь: в Chrome узбекских данных Intl нет («M10 18», «Sun»), а на сервере (Node) есть — сервер и браузер
 * рисовали бы разное. Формы — как у Node: «18-oktabr», «18-okt», «oktabr, 2026», «dushanba, 18-oktabr», «14:05». Русский — Intl.
 */
export function formatDate(date: Date, locale: string, options?: Intl.DateTimeFormatOptions): string {
  if (!locale.startsWith("uz")) return new Intl.DateTimeFormat(locale, options).format(date);

  const o = options ?? { day: "2-digit", month: "2-digit", year: "numeric" };
  // Части даты в нужном часовом поясе — цифрами, без названий.
  const parts: Record<string, string> = {};
  for (const p of new Intl.DateTimeFormat("en-US", {
    timeZone: o.timeZone,
    year: "numeric",
    month: "numeric",
    day: "numeric",
    weekday: "short",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(date)) parts[p.type] = p.value;
  const month = Number(parts.month) - 1;
  const pad = (v: string) => v.padStart(2, "0");

  const text: string[] = [];
  if (o.weekday) text.push((o.weekday === "long" ? uzWeekdays : uzWeekdaysShort)[weekdayIndex[parts.weekday] ?? 0]);
  if (o.month === "long" || o.month === "short") {
    const name = (o.month === "long" ? uzMonths : uzMonthsShort)[month];
    const core = o.day ? `${Number(parts.day)}-${name}` : name;
    text.push(o.year ? `${core}, ${parts.year}` : core);
  } else if (o.day || o.month || o.year) {
    text.push([o.day && pad(parts.day), o.month && pad(parts.month), o.year && parts.year].filter(Boolean).join("."));
  }
  const date_ = text.join(", ");
  const time = o.hour || o.minute ? `${pad(parts.hour === "24" ? "00" : parts.hour)}:${pad(parts.minute)}` : "";
  return [date_, time].filter(Boolean).join(" ");
}
