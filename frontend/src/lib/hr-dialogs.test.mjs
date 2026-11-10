// «Диалоги Telegram» (lib/hr-dialogs-view.ts): node --test src/lib/hr-dialogs.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { authorPrefix, avatarTone, dayLabel, filterCounts, initials, listTime, matchesFilter, matchesSearch, scoreTone, sortDialogs, stageIndex } from "./hr-dialogs-view.ts";

// Четверг 12.11.2026, 15:00 Ташкента.
const now = new Date("2026-11-12T10:00:00Z");

test("время в списке: сегодня — часы, вчера, день недели этой недели, раньше — дата", () => {
  assert.equal(listTime("2026-11-12T05:15:00Z", now), "10:15");
  assert.equal(listTime("2026-11-11T05:15:00Z", now), "Вчера");
  assert.equal(listTime("2026-11-10T05:15:00Z", now), "Вт");
  assert.equal(listTime("2026-11-09T05:15:00Z", now), "Пн");
  assert.equal(listTime("2026-11-07T05:15:00Z", now), "07.11");
  // Полночь по Ташкенту, а не по UTC: 20:30 UTC 11.11 — это уже 12.11 в Ташкенте.
  assert.equal(listTime("2026-11-11T20:30:00Z", now), "01:30");
  assert.equal(dayLabel("2026-11-12T05:00:00Z", now), "Сегодня");
  assert.equal(dayLabel("2026-11-11T05:00:00Z", now), "Вчера");
  assert.equal(dayLabel("2026-11-08T05:00:00Z", now), "8 ноября");
});

const rows = [
  { id: "a", name: "Жасур Тошматов", username: "jasur_t", mode: "hr", unread: 0, lastAt: "2026-11-12T05:15:00Z" },
  { id: "b", name: "Малика Юсупова", username: null, mode: "ai", unread: 2, lastAt: "2026-11-12T05:09:00Z" },
  { id: "c", name: "Бобур Алиев", username: "bobur", mode: "need_hr", unread: 1, lastAt: "2026-11-12T04:58:00Z" },
  { id: "d", name: "Азиз Хамидов", username: null, mode: "need_hr", unread: 0, lastAt: "2026-11-09T04:58:00Z" },
];

test("сначала «Нужен HR», затем по последнему сообщению", () => {
  assert.deepEqual(sortDialogs(rows).map((r) => r.id), ["c", "d", "a", "b"]);
});

test("фильтры и счётчики: все, AI, нужен HR, новые (есть непрочитанные)", () => {
  assert.deepEqual(filterCounts(rows), { all: 4, ai: 1, need_hr: 2, new: 2 });
  assert.deepEqual(rows.filter((r) => matchesFilter(r, "new")).map((r) => r.id), ["b", "c"]);
  assert.ok(matchesSearch(rows[0], "@JASUR"));
  assert.ok(matchesSearch(rows[0], "тошм"));
  assert.ok(!matchesSearch(rows[1], "jasur"));
});

test("аватар: инициалы и постоянный цвет; префиксы; этапы; цвет оценки", () => {
  assert.equal(initials("Жасур Тошматов"), "ЖТ");
  assert.equal(initials("@jasur_t"), "J");
  assert.equal(avatarTone("a"), avatarTone("a"));
  assert.ok(["accent", "violet", "ok", "warn", "bad"].includes(avatarTone("xyz")));
  assert.equal(authorPrefix("ai"), "AI: ");
  assert.equal(authorPrefix("hr"), "Вы: ");
  assert.equal(authorPrefix("candidate"), "");
  assert.equal(stageIndex("Interview"), 2);
  assert.equal(stageIndex("Hired"), 4);
  assert.equal(stageIndex("Rejected"), -1);
  assert.deepEqual([82, 55, 20].map(scoreTone), ["ok", "warn", "bad"]);
});
