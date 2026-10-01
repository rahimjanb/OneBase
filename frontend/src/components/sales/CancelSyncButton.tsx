"use client";

import { useState } from "react";
import { Loader2, X } from "lucide-react";

/** «Отменить» для идущей синхронизации Linko. Загруженное до отмены сохраняется, прерванный шаг повторится со следующей синхронизацией. */
export function CancelSyncButton({
  cancelling,
  onCancelled,
  size = "md",
}: {
  /** Отмена уже запрошена — синхронизация сворачивается. */
  cancelling?: boolean;
  onCancelled?: () => void;
  size?: "sm" | "md";
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const cancel = async () => {
    if (!window.confirm("Отменить синхронизацию с Linko? Уже загруженное сохранится, остальное догрузится при следующей синхронизации.")) return;
    setBusy(true);
    setError(null);
    try {
      const response = await fetch("/bff/api/sales/sync/cancel", { method: "POST" });
      if (response.status === 403) setError("Нет прав на отмену");
      else if (!response.ok && response.status !== 409) setError("Не удалось отменить");
      else onCancelled?.();
    } catch {
      setError("Не удалось отменить");
    } finally {
      setBusy(false);
    }
  };

  const waiting = busy || cancelling;
  const cls =
    size === "sm"
      ? "inline-flex h-8 items-center gap-1.5 rounded-lg border border-bad/30 bg-surface px-3 text-xs font-medium text-bad hover:bg-bad-soft disabled:opacity-60"
      : "inline-flex h-9 items-center gap-2 rounded-lg border border-bad/30 bg-surface px-4 text-sm font-medium text-bad hover:bg-bad-soft disabled:opacity-60";

  return (
    <>
      <button type="button" className={cls} onClick={cancel} disabled={waiting} title="Прервать синхронизацию с Linko">
        {waiting ? <Loader2 className="size-3.5 animate-spin" /> : <X className="size-3.5" />}
        {cancelling ? "Отменяется…" : "Отменить"}
      </button>
      {error && <span className="text-xs text-bad">{error}</span>}
    </>
  );
}
