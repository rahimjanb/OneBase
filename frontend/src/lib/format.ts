/** Русское склонение: plural(5, ["файл", "файла", "файлов"]) → "файлов". */
export function plural(n: number, forms: [one: string, few: string, many: string]): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return forms[0];
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return forms[1];
  return forms[2];
}

export const filesLabel = (n: number) => `${n} ${plural(n, ["файл", "файла", "файлов"])}`;
export const foldersLabel = (n: number) => `${n} ${plural(n, ["папка", "папки", "папок"])}`;

export const percent = (value: number, digits = 0) =>
  `${value.toLocaleString("ru-RU", { minimumFractionDigits: digits, maximumFractionDigits: digits })}%`;

export function fileSize(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

/** Часовой пояс компании: по нему считаются дата и приветствие на главной. */
export const TIME_ZONE = "Asia/Tashkent";

/** «суббота · 3 октября» — строка над приветствием на главной. */
export function todayLabel(date = new Date()): string {
  const weekday = new Intl.DateTimeFormat("ru-RU", { weekday: "long", timeZone: TIME_ZONE }).format(date);
  const day = new Intl.DateTimeFormat("ru-RU", { day: "numeric", month: "long", timeZone: TIME_ZONE }).format(date);
  return `${weekday} · ${day}`;
}

/** Приветствие по времени суток компании: утро 5–11, день 12–17, вечер 18–22, ночь — остальное. */
export function greeting(date = new Date()): string {
  const hour = Number(new Intl.DateTimeFormat("en-US", { hour: "numeric", hourCycle: "h23", timeZone: TIME_ZONE }).format(date));
  if (hour >= 5 && hour < 12) return "Доброе утро";
  if (hour >= 12 && hour < 18) return "Добрый день";
  if (hour >= 18 && hour < 23) return "Добрый вечер";
  return "Доброй ночи";
}
