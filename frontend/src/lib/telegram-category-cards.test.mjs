import { test } from "node:test";
import assert from "node:assert/strict";
import {
  allProducts,
  cardJobs,
  cardRecipients,
  categoryLabel,
  choiceText,
  effectiveCategories,
  effectiveProducts,
  keepProducts,
  pickedProducts,
  sameChoice,
  sameProducts,
  toggleCategory,
  toggleProduct,
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

test("товары категории: отмечены все — вся категория, последний не снимается, снятая категория забывает товары", () => {
  const prizel = { name: "Придзел", alias: "Twizos", products: [300, 302, 304].map((id) => ({ id, name: `Twizos ${id}`, code: String(id) })) };
  const cripzel = { name: "Cripzel", alias: null, products: [{ id: 337, name: "599 Cripzel Tuzli sneklar, 3-кг", code: "599" }] };
  assert.deepEqual(pickedProducts({}, prizel), [300, 302, 304]); // без выбора — все товары
  let p = toggleProduct({}, prizel, 302);
  assert.deepEqual(p, { Придзел: [300, 304] });
  p = toggleProduct(p, prizel, 300);
  assert.deepEqual(p, { Придзел: [304] });
  assert.equal(toggleProduct(p, prizel, 304), p); // последний отмеченный — не снимается
  assert.deepEqual(toggleProduct(toggleProduct(p, prizel, 300), prizel, 302), {}); // снова все — вся категория (новые товары тоже попадут)
  assert.deepEqual(toggleProduct({ Придзел: [300] }, prizel, 304), { Придзел: [300, 304] }); // порядок — как в справочнике
  assert.deepEqual(toggleProduct({}, cripzel, 337), {}); // единственный товар снять нельзя
  assert.deepEqual(allProducts({ Придзел: [300], Кекс: [1] }, "Придзел"), { Кекс: [1] });
  assert.deepEqual(keepProducts({ Придзел: [300], Кекс: [1], Помадка: [] }, ["Придзел", "Помадка"]), { Придзел: [300] });
  assert.ok(sameProducts({ Придзел: [302, 300] }, { Придзел: [300, 302] }));
  assert.ok(!sameProducts({ Придзел: [300] }, {}));
  assert.ok(!sameProducts({ Придзел: [300] }, { Придзел: [302] }));
});

test("подпись выбора с товарами и какие товары уйдут получателю", () => {
  assert.equal(choiceText(["Придзел", "Cripzel"], 10, { Придзел: [300, 302] }), "Придзел (товаров: 2), Cripzel");
  assert.equal(choiceText(["Придзел"], 1, { Придзел: [300] }, (n) => `tovarlar: ${n}`), "Придзел (tovarlar: 1)"); // выбраны товары — не «все категории»
  assert.equal(choiceText(["Придзел"], 1, {}), "все категории");
  const view = { defaults: ["Придзел"], defaultProducts: { Придзел: [300] } };
  assert.deepEqual(effectiveProducts(null, null, view), { Придзел: [300] });
  assert.deepEqual(effectiveProducts(["Кекс"], { Кекс: [1] }, view), { Кекс: [1] });
  assert.deepEqual(effectiveProducts(["Кекс"], null, view), {});
  assert.deepEqual(effectiveProducts(null, null, { defaults: [], defaultProducts: { Придзел: [300] } }), {}); // «все категории» — без выбора товаров
});
