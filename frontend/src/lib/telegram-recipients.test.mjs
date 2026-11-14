// «Автоотчёты → Получатели» (lib/telegram-recipients.ts): node --test src/lib/telegram-recipients.test.mjs.
import assert from "node:assert/strict";
import { test } from "node:test";
import {
  NO_FILTERS,
  activeCount,
  addReports,
  attention,
  disable,
  displayName,
  enable,
  facets,
  filtersQuery,
  groupRows,
  inputOf,
  issuesOf,
  lastSendOf,
  matches,
  matrix,
  nearSends,
  parseFilters,
  isProblem,
  regionIdsOf,
  reportSummary,
  scopeProblem,
  setTimes,
  sortRows,
  templateCodes,
  templateOf,
  timesOf,
} from "./telegram-recipients.ts";

const ALL = [1, 2, 3, 4, 5, 6, 7];
const WORK = [1, 2, 3, 4, 5, 6];
const job = (code, report, slot, time, roles, days = WORK, extra = {}) => ({
  code, report, reportTitle: `${report}`, slot, slotTitle: slot === "evening" ? "итог дня" : slot, roles, enabled: true, time, timeZone: "Asia/Tashkent", days,
  catchUpMinutes: 60, nextRunAt: null, lastRun: null, recipients: 0, updatedAt: null, ...extra,
});
const jobs = [
  job("daily.morning", "daily", "morning", "08:00", ["rm", "sv", "tp"]),
  job("daily.midday", "daily", "midday", "13:00", ["rm", "sv", "tp"]),
  job("daily.evening", "daily", "evening", "18:00", ["rm", "sv", "tp"]),
  job("no-orders.midday", "no-orders", "midday", "13:00", ["rm", "sv"]),
  job("no-orders.reminder", "no-orders", "reminder", "17:00", ["sv", "tp"]),
  job("no-orders.evening", "no-orders", "evening", "18:00", ["rm", "sv"]),
  job("weekly.saturday", "weekly", "saturday", "18:00", ["rm", "sv", "tp"], [6]),
  job("weekly.monday", "weekly", "monday", "08:30", ["rm", "sv", "tp"], [1]),
  job("battle.evening", "battle", "evening", "18:00", ["sv", "tp"]),
  job("weekly-pdf.monday", "weekly-pdf", "monday", "09:00", ["rm", "sv", "tp"], [1]),
];
const regions = [
  { id: "nav", name: "Навои", directionId: "d1", manager: null, supervisorName: "Шахзод К.", supervisors: [], agents: [] },
  { id: "bux", name: "Бухара", directionId: "d1", manager: null, supervisorName: null, supervisors: [], agents: [] },
  { id: "tash", name: "Ташкент", directionId: "d2", manager: null, supervisorName: null, supervisors: [], agents: [] },
];
const chat = (id, canSend = true) => ({ id, title: `Чат ${id}`, type: "Private", canSend, peerId: 1, linked: null });
const now = new Date("2026-11-14T10:00:00Z"); // 15:00 по Ташкенту
const base = {
  scope: "", directionId: null, regionId: null, region: null, supervisorId: null, agentId: null, agents: 0, chats: [chat("c1")], telegramStatus: "chat",
  reports: ["daily.evening"], enabled: true, problem: null, last: [], pdfLanguage: null, language: null, supervisor: null, lastSentAt: "2026-11-14T08:05:00Z", failedWeek: 0,
};
const r = (id, role, name, extra = {}) => ({ ...base, id, role, name, scope: name, ...extra });
const ilhom = r("rm1", "rm", "Ильхом Рахимов", { directionId: "d1", reports: ["daily.evening", "weekly.saturday"] });
const shahzod = r("sv1", "sv", "Шахзод Кувондиков", { regionId: "nav", region: "Навои", supervisor: "Шахзод К.", reports: ["daily.evening", "battle.evening"] });
const aziz = r("tp1", "tp", "Азиз Каримов", { regionId: "nav", region: "Навои", supervisor: "Шахзод К.", chats: [], telegramStatus: "none", lastSentAt: null });
const nodira = r("tp2", "tp", "Нодира Саидова", { regionId: "nav", region: "Навои", supervisor: "Шахзод К.", chats: [chat("c9", false)], telegramStatus: "blocked" });
const jamshid = r("tp3", "tp", "Жамшид Олимов", { regionId: "nav", region: "Навои", supervisor: "Шахзод К.", enabled: false, disabledAt: "2026-11-01T05:00:00Z", disabledReason: "отпуск", disabledUntil: "2026-11-10" });
const bekzod = r("tp4", "tp", "Бекзод Тураев", { regionId: "bux", region: "Бухара", supervisor: null, failedWeek: 2, lastSentAt: "2026-11-03T13:00:00Z" });
const all = [ilhom, shahzod, aziz, nodira, jamshid, bekzod];
const ctx = { regions, jobs, now };

test("фильтры из адреса и обратно; чужое — по умолчанию", () => {
  assert.deepEqual(parseFilters({}), NO_FILTERS);
  const f = parseFilters(new URLSearchParams("q=Азиз&role=tp&region=nav,bux,nav&report=7&slot=battle.evening&status=active&issue=no_chat,x,disabled&last=7d&sort=name&group=0"));
  assert.deepEqual(f, { q: "Азиз", role: "tp", regions: ["nav", "bux"], report: 7, slot: "battle.evening", status: "active", issues: ["no_chat", "disabled"], last: "7d", sort: "name", flat: true });
  assert.equal(filtersQuery(f), "q=%D0%90%D0%B7%D0%B8%D0%B7&role=tp&region=nav%2Cbux&report=7&slot=battle.evening&status=active&issue=no_chat%2Cdisabled&last=7d&sort=name&group=0");
  assert.deepEqual(parseFilters(new URLSearchParams(filtersQuery(f))), f);
  assert.equal(filtersQuery(NO_FILTERS), "");
  assert.deepEqual(parseFilters({ role: "boss", report: "12", slot: "x", status: "on", sort: "x" }), NO_FILTERS); // slot без отчёта — нет
  assert.equal(activeCount(f), 2 + 2 + 1 + 1 + 1);
  assert.equal(activeCount({ ...NO_FILTERS, role: "tp", q: "x", regions: ["nav"], issues: ["no_chat"] }), 2); // как «Фильтры 2» на телефоне
});

test("проблемы получателя и регионы РМ", () => {
  assert.deepEqual(issuesOf(aziz), ["no_chat"]);
  assert.deepEqual(issuesOf(nodira), ["no_chat"]); // в единственный чат писать нельзя
  assert.deepEqual(issuesOf(jamshid), ["disabled"]);
  assert.deepEqual(issuesOf(bekzod), ["delivery_error", "no_supervisor"]);
  assert.deepEqual(issuesOf(shahzod), []);
  assert.deepEqual(regionIdsOf(ilhom, regions), ["nav", "bux"]);
  assert.deepEqual(regionIdsOf({ ...ilhom, directionId: null }, regions), ["nav", "bux", "tash"]);
});

test("РМ — пользователь с ролью «РМ»: регионы из профиля, имя, группа, сохранение, причина с сервера", () => {
  const rmUsers = [{ id: "u1", name: "Рустам Алиев", regionIds: ["tash", "bux", "gone"], agents: 12 }];
  const rustam = r("rm2", "rm", "РМ Рустам Алиев", { userId: "u1" });
  // Только регионы профиля, по порядку оргструктуры; регион без данных месяца («gone») не показывается.
  assert.deepEqual(regionIdsOf(rustam, regions, rmUsers), ["bux", "tash"]);
  assert.deepEqual(regionIdsOf(rustam, regions, []), []); // роль сняли — регионов нет, а не «вся страна»
  assert.equal(displayName(rustam, regions), "Рустам Алиев");
  assert.equal(displayName({ ...rustam, name: "Руководство" }, regions), "Руководство"); // своя подпись — как есть
  const groups = groupRows([rustam, { ...ilhom, directionId: null }], NO_FILTERS, { ...ctx, rmUsers });
  assert.deepEqual(groups.map((g) => [g.key, g.rows.map((x) => x.id)]), [["country", ["rm1"]], ["bux", ["rm2"]]]);
  assert.deepEqual(
    all.concat(rustam).filter((x) => matches(x, { ...NO_FILTERS, regions: ["tash"] }, { ...ctx, rmUsers })).map((x) => x.id),
    ["rm2"],
  );
  assert.equal(inputOf(rustam).userId, "u1");
  assert.equal(inputOf(ilhom).userId, null);
  // Причина с сервера (РМ потерял роль) — у включённого с чатом; без чата её показывает «нет чата», у выключенного — не проблема.
  const lost = { ...rustam, problem: "Пользователь больше не РМ или отключён — проверьте роль и регионы в окне пользователя." };
  assert.equal(scopeProblem(lost), lost.problem);
  assert.equal(isProblem(lost), true);
  assert.equal(scopeProblem({ ...lost, enabled: false }), null);
  assert.equal(scopeProblem({ ...lost, chats: [] }), null);
  assert.equal(isProblem(rustam), false);
});

test("поиск, роль, регионы, отчёт и запуск, статус, проблемы, последняя отправка", () => {
  const pick = (f) => all.filter((x) => matches(x, { ...NO_FILTERS, ...f }, ctx)).map((x) => x.id);
  assert.deepEqual(pick({ q: "навои шахзод" }), ["sv1", "tp1", "tp2", "tp3"]);
  assert.deepEqual(pick({ q: "чат c1", role: "rm" }), ["rm1"]);
  assert.deepEqual(pick({ regions: ["bux"] }), ["rm1", "tp4"]); // РМ направления — и в Бухаре
  assert.deepEqual(pick({ report: 7 }), ["sv1"]);
  assert.deepEqual(pick({ report: 6, slot: "weekly.saturday" }), ["rm1"]);
  assert.deepEqual(pick({ status: "disabled" }), ["tp3"]);
  assert.deepEqual(pick({ issues: ["no_chat", "no_supervisor"] }), ["tp1", "tp2", "tp4"]);
  assert.deepEqual(pick({ last: "today" }), ["rm1", "sv1", "tp2", "tp3"]);
  assert.deepEqual(pick({ last: "7d" }), ["rm1", "sv1", "tp2", "tp3"]);
  assert.deepEqual(pick({ last: "never" }), ["tp1"]);
});

test("счётчики на кнопках — без своего фильтра", () => {
  const f = { ...NO_FILTERS, role: "tp", regions: ["nav"], issues: ["no_chat"] };
  const x = facets(all, f, ctx);
  assert.equal(x.total, 2);
  assert.deepEqual(x.role, { all: 2, rm: 0, sv: 0, tp: 2 }); // роль — по остальным фильтрам
  assert.deepEqual([x.region.nav, x.region.bux, x.region.tash], [2, 0, 0]);
  assert.deepEqual(x.issue, { no_chat: 2, disabled: 1, delivery_error: 0, no_supervisor: 0 });
  assert.equal(x.report[1], 2);
});

test("группы по регионам: РМ — в первом регионе направления, порядок РМ → СВ → ТП, проблемные сверху", () => {
  const groups = groupRows(all, NO_FILTERS, ctx);
  assert.deepEqual(groups.map((g) => [g.name, g.rows.map((x) => x.id)]), [
    ["Навои", ["tp1", "tp3", "tp2", "rm1", "sv1"]],
    ["Бухара", ["tp4"]],
  ]);
  assert.deepEqual(groups[0].counts, { rm: 1, sv: 1, tp: 3 });
  assert.deepEqual([groups[0].noChat, groups[0].disabled, groups[1].noSupervisor], [2, 1, 1]);
  assert.deepEqual(sortRows(all, "region").map((x) => x.id), ["rm1", "sv1", "tp1", "tp4", "tp3", "tp2"]);
  assert.deepEqual(sortRows(all, "name").map((x) => x.name)[0], "Азиз Каримов");
  assert.deepEqual(groupRows([{ ...ilhom, directionId: null }], NO_FILTERS, ctx).map((g) => g.name), ["Вся страна"]);
  assert.deepEqual(groupRows(all, { ...NO_FILTERS, flat: true }, ctx).map((g) => g.rows.length), [6]);
});

test("матрица 1–9, сводка «N из 9 · M в день», шаблоны", () => {
  const m = matrix(shahzod, jobs);
  assert.equal(m.length, 9);
  assert.deepEqual(m.filter((c) => c.on).map((c) => c.n), [1, 7]);
  assert.deepEqual(m.find((c) => c.n === 3).slots.map((s) => s.code), ["no-orders.midday", "no-orders.reminder", "no-orders.evening"]);
  assert.deepEqual(matrix(ilhom, jobs).find((c) => c.n === 7).slots, []); // «Поле боя» РМ не положено
  assert.deepEqual(reportSummary(shahzod.reports, "sv", jobs), { on: 2, of: 5, perDay: 2 });
  assert.deepEqual(templateCodes("role", jobs, "sv"), ["daily.morning", "daily.midday", "daily.evening", "no-orders.midday", "no-orders.evening", "weekly.saturday", "battle.evening"]);
  assert.ok(templateCodes("role", jobs, "tp").includes("no-orders.reminder")); // напоминание 17:00 — только ТП
  assert.ok(!templateCodes("role", jobs, "rm").includes("no-orders.reminder"));
  assert.deepEqual(templateCodes("evening", jobs, "tp"), ["daily.evening", "battle.evening"]);
  assert.equal(templateOf(templateCodes("all", jobs, "tp"), jobs, "tp"), "all");
  assert.equal(templateOf(["daily.evening"], jobs, "tp"), null);
  assert.equal(templateOf([], jobs, "tp"), "clear");
});

test("последняя отправка", () => {
  assert.equal(lastSendOf(jamshid, jobs).kind, "off");
  assert.equal(lastSendOf(jamshid, jobs).error, "отпуск");
  assert.equal(lastSendOf(aziz, jobs).kind, "nochat");
  const fresh = { ...shahzod, last: [{ report: "daily.evening", chat: "Чат", status: "sent", at: "2026-11-14T13:00:00Z", error: null }] };
  assert.equal(lastSendOf(fresh, jobs).job.code, "daily.evening");
  assert.equal(lastSendOf({ ...fresh, last: [{ ...fresh.last[0], status: "sending" }] }, jobs).kind, "pending");
  assert.deepEqual(lastSendOf(bekzod, jobs), { kind: "sent", at: "2026-11-03T13:00:00Z", job: null, error: null }); // журнал старше окна — последняя доставка
  assert.equal(lastSendOf({ ...bekzod, lastSentAt: null }, jobs).kind, "none");
});

test("ближайшие отправки и «Требуют внимания»", () => {
  const at = (iso) => ({ nextRunAt: iso });
  const planned = [
    { ...jobs[2], ...at("2026-11-14T13:00:00Z") },
    { ...jobs[8], ...at("2026-11-14T13:00:00Z") },
    { ...jobs[0], ...at("2026-11-15T03:00:00Z") },
    { ...jobs[7], ...at("2026-11-16T03:30:00Z") },
    { ...jobs[1], ...at("2026-11-15T08:00:00Z"), enabled: false },
  ];
  const near = nearSends(planned, all, now);
  assert.deepEqual(near.map((n) => [n.time, n.day, n.jobs.map((j) => j.code), n.recipients]), [
    ["18:00", "today", ["daily.evening", "battle.evening"], 3], // без чата и выключенные не в счёт
    ["08:00", "tomorrow", ["daily.morning"], 0],
    ["08:30", "later", ["weekly.monday"], 0],
  ]);
  const a = attention(all, { sent: 0, failed: 0, skipped: 5, unknown: 0, pending: 0, skippedBy: [{ key: "no_chat", count: 412 }] }, now);
  assert.deepEqual([a.noChat.length, a.noSupervisor.length, a.disabled.length, a.overdue.length, a.failing.length, a.skippedNoChat], [2, 1, 1, 1, 1, 412]);
});

test("массовые действия: отчёт, время, отключение", () => {
  const input = inputOf(shahzod);
  assert.deepEqual(addReports(input, ["weekly.saturday", "battle.evening"], jobs).reports, ["daily.evening", "battle.evening", "weekly.saturday"]);
  assert.deepEqual(addReports(inputOf(ilhom), ["battle.evening"], jobs).reports, ilhom.reports); // РМ «Поле боя» не положено
  // 13:00 включено — промежуточные запуски включённых отчётов (дневной план), не всех; 18:00 выключено — снимаются.
  assert.deepEqual(setTimes(input, { "13:00": true }, jobs).reports, ["daily.evening", "battle.evening", "daily.midday"]);
  assert.deepEqual(setTimes(input, { "18:00": false }, jobs).reports, []);
  assert.deepEqual(timesOf([input, inputOf(ilhom)], jobs).filter((x) => x.state !== "off").map((x) => [x.time, x.state]), [["18:00", "on"]]);
  const off = disable(input, "  отпуск ", "2026-11-20");
  assert.deepEqual([off.enabled, off.disabledReason, off.disabledUntil], [false, "отпуск", "2026-11-20"]);
  assert.deepEqual([enable(off).enabled, enable(off).disabledReason, enable(off).disabledUntil], [true, null, null]);
  assert.deepEqual([inputOf(jamshid).disabledReason, inputOf(jamshid).disabledUntil], ["отпуск", "2026-11-10"]);
});
