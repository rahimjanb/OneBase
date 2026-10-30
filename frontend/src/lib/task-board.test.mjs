// Канбан и карточка задачи (lib/task-board.ts): node --test src/lib/task-board.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import {
  activeFilterCount,
  activity,
  boardOrder,
  canReview,
  dueState,
  initialsOf,
  insertMention,
  mentionAt,
  mentionedIds,
  neighbors,
  presetOf,
  presetRange,
  showProgress,
  splitMentions,
  stageSteps,
} from "./task-board.ts";

const today = "2026-10-31";

test("срок: просрочен — красный, сегодня и завтра — жёлтый, у выполненной не подсвечивается", () => {
  assert.equal(dueState({ dueDate: "2026-10-30", status: "InProgress", overdue: true }, today), "overdue");
  assert.equal(dueState({ dueDate: today, status: "New", overdue: false }, today), "soon");
  assert.equal(dueState({ dueDate: "2026-11-01", status: "Review", overdue: false }, today), "soon");
  assert.equal(dueState({ dueDate: "2026-11-12", status: "New", overdue: false }, today), "later");
  assert.equal(dueState({ dueDate: "2026-10-20", status: "Done", overdue: false }, today), "later");
  assert.equal(dueState({ dueDate: null, status: "New", overdue: false }, today), "none");
});

test("прогресс на карточке — только между 0 и 100; принять и вернуть — проверяющему", () => {
  assert.deepEqual([0, 40, 100].map(showProgress), [false, true, false]);
  assert.equal(canReview({ status: "Review", nextStatuses: ["Done", "InProgress", "Cancelled"] }), true);
  assert.equal(canReview({ status: "Review", nextStatuses: [] }), false);
  assert.equal(canReview({ status: "InProgress", nextStatuses: ["Review", "Done"] }), false);
});

test("период «Создана»: пресеты и свой период; счётчик фильтров без поиска", () => {
  assert.deepEqual(presetRange("today", today), { from: today, to: today });
  assert.deepEqual(presetRange("7d", today), { from: "2026-10-25", to: today });
  assert.equal(presetOf("2026-10-02", today, today), "30d");
  assert.equal(presetOf("2026-10-10", "2026-10-12", today), "custom");
  assert.equal(presetOf(null, null, today), null);
  const params = new URLSearchParams("search=акт&priority=High,Urgent&from=2026-10-31&to=2026-10-31&mine=true&department=sales");
  assert.equal(activeFilterCount((k) => params.get(k)), 4);
  assert.equal(activeFilterCount((k) => params.get(k), "sales"), 3);
});

test("этапы: пройденные с датой, текущий «с …», будущие без даты", () => {
  const stages = [
    { status: "New", at: "2026-10-26T03:29:00Z", actorName: "A" },
    { status: "Done", at: "2026-10-26T13:14:00Z", actorName: "A" },
    { status: "InProgress", at: "2026-10-29T16:23:00Z", actorName: "A" },
    { status: "Review", at: "2026-10-29T16:23:30Z", actorName: "Исполнитель" },
  ];
  const steps = stageSteps("Review", stages, "2026-10-26T03:29:00Z");
  assert.deepEqual(steps.map((s) => s.state), ["done", "done", "current", "future"]);
  assert.equal(steps[1].at, "2026-10-29T16:23:00Z");
  assert.equal(steps[2].actor, "Исполнитель");
  assert.equal(steps[3].at, null);
  assert.deepEqual(stageSteps("Done", stages, "x").map((s) => s.state), ["done", "done", "done", "current"]);
});

test("лента: комментарии и история вместе, новые сверху; фильтры", () => {
  const detail = {
    comments: [{ id: "c1", authorId: "u", authorName: "Admin", text: "Готово", createdAt: "2026-10-29T16:24:00Z" }],
    history: [
      { id: "h2", kind: "review", text: "Отправлена на проверку", actorName: "Admin", createdAt: "2026-10-29T16:23:00Z" },
      { id: "h1", kind: "created", text: "Задача создана", actorName: "Admin", createdAt: "2026-10-26T03:29:00Z" },
    ],
  };
  assert.deepEqual(activity(detail, "all").map((e) => e.id), ["c1", "h2", "h1"]);
  assert.deepEqual(activity(detail, "comments").map((e) => e.id), ["c1"]);
  assert.deepEqual(activity(detail, "history").map((e) => (e.kind === "event" ? e.look : "")), ["review", "created"]);
});

test("@-упоминания: набор, вставка, кто упомянут, подсветка", () => {
  assert.deepEqual(mentionAt("Привет @Бух", 11), { start: 7, query: "Бух" });
  assert.equal(mentionAt("почта a@b", 9), null);
  assert.equal(mentionAt("@Бух\nдалее", 10), null);
  const r = insertMention("Привет @Бух", 7, 11, "Бухгалтер Второй");
  assert.deepEqual(r, { text: "Привет @Бухгалтер Второй ", caret: 25 });
  const people = [
    { id: "1", name: "Бухгалтер Второй" },
    { id: "2", name: "Admin Admin" },
  ];
  assert.deepEqual(mentionedIds(r.text, people), ["1"]);
  assert.deepEqual(splitMentions("@Admin Admin посмотрите, @кто-то", ["Admin Admin"]), [
    { text: "@Admin Admin", mention: true },
    { text: " посмотрите, ", mention: false },
    { text: "@кто-то", mention: true },
  ]);
  assert.deepEqual(splitMentions("почта a@b.uz"), [{ text: "почта a@b.uz", mention: false }]);
});

test("соседние задачи и порядок доски; инициалы", () => {
  assert.deepEqual(neighbors(["a", "b", "c"], "b"), { index: 1, total: 3, prev: "a", next: "c" });
  assert.equal(neighbors(["a"], "z"), null);
  const t = (id, status, completedAt = null) => ({ id, status, completedAt });
  assert.deepEqual(
    boardOrder([t("d1", "Done", "2026-10-01"), t("r", "Review"), t("n", "New"), t("d2", "Done", "2026-10-20")]).map((x) => x.id),
    ["n", "r", "d2", "d1"],
  );
  assert.deepEqual([initialsOf("Admin Admin"), initialsOf("Карим"), initialsOf(null)], ["AA", "КА", "?"]);
});

test("время по Ташкенту: с годом и без", async () => {
  const { stamp, stampParts } = await import("./task-board.ts");
  assert.equal(stamp("2026-10-29T11:23:00Z"), "29.10.2026, 16:23");
  assert.equal(stamp("2026-10-29T11:23:00Z", false), "29.10, 16:23");
  assert.deepEqual(stampParts("2026-10-25T22:29:00Z"), { day: "26.10", year: "2026", time: "03:29" });
});
