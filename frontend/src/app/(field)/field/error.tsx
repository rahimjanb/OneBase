"use client";

import { useT } from "@/i18n/client";

/** Ошибка страницы Sales Base: понятный текст и повтор, без потери оболочки. */
// retry (Next 16.3) заново запрашивает страницу с сервера; reset только сбросил бы ошибку и показал тот же ответ.
export default function FieldError({ error, retry }: { error: Error & { digest?: string }; retry: () => void }) {
  const t = useT();
  const offline = typeof navigator !== "undefined" && !navigator.onLine;
  return (
    <div className="mx-auto max-w-lg px-4 pt-10 text-center">
      <h1 className="text-lg font-semibold text-ink">{offline ? t("Нет связи") : t("Не удалось загрузить страницу")}</h1>
      <p className="mt-2 text-sm text-ink-2">
        {offline ? t("Проверьте интернет. Уже открытые страницы маршрута и точек доступны из кэша.") : t("Сервер OneBase ответил ошибкой. Попробуйте ещё раз.")}
      </p>
      {error.digest && <p className="mt-1 text-xs text-ink-3">{t("Код: {code}", { code: error.digest })}</p>}
      <button type="button" onClick={() => retry()} className="mt-5 inline-flex h-11 items-center rounded-lg bg-accent px-5 text-sm font-semibold text-white">
        {t("Повторить")}
      </button>
    </div>
  );
}
