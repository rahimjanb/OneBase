import { BlockSkeleton, Sk, card } from "./parts";

/**
 * Заготовки «Моего дня»: те же рамки, что у блоков, на месте данных — мерцающие плашки. Общие для страницы (Suspense каждого блока) и
 * макета перехода (RouteSkeleton): по нажатию «Главная» сотрудник сразу видит свой день, данные приходят следом.
 */

export function WorkdaySkeleton({ label }: { label: string }) {
  return (
    <div role="status" aria-busy="true" aria-label={label} className={card}>
      <Sk className="h-5 w-40" />
      <div className="mt-5 flex items-center gap-6">
        <Sk className="size-[96px] shrink-0 rounded-full lg:size-[120px]" />
        <div className="grid flex-1 grid-cols-2 gap-4 lg:grid-cols-4">
          {Array.from({ length: 4 }, (_, i) => (
            <Sk key={i} className="h-12 w-full" />
          ))}
        </div>
      </div>
      <Sk className="mt-5 h-4 w-32" />
      <div className="mt-3 grid grid-cols-7 gap-2">
        {Array.from({ length: 7 }, (_, i) => (
          <Sk key={i} className="h-14 w-full lg:h-[76px]" />
        ))}
      </div>
    </div>
  );
}

export function TasksSkeleton({ label }: { label: string }) {
  return (
    <div role="status" aria-busy="true" aria-label={label} className={card}>
      <Sk className="h-5 w-32" />
      <div className="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-5">
        {Array.from({ length: 5 }, (_, i) => (
          <Sk key={i} className={`h-[52px] w-full lg:h-[84px] ${i === 0 ? "hidden lg:block" : ""}`} />
        ))}
      </div>
      <Sk className="mt-5 h-4 w-40" />
      <div className="mt-3 space-y-2">
        {Array.from({ length: 3 }, (_, i) => (
          <Sk key={i} className="h-12 w-full" />
        ))}
      </div>
    </div>
  );
}

/** Вся страница: шапка, левая колонка (рабочий день, задачи, эффективность), правая (AI, календарь, уведомления, документы). */
export function MyDaySkeleton({ label }: { label: string }) {
  return (
    <div data-mobile-home className="me-ui min-h-full">
      <div className="mx-auto flex max-w-[1440px] flex-col gap-4 px-4 pb-10 pt-2 sm:px-6 lg:gap-5 lg:px-8 lg:pb-12 lg:pt-8">
        <div className="flex items-center gap-3 lg:gap-4">
          <Sk className="size-[46px] shrink-0 rounded-full lg:size-[60px]" />
          <div className="flex-1 space-y-2">
            <Sk className="hidden h-3 w-56 lg:block" />
            <Sk className="h-6 w-60 lg:h-8 lg:w-80" />
            <Sk className="h-3.5 w-48" />
          </div>
        </div>
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-[minmax(0,1fr)_400px] lg:items-start lg:gap-5">
          <div className="flex min-w-0 flex-col gap-4 lg:gap-5">
            <WorkdaySkeleton label={label} />
            <TasksSkeleton label={label} />
          </div>
          <div className="hidden min-w-0 flex-col gap-5 lg:flex">
            <BlockSkeleton rows={3} label={label} />
            <BlockSkeleton rows={4} label={label} />
          </div>
        </div>
      </div>
    </div>
  );
}
