// Доступ к отделам и разделам меню (lib/navigation.ts): node --test src/lib/navigation.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { canOpen, departmentCodes, departmentHref, departmentOpen, screenPermissions } from "./navigation.ts";

const open = (me) => departmentCodes.filter((code) => departmentOpen(code, screenPermissions(me), me.departments));

test("руководство (все отделы из /api/auth/me) видит все отделы", () => {
  const director = { permissions: ["users.manage", "work.tasks", "sales.read", "hr.read", "procurement.read", "production.read"], departments: [...departmentCodes] };
  assert.deepEqual(open(director), [...departmentCodes]);
});

test("РМ продаж: только «Продажи» — свой отдел и раздел; чужие отделы не видны", () => {
  const rm = { permissions: ["sales.read", "work.tasks", "ai.agents.run", "files.read"], departments: ["sales"] };
  assert.deepEqual(open(rm), ["sales"]);
  assert.equal(departmentHref("sales", screenPermissions(rm)), "/sales");
});

test("свой раздел без задач отдела: технолог — «Производство», кладовщик — «Снабжение» и «Производство»", () => {
  assert.deepEqual(open({ permissions: ["production.read", "production.recipes"], departments: [] }), ["production"]);
  assert.deepEqual(open({ permissions: ["procurement.warehouse"] }), ["production", "supply"]);
});

test("отдел без своего раздела — по задачам отдела: маркетинг, финансы", () => {
  const marketer = { permissions: ["work.tasks", "ai.agents.run"], departments: ["marketing"] };
  assert.deepEqual(open(marketer), ["marketing"]);
  assert.equal(departmentHref("marketing", screenPermissions(marketer)), "/tasks/marketing");
});

test("«Продажи» OneBase — только с правом «Продажи»: Sales Base (field.use) их не открывает", () => {
  assert.equal(canOpen("/sales", ["field.use", "work.tasks"]), false);
  assert.equal(canOpen("/sales", ["sales.read"]), true);
  // Полевая команда — задачи «Продаж» (backend отдаёт sales в departments).
  assert.deepEqual(open({ permissions: ["field.use", "work.tasks"], departments: ["sales"] }), ["sales"]);
});

test("«Отделы» в меню — только если открывается хотя бы один отдел", () => {
  assert.equal(canOpen("/departments", screenPermissions({ permissions: ["files.read", "ai.agents.run"], departments: [] })), false);
  assert.equal(canOpen("/departments", screenPermissions({ permissions: ["files.read"], hrAccess: true })), true);
  assert.equal(canOpen("/departments", screenPermissions({ permissions: ["work.tasks"], departments: ["finance"] })), true);
});

test("руководитель с заявками на сотрудника — HR открыт (вкладка «Заявки на сотрудника»)", () => {
  assert.deepEqual(open({ permissions: ["work.tasks"], hiringRequests: true, departments: ["sales"] }), ["sales", "hr"]);
});
