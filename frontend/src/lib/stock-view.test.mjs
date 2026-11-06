// Склад (lib/stock-view.ts): node --test src/lib/stock-view.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { availableTone, minimumHint, movedWhen, nextSort, pageList, readColumns } from "./stock-view.ts";

test("движение: сегодня и вчера по Ташкенту — со временем, раньше — дата", () => {
  const now = new Date("2026-11-06T10:00:00Z"); // 15:00 в Ташкенте
  assert.deepEqual(movedWhen("2026-11-06T07:36:00Z", now), { when: "today", time: "12:36", date: "06.11.2026" });
  assert.deepEqual(movedWhen("2026-11-05T07:36:00Z", now), { when: "yesterday", time: "12:36", date: "05.11.2026" });
  assert.equal(movedWhen("2026-11-04T07:36:00Z", now).when, "date");
  // 23:30 UTC 5 ноября — уже 6 ноября в Ташкенте: «сегодня», не «вчера».
  assert.deepEqual(movedWhen("2026-11-05T23:30:00Z", now), { when: "today", time: "04:30", date: "06.11.2026" });
});

test("страницы: как на макете, пропуск в одну страницу — сама страница", () => {
  assert.deepEqual(pageList(1, 1), [1]);
  assert.deepEqual(pageList(2, 5), [1, 2, 3, 4, 5]);
  assert.deepEqual(pageList(1, 11), [1, 2, 3, "gap", 11]);
  assert.deepEqual(pageList(3, 11), [1, 2, 3, 4, "gap", 11]);
  assert.deepEqual(pageList(4, 11), [1, 2, 3, 4, 5, "gap", 11]);
  assert.deepEqual(pageList(6, 11), [1, "gap", 5, 6, 7, "gap", 11]);
  assert.deepEqual(pageList(9, 11), [1, "gap", 8, 9, 10, 11]);
  assert.deepEqual(pageList(11, 11), [1, "gap", 9, 10, 11]);
});

test("сортировка: та же колонка — другое направление, новая — по убыванию для чисел и дат, по возрастанию для названия", () => {
  assert.deepEqual(nextSort("", false, ""), { sort: "", desc: true });
  assert.deepEqual(nextSort("", false, "available"), { sort: "available", desc: true });
  assert.deepEqual(nextSort("available", true, "available"), { sort: "available", desc: false });
  assert.deepEqual(nextSort("moved", true, "sku"), { sort: "sku", desc: false });
});

test("колонки из браузера: нет записи или испорчена — все, чужие ключи отбрасываются", () => {
  assert.deepEqual(readColumns(null), ["reserved", "min", "moved"]);
  assert.deepEqual(readColumns("не json"), ["reserved", "min", "moved"]);
  assert.deepEqual(readColumns('["moved","x","reserved"]'), ["reserved", "moved"]);
  assert.deepEqual(readColumns("[]"), []);
});

test("подсказка о минимумах: большинство без минимума — текст макета, иначе — если есть ходовые", () => {
  assert.equal(minimumHint({ items: 544, noMin: 543, noMinMoving: 10 }), "most");
  assert.equal(minimumHint({ items: 10, noMin: 5, noMinMoving: 2 }), "moving");
  assert.equal(minimumHint({ items: 10, noMin: 5, noMinMoving: 0 }), null);
  assert.equal(minimumHint({ items: 0, noMin: 0, noMinMoving: 0 }), null);
});

test("плашка «Доступно»", () => {
  assert.equal(availableTone({ onHand: 0, available: 0, low: false }), "empty");
  assert.equal(availableTone({ onHand: 5, available: 3, low: true }), "low");
  assert.equal(availableTone({ onHand: 5, available: 0, low: false }), "none");
  assert.equal(availableTone({ onHand: 9400, available: 9400, low: false }), "ok");
});
