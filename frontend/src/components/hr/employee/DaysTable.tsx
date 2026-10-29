"use client";

import { Fragment, useState } from "react";
import { ArrowRight, Check, ChevronDown } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { clock, duration, trackTypeLabel, weekday, type HrDay, type HrEvent } from "@/lib/hr";
import { dayBadges, dayNote, FILTER_LABEL, isWorkday, leavesNextDay, matchesFilter, scheduleLabel, type Badge, type DayFilter, type PeriodStats } from "@/lib/hr-employee";
import { toneSoft, toneText } from "@/lib/hr-overview";
import { card, focus } from "../overview/bits";

export function DayBadge({ badge }: { badge: Badge }) {
  const t = useT();
  return <span className={`inline-flex h-[22px] items-center whitespace-nowrap rounded-[6px] px-2 text-[11px] font-semibold ${toneSoft[badge.tone]} ${toneText[badge.tone]}`}>{t(badge.label)}</span>;
}

/** «07:25 → 00:35 +1»; нет времени — «—». */
function Times({ d, className = "" }: { d: HrDay; className?: string }) {
  const t = useT();
  const time = (v: string | null) => (v ? <span>{clock(v)}</span> : <span className="text-(--ts-subtle)">—</span>);
  return (
    <span className={`inline-flex items-center gap-1.5 tabular-nums ${className}`}>
      {time(d.firstIn)}
      <ArrowRight aria-label={t("до")} className="size-3 shrink-0 text-(--ts-subtle)" />
      {time(d.lastOut)}
      {leavesNextDay(d) && (
        <span title={t("уход на следующий день")} className="rounded-[4px] bg-(--ts-surface-2) px-1 text-[10px] font-semibold text-(--ts-muted)">
          +1
        </span>
      )}
    </span>
  );
}

/** Отметки дня: время, тип (приход, уход…), где. */
function MarksList({ marks }: { marks: HrEvent[] }) {
  const t = useT();
  if (marks.length === 0) return <p className="text-[13px] text-(--ts-muted)">{t("Отметок нет")}</p>;
  return (
    <ul className="grid gap-x-6 gap-y-1.5 sm:grid-cols-2 xl:grid-cols-3">
      {marks.map((m) => (
        <li key={m.id} className="flex min-w-0 items-baseline gap-2.5 text-[13px]">
          <span className="font-semibold tabular-nums text-(--ts-text)">{clock(m.localTime)}</span>
          <span className={m.trackType === "I" ? "text-(--ts-success)" : m.trackType === "O" ? "text-(--ts-text-2)" : "text-(--ts-muted)"}>
            {t(trackTypeLabel[m.trackType] ?? m.trackType)}
          </span>
          {m.location && <span className="truncate text-(--ts-subtle)">{m.location}</span>}
        </li>
      ))}
    </ul>
  );
}

/**
 * «Посещаемость по дням»: фильтры «Все · С нарушениями · Нужна проверка» (и фильтр из KPI), новые дни сверху. Строка раскрывает отметки дня
 * (мышью, Enter и пробелом); «Итого за период» — по всем дням, независимо от фильтра. На телефоне — список, фильтр и детали дня в шторке.
 */
export function DaysTable({
  days,
  marks,
  stats,
  filter,
  onFilter,
  highlight,
}: {
  days: HrDay[];
  marks: Map<string, HrEvent[]>;
  stats: PeriodStats;
  filter: DayFilter;
  onFilter: (f: DayFilter) => void;
  highlight: string | null;
}) {
  const t = useT();
  const [open, setOpen] = useState<Set<string>>(new Set());
  const [sheetDay, setSheetDay] = useState<HrDay | null>(null);
  const [filtersOpen, setFiltersOpen] = useState(false);

  const sorted = [...days].sort((a, b) => b.date.localeCompare(a.date));
  const rows = sorted.filter((d) => matchesFilter(d, filter));
  const count = (f: DayFilter) => sorted.filter((d) => matchesFilter(d, f)).length;
  const chips: DayFilter[] = ["all", "violations", "unclear", ...(["late", "early", "absent"] as DayFilter[]).filter((f) => f === filter)];
  const marksOf = (d: HrDay) => marks.get(d.date.slice(0, 10)) ?? [];
  const toggle = (date: string) =>
    setOpen((prev) => {
      const next = new Set(prev);
      if (next.has(date)) next.delete(date);
      else next.add(date);
      return next;
    });
  const dash = <span className="text-(--ts-subtle)">—</span>;
  const dateLabel = (d: HrDay) => `${d.date.slice(8, 10)}.${d.date.slice(5, 7)}`;

  return (
    <section aria-labelledby="hr-days" className={`${card} overflow-hidden`}>
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 px-4 py-3.5 md:px-5">
        <h2 id="hr-days" className="text-[15px] font-semibold text-(--ts-text)">
          <span className="md:hidden">{t("По дням")}</span>
          <span className="max-md:hidden">{t("Посещаемость по дням")}</span>
        </h2>
        <div role="radiogroup" aria-label={t("Фильтр дней")} className="flex flex-wrap items-center gap-1 max-md:hidden">
          {chips.map((f) => {
            const on = filter === f;
            return (
              <button
                key={f}
                type="button"
                role="radio"
                aria-checked={on}
                onClick={() => onFilter(f)}
                className={`inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-[9px] border px-3 text-[13px] font-medium transition-colors duration-150 ${focus} ${
                  on ? "border-(--ts-border-strong) bg-(--ts-surface-2) text-(--ts-text)" : "border-transparent text-(--ts-muted) hover:text-(--ts-text-2)"
                }`}
              >
                {t(FILTER_LABEL[f])}
                <span className="tabular-nums text-(--ts-subtle)">{count(f)}</span>
              </button>
            );
          })}
        </div>
        <button
          type="button"
          onClick={() => setFiltersOpen(true)}
          className={`inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-[9px] border border-(--ts-border-strong) bg-(--ts-surface-2) px-3 text-[13px] font-medium text-(--ts-text) md:hidden ${focus}`}
        >
          {t(FILTER_LABEL[filter])} · {count(filter)}
          <ChevronDown aria-hidden className="size-3.5 text-(--ts-muted)" />
        </button>
      </div>

      {rows.length === 0 ? (
        <p className="border-t border-(--ts-border) px-5 py-8 text-center text-sm text-(--ts-muted)">{t("Таких дней за период нет")}</p>
      ) : (
        <>
          {/* Планшет и компьютер: таблица (узкий планшет — с прокруткой вбок). */}
          <div className="overflow-x-auto max-md:hidden">
            <table className="w-full min-w-[720px] table-fixed text-sm [&_td:not(:nth-child(2))]:whitespace-nowrap">
              <colgroup>
                <col className="w-[104px]" />
                <col />
                <col className="w-[148px]" />
                <col className="w-[92px]" />
                <col className="w-[104px]" />
                <col className="w-[116px]" />
                <col className="w-[60px]" />
              </colgroup>
              <thead>
                <tr className="bg-(--ts-surface-2) text-left text-[11px] font-semibold uppercase tracking-[0.5px] text-(--ts-subtle)">
                  <th className="py-2.5 pl-5 pr-2 font-semibold">{t("Дата")}</th>
                  <th className="px-2 py-2.5 font-semibold">{t("Итог")}</th>
                  <th className="px-2 py-2.5 font-semibold">{t("Приход → уход")}</th>
                  <th className="px-2 py-2.5 text-right font-semibold">{t("Опоздание")}</th>
                  <th className="px-2 py-2.5 text-right font-semibold">{t("Ранний уход")}</th>
                  <th className="px-2 py-2.5 text-right font-semibold">{t("Отработано")}</th>
                  <th className="py-2.5 pl-2 pr-5 text-right font-semibold" title={t("Отметок")}>
                    {t("Отм.")}
                  </th>
                </tr>
              </thead>
              <tbody>
                {rows.map((d) => {
                  const date = d.date.slice(0, 10);
                  const expanded = open.has(date);
                  const note = dayNote(d, t);
                  const unclear = d.status === "Unknown";
                  return (
                    <Fragment key={date}>
                      <tr
                        id={`day-${date}`}
                        tabIndex={0}
                        aria-expanded={expanded}
                        onClick={() => toggle(date)}
                        onKeyDown={(e) => {
                          if (e.key === "Enter" || e.key === " ") {
                            e.preventDefault();
                            toggle(date);
                          }
                        }}
                        className={`cursor-pointer scroll-mt-24 border-t border-(--ts-border) align-middle tabular-nums transition-colors duration-500 hover:bg-(--ts-row-hover) focus-visible:bg-(--ts-row-hover) focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-(--ts-accent) ${
                          highlight === date ? "bg-(--ts-accent-soft)!" : unclear ? "bg-(--ts-text)/[0.02]" : ""
                        }`}
                      >
                        <td className="py-3 pl-5 pr-2">
                          <span className="flex items-baseline gap-1.5">
                            <span className="font-semibold text-(--ts-text)">{dateLabel(d)}</span>
                            <span className={`text-xs ${isWorkday(d) ? "text-(--ts-muted)" : "text-(--ts-accent)"}`}>{t(weekday(date))}</span>
                          </span>
                          <span className="block truncate text-[11px] text-(--ts-subtle)">{t(scheduleLabel(d))}</span>
                        </td>
                        <td className="px-2 py-3">
                          <span className="flex flex-wrap gap-1.5">
                            {dayBadges(d).map((b) => (
                              <DayBadge key={b.label} badge={b} />
                            ))}
                          </span>
                          {note && <span className="mt-1 block text-[11px] leading-snug text-(--ts-muted)">{note}</span>}
                        </td>
                        <td className="px-2 py-3 text-sm font-medium text-(--ts-text)">
                          <Times d={d} />
                        </td>
                        <td className="px-2 py-3 text-right font-medium text-(--ts-warning)">{d.late ? duration(d.lateMinutes, t) : dash}</td>
                        <td className="px-2 py-3 text-right font-medium text-(--ts-danger)">{d.earlyLeave ? duration(d.earlyLeaveMinutes, t) : dash}</td>
                        <td className="px-2 py-3 text-right font-semibold text-(--ts-text)">{d.workedMinutes != null ? duration(d.workedMinutes, t) : dash}</td>
                        <td className="py-3 pl-2 pr-5 text-right text-(--ts-muted)">{marksOf(d).length}</td>
                      </tr>
                      {expanded && (
                        <tr className="bg-(--ts-bg)/60">
                          <td colSpan={7} className="px-5 pb-4 pt-1">
                            <p className="mb-2 text-[11px] font-semibold uppercase tracking-[0.5px] text-(--ts-subtle)">{t("Отметки дня")}</p>
                            <MarksList marks={marksOf(d)} />
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  );
                })}
              </tbody>
              <tfoot>
                <tr className="border-t border-(--ts-border) bg-(--ts-surface-2) tabular-nums">
                  <th scope="row" colSpan={3} className="py-3 pl-5 pr-2 text-left text-sm font-semibold text-(--ts-text)">
                    {t("Итого за период")}
                  </th>
                  <td className="px-2 py-3 text-right font-semibold text-(--ts-warning)">{stats.lateMinutes > 0 ? duration(stats.lateMinutes, t) : dash}</td>
                  <td className="px-2 py-3 text-right font-semibold text-(--ts-danger)">{stats.earlyMinutes > 0 ? duration(stats.earlyMinutes, t) : dash}</td>
                  <td className="px-2 py-3 text-right font-semibold text-(--ts-text)">{duration(stats.worked, t)}</td>
                  <td className="py-3 pl-2 pr-5 text-right font-medium text-(--ts-muted)">{stats.marks}</td>
                </tr>
              </tfoot>
            </table>
          </div>

          {/* Телефон: список, детали — в шторке. */}
          <ul className="md:hidden">
            {rows.map((d) => {
              const date = d.date.slice(0, 10);
              const note = dayNote(d, t);
              return (
                <li key={date} id={`day-m-${date}`} className="scroll-mt-20 border-t border-(--ts-border)">
                  <button
                    type="button"
                    onClick={() => setSheetDay(d)}
                    className={`flex w-full cursor-pointer gap-3 px-4 py-3 text-left transition-colors duration-500 ${highlight === date ? "bg-(--ts-accent-soft)" : ""} ${focus}`}
                  >
                    <span className="flex w-[34px] shrink-0 flex-col items-center">
                      <span className="text-[17px] font-bold leading-tight text-(--ts-text)">{Number(date.slice(8, 10))}</span>
                      <span className={`text-[11px] ${isWorkday(d) ? "text-(--ts-subtle)" : "text-(--ts-accent)"}`}>{t(weekday(date))}</span>
                    </span>
                    <span className="flex min-w-0 flex-1 flex-col gap-1">
                      <span className="flex flex-wrap gap-1">
                        {dayBadges(d).map((b) => (
                          <DayBadge key={b.label} badge={b} />
                        ))}
                      </span>
                      <Times d={d} className="text-[13px] text-(--ts-text-2)" />
                      {note && <span className="text-[11px] leading-snug text-(--ts-muted)">{note}</span>}
                    </span>
                    <span className="flex shrink-0 flex-col items-end gap-0.5 tabular-nums">
                      <span className="text-sm font-semibold text-(--ts-text)">{d.workedMinutes != null ? duration(d.workedMinutes, t) : "—"}</span>
                      {d.late && <span className="text-[11px] font-medium text-(--ts-warning)">+{duration(d.lateMinutes, t)}</span>}
                      {d.earlyLeave && <span className="text-[11px] font-medium text-(--ts-danger)">−{duration(d.earlyLeaveMinutes, t)}</span>}
                    </span>
                  </button>
                </li>
              );
            })}
            <li className="flex items-center justify-between gap-3 border-t border-(--ts-border) bg-(--ts-surface-2) px-4 py-3 text-sm tabular-nums">
              <span className="font-semibold text-(--ts-text)">{t("Итого")}</span>
              <span className="flex flex-col items-end">
                <span className="font-semibold text-(--ts-text)">{duration(stats.worked, t)}</span>
                {stats.lateMinutes > 0 && <span className="text-[11px] font-medium text-(--ts-warning)">+{duration(stats.lateMinutes, t)}</span>}
                {stats.earlyMinutes > 0 && <span className="text-[11px] font-medium text-(--ts-danger)">−{duration(stats.earlyMinutes, t)}</span>}
              </span>
            </li>
          </ul>
        </>
      )}

      <Sheet open={filtersOpen} onClose={() => setFiltersOpen(false)} title={t("Какие дни показать")}>
        <ul className="flex flex-col gap-1 pb-2">
          {(["all", "violations", "unclear", "late", "early", "absent"] as DayFilter[]).map((f) => (
            <li key={f}>
              <button
                type="button"
                onClick={() => {
                  onFilter(f);
                  setFiltersOpen(false);
                }}
                className="flex h-12 w-full items-center justify-between rounded-xl px-3 text-left text-[15px] text-ink hover:bg-muted"
              >
                <span>
                  {t(FILTER_LABEL[f])} <span className="text-ink-3">· {count(f)}</span>
                </span>
                {filter === f && <Check className="size-5 text-accent" />}
              </button>
            </li>
          ))}
        </ul>
      </Sheet>

      <Sheet
        open={!!sheetDay}
        onClose={() => setSheetDay(null)}
        title={sheetDay ? `${dateLabel(sheetDay)} · ${t(weekday(sheetDay.date.slice(0, 10)))}` : ""}
        subtitle={sheetDay ? t(scheduleLabel(sheetDay)) : undefined}
      >
        {sheetDay && <DayDetails d={sheetDay} marks={marksOf(sheetDay)} />}
      </Sheet>
    </section>
  );
}

/** Детали дня в шторке (телефон): итог, время, опоздание, ранний уход, отработано и отметки. Шторка — в цветах приложения (портал вне страницы). */
function DayDetails({ d, marks }: { d: HrDay; marks: HrEvent[] }) {
  const t = useT();
  const note = dayNote(d, t);
  const rows: [string, string][] = [
    ["Приход", clock(d.firstIn)],
    ["Уход", d.lastOut ? `${clock(d.lastOut)}${leavesNextDay(d) ? " (+1)" : ""}` : "—"],
    ["Опоздание", d.late ? duration(d.lateMinutes, t) : "—"],
    ["Ранний уход", d.earlyLeave ? duration(d.earlyLeaveMinutes, t) : "—"],
    ["Отработано", d.workedMinutes != null ? duration(d.workedMinutes, t) : "—"],
  ];
  return (
    <div className="flex flex-col gap-4 pb-2">
      <div className="flex flex-wrap gap-1.5">
        {dayBadges(d).map((b) => (
          <span key={b.label} className="rounded-md bg-muted px-2 py-0.5 text-xs font-semibold text-ink-2">
            {t(b.label)}
          </span>
        ))}
      </div>
      {note && <p className="-mt-2 text-sm text-ink-2">{note}</p>}
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
        {rows.map(([k, v]) => (
          <Fragment key={k}>
            <dt className="text-ink-3">{t(k)}</dt>
            <dd className="text-right font-medium tabular-nums text-ink">{v}</dd>
          </Fragment>
        ))}
      </dl>
      <div>
        <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-ink-3">{t("Отметки дня")}</h3>
        {marks.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Отметок нет")}</p>
        ) : (
          <ul className="flex flex-col divide-y divide-line">
            {marks.map((m) => (
              <li key={m.id} className="flex items-baseline gap-3 py-2 text-sm">
                <span className="w-12 font-semibold tabular-nums text-ink">{clock(m.localTime)}</span>
                <span className="text-ink-2">{t(trackTypeLabel[m.trackType] ?? m.trackType)}</span>
                {m.location && <span className="ml-auto truncate text-ink-3">{m.location}</span>}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
