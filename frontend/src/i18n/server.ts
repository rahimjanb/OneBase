import "server-only";
import { cache } from "react";
import { cookies } from "next/headers";
import { LANG_COOKIE, localeOf, makeT, parseLang, type T } from "./core";
import { uz } from "./uz";

/** Язык интерфейса из cookie (кнопка RU/UZ); без выбора — русский. */
export const getLang = cache(async () => parseLang((await cookies()).get(LANG_COOKIE)?.value));

/** Перевод в серверных компонентах: const t = await getT(); t("Задачи"). */
export async function getT(): Promise<T> {
  return makeT((await getLang()) === "uz" ? uz : null);
}

/** Локаль для дат и чисел в серверных компонентах (месяцы — на языке интерфейса). */
export async function getLocale() {
  return localeOf(await getLang());
}
