import { bff } from "./bff";
import type { Stage, Vacancy } from "./recruitment";

/** Этапы 3–7 подбора: Telegram HR, HH.uz, AI-оценка, собеседования, исключения, настройки AI, аналитика (/api/hr/recruitment/...). */

export type HrAccount = {
  id: string;
  name: string;
  username: string | null;
  status: string;
  online: boolean;
  pausedUntil: string | null;
  pauseReason: string | null;
  dailyLimit: number;
  minIntervalSeconds: number;
  sentToday: number;
};

export type HrConversationRow = {
  id: string;
  accountId: string;
  peerId: number;
  candidateId: string | null;
  name: string;
  username: string | null;
  mode: "Ai" | "Manual";
  lastText: string | null;
  lastMessageAt: string | null;
  lastInboundAt: string | null;
};

export type HrMessage = {
  id: number | null;
  direction: "In" | "Out";
  text: string | null;
  fileName: string | null;
  resumeId: string | null;
  author: string;
  authorName: string | null;
  at: string;
  status: string;
  error: string | null;
};

export type HrConversation = {
  id: string;
  accountId: string;
  accountName: string;
  peerId: number;
  candidateId: string | null;
  name: string;
  username: string | null;
  mode: "Ai" | "Manual";
  messages: HrMessage[];
};

export type PublicationChat = { id: string; title: string; username: string | null; type: string; canSend: boolean; reason: string | null };

export type Publication = {
  id: string;
  vacancyId: string;
  vacancyCode: string;
  chatTitle: string;
  status: string;
  scheduledAt: string;
  link: string | null;
  error: string | null;
  text: string;
  createdAt: string;
};

export type Source = { id: string; accountId: string; title: string; category: string; collectLeads: boolean; note: string | null; leads: number };

export type Lead = {
  id: string;
  chatTitle: string;
  fromName: string | null;
  fromUsername: string | null;
  excerpt: string;
  keywords: string;
  status: string;
  candidateId: string | null;
  createdAt: string;
};

export type HhStatus = {
  configured: boolean;
  connected: boolean;
  enabled: boolean;
  clientId: string | null;
  hasSecret: boolean;
  redirectUri: string | null;
  contactEmail: string | null;
  employerName: string | null;
  tokenExpiresAt: string | null;
  lastSyncAt: string | null;
  lastSyncOk: boolean | null;
  lastSyncMessage: string | null;
  linkedVacancies: number;
  /** Предохранитель Quota Guard: HH ответил, что нужна оплата или лимит исчерпан — автоматическая синхронизация остановлена. */
  guardTrippedAt: string | null;
  guardReason: string | null;
  /** Резюме, полученные через API HH, можно передавать AI (после подтверждения юриста). */
  aiOnHhData: boolean;
};

export type HhVacancyLink = { hhVacancyId: string; name: string; area: string | null; vacancyId: string | null; vacancyCode: string | null };

export type HhNegotiation = {
  id: string;
  negotiationId: string;
  state: string | null;
  hhUpdatedAt: string | null;
  lastError: string | null;
  actions: { id: string; name: string; method: string; url: string }[];
};

export type Score = {
  id: string;
  applicationId: string;
  overall: number;
  summary: string;
  criteria: { name: string; score: number; explanation: string; evidence: string | null; verified: boolean }[];
  risks: string | null;
  model: string | null;
  createdAt: string;
};

export type Slot = { startsAt: string; endsAt: string };

export type InterviewRow = {
  id: string;
  applicationId: string;
  candidateId: string;
  candidateName: string;
  vacancyId: string;
  vacancyCode: string;
  vacancyTitle: string;
  startsAt: string | null;
  endsAt: string | null;
  format: string;
  location: string | null;
  status: string;
  interviewers: string[];
  proposedSlots: Slot[];
  confirmedAt: string | null;
  candidateConfirmation: "Pending" | "Confirmed" | "Declined" | "ByHr";
  interviewer: string | null;
  department: string | null;
  timeZone: string;
  meetingUrl: string | null;
  holdUntil: string | null;
  previousStartsAt: string | null;
  cancelledBy: string | null;
  cancelReason: string | null;
  resumeId: string | null;
  aiScore: number | null;
  aiSummary: string | null;
  calendarEventId: string | null;
  feedback: Feedback[] | null;
};

export type InterviewResult = "Passed" | "Rejected" | "AdditionalInterview" | "WaitingDecision";

export type Feedback = {
  id: string;
  authorId: string;
  author: string;
  result: InterviewResult;
  technical: number | null;
  communication: number | null;
  professional: number | null;
  strengths: string | null;
  weaknesses: string | null;
  comments: string | null;
  recommendation: string | null;
  nextStage: Stage | null;
  at: string;
};

export type FeedbackInput = Omit<Feedback, "id" | "authorId" | "author" | "at">;

/** Настройки собеседования вакансии: свои значения (null — общие) и что действует (effective…). */
export type InterviewSettings = {
  vacancyId: string;
  interviewerId: string | null;
  participants: string[];
  durationMinutes: number | null;
  days: string | null;
  dayStart: string | null;
  dayEnd: string | null;
  breaks: string | null;
  minNoticeHours: number | null;
  maxAdvanceDays: number | null;
  format: string | null;
  address: string | null;
  meetingUrl: string | null;
  timeZone: string | null;
  inviteMinScore: number | null;
  instructions: string | null;
  effectiveInterviewerId: string | null;
  effectiveDuration: number;
  effectiveDays: string;
  effectiveDayStart: string;
  effectiveDayEnd: string;
  effectiveNotice: number;
  effectiveHorizon: number;
  effectiveFormat: string;
  effectiveTimeZone: string;
};

export const interviewViews: [string, string][] = [
  ["today", "Сегодня"],
  ["tomorrow", "Завтра"],
  ["week", "Эта неделя"],
  ["upcoming", "Предстоящие"],
  ["completed", "Прошедшие"],
  ["cancelled", "Отменённые"],
  ["rescheduled", "Перенесённые"],
];

export const confirmationLabel: Record<string, string> = {
  Pending: "ждём подтверждения кандидата",
  Confirmed: "кандидат подтвердил",
  Declined: "кандидат отказался",
  ByHr: "подтверждено со слов кандидата",
};

export const resultLabel: Record<InterviewResult, string> = {
  Passed: "Прошёл",
  Rejected: "Отказ",
  AdditionalInterview: "Нужно ещё собеседование",
  WaitingDecision: "Ждёт решения",
};

export const cancelledByLabel: Record<string, string> = { hr: "HR", candidate: "кандидат", system: "система" };

export type ExceptionRow = {
  id: string;
  kind: string;
  message: string;
  candidateId: string | null;
  candidateName: string | null;
  vacancyId: string | null;
  vacancyCode: string | null;
  conversationId: string | null;
  status: string;
  createdAt: string;
  resolvedBy: string | null;
  resolvedAt: string | null;
  resolution: string | null;
};

/** Настройки подбора: собеседования, автооценка, распознавание фото и сканов. AI-рекрутер — в «Настройки → ИИ-агенты» (recruiterAgentId — ссылка). */
export type AiSettings = {
  interviewDayStart: string;
  interviewDayEnd: string;
  interviewSlotMinutes: number;
  interviewDays: string;
  autoScore: boolean;
  readScans: boolean;
  /** Разбирать резюме AI (поля, сводка) и оценивать под вакансию; выключено — только контакты из текста. */
  analyzeResumes: boolean;
  recruiterAgentId: string | null;
  recruiterEnabled: boolean;
};

export type Analytics = {
  vacancies: { vacancyId: string; code: string; title: string; status: string; total: number; stages: Record<Stage, number>; interviews: number; hired: number; daysToHire: number | null }[];
  sources: { source: string; candidates: number; hired: number }[];
  aiReplies: number;
  handoffs: number;
  openExceptions: number;
  candidates: number;
  conversations: number;
  medianReplyMinutes: number | null;
};

/** ИИ-рекрутер HR для выбора у вакансии (настраивается в «Настройки → ИИ-агенты»). */
export type RecruiterBrief = { id: string; name: string; enabled: boolean; isDefault: boolean; displayName: string | null };

export type Person = { id: string; name: string; position: string | null; department: string | null };

export const exceptionLabel: Record<string, string> = {
  human_requested: "Кандидат просит человека",
  salary: "Торг о зарплате",
  conditions: "Спорные условия",
  unclear: "Непонятный ответ",
  technical: "Техническая ошибка",
  telegram: "Проблема Telegram",
  hh: "Проблема HH.uz",
  schedule: "Конфликт расписания",
  duplicate: "Возможный дубль",
  send_failed: "Сообщение не отправлено",
  review: "Нужно решение HR",
  blocked: "Ответ AI остановлен ограничением",
};

export const formatLabel: Record<string, string> = { office: "В офисе", online: "Онлайн", phone: "По телефону" };

export const interviewStatusLabel: Record<string, string> = {
  Proposed: "Предложены варианты",
  Awaiting: "Ждёт подтверждения",
  Confirmed: "Назначено",
  Rescheduled: "Перенесено",
  Completed: "Прошло",
  Cancelled: "Отменено",
  NoShow: "Не пришёл",
};

export const outboxLabel: Record<string, string> = {
  Pending: "в очереди",
  Sending: "отправляется",
  Sent: "отправлено",
  Failed: "не отправлено",
  Unknown: "неизвестно, дошло ли",
  Cancelled: "отменено",
};

export const sourceCategoryLabel: Record<string, string> = {
  jobs: "Вакансии и поиск работы",
  professional: "Профессиональное сообщество",
  channel: "Профильный канал",
  other: "Другое",
};

/** Время по Ташкенту: «пн, 10.11, 10:00». */
/** Отклик для окна «Новое собеседование»: кандидат, вакансия, этап, действующее собеседование (если уже есть) и есть ли диалог в Telegram. */
export type BookableRow = {
  applicationId: string;
  candidateId: string;
  candidateName: string;
  phone: string | null;
  vacancyId: string;
  vacancyCode: string;
  vacancyTitle: string;
  stage: string;
  interviewId: string | null;
  interviewStatus: string | null;
  interviewStartsAt: string | null;
  hasTelegram: boolean;
};

/** Время собеседования из дня и часа по Ташкенту («2026-11-13», «15:00») — ISO с поясом +05:00. */
export const tashkentIso = (date: string, time: string) => `${date}T${time.slice(0, 5)}:00+05:00`;

/** Час по Ташкенту («15:00») из ISO. */
export const tashkentTime = (iso: string) =>
  new Date(iso).toLocaleTimeString("ru-RU", { timeZone: "Asia/Tashkent", hour: "2-digit", minute: "2-digit" });

export const slotText = (iso: string) =>
  new Date(iso).toLocaleString("ru-RU", { timeZone: "Asia/Tashkent", weekday: "short", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" });

const base = "hr/recruitment";
const tg = `${base}/telegram`;
const hh = `${base}/hh`;
const body = (method: string, data?: unknown): RequestInit => ({ method, body: JSON.stringify(data ?? {}) });
const qs = (q: Record<string, string | number | boolean | null | undefined>) => {
  const s = Object.entries(q)
    .filter(([, v]) => v !== null && v !== undefined && v !== "")
    .map(([k, v]) => `${k}=${encodeURIComponent(String(v))}`)
    .join("&");
  return s ? `?${s}` : "";
};

export const recruitmentTelegramApi = {
  accounts: () => bff<HrAccount[]>(`${tg}/accounts`),
  conversations: (accountId?: string, search?: string) => bff<HrConversationRow[]>(`${tg}/conversations${qs({ accountId, search })}`),
  conversation: (id: string) => bff<HrConversation>(`${tg}/conversations/${id}`),
  send: (id: string, text: string, clientKey: string) => bff<HrConversation>(`${tg}/conversations/${id}/messages`, body("POST", { text, clientKey })),
  mode: (id: string, mode: "Ai" | "Manual") => bff<HrConversation>(`${tg}/conversations/${id}/mode`, body("PUT", { mode })),
  chats: (accountId: string) => bff<PublicationChat[]>(`${tg}/accounts/${accountId}/chats`),
  template: (vacancyId: string, accountId: string) => bff<{ text: string }>(`${tg}/vacancies/${vacancyId}/template${qs({ accountId })}`),
  publications: (vacancyId?: string) => bff<Publication[]>(`${tg}/publications${qs({ vacancyId })}`),
  publish: (input: { vacancyId: string; accountId: string; chatIds: string[]; text: string; scheduledAt: string | null }) =>
    bff<Publication[]>(`${tg}/publications`, body("POST", input)),
  cancelPublication: (id: string) => bff<Publication[]>(`${tg}/publications/${id}/cancel`, body("POST")),
  sources: () => bff<Source[]>(`${tg}/sources`),
  addSource: (input: { accountId: string; chatId: string; category: string; collectLeads: boolean; note: string | null }) => bff<Source[]>(`${tg}/sources`, body("POST", input)),
  removeSource: (id: string) => bff<Source[]>(`${tg}/sources/${id}`, { method: "DELETE" }),
  leads: (status?: string) => bff<Lead[]>(`${tg}/leads${qs({ status })}`),
  convertLead: (id: string, vacancyId: string | null) => bff<{ candidateId: string }>(`${tg}/leads/${id}/convert`, body("POST", { vacancyId })),
  ignoreLead: (id: string) => bff<Lead[]>(`${tg}/leads/${id}/ignore`, body("POST")),
};

export const hhApi = {
  status: () => bff<HhStatus>(hh),
  save: (input: { clientId: string | null; clientSecret: string | null; redirectUri: string | null; contactEmail: string | null; enabled: boolean; aiOnHhData?: boolean }) =>
    bff<HhStatus>(`${hh}/settings`, body("PUT", input)),
  connect: () => bff<{ url: string }>(`${hh}/connect`, body("POST")),
  complete: (code: string, state: string) => bff<HhStatus>(`${hh}/complete`, body("POST", { code, state })),
  disconnect: () => bff<HhStatus>(`${hh}/disconnect`, body("POST")),
  vacancies: () => bff<HhVacancyLink[]>(`${hh}/vacancies`),
  link: (vacancyId: string, hhVacancyId: string | null) => bff<void>(`${hh}/link`, body("POST", { vacancyId, hhVacancyId })),
  sync: () => bff<{ vacancies: number; responses: number; newCandidates: number; errors: number; message: string }>(`${hh}/sync`, body("POST")),
  negotiations: (candidateId: string) => bff<HhNegotiation[]>(`${hh}/candidates/${candidateId}/negotiations`),
  action: (id: string, actionId: string, message: string | null) => bff<HhNegotiation[]>(`${hh}/negotiations/${id}/action`, body("POST", { actionId, message })),
};

export const flowApi = {
  score: (applicationId: string) => bff<Score>(`${base}/applications/${applicationId}/score`, body("POST")),
  scores: (candidateId: string) => bff<Score[]>(`${base}/candidates/${candidateId}/scores`),
  postText: (vacancyId: string, accountId: string, audience: string) =>
    bff<{ text: string }>(`${base}/vacancies/${vacancyId}/post-text`, body("POST", { accountId, audience })),
  interviews: (upcoming = true) => bff<InterviewRow[]>(`${base}/interviews${qs({ upcoming })}`),
  interviewsView: (view: string) => bff<InterviewRow[]>(`${base}/interviews${qs({ view })}`),
  interview: (id: string) => bff<InterviewRow>(`${base}/interviews/${id}`),
  reschedule: (id: string, startsAt: string) => bff<InterviewRow>(`${base}/interviews/${id}/reschedule`, body("POST", { startsAt })),
  cancelInterview: (id: string, reason: string | null) => bff<InterviewRow>(`${base}/interviews/${id}/cancel`, body("POST", { reason })),
  confirmInterview: (id: string) => bff<InterviewRow>(`${base}/interviews/${id}/confirm`, body("POST")),
  feedback: (id: string, input: FeedbackInput) => bff<InterviewRow>(`${base}/interviews/${id}/feedback`, body("POST", input)),
  interviewSettings: (vacancyId: string) => bff<InterviewSettings>(`${base}/vacancies/${vacancyId}/interview-settings`),
  saveInterviewSettings: (vacancyId: string, input: Omit<InterviewSettings, "vacancyId" | `effective${string}`>) =>
    bff<InterviewSettings>(`${base}/vacancies/${vacancyId}/interview-settings`, body("PUT", input)),
  candidateInterviews: (candidateId: string) => bff<InterviewRow[]>(`${base}/candidates/${candidateId}/interviews`),
  interviewers: (vacancyId: string) => bff<string[]>(`${base}/vacancies/${vacancyId}/interviewers`),
  setInterviewers: (vacancyId: string, userIds: string[]) => bff<string[]>(`${base}/vacancies/${vacancyId}/interviewers`, body("PUT", { userIds })),
  slots: (vacancyId: string) => bff<Slot[]>(`${base}/vacancies/${vacancyId}/slots`),
  /** Свободное время интервьюеров вакансии на день (все варианты по сетке вакансии). */
  daySlots: (vacancyId: string, date: string) => bff<Slot[]>(`${base}/vacancies/${vacancyId}/slots${qs({ date })}`),
  /** Отклики для «Нового собеседования»: поиск по кандидату, телефону, вакансии. */
  bookable: (search: string) => bff<BookableRow[]>(`${base}/interviews/bookable${qs({ search })}`),
  /** HR назначает время: confirmedByCandidate — кандидат уже подтвердил (по телефону); иначе — ждёт его «да» в Telegram. */
  book: (applicationId: string, startsAt: string, format: string | null, location: string | null, confirmedByCandidate = false, meetingUrl: string | null = null) =>
    bff<InterviewRow[]>(`${base}/interviews/book`, body("POST", { applicationId, startsAt, format, location, confirmedByCandidate, meetingUrl })),
  propose: (applicationId: string) => bff<InterviewRow[]>(`${base}/applications/${applicationId}/propose`, body("POST")),
  interviewStatus: (id: string, status: string) => bff<InterviewRow[]>(`${base}/interviews/${id}/status`, body("POST", { status })),
  exceptions: (status?: string) => bff<ExceptionRow[]>(`${base}/exceptions${qs({ status })}`),
  resolve: (id: string, resolution: string | null, returnToAi: boolean) => bff<ExceptionRow[]>(`${base}/exceptions/${id}/resolve`, body("POST", { resolution, returnToAi })),
  aiSettings: () => bff<AiSettings>(`${base}/ai-settings`),
  saveAiSettings: (input: AiSettings) => bff<AiSettings>(`${base}/ai-settings`, body("PUT", input)),
  vacancyAi: (vacancyId: string, input: { aiRecruiter: boolean; criteria: string | null; agentId: string | null; screeningQuestions: string | null }) =>
    bff<Vacancy>(`${base}/vacancies/${vacancyId}/ai`, body("PUT", input)),
  recruiters: () => bff<RecruiterBrief[]>(`${base}/recruiters`),
  analytics: () => bff<Analytics>(`${base}/analytics`),
  people: (q = "") => bff<Person[]>(`calendar/people${qs({ q })}`),
};
