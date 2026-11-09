/**
 * Календарь OneBase: типы /api/calendar и даты недели. Без зависимостей — проверяется node --test (src/lib/my-day.test.mjs).
 * Даты — «YYYY-MM-DD» по Ташкенту, время — «HH:mm» (сервер отдаёт «HH:mm:ss»).
 */

import { formatDate } from "../i18n/core.ts";

export type CalendarKind = "Meeting" | "ReportDue" | "Event";

export type CalendarPerson = { id: string; name: string; position: string | null; department: string | null };

export type CalendarEventView = {
  id: string;
  title: string;
  description: string | null;
  kind: CalendarKind;
  date: string;
  start: string | null;
  end: string | null;
  allDay: boolean;
  location: string | null;
  departmentCode: string | null;
  departmentName: string | null;
  forEveryone: boolean;
  organizer: CalendarPerson | null;
  participants: CalendarPerson[];
  canEdit: boolean;
  /** Событие — отражение записи другого раздела (recruitment — собеседование подбора): меняется только там. */
  managedBy?: string | null;
};

export type CalendarAbilities = { departments: { code: string; name: string }[]; canAddForEveryone: boolean };

export type CalendarEventInput = {
  title: string;
  description: string | null;
  kind: CalendarKind;
  date: string;
  start: string | null;
  end: string | null;
  allDay: boolean;
  location: string | null;
  participants: string[];
  department: string | null;
  forEveryone: boolean;
};

export const KIND_LABELS: Record<CalendarKind, string> = { Meeting: "Встреча", ReportDue: "Срок отчёта", Event: "Важное событие" };

const utc = (iso: string) => Date.UTC(+iso.slice(0, 4), +iso.slice(5, 7) - 1, +iso.slice(8, 10));
const iso = (ms: number) => new Date(ms).toISOString().slice(0, 10);

export function addDays(date: string, days: number): string {
  return iso(utc(date) + days * 86_400_000);
}

/** Понедельник недели даты. */
export function mondayOf(date: string): string {
  const weekday = (new Date(utc(date)).getUTCDay() + 6) % 7;
  return addDays(date, -weekday);
}

/** Неделя: семь дат с понедельника. */
export function weekOf(date: string): string[] {
  const monday = mondayOf(date);
  return Array.from({ length: 7 }, (_, i) => addDays(monday, i));
}

/** «2 – 8 ноября», «27 октября – 2 ноября» — заголовок недели. */
export function weekTitle(monday: string, locale: string): string {
  const sunday = addDays(monday, 6);
  const day = (d: string, month: boolean) =>
    formatDate(new Date(utc(d) + 43_200_000), locale, month ? { day: "numeric", month: "long", timeZone: "UTC" } : { day: "numeric", timeZone: "UTC" });
  const sameMonth = monday.slice(5, 7) === sunday.slice(5, 7);
  return `${day(monday, !sameMonth)} – ${day(sunday, true)}`;
}

/** Корректная дата «YYYY-MM-DD» из адреса или null. */
export function parseDate(value: string | undefined | null): string | null {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return null;
  return iso(utc(value)) === value ? value : null;
}

/** Время для формы и API: «15:00:00» → «15:00»; пусто — null. */
export const timeOf = (value: string | null | undefined) => (value ? value.slice(0, 5) : null);
