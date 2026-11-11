import type { AgentAccountOption } from "./agent-account";
import { bff } from "./bff";

export type { AgentAccountOption } from "./agent-account";
export { accountChange, accountLabel } from "./agent-account";

/** «Настройки → ИИ-агенты» (/api/agents): агенты отделов, готовые ответы, журнал ответов, диалоги отдела, проверка. */

export type AgentKind = "recruiter" | "assistant";

export type AgentListItem = {
  id: string;
  name: string;
  kind: AgentKind;
  enabled: boolean;
  isDefault: boolean;
  mode: "Auto" | "Approval";
  displayName: string | null;
  drafts: number;
  blocked: number;
  repliesToday: number;
  updatedAt: string | null;
};

export type SalesAgentItem = { accountId: string; name: string; username: string | null; status: string; enabled: boolean };

export type AgentDepartment = {
  code: string;
  name: string;
  kinds: AgentKind[];
  agents: AgentListItem[];
  salesAgents: SalesAgentItem[];
  telegramAccounts: number;
};

export type AgentAnswer = { id: string; question: string; answer: string; enabled: boolean };

export type AgentSettingsInput = {
  name: string;
  enabled: boolean;
  mode: "Auto" | "Approval";
  displayName: string | null;
  intro: string | null;
  tone: "friendly" | "neutral" | "formal";
  language: "auto" | "ru" | "uz";
  length: "short" | "normal" | "detailed";
  emoji: boolean;
  splitMessages: boolean;
  instructions: string | null;
  screeningQuestions: string | null;
  allowedTopics: string | null;
  forbiddenTopics: string | null;
  forbiddenPhrases: string | null;
  handoffPhrases: string | null;
  maxReplyChars: number;
  maxRepliesPerHour: number;
  maxRepliesPerDay: number;
  canDiscussSalary: boolean;
  canScheduleInterviews: boolean;
  handoffReply: string | null;
  workFrom: string | null;
  workTo: string | null;
  workDays: string;
  followUpHours: number;
  modelProvider: string | null;
  model: string | null;
};

export type AgentView = AgentSettingsInput & {
  id: string;
  department: string;
  departmentName: string;
  kind: AgentKind;
  isDefault: boolean;
  updatedAt: string | null;
  answers: AgentAnswer[];
  models: { provider: string; model: string; name: string }[];
  vacancies: { id: string; code: string; title: string; aiRecruiter: boolean }[];
  telegramAccounts: number;
  drafts: number;
  /** Номер Telegram, через который агент отвечает; null — любой номер отдела. */
  accountId: string | null;
  accounts: AgentAccountOption[];
  /** Может выбрать любой номер (номер начнёт работать на отдел). Иначе — только номера, которые уже работают на отдел. */
  canConfigureAccounts: boolean;
};


/** Проверка «почему агент может не отвечать»: ok, info, warn — ответ задержится или придёт не всем, bad — агент не ответит. */
export type AgentCheck = { status: "ok" | "info" | "warn" | "bad"; title: string; detail: string | null };

export type AgentHealthDialog = {
  id: string;
  name: string;
  at: string | null;
  accountName: string;
  department: string;
  ours: boolean;
  reason: string;
  mode: "Ai" | "Manual";
  outcome: string;
};

export type AgentHealth = { checks: AgentCheck[]; dialogs: AgentHealthDialog[]; checkedAt: string };

export type AgentReply = {
  id: string;
  conversationId: string;
  peerName: string | null;
  incoming: string | null;
  action: string;
  text: string;
  status: "Sent" | "Draft" | "Approved" | "Rejected" | "Blocked" | "Superseded";
  blockReason: string | null;
  rating: "good" | "bad" | null;
  correction: string | null;
  model: string | null;
  createdAt: string;
  reviewedBy: string | null;
  reviewedAt: string | null;
};

export type AgentRepliesPage = { items: AgentReply[]; drafts: number; blocked: number; bad: number };

export type AgentConversationRow = {
  id: string;
  accountId: string;
  name: string;
  username: string | null;
  mode: "Ai" | "Manual";
  lastText: string | null;
  lastMessageAt: string | null;
  waitsForAnswer: boolean;
};

export type AgentMessage = { direction: "In" | "Out"; text: string | null; fileName: string | null; author: string; authorName: string | null; at: string; status: string; error: string | null };

export type AgentConversation = { id: string; accountId: string; accountName: string; name: string; username: string | null; mode: "Ai" | "Manual"; messages: AgentMessage[] };

export type AgentTestMessage = { role: "user" | "agent"; text: string };

export type AgentTestResult = {
  action: string;
  messages: string[];
  handoffReason: string | null;
  blocked: string | null;
  notes: string[];
  model: string | null;
  inputTokens: number;
  outputTokens: number;
};

export const kindLabel: Record<AgentKind, string> = { recruiter: "Рекрутер", assistant: "Помощник отдела" };

export const replyStatusLabel: Record<AgentReply["status"], string> = {
  Sent: "Отправлен",
  Draft: "Ждёт проверки",
  Approved: "Одобрен и отправлен",
  Rejected: "Отклонён",
  Blocked: "Остановлен ограничением",
  Superseded: "Заменён новым",
};

export const actionLabel: Record<string, string> = {
  reply: "Ответ",
  propose_interview: "Приглашение на собеседование",
  handoff: "Передал сотруднику",
  consent: "Просьба о согласии (раньше)",
  booked: "Подтверждение записи",
  error: "Ошибка формата",
};

const base = "agents";
const body = (method: string, data?: unknown): RequestInit => ({ method, body: JSON.stringify(data ?? {}) });

export const agentsApi = {
  departments: () => bff<AgentDepartment[]>(`${base}/departments`),
  create: (department: string, kind: AgentKind, name: string) => bff<AgentView>(base, body("POST", { department, kind, name })),
  get: (id: string) => bff<AgentView>(`${base}/${id}`),
  save: (id: string, input: AgentSettingsInput) => bff<AgentView>(`${base}/${id}`, body("PUT", input)),
  remove: (id: string) => bff<void>(`${base}/${id}`, { method: "DELETE" }),
  makeDefault: (id: string) => bff<AgentView>(`${base}/${id}/default`, body("POST")),
  addAnswer: (id: string, input: Omit<AgentAnswer, "id">) => bff<AgentView>(`${base}/${id}/answers`, body("POST", input)),
  updateAnswer: (id: string, answerId: string, input: Omit<AgentAnswer, "id">) => bff<AgentView>(`${base}/${id}/answers/${answerId}`, body("PUT", input)),
  deleteAnswer: (id: string, answerId: string) => bff<AgentView>(`${base}/${id}/answers/${answerId}`, { method: "DELETE" }),
  replies: (id: string, filter?: string) => bff<AgentRepliesPage>(`${base}/${id}/replies${filter ? `?filter=${filter}` : ""}`),
  approve: (replyId: string, text: string | null) => bff<AgentRepliesPage>(`${base}/replies/${replyId}/approve`, body("POST", { text })),
  reject: (replyId: string) => bff<AgentRepliesPage>(`${base}/replies/${replyId}/reject`, body("POST")),
  rate: (replyId: string, rating: "good" | "bad" | null, correction: string | null, addToAnswers: boolean) =>
    bff<AgentRepliesPage>(`${base}/replies/${replyId}/rate`, body("POST", { rating, correction, addToAnswers })),
  conversations: (id: string) => bff<AgentConversationRow[]>(`${base}/${id}/conversations`),
  conversation: (id: string, conversationId: string) => bff<AgentConversation>(`${base}/${id}/conversations/${conversationId}`),
  send: (id: string, conversationId: string, text: string, clientKey: string) =>
    bff<AgentConversation>(`${base}/${id}/conversations/${conversationId}/messages`, body("POST", { text, clientKey })),
  mode: (id: string, conversationId: string, mode: "Ai" | "Manual") => bff<AgentConversation>(`${base}/${id}/conversations/${conversationId}/mode`, body("PUT", { mode })),
  health: (id: string) => bff<AgentHealth>(`${base}/${id}/health`),
  account: (id: string, accountId: string | null) => bff<AgentView>(`${base}/${id}/account`, body("PUT", { accountId })),
  test: (id: string, messages: AgentTestMessage[], vacancyId: string | null) => bff<AgentTestResult>(`${base}/${id}/test`, body("POST", { messages, vacancyId })),
};

/** Настройки агента из его представления — для формы и сохранения. */
export function settingsOf(v: AgentView): AgentSettingsInput {
  const {
    name, enabled, mode, displayName, intro, tone, language, length, emoji, splitMessages, instructions, screeningQuestions, allowedTopics, forbiddenTopics,
    forbiddenPhrases, handoffPhrases, maxReplyChars, maxRepliesPerHour, maxRepliesPerDay, canDiscussSalary, canScheduleInterviews, handoffReply, workFrom, workTo,
    workDays, followUpHours, modelProvider, model,
  } = v;
  return {
    name, enabled, mode, displayName, intro, tone, language, length, emoji, splitMessages, instructions, screeningQuestions, allowedTopics, forbiddenTopics,
    forbiddenPhrases, handoffPhrases, maxReplyChars, maxRepliesPerHour, maxRepliesPerDay, canDiscussSalary, canScheduleInterviews, handoffReply, workFrom, workTo,
    workDays, followUpHours, modelProvider, model,
  };
}

