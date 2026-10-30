"use client";

import { useEffect } from "react";

/**
 * Стоит рядом со страницей внутри границы ожидания корневого layout: эффект срабатывает, когда страница пришла с сервера и React её
 * оживил, — заставка (Splash) уходит. Если буквы логотипа ещё собираются, она сама дождётся конца сборки.
 */
export function SplashReady() {
  useEffect(() => {
    (window as Window & { hideSplash?: () => void }).hideSplash?.();
  }, []);
  return null;
}
