import { test } from "node:test";
import assert from "node:assert/strict";
import {
  cardJobs,
  cardRecipients,
  cardReportsOf,
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

test("запуски и подписчики карточек категорий", () => {
  const job = (code, report) => ({ code, report });
  assert.deepEqual(
    cardJobs([job("daily.evening", "daily"), job("category-cards.morning", "category-cards"), job("category-cards.evening", "category-cards")]).map((j) => j.code),
    ["category-cards.morning", "category-cards.evening"],
  );
  const r = (id, reports) => ({ id, reports });
  assert.deepEqual(cardRecipients([r("a", ["daily.evening"]), r("b", ["category-cards.evening"]), r("c", [])]).map((x) => x.id), ["b"]);
});

test("итоги дня по категориям — в той же вкладке: запуски, подписчики, номер отчёта", () => {
  const job = (code, report) => ({ code, report });
  assert.deepEqual(
    cardJobs([
      job("category-cards.morning", "category-cards"),
      job("category-cards.evening", "category-cards"),
      job("category-day.morning", "category-day"),
      job("category-day.evening", "category-day"),
      job("daily.evening", "daily"),
    ]).map((j) => j.code),
    ["category-cards.morning", "category-cards.evening", "category-day.morning", "category-day.evening"],
  );
  const r = (id, reports) => ({ id, reports });
  const list = [r("a", ["category-day.evening"]), r("b", ["category-cards.morning", "category-day.morning"]), r("c", ["daily.evening"])];
  assert.deepEqual(cardRecipients(list).map((x) => x.id), ["a", "b"]);
  assert.deepEqual(list.map(cardReportsOf), ["11", "10, 11", ""]);
});
