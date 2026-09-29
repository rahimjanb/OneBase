"use client";

import { useEffect, useState } from "react";
import { dateTime } from "@/lib/sales/format";
import type { SyncStatus } from "@/lib/sales/types";

/** Понятное сообщение, если расчёт или API упали, — с датой последних удачных данных. */
export default function SalesError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  const [status, setStatus] = useState<SyncStatus | null>(null);

  useEffect(() => {
    fetch("/bff/api/sales/status")
      .then((r) => (r.ok ? r.json() : null))
      .then(setStatus)
      .catch(() => setStatus(null));
  }, []);

  return (
    <div className="mx-auto max-w-[1240px] px-4 py-10 sm:px-8">
      <div className="max-w-xl rounded-xl border border-bad/30 bg-bad-soft p-6">
        <h1 className="text-lg font-semibold text-ink">Не удалось загрузить аналитику продаж</h1>
        <p className="mt-2 text-sm text-ink-2">
          Сервер OneBase не ответил или вернул ошибку. Данные в системе при этом сохранены
          {status?.dataAsOf ? ` — последние удачные данные Linko по ${dateTime(status.dataAsOf)}` : ""}.
        </p>
        {status?.hasErrors && (
          <p className="mt-2 text-sm text-ink-2">Последняя синхронизация с Linko завершилась с ошибкой — подробности в «Настройки → Интеграции → Продажи → Linko».</p>
        )}
        {error.digest && <p className="mt-2 text-xs text-ink-3">Код ошибки: {error.digest}</p>}
        <button type="button" onClick={reset} className="mt-4 h-9 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong">
          Попробовать снова
        </button>
      </div>
    </div>
  );
}
