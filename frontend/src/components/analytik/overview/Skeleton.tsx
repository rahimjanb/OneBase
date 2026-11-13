"use client";

import { useT } from "@/i18n/client";
import { Bone } from "./parts";
import { AskBar } from "./top";

/**
 * Загрузка «Обзора» Аналитика: макет страницы скелетонами — шапка, период, кольцо (серый трек без дуги), 12 показателей, график (серая
 * область), риски и нижний ряд; строка вопроса AI-аналитику — настоящая, работает сразу. Палитра — как у страницы.
 */
export function AnalytikOverviewSkeleton() {
  const t = useT();
  const card = "rounded-[18px] border border-line bg-surface p-5 max-md:p-4";
  return (
    <div data-palette="sales-secondary" data-glow="both" className="min-h-full" aria-busy="true" aria-label={t("Загрузка")}>
      <div className="border-b border-line max-lg:hidden">
        <div className="mx-auto flex max-w-[1800px] items-end justify-between gap-6 px-6 pb-5 pt-5">
          <div className="space-y-2.5">
            <Bone className="h-3 w-40" />
            <p className="text-[28px] font-semibold leading-tight text-ink">{t("Аналитик")}</p>
            <Bone className="h-3.5 w-[420px]" />
          </div>
          <div className="flex gap-2.5">
            <Bone className="h-9 w-44" />
            <Bone className="h-9 w-28" />
            <Bone className="h-9 w-28" />
          </div>
        </div>
      </div>
      <div className="@container mx-auto max-w-[1800px] space-y-4 px-4 pb-10 pt-[18px] sm:px-6 max-md:space-y-3.5 max-md:pt-3.5">
        <div className="flex items-center gap-2.5 lg:hidden">
          <Bone className="size-9 rounded-[10px]" />
          <div className="space-y-1.5">
            <p className="text-[17px] font-bold leading-tight text-ink">{t("Аналитик")}</p>
            <Bone className="h-2.5 w-28" />
          </div>
        </div>
        <Bone className="h-[42px] w-full max-w-[640px] rounded-xl" />
        {/* Вопрос аналитику работает и до загрузки данных. */}
        <AskBar chips={[]} periodLabel={null} />
        <div className="grid gap-4 @min-[1100px]:grid-cols-[300px_minmax(0,1fr)] @min-[1340px]:grid-cols-[340px_minmax(0,1fr)]">
          <div className={card}>
            <Bone className="h-4 w-40" />
            <div className="mt-5 flex items-center gap-4">
              <svg viewBox="0 0 100 100" className="size-[112px] shrink-0 max-md:size-[92px]" aria-hidden>
                <circle cx="50" cy="50" r="44" fill="none" stroke="var(--color-muted)" strokeWidth="8" />
              </svg>
              <div className="flex-1 space-y-2">
                <Bone className="h-5 w-28" />
                <Bone className="h-3 w-full" />
                <Bone className="h-3 w-3/4" />
              </div>
            </div>
            <div className="mt-5 space-y-3">
              {[0, 1, 2, 3].map((i) => (
                <Bone key={i} className="h-5 w-full" />
              ))}
            </div>
          </div>
          <div className={card}>
            <Bone className="h-4 w-48" />
            <div className="mt-4 grid grid-cols-2 gap-2.5 lg:grid-cols-3 lg:@min-[900px]:grid-cols-4">
              {Array.from({ length: 12 }, (_, i) => (
                <Bone key={i} className={`h-[104px] rounded-[14px] max-md:h-[96px] ${i >= 8 ? "max-md:hidden" : ""}`} />
              ))}
            </div>
          </div>
        </div>
        <div className="grid gap-4 @min-[1100px]:grid-cols-[minmax(0,1fr)_400px] @min-[1340px]:grid-cols-[minmax(0,1fr)_460px]">
          <div className={card}>
            <Bone className="h-4 w-72 max-md:w-40" />
            <Bone className="mt-5 h-[250px] w-full max-md:h-[150px]" />
          </div>
          <div className={card}>
            <Bone className="h-4 w-44" />
            <div className="mt-4 space-y-2">
              {[0, 1, 2, 3].map((i) => (
                <Bone key={i} className={`h-[58px] w-full rounded-xl ${i === 3 ? "max-md:hidden" : ""}`} />
              ))}
            </div>
          </div>
        </div>
        <div className="grid gap-4 lg:grid-cols-2 @min-[1100px]:grid-cols-3">
          {[0, 1, 2].map((i) => (
            <div key={i} className={`${card} ${i < 2 ? "max-md:hidden" : ""} ${i === 2 ? "lg:@max-[1099px]:col-span-2" : ""}`}>
              <Bone className="h-4 w-52" />
              <Bone className="mt-5 h-[220px] w-full" />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
