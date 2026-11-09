// Номер Telegram у ИИ-агента (lib/agent-account.ts): node --test src/lib/agent-account.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import { accountChange, accountLabel } from "./agent-account.ts";

const number = (over) => ({ id: "1", name: "Отдел продаж", username: "candy_sales", phoneHint: "4567", status: "Connected", serves: false,
  defaultDepartment: "sales", defaultDepartmentName: "Продажи", takenBy: null, selectable: true, ...over });

test("подпись номера — название, @username и последние цифры", () => {
  assert.equal(accountLabel(number()), "Отдел продаж · @candy_sales · …4567");
  assert.equal(accountLabel(number({ username: null, phoneHint: null })), "Отдел продаж");
});

test("номер отдела, который уже отдаёт ему новые диалоги, не меняется", () => {
  assert.equal(accountChange(number({ serves: true, defaultDepartment: "hr" }), "hr", true), null);
});

test("администратор Telegram подключает номер к отделу; без этого права — только предупреждение о чужих новых диалогах", () => {
  assert.equal(accountChange(number(), "hr", true), "configure");
  assert.equal(accountChange(number({ serves: true }), "hr", true), "configure");
  assert.equal(accountChange(number({ serves: true }), "hr", false), "foreign-default");
  assert.equal(accountChange(number(), "hr", false), null);
});
