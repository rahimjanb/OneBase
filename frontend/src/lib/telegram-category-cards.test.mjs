import { test } from "node:test";
import assert from "node:assert/strict";
import {
  cardJobs,
  cardRecipients,
  categoryLabel,
  choiceText,
  effectiveCategories,
  sameChoice,
  toggleCategory,
} from "./telegram-category-cards.ts";

const options = ["Бамбук", "Кекс", "Трубочки", "Придзел", "Помадка"].map((name) => ({ name, alias: name === "Помадка" ? "Могуль" : null }));

test("выбор категорий: порядок настройки, повтор снимает, больше предела не добавляется", () => {
  assert.deepEqual(toggleCategory([], "Помадка", options, 3), ["Помадка"]);
  assert.deepEqual(toggleCategory(["Помадка"], "Придзел", options, 3), ["Придзел", "Помадка"]); // как в «Настройках продаж»
  assert.deepEqual(toggleCategory(["Придзел", "Помадка"], "Придзел", options, 3), ["Помадка"]);
  const full = ["Бамбук", "Кекс", "Трубочки"];
  assert.equal(toggleCategory(full, "Помадка", options, 3), full); // предел альбома — тот же список
  assert.deepEqual(toggleCategory(full, "Кекс", options, 3), ["Бамбук", "Трубочки"]); // снять можно всегда
  assert.equal(categoryLabel(options[4]), "Помадка · Могуль");
  assert.equal(categoryLabel(options[0]), "Бамбук");
});

test("какие категории уйдут: свой выбор → по умолчанию → все (не больше предела)", () => {
  const view = { categories: options, defaults: [], max: 4 };
  assert.deepEqual(effectiveCategories(null, view), ["Бамбук", "Кекс", "Трубочки", "Придзел"]);
  assert.deepEqual(effectiveCategories([], { ...view, defaults: ["Придзел"] }), ["Придзел"]);
  assert.deepEqual(effectiveCategories(["Помадка"], { ...view, defaults: ["Придзел"] }), ["Помадка"]);
  assert.equal(choiceText([], 5), "все категории");
  assert.equal(choiceText(options.map((o) => o.name), 5), "все категории");
  assert.equal(choiceText(["Придзел", "Помадка"], 5), "Придзел, Помадка");
  assert.ok(sameChoice(["a", "b"], ["a", "b"]));
  assert.ok(!sameChoice(["a", "b"], ["b", "a"])); // порядок — порядок картинок
});

test("запуски и подписчики — только итоги дня по категориям (отчёт 10 убран)", () => {
  const job = (code, report) => ({ code, report });
  assert.deepEqual(
    cardJobs([job("daily.evening", "daily"), job("category-day.morning", "category-day"), job("category-day.evening", "category-day")]).map((j) => j.code),
    ["category-day.morning", "category-day.evening"],
  );
  const r = (id, reports) => ({ id, reports });
  const list = [r("a", ["category-day.evening"]), r("b", ["daily.evening", "category-day.morning"]), r("c", ["daily.evening"]), r("d", ["category-cards.evening"])];
  assert.deepEqual(cardRecipients(list).map((x) => x.id), ["a", "b"]); // старый код 10 — не подписка (миграция переносит его в 11)
});
