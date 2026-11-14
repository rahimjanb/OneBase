// HR → «Подбор» → «AI-скрининг» (lib/screening-view.ts): node --test src/lib/screening-view.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import {
  activeFilters, apiQuery, canCompare, category, EMPTY_FILTERS, experienceText, filtersQuery, parseFilters, pointsText, percent, scoreTone, SETTABLE,
  toggleCompare,
} from "./screening-view.ts";

const params = (q) => new URLSearchParams(q);

test("категории и цвет оценки — пороги 90 / 80 / 70 / 50, как на сервере", () => {
  assert.deepEqual([95, 90, 89, 80, 79, 70, 69, 50, 49, 0].map(category),
    ["excellent", "excellent", "very_good", "very_good", "good", "good", "partial", "partial", "low", "low"]);
  assert.deepEqual([85, 72, 55, 30, null].map(scoreTone), ["ok", "accent", "warn", "bad", "neutral"]);
});

test("стаж словами, баллы и проценты", () => {
  assert.equal(experienceText(72), "6 лет");
  assert.equal(experienceText(40), "3 года 4 мес.");
  assert.equal(experienceText(12), "1 год");
  assert.equal(experienceText(132), "11 лет");
  assert.equal(experienceText(8), "8 мес.");
  assert.equal(experienceText(null), null);
  assert.equal(experienceText(0), null);
  assert.equal(pointsText(12.5, 25), "12,5 / 25");
  assert.equal(pointsText(20, 20), "20 / 20");
  assert.equal(percent(0.45), "45%");
  assert.equal(percent(null), null);
});

test("«анализируется» HR не выбирает", () => {
  assert.ok(!SETTABLE.includes("analyzing"));
  assert.equal(SETTABLE.length, 7);
});

test("фильтры в адресе: туда и обратно, чужие значения — по умолчанию", () => {
  const f = { ...EMPTY_FILTERS, vacancyId: "v1", status: "suitable", minScore: "70", skill: " Excel ", minExperience: "3", view: "table", sort: "recent" };
  const q = filtersQuery(f);
  assert.equal(q, "?vacancy=v1&status=suitable&score=70&skill=Excel&exp=3&sort=recent&view=table");
  assert.deepEqual(parseFilters(params(q.slice(1))), { ...f, skill: "Excel" });
  assert.equal(filtersQuery(EMPTY_FILTERS), "");
  const junk = parseFilters(params("status=boss&score=abc&sort=evil&view=grid&exp=99999"));
  assert.deepEqual([junk.status, junk.minScore, junk.sort, junk.view, junk.minExperience], ["", "", "score", "cards", ""]);
});

test("запрос к API: стаж — в месяцах, пустое не отправляется, сколько фильтров задано", () => {
  const f = { ...EMPTY_FILTERS, vacancyId: "v1", minExperience: "3", city: "Ташкент", search: "  ", sort: "score" };
  assert.deepEqual(apiQuery(f), { vacancyId: "v1", minExperience: "36", city: "Ташкент" });
  assert.equal(activeFilters(f), 2);
  assert.equal(activeFilters(EMPTY_FILTERS), 0);
});

test("сравнение: до пяти кандидатов одной вакансии; другая вакансия — выбор заново", () => {
  const r = (id, v = "v1") => ({ applicationId: id, vacancyId: v });
  let s = [];
  for (const id of ["a", "b", "c", "d", "e", "f"]) s = toggleCompare(s, r(id));
  assert.deepEqual(s.map((x) => x.applicationId), ["a", "b", "c", "d", "e"]);
  s = toggleCompare(s, r("b"));
  assert.deepEqual(s.map((x) => x.applicationId), ["a", "c", "d", "e"]);
  s = toggleCompare(s, r("z", "v2"));
  assert.deepEqual(s, [r("z", "v2")]);
  assert.equal(canCompare([{ analyzed: true }]), false);
  assert.equal(canCompare([{ analyzed: true }, { analyzed: false }]), false);
  assert.equal(canCompare([{ analyzed: true }, { analyzed: true }]), true);
});
