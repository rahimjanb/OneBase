"use client";

import { useCallback, useSyncExternalStore } from "react";

/** Совпадает ли медиазапрос — с подпиской на изменение (поворот, окно); на сервере и при первой отрисовке — false. */
export function useMedia(query: string): boolean {
  const subscribe = useCallback(
    (onChange: () => void) => {
      const list = window.matchMedia(query);
      list.addEventListener("change", onChange);
      return () => list.removeEventListener("change", onChange);
    },
    [query],
  );
  return useSyncExternalStore(subscribe, () => window.matchMedia(query).matches, () => false);
}

/** Телефон — уже брейкпоинта sm (40rem): там весь текст мельче (globals.css), длинные подсказки укорачиваются. */
export const phoneQuery = "(width < 40rem)";
