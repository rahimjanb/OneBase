// Числа в разделе «Продажи»: разделитель тысяч — пробел, крупные суммы сокращаются, пусто — «—».

import { makeT, type T } from "@/i18n/core";
import { plural } from "@/lib/format";
import type { PlanKind } from "./types";

export { plural };

const ru = (digits: number) =>
  new Intl.NumberFormat("ru-RU", { minimumFractionDigits: 0, maximumFractionDigits: digits });

export const DASH = "—";

/** Без перевода (русский) — если вызывающий не передал t. Единицы, подписи и месяцы переводит t из useT() / getT(). */
const plain: T = makeT(null);

export function num(value: number | null | undefined, digits = 0): string {
  return value == null || Number.isNaN(value) ? DASH : ru(digits).format(value);
}

/** Килограммы: до 100 — с одним знаком, дальше целые. */
export function kg(value: number | null | undefined): string {
  return value == null ? DASH : num(value, Math.abs(value) < 100 ? 1 : 0);
}

/** Сокращённые суммы: «11,32 млрд», «412,1 млн», «874 тыс». t — перевод единиц (без него — русские). */
export function money(value: number | null | undefined, t: T = plain): string {
  if (value == null || Number.isNaN(value)) return DASH;
  const abs = Math.abs(value);
  if (abs >= 1e9) return `${ru(2).format(value / 1e9)} ${t("млрд")}`;
  if (abs >= 1e6) return `${ru(1).format(value / 1e6)} ${t("млн")}`;
  if (abs >= 1e3) return `${ru(0).format(value / 1e3)} ${t("тыс")}`;
  return ru(0).format(value);
}

/** Доля 0..1 → «86%» или «86,4%». */
export function pct(share: number | null | undefined, digits = 0): string {
  return share == null || Number.isNaN(share) ? DASH : `${ru(digits).format(share * 100)}%`;
}

/** «1 заказ», «3 заказа», «12 заказов». */
export const ordersLabel = (n: number, t: T = plain) => `${num(n)} ${t(plural(n, ["заказ", "заказа", "заказов"]))}`;

/** «1 товар», «3 товара», «12 товаров». */
export const productsLabel = (n: number, t: T = plain) => `${num(n)} ${t(plural(n, ["товар", "товара", "товаров"]))}`;

/** «1 регион», «3 региона», «12 регионов». */
export const regionsLabel = (n: number, t: T = plain) => `${num(n)} ${t(plural(n, ["регион", "региона", "регионов"]))}`;

/** «1 точка», «3 точки», «12 точек». */
export const outletsLabel = (n: number, t: T = plain) => `${num(n)} ${t(plural(n, ["точка", "точки", "точек"]))}`;

/** «1 строка», «3 строки», «12 строк». */
export const rowsLabel = (n: number, t: T = plain) => `${num(n)} ${t(plural(n, ["строка", "строки", "строк"]))}`;

/** «плана РОП на этот месяц нет» / «плана «Завод» на этот месяц нет» — когда выбранного плана регионов на месяц нет. */
export const planMissingText = (plan: PlanKind, t: T = plain) => t(`плана ${plan === "factory" ? "«Завод»" : "РОП"} на этот месяц нет`);

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

/** «2026-10-05» → «05.10». */
export function dayMonth(iso: string | null | undefined): string {
  if (!iso) return DASH;
  const [, m, d] = iso.slice(0, 10).split("-");
  return `${d}.${m}`;
}

const MONTHS = ["январь", "февраль", "март", "апрель", "май", "июнь", "июль", "август", "сентябрь", "октябрь", "ноябрь", "декабрь"];
const MONTHS_SHORT = ["Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек"];
const MONTHS_GENITIVE = ["января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря"];

// Названия месяцев — по словарю языка интерфейса: t из useT() / getT(); без t — русские.
export const monthName = (m: number, t: T = plain) => t(MONTHS[m - 1]);
export const monthShort = (m: number, t: T = plain) => t(MONTHS_SHORT[m - 1]);
export const monthLabel = (year: number, m: number, t: T = plain) => {
  const name = t(MONTHS[m - 1]);
  return `${name[0].toUpperCase()}${name.slice(1)} ${year}`;
};
export const monthGenitive = (m: number, t: T = plain) => t(MONTHS_GENITIVE[m - 1]);

/** Значение ячейки для копирования в Excel (TSV): число с запятой, без разделителей тысяч. */
export function tsvValue(value: string | number | null | undefined): string {
  if (value == null) return "";
  if (typeof value === "number") return String(Math.round(value * 1000) / 1000).replace(".", ",");
  return value.replace(/[\t\n]/g, " ");
}
