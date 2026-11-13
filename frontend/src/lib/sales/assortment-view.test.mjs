// «Продажи · Ассортимент · Категория» (lib/sales/assortment-view.ts): node --test src/lib/sales/assortment-view.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { matchesSku, skuName, sortSkus } from "./assortment-view.ts";

test("название без кода Linko в начале", () => {
  assert.equal(skuName({ code: "002", name: "002 Кукурузные палочки, ROHAT BAMBUK, 3-кг" }), "Кукурузные палочки, ROHAT BAMBUK, 3-кг");
  assert.equal(skuName({ code: "518", name: "518-Kекс Nillo Brauni" }), "Kекс Nillo Brauni");
  assert.equal(skuName({ code: "189-1", name: "189-1Вафельные трубочки" }), "Вафельные трубочки");
  assert.equal(skuName({ code: "496", name: "496 (510) Кекс" }), "(510) Кекс");
  assert.equal(skuName({ code: null, name: "Стеллаж TWIZOS" }), "Стеллаж TWIZOS");
  assert.equal(skuName({ code: "248", name: "Хрустящие палочки" }), "Хрустящие палочки");
});

test("порядок: факт по убыванию, «Пропал» в конце; поиск по названию и артикулу", () => {
  const rows = [
    { name: "А", code: "1", status: "lost", factKg: 0 },
    { name: "Б", code: "2", status: "selling", factKg: 10 },
    { name: "В", code: "3", status: "silent", factKg: 0 },
    { name: "Г Nillo", code: "315", status: "selling", factKg: 50 },
  ];
  assert.deepEqual(sortSkus(rows).map((r) => r.code), ["315", "2", "3", "1"]);
  assert.deepEqual(rows.filter((r) => matchesSku(r, " NILLO ")).map((r) => r.code), ["315"]);
  assert.deepEqual(rows.filter((r) => matchesSku(r, "31")).map((r) => r.code), ["315"]);
  assert.equal(rows.filter((r) => matchesSku(r, "")).length, 4);
});
