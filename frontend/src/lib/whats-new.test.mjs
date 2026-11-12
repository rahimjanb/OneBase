// Окно «Что нового» (lib/whats-new.ts): node --test src/lib/whats-new.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { CURRENT_RELEASE, compareVersions, pluralRu, seenRelease, shouldShow, visibleSteps } from "./whats-new.ts";

const ids = (access) => visibleSteps(CURRENT_RELEASE, access).map((s) => s.id);
const all = ["analytik", "recruitment", "production", "twin", "procurement", "agents", "incentives"];

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
  // Администратор: все права и агенты отделов — все карточки.
  const admin = ["analytik.use", "hr.recruitment.manage", "production.manage", "sales.read", "procurement.manage", "telegram.manage"];
  assert.deepEqual(ids({ permissions: admin, agentDepartments: ["hr"] }), all);
  // Кладовщик: склад и производство (кладовщику открыт раздел «Производство»), без Аналитика, подбора, двойника, агентов, автоотчётов.
  assert.deepEqual(ids({ permissions: ["procurement.warehouse"] }), ["production", "procurement"]);
  // Сотрудник отдела с задачами: только двойник (общий раздел компании).
  assert.deepEqual(ids({ permissions: ["work.tasks"] }), ["twin"]);
  // HR с подбором и агентами HR: подбор, двойник, агенты.
  assert.deepEqual(ids({ permissions: ["hr.read", "hr.recruitment.read"], agentDepartments: ["hr"] }), ["recruitment", "twin", "agents"]);
  // Без прав — ничего, окна нет.
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
  const old = "2026-10-01T09:00:00Z";
  assert.equal(shouldShow(r, { whatsNewSeen: null, createdAt: old }, null, 3), true);
  assert.equal(shouldShow(r, { whatsNewSeen: "2026-11-13", createdAt: old }, null, 3), false);
  assert.equal(shouldShow(r, { whatsNewSeen: null, createdAt: old }, "2026-11-13", 3), false); // запрос не дошёл — окно не повторяется
  assert.equal(shouldShow(r, { whatsNewSeen: "2026-10-20", createdAt: old }, null, 3), true); // прошлый релиз закрыт, этот — нет
  assert.equal(shouldShow(r, { whatsNewSeen: null, createdAt: old }, null, 0), false); // ни одной открытой карточки
  // Пришёл в день релиза по Ташкенту (12.11 20:30 UTC = 13.11 01:30) — новый пользователь; накануне вечером — старый.
  assert.equal(shouldShow(r, { whatsNewSeen: null, createdAt: "2026-11-12T20:30:00Z" }, null, 3), false);
  assert.equal(shouldShow(r, { whatsNewSeen: null, createdAt: "2026-11-12T18:30:00Z" }, null, 3), true);
});

test("склонение «нововведений»", () => {
  assert.equal(pluralRu(1, ["нововведение", "нововведения", "нововведений"]), "нововведение");
  assert.equal(pluralRu(3, ["нововведение", "нововведения", "нововведений"]), "нововведения");
  assert.equal(pluralRu(7, ["нововведение", "нововведения", "нововведений"]), "нововведений");
  assert.equal(pluralRu(11, ["нововведение", "нововведения", "нововведений"]), "нововведений");
});
