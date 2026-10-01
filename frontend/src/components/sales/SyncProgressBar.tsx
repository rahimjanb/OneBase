import { progressText, remainingText } from "@/lib/integrations";
import type { SyncProgress } from "@/lib/sales/types";

/**
 * Ход синхронизации коротко: полоса, «42% выполнено · осталось ~3 мин» и текущий шаг
 * («Шаг 9 из 24 · Заказы · История · 12 000 из 30 000 строк»).
 */
export function SyncProgressBar({ progress }: { progress: SyncProgress | null }) {
  const planned = progress?.stepsTotal ? progress : null;
  const percent = planned?.percent ?? 0;
  const remaining = remainingText(progress);
  return (
    <div className="w-full">
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 text-sm">
        <span className="font-semibold tabular-nums text-ink">{planned ? `${percent}% выполнено` : "Подготовка…"}</span>
        <span className="text-xs text-ink-2">{remaining ?? (planned ? "оцениваю, сколько осталось…" : "")}</span>
      </div>
      <div className="mt-1.5 h-2 w-full overflow-hidden rounded-full bg-muted">
        <div className="h-full rounded-full bg-accent transition-[width] duration-700" style={{ width: `${Math.max(2, percent)}%` }} />
      </div>
      <div className="mt-1.5 text-xs text-ink-3">
        {planned ? `Шаг ${Math.min((planned.stepsDone ?? 0) + 1, planned.stepsTotal ?? 1)} из ${planned.stepsTotal} · ` : ""}
        {progressText(progress)}
      </div>
    </div>
  );
}
