import { bff } from "./bff";
import type { ScreeningCategory, ScreeningStatus } from "./screening-view";

/** HR → «Подбор» → «AI-скрининг»: типы ответов API и вызовы (/api/hr/recruitment/screening, Quota Guard — /api/hr/recruitment/hh/guard). */

export type EvidenceQuote = { text: string; verified: boolean };

export type ScreeningCriterion = {
  code: string;
  name: string;
  max: number;
  points: number;
  score: number;
  status: "met" | "partial" | "not_met" | "not_stated";
  explanation: string;
  evidence: string | null;
  verified: boolean;
  quotes: EvidenceQuote[];
  capped: boolean;
};

export type InterviewQuestion = { question: string; type: string; reason: string | null };

export type ScreeningAnalysis = {
  id: string;
  applicationId: string;
  candidateId: string;
  version: number;
  overall: number;
  category: ScreeningCategory;
  categoryName: string;
  recommendation: string;
  summary: string;
  whyFits: string | null;
  criteria: ScreeningCriterion[];
  strengths: string[];
  weaknesses: string[];
  missing: string[];
  recommendationText: string | null;
  questions: InterviewQuestion[];
  confidence: number | null;
  coverage: number | null;
  lowData: boolean;
  risks: string | null;
  model: string | null;
  createdAt: string;
  delta: number | null;
};

export type ScreeningKpis = { candidates: number; analyzed: number; suitable: number; recommended: number; pending: number };

export type ScreeningVacancy = {
  id: string;
  code: string;
  title: string;
  status: "Draft" | "Open" | "Paused" | "Closed";
  requirements: string | null;
  criteria: string | null;
  found: number;
  analyzed: number;
  suitable: number;
  averageScore: number | null;
  lastSyncAt: string | null;
  lastAnalysisAt: string | null;
  onHh: boolean;
};

export type ScreeningRow = {
  applicationId: string;
  candidateId: string;
  fullName: string;
  desiredPosition: string | null;
  city: string | null;
  experienceMonths: number | null;
  lastJob: string | null;
  skills: string[];
  salaryExpectation: string | null;
  education: string | null;
  vacancyId: string;
  vacancyCode: string;
  vacancyTitle: string;
  status: ScreeningStatus;
  stage: string;
  source: string;
  overall: number | null;
  category: ScreeningCategory | null;
  version: number | null;
  confidence: number | null;
  analyzedAt: string | null;
  analyzing: boolean;
  pending: boolean;
  summary: string | null;
  hhUrl: string | null;
  appliedAt: string;
  hasResume: boolean;
  analysisError: string | null;
};

export type HhGuardBrief = {
  connected: boolean;
  enabled: boolean;
  lastSyncAt: string | null;
  lastSyncOk: boolean | null;
  freeCalls: number;
  blockedCalls: number;
  guardReason: string | null;
  guardTrippedAt: string | null;
  lastBlockReason: string | null;
};

export type ScreeningPage = {
  kpis: ScreeningKpis;
  vacancies: ScreeningVacancy[];
  items: ScreeningRow[];
  total: number;
  cities: string[];
  hh: HhGuardBrief;
};

export type CandidateScreening = {
  applicationId: string;
  vacancyId: string;
  vacancyCode: string;
  vacancyTitle: string;
  status: ScreeningStatus;
  analyzing: boolean;
  pending: boolean;
  hhUrl: string | null;
  history: ScreeningAnalysis[];
};

export type ImportResult = { candidateId: string; applicationId: string | null; created: boolean; matchedBy: string | null; message: string };

export type ComparisonCandidate = {
  applicationId: string;
  candidateId: string;
  fullName: string;
  overall: number;
  category: ScreeningCategory;
  experience: string;
  skills: string[];
  achievements: string;
  education: string;
  salary: string;
  strengths: string[];
  weaknesses: string[];
  fit: string;
  points: Record<string, number>;
};

export type Comparison = {
  id: string;
  vacancyId: string;
  vacancyCode: string;
  vacancyTitle: string;
  candidates: ComparisonCandidate[];
  bestApplicationId: string | null;
  ranking: string[];
  recommendation: string;
  verdicts: { applicationId: string; verdict: string }[];
  model: string | null;
  createdAt: string;
};

export type ComparisonBrief = { id: string; createdAt: string; names: string[]; best: string | null };

export type HhOperation = { code: string; method: string; path: string; cost: "free" | "limit" | "paid" | "unknown"; auto: boolean; manual: boolean; why: string; source: string };

export type HhCall = {
  at: string;
  operation: string;
  method: string;
  path: string;
  cost: string;
  trigger: "auto" | "manual";
  allowed: boolean;
  reason: string | null;
  status: number | null;
  errorType: string | null;
  durationMs: number;
};

export type HhGuard = {
  freeCalls30: number;
  blockedCalls30: number;
  failedCalls30: number;
  callsToday: number;
  dailyCap: number;
  guardTrippedAt: string | null;
  guardReason: string | null;
  lastSyncAt: string | null;
  catalog: HhOperation[];
  journal: HhCall[];
};

const base = "hr/recruitment/screening";
const post = (data?: unknown): RequestInit => ({ method: "POST", body: JSON.stringify(data ?? {}) });

async function upload<T>(path: string, form: FormData): Promise<T> {
  // multipart: Content-Type с границей ставит браузер (bff ставит JSON).
  const response = await fetch(`/bff/api/${path}`, { method: "POST", body: form });
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
  return (await response.json()) as T;
}

export const screeningApi = {
  page: (q: Record<string, string>) => bff<ScreeningPage>(`${base}${Object.keys(q).length ? `?${new URLSearchParams(q)}` : ""}`),
  candidate: (candidateId: string) => bff<CandidateScreening[]>(`${base}/candidates/${candidateId}`),
  analyze: (applicationId: string) => bff<ScreeningAnalysis>(`${base}/applications/${applicationId}/analyze`, post()),
  queue: (input: { vacancyId: string | null; applicationIds: string[] | null }) =>
    bff<{ queued: number; skipped: number; message: string }>(`${base}/queue`, post(input)),
  status: (applicationId: string, status: ScreeningStatus, comment: string | null, rejectReason: string | null) =>
    bff<{ status: ScreeningStatus }>(`${base}/applications/${applicationId}/status`, post({ status, comment, rejectReason })),
  importFile: (vacancyId: string, file: File, consent: string | null) => {
    const form = new FormData();
    form.append("vacancyId", vacancyId);
    if (consent) form.append("consent", consent);
    form.append("file", file);
    return upload<ImportResult>(`${base}/import/file`, form);
  },
  importText: (input: { vacancyId: string; text: string; fullName: string | null; consentChannel: string | null }) =>
    bff<ImportResult>(`${base}/import/text`, post(input)),
  importLink: (input: { vacancyId: string; url: string; fullName: string | null; consentChannel: string | null }) =>
    bff<ImportResult>(`${base}/import/link`, post(input)),
  compare: (vacancyId: string, applicationIds: string[]) => bff<Comparison>(`${base}/compare`, post({ vacancyId, applicationIds })),
  comparisons: (vacancyId: string) => bff<ComparisonBrief[]>(`${base}/vacancies/${vacancyId}/comparisons`),
  comparison: (id: string) => bff<Comparison>(`${base}/comparisons/${id}`),
  guard: () => bff<HhGuard>("hr/recruitment/hh/guard"),
  resetGuard: () => bff<HhGuard>("hr/recruitment/hh/guard/reset", post()),
};
