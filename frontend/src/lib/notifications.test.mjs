// Уведомления (lib/notifications.ts): node --test src/lib/notifications.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import {
  applyRead,
  applyReadAll,
  applyRemove,
  bellBadge,
  filterCount,
  filtersFor,
  freshIds,
  groupByDay,
  monthsRu,
  plural,
  pushStateOf,
  PUSH_SNOOZE_MS,
  sectionOf,
  showPushPrompt,
  timeText,
  TYPES,
  typeOf,
} from "./notifications.ts";

const now = new Date(2026, 9, 30, 15, 36);
const at = (d, h, m) => new Date(2026, 9, d, h, m).toISOString();

test("у каждого типа — значок, цвет и раздел из одного конфига; сбой системы — красный", () => {
  for (const [type, v] of Object.entries(TYPES)) {
    assert.ok(v.icon && v.tone && v.section, type);
  }
  assert.deepEqual(typeOf({ type: "task_overdue", severity: "critical" }), { icon: "alarm-clock", tone: "danger", section: "Задачи" });
  assert.equal(typeOf({ type: "system", severity: "critical" }).tone, "danger");
  assert.equal(typeOf({ type: "system", severity: "info" }).tone, "success");
  assert.equal(typeOf({ type: "unknown_new_type", severity: "info" }).icon, "refresh-cw");
});

test("группы и время — по часовому поясу устройства: сегодня 14:12, вчера 18:00, 28 окт", () => {
  assert.equal(sectionOf(at(30, 14, 12), now), "Сегодня");
  assert.equal(sectionOf(at(29, 18, 0), now), "Вчера");
  assert.equal(sectionOf(at(28, 9, 0), now), "Ранее");
  assert.deepEqual(timeText(at(30, 14, 12), now, monthsRu), { text: "14:12", yesterday: false });
  assert.deepEqual(timeText(at(29, 18, 0), now, monthsRu), { text: "18:00", yesterday: true });
  assert.deepEqual(timeText(at(28, 9, 0), now, monthsRu), { text: "28 окт", yesterday: false });
  assert.equal(timeText(new Date(2025, 9, 28, 9).toISOString(), now, monthsRu).text, "28.10.2025");
  const groups = groupByDay([{ createdAt: at(30, 14, 12) }, { createdAt: at(30, 9, 0) }, { createdAt: at(29, 18, 0) }, { createdAt: at(20, 9, 0) }], now);
  assert.deepEqual(groups.map((g) => [g.label, g.items.length]), [["Сегодня", 2], ["Вчера", 1], ["Ранее", 1]]);
});

test("колокольчик: точка до 9 непрочитанных, «9+» — больше", () => {
  assert.equal(bellBadge(0), "none");
  assert.equal(bellBadge(4), "dot");
  assert.equal(bellBadge(9), "dot");
  assert.equal(bellBadge(12), "9+");
  assert.equal(plural(1, "a", "b", "c"), "a");
  assert.equal(plural(4, "a", "b", "c"), "b");
  assert.equal(plural(12, "a", "b", "c"), "c");
  assert.equal(plural(21, "a", "b", "c"), "a");
});

test("вкладки разделов — только при доступе; числа — у «Все» и «Непрочитанные»", () => {
  assert.deepEqual(filtersFor(["work.tasks"]), ["all", "unread", "tasks"]);
  assert.deepEqual(filtersFor(["work.tasks", "ai.agents.run", "hr.read"]), ["all", "unread", "tasks", "ai", "hr"]);
  assert.deepEqual(filtersFor(["hr.department"]), ["all", "unread", "hr"]);
  const counts = { all: 12, unread: 4, tasks: 5, ai: 3, hr: 1 };
  assert.equal(filterCount("all", counts), 12);
  assert.equal(filterCount("unread", counts), 4);
  assert.equal(filterCount("tasks", counts), null);
});

test("карточка push: default — показать; включено и не поддерживается — нет; «Не сейчас» — на 7 дней", () => {
  const t0 = Date.UTC(2026, 9, 30);
  assert.equal(showPushPrompt("default", null, t0), true);
  assert.equal(showPushPrompt("denied", null, t0), true);
  assert.equal(showPushPrompt("ios-install", null, t0), true);
  assert.equal(showPushPrompt("granted", null, t0), false);
  assert.equal(showPushPrompt("unsupported", null, t0), false);
  assert.equal(showPushPrompt("default", t0, t0 + PUSH_SNOOZE_MS - 1), false);
  assert.equal(showPushPrompt("default", t0, t0 + PUSH_SNOOZE_MS), true);
  assert.equal(showPushPrompt("default", t0, t0 + 1000, true), true); // пустое состояние — всегда
  assert.equal(pushStateOf("supported", "granted", true), "granted");
  assert.equal(pushStateOf("supported", "granted", false), "default");
  assert.equal(pushStateOf("supported", "denied", false), "denied");
  assert.equal(pushStateOf("ios-install", "default", false), "ios-install");
});

test("прочитано, удаление и «прочитать все» обновляют счётчики сразу", () => {
  const item = (id, read, category = "tasks") => ({ id, read, readAt: null, category, type: "task_assigned", kind: "", title: id, body: null, href: null, createdAt: at(30, 9, 0), severity: "info", actions: [] });
  const page = { items: [item("a", false), item("b", true), item("c", false, "ai")], nextCursor: null, counts: { all: 3, unread: 2, tasks: 2, ai: 1, hr: 0 }, unread: 2 };
  const read = applyRead(page, "a", true);
  assert.equal(read.counts.unread, 1);
  assert.equal(read.items[0].read, true);
  assert.equal(applyRead(read, "a", false).counts.unread, 2);
  assert.equal(applyRead(page, "b", true), page); // уже прочитано — без изменений
  const removed = applyRemove(page, "c");
  assert.deepEqual(removed.counts, { all: 2, unread: 1, tasks: 2, ai: 0, hr: 0 });
  assert.equal(applyReadAll(page).counts.unread, 0);
  assert.ok(applyReadAll(page).items.every((n) => n.read));
  assert.deepEqual(freshIds([item("a", false)], [item("n", false), item("a", false)]), ["n"]);
  assert.deepEqual(freshIds([], [item("n", false)]), []);
});
