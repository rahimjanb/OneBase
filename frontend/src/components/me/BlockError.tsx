"use client";

import { useTransition } from "react";
import { useRouter } from "next/navigation";
import { RefreshCw } from "lucide-react";
import { useT } from "@/i18n/client";
import { card, focusRing } from "./parts";

/** Ошибка одного блока: остальные блоки на месте, «Повторить» перезапрашивает данные страницы. */
export function BlockError({ title, onRetry }: { title: string; onRetry?: () => void }) {
  const t = useT();
  const router = useRouter();
  const [pending, start] = useTransition();
  return (
    <div role="alert" className={`${card} flex items-center justify-between gap-3`}>
      <div className="min-w-0">
        <p className="text-[15px] font-semibold text-ink">{t(title)}</p>
        <p className="mt-0.5 text-[13px] text-ink-3">{t("Не удалось загрузить данные.")}</p>
      </div>
      <button
        type="button"
        onClick={() => (onRetry ? onRetry() : start(() => router.refresh()))}
        disabled={pending}
        className={`inline-flex min-h-[44px] shrink-0 items-center gap-1.5 rounded-lg border border-line px-3 text-[13px] font-semibold text-ink-2 hover:bg-surface-2 disabled:opacity-60 lg:min-h-9 ${focusRing}`}
      >
        <RefreshCw aria-hidden className={`size-4 ${pending ? "animate-spin" : ""}`} />
        {t("Повторить")}
      </button>
    </div>
  );
}
