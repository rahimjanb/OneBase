// Подписи карточек AI-сотрудников (lib/agent-state.ts): node --test src/lib/agent-state.test.mjs (Node 22.18+ запускает TypeScript сам).
import assert from "node:assert/strict";
import { test } from "node:test";
import { agentState } from "./agent-state.ts";

test("состояния без данных подписаны по причине, а не одним «нет данных»", () => {
  assert.equal(agentState("no_data", "not_found").label, "не найдено");
  assert.equal(agentState("no_data", "not_in_onebase").label, "нет в источниках OneBase");
  assert.equal(agentState("no_data", "out_of_range").label, "период недоступен");
  assert.equal(agentState("no_data", "source_not_connected").label, "источник не подключён");
  assert.equal(agentState("no_data", "source_not_loaded").label, "источник не подключён");
  assert.equal(agentState("no_data", "no_access").label, "нет доступа");
  for (const reason of ["not_found", "not_in_onebase", "out_of_range", "source_not_connected"]) {
    assert.notEqual(agentState("no_data", reason).label, "нет данных", reason);
  }
});

test("старые ответы без причины сохраняют прежнюю подпись", () => {
  assert.equal(agentState("no_data").label, "нет данных");
  assert.equal(agentState("no_data", null).label, "нет данных");
});

test("сбой — не «нет данных»: подпись сбоя, пояснение вместо слов модели и текста ошибки", () => {
  for (const reason of ["tool_error", "interrupted", "internal_error", null]) {
    const s = agentState("failed", reason);
    assert.equal(s.tone, "bad");
    assert.equal(s.showSummary, false);
    assert.ok(s.note && !/данных нет|нет данных/i.test(s.note), String(reason));
    assert.ok(!/данных нет|нет данных/i.test(s.label));
  }
  assert.equal(agentState("failed", "tool_error").label, "сбой источника");
  assert.equal(agentState("failed", "model_unavailable").label, "модель недоступна");
});

test("найденные и частичные данные показываются как есть", () => {
  assert.deepEqual(agentState("completed"), { label: "готово", tone: "ok", note: null, showSummary: true });
  assert.equal(agentState("partial").label, "частично");
  assert.equal(agentState("partial", "interrupted").label, "частично: прерван");
  assert.equal(agentState("partial", "interrupted").showSummary, true);
  assert.equal(agentState("forbidden", "no_access").label, "нет доступа");
});

test("«период недоступен» не показывает слова модели — пояснение берётся из ошибки сервера", () => {
  const s = agentState("no_data", "out_of_range");
  assert.equal(s.showSummary, false);
  assert.equal(s.note, null);
});
