// Экран AI-консультанта (lib/consultant-view.ts): node --test src/lib/consultant-view.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import {
  answerText,
  chatSection,
  chatSource,
  chatTitle,
  contextPayload,
  currentStep,
  filterChats,
  formatNumber,
  groupChats,
  initialContext,
  plural,
  progressView,
  quickQuestions,
  signed,
  sourceLabels,
  taskDraft,
  tashkentMonth,
} from "./consultant-view.ts";

const now = new Date(2026, 9, 30, 15, 36);
const chats = [
  { id: "a", title: "Состояние продаж", createdAt: "", lastMessageAt: new Date(2026, 9, 30, 15, 36).toISOString(), source: "web" },
  { id: "b", title: "Telegram · Salom", createdAt: "", lastMessageAt: new Date(2026, 9, 30, 5, 7).toISOString(), source: "telegram" },
  { id: "c", title: "Используй фактические…", createdAt: "", lastMessageAt: new Date(2026, 9, 29, 15, 28).toISOString(), source: "web" },
  { id: "d", title: "Telegram · Нмага килолмадим", createdAt: "", lastMessageAt: new Date(2026, 9, 20, 9, 0).toISOString() },
];

test("Telegram-чат — без приставки в названии, источник с сервера или по приставке", () => {
  assert.equal(chatTitle("Telegram · Salom"), "Salom");
  assert.equal(chatTitle("Состояние продаж"), "Состояние продаж");
  assert.equal(chatSource(chats[3]), "telegram");
  assert.deepEqual(filterChats(chats, "telegram").map((c) => c.id), ["b", "d"]);
  assert.deepEqual(filterChats(chats, "web").map((c) => c.id), ["a", "c"]);
  assert.equal(filterChats(chats, "all").length, 4);
});

test("группы истории: закреплённые сверху, затем Сегодня, Вчера, Ранее", () => {
  assert.equal(chatSection(chats[0].lastMessageAt, now), "Сегодня");
  assert.equal(chatSection(chats[2].lastMessageAt, now), "Вчера");
  assert.equal(chatSection(chats[3].lastMessageAt, now), "Ранее");
  const groups = groupChats(chats, new Set(["d"]), now);
  assert.deepEqual(groups.map((g) => [g.label, g.items.map((c) => c.id)]), [
    ["Закреплённые", ["d"]],
    ["Сегодня", ["a", "b"]],
    ["Вчера", ["c"]],
  ]);
});

test("полоса плана: факт и прогноз в процентах от плана, заливка не больше 100", () => {
  assert.deepEqual(progressView({ label: "Факт", fact: 371148, plan: 485645, forecast: 389321, unit: "кг", forecastLabel: null }), {
    fact: 76.4,
    forecast: 80.2,
    factWidth: 76.4,
    forecastWidth: 80.2,
  });
  assert.equal(progressView({ label: "", fact: 130, plan: 100, forecast: null, unit: "кг", forecastLabel: null }).factWidth, 100);
  assert.equal(progressView({ label: "", fact: 5, plan: 0, forecast: null, unit: "кг", forecastLabel: null }).fact, 0);
});

test("изменение всегда со знаком", () => {
  assert.equal(signed("-3,8%"), "−3,8%");
  assert.equal(signed("+5,2%"), "+5,2%");
  assert.equal(signed("−15,2 п.п."), "−15,2 п.п.");
});

test("быстрые вопросы и источники — по правам: без HR — без вопроса об опозданиях", () => {
  const sales = quickQuestions(["sales.read"]);
  assert.ok(!sales.some((q) => q.text.includes("опаздывает")));
  assert.equal(sales.length, 6);
  assert.ok(quickQuestions(["sales.read", "hr.read"]).some((q) => q.text === "Кто чаще всех опаздывает?"));
  assert.ok(quickQuestions(["hr.department"]).some((q) => q.text === "Кто чаще всех опаздывает?"));
  assert.deepEqual(quickQuestions([]), []);
  assert.deepEqual(sourceLabels(["sales.read", "finance.read", "hr.read"]), ["продажи", "финансы", "склад", "HR"]);
  assert.deepEqual(sourceLabels(["hr.department"]), ["HR"]);
});

test("контекст: месяц по Ташкенту, отдел из адреса, неверное не подставляется", () => {
  // 30.09.2026 19:30 UTC — в Ташкенте уже октябрь.
  assert.equal(tashkentMonth(new Date(Date.UTC(2026, 8, 30, 19, 30))), "2026-10");
  assert.deepEqual(initialContext("2026-09", "sales", now), { period: "2026-09", department: "sales", departmentName: "Продажи" });
  assert.deepEqual(initialContext("сентябрь", "unknown", new Date(Date.UTC(2026, 9, 30, 10))), { period: "2026-10", department: null, departmentName: null });
  assert.equal(contextPayload({ period: null, department: null, departmentName: null }), null);
});

test("черновик задачи из рекомендации — со ссылкой на чат, отделом и сроком", () => {
  const draft = taskDraft(
    { title: "Разобрать Ташкент по агентам", text: "агенты с падением больше 15%", owner: "супервайзер", dueDate: "2026-11-05", department: "sales" },
    { url: "https://onebase.uz/consultant/42", title: "Состояние продаж" },
  );
  assert.equal(draft.title, "Разобрать Ташкент по агентам");
  assert.equal(draft.department, "sales");
  assert.equal(draft.dueDate, "2026-11-05");
  assert.equal(draft.description, "агенты с падением больше 15%\nОтветственный: супервайзер\nИз чата AI-консультанта «Состояние продаж»: https://onebase.uz/consultant/42");
});

test("текст ответа для копирования: блоки отчёта или Markdown простого ответа", () => {
  assert.equal(answerText(null, "Привет!"), "Привет!");
  assert.equal(answerText({ report: false, blocks: [], followUps: [], meta: null }, "Ответ"), "Ответ");
  const text = answerText(
    {
      report: true,
      followUps: [],
      meta: null,
      blocks: [
        { type: "summary", text: "Выполнено 76,4% плана.", progress: null },
        { type: "facts", title: null, rows: [{ label: "Выручка", value: "13 446 260 699 сум", deltas: [{ value: "-3,8%", caption: "к сентябрю", tone: "bad" }] }] },
        { type: "recommendations", total: 1, items: [{ title: "Вернуть Урикзор", text: "встреча", owner: "РОП", dueDate: null, department: "sales" }] },
      ],
    },
    "md",
  );
  assert.equal(text, "Выполнено 76,4% плана.\n\n• Выручка: 13 446 260 699 сум (−3,8% к сентябрю)\n\n1. Вернуть Урикзор — встреча (РОП)");
});

test("строка статуса — по шагу, который идёт сейчас", () => {
  assert.equal(currentStep([]).key, "Определяю, каких AI-сотрудников привлечь");
  assert.deepEqual(currentStep([{ stage: "agent", agent: "sales", agentName: "Sales AI", status: "Running", note: null }]), {
    key: "{agent}: собираю данные",
    agentName: "Sales AI",
  });
  assert.equal(currentStep([{ stage: "compose", agent: null, agentName: null, status: "Running", note: null }]).key, "Формирую ответ");
});

test("числа одинаково на сервере и в браузере: пробел тысяч, запятая, минус", () => {
  assert.equal(formatNumber(371148), "371\u00a0148");
  assert.equal(formatNumber(13446260699), "13\u00a0446\u00a0260\u00a0699");
  assert.equal(formatNumber(76.44, 1), "76,4");
  assert.equal(formatNumber(-2.3, 1), "−2,3");
  assert.equal(formatNumber(999), "999");
});

test("русские формы по числу", () => {
  const f = (n) => plural(n, "рекомендация", "рекомендации", "рекомендаций");
  assert.deepEqual([1, 2, 5, 9, 11, 12, 21, 22, 25, 101, 111].map(f), [
    "рекомендация", "рекомендации", "рекомендаций", "рекомендаций", "рекомендаций", "рекомендаций",
    "рекомендация", "рекомендации", "рекомендаций", "рекомендация", "рекомендаций",
  ]);
});
