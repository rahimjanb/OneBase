// Числа в разделе «Продажи»: разделитель тысяч — пробел, крупные суммы сокращаются, пусто — «—».

const ru = (digits: number) =>
  new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 0, maximumFractionDigits: digits });

export const DASH = "—";

export function num(value: number | null | undefined, digits = 0): string {
  return value == null || Number.isNaN(value) ? DASH : ru(digits).format(value);
}

/** Килограммы: до 100 — с одним знаком, дальше целые. */
export function kg(value: number | null | undefined): string {
  return value == null ? DASH : num(value, Math.abs(value) < 100 ? 1 : 0);
}

/** Сокращённые суммы: «11,32 млрд», «412,1 млн», «874 тыс». */
export function money(value: number | null | undefined): string {
  if (value == null || Number.isNaN(value)) return DASH;
  const abs = Math.abs(value);
  if (abs >= 1e9) return `${ru(2).format(value / 1e9)} млрд`;
  if (abs >= 1e6) return `${ru(1).format(value / 1e6)} млн`;
  if (abs >= 1e3) return `${ru(0).format(value / 1e3)} тыс`;
  return ru(0).format(value);
}

/** Доля 0..1 → «86%» или «86,4%». */
export function pct(share: number | null | undefined, digits = 0): string {
  return share == null || Number.isNaN(share) ? DASH : `${ru(digits).format(share * 100)}%`;
}

/** Изменение в долях → «+ 8%» / «− 12%». */
export function delta(share: number | null | undefined): string {
  if (share == null) return DASH;
  const value = ru(0).format(Math.abs(share * 100));
  return share >= 0 ? `+ ${value}%` : `− ${value}%`;
}

export function dateTime(iso: string | null | undefined): string {
  if (!iso) return DASH;
  return new Date(iso).toLocaleString("ru-RU", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

export function date(iso: string | null | undefined): string {
  if (!iso) return DASH;
  const [y, m, d] = iso.slice(0, 10).split("-");
  return `${d}.${m}.${y}`;
}

const MONTHS = ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];
const MONTHS_SHORT = ["Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек"];
const MONTHS_GENITIVE = ["января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря"];

export const monthName = (m: number) => MONTHS[m - 1];
export const monthShort = (m: number) => MONTHS_SHORT[m - 1];
export const monthLabel = (year: number, m: number) => `${MONTHS[m - 1][0].toUpperCase()}${MONTHS[m - 1].slice(1)} ${year}`;
export const monthGenitive = (m: number) => MONTHS_GENITIVE[m - 1];

/** Значение ячейки для копирования в Excel (TSV): число с запятой, без разделителей тысяч. */
export function tsvValue(value: string | number | null | undefined): string {
  if (value == null) return "";
  if (typeof value === "number") return String(Math.round(value * 1000) / 1000).replace(".", ",");
  return value.replace(/[\t\n]/g, " ");
}
