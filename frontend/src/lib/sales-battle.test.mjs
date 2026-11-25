// «Продажи → Поле боя» (lib/sales/battle.ts): node --test src/lib/sales-battle.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { MARKS, SEGMENTS, setText, share } from "./sales/battle.ts";

test("набор «140x8+35x2» читается как на карточке", () => {
  assert.equal(setText("140x8+35x2"), "8 кор. 140 г + 2 кор. 35 г");
  assert.equal(setText("35x1+140x1+200x1"), "1 кор. 35 г + 1 кор. 140 г + 1 кор. 200 г");
  assert.equal(setText("35x3"), "3 кор. 35 г");
  assert.equal(setText(""), "");
});

test("доля разбуженных — целые проценты, 0 из 0 — 0", () => {
  assert.equal(share(4, 6), 67);
  assert.equal(share(0, 0), 0);
  assert.equal(share(21, 46), 46);
});

test("сегменты и отметки — цвета карточек и подписи ✓ ½ ×", () => {
  assert.match(SEGMENTS.existing.className, /E8731A/);
  assert.match(SEGMENTS.new.className, /2E9E4F/);
  assert.match(SEGMENTS.stand.className, /7B3FE4/);
  assert.deepEqual(
    ["taken", "partial", "refused"].map((m) => MARKS[m].label[0]),
    ["✓", "½", "×"],
  );
});
