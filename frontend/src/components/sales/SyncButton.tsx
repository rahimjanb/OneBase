"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { RefreshCw } from "lucide-react";
import { progressText } from "@/lib/integrations";
import type { SyncStatus } from "@/lib/sales/types";

/** Кнопка «Обновить» в шапке продаж: запускает синхронизацию с Linko и показывает, что сейчас загружается. */
export function SyncButton({ initial }: { initial: SyncStatus }) {
  const router = useRouter();
  const [status, setStatus] = useState(initial);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!status.isRunning) return;
    const timer = setInterval(async () => {
      const next = await fetch("/bff/api/sales/status").then((r) => (r.ok ? (r.json() as Promise<SyncStatus>) : null)).catch(() => null);
      if (!next) return;
      setStatus(next);
      if (!next.isRunning) router.refresh(); // загрузка закончилась — пересчитать страницу
    }, 3000);
    return () => clearInterval(timer);
  }, [status.isRunning, router]);

  if (!status.configured) return null;

  const start = async () => {
    setError(null);
    const response = await fetch("/bff/api/sales/sync", { method: "POST" });
    if (response.status === 403) return setError("Нет прав на обновление");
    if (!response.ok && response.status !== 409) return setError("Не удалось запустить обновление");
    setStatus((s) => ({ ...s, isRunning: true }));
  };

  return (
    <span className="inline-flex items-center gap-2">
      {status.isRunning && <span className="max-w-64 truncate text-xs text-ink-2">{progressText(status.progress)}</span>}
      {error && <span className="text-xs text-bad">{error}</span>}
      <button
        type="button"
        onClick={start}
        disabled={status.isRunning}
        title="Загрузить свежие данные из Linko"
        className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-xs font-medium text-ink hover:bg-muted disabled:opacity-60"
      >
        <RefreshCw className={`size-3.5 ${status.isRunning ? "animate-spin" : ""}`} />
        {status.isRunning ? "Обновляется" : "Обновить"}
      </button>
    </span>
  );
}
