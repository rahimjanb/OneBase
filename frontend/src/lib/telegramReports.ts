import { tNow } from "@/i18n/client";
import { bff } from "./bff";

/** Роль получателя: РМ — направление или вся страна, СВ — регион и супервайзер, ТП — торговый представитель. */
export type ReportRole = "rm" | "sv" | "tp";

export const reportRoles: { key: ReportRole; label: string; hint: string; className: string }[] = [
  { key: "rm", label: "РМ", hint: "Региональный менеджер: регионы направления или вся страна", className: "bg-[#1d4ed8] text-white" },
  { key: "sv", label: "СВ", hint: "Супервайзер: ТП своего региона", className: "bg-[#15803d] text-white" },
  { key: "tp", label: "ТП", hint: "Торговый представитель: только свои цифры", className: "bg-[#c2410c] text-white" },
];

export const roleOf = (role: string) => reportRoles.find((r) => r.key === role) ?? reportRoles[1];

/** Шесть отчётов (листы ТЗ «ИИ ассистент в ТГ 2»). */
export type ReportType = "daily" | "no-purchase" | "no-orders" | "promo" | "categories" | "weekly";

export const reportTypes: { key: ReportType; title: string; hint: string }[] = [
  { key: "daily", title: "1. Дневной план", hint: "8:30 — план дня в сумах и кг, по категориям и визитам; 13:00 — промежуточный итог; 18:00 — итог дня" },
  { key: "no-purchase", title: "2. Клиенты без закупа", hint: "8:30 — точки активной базы без закупа N+ дней, кто из них на маршруте сегодня" },
  { key: "no-orders", title: "3. ТП без заказов", hint: "13:00 — контроль; 17:00 — личное напоминание ТП без заказов; 18:00 — итог дня" },
  { key: "promo", title: "4. Заказы с акцией", hint: "13:00 и 18:00 — заказы с акцией, их доля и разбивка по акциям" },
  { key: "categories", title: "5. Заказы по категориям", hint: "13:00 и 18:00 — заказы, сумма и вес по категориям, выполнение плана категорий" },
  { key: "weekly", title: "6. Итоги недели", hint: "Суббота 18:00 — итоги недели пн–сб; понедельник 8:30 — пересчитанные итоги прошлой недели" },
];

export type ReportSettings = { accountId: string | null; language: "ru" | "uz"; noPurchaseDays: number; refreshBeforeSend: boolean; updatedAt: string | null };

export type ReportAccount = { id: string; name: string; status: string; username: string | null };

export type RunStatus = "running" | "done" | "partial" | "failed" | "skipped";

export type ReportRunBrief = {
  id: string;
  period: string;
  startedAt: string;
  finishedAt: string | null;
  status: RunStatus;
  planned: number;
  sent: number;
  failed: number;
  skipped: number;
  error: string | null;
};

/** Запуск отчёта по расписанию: «daily.midday» — Дневной план в 13:00. Дни 1–7 — пн–вс. */
export type ReportJob = {
  code: string;
  report: ReportType;
  reportTitle: string;
  slot: string;
  slotTitle: string;
  roles: ReportRole[];
  enabled: boolean;
  time: string;
  timeZone: string;
  days: number[];
  catchUpMinutes: number;
  nextRunAt: string | null;
  lastRun: ReportRunBrief | null;
  recipients: number;
  updatedAt: string | null;
};

export type ReportJobInput = Pick<ReportJob, "enabled" | "time" | "timeZone" | "days" | "catchUpMinutes">;

export type ChatType = "Private" | "Group" | "Channel";

export type ReportChatRef = { id: string; title: string; type: ChatType; canSend: boolean; peerId: number; linked: string | null };

export type DeliveryStatus = "pending" | "sending" | "sent" | "failed" | "skipped" | "unknown";

export type ReportDeliveryBrief = { report: string; chat: string; status: DeliveryStatus; at: string; error: string | null };

/** Привязка Telegram получателя: linked — подтверждена кодом в OneBase, chat — есть личный чат, group — только группы, blocked — писать нельзя. */
export type TelegramStatus = "linked" | "chat" | "group" | "blocked" | "none";

export type ReportRecipient = {
  id: string;
  role: ReportRole;
  name: string;
  scope: string;
  directionId: string | null;
  regionId: string | null;
  region: string | null;
  supervisorId: number | null;
  agentId: number | null;
  agents: number;
  chats: ReportChatRef[];
  telegramStatus: TelegramStatus;
  reports: string[];
  enabled: boolean;
  /** Почему сейчас отчёт не уйдёт: область пропала из данных «Продаж», нет чатов, в чат писать нельзя. */
  problem: string | null;
  last: ReportDeliveryBrief[];
};

export type ReportRecipientInput = {
  role: ReportRole;
  title: string | null;
  directionId: string | null;
  regionId: string | null;
  supervisorId: number | null;
  agentId: number | null;
  chatIds: string[];
  reports: string[];
  enabled: boolean;
};

export type ReportPerson = { id: number; name: string };

/** Регион OneBase: направление, РМ, «СВР региона» из оргструктуры, супервайзеры и ТП месяца. */
export type ReportRegion = {
  id: string;
  name: string;
  directionId: string | null;
  manager: string | null;
  supervisorName: string | null;
  supervisors: ReportPerson[];
  agents: ReportPerson[];
};

export type ReportDirection = { id: string; name: string; manager: string | null; regions: number };

export type ReportChat = {
  id: string;
  title: string;
  type: ChatType;
  username: string | null;
  peerId: number;
  canSend: boolean;
  reason: string | null;
  /** Пользователь OneBase, чей Telegram подтверждён кодом и чей это личный чат. */
  linkedUser: string | null;
};

export type ReportMetrics = { sent: number; failed: number; skipped: number; unknown: number; pending: number };

export type ReportOverview = {
  settings: ReportSettings;
  accounts: ReportAccount[];
  jobs: ReportJob[];
  recipients: ReportRecipient[];
  directions: ReportDirection[];
  regions: ReportRegion[];
  chats: ReportChat[];
  /** Право на данные продаж (sales.read): без него — ни регионов, ни отчётов. */
  canUseSales: boolean;
  /** Telegram выключен в этой среде (локально): по расписанию ничего не отправляется. */
  telegramEnabled: boolean;
  /** Отправки за 7 дней. */
  metrics: ReportMetrics;
};

export type ReportPreviewInput = {
  job: string;
  role: ReportRole | null;
  directionId: string | null;
  regionId: string | null;
  supervisorId: number | null;
  agentId: number | null;
  recipientId: string | null;
  /** yyyy-mm-dd; null — сегодня. */
  date: string | null;
  /** «13:00» — момент формирования; null — сейчас (прошлый день — время запуска). */
  time: string | null;
  language: "ru" | "uz" | null;
};

export type ReportPreview = {
  pages: string[];
  title: string;
  scope: string;
  caption: string;
  summary: string;
  notes: string[];
  problems: string[];
  bytes: number;
  /** false — условие отчёта не выполнено (напоминание ТП, у которого есть заказы): по расписанию он не уйдёт. */
  applies: boolean;
  skipReason: string | null;
  incomplete: boolean;
};

export type ReportSendResult = {
  deliveryId: string;
  status: DeliveryStatus;
  recipientTitle: string;
  scopeTitle: string;
  error: string | null;
  pages: number;
  sentAt: string | null;
  nextAttemptAt: string | null;
};

export type ReportDelivery = {
  id: string;
  createdAt: string;
  report: string;
  reportTitle: string;
  period: string;
  trigger: "schedule" | "manual" | "test";
  role: ReportRole | null;
  recipientTitle: string;
  scopeTitle: string;
  regionId: string | null;
  status: DeliveryStatus;
  attempts: number;
  sentAt: string | null;
  nextAttemptAt: string | null;
  error: string | null;
  pages: number;
  bytes: number;
  summary: string | null;
  dataAsOf: string | null;
  durationMs: number | null;
  by: string | null;
  canRetry: boolean;
};

export type ReportRun = {
  id: string;
  job: string;
  title: string;
  period: string;
  scheduledAt: string;
  startedAt: string;
  finishedAt: string | null;
  status: RunStatus;
  planned: number;
  sent: number;
  failed: number;
  skipped: number;
  error: string | null;
  dataAsOf: string | null;
};

export type ReportHistory = { items: ReportDelivery[]; total: number; page: number; pageSize: number; runs: ReportRun[] };

export type ReportHistoryQuery = { from: string; to: string; report: string; regionId: string; status: string; page: number };

export const deliveryStatus: Record<DeliveryStatus, { label: string; className: string }> = {
  pending: { label: "В очереди", className: "text-warn" },
  sending: { label: "Отправляется", className: "text-accent-strong" },
  sent: { label: "Отправлен", className: "text-ok" },
  failed: { label: "Ошибка", className: "text-bad" },
  skipped: { label: "Пропущен", className: "text-ink-3" },
  unknown: { label: "Неизвестно, дошёл ли", className: "text-warn" },
};

export const runStatus: Record<RunStatus, { label: string; className: string }> = {
  running: { label: "Идёт", className: "text-accent-strong" },
  done: { label: "Выполнен", className: "text-ok" },
  partial: { label: "С ошибками", className: "text-warn" },
  failed: { label: "Не выполнен", className: "text-bad" },
  skipped: { label: "Пропущен", className: "text-ink-3" },
};

export const telegramStatus: Record<TelegramStatus, { label: string; className: string }> = {
  linked: { label: "Telegram привязан", className: "bg-ok-soft text-ok" },
  chat: { label: "Личный чат", className: "bg-accent-soft text-accent-strong" },
  group: { label: "Только группы", className: "bg-muted text-ink-2" },
  blocked: { label: "Писать нельзя", className: "bg-bad-soft text-bad" },
  none: { label: "Нет чатов", className: "bg-warn-soft text-warn" },
};

export const triggerLabel: Record<ReportDelivery["trigger"], string> = { schedule: "По расписанию", manual: "Вручную", test: "Тест" };

/** Дни недели: 1 — пн … 7 — вс. */
export const weekDays: { day: number; label: string }[] = [
  { day: 1, label: "Пн" },
  { day: 2, label: "Вт" },
  { day: 3, label: "Ср" },
  { day: 4, label: "Чт" },
  { day: 5, label: "Пт" },
  { day: 6, label: "Сб" },
  { day: 7, label: "Вс" },
];

/** Часовые пояса для выбора (IANA). */
export const timeZones = ["Asia/Tashkent", "Asia/Samarkand", "Asia/Almaty", "Asia/Bishkek", "Asia/Dushanbe", "Europe/Moscow", "UTC"];

/** Подпись запуска: «1. Дневной план · 13:00 — промежуточный итог». */
export const jobTitle = (job: Pick<ReportJob, "reportTitle" | "time" | "slotTitle">) => `${job.reportTitle} · ${job.time} — ${job.slotTitle}`;

const base = "settings/telegram/reports";
const json = (body: unknown) => JSON.stringify(body);

/**
 * Запрос, который отправляет в Telegram: один раз (повтор запроса отправил бы отчёт дважды). 409 — { conflict: текст }: уже отправлен
 * или «неизвестно, дошёл ли» — повтор только явным подтверждением (force).
 */
async function sendOnce<T>(path: string, body: unknown): Promise<T | { conflict: string }> {
  const response = await fetch(`/bff/api/${base}/${path}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: json(body) }).catch(() => null);
  if (!response) {
    throw new Error(tNow("Нет связи с сервером OneBase — ответ не пришёл. Обновите страницу и проверьте, не выполнилось ли действие, затем повторите."));
  }

  const result = (await response.json().catch(() => null)) as (T & { error?: string }) | null;
  const error = result && typeof result === "object" && "error" in result ? (result.error as string | undefined) : undefined;
  if (response.status === 409) {
    return { conflict: tNow(error ?? "Уже отправлен сегодня.") };
  }

  if (!response.ok || result === null) {
    throw new Error(response.status === 403 ? tNow("Недостаточно прав.") : tNow(error ?? "Ошибка {status}", { status: response.status }));
  }

  return result as T;
}

/** Дневной план ТП («ТП без заказов»): ok — план есть; no-plan — плана месяца в Linko нет; done — выполнен; day-off — выходной или праздник; no-days — нет рабочих дней. */
export type DailyPlanStatus = "ok" | "no-plan" | "done" | "day-off" | "no-days";

export const dailyPlanStatus: Record<DailyPlanStatus, { label: string; className: string }> = {
  ok: { label: "план есть", className: "bg-ok/15 text-ok" },
  "no-plan": { label: "нет плана месяца", className: "bg-surface-2 text-ink-3" },
  done: { label: "план выполнен", className: "bg-ok/15 text-ok" },
  "day-off": { label: "выходной / праздник", className: "bg-surface-2 text-ink-3" },
  "no-days": { label: "нет рабочих дней", className: "bg-warn/15 text-warn" },
};

export type ReportDailyPlan = {
  id: string;
  agentId: number;
  agent: string;
  region: string | null;
  date: string;
  monthPlanSum: number | null;
  factBeforeSum: number;
  remainingSum: number | null;
  workDaysLeft: number;
  dailyPlanSum: number | null;
  status: DailyPlanStatus;
  note: string | null;
  /** morning — утренняя задача, report — первый отчёт дня, admin — пересчёт администратором, plan-appeared — план появился в Linko. */
  trigger: string;
  reason: string | null;
  computedAt: string;
  /** Заменён пересчётом — строка истории. */
  supersededAt: string | null;
  by: string | null;
};

export type ReportDailyPlans = { date: string; workingDay: boolean; workDaysLeft: number; items: ReportDailyPlan[]; history: ReportDailyPlan[] };

export type ReportHoliday = { id: string; date: string; name: string };

export const dailyPlansApi = {
  list: (date?: string) => bff<ReportDailyPlans>(`${base}/daily-plans${date ? `?date=${date}` : ""}`),
  /** Пересчёт с причиной: один ТП (agentId) или все на день; прежние строки остаются историей. */
  recalculate: (input: { date: string; agentId: number | null; reason: string }) => bff<ReportDailyPlans>(`${base}/daily-plans/recalculate`, { method: "POST", body: json(input) }),
  holidays: (year?: number) => bff<ReportHoliday[]>(`${base}/holidays${year ? `?year=${year}` : ""}`),
  addHoliday: (input: { date: string; name: string }) => bff<ReportHoliday>(`${base}/holidays`, { method: "POST", body: json(input) }),
  deleteHoliday: (id: string) => bff<void>(`${base}/holidays/${id}`, { method: "DELETE" }),
};

export const reportsApi = {
  overview: () => bff<ReportOverview>(base),
  saveSettings: (s: Pick<ReportSettings, "accountId" | "language" | "noPurchaseDays" | "refreshBeforeSend">) =>
    bff<ReportSettings>(`${base}/settings`, { method: "PUT", body: json(s) }),
  saveJob: (code: string, input: ReportJobInput) => bff<ReportJob>(`${base}/jobs/${encodeURIComponent(code)}`, { method: "PUT", body: json(input) }),
  saveRecipient: (id: string | null, input: ReportRecipientInput) =>
    bff<ReportRecipient>(id ? `${base}/recipients/${id}` : `${base}/recipients`, { method: id ? "PUT" : "POST", body: json(input) }),
  deleteRecipient: (id: string) => bff<void>(`${base}/recipients/${id}`, { method: "DELETE" }),
  /** Отправить сейчас во все чаты получателя (реальные данные). */
  send: (id: string, job: string, force: boolean) => sendOnce<ReportSendResult[]>(`recipients/${id}/send`, { job, force }),
  preview: (input: ReportPreviewInput) => bff<ReportPreview>(`${base}/preview`, { method: "POST", body: json(input) }),
  /** Тест — только в выбранный чат, с пометкой «ТЕСТОВАЯ ОТПРАВКА». */
  test: (input: ReportPreviewInput, chatId: string) => sendOnce<ReportSendResult>("test", { report: input, chatId }),
  retry: (id: string, force: boolean) => sendOnce<ReportSendResult>(`deliveries/${id}/retry?force=${force}`, {}),
  history: (q: ReportHistoryQuery) => {
    const params = new URLSearchParams({ page: String(q.page) });
    for (const key of ["from", "to", "report", "regionId", "status"] as const) {
      if (q[key]) params.set(key, q[key]);
    }

    return bff<ReportHistory>(`${base}/history?${params}`);
  },
};
