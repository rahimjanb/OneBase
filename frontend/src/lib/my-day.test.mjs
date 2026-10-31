// «Мой день» и календарь (lib/my-day.ts, lib/calendar.ts): node --test src/lib/my-day.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { addDays, mondayOf, parseDate, weekOf, weekTitle } from "./calendar.ts";
import {
  barHeights,
  calendarGroups,
  calendarSubtitle,
  clock,
  dayPill,
  daysBetween,
  duePill,
  fileKind,
  hhmm,
  hm,
  hmLong,
  ringShare,
  taskStatus,
  todayIn,
  weekDelta,
  weekTone,
} from "./my-day.ts";
import { titleCase } from "./users.ts";

// Перевод в тестах — русский текст с подстановками, как у интерфейса без словаря.
const t = (text, vars) => (vars ? text.replace(/\{(\w+)\}/g, (m, k) => (k in vars ? String(vars[k]) : m)) : text);

test("длительности: кольцо, неделя и телефон", () => {
  assert.equal(hm(92, t), "1 ч 32 м");
  assert.equal(hm(45, t), "45 м");
  assert.equal(hmLong(2822, t), "47 ч 2 мин");
  assert.equal(hmLong(480, t), "8 ч");
  assert.equal(hmLong(12, t), "12 мин");
  assert.equal(clock(92), "1:32");
  assert.equal(clock(558), "9:18");
  assert.equal(hhmm("2026-10-31T08:52:00"), "08:52");
  assert.equal(hhmm("15:00:00"), "15:00");
  assert.equal(ringShare(92, 480), 92 / 480);
  assert.equal(ringShare(600, 480), 1);
  assert.equal(ringShare(null, 480), 0);
});

test("сегодня по Ташкенту и разница дат", () => {
  assert.equal(todayIn(new Date("2026-10-31T20:30:00Z")), "2026-11-01");
  assert.equal(daysBetween("2026-10-31", "2026-11-02"), 2);
  assert.equal(daysBetween("2026-11-01", "2026-10-30"), -2);
});

const today = (status, extra = {}) => ({
  status, shiftStart: "2026-10-31T09:00:00", shiftEnd: "2026-10-31T18:00:00", arrival: null, departure: null, lastMarkAt: null, workedMinutes: null,
  plannedMinutes: 480, lateMinutes: null, earlyLeaveMinutes: null, break: null, leaveName: null, reason: null, ...extra,
});

test("пилюля статуса дня: вовремя, опоздание, не начат, выходной", () => {
  assert.deepEqual(dayPill(today("at_work", { lateMinutes: 0 }), t), { label: "На работе · пришли вовремя", tone: "success" });
  assert.deepEqual(dayPill(today("at_work", { lateMinutes: 12 }), t), { label: "Опоздание 12 мин", tone: "warning" });
  assert.deepEqual(dayPill(today("not_started"), t), { label: "Рабочий день не начат", tone: "muted" });
  assert.deepEqual(dayPill(today("day_off"), t), { label: "Выходной", tone: "muted" });
  assert.equal(dayPill(today("out", { lastMarkAt: "2026-10-31T15:10:00" }), t).label, "Вышли в 15:10");
  assert.equal(dayPill(null, t), null);
  // Статус недели — не только цветом: у каждого есть подпись.
  for (const s of ["ok", "late", "early_leave", "absent", "leave", "day_off", "not_started", "future", "unclear"]) assert.ok(weekTone(s).label, s);
});

test("срок задачи: просрочен — красный, сегодня и завтра — жёлтый, позже — дата", () => {
  const due = (dueDate, dueTime = null, overdue = false) => duePill({ dueDate, dueTime, overdue }, "2026-10-31", "ru-RU", t);
  assert.deepEqual(due("2026-10-30", null, true), { label: "просрочено на 1 дн.", tone: "danger" });
  assert.deepEqual(due("2026-10-31", "15:00:00"), { label: "сегодня до 15:00", tone: "warning" });
  assert.deepEqual(due("2026-10-31", "09:00:00", true), { label: "просрочено · было до 09:00", tone: "danger" });
  assert.deepEqual(due("2026-11-01"), { label: "завтра", tone: "warning" });
  assert.deepEqual(due("2026-11-02"), { label: "пн, 2 ноября", tone: "neutral" });
  assert.equal(due(null), null);
});

test("статус задачи: в процессе — warning, ожидает — accent", () => {
  assert.deepEqual(taskStatus({ status: "InProgress", isBlocked: false }), { label: "В процессе", tone: "warning" });
  assert.deepEqual(taskStatus({ status: "Review", isBlocked: false }), { label: "На проверке", tone: "accent" });
  assert.deepEqual(taskStatus({ status: "InProgress", isBlocked: true }), { label: "Заблокирована", tone: "accent" });
  assert.equal(taskStatus({ status: "New", isBlocked: false }).label, "Новая");
});

test("календарь: группы по дням и подписи", () => {
  const item = (date, type, extra = {}) => ({ id: date + type, type, title: "X", date, start: null, end: null, allDay: false, location: null, participants: 0, organizer: null, href: "/", overdue: false, ...extra });
  const groups = calendarGroups([item("2026-11-02", "meeting"), item("2026-10-31", "report_due"), item("2026-11-01", "event"), item("2026-10-31", "task_due")], "2026-10-31", "ru-RU", t);
  assert.deepEqual(groups.map((g) => [g.label, g.items.length]), [["Сегодня", 2], ["Завтра", 1], ["пн, 2 ноября", 1]]);
  assert.equal(calendarSubtitle(item("2026-10-31", "meeting", { start: "11:00:00", end: "11:30:00" }), t), "Встреча · 30 мин");
  assert.equal(calendarSubtitle(item("2026-10-31", "meeting", { start: "10:00:00", end: "11:00:00" }), t), "Встреча · 1 ч");
  assert.equal(calendarSubtitle(item("2026-10-31", "task_due", { overdue: true }), t), "Задача · просрочена");
  assert.equal(calendarSubtitle(item("2026-10-31", "report_due"), t), "Отчёт");
});

test("неделя календаря с понедельника", () => {
  assert.equal(mondayOf("2026-11-01"), "2026-10-26");
  assert.equal(mondayOf("2026-11-02"), "2026-11-02");
  assert.deepEqual(weekOf("2026-11-04"), ["2026-11-02", "2026-11-03", "2026-11-04", "2026-11-05", "2026-11-06", "2026-11-07", "2026-11-08"]);
  assert.equal(addDays("2026-10-31", 1), "2026-11-01");
  assert.equal(weekTitle("2026-11-02", "ru-RU"), "2 – 8 ноября");
  assert.equal(weekTitle("2026-10-26", "ru-RU"), "26 октября – 1 ноября");
  assert.equal(parseDate("2026-02-30"), null);
  assert.equal(parseDate("abc"), null);
  assert.equal(parseDate("2026-11-02"), "2026-11-02");
});

test("документы, динамика и имена из Verifix", () => {
  assert.equal(fileKind("План продаж_ноябрь.xlsx"), "sheet");
  assert.equal(fileKind("Отчёт.pdf"), "pdf");
  assert.equal(fileKind("КП_шаблон.docx"), "doc");
  assert.equal(fileKind("photo", "image/png"), "image");
  assert.deepEqual(weekDelta(6, 4, t), { label: "+2 к прошлой неделе", tone: "success" });
  assert.deepEqual(weekDelta(3, 3, t), { label: "как на прошлой неделе", tone: "muted" });
  assert.deepEqual(barHeights([0, 2, 4]), [8, 50, 100]);
  assert.deepEqual(barHeights([0, 0]), [8, 8]);
  assert.equal(titleCase("BEKBERGANOV"), "Bekberganov");
  assert.equal(titleCase("RAXIMJON XURSANDBEK O'G'LI"), "Raximjon Xursandbek O'g'li");
});
