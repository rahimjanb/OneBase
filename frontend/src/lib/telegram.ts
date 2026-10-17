import { bff } from "./bff";

export type TelegramAccountStatus = "New" | "CodeRequired" | "PasswordRequired" | "Connected" | "Disconnected" | "Error";

export type TelegramCredentials = { configured: boolean; apiId: number | null; apiHashHint: string | null; updatedAt: string | null };

export type TelegramAccount = {
  id: string;
  name: string;
  phoneHint: string | null;
  status: TelegramAccountStatus;
  telegramUserId: number | null;
  username: string | null;
  displayName: string | null;
  connectedAt: string | null;
  lastActivityAt: string | null;
  lastError: string | null;
  agentEnabled: boolean;
  permissions: number;
  chats: number;
  /** Клиент в памяти сервера: подключён ли и когда Telegram в последний раз присылал события (приём сообщений жив). */
  live: TelegramLive | null;
};

export type TelegramLive = { online: boolean; events: number; lastEventAt: string | null; messages: number; lastMessageAt: string | null };

export type TelegramOverview = { credentials: TelegramCredentials; accounts: TelegramAccount[] };

export type TelegramLogin = { status: TelegramAccountStatus; account: TelegramAccount };

export type TelegramAgentSettings = {
  accountId: string;
  enabled: boolean;
  autoReply: boolean;
  replyToUnknown: boolean;
  modelProvider: string | null;
  model: string | null;
  systemPrompt: string | null;
  restrictions: string | null;
  responseStyle: "short" | "normal" | "detailed";
  language: "auto" | "ru" | "uz" | "en";
  maxMessagesPerHour: number;
  maxOutputTokens: number;
  replyDelaySeconds: number;
  noInfoReply: string;
  allowedAttachments: string;
  maxFileMb: number;
  allowReports: boolean;
  /** Кем агент представляется; пусто — «сотрудник компании Candy Gold». */
  persona: string | null;
  fullAccessUserId: string | null;
  updatedAt: string | null;
};

export type TelegramMode = "Full" | "SentOnly";

/** active — права действуют; pending — Telegram ID определится по первому сообщению; no_rights — для полного доступа не выбран пользователь OneBase; disabled. */
export type PermissionState = "active" | "pending" | "no_rights" | "disabled";

export type TelegramPermission = {
  id: string;
  telegramUserId: number | null;
  username: string | null;
  title: string | null;
  mode: TelegramMode;
  userId: string | null;
  /** Имя пользователя OneBase, чьи права наследует полный доступ. */
  oneBaseUser: string | null;
  allowReports: boolean;
  enabled: boolean;
  verifiedAt: string | null;
  state: PermissionState;
  createdAt: string;
};

export type PermissionInput = {
  telegramUserId: number | null;
  username: string | null;
  title: string | null;
  mode: TelegramMode;
  userId: string | null;
  allowReports: boolean;
  enabled: boolean;
};

export type TelegramChatType = "Private" | "Group" | "Channel";

export type TelegramChat = {
  id: string;
  peerId: number;
  type: TelegramChatType;
  title: string;
  username: string | null;
  canSend: boolean;
  agentEnabled: boolean;
  syncedAt: string | null;
  /** Почему писать нельзя (если canSend = false). */
  reason: string | null;
};

export type TelegramExecution = {
  id: string;
  createdAt: string;
  peerId: number;
  chatTitle: string | null;
  telegramUserId: number | null;
  who: string | null;
  mode: "full" | "sent_only" | "link";
  status: "answered" | "no_info" | "refused" | "rate_limited" | "error" | "linked";
  question: string | null;
  answer: string | null;
  reportId: string | null;
  model: string | null;
  inputTokens: number;
  outputTokens: number;
  durationMs: number;
  error: string | null;
};

export type OneBaseUser = { id: string; fullName: string; login: string; position: string | null };

export type LinkCode = { code: string; expiresAt: string; sendTo: string[] };

export type TelegramLink = {
  permissionId: string;
  account: string;
  accountUsername: string | null;
  telegramUserId: number | null;
  username: string | null;
  mode: TelegramMode;
  verifiedAt: string | null;
};

/** Кому отвечает агент: всем (не из списка — по отправленным данным) или только списку; с чьими правами OneBase — полный доступ. */
export type TelegramAccess = { replyToAll: boolean; fullAccessUserId: string | null; fullAccessUserName: string | null };

export type AgentTestTurn = { question: string; answer: string };

export type AgentTestInput = {
  chatId: string | null;
  permissionId: string | null;
  question: string;
  history: AgentTestTurn[];
  sampleMaterial: string | null;
  conversationId: string | null;
};

export type AgentTestResult = {
  mode: "full" | "sent_only" | "ignored";
  status: "answered" | "no_info" | "refused" | "ignored" | "error";
  note: string | null;
  replies: string[];
  files: string[];
  model: string | null;
  inputTokens: number;
  outputTokens: number;
  durationMs: number;
  conversationId: string | null;
  materials: number;
  error: string | null;
};

const base = "settings/telegram";
const json = (body: unknown) => JSON.stringify(body);

export const telegramApi = {
  overview: () => bff<TelegramOverview>(base),
  saveCredentials: (apiId: number, apiHash: string | null) => bff<TelegramOverview>(`${base}/credentials`, { method: "PUT", body: json({ apiId, apiHash }) }),
  createAccount: (name: string) => bff<TelegramAccount>(`${base}/accounts`, { method: "POST", body: json({ name }) }),
  renameAccount: (id: string, name: string) => bff<TelegramAccount>(`${base}/accounts/${id}`, { method: "PUT", body: json({ name }) }),
  deleteAccount: (id: string) => bff<void>(`${base}/accounts/${id}`, { method: "DELETE" }),
  startLogin: (id: string, phone: string) => bff<TelegramLogin>(`${base}/accounts/${id}/login`, { method: "POST", body: json({ phone }) }),
  submitCode: (id: string, code: string) => bff<TelegramLogin>(`${base}/accounts/${id}/login/code`, { method: "POST", body: json({ code }) }),
  submitPassword: (id: string, password: string) => bff<TelegramLogin>(`${base}/accounts/${id}/login/password`, { method: "POST", body: json({ password }) }),
  check: (id: string) => bff<TelegramAccount>(`${base}/accounts/${id}/check`, { method: "POST" }),
  disconnect: (id: string) => bff<TelegramAccount>(`${base}/accounts/${id}/disconnect`, { method: "POST" }),
  settings: (id: string) => bff<TelegramAgentSettings>(`${base}/accounts/${id}/agent`),
  saveSettings: (id: string, s: TelegramAgentSettings) => bff<TelegramAgentSettings>(`${base}/accounts/${id}/agent`, { method: "PUT", body: json(s) }),
  permissions: (id: string, search = "", mode = "") =>
    bff<TelegramPermission[]>(`${base}/accounts/${id}/users?search=${encodeURIComponent(search)}&mode=${encodeURIComponent(mode)}`),
  savePermission: (id: string, permissionId: string | null, input: PermissionInput) =>
    bff<TelegramPermission>(permissionId ? `${base}/accounts/${id}/users/${permissionId}` : `${base}/accounts/${id}/users`, {
      method: permissionId ? "PUT" : "POST",
      body: json(input),
    }),
  deletePermission: (id: string, permissionId: string) => bff<void>(`${base}/accounts/${id}/users/${permissionId}`, { method: "DELETE" }),
  bulk: (id: string, ids: string[], action: "enable" | "disable" | "sent_only" | "delete") =>
    bff<{ changed: number }>(`${base}/accounts/${id}/users/bulk`, { method: "POST", body: json({ ids, action }) }),
  oneBaseUsers: (search: string) => bff<OneBaseUser[]>(`${base}/onebase-users?search=${encodeURIComponent(search)}`),
  chats: (id: string, search = "", type = "") => bff<TelegramChat[]>(`${base}/accounts/${id}/chats?search=${encodeURIComponent(search)}&type=${encodeURIComponent(type)}`),
  syncChats: (id: string) => bff<{ chats: number }>(`${base}/accounts/${id}/chats/sync`, { method: "POST" }),
  chatAgent: (id: string, chatId: string, enabled: boolean) =>
    bff<TelegramChat>(`${base}/accounts/${id}/chats/${chatId}/agent`, { method: "PUT", body: json({ enabled }) }),
  access: (id: string) => bff<TelegramAccess>(`${base}/accounts/${id}/access`),
  saveAccess: (id: string, replyToAll: boolean, fullAccessUserId: string | null) =>
    bff<TelegramAccess>(`${base}/accounts/${id}/access`, { method: "PUT", body: json({ replyToAll, fullAccessUserId }) }),
  testAgent: (id: string, input: AgentTestInput) => bff<AgentTestResult>(`${base}/accounts/${id}/agent/test`, { method: "POST", body: json(input) }),
  executions: (id: string, status = "") => bff<TelegramExecution[]>(`${base}/accounts/${id}/executions?status=${encodeURIComponent(status)}`),
};

export const myTelegramApi = {
  links: () => bff<TelegramLink[]>("me/telegram"),
  code: () => bff<LinkCode>("me/telegram/code", { method: "POST" }),
  unlink: (permissionId: string) => bff<void>(`me/telegram/${permissionId}`, { method: "DELETE" }),
};

export const accountStatus: Record<TelegramAccountStatus, { label: string; className: string }> = {
  New: { label: "Не подключён", className: "bg-muted text-ink-2" },
  CodeRequired: { label: "Ждёт код", className: "bg-warn-soft text-warn" },
  PasswordRequired: { label: "Ждёт пароль 2FA", className: "bg-warn-soft text-warn" },
  Connected: { label: "Подключён", className: "bg-ok-soft text-ok" },
  Disconnected: { label: "Отключён", className: "bg-muted text-ink-2" },
  Error: { label: "Ошибка", className: "bg-bad-soft text-bad" },
};

export const permissionState: Record<PermissionState, { label: string; className: string }> = {
  active: { label: "Действует", className: "text-ok" },
  pending: { label: "Ждёт первого сообщения", className: "text-warn" },
  no_rights: { label: "Нет прав OneBase — отвечает по отправленным", className: "text-warn" },
  disabled: { label: "Выключен", className: "text-ink-3" },
};

/** «Последнее событие 2 мин назад». */
export function ago(iso: string | null): string {
  if (!iso) return "—";
  const s = Math.max(0, Math.round((Date.now() - new Date(iso).getTime()) / 1000));
  if (s < 60) return "только что";
  if (s < 3600) return `${Math.round(s / 60)} мин назад`;
  if (s < 86400) return `${Math.round(s / 3600)} ч назад`;
  return new Date(iso).toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" });
}

export const modeLabel: Record<TelegramMode, string> = {
  Full: "Полный доступ",
  SentOnly: "Только отправленные данные",
};

export const executionStatus: Record<TelegramExecution["status"], { label: string; className: string }> = {
  answered: { label: "Ответил", className: "text-ok" },
  no_info: { label: "Нет данных", className: "text-warn" },
  refused: { label: "Отказ", className: "text-ink-2" },
  rate_limited: { label: "Лимит", className: "text-warn" },
  error: { label: "Ошибка", className: "text-bad" },
  linked: { label: "Привязка", className: "text-accent-strong" },
};

export const chatTypeLabel: Record<TelegramChatType, string> = { Private: "Личный", Group: "Группа", Channel: "Канал" };
