import { bff } from "./bff";
import type { NavCounters } from "./recruitment-view";

/** HR → «Подбор»: типы ответов API и вызовы (/api/hr/recruitment). Права проверяет backend; интерфейс только прячет недоступное. */

export type Option = { id: string; name: string; code: string | null };

export type RecruitmentContext = {
  canRead: boolean;
  canManage: boolean;
  canRequest: boolean;
  requestDepartments: Option[];
  departments: Option[];
  recruiters: Option[];
  retentionDays: number;
};

export type RequestStatus = "Submitted" | "Approved" | "Rejected" | "Cancelled";

export type HiringRequest = {
  id: string;
  number: number;
  departmentId: string;
  department: string;
  position: string;
  headcount: number;
  reason: string | null;
  requirements: string | null;
  salaryFrom: number | null;
  salaryTo: number | null;
  desiredStart: string | null;
  status: RequestStatus;
  createdBy: string | null;
  createdAt: string;
  decidedBy: string | null;
  decidedAt: string | null;
  decisionComment: string | null;
  vacancyId: string | null;
  vacancyCode: string | null;
  canCancel: boolean;
  canDecide: boolean;
};

export type HiringRequestInput = {
  departmentId: string;
  position: string;
  headcount: number;
  reason: string | null;
  requirements: string | null;
  salaryFrom: number | null;
  salaryTo: number | null;
  desiredStart: string | null;
};

export type VacancyStatus = "Draft" | "Open" | "Paused" | "Closed";
export type Stage = "New" | "Screening" | "Interview" | "Offer" | "Hired" | "Rejected" | "Withdrawn";

export type VacancyRow = {
  id: string;
  code: string;
  title: string;
  department: string;
  status: VacancyStatus;
  headcount: number;
  salaryFrom: number | null;
  salaryTo: number | null;
  city: string | null;
  recruiter: string | null;
  active: number;
  hired: number;
  createdAt: string;
  openedAt: string | null;
  departmentId: string;
  /** Последнее изменение (нет — создание): порядок внутри статуса. */
  updatedAt: string;
  /** Источники: связана с вакансией на hh.uz, опубликована в Telegram. */
  onHh: boolean;
  inTelegram: boolean;
};

export type VacancyInput = {
  title: string;
  departmentId: string;
  description: string | null;
  requirements: string | null;
  conditions: string | null;
  salaryFrom: number | null;
  salaryTo: number | null;
  city: string | null;
  employmentType: string | null;
  headcount: number;
  recruiterId: string | null;
};

export type VacancyApplicationRow = {
  applicationId: string;
  candidateId: string;
  candidateName: string;
  stage: Stage;
  source: string;
  stageChangedAt: string;
  phone: string | null;
};

export type Vacancy = VacancyInput & {
  aiRecruiter: boolean;
  criteria: string | null;
  hhVacancyId: string | null;
  agentId: string | null;
  screeningQuestions: string | null;
  id: string;
  code: string;
  department: string;
  status: VacancyStatus;
  recruiter: string | null;
  hiringRequestId: string | null;
  hiringRequestNumber: number | null;
  createdAt: string;
  openedAt: string | null;
  closedAt: string | null;
  stages: Record<Stage, number>;
  applications: VacancyApplicationRow[];
};

export type ContactKind = "Phone" | "Email" | "Telegram" | "TelegramUsername";

export type CandidateRow = {
  id: string;
  fullName: string;
  city: string | null;
  phone: string | null;
  applications: { vacancyId: string; vacancyCode: string; vacancyTitle: string; stage: Stage }[];
  lastActivityAt: string;
  deleteAt: string | null;
  hasConsent: boolean;
  openDuplicates: number;
  resumes: number;
};

export type CandidatePage = { items: CandidateRow[]; total: number; page: number; pageSize: number };

export type CandidateCard = {
  id: string;
  fullName: string;
  city: string | null;
  language: string | null;
  notes: string | null;
  createdAt: string;
  lastActivityAt: string;
  deleteAt: string | null;
  retentionDays: number;
  hasConsent: boolean;
  contacts: { id: string; kind: ContactKind; value: string; verified: boolean; source: string }[];
  sources: { source: string; externalId: string | null; note: string | null; createdAt: string }[];
  resumes: CandidateDocument[];
  applications: {
    id: string;
    vacancyId: string;
    vacancyCode: string;
    vacancyTitle: string;
    vacancyStatus: VacancyStatus;
    stage: Stage;
    source: string;
    rejectReason: string | null;
    stageChangedAt: string;
    history: { from: Stage | null; to: Stage; comment: string | null; actor: string | null; at: string }[];
    screening: Record<string, string> | null;
  }[];
  consents: { id: string; channel: string; textVersion: number; givenAt: string; withdrawnAt: string | null; recordedBy: string | null }[];
  duplicates: { id: string; otherCandidateId: string; otherName: string; kind: ContactKind; value: string }[];
  /** Язык переписки: код (ru-Cyrl, uz-Latn, uz-Cyrl), кто выбрал (auto, candidate, hr), последнее определение и уверенность. */
  languageInfo: { code: string | null; source: string | null; lastDetected: string | null; confidence: number | null } | null;
};

/** Поле разбора резюме: значение, цитата-доказательство, сказано прямо или выведено, проверено ли (цитата найдена в тексте). */
export type ProfileField = { value: string | null; evidence?: string | null; basis: "stated" | "inferred"; verified: boolean };

export type ResumeProfile = {
  fullName?: ProfileField | null;
  phone?: ProfileField | null;
  email?: ProfileField | null;
  location?: ProfileField | null;
  professionalTitle?: ProfileField | null;
  experienceYears?: ProfileField | null;
  workExperience: { company?: string | null; position?: string | null; from?: string | null; to?: string | null; description?: string | null; verified: boolean }[];
  education: { institution?: string | null; degree?: string | null; year?: string | null; verified: boolean }[];
  skills: ProfileField[];
  languages: ProfileField[];
  certificates: ProfileField[];
  salaryExpectation?: ProfileField | null;
  additionalInformation?: ProfileField | null;
  summary?: string | null;
  quality: "good" | "partial" | "poor";
};

export type DocumentStatus = "Received" | "Stored" | "Processing" | "Done" | "Failed" | "Rejected";
export type DocumentStep = "NotNeeded" | "Pending" | "Done" | "Failed" | "Skipped";

/** Документ кандидата: оригинал в хранилище OneBase, статусы обработки, текст и разбор. */
export type CandidateDocument = {
  id: string;
  fileName: string;
  sizeBytes: number;
  source: string;
  createdAt: string;
  hasText: boolean;
  textError: string | null;
  textPreview: string | null;
  textByAi: boolean;
  documentType: string | null;
  canReadAi: boolean;
  status: DocumentStatus;
  ocrStatus: DocumentStep;
  analysisStatus: DocumentStep;
  detectedType: string | null;
  contentType: string | null;
  pageCount: number | null;
  processingError: string | null;
  summary: string | null;
  profile: ResumeProfile | null;
  vacancyCode: string | null;
  canPreview: boolean;
  processedAt: string | null;
};

export const documentStatusLabel: Record<DocumentStatus, string> = {
  Received: "Получен, скачивается",
  Stored: "Сохранён, ждёт обработки",
  Processing: "Обрабатывается",
  Done: "Обработан",
  Failed: "Ошибка обработки",
  Rejected: "Отклонён",
};

export const ocrStatusLabel: Record<DocumentStep, string> = {
  NotNeeded: "из текстового слоя",
  Pending: "ждёт",
  Done: "распознан AI",
  Failed: "не прочитан",
  Skipped: "не распознавался",
};

export const analysisStatusLabel: Record<DocumentStep, string> = {
  NotNeeded: "не нужен",
  Pending: "ждёт",
  Done: "разобран AI",
  Failed: "не удался",
  Skipped: "без AI — только контакты",
};

/** Документ ещё в работе: карточку стоит обновлять, пока он не готов. */
export function documentBusy(d: CandidateDocument): boolean {
  return d.status === "Received" || d.status === "Stored" || d.status === "Processing";
}

/** Язык и графика переписки с кандидатом. */
export const LANGUAGE_CODES = ["ru-Cyrl", "uz-Latn", "uz-Cyrl"] as const;

export const languageCodeLabel: Record<string, string> = {
  "ru-Cyrl": "Русский",
  "uz-Latn": "Узбекский (латиница)",
  "uz-Cyrl": "Узбекский (кириллица)",
};

export const languageSourceLabel: Record<string, string> = {
  auto: "по переписке",
  candidate: "попросил кандидат",
  hr: "выбрал сотрудник HR",
};

export type CandidateInput = {
  fullName: string;
  city: string | null;
  language: string | null;
  notes: string | null;
  contacts: { kind: ContactKind; value: string }[];
  source: string;
  sourceNote: string | null;
  vacancyId: string | null;
  consentChannel: string | null;
};

export type IntakeResult = { candidateId: string; created: boolean; matchedBy: string | null; duplicates: number };

export type RecruitmentSettings = { retentionDays: number; consentVersion: number; consentText: string; updatedAt: string | null };

export const STAGES: Stage[] = ["New", "Screening", "Interview", "Offer", "Hired", "Rejected", "Withdrawn"];

export const stageLabel: Record<Stage, string> = {
  New: "Новый",
  Screening: "Отбор",
  Interview: "Собеседование",
  Offer: "Оффер",
  Hired: "Принят",
  Rejected: "Отказ",
  Withdrawn: "Отказался сам",
};

export const stageTone: Record<Stage, string> = {
  New: "bg-accent-soft text-accent-strong",
  Screening: "bg-muted text-ink",
  Interview: "bg-warn-soft text-warn",
  Offer: "bg-warn-soft text-warn",
  Hired: "bg-ok-soft text-ok",
  Rejected: "bg-bad-soft text-bad",
  Withdrawn: "bg-muted text-ink-3",
};

export const vacancyStatusLabel: Record<VacancyStatus, string> = { Draft: "Черновик", Open: "Активна", Paused: "Пауза", Closed: "Закрыта" };

export const requestStatusLabel: Record<RequestStatus, string> = {
  Submitted: "Ждёт решения",
  Approved: "Одобрена",
  Rejected: "Отклонена",
  Cancelled: "Отозвана",
};

export const contactLabel: Record<ContactKind, string> = { Phone: "Телефон", Email: "E-mail", Telegram: "Telegram ID", TelegramUsername: "Telegram" };

export const sourceLabel: Record<string, string> = {
  manual: "Вручную",
  hh: "HH.uz",
  telegram: "Telegram",
  referral: "Рекомендация",
  other: "Другое",
};

export const consentLabel: Record<string, string> = { written: "Письменно", telegram: "В Telegram", hh: "На HH", email: "По e-mail", verbal: "Устно" };

/** Что за документ, по мнению AI (фото и сканы). */
export const documentLabel: Record<string, string> = { resume: "Резюме", certificate: "Диплом или сертификат", id_document: "Документ личности", other: "Другой документ" };

export const employmentLabel: Record<string, string> = {
  full: "Полная занятость",
  part: "Частичная",
  shift: "Сменная",
  remote: "Удалённая",
  temporary: "Временная",
};

export const money = (v: number | null) => (v == null ? null : new Intl.NumberFormat("ru-RU").format(v));

export const salaryText = (from: number | null, to: number | null) =>
  from == null && to == null ? "—" : from != null && to != null ? `${money(from)} – ${money(to)}` : from != null ? `от ${money(from)}` : `до ${money(to)}`;

export const when = (iso: string | null) => (iso ? new Date(iso).toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" }) : "—");
export const day = (iso: string | null) => (iso ? new Date(iso).toLocaleDateString("ru-RU") : "—");

const base = "hr/recruitment";
const json = (body: unknown) => JSON.stringify(body);
const post = (body?: unknown): RequestInit => ({ method: "POST", body: json(body ?? {}) });
const put = (body: unknown): RequestInit => ({ method: "PUT", body: json(body) });
const qs = (q: Record<string, string | number | boolean | null | undefined>) => {
  const s = Object.entries(q)
    .filter(([, v]) => v !== null && v !== undefined && v !== "")
    .map(([k, v]) => `${k}=${encodeURIComponent(String(v))}`)
    .join("&");
  return s ? `?${s}` : "";
};

export const recruitmentApi = {
  context: () => bff<RecruitmentContext>(`${base}/context`),
  /** Подписи кнопок разделов подбора: один лёгкий запрос на все кнопки. */
  nav: () => bff<NavCounters>(`${base}/nav`),
  requests: (status?: string) => bff<HiringRequest[]>(`${base}/requests${qs({ status })}`),
  createRequest: (input: HiringRequestInput) => bff<HiringRequest>(`${base}/requests`, post(input)),
  cancelRequest: (id: string) => bff<HiringRequest>(`${base}/requests/${id}/cancel`, post()),
  approveRequest: (id: string, comment: string | null) => bff<HiringRequest>(`${base}/requests/${id}/approve`, post({ comment })),
  rejectRequest: (id: string, comment: string) => bff<HiringRequest>(`${base}/requests/${id}/reject`, post({ comment })),
  vacancies: (status?: string, search?: string) => bff<VacancyRow[]>(`${base}/vacancies${qs({ status, search })}`),
  vacancy: (id: string) => bff<Vacancy>(`${base}/vacancies/${id}`),
  createVacancy: (input: VacancyInput) => bff<Vacancy>(`${base}/vacancies`, post(input)),
  updateVacancy: (id: string, input: VacancyInput) => bff<Vacancy>(`${base}/vacancies/${id}`, put(input)),
  vacancyStatus: (id: string, status: VacancyStatus) => bff<Vacancy>(`${base}/vacancies/${id}/status`, post({ status })),
  candidates: (q: { search?: string; vacancyId?: string; stage?: string; duplicates?: boolean; page?: number }) =>
    bff<CandidatePage>(`${base}/candidates${qs(q)}`),
  candidate: (id: string) => bff<CandidateCard>(`${base}/candidates/${id}`),
  createCandidate: (input: CandidateInput) => bff<IntakeResult>(`${base}/candidates`, post(input)),
  updateCandidate: (id: string, input: { fullName: string; city: string | null; language: string | null; notes: string | null; languageCode?: string | null }) =>
    bff<CandidateCard>(`${base}/candidates/${id}`, put(input)),
  deleteCandidate: (id: string) => bff<void>(`${base}/candidates/${id}`, { method: "DELETE" }),
  addContact: (id: string, kind: ContactKind, value: string) => bff<CandidateCard>(`${base}/candidates/${id}/contacts`, post({ kind, value })),
  removeContact: (id: string, contactId: string) => bff<CandidateCard>(`${base}/candidates/${id}/contacts/${contactId}`, { method: "DELETE" }),
  uploadResume: async (id: string, file: File) => {
    // multipart: заголовок Content-Type с границей ставит сам браузер (bff ставит JSON — для файла не подходит).
    const form = new FormData();
    form.append("file", file);
    const response = await fetch(`/bff/api/${base}/candidates/${id}/resumes`, { method: "POST", body: form });
    if (!response.ok) {
      const text = await response.text();
      let message = text;
      try {
        message = JSON.parse(text).error ?? text;
      } catch {
        // не JSON
      }
      throw new Error(response.status === 403 ? "Недостаточно прав." : message || `Ошибка ${response.status}`);
    }
    return (await response.json()) as CandidateCard;
  },
  resumeUrl: (resumeId: string) => `/bff/api/${base}/resumes/${resumeId}`,
  deleteResume: (resumeId: string) => bff<CandidateCard>(`${base}/resumes/${resumeId}`, { method: "DELETE" }),
  /** Просмотр в браузере (PDF, фото, текст) — тем же правом, что и скачивание. */
  previewUrl: (resumeId: string) => `/bff/api/${base}/resumes/${resumeId}/preview`,
  /** «Распознать заново (AI)»: фото или скан читает AI (платный вызов), затем разбор. */
  readResume: (resumeId: string) => bff<CandidateCard>(`${base}/resumes/${resumeId}/read`, { method: "POST" }),
  /** «Обработать заново»: скачать (если не скачан), прочитать и разобрать — в фоне. */
  reprocessResume: (resumeId: string) => bff<CandidateCard>(`${base}/resumes/${resumeId}/reprocess`, { method: "POST" }),
  addApplication: (id: string, vacancyId: string) => bff<CandidateCard>(`${base}/candidates/${id}/applications`, post({ vacancyId })),
  stage: (applicationId: string, stage: Stage, comment: string | null, rejectReason: string | null) =>
    bff<CandidateCard>(`${base}/applications/${applicationId}/stage`, post({ stage, comment, rejectReason })),
  recordConsent: (id: string, channel: string) => bff<CandidateCard>(`${base}/candidates/${id}/consents`, post({ channel })),
  withdrawConsent: (consentId: string) => bff<void>(`${base}/consents/${consentId}/withdraw`, post()),
  merge: (duplicateId: string, keepId: string, viewFor: string) => bff<CandidateCard>(`${base}/duplicates/${duplicateId}/merge`, post({ keepId, viewFor })),
  dismiss: (duplicateId: string, viewFor: string) => bff<CandidateCard>(`${base}/duplicates/${duplicateId}/dismiss`, post({ keepId: viewFor, viewFor })),
  settings: () => bff<RecruitmentSettings>(`${base}/settings`),
  saveSettings: (retentionDays: number, consentText: string) => bff<RecruitmentSettings>(`${base}/settings`, put({ retentionDays, consentText })),
};
