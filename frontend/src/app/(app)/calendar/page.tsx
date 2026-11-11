import { CalendarView } from "@/components/calendar/CalendarView";
import { getLocale, getT } from "@/i18n/server";
import { parseDate, weekOf, type CalendarAbilities } from "@/lib/calendar";
import type { CalendarItem } from "@/lib/my-day";
import { todayIn } from "@/lib/my-day";
import { apiTry } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Календарь · OneBase") };
}

/**
 * Календарь: неделя (с понедельника) — встречи, сроки отчётов, важные события и сроки моих задач. ?date — любой день недели,
 * ?event — открыть событие (ссылка из уведомления или «Моего дня»). Что видно и что можно менять — решает сервер; собеседование с кандидатом
 * в форме — тем, кто ведёт подбор.
 */
export default async function CalendarPage({ searchParams }: { searchParams: Promise<{ date?: string; event?: string }> }) {
  const params = await searchParams;
  const today = todayIn(new Date());
  const week = weekOf(parseDate(params.date) ?? today);
  const [items, abilities, recruitment, locale] = await Promise.all([
    apiTry<CalendarItem[]>(`/api/calendar?from=${week[0]}&to=${week[6]}`),
    apiTry<CalendarAbilities>("/api/calendar/abilities"),
    apiTry<{ canManage: boolean }>("/api/hr/recruitment/context"),
    getLocale(),
  ]);
  return (
    <CalendarView
      week={week}
      today={today}
      items={items}
      abilities={abilities}
      openEvent={params.event ?? null}
      locale={locale}
      canInterview={recruitment?.canManage ?? false}
    />
  );
}
