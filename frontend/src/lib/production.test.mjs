// Производство (lib/production.ts): node --test src/lib/production.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { deviation, leftToIssue, percentLabel, requirement } from "./production.ts";

test("потребность — норма на план с потерями, до грамма, как на сервере", () => {
  assert.equal(requirement(55, 250, 100, 2), 140.25);
  assert.equal(requirement(12.5, 250, 100, 0), 31.25);
  assert.equal(requirement(1, 1, 3, 0.1), 0.334);
  assert.equal(requirement(10, 5, 0, 0), 0);
  assert.equal(requirement(1, 100, 100, 100), 2);
});

test("отклонение и остаток к выдаче", () => {
  assert.deepEqual(deviation(52, 45), { quantity: 7, percent: 15.6 });
  assert.deepEqual(deviation(45, 50), { quantity: -5, percent: -10 });
  assert.deepEqual(deviation(3, 0), { quantity: 3, percent: null });
  assert.equal(leftToIssue(50, 55, 3), 0);
  assert.equal(leftToIssue(50, 45, 3), 8);
  assert.equal(percentLabel(15.6), "+15,6 %");
  assert.equal(percentLabel(-10), "−10 %");
  assert.equal(percentLabel(null), "—");
});

test("дата отметки времени — по Ташкенту", async () => {
  const { dayRu } = await import("./production.ts");
  assert.equal(dayRu("2026-11-05T23:17:00Z"), "06.11.2026");
  assert.equal(dayRu(null), "—");
});
