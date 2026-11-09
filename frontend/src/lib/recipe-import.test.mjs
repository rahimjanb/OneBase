// Импорт техкарт из Excel (lib/recipe-import-view.ts): node --test src/lib/recipe-import.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { fileSize, filterCounts, matchesFilter, parseManualNorm } from "./recipe-import-view.ts";

test("норма вручную: запятая и пробелы, не больше 6 знаков, больше нуля — иначе ничего не округляется", () => {
  assert.equal(parseManualNorm("25,542"), 25.542);
  assert.equal(parseManualNorm("1 050"), 1050);
  assert.equal(parseManualNorm("0.123456"), 0.123456);
  assert.equal(parseManualNorm("0.1234567"), null);
  assert.equal(parseManualNorm("0"), null);
  assert.equal(parseManualNorm("-3"), null);
  assert.equal(parseManualNorm("12кг"), null);
});

test("отборы: исключённые — только во вкладке «Исключены», дубли и пустые — вместе", () => {
  const rows = [
    { status: "Ready", include: true },
    { status: "Ready", include: false },
    { status: "NeedsReview", include: true },
    { status: "Duplicate", include: true },
    { status: "Empty", include: true },
    { status: "Applied", include: true },
  ];
  assert.deepEqual(filterCounts(rows), { all: 6, ready: 1, review: 1, unchanged: 0, skipped: 2, applied: 1, excluded: 1 });
  assert.equal(matchesFilter({ status: "Ready", include: false }, "ready"), false);
});

test("размер файла", () => {
  assert.equal(fileSize(2_617_442), "2,5 МБ");
  assert.equal(fileSize(840 * 1024), "840 КБ");
});
