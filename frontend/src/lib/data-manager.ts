/** AI Data Manager: чат администратора с Claude, отчёты, планы изменений данных OneBase. */

import { makeT, type T } from "@/i18n/core";

/** Тексты по-русски — t по умолчанию; компоненты передают свой t (язык интерфейса). */
const ru = makeT(null);

export type DataStatus = { ready: boolean; message: string | null; model: string; extensions: string[]; maxFileBytes: number };

export type DataStep = { tool: string; title: string; status: "running" | "done" | "failed"; note: string | null };

export type DataMessage = {
  id: string;
  role: "User" | "Assistant" | "Event";
  status: "Completed" | "Failed";
  content: string;
  steps: DataStep[];
  fileIds: string[];
  changeSetIds: string[];
  createdAt: string;
  error: string | null;
};

export type SheetInfo = { name: string; rows: number; columns: number; truncated: boolean };
export type FileDuplicate = { fileId: string; fileName: string; uploadedAt: string; appliedChangeSets: string[] };
export type DataFile = { id: string; fileName: string; sizeBytes: number; sha256: string; createdAt: string; sheets: SheetInfo[]; duplicates: FileDuplicate[] };

export type ChangeAction = "Create" | "Update" | "Delete" | "Unchanged" | "Conflict";
export type ItemStatus = "Pending" | "Applied" | "Skipped" | "RolledBack" | "RollbackConflict";
export type SetStatus = "Draft" | "Applying" | "Applied" | "Stale" | "Cancelled" | "Failed" | "RolledBack" | "PartiallyRolledBack";

export type ChangeItem = {
  id: string;
  position: number;
  entityType: string;
  label: string;
  action: ChangeAction;
  before: string | null;
  after: string | null;
  sourceRef: string | null;
  message: string | null;
  status: ItemStatus;
};

export type Counts = { create: number; update: number; delete: number; unchanged: number; conflict: number };
export type ImpactRow = { name: string; planBefore: number | null; planAfter: number | null; factKg: number | null; executionBefore: number | null; executionAfter: number | null };
export type Impact = { period: string; planSourceBefore: string | null; republic: ImpactRow | null; regions: ImpactRow[]; note: string };

export type PnlMonthTotal = { year: number; month: number; before: Record<string, number | null>; after: Record<string, number | null> };
export type PnlCheck = { dealer: string; year: number; month: number; line: string; label: string; report: number; oneBase: number | null; diff: number | null; sourceRef: string };
export type PnlSummary = { months: PnlMonthTotal[]; checks: PnlCheck[]; checkedValues: number; newDealers: string[] };

export type ChangeSummary = {
  counts: Counts;
  warnings: string[];
  impact: Impact[];
  previous: { changeSetId: string; fileName: string | null; appliedAt: string | null; correctedRows: number } | null;
  source: string;
  fileName: string | null;
  sheet: string | null;
  skippedEmpty: number;
  notes: string[];
  pnl?: PnlSummary | null;
};

export type Verification = {
  checkedAt: string;
  checked: number;
  mismatches: number;
  problems: { label: string; expected: string | null; actual: string | null; ok: boolean }[];
  kpi: Impact[];
  notes: string[];
  pnl?: PnlMonthTotal[] | null;
};

export type ChangeSet = {
  id: string;
  title: string;
  target: string;
  status: SetStatus;
  mode: string;
  period: string | null;
  planKind: string | null;
  createdAt: string;
  userId: string;
  userName: string | null;
  conversationId: string | null;
  fileId: string | null;
  fileName: string | null;
  summary: ChangeSummary;
  items: ChangeItem[];
  itemCount: number;
  appliedAt: string | null;
  appliedBy: string | null;
  verification: Verification | null;
  rolledBackAt: string | null;
  rolledBackBy: string | null;
  error: string | null;
  canApply: boolean;
  canCancel: boolean;
  canRollback: boolean;
};

export type ChangeSetRow = {
  id: string;
  title: string;
  target: string;
  status: SetStatus;
  period: string | null;
  planKind: string | null;
  createdAt: string;
  userName: string | null;
  fileName: string | null;
  counts: Counts | null;
  appliedAt: string | null;
  appliedBy: string | null;
  rolledBackAt: string | null;
};

export type DataConversationSummary = { id: string; title: string; createdAt: string; lastMessageAt: string };
export type DataConversation = {
  id: string;
  title: string;
  createdAt: string;
  messages: DataMessage[];
  files: DataFile[];
  changeSets: ChangeSet[];
  busy: boolean;
};

export type ApplyOutcome = { status: SetStatus; applied: number; skipped: number; message: string; problems: string[] };

export type DataEvent =
  | { type: "start"; data: { conversationId: string; title: string; userMessage: DataMessage } }
  | { type: "step"; data: DataStep }
  | { type: "changeset"; data: ChangeSet }
  | { type: "event"; data: { message: DataMessage } }
  | { type: "outcome"; data: ApplyOutcome }
  | { type: "done"; data: { message: DataMessage; changeSets: ChangeSet[] } }
  | { type: "error"; data: { message?: DataMessage; error: string } };

/** POST с ответом text/event-stream: события отдаются по мере прихода (чат, подтверждение, откат). */
export async function streamDataManager(path: string, body: unknown, onEvent: (e: DataEvent) => void, t: T = ru): Promise<void> {
  const response = await fetch(`/bff/api/data-manager/${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body ?? {}),
  });
  if (!response.ok || !response.body) {
    let text = await response.text().catch(() => "");
    try {
      const json = JSON.parse(text);
      text = json.error ?? json.detail ?? json.title ?? text;
    } catch {
      // не JSON
    }
    // Текст ошибки сервера — на языке интерфейса (по словарю или шаблону).
    text = text && t(text);
    throw new Error(
      response.status === 403
        ? t("Нет права «AI Data Manager».")
        : response.status === 429
          ? t("Слишком много запросов подряд — подождите минуту.")
          : text || t("Ошибка {status}", { status: response.status }),
    );
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  for (;;) {
    const { done, value } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });
    let end: number;
    while ((end = buffer.indexOf("\n\n")) >= 0) {
      const chunk = buffer.slice(0, end);
      buffer = buffer.slice(end + 2);
      let type = "message";
      const data: string[] = [];
      for (const line of chunk.split("\n")) {
        if (line.startsWith("event:")) type = line.slice(6).trim();
        else if (line.startsWith("data:")) data.push(line.slice(5).trimStart());
      }
      if (data.length) onEvent({ type, data: JSON.parse(data.join("\n")) } as DataEvent);
    }
  }
}

/** Загрузка отчёта с прогрессом (XHR — у fetch нет прогресса отправки). */
export function uploadReport(file: File, conversationId: string | null, onProgress: (loaded: number, total: number) => void, t: T = ru): Promise<DataFile> {
  return new Promise((resolve, reject) => {
    const form = new FormData();
    form.append("file", file, file.name);
    if (conversationId) form.append("conversationId", conversationId);
    const xhr = new XMLHttpRequest();
    xhr.open("POST", "/bff/api/data-manager/files");
    xhr.upload.onprogress = (e) => e.lengthComputable && onProgress(e.loaded, e.total);
    xhr.onerror = () => reject(new Error(t("Нет связи с сервером OneBase — файл не загружен.")));
    xhr.onload = () => {
      if (xhr.status >= 200 && xhr.status < 300) return resolve(JSON.parse(xhr.responseText) as DataFile);
      let message = t("Ошибка {status}", { status: xhr.status });
      try {
        const error = JSON.parse(xhr.responseText).error;
        if (error) message = t(error);
      } catch {
        // не JSON
      }
      reject(new Error(xhr.status === 413 ? t("Файл слишком большой для загрузки через сайт.") : message));
    };
    xhr.send(form);
  });
}

export const statusView: Record<SetStatus, { label: string; tone: string }> = {
  Draft: { label: "Ждёт подтверждения", tone: "bg-warn-soft text-warn" },
  Applying: { label: "Применяется", tone: "bg-accent-soft text-accent-strong" },
  Applied: { label: "Применён", tone: "bg-ok-soft text-ok" },
  Stale: { label: "Устарел", tone: "bg-bad-soft text-bad" },
  Cancelled: { label: "Отменён", tone: "bg-muted text-ink-2" },
  Failed: { label: "Ошибка", tone: "bg-bad-soft text-bad" },
  RolledBack: { label: "Откачен", tone: "bg-muted text-ink-2" },
  PartiallyRolledBack: { label: "Откачен частично", tone: "bg-warn-soft text-warn" },
};

export const actionView: Record<ChangeAction, { label: string; tone: string }> = {
  Create: { label: "Создать", tone: "bg-ok-soft text-ok" },
  Update: { label: "Изменить", tone: "bg-accent-soft text-accent-strong" },
  Delete: { label: "Удалить", tone: "bg-bad-soft text-bad" },
  Unchanged: { label: "Без изменений", tone: "bg-muted text-ink-2" },
  Conflict: { label: "Конфликт", tone: "bg-warn-soft text-warn" },
};

export const itemStatusLabel: Record<ItemStatus, string> = {
  Pending: "",
  Applied: "применено",
  Skipped: "",
  RolledBack: "откачено",
  RollbackConflict: "не откачено: изменили позже",
};

/**
 * Пометка шага инструмента на языке интерфейса. План изменений сервер пишет как «Название плана: Draft» —
 * название переводится по словарю, статус — подписью статуса.
 */
export function stepNote(note: string, t: T): string {
  const plan = /^(.*): (Draft|Applying|Applied|Stale|Cancelled|Failed|RolledBack|PartiallyRolledBack)$/s.exec(note);
  return plan ? `${t(plan[1])}: ${t(statusView[plan[2] as SetStatus].label)}` : t(note);
}

/** Служебные вставки сервера в ответ модели (отдельными абзацами): «Готово.», «ответ обрезан…», «лимит шагов…». */
const serverInserts = new Set([
  "Готово.",
  "Ответ не получен.",
  "Модель отказалась выполнять этот запрос.",
  "_(ответ обрезан по длине — попросите продолжить)_",
  "_(достигнут лимит шагов за один ход — напишите «продолжи»)_",
]);

/** Ответ AI Data Manager: текст модели как есть, служебные вставки сервера — на языке интерфейса. */
export const answerText = (content: string, t: T) =>
  content
    .split("\n\n")
    .map((part) => (serverInserts.has(part) ? t(part) : part))
    .join("\n\n");

export const exampleTasks = [
  "Обнови план РОП на октябрь по этому отчёту",
  "Сверь факт продаж из отчёта с OneBase за сентябрь",
  "Что изменилось в новой версии отчёта по сравнению с прошлой?",
  "Какие данные можно обновить через AI Data Manager?",
];

const number = new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 2 });
const percent = new Intl.NumberFormat("ru-RU", { style: "percent", maximumFractionDigits: 1 });
const dateTime = new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });

/** Значение из плана изменений: число («1234.5») — с разрядами, текст — как есть, пусто — «—». */
export function valueText(value: string | null): string {
  if (value === null || value === "") return "—";
  const n = Number(value);
  return Number.isFinite(n) && /^-?\d+(\.\d+)?$/.test(value) ? number.format(n) : value;
}

export const kg = (v: number | null | undefined, t: T = ru) => (v === null || v === undefined ? "—" : t("{n} кг", { n: number.format(v) }));
export const pct = (v: number | null | undefined) => (v === null || v === undefined ? "—" : percent.format(v));
export const when = (iso: string | null | undefined) => (iso ? dateTime.format(new Date(iso)) : "—");
