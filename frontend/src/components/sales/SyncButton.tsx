"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { RefreshCw } from "lucide-react";
import { progressShort, progressText } from "@/lib/integrations";
import { CancelSyncButton } from "./CancelSyncButton";
import { dateTime } from "@/lib/sales/format";
import type { SyncStatus } from "@/lib/sales/types";

const IDLE_POLL_MS = 60_000;
const RUNNING_POLL_MS = 3_000;

/** Чип «данные по …» — время последней успешной синхронизации с Linko. */
function DataChip({ status }: { status: SyncStatus }) {
  if (!status.configured) {
    return (
      <Link href="/settings/integrations/sales/linko" className="rounded-full bg-warn-soft px-3 py-1 text-xs font-medium text-warn hover:underline">
        Linko не настроен — подключить
      </Link>
    );
  }
  if (status.hasErrors) {
    return (
      <span className="rounded-full bg-warn-soft px-3 py-1 text-xs font-medium text-warn" title="Последняя синхронизация с Linko завершилась с ошибкой — подробности в «Настройках»">
        Синхронизация с ошибкой · данные по {dateTime(status.dataAsOf)}
      </span>
    );
  }
  return <span className="rounded-full border border-line bg-surface px-3 py-1 text-xs tabular-nums text-ink-2 max-lg:py-1.5">данные по {dateTime(status.dataAsOf)}</span>;
}

/**
 * Чип свежести данных и кнопка «Обновить». Статус опрашивается раз в минуту (во время загрузки — каждые 3 с):
 * когда фоновая синхронизация принесла новые данные, страница перечитывается сама, без перезагрузки.
 */
export function SyncControls({ initial }: { initial: SyncStatus }) {
  const router = useRouter();
  const [status, setStatus] = useState(initial);
  const [error, setError] = useState<string | null>(null);
  const seen = useRef(initial.dataAsOf);

  useEffect(() => {
    const timer = setInterval(
      async () => {
        const next = await fetch("/bff/api/sales/status").then((r) => (r.ok ? (r.json() as Promise<SyncStatus>) : null)).catch(() => null);
        if (!next) return;
        setStatus(next);
        if (!next.isRunning && next.dataAsOf !== seen.current) {
          seen.current = next.dataAsOf;
          router.refresh(); // пришли новые данные — перечитать страницу
        }
      },
      status.isRunning ? RUNNING_POLL_MS : IDLE_POLL_MS,
    );
    return () => clearInterval(timer);
  }, [status.isRunning, router]);

  const start = async () => {
    setError(null);
    const response = await fetch("/bff/api/sales/sync", { method: "POST" });
    if (response.status === 403) return setError("Нет прав на обновление");
    if (!response.ok && response.status !== 409) return setError("Не удалось запустить обновление");
    setStatus((s) => ({ ...s, isRunning: true }));
  };

  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      <DataChip status={status} />
      {status.configured && (
        <>
          {status.isRunning && (
            <span className="max-w-64 truncate text-xs tabular-nums text-ink-2" title={progressText(status.progress)}>
              {progressShort(status.progress)}
            </span>
          )}
          {error && <span className="text-xs text-bad">{error}</span>}
          <button
            type="button"
            onClick={start}
            disabled={status.isRunning}
            title="Загрузить свежие данные из Linko"
            className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-xs font-medium text-ink hover:bg-muted disabled:opacity-60 max-lg:h-10 max-lg:text-sm"
          >
            <RefreshCw className={`size-3.5 ${status.isRunning ? "animate-spin" : ""}`} />
            {status.isRunning ? "Обновляется" : "Обновить"}
          </button>
          {status.isRunning && (
            <CancelSyncButton
              size="sm"
              cancelling={status.isCancelling}
              onCancelled={() => setStatus((s) => ({ ...s, isCancelling: true }))}
            />
          )}
        </>
      )}
    </span>
  );
}
