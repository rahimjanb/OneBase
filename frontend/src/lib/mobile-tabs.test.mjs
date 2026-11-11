// Нижняя панель на телефоне (lib/mobile-tabs.ts): node --test src/lib/mobile-tabs.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { roleTab } from "./mobile-tabs.ts";

// Права ролей — как в справочнике ролей (HR, Технолог, Снабжение, Кладовщик, Продажи).
const hr = ["files.read", "ai.agents.run", "hr.read", "work.tasks", "procurement.request", "hr.recruitment.read", "hr.recruitment.manage"];
const technologist = ["files.read", "ai.agents.run", "work.tasks", "procurement.request", "production.read", "production.recipes"];
const supply = ["files.read", "ai.agents.run", "sales.read", "work.tasks", "procurement.request", "procurement.read", "procurement.manage", "production.read"];
const keeper = ["files.read", "ai.agents.run", "work.tasks", "procurement.request", "procurement.warehouse"];

test("есть продажи — остаются «Продажи»", () => {
  assert.equal(roleTab(["sales.read", "hr.recruitment.manage", "production.recipes"]), null);
  assert.equal(roleTab(supply), null); // у роли «Снабжение» в справочнике есть и продажи
});

test("без продаж: HR — «Подбор», технолог — «Технолог», снабженец — «Снабжение»", () => {
  assert.equal(roleTab(hr), "recruitment");
  assert.equal(roleTab(["hr.recruitment.read"]), "recruitment");
  assert.equal(roleTab(technologist), "technologist");
  assert.equal(roleTab(supply.filter((p) => p !== "sales.read")), "procurement");
});

test("несколько ролей — по порядку HR → технолог → снабженец; без своей роли — как раньше", () => {
  assert.equal(roleTab([...technologist, "hr.recruitment.read"]), "recruitment");
  assert.equal(roleTab([...technologist, "procurement.manage"]), "technologist");
  assert.equal(roleTab(keeper), null);
  assert.equal(roleTab(["hr.read"]), null); // HR без права «Подбор» — подбор ему не открыть
  assert.equal(roleTab([]), null);
});
