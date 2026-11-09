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

test("технолог: базы нормы по типу этапа, масса строки и единицы, как на сервере", async () => {
  const { basesFor, lineKg, unitChoices, batchMass, massFactor } = await import("./production.ts");
  assert.deepEqual(basesFor("Mixing"), ["Batch"]);
  assert.deepEqual(basesFor("Packing"), ["Pack", "Box", "Piece", "Output"]);
  assert.deepEqual(basesFor("Glazing"), ["Output", "Piece"]);
  assert.deepEqual(basesFor(null), ["Output", "Piece"]);
  assert.equal(massFactor("г"), 0.001);
  assert.equal(massFactor("шт"), null);
  // Масса единицы: масса; мешок по 50 кг у позиции в кг; иначе — из карты.
  assert.equal(lineKg("г", "кг", [], null), 0.001);
  assert.equal(lineKg("упак", "кг", [{ unitCode: "упак", factor: 50 }], null), 50);
  assert.equal(lineKg("шт", "шт", [], 0.06), 0.06);
  assert.equal(lineKg("л", "л", [], null), null);
  assert.deepEqual(unitChoices("кг", [{ unitCode: "упак", factor: 25 }]), ["кг", "г", "т", "упак"]);
  assert.deepEqual(unitChoices("шт", []), ["шт"]);
  // Замес 150 + 60 + 40 + 500 яиц × 0,06 = 280 кг; строка без массы — неизвестно.
  assert.equal(batchMass([{ quantity: 150, kg: 1 }, { quantity: 60, kg: 1 }, { quantity: 40, kg: 1 }, { quantity: 500, kg: 0.06 }]), 280);
  assert.equal(batchMass([{ quantity: 150, kg: 1 }, { quantity: 10, kg: null }]), null);
});

test("технолог: поток этапа — вход, потери массы, брак, дальше", async () => {
  const { stageFlow } = await import("./production.ts");
  const stage = (defectBase, inputKg, outputKg, defectKg, addedKg = 0) => ({ no: 1, name: "", kind: "Baking", yieldPercent: 87.5, defectPercent: 2, defectBase, processed: 0, good: 0, rejected: 0, inputKg, addedKg, outputKg, defectKg, hasActual: true });
  // Выпечка 280 → 245 кг, брак 4,9 кг после обработки: потери 35, дальше 240,1.
  assert.deepEqual(stageFlow(stage("Output", 280, 245, 4.9)), { inKg: 280, addedKg: 0, lossKg: 35, defectKg: 4.9, nextKg: 240.1 });
  // Брак 10 кг отсеян на входе: обработано 270 → 236,25; потери 33,75; дальше всё обработанное.
  assert.deepEqual(stageFlow(stage("Input", 280, 236.25, 10)), { inKg: 280, addedKg: 0, lossKg: 33.75, defectKg: 10, nextKg: 236.25 });
  assert.equal(stageFlow(stage("Output", null, 245, 0)), null);
  // С сервера — посчитанные до округления: «дальше» совпадает со входом следующего этапа.
  assert.equal(stageFlow({ ...stage("Output", 5075.884, 5075.884, 50.759), lossKg: 0, nextKg: 5025.126 }).nextKg, 5025.126);
});

test("смена: выпечка 280 → 245 кг и выход вне допуска, как на сервере", async () => {
  const { bakeResult } = await import("./production.ts");
  assert.deepEqual(bakeResult(280, 245, 87.5, null, null), { lossKg: 35, lossPercent: 12.5, yieldPercent: 87.5, deviation: 0, outOfRange: false });
  const low = bakeResult(280, 238, 87.5, 86, 89);
  assert.equal(low.yieldPercent, 85);
  assert.equal(low.deviation, -2.5);
  assert.equal(low.outOfRange, true);
  assert.equal(bakeResult(1120, 900, 87.5, 86, 89).yieldPercent, 80.36);
});
