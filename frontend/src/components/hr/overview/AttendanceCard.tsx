"use client";

import Link from "next/link";
import { useT } from "@/i18n/client";
import type { HrOverview } from "@/lib/hr";
import { compareLine, counted, dayCounts, num, pct, share, STATUSES, toneDot, type StatusKey } from "@/lib/hr-overview";
import { card, DeltaLine, focus, Tip } from "./bits";
import layout from "./overview.module.css";

/**
 * «Явка сегодня»: на месте (вовремя + опоздали) от «по графику», сравнение с прошлым рабочим днём, полоса из шести итогов дня
 * (нулевые не рисуются) и легенда — каждая колонка открывает «Посещаемость» с этим статусом.
 */
export function AttendanceCard({ o, attendanceHref }: { o: HrOverview; attendanceHref: (status: string) => string }) {
  const t = useT();
  const counts = dayCounts(o);
  const present = counts.onTime + counts.late;
  const presentPct = share(present, o.scheduled);
  const statuses = STATUSES.filter((s) => o.isToday || s.key !== "expected");
  const segments = statuses.filter((s) => counts[s.key] > 0);

  const hint: Record<StatusKey, string | null> = {
    onTime: o.scheduled > 0 ? t("{pct} от графика", { pct: pct(share(counts.onTime, o.scheduled) ?? 0, 0) }) : null,
    late: o.avgLateMinutes != null ? t("в среднем +{n} мин", { n: o.avgLateMinutes }) : null,
    absent: t("без документа"),
    byDocument: t("отпуск, больничный"),
    expected: t("смена позже"),
    unclear: t("мало отметок"),
  };

  const title = o.isToday ? t("Явка сегодня") : t("Явка за день");
  const label = statuses.map((s) => `${t(s.label)}: ${counts[s.key]}`).join(", ");
  const breakdown = [
    o.dayOff > 0 && t("выходной по графику: {n}", { n: o.dayOff }),
    o.noSchedule > 0 && t("без графика в Verifix: {n}", { n: o.noSchedule }),
  ].filter(Boolean);

  return (
    <section aria-labelledby="hr-att" className={`${card} @container flex h-full flex-col gap-5 p-5 lg:gap-[22px] lg:px-6 lg:py-[26px]`}>
      <div className="flex items-baseline justify-between gap-3">
        <h2 id="hr-att" className="text-base font-semibold text-(--ts-text)">
          {title}
        </h2>
        <span className="text-[13px] text-(--ts-muted)" title={breakdown.length ? breakdown.join(" · ") : undefined}>
          <span className="lg:hidden">{t("из {n}", { n: num(o.scheduled) })}</span>
          <span className="max-lg:hidden">
            {counted(o.scheduled, ["сотрудник", "сотрудника", "сотрудников"], t)} {t("по графику")}
          </span>
        </span>
      </div>

      <div className="flex items-end gap-3 lg:gap-4">
        <span className="text-[48px] font-bold leading-none tracking-[-1.5px] text-(--ts-text) lg:text-[56px]">{num(present)}</span>
        <div className="flex min-w-0 flex-col gap-0.5 pb-0.5">
          <span className="text-[15px] font-medium text-(--ts-text-2) lg:text-base">
            {t("на месте")}
            {presentPct != null && <> · {pct(presentPct)}</>}
          </span>
          <DeltaLine delta={compareLine(o, present, t)} className="text-xs max-lg:hidden" />
          <DeltaLine delta={compareLine(o, present, t, true)} className="text-xs lg:hidden" />
        </div>
      </div>

      <div role="img" aria-label={label} className="flex h-3 gap-[3px] lg:h-3.5">
        {segments.length === 0 ? (
          <span className="h-full flex-1 rounded-[7px] bg-(--ts-surface-2)" />
        ) : (
          segments.map((s) => (
            <span key={s.key} className="group/tip relative h-full min-w-1.5 first:rounded-l-[7px] last:rounded-r-[7px]" style={{ flex: `${counts[s.key]} 1 0` }}>
              <span aria-hidden className={`block h-full rounded-[inherit] ${toneDot[s.tone]}`} />
              <Tip>
                {t(s.label)}: {num(counts[s.key])} ({pct(share(counts[s.key], o.scheduled) ?? 0)})
              </Tip>
            </span>
          ))
        )}
      </div>

      <ul className={layout.legend}>
        {statuses.map((s) => (
          <li key={s.key}>
            <Link
              href={attendanceHref(s.filter)}
              className={`group flex items-center gap-2 rounded-[8px] py-3 max-lg:justify-between lg:flex-col lg:items-stretch lg:gap-1.5 lg:py-0 ${focus}`}
            >
              <span className="flex min-w-0 items-center gap-2">
                <span aria-hidden className={`size-2 shrink-0 rounded-full ${toneDot[s.tone]}`} />
                <span className="truncate text-[13px] font-medium text-(--ts-muted) group-hover:text-(--ts-text-2) lg:text-xs">
                  <span className="lg:hidden">{t(s.short)}</span>
                  <span className="max-lg:hidden">{t(s.label)}</span>
                </span>
              </span>
              <span className="text-[15px] font-semibold text-(--ts-text) lg:text-2xl lg:font-bold">{num(counts[s.key])}</span>
              {hint[s.key] && <span className="truncate text-[11px] text-(--ts-subtle) max-lg:hidden">{hint[s.key]}</span>}
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
