// Контроль качества (lib/quality.ts): node --test src/lib/quality.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { normText, parameterPassed, qualitySteps, specProblem, stepState, withinNorm } from "./quality.ts";

const draft = (over = {}) => ({ key: "k", id: null, kind: "Parameter", name: "Влажность", unit: "%", min: "", max: "14", expected: "", method: "", required: true, ...over });

test("норма показателя — границы входят, как на сервере", () => {
  assert.equal(withinNorm(null, 14, 14), true);
  assert.equal(withinNorm(null, 14, 14.01), false);
  assert.equal(withinNorm(10, 12, 9.99), false);
  assert.equal(withinNorm(null, null, -1), true);
});

test("итог показателя: количественный — по значению, качественный — отметкой", () => {
  assert.equal(parameterPassed({ min: null, max: 14 }, "15,2", true), false);
  assert.equal(parameterPassed({ min: null, max: 14 }, "12,5", null), true);
  assert.equal(parameterPassed({ min: null, max: 14 }, "", true), null);
  assert.equal(parameterPassed({ min: null, max: null }, "", false), false);
});

test("подпись нормы", () => {
  assert.equal(normText({ min: null, max: 14, expected: null, unit: "%" }), "≤ 14 %");
  assert.equal(normText({ min: 10.5, max: 12, expected: null, unit: "%" }), "10,5–12 %");
  assert.equal(normText({ min: 5, max: null, expected: null, unit: null }), "≥ 5");
  assert.equal(normText({ min: null, max: null, expected: "без посторонних запахов", unit: null }), "без посторонних запахов");
});

test("шаги проверки: лаборатория — только при контроле «документы и лаборатория»", () => {
  assert.deepEqual(qualitySteps("Lab"), ["Documents", "Lab", "Decision"]);
  assert.deepEqual(qualitySteps("Documents"), ["Documents", "Decision"]);
  assert.equal(stepState("Lab", "Lab", "Documents"), "done");
  assert.equal(stepState("Lab", "Lab", "Lab"), "current");
  assert.equal(stepState("Lab", "Lab", "Decision"), "todo");
  assert.equal(stepState("Documents", "Decided", "Decision"), "done");
});

test("строка спецификации проверяется до отправки", () => {
  assert.equal(specProblem(draft()), null);
  assert.equal(specProblem(draft({ name: " " })), "У строки нет названия");
  assert.equal(specProblem(draft({ min: "15", max: "14" })), "Норма «от» больше нормы «до»");
  assert.equal(specProblem(draft({ max: "много" })), "Норма — числом");
  assert.equal(specProblem(draft({ max: "" })), "Укажите норму — от и до или каким должен быть показатель");
  assert.equal(specProblem(draft({ max: "", expected: "без запаха" })), null);
  assert.equal(specProblem(draft({ kind: "Document", name: "Сертификат качества", max: "" })), null);
});
