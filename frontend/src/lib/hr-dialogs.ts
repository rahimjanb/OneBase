import { bff } from "./bff";
import type { DialogMode } from "./hr-dialogs-view";

/** «Диалоги Telegram»: типы ответа API и запросы (/api/hr/recruitment/telegram/dialogs…). Логика экрана без запросов — hr-dialogs-view.ts. */

export type HrNumber = {
  accountId: string;
  name: string;
  phoneHint: string | null;
  username: string | null;
  connected: boolean;
  online: boolean;
  /** Telegram остановил отправку с номера (PEER_FLOOD) до этого времени. */
  pausedUntil: string | null;
};

export type DialogSummary = {
  number: HrNumber | null;
  canConfigure: boolean;
  counts: { total: number; ai: number; needHr: number; unread: number };
};

export type DialogRow = {
  id: string;
  candidateId: string | null;
  name: string;
  username: string | null;
  vacancyTitle: string | null;
  mode: DialogMode;
  lastText: string | null;
  lastAuthor: "candidate" | "ai" | "auto" | "hr" | null;
  lastAt: string | null;
  unread: number;
  handoffReason: string | null;
};

export type DialogItem = {
  kind: "candidate" | "ai" | "auto" | "hr" | "system";
  text: string | null;
  at: string;
  author: string | null;
  self: boolean;
  status: "sent" | "pending" | "failed" | "unknown";
  error: string | null;
  outgoingId: string | null;
  attachment: { fileName: string; size: number | null; resumeId: string | null; image: boolean; canPreview: boolean } | null;
  event: "handoff_to_hr" | "hr_took_over" | "returned_to_ai" | "interview_scheduled" | null;
};

export type DialogCandidate = {
  id: string;
  name: string;
  city: string | null;
  username: string | null;
  phone: string | null;
  applicationId: string | null;
  vacancyId: string | null;
  vacancyTitle: string | null;
  vacancyPlace: string | null;
  source: string | null;
  stage: string | null;
  interviewAt: string | null;
  interviewTimeZone: string | null;
  score: { total: number; criteria: { label: string; value: number }[] } | null;
  form: { label: string; value: string }[];
  language: string | null;
};

export type Dialog = {
  id: string;
  accountId: string;
  name: string;
  username: string | null;
  mode: DialogMode;
  handoffReason: string | null;
  lastInboundAt: string | null;
  items: DialogItem[];
  candidate: DialogCandidate | null;
  templates: { key: string; label: string; text: string }[];
  canManage: boolean;
};

const tg = "hr/recruitment/telegram";
const post = (body?: unknown): RequestInit => ({ method: "POST", body: JSON.stringify(body ?? {}) });

export const dialogsApi = {
  summary: () => bff<DialogSummary>(`${tg}/dialogs/summary`),
  list: () => bff<DialogRow[]>(`${tg}/dialogs`),
  get: (id: string) => bff<Dialog>(`${tg}/dialogs/${id}`),
  send: (id: string, text: string, clientKey: string) => bff<unknown>(`${tg}/conversations/${id}/messages`, post({ text, clientKey })),
  mode: (id: string, mode: "Ai" | "Manual") => bff<unknown>(`${tg}/conversations/${id}/mode`, { method: "PUT", body: JSON.stringify({ mode }) }),
  retry: (id: string, outgoingId: string) => bff<Dialog>(`${tg}/dialogs/${id}/retry/${outgoingId}`, post()),
  draft: (id: string) => bff<{ text: string }>(`${tg}/dialogs/${id}/draft`, post()),
  askAdmin: () => bff<{ notified: number }>(`${tg}/dialogs/ask-admin`, post()),
  /** Файл с подписью: multipart (заголовок с границей ставит браузер). */
  sendFile: async (id: string, file: File, text: string, clientKey: string): Promise<Dialog> => {
    const form = new FormData();
    form.append("file", file);
    form.append("text", text);
    form.append("clientKey", clientKey);
    const response = await fetch(`/bff/api/${tg}/dialogs/${id}/files`, { method: "POST", body: form });
    if (!response.ok) {
      const body = await response.text();
      let message = body;
      try {
        message = JSON.parse(body).error ?? body;
      } catch {
        // не JSON
      }
      throw new Error(response.status === 403 ? "Недостаточно прав." : message || `Ошибка ${response.status}`);
    }
    return (await response.json()) as Dialog;
  },
};

export const DIALOGS_PATH = "/hr/recruitment/telegram";
export const dialogPath = (id: string) => `${DIALOGS_PATH}/${id}`;
