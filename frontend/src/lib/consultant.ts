import { formatDate } from "@/i18n/core";
import { makeT, type T } from "@/i18n/core";
import type { ChatContext, ChatSource, StructuredAnswer } from "./consultant-view";
import type { VoiceStatus } from "./voice";

/** Тексты по-русски — t по умолчанию; компоненты передают свой t (язык интерфейса). */
const ru = makeT(null);

export type DataSource = { title: string; period: string | null; href: string | null };
export type Metric = { name: string; value: string; unit: string | null };

export type AgentResult = {
  agent: string;
  agentName: string;
  status: string;
  summary: string;
  findings: string[];
  metrics: Metric[];
  problems: string[];
  recommendations: string[];
  dataSources: DataSource[];
  toolsUsed: string[];
  error: string | null;
  /** Числа итога, которых нет в результатах инструментов (после попытки исправить). */
  unverified?: string[] | null;
  /** Почему данных нет или они неполные: no_access, source_not_connected, tool_error, not_found, out_of_range, not_in_onebase, interrupted… */
  reason?: string | null;
  /** Источники, которые не дали данных: инструмент, вид сбоя, безопасный текст. */
  toolErrors?: { tool: string; kind: string; message: string }[] | null;
};

/** Проверка ответа по данным: сколько чисел сверено, какие не подтверждены, исправлялся ли ответ и помечен ли текст. */
export type GroundingSummary = { checked: number; supported: number; unsupported: string[]; quoted: string[]; revised: boolean; warned: boolean };

/** Перепроверка после возражения пользователя: итог считает код по свежим данным. */
export type CorrectionReview = {
  verdict: "confirmed" | "corrected" | "insufficient";
  previousConfirmed: string[];
  previousNotConfirmed: string[];
  userConfirmed: string[];
  userNotConfirmed: string[];
  attempt: number;
  previousMessageId: string | null;
};

export type ConsultantDetails = {
  agents: AgentResult[];
  sources: DataSource[];
  model: string | null;
  usedFallback: boolean;
  routingReason: string | null;
  memoryUsed?: number;
  /** Отчёты Excel, сформированные по просьбе пользователя. */
  reports?: ReportRef[] | null;
  grounding?: GroundingSummary | null;
  correction?: CorrectionReview | null;
};

/** Период источника на языке интерфейса: по словарю или шаблону («октябрь 2026, данные по 19.10.2026»), «октябрь 2026» — месяц словом. */
export function periodText(period: string, t: T): string {
  const translated = t(period);
  if (translated !== period) return translated;
  const month = /^([а-яё]+) (\d{4})$/.exec(period);
  return month ? `${t(month[1])} ${month[2]}` : period;
}

/** Источник отчёта Excel — «Название (период)» одной строкой: название и период переводятся по отдельности. */
export function reportSourceText(source: string, t: T): string {
  const translated = t(source);
  if (translated !== source) return translated;
  const parts = /^(.*) \(([^()]*)\)$/s.exec(source);
  return parts ? `${t(parts[1])} (${periodText(parts[2], t)})` : source;
}

/** Пометка шага консультанта: текст сервера или список AI-сотрудников через запятую («Sales AI, Финансовый директор»). */
export function progressNote(note: string, t: T): string {
  const translated = t(note);
  return translated !== note || !note.includes(", ") ? translated : note.split(", ").map((part) => t(part)).join(", ");
}

/** Отчёт консультанта в ответе: ссылка без данных — листы загружаются для предпросмотра отдельно. */
export type ReportRef = { id: string; title: string; sheetCount: number; rowCount: number };

/** Формат колонки: как показывать число (как в Excel). percent — значение в процентах (27.8 = 27,8 %). */
export type ReportFormat = "text" | "integer" | "number" | "kg" | "money" | "percent" | "date";
export type ReportCell = number | string | boolean | null;
export type ReportSheet = { name: string; columns: { header: string; format: ReportFormat }[]; rows: ReportCell[][]; total: boolean; note: string | null };
export type ReportView = { id: string; title: string; createdAt: string; sheets: ReportSheet[]; sources: string[] };

/** Ссылка на файл Excel: имя файла — в пути, чтобы его взяли iOS и браузеры. */
export const reportDownloadUrl = (report: { id: string; title: string }) =>
  `/bff/api/ai/reports/${report.id}/xlsx/${encodeURIComponent(report.title.replace(/[\\/:*?"<>|«»]/g, " ").replace(/\s+/g, " ").trim() || "Отчёт")}.xlsx`;

export type ChatMessage = {
  id: string;
  role: "user" | "assistant";
  status: "completed" | "failed";
  content: string;
  details: ConsultantDetails | null;
  error: string | null;
  createdAt: string;
  durationMs: number;
  /** Структура готового ответа для экрана (блоки отчёта); нет — ответ показывается Markdown-ом (content). */
  answer?: StructuredAnswer | null;
};

/** source — web или telegram (вопросы из Telegram-бота идут в чат консультанта пользователя). */
export type ConversationSummary = { id: string; title: string; createdAt: string; lastMessageAt: string; source?: ChatSource };
export type ConversationView = { id: string; title: string; createdAt: string; messages: ChatMessage[] };
/** voice — голос консультанта на сервере (OpenAI); без него — голос браузера, если он есть. */
export type AiStatus = { ready: boolean; message: string | null; voice?: VoiceStatus };

export type ProgressStatus = "Pending" | "Running" | "Done" | "Failed" | "Skipped";
export type ConsultantProgress = { stage: string; agent: string | null; agentName: string | null; status: ProgressStatus; note: string | null };

/**
 * Черновик итогового ответа, пока он пишется: text — следующий фрагмент, reset — начать текст заново (повтор запроса к модели),
 * checking — черновик дописан, числа сверяются с данными, revising — числа не подтвердились, ответ переписывается.
 * Черновик — не ответ: ответ приходит событием done и заменяет черновик.
 */
export type DraftEvent = { kind: "text" | "reset" | "checking" | "revising"; text?: string };

export type ChatEvent =
  | { type: "start"; data: { conversationId: string; title: string; userMessage: ChatMessage } }
  | { type: "progress"; data: ConsultantProgress }
  | { type: "draft"; data: DraftEvent }
  | { type: "done"; data: { message: ChatMessage } }
  | { type: "error"; data: { message?: ChatMessage; error: string } };

/**
 * Вопрос консультанту: читает поток text/event-stream и отдаёт события по мере прихода. context — период и отдел с экрана (уходят
 * консультанту вместе с вопросом); signal — «Стоп»: поток закрывается, а сервер дорабатывает ответ и сохраняет его в чат.
 */
export async function streamChat(
  conversationId: string | null,
  message: string,
  onEvent: (e: ChatEvent) => void,
  t: T = ru,
  options: { context?: ChatContext | null; signal?: AbortSignal } = {},
): Promise<void> {
  const response = await fetch("/bff/api/ai/chat", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(options.context ? { conversationId, message, context: options.context } : { conversationId, message }),
    signal: options.signal,
  });
  if (!response.ok || !response.body) {
    let text = await response.text().catch(() => "");
    try {
      const json = JSON.parse(text);
      text = json.error ?? json.detail ?? text;
    } catch {
      // не JSON
    }
    // Текст ошибки сервера — на языке интерфейса (по словарю или шаблону).
    text = text && t(text);
    throw new Error(response.status === 403 ? t("Нет права пользоваться консультантом.") : text || t("Ошибка {status}", { status: response.status }));
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
      if (data.length) onEvent({ type, data: JSON.parse(data.join("\n")) } as ChatEvent);
    }
  }
}

/** Примеры вопросов для пустого чата. */
export const exampleQuestions = [
  "Как прошли продажи за этот месяц?",
  "Какие регионы не выполняют план?",
  "Какие товары продаются хуже всего?",
  "Сравни этот месяц с предыдущим",
  "Где заканчиваются остатки?",
  "Что нужно сделать, чтобы увеличить продажи?",
];

/** Раздел истории консультанта: Сегодня, Вчера, Ранее. */
export function historySection(iso: string, now = new Date()): "Сегодня" | "Вчера" | "Ранее" {
  const group = historyGroup(iso, now);
  return group === "Сегодня" || group === "Вчера" ? group : "Ранее";
}

/** Время чата в истории: сегодня и вчера — «14:05», раньше — «12 окт», в другом году — «12.10.2025». */
export function historyTime(iso: string, now = new Date(), locale = "ru-RU"): string {
  const d = new Date(iso);
  if (historySection(iso, now) !== "Ранее") return formatDate(d, locale, { hour: "2-digit", minute: "2-digit" });
  return d.getFullYear() === now.getFullYear()
    ? formatDate(d, locale, { day: "numeric", month: "short" }).replace(".", "")
    : formatDate(d, locale);
}

/** Группа истории: Сегодня, Вчера, дата. */
export function historyGroup(iso: string, now = new Date(), locale = "ru-RU"): string {
  const d = new Date(iso);
  const day = (x: Date) => new Date(x.getFullYear(), x.getMonth(), x.getDate()).getTime();
  const diff = Math.round((day(now) - day(d)) / 86_400_000);
  if (diff === 0) return "Сегодня";
  if (diff === 1) return "Вчера";
  return formatDate(d, locale, { day: "numeric", month: "long", year: d.getFullYear() === now.getFullYear() ? undefined : "numeric" });
}
