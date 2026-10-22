import { makeT, type T } from "@/i18n/core";
import { bff } from "./bff";

/** Без перевода (русский) — если вызывающий не передал t. */
const plain: T = makeT(null);

export type CampaignStatus = "Draft" | "Scheduled" | "Queued" | "Sending" | "Completed" | "CompletedWithErrors" | "Paused" | "Cancelled" | "Failed";

export type RecipientStatus = "Pending" | "Sending" | "Sent" | "Failed" | "Cancelled";

export type CampaignCounts = { total: number; pending: number; sending: number; sent: number; failed: number; cancelled: number };

export type CampaignListItem = {
  id: string;
  name: string;
  accountId: string;
  accountName: string;
  status: CampaignStatus;
  scheduledAt: string | null;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  createdBy: string | null;
  counts: CampaignCounts;
  /** Начало текста (до 160 символов). */
  textPreview: string | null;
  files: number;
};

export type CampaignRecipient = {
  id: string;
  chatId: string;
  peerId: number;
  title: string;
  username: string | null;
  type: "Private" | "Group" | "Channel";
  status: RecipientStatus;
  attempts: number;
  nextAttemptAt: string | null;
  sentAt: string | null;
  error: string | null;
};

export type CampaignAttachment = { id: string; kind: "file" | "report"; fileName: string; contentType: string; sizeBytes: number; reportId: string | null };

export type Campaign = {
  id: string;
  accountId: string;
  accountName: string;
  accountStatus: string;
  name: string;
  description: string | null;
  status: CampaignStatus;
  text: string | null;
  formatted: boolean;
  scheduledAt: string | null;
  timeZone: string;
  delaySeconds: number;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  lastError: string | null;
  createdBy: string | null;
  counts: CampaignCounts;
  attachments: CampaignAttachment[];
  recipients: CampaignRecipient[];
};

export type CampaignInput = {
  accountId: string;
  name: string;
  description: string | null;
  text: string | null;
  formatted: boolean;
  scheduledAt: string | null;
  delaySeconds: number;
};

export type CampaignAccount = { id: string; name: string; username: string | null; status: string };

/** Чат аккаунта; canSend = false — reason объясняет, почему писать нельзя. */
export type CampaignChat = {
  id: string;
  peerId: number;
  title: string;
  username: string | null;
  type: "Private" | "Group" | "Channel";
  canSend: boolean;
  added: boolean;
  reason: string | null;
};

export type RecipientsResult = { added: number; skipped: { title: string; reason: string }[] };

export type CampaignPreview = { html: string | null; parts: number; captionWithFile: boolean; warnings: string[]; recipients: number; unavailable: number };

export type UserReport = { id: string; title: string; createdAt: string };

const base = "telegram/campaigns";
const json = (body: unknown) => JSON.stringify(body);

export const campaignsApi = {
  list: (status = "") => bff<CampaignListItem[]>(`${base}?status=${encodeURIComponent(status)}`),
  accounts: () => bff<CampaignAccount[]>(`${base}/accounts`),
  reports: () => bff<UserReport[]>(`${base}/reports`),
  get: (id: string) => bff<Campaign>(`${base}/${id}`),
  create: (input: CampaignInput) => bff<Campaign>(base, { method: "POST", body: json(input) }),
  update: (id: string, input: CampaignInput) => bff<Campaign>(`${base}/${id}`, { method: "PUT", body: json(input) }),
  remove: (id: string) => bff<void>(`${base}/${id}`, { method: "DELETE" }),
  chats: (id: string, search = "", type = "") => bff<CampaignChat[]>(`${base}/${id}/chats?search=${encodeURIComponent(search)}&type=${encodeURIComponent(type)}`),
  syncChats: (id: string) => bff<{ chats: number }>(`${base}/${id}/chats/sync`, { method: "POST" }),
  addRecipients: (id: string, chatIds: string[]) => bff<RecipientsResult>(`${base}/${id}/recipients`, { method: "POST", body: json({ chatIds }) }),
  removeRecipient: (id: string, recipientId: string) => bff<void>(`${base}/${id}/recipients/${recipientId}`, { method: "DELETE" }),
  clearRecipients: (id: string) => bff<void>(`${base}/${id}/recipients`, { method: "DELETE" }),
  addReport: (id: string, reportId: string) => bff<CampaignAttachment>(`${base}/${id}/reports`, { method: "POST", body: json({ reportId }) }),
  removeAttachment: (id: string, attachmentId: string) => bff<void>(`${base}/${id}/attachments/${attachmentId}`, { method: "DELETE" }),
  preview: (id: string) => bff<CampaignPreview>(`${base}/${id}/preview`),
  test: (id: string, chatId: string) => bff<{ sentTo: string }>(`${base}/${id}/test`, { method: "POST", body: json({ chatId }) }),
  launch: (id: string) => bff<Campaign>(`${base}/${id}/launch`, { method: "POST" }),
  pause: (id: string) => bff<Campaign>(`${base}/${id}/pause`, { method: "POST" }),
  resume: (id: string) => bff<Campaign>(`${base}/${id}/resume`, { method: "POST" }),
  cancel: (id: string) => bff<Campaign>(`${base}/${id}/cancel`, { method: "POST" }),
  duplicate: (id: string) => bff<Campaign>("telegram/campaigns/" + id + "/duplicate", { method: "POST" }),
  retry: (id: string) => bff<Campaign>(`${base}/${id}/retry`, { method: "POST" }),
};

/** Файл — multipart, мимо JSON-обёртки bff (у неё Content-Type: application/json). */
export async function uploadCampaignFile(id: string, file: File, t: T = plain): Promise<CampaignAttachment> {
  const form = new FormData();
  form.append("file", file);
  const res = await fetch(`/bff/api/${base}/${id}/files`, { method: "POST", body: form });
  if (!res.ok) {
    let message = t("Файл не загрузился ({status})", { status: res.status });
    try {
      const error = ((await res.json()) as { error?: string }).error;
      if (error) message = t(error);
    } catch {
      // не JSON
    }
    throw new Error(res.status === 413 ? t("Файл слишком большой (до 50 МБ).") : message);
  }
  return (await res.json()) as CampaignAttachment;
}

export const campaignStatus: Record<CampaignStatus, { label: string; className: string }> = {
  Draft: { label: "Черновик", className: "bg-muted text-ink-2" },
  Scheduled: { label: "Запланирована", className: "bg-accent-soft text-accent-strong" },
  Queued: { label: "В очереди", className: "bg-accent-soft text-accent-strong" },
  Sending: { label: "Отправляется", className: "bg-accent-soft text-accent-strong" },
  Completed: { label: "Завершена", className: "bg-ok-soft text-ok" },
  CompletedWithErrors: { label: "Завершена с ошибками", className: "bg-warn-soft text-warn" },
  Paused: { label: "Пауза", className: "bg-warn-soft text-warn" },
  Cancelled: { label: "Отменена", className: "bg-muted text-ink-2" },
  Failed: { label: "Ошибка", className: "bg-bad-soft text-bad" },
};

export const recipientStatus: Record<RecipientStatus, { label: string; className: string }> = {
  Pending: { label: "Ожидает", className: "text-ink-2" },
  Sending: { label: "Отправляется", className: "text-accent-strong" },
  Sent: { label: "Отправлено", className: "text-ok" },
  Failed: { label: "Ошибка", className: "text-bad" },
  Cancelled: { label: "Отменено", className: "text-ink-3" },
};

/** Ссылка на файл рассылки (скачать ровно то, что ушло получателям). */
export const attachmentUrl = (id: string, attachmentId: string) => `/bff/api/telegram/campaigns/${id}/attachments/${attachmentId}`;

export const editable = (s: CampaignStatus) => s === "Draft" || s === "Scheduled";

export const active = (s: CampaignStatus) => s === "Queued" || s === "Sending";

/** Время по Ташкенту (UTC+5, без перехода на летнее время). */
export const tashkent = (iso: string | null) =>
  iso ? new Date(iso).toLocaleString("ru-RU", { timeZone: "Asia/Tashkent", dateStyle: "short", timeStyle: "short" }) : "—";

/** Значение поля datetime-local (время Ташкента) ↔ ISO UTC. */
export const fromLocal = (value: string) => (value ? new Date(`${value}:00+05:00`).toISOString() : null);

export function toLocal(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(new Date(iso).getTime() + 5 * 3600 * 1000);
  return d.toISOString().slice(0, 16);
}

export const size = (bytes: number, t: T = plain) =>
  bytes < 1024 * 1024 ? t("{n} КБ", { n: Math.max(1, Math.round(bytes / 1024)) }) : t("{n} МБ", { n: (bytes / 1024 / 1024).toFixed(1) });
