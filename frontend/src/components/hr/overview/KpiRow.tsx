"use client";

import Link from "next/link";
import { AlarmClock, Clock3, DoorOpen, Users, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import { qs, type HrOverview } from "@/lib/hr";
import { activeDelta, arrivalDelta, counted, hhmm, lateWeekDelta, mondayOf, num, type Delta } from "@/lib/hr-overview";
import { card, DeltaLine, focus } from "./bits";

type Kpi = { key: string; label: string; short: string; icon: LucideIcon; value: string | null; delta: Delta; deltaShort: Delta; href?: string };

/**
 * Строка KPI: активные сотрудники, средний приход, опоздания за неделю, проходы за день. На ноутбуке — четыре карточки в ряд,
 * на планшете — тоже в ряд, на телефоне — сеткой 2×2 с короткими подписями. Нет значения (средний приход до первого прихода) — «—» без динамики.
 */
export function KpiRow({ o }: { o: HrOverview }) {
  const t = useT();
  const branches = o.locations.filter((l) => l.id != null).length;
  const branchLine: Delta = branches > 0 ? { text: counted(branches, ["филиал", "филиала", "филиалов"], t), tone: "muted" } : null;

  const items: Kpi[] = [
    {
      key: "active",
      label: "Активные сотрудники",
      short: "Активные",
      icon: Users,
      value: num(o.active),
      delta: activeDelta(o, t),
      deltaShort: activeDelta(o, t),
      href: "/hr/employees",
    },
    {
      key: "arrival",
      label: "Средний приход",
      short: "Средний приход",
      icon: Clock3,
      value: o.avgArrival ? hhmm(o.avgArrival) : null,
      delta: arrivalDelta(o, t),
      deltaShort: arrivalDelta(o, t, true),
    },
    {
      key: "late",
      label: "Опозданий за неделю",
      short: "Опозданий / нед.",
      icon: AlarmClock,
      value: num(o.lateWeek),
      delta: lateWeekDelta(o, t),
      deltaShort: lateWeekDelta(o, t, true),
      href: `/hr/deviations${qs({ kind: "late", from: mondayOf(o.date), to: o.date })}`,
    },
    {
      key: "passes",
      label: o.isToday ? "Проходов сегодня" : "Проходов за день",
      short: "Проходов",
      icon: DoorOpen,
      value: num(o.eventsToday),
      delta: branchLine,
      deltaShort: branchLine,
      href: `/hr/attendance${qs({ view: "events", from: o.date, to: o.date })}`,
    },
  ];

  return (
    <ul className="grid grid-cols-2 gap-3 md:grid-cols-4 lg:gap-4">
      {items.map((k) => {
        const body = (
          <>
            <span className="flex items-start justify-between gap-2">
              <span className="truncate text-xs font-medium text-(--ts-muted) lg:text-[13px]">
                <span className="lg:hidden">{t(k.short)}</span>
                <span className="max-lg:hidden">{t(k.label)}</span>
              </span>
              <k.icon aria-hidden className="size-4 shrink-0 text-(--ts-subtle)" strokeWidth={1.75} />
            </span>
            <span className="mt-2.5 block text-2xl font-bold leading-tight tracking-[-0.5px] text-(--ts-text) lg:mt-3 lg:text-[30px]">{k.value ?? "—"}</span>
            <DeltaLine delta={k.deltaShort} className="mt-1.5 text-[11px] lg:hidden" />
            <DeltaLine delta={k.delta} className="mt-2 text-xs max-lg:hidden" />
          </>
        );
        const box = `${card} block h-full min-w-0 p-4 lg:p-5`;
        return (
          <li key={k.key} className="min-w-0">
            {k.href ? (
              <Link href={k.href} className={`${box} transition-colors duration-150 hover:border-(--ts-border-strong) ${focus}`}>
                {body}
              </Link>
            ) : (
              <div className={box}>{body}</div>
            )}
          </li>
        );
      })}
    </ul>
  );
}
