// Сквозная проверка Sales Base через API под тремя ролями: администратор выдаёт доступы; супервайзер строит маршрут,
// меняет агента точки, ставит задачу и совместный выезд; агент проходит свой день и визит с геопозицией; проверяется
// изоляция (403/404), задачи по ролям и AI-рекомендации.
//
// ТОЛЬКО ЛОКАЛЬНО: сценарий меняет данные (выдаёт входы, строит маршруты, переназначает точку, создаёт задачи).
// Запуск: API на http://localhost:5080, состав Sales Base импортирован, команда «Ташкент» с супервайзером.
//   node backend/tests/field-smoke/field-roles.js
// Переменные: FIELD_API (по умолчанию http://localhost:5080), FIELD_ADMIN_LOGIN / FIELD_ADMIN_PASSWORD (1 / 1), FIELD_TEAM (Ташкент).
const fs = require("fs");
const base = process.env.FIELD_API ?? "http://localhost:5080";
if (!/^https?:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/.test(base)) {
  console.error("Сценарий меняет данные — запускается только против localhost.");
  process.exit(2);
}
const adminLogin = process.env.FIELD_ADMIN_LOGIN ?? "1";
const adminPassword = process.env.FIELD_ADMIN_PASSWORD ?? "1";
const teamName = process.env.FIELD_TEAM ?? "Ташкент";
const stateFile = __dirname + "/.field-roles.state.json"; // пароли выданных входов — локально, в git не попадает

async function loginAs(login, password) {
  const res = await fetch(`${base}/api/auth/login`, { method: "POST", headers: { "content-type": "application/json" }, body: JSON.stringify({ login, password }) });
  if (!res.ok) throw new Error(`login ${login}: ${res.status}`);
  const j = await res.json();
  return { authorization: `Bearer ${j.accessToken ?? j.token}` };
}

function client(headers) {
  return async (method, path, body) => {
    const res = await fetch(base + path, { method, headers: { ...headers, "content-type": "application/json" }, body: body ? JSON.stringify(body) : undefined });
    const text = await res.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch { json = text; }
    return { status: res.status, json };
  };
}

let failures = 0;
function check(label, cond, extra) {
  if (!cond) failures++;
  console.log(`${cond ? "OK  " : "FAIL"} ${label}${extra !== undefined ? " — " + (typeof extra === "string" ? extra : JSON.stringify(extra)).slice(0, 300) : ""}`);
}

(async () => {
  const admin = client(await loginAs(adminLogin, adminPassword));

  // 1. Структура: команда Ташкента, её супервайзер и два агента.
  let s = await admin("GET", "/api/field/structure");
  const zavod = s.json.teams.find((t) => t.name === "Филиал 4");
  if (zavod) {
    const r = await admin("PUT", `/api/field/teams/${zavod.id}`, { name: "Завод", supervisorId: null, branchId: zavod.branchId, isActive: true });
    check("rename team «Филиал 4» → «Завод»", r.status === 200, r.status);
    s = await admin("GET", "/api/field/structure");
  }

  const team = s.json.teams.find((t) => t.name === teamName);
  const agents = s.json.members.filter((m) => m.teamId === team.id && m.role === "Agent").sort((a, b) => b.markets - a.markets);
  const sv = s.json.members.find((m) => m.id === team.supervisorId);
  const [a, b] = agents;
  check(`team ${teamName} has supervisor and ≥2 agents`, !!sv && agents.length >= 2, { sv: sv?.fullName, agents: agents.length, a: a.fullName, b: b.fullName });

  // 2. Доступы (пароль показывается один раз).
  let state = fs.existsSync(stateFile) ? JSON.parse(fs.readFileSync(stateFile, "utf8")) : {};
  for (const m of [sv, a]) {
    if (!state[m.id]) {
      const r = await admin("POST", `/api/field/members/${m.id}/access`, {});
      check(`grant access ${m.fullName}`, r.status === 200 && r.json.password?.length >= 8, r.json);
      state[m.id] = r.json;
    }
  }
  fs.writeFileSync(stateFile, JSON.stringify(state));

  const svc = client(await loginAs(state[sv.id].login, state[sv.id].password));
  const ag = client(await loginAs(state[a.id].login, state[a.id].password));

  // 3. Супервайзер: только своя команда.
  const svMe = await svc("GET", "/api/field/me");
  check("supervisor me: role Supervisor", svMe.json.role === "Supervisor", svMe.json.role);
  const svDash = await svc("GET", "/api/field/dashboard?date=2026-10-02");
  check("supervisor dashboard lists only team agents", svDash.status === 200 && svDash.json.agentRows.every((r) => r.teamId === team.id), { agents: svDash.json.agents, sum: svDash.json.today?.sum });
  const svCustomers = await svc("GET", "/api/field/customers?pageSize=5");
  const allCustomers = await admin("GET", "/api/field/customers?pageSize=5");
  check("supervisor sees fewer customers than org", svCustomers.json.total > 0 && svCustomers.json.total < allCustomers.json.total, { team: svCustomers.json.total, org: allCustomers.json.total });
  const other = s.json.members.find((m) => m.role === "Agent" && m.teamId !== team.id);
  const svOther = await svc("GET", `/api/field/agents/${other.id}`);
  check("supervisor cannot open agent of other team (404)", svOther.status === 404, svOther.status);

  // Маршрут агента A на сегодня.
  const built = await svc("POST", "/api/field/routes/build", { agentId: a.id });
  check("supervisor builds route for agent", built.status === 200 && built.json.points.length > 0, { status: built.status, points: built.json?.points?.length, km: built.json?.distanceKm, min: built.json?.estimatedMinutes, source: built.json?.source, err: built.json?.error });

  // Смена агента точки: вторая точка маршрута A → агенту B.
  // Непосещённая точка: посещённые остаются в истории маршрута и при смене агента.
  const moved = built.json.points.find((p, i) => i > 0 && p.status === "Planned");
  const change = await svc("POST", `/api/field/customers/${moved.marketId}/agent`, { agentId: b.id });
  check("supervisor changes agent of a store", change.status === 200 && change.json.toAgentId === b.id, change.json);
  const afterRoute = await svc("GET", `/api/field/routes/${built.json.id}`);
  check("store removed from old agent route and resequenced", afterRoute.json.points.every((p) => p.marketId !== moved.marketId) && afterRoute.json.points.every((p, i) => p.sequence === i + 1),
    { before: built.json.points.length, after: afterRoute.json.points.length });

  // Задача агенту A по первой точке маршрута.
  const first = afterRoute.json.points.find((p) => p.status === "Planned") ?? afterRoute.json.points[0];
  const task = await svc("POST", "/api/field/tasks", { title: "Проверить выкладку и остатки", description: "Тест Sales Base", assignedToId: a.id, marketId: first.marketId, priority: "High", dueDate: new Date().toISOString().slice(0, 10) });
  check("supervisor creates task for agent", task.status === 200, task.json);

  // Совместный выезд супервайзер + агент.
  const joint = await svc("POST", "/api/field/visits/joint", { date: new Date(Date.now() + 864e5).toISOString().slice(0, 10), time: "10:00", marketId: first.marketId, objective: "Обучение работе с выкладкой", participantIds: [sv.id, a.id] });
  check("supervisor creates joint visit", joint.status === 200, joint.json);

  // 4. Агент: свой день, изоляция, визит.
  const agMe = await ag("GET", "/api/field/me");
  check("agent me: role Agent", agMe.json.role === "Agent" && agMe.json.supervisorName === sv.fullName, { role: agMe.json.role, supervisor: agMe.json.supervisorName });
  const today = await ag("GET", "/api/field/today");
  check("agent today: route and tasks", today.status === 200 && today.json.route?.points?.length > 0 && today.json.tasksOpen >= 1, { orders: today.json.today, route: today.json.route?.points?.length, tasks: today.json.tasksOpen });
  check("agent cannot open dashboard (403)", (await ag("GET", "/api/field/dashboard")).status === 403);
  check("agent cannot open other agent (404)", (await ag("GET", `/api/field/agents/${b.id}`)).status === 404);
  check("agent cannot open moved store (404)", (await ag("GET", `/api/field/customers/${moved.marketId}`)).status === 404);
  check("agent cannot build route for other agent (404)", (await ag("POST", "/api/field/routes/build", { agentId: b.id })).status === 404);
  check("agent cannot change store agent (403/404)", [403, 404].includes((await ag("POST", `/api/field/customers/${first.marketId}/agent`, { agentId: b.id })).status));
  check("agent cannot read structure of others", (await ag("GET", "/api/field/structure")).json.members.length === 1);
  const agCustomers = await ag("GET", "/api/field/customers?pageSize=5");
  check("agent sees only own stores", agCustomers.json.items.every((c) => c.agentId === a.id), { total: agCustomers.json.total });

  // Визит на первой точке: координаты в ~40 м от точки.
  const point = today.json.route.points.find((p) => p.status === "Planned" && p.lat);
  const start = await ag("POST", "/api/field/visits/start", { marketId: point.marketId, routePointId: point.id, latitude: point.lat + 0.0003, longitude: point.lon, accuracyM: 25 });
  check("agent starts visit near store (geo Ok)", start.status === 200 && start.json.geoStatus === "Ok", { status: start.status, geo: start.json?.geoStatus, dist: start.json?.distanceM, err: start.json?.error });
  const second = await ag("POST", "/api/field/visits/start", { marketId: point.marketId, latitude: point.lat, longitude: point.lon });
  check("second visit while one is active → 409", second.status === 409, second.status);
  const finish = await ag("POST", `/api/field/visits/${start.json.id}/finish`, { result: "Order", amount: 1250000, comment: "Заказ на неделю" });
  check("agent finishes visit with order", finish.status === 200 && finish.json.status === "Completed", { minutes: finish.json?.minutes, result: finish.json?.result });
  const routeNow = await ag("GET", `/api/field/routes/${today.json.route.id}`);
  check("route point marked visited", routeNow.json.points.find((p) => p.id === point.id)?.status === "Visited", routeNow.json.points.find((p) => p.id === point.id)?.status);

  // Визит далеко от точки — не блокируется, но отмечается.
  const far = today.json.route.points.find((p) => p.status === "Planned" && p.lat && p.id !== point.id);
  if (far) {
    const farStart = await ag("POST", "/api/field/visits/start", { marketId: far.marketId, routePointId: far.id, latitude: far.lat + 0.05, longitude: far.lon, accuracyM: 30 });
    check("far visit allowed but flagged Far", farStart.status === 200 && farStart.json.geoStatus === "Far", { geo: farStart.json?.geoStatus, dist: farStart.json?.distanceM });
    const cancel = await ag("POST", `/api/field/visits/${farStart.json.id}/cancel`);
    check("agent cancels visit", cancel.status === 204, cancel.status);
  }

  // Задача: агент принимает и выполняет, супервайзер подтверждает.
  const myTask = (await ag("GET", "/api/field/tasks")).json.items.find((t) => t.title === "Проверить выкладку и остатки");
  check("agent sees assigned task", !!myTask, myTask?.nextStatuses);
  check("agent cannot verify task", (await ag("POST", `/api/field/tasks/${myTask.id}/status`, { status: "Verified" })).status === 400);
  check("agent accepts task", (await ag("POST", `/api/field/tasks/${myTask.id}/status`, { status: "Accepted" })).status === 204);
  check("agent completes task", (await ag("POST", `/api/field/tasks/${myTask.id}/status`, { status: "Completed", result: "Выкладка исправлена" })).status === 204);
  check("supervisor verifies task", (await svc("POST", `/api/field/tasks/${myTask.id}/status`, { status: "Verified" })).status === 204);

  const notes = await ag("GET", "/api/field/notifications");
  check("agent got notifications", notes.json.items.length > 0, notes.json.items.map((n) => n.title).slice(0, 5));

  // 5. AI: генерация, подтверждение, отклонение.
  const gen = await svc("POST", "/api/field/ai/recommendations/generate");
  check("supervisor generates recommendations", gen.status === 200, gen.json);
  const recs = await svc("GET", "/api/field/ai/recommendations");
  check("pending recommendations visible", recs.status === 200 && recs.json.total >= 0, { total: recs.json.total, kinds: [...new Set(recs.json.items.map((r) => r.kind))] });
  const toApprove = recs.json.items.find((r) => r.canDecide && r.kind !== "Reassign" && r.agentId);
  if (toApprove) {
    const ap = await svc("POST", `/api/field/ai/recommendations/${toApprove.id}/approve`, { priority: "High" });
    check("approve recommendation → task", ap.status === 200 && ap.json.taskId, { kind: ap.json.kind, task: ap.json.taskId, reason: toApprove.reason });
  }

  const toReject = recs.json.items.find((r) => r.canDecide && r.id !== toApprove?.id);
  if (toReject) {
    const rj = await svc("POST", `/api/field/ai/recommendations/${toReject.id}/reject`, { note: "Точка на ремонте" });
    check("reject recommendation", rj.status === 200 && rj.json.status === "Rejected", rj.json.status);
  }

  check("agent cannot generate recommendations (403)", (await ag("POST", "/api/field/ai/recommendations/generate")).status === 403);
  const audit = await admin("GET", "/api/field/audit?take=20");
  check("audit log has field actions", audit.status === 200 && audit.json.some((x) => x.action === "field.customer.agent_changed"), audit.json.slice(0, 6).map((x) => x.action));

  console.log(failures === 0 ? "\nALL CHECKS PASSED" : `\n${failures} CHECK(S) FAILED`);
  process.exitCode = failures === 0 ? 0 : 1;
})().catch((e) => console.error(e));
