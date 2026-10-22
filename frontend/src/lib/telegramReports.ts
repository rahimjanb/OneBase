import { tNow } from "@/i18n/client";
import { bff } from "./bff";

/** Утро — план на день, обед — план / факт, вечер — итоги дня. */
export type ReportKind = "Morning" | "Midday" | "Evening";

export const reportKinds: { key: ReportKind; label: string; hint: string; field: "morning" | "midday" | "evening" }[] = [
  { key: "Morning", label: "План на день", hint: "Утро: цели дня — план продаж и визитов каждого агента, без факта", field: "morning" },
  { key: "Midday", label: "План / факт", hint: "Обед: продажи и визиты на момент отчёта, отставание и кто требует внимания", field: "midday" },
  { key: "Evening", label: "Итоги дня", hint: "Вечер: итоговый факт, выполнение, отклонение, лучшие и невыполненные планы", field: "evening" },
];

export type ReportSettings = { accountId: string | null; language: "ru" | "uz"; updatedAt: string | null };

export type ReportAccount = { id: string; name: string; status: string; username: string | null };

export type ReportSchedule = {
  kind: ReportKind;
  enabled: boolean;
  time: string;
  timeZone: string;
  /** 1–7: пн–вс. */
  days: number[];
  metric: "sum" | "kg";
  nextRunAt: string | null;
  updatedAt: string | null;
};

export type ReportDeliveryBrief = { kind: ReportKind; chat: string; status: DeliveryStatus; at: string; error: string | null };

export type ReportChatRef = { id: string; title: string; type: "Private" | "Group" | "Channel"; canSend: boolean };

/** Автоотправка: регион → супервайзер → чаты и группы, какие отчёты. Агенты — команда региона из «Продаж», подставляются сами. */
export type ReportSubscription = {
  id: string;
  regionId: string;
  region: string;
  supervisorId: number | null;
  supervisor: string;
  agents: number;
  chats: ReportChatRef[];
  morning: boolean;
  midday: boolean;
  evening: boolean;
  enabled: boolean;
  /** Почему сейчас отправить нельзя (регион или супервайзер пропали, нет чатов, в чат писать нельзя). */
  problem: string | null;
  last: ReportDeliveryBrief[];
};

export type ReportSubscriptionInput = {
  regionId: string;
  supervisorId: number | null;
  chatIds: string[];
  morning: boolean;
  midday: boolean;
  evening: boolean;
  enabled: boolean;
};

export type ReportPerson = { id: number; name: string };

/** Регион OneBase: РМ, «СВР региона» из оргструктуры, супервайзеры и агенты месяца. */
export type ReportRegion = { id: string; name: string; manager: string | null; supervisorName: string | null; supervisors: ReportPerson[]; agents: string[] };

export type ReportChat = { id: string; title: string; type: "Private" | "Group" | "Channel"; username: string | null; canSend: boolean; reason: string | null };

export type ReportOverview = {
  settings: ReportSettings;
  accounts: ReportAccount[];
  schedules: ReportSchedule[];
  subscriptions: ReportSubscription[];
  regions: ReportRegion[];
  chats: ReportChat[];
  /** Право на данные продаж: без него — только демонстрационные данные. */
  canUseSales: boolean;
  /** Telegram выключен в этой среде (локально): по расписанию ничего не отправляется. */
  telegramEnabled: boolean;
};

export type ReportPreviewInput = {
  kind: ReportKind;
  mode: "demo" | "real";
  region: string | null;
  agents: number | null;
  subscriptionId: string | null;
  regionId: string | null;
  supervisorId: number | null;
  metric: "sum" | "kg" | null;
  language: "ru" | "uz" | null;
};

export type ReportPreview = { pages: string[]; caption: string; summary: string; notes: string[]; problems: string[]; bytes: number };

export type DeliveryStatus = "pending" | "sending" | "sent" | "failed";

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
  kind: ReportKind;
  period: string;
  trigger: "schedule" | "manual" | "test";
  dataMode: "real" | "demo";
  recipientTitle: string;
  scopeTitle: string;
  status: DeliveryStatus;
  attempts: number;
  sentAt: string | null;
  nextAttemptAt: string | null;
  error: string | null;
  pages: number;
  bytes: number;
  summary: string | null;
  dataAsOf: string | null;
  by: string | null;
};

export const deliveryStatus: Record<DeliveryStatus, { label: string; className: string }> = {
  pending: { label: "Ожидает", className: "text-warn" },
  sending: { label: "Отправляется", className: "text-accent-strong" },
  sent: { label: "Отправлен", className: "text-ok" },
  failed: { label: "Ошибка", className: "text-bad" },
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

/** Часовые пояса для выбора (IANA); можно ввести и другой. */
export const timeZones = ["Asia/Tashkent", "Asia/Samarkand", "Asia/Almaty", "Asia/Bishkek", "Asia/Dushanbe", "Europe/Moscow", "UTC"];

const base = "settings/telegram/reports";
const json = (body: unknown) => JSON.stringify(body);

export const reportsApi = {
  overview: () => bff<ReportOverview>(base),
  saveSettings: (accountId: string | null, language: string) => bff<ReportSettings>(`${base}/settings`, { method: "PUT", body: json({ accountId, language }) }),
  saveSchedule: (kind: ReportKind, s: Pick<ReportSchedule, "enabled" | "time" | "timeZone" | "days" | "metric">) =>
    bff<ReportSchedule>(`${base}/schedules/${kind}`, { method: "PUT", body: json(s) }),
  saveSubscription: (id: string | null, input: ReportSubscriptionInput) =>
    bff<ReportSubscription>(id ? `${base}/subscriptions/${id}` : `${base}/subscriptions`, { method: id ? "PUT" : "POST", body: json(input) }),
  deleteSubscription: (id: string) => bff<void>(`${base}/subscriptions/${id}`, { method: "DELETE" }),
  /** Отправить сейчас во все чаты автоотправки. Уже отправлен сегодня (409) — { conflict: текст }: повтор — только явным подтверждением (force). */
  send: async (id: string, kind: ReportKind, force: boolean): Promise<ReportSendResult[] | { conflict: string }> => {
    // Запрос один: повтор запроса здесь отправил бы отчёт дважды.
    const response = await fetch(`/bff/api/${base}/subscriptions/${id}/send`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: json({ kind, force }),
    }).catch(() => null);
    if (!response) {
      throw new Error(tNow("Нет связи с сервером OneBase — ответ не пришёл. Обновите страницу и проверьте, не выполнилось ли действие, затем повторите."));
    }

    const body = (await response.json().catch(() => null)) as ReportSendResult[] | { error?: string } | null;
    const error = body && !Array.isArray(body) ? body.error : undefined;
    if (response.status === 409) {
      return { conflict: tNow(error ?? "Уже отправлен сегодня.") };
    }

    if (!response.ok || !Array.isArray(body)) {
      throw new Error(response.status === 403 ? tNow("Недостаточно прав.") : tNow(error ?? "Ошибка {status}", { status: response.status }));
    }

    return body;
  },
  preview: (input: ReportPreviewInput) => bff<ReportPreview>(`${base}/preview`, { method: "POST", body: json(input) }),
  test: (input: ReportPreviewInput, chatId: string) => bff<ReportSendResult>(`${base}/test`, { method: "POST", body: json({ report: input, chatId }) }),
  deliveries: (kind = "", status = "", mode = "") =>
    bff<ReportDelivery[]>(`${base}/deliveries?kind=${encodeURIComponent(kind)}&status=${encodeURIComponent(status)}&mode=${encodeURIComponent(mode)}`),
};
