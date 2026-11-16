import { test } from "node:test";
import assert from "node:assert/strict";
import { STOP_STATUS, canGoForward, dayTitle, hm, mapUrl, minutes, shiftDay } from "./sales/agent-day.ts";

test("день ТП: минуты, отметки, соседние дни через границы месяца и года", () => {
  assert.equal(minutes(null), "—");
  assert.equal(minutes(7), "7 мин");
  assert.equal(minutes(65), "1 ч 05 мин");
  assert.equal(minutes(120), "2 ч");
  assert.equal(hm("09:05:00"), "09:05");
  assert.equal(hm(null), null);
  assert.equal(shiftDay("2026-11-30", 1), "2026-12-01");
  assert.equal(shiftDay("2026-03-01", -1), "2026-02-28");
  assert.equal(shiftDay("2027-01-01", -1), "2026-12-31");
  assert.equal(dayTitle("2026-11-16"), "пн, 16.11.2026");
  assert.equal(dayTitle("2026-11-15"), "вс, 15.11.2026");
  assert.ok(canGoForward("2026-11-16", "2026-11-17"));
  assert.ok(!canGoForward("2026-11-17", "2026-11-17")); // в будущее не листаем
});

test("день ТП: статусы точек и ссылка на карту", () => {
  assert.deepEqual(Object.keys(STOP_STATUS), ["done", "missed", "planned", "no_visit"]);
  assert.equal(STOP_STATUS.missed.label, "не посетил");
  assert.equal(mapUrl({ lat: 41.55, lon: 60.63 }), "https://yandex.uz/maps/?pt=60.63,41.55&z=17&l=map");
  assert.equal(mapUrl({ lat: null, lon: 60.63 }), null);
});
