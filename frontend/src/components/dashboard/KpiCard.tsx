import { AlarmClock, Gauge, ListChecks, Plus, Target, TrendingDown, TrendingUp, type LucideIcon } from "lucide-react";
import type { Kpi } from "@/lib/dashboard.mock";
import type { T } from "@/i18n/core";
import { DashCard, Skeleton, toneBar, toneBg, toneSoft, toneText } from "./blocks";

const icons: Record<Kpi["icon"], LucideIcon> = { gauge: Gauge, target: Target, tasks: ListChecks, alarm: AlarmClock };
const changeIcons: Record<Kpi["change"]["icon"], LucideIcon> = { up: TrendingUp, down: TrendingDown, plus: Plus };

/** Карточка показателя: название и плитка иконки, значение и мини-бар-чарт из 12 столбиков, чип изменения с подписью. */
export function KpiCard({ kpi, t }: { kpi: Kpi; t: T }) {
  const Icon = icons[kpi.icon];
  const ChangeIcon = changeIcons[kpi.change.icon];
  return (
    <DashCard className="flex flex-col gap-3 p-5">
      <div className="flex items-center justify-between gap-2">
        <span className="text-[13px] font-medium text-ink-3">{t(kpi.label)}</span>
        <span className={`grid size-7 place-items-center rounded-lg ${toneSoft[kpi.tone]} ${toneText[kpi.tone]}`}>
          <Icon className="size-[15px]" strokeWidth={1.75} />
        </span>
      </div>
      <div className="flex items-end justify-between gap-3">
        <span className="text-[34px] font-semibold leading-none tracking-[-1px] text-ink tabular-nums">{kpi.value}</span>
        <span className="flex h-11 items-end gap-[3px]" aria-hidden>
          {kpi.bars.map((v, i) => (
            <span key={i} className={`w-[5px] rounded-sm ${i === kpi.bars.length - 1 ? toneBg[kpi.tone] : toneBar[kpi.tone]}`} style={{ height: v / 2 }} />
          ))}
        </span>
      </div>
      <div className="flex items-center gap-2 text-[11.5px]">
        <span className={`inline-flex items-center gap-1 rounded-[5px] px-1.5 py-0.5 font-semibold ${toneSoft[kpi.change.tone]} ${toneText[kpi.change.tone]}`}>
          <ChangeIcon className="size-3" strokeWidth={2} />
          {kpi.change.text}
        </span>
        <span className="text-ink-4">{t(kpi.change.hint)}</span>
      </div>
    </DashCard>
  );
}

export function KpiCardSkeleton() {
  return (
    <DashCard className="flex flex-col gap-3 p-5">
      <Skeleton className="h-4 w-28" />
      <Skeleton className="h-9 w-24" />
      <Skeleton className="h-4 w-40" />
    </DashCard>
  );
}
