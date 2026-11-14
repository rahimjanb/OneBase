// «Настройки → Telegram → Автоматические отчёты → Получатели»: фильтры (в адресе), счётчики на кнопках, группы по регионам, матрица
// отчётов 1–9, последняя отправка, шаблоны отчётов панели, ближайшие отправки и «Требуют внимания». Без интерфейса — проверяется тестами.
import type { ReportChat, ReportJob, ReportMetrics, ReportRecipient, ReportRecipientInput, ReportRegion, ReportRmUser, ReportRole, ReportType, SkipKey } from "./telegramReports";

/** Отчёты 1–9 — номера как в расписании и на картинках; short — для легенды и фильтров. */
export const REPORTS: { type: ReportType; n: number; title: string; short: string }[] = [
  { type: "daily", n: 1, title: "Дневной план", short: "план" },
  { type: "no-purchase", n: 2, title: "Клиенты без закупа", short: "без закупа" },
  { type: "no-orders", n: 3, title: "ТП без заказов", short: "без заказов" },
  { type: "promo", n: 4, title: "Заказы с акцией", short: "акции" },
  { type: "categories", n: 5, title: "Заказы по категориям", short: "категории" },
  { type: "weekly", n: 6, title: "Итоги недели", short: "итоги нед." },
  { type: "battle", n: 7, title: "Поле боя", short: "поле боя" },
  { type: "weekly-pdf", n: 8, title: "Еженедельный PDF", short: "PDF" },
  { type: "incentive", n: 9, title: "Накопительная акция", short: "накопит." },
];

export const reportByN = (n: number) => REPORTS.find((r) => r.n === n) ?? null;
export const reportOfType = (type: string) => REPORTS.find((r) => r.type === type) ?? null;

const ROLES: ReportRole[] = ["rm", "sv", "tp"];
const roleOrder = (role: string) => Math.max(0, ROLES.indexOf(role as ReportRole));

// ---------- Фильтры ----------

/** Проблемы получателя: нет чата (ни в один нельзя писать), выключен, ошибки доставки за 7 дней, ТП без супервайзера. */
export type Issue = "no_chat" | "disabled" | "delivery_error" | "no_supervisor";
export const ISSUES: Issue[] = ["no_chat", "disabled", "delivery_error", "no_supervisor"];

export type LastFilter = "today" | "7d" | "never";
export const LAST_FILTERS: LastFilter[] = ["today", "7d", "never"];

export type SortKey = "problems" | "region" | "name";
export const SORTS: SortKey[] = ["problems", "region", "name"];

export type RecipientFilters = {
  q: string;
  role: ReportRole | null;
  /** Регионы — несколько; РМ попадает, если его направление покрывает хотя бы один. */
  regions: string[];
  /** Отчёт 1–9 и, по желанию, запуск (код: «daily.evening»). */
  report: number | null;
  slot: string | null;
  status: "active" | "disabled" | null;
  /** Быстрые фильтры проблем — любая из выбранных. */
  issues: Issue[];
  last: LastFilter | null;
  sort: SortKey;
  /** Без группировки по регионам. */
  flat: boolean;
};

export const NO_FILTERS: RecipientFilters = { q: "", role: null, regions: [], report: null, slot: null, status: null, issues: [], last: null, sort: "problems", flat: false };

type Params = URLSearchParams | Record<string, string | string[] | undefined>;

const read = (params: Params, key: string): string | null => {
  if (params instanceof URLSearchParams) return params.get(key);
  const v = params[key];
  return (Array.isArray(v) ? v[0] : v) ?? null;
};

const list = (v: string | null) => (v ? v.split(",").map((x) => x.trim()).filter(Boolean) : []);

/** Фильтры из адреса; чужое и неверное — по умолчанию. */
export function parseFilters(params: Params): RecipientFilters {
  const role = read(params, "role");
  const report = Number(read(params, "report"));
  const status = read(params, "status");
  const last = read(params, "last");
  const sort = read(params, "sort");
  const hasReport = Number.isInteger(report) && report >= 1 && report <= 9;
  return {
    q: (read(params, "q") ?? "").slice(0, 100),
    role: ROLES.includes(role as ReportRole) ? (role as ReportRole) : null,
    regions: [...new Set(list(read(params, "region")))],
    report: hasReport ? report : null,
    slot: hasReport ? read(params, "slot") || null : null,
    status: status === "active" || status === "disabled" ? status : null,
    issues: ISSUES.filter((i) => list(read(params, "issue")).includes(i)),
    last: LAST_FILTERS.includes(last as LastFilter) ? (last as LastFilter) : null,
    sort: SORTS.includes(sort as SortKey) ? (sort as SortKey) : "problems",
    flat: read(params, "group") === "0",
  };
}

/** Фильтры в адрес: только отличные от умолчания (вкладка — отдельно). */
export function filtersQuery(f: RecipientFilters): string {
  const p = new URLSearchParams();
  if (f.q.trim()) p.set("q", f.q.trim());
  if (f.role) p.set("role", f.role);
  if (f.regions.length) p.set("region", f.regions.join(","));
  if (f.report) p.set("report", String(f.report));
  if (f.report && f.slot) p.set("slot", f.slot);
  if (f.status) p.set("status", f.status);
  if (f.issues.length) p.set("issue", f.issues.join(","));
  if (f.last) p.set("last", f.last);
  if (f.sort !== "problems") p.set("sort", f.sort);
  if (f.flat) p.set("group", "0");
  return p.toString();
}

/** Сколько фильтров включено в «Фильтрах» (поиск и роль — свои поля, их нет в счёте). */
export const activeCount = (f: RecipientFilters) => f.regions.length + f.issues.length + (f.report ? 1 : 0) + (f.status ? 1 : 0) + (f.last ? 1 : 0);

/** Есть ли что сбрасывать («Сбросить фильтры»). */
export const isFiltered = (f: RecipientFilters) => activeCount(f) > 0 || !!f.q.trim() || !!f.role;

export type Ctx = {
  regions: ReportRegion[];
  jobs: ReportJob[];
  /** Пользователи с ролью «РМ» — регионы РМ-получателя из профиля. */
  rmUsers?: ReportRmUser[] | null;
  /** Чаты аккаунта — для поиска по @username. */
  chats?: ReportChat[];
  now: Date;
};

/** В чат получателя можно писать хотя бы в один. */
export const hasChat = (r: Pick<ReportRecipient, "chats">) => r.chats.some((c) => c.canSend);

export function issuesOf(r: ReportRecipient): Issue[] {
  const out: Issue[] = [];
  if (!hasChat(r)) out.push("no_chat");
  if (!r.enabled) out.push("disabled");
  if ((r.failedWeek ?? 0) > 0) out.push("delivery_error");
  if (r.role === "tp" && !r.supervisor) out.push("no_supervisor");
  return out;
}

/** Имя в списке: своя подпись; без неё ТП — по имени из «Продаж», СВ — по супервайзеру, РМ-пользователь — по имени, иначе — область. */
export function displayName(r: ReportRecipient, regions: ReportRegion[]): string {
  if (r.name !== r.scope) return r.name;
  if (r.role === "rm" && r.userId) return r.name.replace(/^РМ\s+/, "") || r.name;
  if (r.role === "tp") {
    const agent = regions.flatMap((x) => x.agents).find((a) => a.id === r.agentId);
    return agent?.name ?? r.name.replace(/^ТП\s+/, "").replace(/\s+·\s+[^·]+$/, "");
  }

  return r.role === "sv" && r.supervisor ? r.supervisor : r.name;
}

export const nameOf = (r: ReportRecipient) => r.display ?? r.name;

/**
 * Причина с сервера, почему отчёт включённого получателя не уйдёт, кроме «нет чата» (её показывает колонка чатов): область не строится
 * (РМ потерял роль, в профиле нет регионов с ТП, ТП нет в командах месяца), в один из чатов писать нельзя, не выбран ни один отчёт.
 */
export const scopeProblem = (r: Pick<ReportRecipient, "enabled" | "problem" | "chats">) => (r.enabled && r.problem && hasChat(r) ? r.problem : null);

/** Проблема, из-за которой отчёт сейчас не уходит (выключенный — не проблема, а решение). */
export const isProblem = (r: ReportRecipient) => issuesOf(r).some((i) => i !== "disabled") || !!scopeProblem(r);

/** Регионы получателя: СВ и ТП — свой, РМ-пользователь — из профиля, РМ — регионы направления (без направления и пользователя — все). */
export function regionIdsOf(
  r: Pick<ReportRecipient, "role" | "regionId" | "directionId" | "userId">,
  regions: ReportRegion[],
  rmUsers?: ReportRmUser[] | null,
): string[] {
  if (r.role === "rm" && r.userId) {
    const own = new Set(rmUsers?.find((u) => u.id === r.userId)?.regionIds ?? []);
    return regions.filter((x) => own.has(x.id)).map((x) => x.id);
  }

  if (r.role === "rm") return regions.filter((x) => !r.directionId || x.directionId === r.directionId).map((x) => x.id);
  return r.regionId ? [r.regionId] : [];
}

/** Типы отчётов, у которых получатель подписан хотя бы на один запуск. */
export function reportTypesOf(r: Pick<ReportRecipient, "reports">, jobs: ReportJob[]): Set<string> {
  return new Set(jobs.filter((j) => r.reports.includes(j.code)).map((j) => j.report));
}

/** День по Ташкенту (yyyy-mm-dd) — так считает и сервер. */
export const tashkentDay = (d: Date) => d.toLocaleDateString("sv-SE", { timeZone: "Asia/Tashkent" });

function lastMatches(r: ReportRecipient, last: LastFilter, now: Date): boolean {
  if (last === "never") return !r.lastSentAt;
  if (!r.lastSentAt) return false;
  const at = new Date(r.lastSentAt);
  return last === "today" ? tashkentDay(at) === tashkentDay(now) : now.getTime() - at.getTime() <= 7 * 86_400_000;
}

type Dim = "role" | "regions" | "report" | "status" | "issues" | "last";

/** Подходит ли получатель под фильтры; skip — измерение, по которому считаются счётчики (его фильтр не применяется). */
export function matches(r: ReportRecipient, f: RecipientFilters, ctx: Ctx, skip?: Dim): boolean {
  const q = f.q.trim().toLowerCase();
  if (q) {
    const usernames = ctx.chats ? r.chats.map((c) => ctx.chats!.find((x) => x.id === c.id)?.username ?? "") : [];
    const hay = [nameOf(r), r.scope, r.region ?? "", r.supervisor ?? "", ...r.chats.map((c) => c.title), ...usernames.map((u) => (u ? `@${u}` : ""))].join(" ").toLowerCase();
    if (!q.split(/\s+/).every((word) => hay.includes(word.replace(/^@/, "")) || hay.includes(word))) return false;
  }

  if (skip !== "role" && f.role && r.role !== f.role) return false;
  if (skip !== "regions" && f.regions.length && !regionIdsOf(r, ctx.regions, ctx.rmUsers).some((id) => f.regions.includes(id))) return false;
  if (skip !== "report" && f.report) {
    const type = reportByN(f.report)?.type;
    if (f.slot ? !r.reports.includes(f.slot) : !reportTypesOf(r, ctx.jobs).has(type ?? "")) return false;
  }

  if (skip !== "status" && f.status && (f.status === "active") !== r.enabled) return false;
  if (skip !== "issues" && f.issues.length && !issuesOf(r).some((i) => f.issues.includes(i))) return false;
  if (skip !== "last" && f.last && !lastMatches(r, f.last, ctx.now)) return false;
  return true;
}

export type Facets = {
  total: number;
  role: Record<"all" | ReportRole, number>;
  region: Record<string, number>;
  issue: Record<Issue, number>;
  report: Record<number, number>;
  status: Record<"active" | "disabled", number>;
  last: Record<LastFilter, number>;
};

/** Счётчики на кнопках: по каждому измерению — с остальными фильтрами, но без своего. */
export function facets(all: ReportRecipient[], f: RecipientFilters, ctx: Ctx): Facets {
  const by = (dim: Dim) => all.filter((r) => matches(r, f, ctx, dim));
  const roles = by("role");
  const regions = by("regions");
  const issues = by("issues");
  const reports = by("report");
  const statuses = by("status");
  const lasts = by("last");
  const region: Record<string, number> = {};
  for (const x of ctx.regions) region[x.id] = regions.filter((r) => regionIdsOf(r, ctx.regions, ctx.rmUsers).includes(x.id)).length;
  const report: Record<number, number> = {};
  for (const x of REPORTS) report[x.n] = reports.filter((r) => reportTypesOf(r, ctx.jobs).has(x.type)).length;
  return {
    total: all.filter((r) => matches(r, f, ctx)).length,
    role: { all: roles.length, rm: roles.filter((r) => r.role === "rm").length, sv: roles.filter((r) => r.role === "sv").length, tp: roles.filter((r) => r.role === "tp").length },
    region,
    issue: Object.fromEntries(ISSUES.map((i) => [i, issues.filter((r) => issuesOf(r).includes(i)).length])) as Record<Issue, number>,
    report,
    status: { active: statuses.filter((r) => r.enabled).length, disabled: statuses.filter((r) => !r.enabled).length },
    last: Object.fromEntries(LAST_FILTERS.map((l) => [l, lasts.filter((r) => lastMatches(r, l, ctx.now)).length])) as Record<LastFilter, number>,
  };
}

// ---------- Группы по регионам ----------

export type Group = {
  key: string;
  regionId: string | null;
  name: string;
  rows: ReportRecipient[];
  counts: Record<ReportRole, number>;
  noChat: number;
  disabled: number;
  noSupervisor: number;
};

const byName = (a: ReportRecipient, b: ReportRecipient) => nameOf(a).localeCompare(nameOf(b), "ru");

/** Порядок строк: «проблемные сверху» — проблема, роль, имя; «по региону» — роль, имя; «по имени» — имя. */
export function sortRows(rows: ReportRecipient[], sort: SortKey): ReportRecipient[] {
  return [...rows].sort((a, b) => {
    if (sort === "name") return byName(a, b);
    if (sort === "problems") {
      const d = Number(!isProblem(a) && a.enabled) - Number(!isProblem(b) && b.enabled);
      if (d !== 0) return d;
    }
    return roleOrder(a.role) - roleOrder(b.role) || byName(a, b);
  });
}

/**
 * Группы-регионы: СВ и ТП — в своём регионе, РМ — в первом регионе из профиля или своего направления (по порядку оргструктуры), РМ всей
 * страны — в «Вся страна» сверху. Без группировки — одна группа.
 */
export function groupRows(rows: ReportRecipient[], f: RecipientFilters, ctx: Ctx): Group[] {
  const make = (key: string, regionId: string | null, name: string, items: ReportRecipient[]): Group => ({
    key,
    regionId,
    name,
    rows: sortRows(items, f.sort),
    counts: { rm: items.filter((r) => r.role === "rm").length, sv: items.filter((r) => r.role === "sv").length, tp: items.filter((r) => r.role === "tp").length },
    noChat: items.filter((r) => !hasChat(r)).length,
    disabled: items.filter((r) => !r.enabled).length,
    noSupervisor: items.filter((r) => r.role === "tp" && !r.supervisor).length,
  });
  if (f.flat) return rows.length ? [make("all", null, "Все получатели", rows)] : [];

  const buckets = new Map<string, ReportRecipient[]>();
  const names = new Map<string, string>();
  for (const r of rows) {
    let key: string;
    if (r.role === "rm") {
      const ids = regionIdsOf(r, ctx.regions, ctx.rmUsers);
      key = !r.directionId && !r.userId ? "country" : (ids[0] ?? "none");
    } else {
      key = r.regionId ?? "none";
      if (r.regionId && r.region) names.set(r.regionId, r.region);
    }

    buckets.set(key, [...(buckets.get(key) ?? []), r]);
  }

  const order = ["country", ...ctx.regions.map((x) => x.id)];
  const rank = (key: string) => (order.includes(key) ? order.indexOf(key) : key === "none" ? Number.MAX_SAFE_INTEGER : order.length);
  return [...buckets.entries()]
    .map(([key, items]) =>
      make(key, key === "country" || key === "none" ? null : key, key === "country" ? "Вся страна" : key === "none" ? "Без региона" : (ctx.regions.find((x) => x.id === key)?.name ?? names.get(key) ?? "—"), items),
    )
    .sort((a, b) => rank(a.key) - rank(b.key) || a.name.localeCompare(b.name, "ru"));
}

// ---------- Отчёты 1–9 ----------

export type MatrixCell = { n: number; type: ReportType; title: string; short: string; on: boolean; slots: { code: string; time: string; title: string; on: boolean }[] };

/** Матрица отчётов получателя: включён ли отчёт (хотя бы один запуск) и запуски роли с отметкой. */
export function matrix(r: Pick<ReportRecipient, "role" | "reports">, jobs: ReportJob[]): MatrixCell[] {
  return REPORTS.map((x) => {
    const own = jobs.filter((j) => j.report === x.type && j.roles.includes(r.role));
    const slots = own.map((j) => ({ code: j.code, time: j.time, title: j.slotTitle, on: r.reports.includes(j.code) }));
    return { ...x, on: slots.some((s) => s.on), slots };
  });
}

/** Запуск выпадает на каждый рабочий день — в счёт «N в день». */
const daily = (j: ReportJob) => !j.monthEnd && !j.monthStart && j.days.length >= 5;

/** Сводка шага 3: «8 из 9 · 19 в день». */
export function reportSummary(codes: string[], role: ReportRole, jobs: ReportJob[]): { on: number; of: number; perDay: number } {
  const cells = matrix({ role, reports: codes }, jobs).filter((c) => c.slots.length > 0);
  return { on: cells.filter((c) => c.on).length, of: cells.length, perDay: jobs.filter((j) => codes.includes(j.code) && j.roles.includes(role) && daily(j)).length };
}

export type Template = "role" | "all" | "evening" | "clear";

/** Отчёты роли по умолчанию: всё, что положено роли, кроме понедельничных итогов недели, PDF и напоминания 17:00 не ТП (как раньше). */
export const defaultCodes = (jobs: ReportJob[], role: ReportRole) =>
  jobs.filter((j) => j.roles.includes(role) && j.code !== "weekly.monday" && j.report !== "weekly-pdf" && (role === "tp" || j.code !== "no-orders.reminder")).map((j) => j.code);

export function templateCodes(template: Template, jobs: ReportJob[], role: ReportRole): string[] {
  const own = jobs.filter((j) => j.roles.includes(role));
  if (template === "role") return defaultCodes(jobs, role);
  if (template === "all") return own.map((j) => j.code);
  if (template === "evening") return own.filter((j) => j.slot === "evening").map((j) => j.code);
  return [];
}

/** Какой шаблон сейчас совпадает с выбором; null — «свой набор». */
export function templateOf(codes: string[], jobs: ReportJob[], role: ReportRole): Template | null {
  const same = (a: string[], b: string[]) => a.length === b.length && a.every((x) => b.includes(x));
  return (["role", "all", "evening", "clear"] as Template[]).find((t) => same(templateCodes(t, jobs, role), codes)) ?? null;
}

// ---------- Последняя отправка ----------

export type LastSend = {
  kind: "sent" | "skipped" | "failed" | "unknown" | "pending" | "none" | "nochat" | "off";
  at: string | null;
  job: ReportJob | null;
  error: string | null;
};

/** Последняя отправка строки: выключен → «Отключён», писать некуда → «нет чата», иначе — свежая строка журнала или последняя доставка. */
export function lastSendOf(r: ReportRecipient, jobs: ReportJob[]): LastSend {
  if (!r.enabled) return { kind: "off", at: r.disabledAt ?? null, job: null, error: r.disabledReason ?? null };
  if (!hasChat(r)) return { kind: "nochat", at: null, job: null, error: null };
  const d = r.last[0];
  if (d) {
    const kind = d.status === "sending" ? "pending" : d.status;
    return { kind, at: d.at, job: jobs.find((j) => j.code === d.report) ?? null, error: d.error };
  }

  return r.lastSentAt ? { kind: "sent", at: r.lastSentAt, job: null, error: null } : { kind: "none", at: null, job: null, error: null };
}

// ---------- Ближайшие отправки и «Требуют внимания» ----------

export type NearSend = { at: string; time: string; day: "today" | "tomorrow" | "later"; jobs: ReportJob[]; recipients: number };

/** Ближайшие запуски, сложенные по времени: что уходит и скольким получателям (включён и есть чат — иначе не уйдёт). */
export function nearSends(jobs: ReportJob[], recipients: ReportRecipient[], now: Date, limit = 4): NearSend[] {
  const slots = new Map<string, ReportJob[]>();
  for (const j of jobs.filter((x) => x.enabled && x.nextRunAt && new Date(x.nextRunAt).getTime() >= now.getTime() - 60_000)) {
    const key = new Date(j.nextRunAt!).toISOString().slice(0, 16);
    slots.set(key, [...(slots.get(key) ?? []), j]);
  }

  const today = tashkentDay(now);
  const tomorrow = tashkentDay(new Date(now.getTime() + 86_400_000));
  return [...slots.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .slice(0, limit)
    .map(([key, list]) => {
      const at = new Date(`${key}:00Z`);
      const day = tashkentDay(at);
      const codes = list.map((j) => j.code);
      return {
        at: at.toISOString(),
        time: list[0].time,
        day: day === today ? "today" : day === tomorrow ? "tomorrow" : "later",
        jobs: [...list].sort((a, b) => (reportOfType(a.report)?.n ?? 0) - (reportOfType(b.report)?.n ?? 0)),
        recipients: recipients.filter((r) => r.enabled && hasChat(r) && r.reports.some((c) => codes.includes(c))).length,
      };
    });
}

export type Attention = {
  noChat: ReportRecipient[];
  noSupervisor: ReportRecipient[];
  disabled: ReportRecipient[];
  /** Выключенные, у которых срок отключения прошёл. */
  overdue: ReportRecipient[];
  failing: ReportRecipient[];
  /** Пропущено за 7 дней из-за чата. */
  skippedNoChat: number;
};

export function attention(recipients: ReportRecipient[], metrics: ReportMetrics, now: Date): Attention {
  const today = tashkentDay(now);
  return {
    noChat: recipients.filter((r) => r.enabled && !hasChat(r)),
    noSupervisor: recipients.filter((r) => r.role === "tp" && !r.supervisor),
    disabled: recipients.filter((r) => !r.enabled),
    overdue: recipients.filter((r) => !r.enabled && !!r.disabledUntil && r.disabledUntil < today),
    failing: recipients.filter((r) => r.enabled && (r.failedWeek ?? 0) > 0),
    skippedNoChat: metrics.skippedBy?.find((x) => x.key === "no_chat")?.count ?? 0,
  };
}

/** Подписи причин пропуска (короткие — для плитки). */
export const SKIP_LABELS: Record<SkipKey, string> = {
  no_chat: "нет чата",
  disabled: "отключён",
  not_needed: "не требуется",
  promo: "нет акции",
  stale: "данные не готовы",
};

// ---------- Сохранение ----------

export const inputOf = (r: ReportRecipient): ReportRecipientInput => ({
  role: r.role,
  title: r.name !== r.scope ? r.name : null,
  userId: r.userId ?? null,
  directionId: r.directionId,
  regionId: r.regionId,
  supervisorId: r.supervisorId,
  agentId: r.agentId,
  chatIds: r.chats.map((c) => c.id),
  reports: r.reports,
  enabled: r.enabled,
  pdfLanguage: r.pdfLanguage,
  language: r.language,
  disabledReason: r.enabled ? null : (r.disabledReason ?? null),
  disabledUntil: r.enabled ? null : (r.disabledUntil ?? null),
});

/** Включить запуски (только положенные роли получателя). */
export function addReports(input: ReportRecipientInput, codes: string[], jobs: ReportJob[]): ReportRecipientInput {
  const allowed = codes.filter((c) => jobs.find((j) => j.code === c)?.roles.includes(input.role));
  return { ...input, reports: [...new Set([...input.reports, ...allowed])] };
}

/**
 * «Время отправки»: в какое время получатель получает свои отчёты. Время включено — все запуски этого времени у включённых отчётов,
 * выключено — запуски этого времени снимаются. Расписание (само время запусков) не меняется.
 */
export function setTimes(input: ReportRecipientInput, times: Record<string, boolean>, jobs: ReportJob[]): ReportRecipientInput {
  const types = new Set(jobs.filter((j) => input.reports.includes(j.code)).map((j) => j.report));
  let reports = [...input.reports];
  for (const [time, on] of Object.entries(times)) {
    const at = jobs.filter((j) => j.time === time && j.roles.includes(input.role));
    reports = on ? [...new Set([...reports, ...at.filter((j) => types.has(j.report)).map((j) => j.code)])] : reports.filter((c) => !at.some((j) => j.code === c));
  }

  return { ...input, reports };
}

/** Время запусков у выбранных: on — у всех, off — ни у кого, some — у части. */
export function timesOf(inputs: ReportRecipientInput[], jobs: ReportJob[]): { time: string; state: "on" | "off" | "some"; jobs: number }[] {
  const times = [...new Set(jobs.filter((j) => inputs.some((i) => j.roles.includes(i.role))).map((j) => j.time))].sort();
  return times.map((time) => {
    const at = jobs.filter((j) => j.time === time);
    const has = inputs.map((i) => at.some((j) => i.reports.includes(j.code)));
    return { time, state: has.every(Boolean) ? "on" : has.some(Boolean) ? "some" : "off", jobs: at.length };
  });
}

export const addChats = (input: ReportRecipientInput, chatIds: string[]): ReportRecipientInput => ({ ...input, chatIds: [...new Set([...input.chatIds, ...chatIds])] });

export const disable = (input: ReportRecipientInput, reason: string | null, until: string | null): ReportRecipientInput => ({
  ...input,
  enabled: false,
  disabledReason: reason?.trim() || null,
  disabledUntil: until || null,
});

export const enable = (input: ReportRecipientInput): ReportRecipientInput => ({ ...input, enabled: true, disabledReason: null, disabledUntil: null });
