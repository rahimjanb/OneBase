import ts from "@/components/hr/timesheet/timesheet.module.css";
import m from "./mobile.module.css";
import { card, Sk } from "./ui";

/**
 * Заготовки блоков мобильного «Обзора» тех же размеров, что сами блоки: пока идут данные, вёрстка не прыгает. aria-busy — признак
 * загрузки для полоски перехода (NavProgress) и восстановления прокрутки (ScrollManager).
 */
function Busy({ label, className = "", children }: { label: string; className?: string; children: React.ReactNode }) {
  return (
    <div role="status" aria-busy="true" aria-label={label} className={className}>
      {children}
    </div>
  );
}

export function FocusSkeleton({ label }: { label: string }) {
  return (
    <Busy label={label} className={`${card} ${m.focus} pb-[6px] pt-[18px]`}>
      <div className="flex items-center gap-[12px] px-[18px] pb-[14px]">
        <Sk className="size-[40px] rounded-[12px]" />
        <div className="flex flex-1 flex-col gap-[8px]">
          <Sk className="h-[16px] w-[120px]" />
          <Sk className="h-[12px] w-[180px]" />
        </div>
      </div>
      {[0, 1, 2].map((i) => (
        <div key={i} className="flex items-center gap-[12px] border-t border-(--mh-row) px-[18px] py-[13px]">
          <Sk className="size-[46px] rounded-[12px]" />
          <div className="flex flex-1 flex-col gap-[8px]">
            <Sk className="h-[11px] w-[110px]" />
            <Sk className="h-[14px] w-full" />
          </div>
        </div>
      ))}
    </Busy>
  );
}

export function TasksSkeleton({ label }: { label: string }) {
  return (
    <section className="flex flex-col gap-[12px]">
      <div className="flex items-center justify-between px-[4px]">
        <Sk className="h-[18px] w-[80px]" />
        <Sk className="h-[14px] w-[64px]" />
      </div>
      <Busy label={label} className={`${card} p-[18px]`}>
        <div className="flex items-center gap-[20px]">
          <Sk className="size-[120px] shrink-0 rounded-full" />
          <div className="flex flex-1 flex-col gap-[14px]">
            {[0, 1, 2, 3].map((i) => (
              <Sk key={i} className="h-[14px] w-full" />
            ))}
          </div>
        </div>
        <Sk className="mt-[16px] h-[44px] w-full rounded-[12px]" />
      </Busy>
    </section>
  );
}

export function DepartmentsSkeleton({ label }: { label: string }) {
  return (
    <section className="flex flex-col gap-[12px]">
      <div className="flex items-center justify-between px-[4px]">
        <Sk className="h-[18px] w-[150px]" />
        <Sk className="h-[14px] w-[40px]" />
      </div>
      <Busy label={label} className={card}>
        {[0, 1, 2, 3, 4, 5].map((i) => (
          <div key={i} className={`flex flex-col gap-[10px] px-[16px] py-[14px] ${i > 0 ? "border-t border-(--ts-border)" : ""}`}>
            <div className="flex items-center gap-[12px]">
              <Sk className="size-[38px] rounded-[11px]" />
              <div className="flex flex-1 flex-col gap-[7px]">
                <Sk className="h-[14px] w-[110px]" />
                <Sk className="h-[11px] w-[150px]" />
              </div>
              <Sk className="h-[30px] w-[52px]" />
            </div>
            <Sk className="h-[6px] w-full rounded-full" />
          </div>
        ))}
      </Busy>
    </section>
  );
}

export function GreetingSkeleton({ label }: { label: string }) {
  return (
    <Busy label={label} className="flex flex-col gap-[10px] px-[4px]">
      <Sk className="h-[12px] w-[150px]" />
      <Sk className="h-[28px] w-[230px]" />
      <Sk className="h-[14px] w-[260px]" />
    </Busy>
  );
}

export function KpiSkeleton({ label }: { label: string }) {
  return (
    <Busy label={label} className={`${card} ${m.kpi} rounded-[22px] p-[20px]`}>
      <div className="flex justify-between gap-[12px]">
        <div className="flex flex-col gap-[10px]">
          <Sk className="h-[13px] w-[130px]" />
          <Sk className="h-[44px] w-[150px]" />
          <Sk className="h-[20px] w-[180px]" />
        </div>
        <Sk className="size-[72px] rounded-full" />
      </div>
      <Sk className="mt-[36px] h-[48px] w-full" />
    </Busy>
  );
}

export function DynamicsSkeleton({ label }: { label: string }) {
  return (
    <section className="flex flex-col gap-[12px]">
      <div className="flex items-center justify-between px-[4px]">
        <Sk className="h-[18px] w-[110px]" />
        <Sk className="h-[14px] w-[80px]" />
      </div>
      <Busy label={label} className={`${card} flex flex-col gap-[16px] p-[18px]`}>
        <Sk className="h-[40px] w-full rounded-[12px]" />
        <Sk className="h-[40px] w-full" />
        <div className="flex h-[130px] items-end gap-[5px]">
          {[45, 60, 52, 70, 64, 80, 72, 86, 66, 90, 84, 76].map((v, i) => (
            <Sk key={i} className="flex-1 rounded-b-none rounded-t-[5px]" style={{ height: `${v}%` }} />
          ))}
        </div>
      </Busy>
    </section>
  );
}

/** Вся страница целиком — пока открывается «Обзор» (loading.tsx): шапка и нижняя панель уже на месте. */
export function MobileOverviewSkeleton({ label }: { label: string }) {
  return (
    <div data-mobile-home className={`${ts.root} ${m.page} md:hidden`}>
      <div className="flex flex-col gap-[20px] px-[16px] pb-[40px] pt-[8px]">
        <GreetingSkeleton label={label} />
        <div className="flex gap-[8px] overflow-hidden">
          {[96, 128, 150].map((w) => (
            <Sk key={w} className="h-[34px] shrink-0 rounded-[20px]" style={{ width: w }} />
          ))}
        </div>
        <KpiSkeleton label={label} />
        <div className="grid grid-cols-4 gap-[8px]">
          {[0, 1, 2, 3].map((i) => (
            <Sk key={i} className="h-[86px] rounded-[16px]" />
          ))}
        </div>
        <FocusSkeleton label={label} />
        <TasksSkeleton label={label} />
      </div>
    </div>
  );
}
