"use client";

/** Общая страница ошибки приложения — вместо сырого стека. */
export default function AppError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <div className="mx-auto max-w-[1800px] px-4 py-16 sm:px-6">
      <div className="max-w-lg rounded-xl border border-bad/30 bg-bad-soft p-6">
        <h1 className="text-lg font-semibold text-ink">Не удалось загрузить страницу</h1>
        <p className="mt-2 text-sm text-ink-2">Сервер OneBase не ответил или вернул ошибку. Попробуйте ещё раз через несколько секунд.</p>
        {error.digest && <p className="mt-2 text-xs text-ink-3">Код ошибки: {error.digest}</p>}
        <button type="button" onClick={reset} className="mt-4 h-9 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong max-lg:h-11">
          Попробовать снова
        </button>
      </div>
    </div>
  );
}
