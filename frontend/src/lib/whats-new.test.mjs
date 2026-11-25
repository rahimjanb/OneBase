// Окно «Что нового» (lib/whats-new.ts): node --test src/lib/whats-new.test.mjs.
import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import { test } from "node:test";
import { CURRENT_RELEASE, compareVersions, pluralRu, seenRelease, shouldShow, visibleSteps } from "./whats-new.ts";

const ids = (access) => visibleSteps(CURRENT_RELEASE, access).map((s) => s.id);
const all = ["battle", "sales-linko", "agent-day", "assortment", "analytik", "screening", "telegram"];
const sales = ["battle", "sales-linko", "agent-day", "assortment"];

test("релиз: 7 карточек, у каждой заголовок, 2–4 пункта, путь и раздел", () => {
  assert.deepEqual(CURRENT_RELEASE.steps.map((s) => s.id), all);
  for (const s of CURRENT_RELEASE.steps) {
    assert.ok(s.heading && s.description && s.descriptionShort && s.alt, s.id);
    assert.ok(s.bullets.length >= 2 && s.bullets.length <= 4, s.id);
    assert.ok(s.path.length >= 1 && s.route.startsWith("/"), s.id);
    assert.ok(s.descriptionShort.length < s.description.length, s.id);
  }
});

test("карточки — только разделов, открытых пользователю", () => {
  // Администратор: все права — все карточки.
  const admin = ["sales.read", "analytik.use", "hr.recruitment.manage", "telegram.manage"];
  assert.deepEqual(ids({ permissions: admin }), all);
  // РМ, СВР и ТП с правом «Продажи»: четыре карточки «Продаж».
  assert.deepEqual(ids({ permissions: ["sales.read"] }), sales);
  // Продажи и автоотчёты Telegram: плюс «Получатели».
  assert.deepEqual(ids({ permissions: ["sales.read", "telegram.manage"] }), [...sales, "telegram"]);
  // Автоотчёты строятся по «Продажам»: Telegram без «Продаж» — карточки нет.
  assert.deepEqual(ids({ permissions: ["telegram.manage"] }), []);
  // HR с подбором: только скрининг; Аналитик — только своя карточка.
  assert.deepEqual(ids({ permissions: ["hr.read", "hr.recruitment.read"] }), ["screening"]);
  assert.deepEqual(ids({ permissions: ["analytik.use"] }), ["analytik"]);
  // Кладовщик и сотрудник с задачами: их разделы не менялись — окна нет.
  assert.deepEqual(ids({ permissions: ["procurement.warehouse"] }), []);
  assert.deepEqual(ids({ permissions: ["work.tasks"] }), []);
  assert.deepEqual(ids({ permissions: [] }), []);
});

test("версии: по дню, затем по номеру за день", () => {
  assert.ok(compareVersions("2026-11-13", "2026-11-12") > 0);
  assert.ok(compareVersions("2026-11-13.2", "2026-11-13") > 0);
  assert.ok(compareVersions("2026-11-13.10", "2026-11-13.9") > 0);
  assert.equal(compareVersions("2026-11-13", "2026-11-13"), 0);
  assert.equal(seenRelease("2026-12-01", "2026-11-13"), true);
  assert.equal(seenRelease("2026-11-01", "2026-11-13"), false);
  assert.equal(seenRelease(null, "2026-11-13"), false);
});

test("показ один раз: закрыто на сервере или в браузере — нет; новым пользователям — нет", () => {
  const r = CURRENT_RELEASE;
  assert.equal(r.version, "2026-11-25");
  const old = "2026-10-01T09:00:00Z";
  assert.equal(shouldShow(r, { whatsNewSeen: null, createdAt: old }, null, 3), true);
  assert.equal(shouldShow(r, { whatsNewSeen: "2026-11-25", createdAt: old }, null, 3), false);
  assert.equal(shouldShow(r, { whatsNewSeen: null, createdAt: old }, "2026-11-25", 3), false); // запрос не дошёл — окно не повторяется
  assert.equal(shouldShow(r, { whatsNewSeen: "2026-11-13", createdAt: old }, "2026-11-13", 3), true); // окно 13.11 закрыто, это — нет
  assert.equal(shouldShow(r, { whatsNewSeen: null, createdAt: old }, null, 0), false); // ни одной открытой карточки
  // Пришёл в день релиза по Ташкенту (24.11 20:30 UTC = 25.11 01:30) — новый пользователь; накануне вечером — старый.
  assert.equal(shouldShow(r, { whatsNewSeen: null, createdAt: "2026-11-24T20:30:00Z" }, null, 3), false);
  assert.equal(shouldShow(r, { whatsNewSeen: null, createdAt: "2026-11-24T18:30:00Z" }, null, 3), true);
});

test("у каждой надписи окна и превью есть перевод на узбекский", () => {
  // Ключи всех словарей i18n/uz (окно переводит строки релиза через общий словарь); кириллица строится из латиницы сама.
  const dir = new URL("../i18n/uz/", import.meta.url);
  const keys = new Set();
  for (const file of readdirSync(dir).filter((f) => f.endsWith(".ts"))) {
    for (const m of readFileSync(new URL(file, dir), "utf8").matchAll(/^\s*"((?:[^"\\]|\\.)+)":/gm)) keys.add(m[1]);
  }
  const preview = readFileSync(new URL("../components/whats-new/WhatsNewPreview.tsx", import.meta.url), "utf8");
  const texts = [
    ...CURRENT_RELEASE.steps.flatMap((s) => [s.title, s.heading, s.description, s.descriptionShort, s.alt, ...s.bullets, ...s.path]),
    ...[...preview.matchAll(/t\("([^"]+)"\)/g)].map((m) => m[1]),
  ];
  assert.deepEqual(texts.filter((x) => !keys.has(x)), []);
});

test("склонение «нововведений»", () => {
  assert.equal(pluralRu(1, ["нововведение", "нововведения", "нововведений"]), "нововведение");
  assert.equal(pluralRu(3, ["нововведение", "нововведения", "нововведений"]), "нововведения");
  assert.equal(pluralRu(7, ["нововведение", "нововведения", "нововведений"]), "нововведений");
  assert.equal(pluralRu(11, ["нововведение", "нововведения", "нововведений"]), "нововведений");
});
