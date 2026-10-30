import { TIME_ZONE, greeting } from "@/lib/format";

export type HeaderClock = { label: string; greeting: string; night: boolean };

/**
 * «суббота · 24 октября · 23:14» и приветствие по времени компании (Ташкент) для шапки главной; night — вечер и ночь (иконка луны).
 * Общий модуль для сервера (первая отрисовка) и клиента (обновление раз в 30 с).
 */
export function headerClock(date: Date, locale: string): HeaderClock {
  const weekday = new Intl.DateTimeFormat(locale, { weekday: "long", timeZone: TIME_ZONE }).format(date);
  const day = new Intl.DateTimeFormat(locale, { day: "numeric", month: "long", timeZone: TIME_ZONE }).format(date);
  const time = new Intl.DateTimeFormat("ru-RU", { hour: "2-digit", minute: "2-digit", hourCycle: "h23", timeZone: TIME_ZONE }).format(date);
  const hour = Number(new Intl.DateTimeFormat("en-US", { hour: "numeric", hourCycle: "h23", timeZone: TIME_ZONE }).format(date));
  return { label: `${weekday} · ${day} · ${time}`, greeting: greeting(date), night: hour >= 18 || hour < 5 };
}

export type HomeClock = { label: string; greeting: string; night: boolean };

/** Телефон: «пятница · 30 октября» без времени (заглавными — стилем), приветствие и вечер/ночь — как у headerClock. */
export function homeClock(date: Date, locale: string): HomeClock {
  const weekday = new Intl.DateTimeFormat(locale, { weekday: "long", timeZone: TIME_ZONE }).format(date);
  const day = new Intl.DateTimeFormat(locale, { day: "numeric", month: "long", timeZone: TIME_ZONE }).format(date);
  const { greeting, night } = headerClock(date, locale);
  return { label: `${weekday} · ${day}`, greeting, night };
}
