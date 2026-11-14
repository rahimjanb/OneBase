// HR → «Подбор» (lib/recruitment-view.ts): node --test src/lib/recruitment-view.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import {
  avatarTone, hiredProgress, initials, matchesQuery, matchesStatus, navBadge, navCaption, parseStatus, salaryFull, salaryShort, shortName, sortVacancies,
  statusCounts,
} from "./recruitment-view.ts";

const counters = { openVacancies: 4, candidatesInWork: 48, telegramNew: 5, activePublications: 0, interviewsToday: 120, openExceptions: 4, hhConnected: false };

test("подписи кнопок: число, ноль — «нет …» цветом subtle, HH — подключён или нет, пока грузится — null", () => {
  assert.deepEqual(navCaption("vacancies", counters), { key: "{n} открыто", vars: { n: 4 } });
  assert.deepEqual(navCaption("candidates", counters), { key: "{n} в работе", vars: { n: 48 } });
  assert.deepEqual(navCaption("publications", counters), { key: "нет активных", tone: "zero" });
  assert.deepEqual(navCaption("hh", counters), { key: "не подключён", tone: "warn" });
  assert.deepEqual(navCaption("hh", { ...counters, hhConnected: true }), { key: "подключён" });
  assert.deepEqual(navCaption("analytics", null), { key: "воронка" });
  assert.deepEqual(navCaption("screening", null), { key: "рейтинг AI" });
  assert.deepEqual(navCaption("settings", null), { key: "этапы" });
  assert.equal(navCaption("telegram", null), null);
});

test("значок на плитке телефона — только Telegram и собеседования, больше 99 — «99+»", () => {
  assert.equal(navBadge("telegram", counters), "5");
  assert.equal(navBadge("interviews", counters), "99+");
  assert.equal(navBadge("vacancies", counters), null);
  assert.equal(navBadge("telegram", { ...counters, telegramNew: 0 }), null);
  assert.equal(navBadge("telegram", null), null);
});

const v = (title, status, updatedAt, city = "Ташкент", departmentId = "sales") => ({ title, status, updatedAt, city, departmentId });
const rows = [
  v("Технолог", "Closed", "2026-11-10T10:00:00Z", "Ташкент", "prod"),
  v("Бухгалтер", "Paused", "2026-11-09T10:00:00Z", "Ташкент", "fin"),
  v("Кассир", "Open", "2026-11-01T10:00:00Z", "Ташкент, Юнусабад"),
  v("Кладовщик", "Draft", "2026-11-11T10:00:00Z", "Самарканд"),
  v("Продавец-консультант", "Open", "2026-11-08T10:00:00Z", "Ташкент, Чиланзар"),
];

test("порядок: активные → пауза → черновики → закрытые, внутри — недавно изменённые", () => {
  assert.deepEqual(sortVacancies(rows).map((r) => r.title), ["Продавец-консультант", "Кассир", "Бухгалтер", "Кладовщик", "Технолог"]);
});

test("фильтр: поиск по должности и городу без регистра, отдел, статус из адреса; счётчики статусов", () => {
  assert.deepEqual(rows.filter((r) => matchesQuery(r, "юнусабад", "")).map((r) => r.title), ["Кассир"]);
  assert.deepEqual(rows.filter((r) => matchesQuery(r, "  КАС ", "")).map((r) => r.title), ["Кассир"]);
  assert.deepEqual(rows.filter((r) => matchesQuery(r, "", "fin")).map((r) => r.title), ["Бухгалтер"]);
  assert.equal(parseStatus("pause"), "pause");
  assert.equal(parseStatus("Open"), "all");
  assert.equal(parseStatus(null), "all");
  assert.deepEqual(rows.filter((r) => matchesStatus(r, "active")).map((r) => r.title), ["Кассир", "Продавец-консультант"]);
  assert.deepEqual(statusCounts(rows), { all: 5, active: 2, pause: 1, closed: 1, draft: 1 });
});

test("зарплата: полностью для таблицы, коротко для телефона", () => {
  assert.deepEqual(salaryFull(6_000_000, 8_000_000), { key: "6 000 000 – 8 000 000" });
  assert.deepEqual(salaryFull(6_000_000, null), { key: "от {sum}", vars: { sum: "6 000 000" } });
  assert.deepEqual(salaryFull(null, 8_000_000), { key: "до {sum}", vars: { sum: "8 000 000" } });
  assert.deepEqual(salaryFull(null, null), { key: "—" });
  assert.deepEqual(salaryShort(6_000_000, 8_000_000), { key: "{v} млн", vars: { v: "6–8" } });
  assert.deepEqual(salaryShort(4_500_000, 5_500_000), { key: "{v} млн", vars: { v: "4,5–5,5" } });
  assert.deepEqual(salaryShort(100_000, 200_000), { key: "{v} тыс", vars: { v: "100–200" } });
  assert.deepEqual(salaryShort(500_000, 1_500_000), { key: "{v} млн", vars: { v: "0,5–1,5" } });
  assert.deepEqual(salaryShort(6_000_000, null), { key: "от {v} млн", vars: { v: "6" } });
  assert.deepEqual(salaryShort(null, 8_000_000), { key: "до {v} млн", vars: { v: "8" } });
  assert.deepEqual(salaryShort(null, null), { key: "—" });
});

test("рекрутер: инициалы, короткое имя, постоянный цвет; «Принято» — доля плана", () => {
  assert.equal(initials("Дилноза Юсупова"), "ДЮ");
  assert.equal(initials("Admin Admin"), "AA");
  assert.equal(initials("Мадина"), "МА");
  assert.equal(initials(null), "?");
  assert.equal(shortName("Дилноза Юсупова"), "Дилноза Ю.");
  assert.equal(shortName("Мадина"), "Мадина");
  assert.equal(avatarTone("Дилноза Юсупова"), avatarTone("Дилноза Юсупова"));
  assert.deepEqual(hiredProgress(2, 5), { pct: 40, done: false });
  assert.deepEqual(hiredProgress(1, 1), { pct: 100, done: true });
  assert.deepEqual(hiredProgress(3, 2), { pct: 100, done: true });
  assert.deepEqual(hiredProgress(0, 0), { pct: 0, done: false });
});
