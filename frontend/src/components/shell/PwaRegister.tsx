"use client";

import { useEffect } from "react";

/** Регистрирует service worker (/sw.js): офлайн-заглушка OneBase и офлайн-кэш страниц и данных Sales Base. */
export function PwaRegister() {
  useEffect(() => {
    if (!("serviceWorker" in navigator)) return;
    navigator.serviceWorker.register("/sw.js", { updateViaCache: "none" }).catch(() => {
      // Без service worker приложение работает как обычно — просто не будет страницы «Нет соединения».
    });
  }, []);
  return null;
}
