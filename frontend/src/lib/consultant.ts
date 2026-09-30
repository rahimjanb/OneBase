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
};

export type ConsultantDetails = {
  agents: AgentResult[];
  sources: DataSource[];
  model: string | null;
  usedFallback: boolean;
  routingReason: string | null;
  memoryUsed?: number;
};

export type ChatMessage = {
  id: string;
  role: "user" | "assistant";
  status: "completed" | "failed";
  content: string;
  details: ConsultantDetails | null;
  error: string | null;
  createdAt: string;
  durationMs: number;
};

export type ConversationSummary = { id: string; title: string; createdAt: string; lastMessageAt: string };
export type ConversationView = { id: string; title: string; createdAt: string; messages: ChatMessage[] };
export type AiStatus = { ready: boolean; message: string | null };

export type ProgressStatus = "Pending" | "Running" | "Done" | "Failed" | "Skipped";
export type ConsultantProgress = { stage: string; agent: string | null; agentName: string | null; status: ProgressStatus; note: string | null };

export type ChatEvent =
  | { type: "start"; data: { conversationId: string; title: string; userMessage: ChatMessage } }
  | { type: "progress"; data: ConsultantProgress }
  | { type: "done"; data: { message: ChatMessage } }
  | { type: "error"; data: { message?: ChatMessage; error: string } };

/** Вопрос консультанту: читает поток text/event-stream и отдаёт события по мере прихода. */
export async function streamChat(conversationId: string | null, message: string, onEvent: (e: ChatEvent) => void): Promise<void> {
  const response = await fetch("/bff/api/ai/chat", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ conversationId, message }),
  });
  if (!response.ok || !response.body) {
    let text = await response.text().catch(() => "");
    try {
      const json = JSON.parse(text);
      text = json.error ?? json.detail ?? text;
    } catch {
      // не JSON
    }
    throw new Error(response.status === 403 ? "Нет права пользоваться консультантом." : text || `Ошибка ${response.status}`);
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

/** Группа истории: Сегодня, Вчера, дата. */
export function historyGroup(iso: string, now = new Date()): string {
  const d = new Date(iso);
  const day = (x: Date) => new Date(x.getFullYear(), x.getMonth(), x.getDate()).getTime();
  const diff = Math.round((day(now) - day(d)) / 86_400_000);
  if (diff === 0) return "Сегодня";
  if (diff === 1) return "Вчера";
  return d.toLocaleDateString("ru-RU", { day: "numeric", month: "long", year: d.getFullYear() === now.getFullYear() ? undefined : "numeric" });
}
