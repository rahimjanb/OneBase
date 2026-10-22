"use client";

import { createContext, useContext, useMemo } from "react";
import { LANG_COOKIE, localeOf, makeT, type Lang, type T, type Vars } from "./core";
import { uz } from "./uz";

const LangContext = createContext<Lang>("ru");

/** Язык интерфейса для клиентских компонентов — из корневого layout (cookie onebase_lang). */
export function I18nProvider({ lang, children }: { lang: Lang; children: React.ReactNode }) {
  return <LangContext.Provider value={lang}>{children}</LangContext.Provider>;
}

export const useLang = () => useContext(LangContext);

/** Перевод в клиентских компонентах: const t = useT(); t("Задачи"). */
export function useT(): T {
  const lang = useLang();
  return useMemo(() => makeT(lang === "uz" ? uz : null), [lang]);
}

/** Локаль для дат и чисел (месяцы — на языке интерфейса). */
export const useLocale = () => localeOf(useLang());

/**
 * Текст на языке интерфейса в разметке компонента, который рендерится и на сервере, и в браузере (без "use client" и без async):
 * <Tr>Назад</Tr>, <Tr vars={{ n }}>{"Осталось {n} дн."}</Tr>.
 */
export function Tr({ children, vars }: { children: string; vars?: Vars }) {
  return <>{useT()(children, vars)}</>;
}

/**
 * Перевод вне компонентов, в коде браузера (ошибки сети в lib/bff и т. п.): язык — из <html lang>, который ставит корневой layout.
 * В компонентах — useT.
 */
export function tNow(text: string, vars?: Vars): string {
  const uzbek = typeof document !== "undefined" && document.documentElement.lang === "uz";
  return makeT(uzbek ? uz : null)(text, vars);
}

/** Сменить язык: cookie на год и перезагрузка — сервер и браузер сразу рисуют страницу на новом языке. */
export function setLang(lang: Lang) {
  document.cookie = `${LANG_COOKIE}=${lang}; path=/; max-age=${60 * 60 * 24 * 365}; samesite=lax`;
  window.location.reload();
}
