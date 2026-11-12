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

/** Шесть отчётов (листы ТЗ «ИИ ассистент в ТГ 2»), «Поле боя», еженедельный PDF и накопительная акция. */
export type ReportType = "daily" | "no-purchase" | "no-orders" | "promo" | "categories" | "weekly" | "battle" | "weekly-pdf" | "incentive";

export const reportTypes: { key: ReportType; title: string; hint: string }[] = [
  { key: "daily", title: "1. Дневной план", hint: "8:30 — план дня в сумах и кг, по категориям и визитам; 13:00 — промежуточный итог; 18:00 — итог дня" },
  { key: "no-purchase", title: "2. Клиенты без закупа", hint: "8:30 — точки активной базы без закупа N+ дней, кто из них на маршруте сегодня" },
  { key: "no-orders", title: "3. ТП без заказов", hint: "13:00 — контроль; 17:00 — личное напоминание ТП без заказов; 18:00 — итог дня" },
  { key: "promo", title: "4. Заказы с акцией", hint: "13:00 и 18:00 — заказы с акцией, их доля и разбивка по акциям" },
  { key: "categories", title: "5. Заказы по категориям", hint: "13:00 и 18:00 — заказы, сумма и вес по категориям, выполнение плана категорий" },
  { key: "weekly", title: "6. Итоги недели", hint: "Суббота 18:00 — итоги недели пн–сб; понедельник 8:30 — пересчитанные итоги прошлой недели" },
  { key: "battle", title: "7. Поле боя", hint: "Карточки целей ТП по магазинам маршрута: 8:30 — маршрут дня, 13:00 — полдень, 18:00 — итог дня, суббота 18:00 — итог недели; СВ — копии карточек своих ТП" },
  { key: "weekly-pdf", title: "8. Еженедельный PDF", hint: "Понедельник 9:00 — PDF за прошлую неделю пн–сб: РОП — страна, РМ — направление, СВ — регион и команда, ТП — личная карточка. Предпросмотр и формулы — на вкладке «Отчёты PDF»" },
  { key: "incentive", title: "9. Накопительная акция", hint: "Twizos: 8:30 — маршрут дня с целями в кг, 13:00 — полдень, 16:00 — промежуточный итог, 18:00 — итог дня (после «Поля боя»), суббота 18:00 — итог недели и 18:30 — рейтинг, 1-е число — итог прошлого месяца; СВ — сводка команды и копии карточек ТП, РМ — сводка регионов; акции ноября: СВР 19:30 — итог дня команды, РМ суббота 19:00 — итог недели зоны, понедельник 9:00 — сводка руководства" },
];

/** Запуск рисуется PDF-документом, а не картинками: у него своя вкладка, в предпросмотре картинок и «Отправить» его нет. */
export const isPdfJob = (job: Pick<ReportJob, "report">) => job.report === "weekly-pdf";

/** Язык подписей отчётов и PDF: русский, узбекский латиницей, узбекский кириллицей. */
export type ReportLanguage = "ru" | "uz" | "uz-Cyrl";

export const reportLanguages: { code: ReportLanguage; label: string }[] = [
  { code: "ru", label: "Русский" },
  { code: "uz", label: "Узбекский (латиница)" },
  { code: "uz-Cyrl", label: "Узбекский (кириллица)" },
];

export type ReportSettings = { accountId: string | null; language: ReportLanguage; noPurchaseDays: number; refreshBeforeSend: boolean; updatedAt: string | null };

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
  /** Только в последний рабочий день месяца: дни недели не важны. */
  monthEnd?: boolean;
  /** Только 1-го числа (итог прошлого месяца акции): дни недели не важны. */
  monthStart?: boolean;
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
  /** Язык еженедельного PDF; null — язык отчётов из общих настроек. */
  pdfLanguage: ReportLanguage | null;
  /** Язык картинок автоотчётов получателя (ТП — по-узбекски, СВ — по-русски); null — из общих настроек. */
  language: ReportLanguage | null;
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
  pdfLanguage?: ReportLanguage | null;
  language?: ReportLanguage | null;
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
  weeklyPdf: WeeklyPdfSettings;
  /** Когда последний раз обновлялись заказы и визиты Linko. */
  freshness: ReportFreshness;
};

/** Формулы целей недели и пороги еженедельного PDF (доли: 0.3 = 30%). */
export type WeeklyPdfFormulas = {
  goalAkbFactor: number;
  goalPromoFactor: number;
  goalPromoMin: number;
  returnShare15To30: number;
  returnShare30Plus: number;
  sleepFromDays: number;
  sleepDays: number;
  deficitDays: number;
};

/** «Отчёты PDF»: только при полных данных, карточки ТП для СВ, действующие формулы и значения по умолчанию (appsettings). */
export type WeeklyPdfSettings = {
  requireCompleteData: boolean;
  svTpCards: boolean;
  /** Суммы КПУ и DSO в отправляемых PDF. */
  includeMoney: boolean;
  formulas: WeeklyPdfFormulas;
  defaults: WeeklyPdfFormulas;
  updatedAt: string | null;
};

export type ReportFreshness = { orders: string | null; visits: string | null };

/** Область PDF: РОП — вся страна (на сервере РМ без направления), РМ — направление, СВ — регион и команда, ТП — личная карточка. */
export type WeeklyPdfRole = "rop" | "rm" | "sv" | "tp";

export type WeeklyPdfInput = { role: WeeklyPdfRole; directionId: string | null; regionId: string | null; agentId: number | null; date: string | null };

/** Что показать: страницы РОП / РМ / СВ или карточки ТП (у ТП — всегда карточка). */
export type WeeklyPdfKind = "team" | "cards";

export type WeeklySourceState = "ok" | "stale" | "missing";

export type WeeklySourceStatus = { key: string; state: WeeklySourceState; asOf: string | null; note: string | null };

/** Предпросмотр PDF: страницы картинками, неделя, источники и их свежесть, чего не хватает для «полных данных», замечания. */
export type WeeklyPdfPreview = {
  pages: string[];
  role: string;
  scopeName: string;
  supervisorName: string | null;
  periodStart: string;
  periodEnd: string;
  dataThrough: string;
  dataAsOf: string | null;
  sourceStatuses: WeeklySourceStatus[];
  incomplete: string[];
  warnings: string[];
  fileName: string;
};

export const weeklySources: Record<string, string> = {
  "linko-orders": "Заказы Linko",
  "linko-visits": "Визиты Linko",
  "linko-plans": "Планы Linko",
  "linko-kpi": "KPI Linko",
  promo: "Акции",
  "linko-stock": "Остатки дилеров",
  dso: "DSO (оплаты дилеров)",
  dmp: "ДМП (стеллажи)",
};

export const sourceState: Record<WeeklySourceState, { label: string; className: string }> = {
  ok: { label: "есть", className: "text-ok" },
  stale: { label: "устарели", className: "text-warn" },
  missing: { label: "нет данных", className: "text-bad" },
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
  language: ReportLanguage | null;
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
  /** Еженедельный PDF: файлы в хранилище, этап и подробный лог. */
  files?: WeeklyPdfFile[] | null;
  stage?: WeeklyPdfStage | null;
  log?: string | null;
};

/** PDF отправки: team — страницы РОП / РМ / СВ, cards — карточки ТП. */
export type WeeklyPdfFile = { index: number; kind: "team" | "cards"; name: string; pages: number; bytes: number };

export type WeeklyPdfStage = "collecting" | "generating" | "ready" | "sent";

export const pdfStage: Record<WeeklyPdfStage, string> = {
  collecting: "сбор данных",
  generating: "формирование PDF",
  ready: "отправка",
  sent: "отправлен",
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

/** Тело запроса PDF: РОП — РМ без направления; ТП — агент. */
const weeklyBody = (i: WeeklyPdfInput) =>
  json({
    role: i.role === "rop" ? "rm" : i.role,
    directionId: i.role === "rm" ? i.directionId : null,
    regionId: i.role === "sv" ? i.regionId : null,
    agentId: i.role === "tp" ? i.agentId : null,
    date: i.date,
  });

/** Скачать файл ответа сервера (PDF): ошибка — текст сервера. */
async function saveFile(path: string, fileName: string, init?: RequestInit) {
  const response = await fetch(`/bff/api/${base}/${path}`, { ...init, headers: { "Content-Type": "application/json", ...init?.headers } }).catch(() => null);
  if (!response) {
    throw new Error(tNow("Нет связи с сервером OneBase — ответ не пришёл. Обновите страницу и проверьте, не выполнилось ли действие, затем повторите."));
  }

  if (!response.ok) {
    const result = (await response.json().catch(() => null)) as { error?: string } | null;
    throw new Error(response.status === 403 ? tNow("Недостаточно прав.") : tNow(result?.error ?? "Ошибка {status}", { status: response.status }));
  }

  const url = URL.createObjectURL(await response.blob());
  const a = Object.assign(document.createElement("a"), { href: url, download: fileName });
  document.body.append(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 10_000);
}

export const weeklyPdfApi = {
  saveSettings: (s: Pick<WeeklyPdfSettings, "requireCompleteData" | "svTpCards" | "includeMoney" | "formulas">) =>
    bff<WeeklyPdfSettings>(`${base}/weekly-pdf`, { method: "PUT", body: json(s) }),
  /** Страницы картинками и состояние данных — без отправки. */
  preview: (i: WeeklyPdfInput, language: ReportLanguage, kind: WeeklyPdfKind) =>
    bff<WeeklyPdfPreview>(`${base}/weekly/pdf?images=1&language=${language}&kind=${kind}`, { method: "POST", body: weeklyBody(i) }),
  /** Скачать PDF файлом (тот же отчёт, что в предпросмотре). */
  download: (i: WeeklyPdfInput, language: ReportLanguage, kind: WeeklyPdfKind, fileName: string) =>
    saveFile(`weekly/pdf?language=${language}&kind=${kind}`, fileName, { method: "POST", body: weeklyBody(i) }),
  /** Тест — только в выбранный чат, с пометкой «ТЕСТОВАЯ ОТПРАВКА». */
  test: (i: WeeklyPdfInput, language: ReportLanguage, chatId: string) =>
    sendOnce<ReportSendResult>("weekly/test", {
      role: i.role,
      directionId: i.role === "rm" ? i.directionId : null,
      regionId: i.role === "sv" ? i.regionId : null,
      agentId: i.role === "tp" ? i.agentId : null,
      date: i.date,
      language,
      chatId,
    }),
  /** PDF отправки из истории. */
  file: (deliveryId: string, file: WeeklyPdfFile) => saveFile(`deliveries/${deliveryId}/files/${file.index}`, file.name),
  /** Сформировать заново — новые PDF той же отправки, без отправки. */
  rebuild: (deliveryId: string) => bff<WeeklyPdfFile[]>(`${base}/deliveries/${deliveryId}/rebuild`, { method: "POST" }),
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

// ---------- Накопительные акции (вкладка «Акции») ----------

/** Ступень акции: порог, кг; приз; коротко («планшет»); «до кого» по-русски («планшета»); картинка; по-узбекски; порог опта (TW-03; нет — как у розницы). */
export type IncentiveTier = {
  kg: number;
  prize: string;
  short: string;
  image: string;
  prizeUz: string | null;
  shortUz: string | null;
  of: string | null;
  wholesaleKg?: number | null;
};

/** Ориентир месяца: доля порога (0,35 = 35%). */
export type IncentiveMonthShare = { year: number; month: number; share: number };

/**
 * Цель магазина: набор для ТТ со средним закупом от minAvg сум/мес, вес, приз клиенту, уровень набора «Поля боя». Наборы акций ноября (TW-02) — с
 * кодом (K35, K140, T, C), суммой по прайсу и картинкой подарка (mug, thermos, kettle): их выбирают правила чека, а не «закуп от».
 */
export type IncentiveStoreSet = {
  minAvg: number;
  contents: string;
  kg: number;
  prize: string | null;
  level: string | null;
  contentsUz: string | null;
  prizeUz: string | null;
  code?: string | null;
  sum?: number;
  gift?: string | null;
};

/** Подарок клиенту TW-02: заказ с суммой Twizos от minSum — подарок; товары Linko подарка (бонусные строки), выделено штук. */
export type IncentiveGiftTier = { minSum: number; gift: string; name: string; nameUz: string | null; productIds: number[] | null; stock: number };

/** Пара наборов по среднему чеку ТТ: чек до maxCheck (null — без верхней границы) — наборы по кодам. */
export type IncentivePairRule = { maxCheck: number | null; sets: string[] };

export type IncentiveTwizosPromo = {
  gifts: IncentiveGiftTier[] | null;
  pairs: IncentivePairRule[] | null;
  startSet: string;
  smallStartSet: string;
  smallCheck: number;
  minCheck: number;
  pairNewMinCheck: number;
  reserve: number;
  conversion: number;
  extraMax: number;
  checkDays: number;
  topDays: number;
  targetPrize: string | null;
  extraPairs: IncentivePairRule[] | null;
};

export type IncentivePacking = { unitKg: number; perBox: number; name: string };

export type IncentiveYamellyPromo = {
  start: string;
  end: string;
  productTypeIds: number[] | null;
  packings: IncentivePacking[] | null;
  product: string;
  discountPercent: number;
  cashbackPercent: number;
  minBoxes: number;
  wakeShare: number;
  minCashback: number;
  sleepingFrom: string | null;
  sleepingSince: string | null;
};

export type IncentiveBestPromo = { start: string; end: string; retailMinKg: number; wholesaleMinKg: number; prize: number };

export type IncentiveTerritory = { name: string; branches: string[]; planKg: number };

export type IncentiveTerritoryPromo = {
  start: string;
  end: string;
  territories: IncentiveTerritory[] | null;
  productTypeIds: number[] | null;
  threshold: number;
  agentMinKg: number;
  agentPrize: number;
  supervisorPrize: number;
};

export type IncentiveBudget = { cashback: number; best: number; territory: number };

/** Акции ноября вокруг накопительной: раздел null — акции нет. */
export type IncentivePromos = {
  twizos: IncentiveTwizosPromo | null;
  yamelly: IncentiveYamellyPromo | null;
  best: IncentiveBestPromo | null;
  territory: IncentiveTerritoryPromo | null;
  budget: IncentiveBudget | null;
};

/** Список спящих: в списке, исключено, с какого дня, когда меняли, у активных ТП, правило Linko; computed — снимка ещё нет. */
export type IncentiveSleepingSummary = {
  code: string;
  active: number;
  removed: number;
  listedFrom: string | null;
  changedAt: string | null;
  withActiveAgent: number;
  boughtFrom: string | null;
  notSince: string | null;
  computed: boolean;
};

export type IncentiveSleepingRefresh = { added: number; removed: number; restored: number; kept: number; summary: IncentiveSleepingSummary };

export type IncentivePayoutWeek = {
  monday: string;
  woken: number;
  issued: number;
  confirmed: number;
  cashbackPaid: number;
  cashbackToPay: number;
  bestWins: number;
  bestPrize: number;
  bestPaid: number;
  bestToPay: number;
  paidOn: string | null;
  note: string | null;
};

export type IncentivePayoutRow = {
  agentId: number;
  name: string;
  region: string | null;
  listed: number;
  woken: number;
  issued: number;
  pending: number;
  confirmed: number;
  cashbackPaid: number;
  cashbackToPay: number;
  canEarn: number;
  bestWins: number;
  bestAccrued: number;
  bestPaid: number;
  bestToPay: number;
  weeks: IncentivePayoutWeek[];
};

export type IncentivePayouts = { code: string; date: string; dataAsOf: string | null; bestPrize: number; rows: IncentivePayoutRow[] };

export type IncentivePayoutInput = { agentId: number; weekStart: string; kind: "cashback" | "best"; amount: number; paidOn: string | null; note: string | null };

export type IncentiveState = "off" | "upcoming" | "running" | "finished";

export type IncentiveProgram = {
  code: string;
  name: string;
  product: string;
  productTypeIds: number[];
  start: string;
  end: string;
  tiers: IncentiveTier[];
  months: IncentiveMonthShare[];
  storeSets: IncentiveStoreSet[];
  wholesaleAgentIds: number[];
  separateWholesale: boolean;
  svCopies: boolean;
  svSummary: boolean;
  rmMode: "summary" | "cards";
  staleData: "stamp" | "skip";
  enabled: boolean;
  state: IncentiveState;
  workDaysLeft: number;
  updatedAt: string | null;
  /** Магазинов с целью на маршруте дня — не больше (1–30). */
  maxStores: number;
  /** Акции ноября (YM-01…03, TW-01…03); null — нет. Сохраняются отдельно. */
  promos: IncentivePromos | null;
};

export type IncentiveOverview = {
  programs: IncentiveProgram[];
  productTypes: { id: number; name: string; parentId: number | null }[];
  agents: { id: number; name: string }[];
  images: string[];
};

export type IncentiveProgramInput = Omit<IncentiveProgram, "code" | "state" | "workDaysLeft" | "updatedAt" | "promos"> & { code?: string };

export type IncentiveStandingRow = {
  place: number | null;
  agentId: number;
  name: string;
  region: string | null;
  wholesale: boolean;
  total: number;
  delivered: number;
  returned: number;
  today: number;
  reached: string | null;
  next: string | null;
  toNext: number | null;
};

export type IncentiveStandings = { code: string; date: string; dataAsOf: string | null; workDaysLeft: number; rows: IncentiveStandingRow[] };

export const incentiveStates: Record<IncentiveState, { label: string; className: string }> = {
  off: { label: "Выключена", className: "text-ink-3" },
  upcoming: { label: "Скоро начнётся", className: "text-warn" },
  running: { label: "Идёт", className: "text-ok" },
  finished: { label: "Закончилась", className: "text-ink-3" },
};

export const incentiveImages: Record<string, string> = { tablet: "Планшет", tv: "Телевизор", phone: "Телефон", gift: "Подарок (без картинки)" };

export const incentivesApi = {
  overview: () => bff<IncentiveOverview>(`${base}/incentives`),
  save: (code: string, input: IncentiveProgramInput) => bff<IncentiveProgram>(`${base}/incentives/${encodeURIComponent(code)}`, { method: "PUT", body: json(input) }),
  /** Новая акция по образцу — создаётся выключенной. */
  create: (input: IncentiveProgramInput) => bff<IncentiveProgram>(`${base}/incentives`, { method: "POST", body: json(input) }),
  /** Накоплено сейчас по оргструктуре «Продаж» (нужно право sales.read). */
  standings: (code: string, date?: string) => bff<IncentiveStandings>(`${base}/incentives/${encodeURIComponent(code)}/standings${date ? `?date=${date}` : ""}`),
  /** Акции ноября — сохраняются отдельно от основной формы. */
  savePromos: (code: string, promos: IncentivePromos) =>
    bff<IncentiveProgram>(`${base}/incentives/${encodeURIComponent(code)}/promos`, { method: "PUT", body: json(promos) }),
  sleeping: (code: string) => bff<IncentiveSleepingSummary>(`${base}/incentives/${encodeURIComponent(code)}/sleeping`),
  /** Пересчитать список спящих по Linko на сегодня. */
  refreshSleeping: (code: string) => bff<IncentiveSleepingRefresh>(`${base}/incentives/${encodeURIComponent(code)}/sleeping/refresh`, { method: "POST" }),
  payouts: (code: string) => bff<IncentivePayouts>(`${base}/incentives/${encodeURIComponent(code)}/payouts`),
  setPayout: (code: string, input: IncentivePayoutInput) =>
    bff<IncentivePayouts>(`${base}/incentives/${encodeURIComponent(code)}/payouts`, { method: "PUT", body: json(input) }),
};

export const incentiveGifts: Record<string, string> = { mug: "Кружка", thermos: "Термос", kettle: "Чайник" };
