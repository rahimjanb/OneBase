/**
 * Экран AI-консультанта (ТЗ ai-consultant): структурированный ответ с сервера (блоки отчёта), история чатов (источник web / Telegram,
 * группы «Сегодня / Вчера / Ранее», закреплённые), быстрые вопросы по правам, контекст вопроса, черновик задачи из рекомендации, текст
 * ответа для копирования. Без зависимостей: проверяется node --test (src/lib/consultant-view.test.mjs).
 */

/** Цвет изменения решает сервер по смыслу показателя: good — улучшился, bad — ухудшился, warn — ниже цели, neutral — без изменения. */
export type Tone = "good" | "bad" | "warn" | "neutral";

export type AnswerProgress = { label: string; fact: number; plan: number; forecast: number | null; unit: string; forecastLabel: string | null };
export type KpiItem = { label: string; value: string; delta: string | null; tone: Tone };
export type FactDelta = { value: string; caption: string | null; tone: Tone };
export type FactRow = { label: string; value: string | null; deltas: FactDelta[] };
export type DeviationItem = { title: string; text: string; severity: "high" | "medium" };
export type RecommendationItem = { title: string; text: string | null; owner: string | null; dueDate: string | null; department: string | null };

export type AnswerBlock =
  | { type: "summary"; text: string; progress: AnswerProgress | null }
  | { type: "kpis"; items: KpiItem[] }
  | { type: "facts"; title: string | null; rows: FactRow[] }
  | { type: "deviations"; items: DeviationItem[] }
  | { type: "recommendations"; items: RecommendationItem[]; total: number }
  | { type: "markdown"; text: string };

export type AnswerMeta = { departments: number; facts: number; recommendations: number; model: string | null; latencyMs: number; dataAsOf: string | null };

/** report = false — простой ответ (приветствие, уточнение): показывается Markdown-ом, без карточки отчёта. */
export type StructuredAnswer = { meta: AnswerMeta; blocks: AnswerBlock[]; followUps: string[]; report: boolean };

export type ChatSource = "web" | "telegram";
export type SourceFilter = "all" | ChatSource;
export type ChatListItem = { id: string; title: string; createdAt: string; lastMessageAt: string; source?: ChatSource };

/** Начало названия чата, начатого в Telegram (сервер: ConsultantChatService.TelegramTitlePrefix). */
export const telegramTitlePrefix = "Telegram · ";

/** Название чата для списка: у Telegram-чата без «Telegram · » — источник показывает значок. */
export const chatTitle = (title: string) => (title.startsWith(telegramTitlePrefix) ? title.slice(telegramTitlePrefix.length) || title : title);

export const chatSource = (c: ChatListItem): ChatSource => c.source ?? (c.title.startsWith(telegramTitlePrefix) ? "telegram" : "web");

export const filterChats = <C extends ChatListItem>(list: C[], filter: SourceFilter): C[] => (filter === "all" ? list : list.filter((c) => chatSource(c) === filter));

/** Раздел истории по дате последнего сообщения (по местному времени устройства): Сегодня, Вчера, Ранее. */
export function chatSection(iso: string, now = new Date()): "Сегодня" | "Вчера" | "Ранее" {
  const d = new Date(iso);
  const day = (x: Date) => new Date(x.getFullYear(), x.getMonth(), x.getDate()).getTime();
  const diff = Math.round((day(now) - day(d)) / 86_400_000);
  return diff <= 0 ? "Сегодня" : diff === 1 ? "Вчера" : "Ранее";
}

/** Группы списка чатов: закреплённые сверху, затем Сегодня / Вчера / Ранее; порядок внутри — как пришёл (по последнему сообщению). */
export function groupChats<C extends ChatListItem>(list: C[], pinned: ReadonlySet<string>, now = new Date()): { label: string; items: C[] }[] {
  const groups: { label: string; items: C[] }[] = [];
  const add = (label: string, c: C) => {
    const g = groups.find((x) => x.label === label);
    if (g) g.items.push(c);
    else groups.push({ label, items: [c] });
  };
  for (const c of list) if (pinned.has(c.id)) add("Закреплённые", c);
  for (const c of list) if (!pinned.has(c.id)) add(chatSection(c.lastMessageAt, now), c);
  return groups;
}

/** Полоса плана: доли факта и прогноза от плана в процентах (одна десятая), ширина заливки — не больше 100. */
export function progressView(p: AnswerProgress) {
  const pct = (v: number) => (p.plan > 0 ? Math.round((v / p.plan) * 1000) / 10 : 0);
  const fact = pct(p.fact);
  const forecast = p.forecast === null ? null : pct(p.forecast);
  return { fact, forecast, factWidth: Math.min(100, Math.max(0, fact)), forecastWidth: forecast === null ? null : Math.min(100, Math.max(0, forecast)) };
}

/**
 * Число без Intl (одинаково на сервере и в браузере — без расхождения при гидратации, как у узбекской локали): тысячи через неразрывный
 * пробел, дробная часть через запятую, минус — «−».
 */
export function formatNumber(n: number, digits = 0): string {
  const [int, frac] = Math.abs(n).toFixed(digits).split(".");
  return `${n < 0 ? "−" : ""}${int.replace(/\B(?=(\d{3})+(?!\d))/g, "\u00a0")}${frac ? `,${frac}` : ""}`;
}

/** Знак изменения, а не только цвет: «+5,2%», «−3,8%». Сервер уже ставит знак; дефис меняется на минус. */
export const signed = (delta: string) => (delta.startsWith("-") ? `−${delta.slice(1)}` : delta);

/** Быстрые вопросы стартового экрана — по правам пользователя (без права «HR» — без вопроса об опозданиях). */
export type QuickQuestion = { text: string; icon: "trending-up" | "target" | "trending-down" | "compare" | "package" | "user-x" | "wallet" | "lightbulb"; tone: string };

const questions: (QuickQuestion & { needs: string | null })[] = [
  { text: "Как прошли продажи за месяц?", icon: "trending-up", tone: "success", needs: "sales.read" },
  { text: "Какие регионы не выполняют план?", icon: "target", tone: "danger", needs: "sales.read" },
  { text: "Что продаётся хуже всего?", icon: "trending-down", tone: "warning", needs: "sales.read" },
  { text: "Сравни с прошлым месяцем", icon: "compare", tone: "accent", needs: "sales.read" },
  { text: "Где заканчиваются остатки?", icon: "package", tone: "violet", needs: "sales.read" },
  { text: "Кто чаще всех опаздывает?", icon: "user-x", tone: "teal", needs: "hr.read" },
  { text: "Какая дебиторка просрочена?", icon: "wallet", tone: "warning", needs: "finance.read" },
  { text: "Что сделать, чтобы увеличить продажи?", icon: "lightbulb", tone: "accent", needs: "sales.read" },
];

/** Вопросы, доступные по правам: первые 6 — на экране, остальные — по «Ещё». Руководитель отдела видит HR своего отдела (hr.department). */
export function quickQuestions(permissions: readonly string[]): QuickQuestion[] {
  const has = (p: string) => permissions.includes(p) || (p === "hr.read" && permissions.includes("hr.department"));
  return questions.filter((q) => q.needs === null || has(q.needs)).map(({ needs: _, ...q }) => q);
}

/** Источники данных пользователя для строки под названием чата: «продажи, финансы, склад, HR» (ключи перевода). */
export function sourceLabels(permissions: readonly string[]): string[] {
  const out: string[] = [];
  if (permissions.includes("sales.read")) out.push("продажи");
  if (permissions.includes("finance.read")) out.push("финансы");
  if (permissions.includes("sales.read")) out.push("склад");
  if (permissions.includes("hr.read") || permissions.includes("hr.department")) out.push("HR");
  return out;
}

/** Отделы для чипа контекста: код → название (ключ перевода). */
export const departmentNames: Record<string, string> = {
  sales: "Продажи",
  finance: "Финансы",
  marketing: "Маркетинг",
  hr: "HR",
  production: "Производство",
  supply: "Снабжение",
};

/** Контекст вопроса: период (yyyy-MM) и отдел. Уходит на сервер вместе с вопросом; в истории чата вопрос остаётся как написан. */
export type ChatContext = { period: string | null; department: string | null; departmentName: string | null };

/** Месяц «сегодня» по Ташкенту (UTC+5): «2026-10». */
export function tashkentMonth(now = new Date()): string {
  const t = new Date(now.getTime() + 5 * 3_600_000);
  return `${t.getUTCFullYear()}-${String(t.getUTCMonth() + 1).padStart(2, "0")}`;
}

/** Контекст из адреса (?period=2026-10&dept=sales) или по умолчанию — текущий месяц; неверные значения не подставляются. */
export function initialContext(period: string | null | undefined, department: string | null | undefined, now = new Date()): ChatContext {
  const month = period && /^\d{4}-(0[1-9]|1[0-2])$/.test(period) ? period : tashkentMonth(now);
  const dept = department && department in departmentNames ? department : null;
  return { period: month, department: dept, departmentName: dept ? departmentNames[dept] : null };
}

/** Есть ли что отправить: пустой контекст — без поля context в запросе. */
export const contextPayload = (c: ChatContext) => (c.period || c.department ? c : null);

/** Черновик задачи из рекомендации: название, описание со ссылкой на чат, отдел и срок, если их определил сервер. */
export function taskDraft(item: RecommendationItem, chat: { url: string; title: string }) {
  const lines = [item.text, item.owner ? `Ответственный: ${item.owner}` : null, `Из чата AI-консультанта «${chat.title}»: ${chat.url}`].filter(Boolean);
  return { title: item.title.slice(0, 200), description: lines.join("\n"), department: item.department, dueDate: item.dueDate };
}

/** Текст ответа для «Копировать» и «Поделиться»: у отчёта — блоки текстом, у простого ответа — Markdown как есть. */
export function answerText(answer: StructuredAnswer | null | undefined, content: string): string {
  if (!answer?.report) return content;
  const out: string[] = [];
  for (const b of answer.blocks) {
    switch (b.type) {
      case "summary":
        out.push(b.text);
        break;
      case "kpis":
        out.push(b.items.map((k) => `${k.label}: ${k.value}${k.delta ? ` (${signed(k.delta)})` : ""}`).join("\n"));
        break;
      case "facts":
        out.push(b.rows.map((r) => `• ${r.label}${r.value ? `: ${r.value}` : ""}${r.deltas.length ? ` (${r.deltas.map((d) => `${signed(d.value)}${d.caption ? ` ${d.caption}` : ""}`).join(", ")})` : ""}`).join("\n"));
        break;
      case "deviations":
        out.push(b.items.map((d) => `• ${d.title}${d.text ? ` — ${d.text}` : ""}`).join("\n"));
        break;
      case "recommendations":
        out.push(b.items.map((r, i) => `${i + 1}. ${r.title}${r.text ? ` — ${r.text}` : ""}${r.owner ? ` (${r.owner})` : ""}`).join("\n"));
        break;
      case "markdown":
        out.push(b.text);
        break;
    }
  }
  return out.filter((x) => x.trim()).join("\n\n");
}

/** Шаги ответа с сервера (progress) → строка статуса: что идёт сейчас. */
export type ProgressStep = { stage: string; agent: string | null; agentName: string | null; status: string; note: string | null };

export function currentStep(steps: ProgressStep[]): { key: string; agentName: string | null } {
  const running = [...steps].reverse().find((s) => s.status === "Running");
  const s = running ?? steps[steps.length - 1];
  if (!s) return { key: "Определяю, каких AI-сотрудников привлечь", agentName: null };
  if (s.stage === "compose") return { key: "Формирую ответ", agentName: null };
  if (s.stage === "memory") return { key: "Вспоминаю прошлые анализы", agentName: null };
  if (s.stage === "routing") return { key: "Определяю, каких AI-сотрудников привлечь", agentName: null };
  if (s.agentName && s.status === "Running") return { key: "{agent}: собираю данные", agentName: s.agentName };
  return { key: s.status === "Running" ? "Анализирую" : "Сверяю данные", agentName: null };
}

/** Русская форма по числу: 1 рекомендация, 2 рекомендации, 5 рекомендаций (11–14 — «многие»). Ключи перевода — для каждой формы. */
export function plural(n: number, one: string, few: string, many: string): string {
  const d = Math.abs(n) % 100;
  if (d >= 11 && d <= 14) return many;
  switch (d % 10) {
    case 1:
      return one;
    case 2:
    case 3:
    case 4:
      return few;
    default:
      return many;
  }
}
