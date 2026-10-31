"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ChevronLeft, ChevronRight, Plus } from "lucide-react";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { useT } from "@/i18n/client";
import { addDays, weekTitle, type CalendarAbilities } from "@/lib/calendar";
import { CALENDAR_TYPES, calendarSubtitle, dayLabel, hhmm, weekdayCell, type CalendarItem } from "@/lib/my-day";
import { EventDialog, EventForm } from "./EventDialogs";

const bar: Record<string, string> = { accent: "bg-accent", warning: "bg-warn", violet: "bg-violet", pink: "bg-[#f472b6]" };

const button =
  "inline-flex min-h-[44px] items-center justify-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-[13px] font-semibold text-ink-2 transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent lg:min-h-9";

/**
 * Календарь: неделя по дням (на компьютере — семь колонок, на телефоне — список дней). Событие открывается окном с подробностями;
 * «Новое событие» и «+» у дня — форма. Сроки задач ведут в карточку задачи. Переход по неделям — адрес ?date, данные — с сервера.
 */
export function CalendarView({
  week,
  today,
  items,
  abilities,
  openEvent,
  locale,
}: {
  week: string[];
  today: string;
  items: CalendarItem[] | null;
  abilities: CalendarAbilities | null;
  openEvent: string | null;
  locale: string;
}) {
  const t = useT();
  const router = useRouter();
  const [viewing, setViewing] = useState<string | null>(openEvent);
  const [creating, setCreating] = useState<string | null>(null);

  const go = (date: string) => router.push(`/calendar?date=${date}`);
  const byDay = (date: string) => (items ?? []).filter((i) => i.date === date);
  const legend = (["meeting", "report_due", "event", "task_due"] as const).map((type) => ({ type, ...CALENDAR_TYPES[type] }));

  return (
    <div className="me-ui min-h-full">
      <PageHeader
        title={t("Календарь")}
        subtitle={t("Встречи, сроки отчётов, важные события и сроки ваших задач")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Календарь" }]}
        actions={
          <button type="button" onClick={() => setCreating(today >= week[0] && today <= week[6] ? today : week[0])} className={`${button} border-transparent bg-accent text-white hover:bg-accent`}>
            <Plus aria-hidden className="size-4" />
            {t("Новое событие")}
          </button>
        }
      />
      <PageBody>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <button type="button" onClick={() => go(addDays(week[0], -7))} aria-label={t("Предыдущая неделя")} className={`${button} w-11 px-0 lg:w-9`}>
              <ChevronLeft aria-hidden className="size-4" />
            </button>
            <button type="button" onClick={() => go(addDays(week[0], 7))} aria-label={t("Следующая неделя")} className={`${button} w-11 px-0 lg:w-9`}>
              <ChevronRight aria-hidden className="size-4" />
            </button>
            <h2 className="ml-1 text-[17px] font-semibold text-ink">{weekTitle(week[0], locale)}</h2>
            {!(today >= week[0] && today <= week[6]) && (
              <button type="button" onClick={() => go(today)} className={`${button} ml-1`}>
                {t("Сегодня")}
              </button>
            )}
          </div>
          <ul className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[12.5px] text-ink-3">
            {legend.map((x) => (
              <li key={x.type} className="flex items-center gap-1.5">
                <span aria-hidden className={`h-3 w-[3px] rounded-full ${bar[x.bar]}`} />
                {t(x.label)}
              </li>
            ))}
          </ul>
        </div>

        {items === null ? (
          <p role="alert" className="mt-5 rounded-xl border border-line bg-surface p-4 text-[14px] text-ink-3">
            {t("Не удалось загрузить календарь.")}{" "}
            <button type="button" onClick={() => router.refresh()} className="font-semibold text-link">
              {t("Повторить")}
            </button>
          </p>
        ) : (
          <ol className="mt-5 grid gap-3 lg:grid-cols-7 lg:gap-2.5">
            {week.map((date) => {
              const list = byDay(date);
              const isToday = date === today;
              return (
                <li
                  key={date}
                  aria-current={isToday ? "date" : undefined}
                  className={`flex min-w-0 flex-col rounded-2xl border bg-surface p-3 lg:min-h-[340px] ${isToday ? "border-accent/60" : "border-line"}`}
                >
                  <div className="flex items-center justify-between gap-2">
                    <h3 className={`min-w-0 truncate text-[12px] font-semibold uppercase tracking-[0.6px] ${isToday ? "text-link" : "text-ink-3"}`}>
                      {/* Колонка на компьютере узкая — «ПН 2» (месяц — в заголовке недели), на телефоне — «пн, 2 ноября». */}
                      <span className="lg:hidden">{isToday ? `${t("Сегодня")} · ${dayLabel(date, locale)}` : dayLabel(date, locale)}</span>
                      <span className="hidden lg:inline">
                        {weekdayCell(date, locale).weekday} {Number(date.slice(8, 10))}
                        {isToday ? ` · ${t("сегодня")}` : ""}
                      </span>
                    </h3>
                    <button
                      type="button"
                      onClick={() => setCreating(date)}
                      aria-label={t("Новое событие на {day}", { day: dayLabel(date, locale) })}
                      className="grid size-9 shrink-0 place-items-center rounded-lg text-ink-3 hover:bg-surface-2 hover:text-ink focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent lg:size-7"
                    >
                      <Plus aria-hidden className="size-4" />
                    </button>
                  </div>
                  {list.length === 0 ? (
                    <p className="mt-2 text-[12.5px] text-ink-4 lg:mt-3">{t("Нет событий")}</p>
                  ) : (
                    <ul className="mt-2 space-y-1.5">
                      {list.map((item) => {
                        const kind = CALENDAR_TYPES[item.type];
                        const body = (
                          <>
                            <span aria-hidden className={`w-[3px] shrink-0 self-stretch rounded-full ${bar[kind.bar]}`} />
                            <span className="min-w-0">
                              <span className="block text-[12px] font-medium text-ink-3">{item.allDay || !item.start ? t("весь день") : `${hhmm(item.start)}${item.end ? `–${hhmm(item.end)}` : ""}`}</span>
                              <span className="block text-[13.5px] font-medium leading-snug text-ink lg:line-clamp-2">
                                {item.type === "task_due" ? t("Срок: {title}", { title: item.title }) : item.title}
                              </span>
                              <span className={`block truncate text-[11.5px] ${item.overdue ? "text-bad" : "text-ink-3"}`}>{calendarSubtitle(item, t)}</span>
                            </span>
                          </>
                        );
                        const cls =
                          "flex w-full gap-2.5 rounded-lg bg-surface-2/60 px-2.5 py-2 text-left transition-colors hover:bg-surface-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent";
                        return (
                          <li key={`${item.type}-${item.id}`}>
                            {item.type === "task_due" ? (
                              <Link href={item.href} className={cls}>
                                {body}
                              </Link>
                            ) : (
                              <button type="button" onClick={() => setViewing(item.id)} className={cls}>
                                {body}
                              </button>
                            )}
                          </li>
                        );
                      })}
                    </ul>
                  )}
                </li>
              );
            })}
          </ol>
        )}
      </PageBody>

      {viewing && <EventDialog id={viewing} locale={locale} abilities={abilities} onClose={() => setViewing(null)} />}
      {creating && <EventForm date={creating} abilities={abilities} onClose={() => setCreating(null)} />}
    </div>
  );
}
