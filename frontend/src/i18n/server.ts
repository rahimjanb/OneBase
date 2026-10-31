import "server-only";
import { cache } from "react";
import { cookies } from "next/headers";
import { LANG_COOKIE, localeOf, makeT, parseLang, type T } from "./core";
import { uz } from "./uz";
import { uzCyrl } from "./uzCyrl";

/** Язык интерфейса из cookie (кнопка RU / UZ / ЎЗ); без выбора — русский. */
export const getLang = cache(async () => parseLang((await cookies()).get(LANG_COOKIE)?.value));

/** Перевод в серверных компонентах: const t = await getT(); t("Задачи"). */
export async function getT(): Promise<T> {
  const lang = await getLang();
  return makeT(lang === "uz" ? uz : lang === "uz-Cyrl" ? uzCyrl : null);
}

/** Локаль для дат и чисел в серверных компонентах (месяцы — на языке интерфейса). */
export async function getLocale() {
  return localeOf(await getLang());
}
