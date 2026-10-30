"use client";

import { usePathname } from "next/navigation";
import { MobileOverviewSkeleton } from "@/components/dashboard/mobile/Skeletons";
import { useT } from "@/i18n/client";

function Block({ className = "" }: { className?: string }) {
  return <div aria-hidden className={`animate-pulse rounded-[14px] bg-muted ${className}`} />;
}

/** «Обзор» на компьютере: шапка, фильтры, «Фокус дня», четыре KPI, график и задачи, отделы. */
export function DesktopOverviewSkeleton() {
  const t = useT();
  return (
    <div role="status" aria-busy="true" aria-label={t("Загрузка")} className="flex flex-col gap-5 px-4 pb-12 pt-6 sm:px-6 lg:px-8 lg:pt-8">
      <div className="flex flex-col gap-2">
        <Block className="h-3.5 w-56 rounded-md" />
        <Block className="h-9 w-80 rounded-lg" />
        <Block className="h-4 w-96 max-w-full rounded-md" />
      </div>
      <Block className="h-[38px] w-[520px] max-w-full rounded-[10px]" />
      <Block className="h-44" />
      <div className="grid grid-cols-2 gap-3 sm:gap-4 xl:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <Block key={i} className="h-[136px]" />
        ))}
      </div>
      <div className="grid grid-cols-1 gap-4 xl:grid-cols-[minmax(0,1fr)_380px]">
        <Block className="h-[380px]" />
        <Block className="h-[380px]" />
      </div>
      <Block className="h-[440px]" />
    </div>
  );
}

/** Любая другая страница: заголовок, ряд плиток и два больших блока — сразу после нажатия, пока сервер собирает страницу. */
function PageSkeleton() {
  const t = useT();
  return (
    <div role="status" aria-busy="true" aria-label={t("Загрузка")} className="mx-auto flex max-w-[1800px] flex-col gap-5 px-4 py-6 sm:px-6 lg:px-8">
      <div className="flex flex-col gap-2.5">
        <Block className="h-8 w-56 rounded-lg" />
        <Block className="h-4 w-80 max-w-full rounded-md" />
      </div>
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <Block key={i} className="h-24" />
        ))}
      </div>
      <Block className="h-64" />
      <Block className="h-48" />
    </div>
  );
}

/**
 * Заготовка страницы на время перехода ((app)/loading.tsx): новая страница открывается сразу, а не через 2–3 секунды ожидания сервера
 * со старой на экране. Для «Обзора» — его собственные блоки (телефон и компьютер), для остальных — общая раскладка. aria-busy держит
 * полоску загрузки (NavProgress), пока не придёт настоящая страница.
 */
export function RouteSkeleton() {
  const t = useT();
  const pathname = usePathname();
  if (pathname === "/") {
    return (
      <>
        <div className="max-md:hidden">
          <DesktopOverviewSkeleton />
        </div>
        <MobileOverviewSkeleton label={t("Загрузка")} />
      </>
    );
  }
  return <PageSkeleton />;
}
