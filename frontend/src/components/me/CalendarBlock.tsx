import Link from "next/link";
import { CalendarDays } from "lucide-react";
import type { T } from "@/i18n/core";
import { CALENDAR_TYPES, calendarGroups, calendarSubtitle, hhmm, type CalendarItem } from "@/lib/my-day";
import { BlockError } from "./BlockError";
import { BlockHeader, Empty, card, focusRing } from "./parts";

/** Цвет полосы: встреча — accent, отчёт — warning, задача — violet, событие — pink. */
const bar: Record<string, string> = { accent: "bg-accent", warning: "bg-warn", violet: "bg-violet", pink: "bg-[#f472b6]" };

/**
 * «Календарь»: сегодня и неделя вперёд — встречи, сроки отчётов, важные события и сроки моих задач, по дням («СЕГОДНЯ», «ПН, 2 НОЯБРЯ»).
 * Строка: время (или «весь день»), цветная полоса по виду, название и подпись. На телефоне — два ближайших дня с событиями.
 */
export async function CalendarBlock({ data, today, locale, t }: { data: Promise<CalendarItem[] | null>; today: string; locale: string; t: T }) {
  const items = await data;
  if (!items) return <BlockError title="Календарь" />;
  const groups = calendarGroups(items, today, locale, t);

  return (
    <section aria-label={t("Календарь")} className={card}>
      <BlockHeader icon={CalendarDays} title={t("Календарь")} href="/calendar" action={t("Открыть")} />
      {groups.length === 0 ? (
        <div className="mt-4">
          <Empty>{t("На ближайшую неделю событий нет.")}</Empty>
        </div>
      ) : (
        <div className="mt-3 space-y-3">
          {groups.map((group, i) => (
            <div key={group.date} className={i >= 2 ? "max-lg:hidden" : undefined}>
              <h3 className="text-[11px] font-semibold uppercase tracking-[0.8px] text-ink-3">{group.label}</h3>
              <ul className="mt-1.5">
                {group.items.slice(0, 4).map((item) => {
                  const kind = CALENDAR_TYPES[item.type];
                  return (
                    <li key={`${item.type}-${item.id}`}>
                      <Link
                        href={item.href}
                        className={`-mx-2 flex min-h-[48px] items-center gap-3 rounded-lg px-2 py-1.5 transition-colors hover:bg-surface-2 ${focusRing}`}
                      >
                        <span className="w-[58px] shrink-0 text-[13px] font-medium text-ink-2">{item.allDay || !item.start ? t("весь день") : hhmm(item.start)}</span>
                        <span aria-hidden className={`h-9 w-[3px] shrink-0 rounded-full ${bar[kind.bar]}`} />
                        <span className="min-w-0">
                          <span className="block truncate text-[14px] font-medium text-ink">
                            {item.type === "task_due" ? t("Срок: {title}", { title: item.title }) : item.title}
                          </span>
                          <span className={`block truncate text-[12px] ${item.overdue ? "text-bad" : "text-ink-3"}`}>{calendarSubtitle(item, t)}</span>
                        </span>
                      </Link>
                    </li>
                  );
                })}
              </ul>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
