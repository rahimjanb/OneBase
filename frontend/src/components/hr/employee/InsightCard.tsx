"use client";

import { useEffect, useState } from "react";
import { ArrowUpRight, TriangleAlert, X } from "lucide-react";
import { useT } from "@/i18n/client";
import type { Insight } from "@/lib/hr-employee";
import { focus } from "../overview/bits";

/**
 * Подсказка периода (одна, самая важная) — только когда сработало правило. «Проверить график ↗» — Verifix в новой вкладке.
 * Крестик скрывает её для этого сотрудника и периода (запоминается в браузере).
 */
export function InsightCard({ insight, storageKey, verifixUrl }: { insight: Insight; storageKey: string; verifixUrl: string | null }) {
  const t = useT();
  // До чтения localStorage подсказка видна: на сервере неизвестно, скрыта ли она, — иначе разметка сервера и браузера разошлась бы.
  const [hidden, setHidden] = useState(false);
  useEffect(() => {
    try {
      setHidden(localStorage.getItem(storageKey) === "1");
    } catch {
      setHidden(false);
    }
  }, [storageKey]);

  if (hidden) return null;
  const close = () => {
    setHidden(true);
    try {
      localStorage.setItem(storageKey, "1");
    } catch {
      // приватный режим — скрыто до перезагрузки
    }
  };
  const action = insight.type === "unclear_days" ? "Открыть Verifix" : "Проверить график";

  return (
    <section
      role="note"
      aria-label={insight.title}
      className="relative flex flex-col gap-3 rounded-[16px] border border-(--ts-warning)/20 bg-(--ts-warning-soft) px-4 py-4 md:flex-row md:items-center md:gap-4 md:rounded-[14px] md:px-[18px]"
    >
      <div className="flex min-w-0 flex-1 gap-3 md:gap-4">
        <span aria-hidden className="grid size-9 shrink-0 place-items-center rounded-[10px] bg-(--ts-warning)/15 text-(--ts-warning)">
          <TriangleAlert className="size-[18px]" />
        </span>
        <div className="min-w-0 flex-1 pr-7 md:pr-0">
          <h2 className="text-sm font-semibold text-(--ts-text)">{insight.title}</h2>
          <p className="mt-1 text-[13px] leading-relaxed text-(--ts-text-2)">{insight.text}</p>
        </div>
      </div>
      <div className="flex shrink-0 items-center gap-1 max-md:pl-12">
        {verifixUrl && (
          <a href={verifixUrl} target="_blank" rel="noreferrer" className={`inline-flex h-9 items-center gap-1 rounded-[9px] px-2.5 text-[13px] font-semibold text-(--ts-warning) hover:bg-(--ts-warning)/10 max-md:-ml-2.5 ${focus}`}>
            {t(action)}
            <ArrowUpRight aria-hidden className="size-4" />
          </a>
        )}
        <button
          type="button"
          onClick={close}
          aria-label={t("Скрыть подсказку")}
          className={`grid size-8 cursor-pointer place-items-center rounded-[8px] text-(--ts-muted) hover:bg-(--ts-warning)/10 hover:text-(--ts-text-2) max-md:absolute max-md:right-2 max-md:top-2 ${focus}`}
        >
          <X className="size-4" />
        </button>
      </div>
    </section>
  );
}
