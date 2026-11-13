// Навигация модуля (lib/module-nav.ts): node --test src/lib/module-nav.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { arrange, emptyPrefs, fit, move, pin, prefsKey, readPrefs, reorder, search, unpin } from "./module-nav.ts";

const keys = ["analytics", "assortment", "skuSales", "primary", "dso"];

test("закреплённые — первыми в порядке закрепления, остальные — свой порядок, новые разделы — по конфигу", () => {
  assert.deepEqual(arrange(keys, emptyPrefs), keys);
  assert.deepEqual(arrange(keys, { order: [], pins: ["dso", "primary"] }), ["dso", "primary", "analytics", "assortment", "skuSales"]);
  assert.deepEqual(arrange(keys, { order: ["skuSales", "analytics"], pins: [] }), ["skuSales", "analytics", "assortment", "primary", "dso"]);
  // Раздел без права (нет в keys) в закреплённых не мешает.
  assert.deepEqual(arrange(["analytics", "dso"], { order: [], pins: ["pnl", "dso"] }), ["dso", "analytics"]);
});

test("закрепить и открепить", () => {
  const p = pin(pin(emptyPrefs, "dso"), "primary");
  assert.deepEqual(p.pins, ["dso", "primary"]);
  assert.equal(pin(p, "dso"), p);
  assert.deepEqual(unpin(p, "dso").pins, ["primary"]);
});

test("перетаскивание в «Настроить панель»: порядок закреплённых и остальных из списка", () => {
  const prefs = { order: [], pins: ["dso", "primary"] };
  const list = move(arrange(keys, prefs), 1, 0); // primary выше dso
  assert.deepEqual(list, ["primary", "dso", "analytics", "assortment", "skuSales"]);
  const next = reorder(prefs, move(list, 4, 2)); // skuSales — первым среди незакреплённых
  assert.deepEqual(next, { pins: ["primary", "dso"], order: ["skuSales", "analytics", "assortment"] });
  assert.deepEqual(arrange(keys, next), ["primary", "dso", "skuSales", "analytics", "assortment"]);
  assert.deepEqual(move(["a", "b"], 0, 5), ["a", "b"]);
});

test("сколько вкладок помещается: все без «Ещё» или столько, чтобы поместилась «Ещё»", () => {
  const widths = [100, 120, 80, 90];
  assert.equal(fit(widths, 1000, 70, 4), 4); // 390 + 12 — все
  assert.equal(fit(widths, 402, 70, 4), 4); // ровно впритык — все, без «Ещё»
  assert.equal(fit(widths, 401, 70, 4), 3); // не все: «Ещё» 70 + 100 + 120 + 80 и три промежутка = 382 ≤ 401, четвёртая уже нет
  assert.equal(fit(widths, 300, 70, 4), 2);
  assert.equal(fit(widths, 100, 70, 4), 1); // хотя бы одна
  assert.equal(fit([], 100, 70, 4), 0);
});

test("поиск по названию и пояснению, «ё» = «е», с начала слова", () => {
  const items = [
    { label: "Вторичка", description: "план, факт и работа ТП" },
    { label: "Рек. остаток", description: "рекомендуемые остатки" },
    { label: "Акт сверки", description: "взаиморасчёты" },
  ];
  assert.deepEqual(search(items, "  ").map((i) => i.label), ["Вторичка", "Рек. остаток", "Акт сверки"]);
  assert.deepEqual(search(items, "ост").map((i) => i.label), ["Рек. остаток"]);
  assert.deepEqual(search(items, "взаиморасчеты").map((i) => i.label), ["Акт сверки"]);
  assert.deepEqual(search(items, "факт план").map((i) => i.label), ["Вторичка"]);
  assert.deepEqual(search(items, "сверки акт").map((i) => i.label), ["Акт сверки"]);
  assert.deepEqual(search(items, "склад"), []);
});

test("сохранённые настройки: только известные разделы, испорченная запись — пустая", () => {
  assert.equal(prefsKey("u1", "sales"), "nav:pins:u1:sales");
  assert.deepEqual(readPrefs(JSON.stringify({ pins: ["dso", "dso", "nope", 5], order: ["primary"] }), keys), { pins: ["dso"], order: ["primary"] });
  assert.deepEqual(readPrefs("{oops", keys), emptyPrefs);
  assert.deepEqual(readPrefs(null, keys), emptyPrefs);
});

test("редкие разделы — в конце и всегда в «Ещё», закреплённые — в строке", async () => {
  const { withRare } = await import("./module-nav.ts");
  const keys = ["overview", "chat", "risks", "sales", "reports"];
  assert.deepEqual(withRare(keys, ["risks", "reports"], emptyPrefs), { keys: ["overview", "chat", "sales", "risks", "reports"], regular: 3 });
  assert.deepEqual(withRare(keys, ["risks", "reports"], { order: [], pins: ["risks"] }), { keys: ["overview", "chat", "risks", "sales", "reports"], regular: 4 });
  // Всё помещается, но «Ещё» нужна под редкие: место под неё оставляется.
  assert.equal(fit([100, 100, 100], 380, 80, 4), 3);
  assert.equal(fit([100, 100, 100], 380, 80, 4, true), 2);
});
