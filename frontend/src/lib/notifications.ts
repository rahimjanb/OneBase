/**
 * Уведомления OneBase (ТЗ notifications): типы и их вид в одном конфиге, группы «Сегодня / Вчера / Ранее» по часовому поясу устройства,
 * время («14:12», «вчера 18:00», «28 окт»), бейдж колокольчика, логика карточки push. Без зависимостей: проверяется node --test
 * (src/lib/notifications.test.mjs). Тексты — ключи перевода (русский).
 */

export type NotificationType =
  | "ai_insight"
  | "ai_report"
  | "ai_budget"
  | "task_overdue"
  | "task_due"
  | "task_done"
  | "task_comment"
  | "task_assigned"
  | "mention"
  | "hr_event"
  | "sales_alert"
  | "system";

export type NotificationCategory = "tasks" | "ai" | "hr" | "sales" | "system";
export type Severity = "critical" | "warning" | "info";
export type NotificationAction = { id: string; label: string; kind: "primary" | "secondary"; action: "open" | "extend_due" | "accept" | "dismiss" };

export type NotificationItem = {
  id: string;
  type: NotificationType | string;
  category: NotificationCategory | string;
  kind: string;
  title: string;
  body: string | null;
  href: string | null;
  createdAt: string;
  readAt: string | null;
  read: boolean;
  severity: Severity;
  actions: NotificationAction[];
};

export type NotificationCounts = { all: number; unread: number; tasks: number; ai: number; hr: number };
export type NotificationPage = { items: NotificationItem[]; nextCursor: string | null; counts: NotificationCounts; unread: number };
export type Filter = "all" | "unread" | "tasks" | "ai" | "hr";

/** Цвет типа — токен темы: значок и подпись раздела этим цветом, плитка — его «-soft». */
export type Tone = "violet" | "danger" | "accent" | "teal" | "warning" | "success";

/** Вид типа: значок (имя lucide), цвет и подпись раздела. Новый тип — строка здесь. */
export const TYPES: Record<NotificationType, { icon: string; tone: Tone; section: string }> = {
  ai_insight: { icon: "sparkles", tone: "violet", section: "AI" },
  ai_report: { icon: "file-chart-column", tone: "violet", section: "AI" },
  ai_budget: { icon: "gauge", tone: "warning", section: "AI" },
  task_overdue: { icon: "alarm-clock", tone: "danger", section: "Задачи" },
  task_due: { icon: "clock", tone: "warning", section: "Задачи" },
  task_done: { icon: "circle-check-big", tone: "success", section: "Задачи" },
  task_comment: { icon: "message-square-text", tone: "accent", section: "Задачи" },
  task_assigned: { icon: "square-check-big", tone: "accent", section: "Задачи" },
  mention: { icon: "at-sign", tone: "accent", section: "Задачи" },
  hr_event: { icon: "users", tone: "teal", section: "HR" },
  sales_alert: { icon: "trending-down", tone: "warning", section: "Продажи" },
  system: { icon: "refresh-cw", tone: "success", section: "Система" },
};

/** Вид уведомления: тип из конфига; сбой системы — красный значок, неизвестный тип — как системный. */
export function typeOf(n: Pick<NotificationItem, "type" | "severity">): { icon: string; tone: Tone; section: string } {
  const base = TYPES[n.type as NotificationType] ?? TYPES.system;
  if (n.type === "system" && n.severity === "critical") return { ...base, tone: "danger" };
  if (n.type === "sales_alert" && n.severity === "info") return { ...base, icon: "trending-up", tone: "success" };
  return base;
}

/** Вкладки: «Все», «Непрочитанные», разделы — только с доступом (HR — с правом HR или руководителю отдела). */
export function filtersFor(permissions: readonly string[] | null): Filter[] {
  const has = (p: string) => permissions === null || permissions.includes(p);
  const out: Filter[] = ["all", "unread"];
  if (has("work.tasks")) out.push("tasks");
  if (has("ai.agents.run")) out.push("ai");
  if (has("hr.read") || (permissions ?? []).includes("hr.department")) out.push("hr");
  return out;
}

export const filterLabel: Record<Filter, string> = { all: "Все", unread: "Непрочитанные", tasks: "Задачи", ai: "AI", hr: "HR" };

/** Число на вкладке: у «Все» и «Непрочитанные» — всегда, у разделов — не показывается (как в макете). */
export function filterCount(filter: Filter, counts: NotificationCounts | null): number | null {
  if (!counts) return null;
  return filter === "all" ? counts.all : filter === "unread" ? counts.unread : null;
}

const dayStart = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();

/** Раздел ленты по часовому поясу устройства: Сегодня, Вчера, Ранее. */
export function sectionOf(iso: string, now = new Date()): "Сегодня" | "Вчера" | "Ранее" {
  const diff = Math.round((dayStart(now) - dayStart(new Date(iso))) / 86_400_000);
  return diff <= 0 ? "Сегодня" : diff === 1 ? "Вчера" : "Ранее";
}

export function groupByDay<T extends { createdAt: string }>(items: T[], now = new Date()): { label: string; items: T[] }[] {
  const out: { label: string; items: T[] }[] = [];
  for (const n of items) {
    const label = sectionOf(n.createdAt, now);
    const g = out.find((x) => x.label === label);
    if (g) g.items.push(n);
    else out.push({ label, items: [n] });
  }
  return out;
}

const two = (n: number) => String(n).padStart(2, "0");

/**
 * Время уведомления: сегодня — «14:12», вчера — «вчера 18:00» (слово — ключ перевода, второе значение), раньше — «28 окт» (месяцы —
 * короткие названия языка интерфейса), в другом году — «28.10.2025».
 */
export function timeText(iso: string, now: Date, months: readonly string[]): { text: string; yesterday: boolean } {
  const d = new Date(iso);
  const time = `${two(d.getHours())}:${two(d.getMinutes())}`;
  const section = sectionOf(iso, now);
  if (section === "Сегодня") return { text: time, yesterday: false };
  if (section === "Вчера") return { text: time, yesterday: true };
  return d.getFullYear() === now.getFullYear()
    ? { text: `${d.getDate()} ${months[d.getMonth()]}`, yesterday: false }
    : { text: `${two(d.getDate())}.${two(d.getMonth() + 1)}.${d.getFullYear()}`, yesterday: false };
}

export const monthsRu = ["янв", "фев", "мар", "апр", "мая", "июн", "июл", "авг", "сен", "окт", "ноя", "дек"];
export const monthsUz = ["yan", "fev", "mar", "apr", "may", "iyn", "iyl", "avg", "sen", "okt", "noy", "dek"];

/** Колокольчик: без непрочитанных — ничего, до 9 — красная точка, больше 9 — «9+». */
export const bellBadge = (unread: number): "none" | "dot" | "9+" => (unread <= 0 ? "none" : unread > 9 ? "9+" : "dot");

/** Русская форма по числу (1 непрочитанное, 2 непрочитанных…) — ключи перевода на каждую форму. */
export function plural(n: number, one: string, few: string, many: string): string {
  const d = Math.abs(n) % 100;
  if (d >= 11 && d <= 14) return many;
  const u = d % 10;
  return u === 1 ? one : u >= 2 && u <= 4 ? few : many;
}

/** Push на устройстве: что показать на карточке. */
export type PushState = "default" | "granted" | "denied" | "ios-install" | "unsupported";

/** «Не сейчас» и крестик скрывают карточку push на этом устройстве на 7 дней. */
export const PUSH_SNOOZE_MS = 7 * 24 * 3_600_000;

/** Показывать ли карточку push: включено или не поддерживается — нет; скрыта «Не сейчас» меньше 7 дней назад — нет (в пустом состоянии — всегда). */
export function showPushPrompt(state: PushState, snoozedAt: number | null, now: number, ignoreSnooze = false): boolean {
  if (state === "granted" || state === "unsupported") return false;
  return ignoreSnooze || snoozedAt === null || now - snoozedAt >= PUSH_SNOOZE_MS;
}

/** Состояние push по поддержке, разрешению браузера и подписке этого устройства. */
export function pushStateOf(support: "supported" | "ios-install" | "unsupported", permission: string, subscribed: boolean): PushState {
  if (support !== "supported") return support;
  if (permission === "denied") return "denied";
  return permission === "granted" && subscribed ? "granted" : "default";
}

/** Список после действия: прочитано / не прочитано / удалено — и счётчики, чтобы колокольчик, вкладки и бейдж совпали сразу. */
export function applyRead(page: NotificationPage, id: string, read: boolean): NotificationPage {
  const item = page.items.find((n) => n.id === id);
  if (!item || item.read === read) return page;
  const delta = read ? -1 : 1;
  return {
    ...page,
    items: page.items.map((n) => (n.id === id ? { ...n, read, readAt: read ? new Date().toISOString() : null } : n)),
    counts: { ...page.counts, unread: Math.max(0, page.counts.unread + delta) },
    unread: Math.max(0, page.unread + delta),
  };
}

export function applyRemove(page: NotificationPage, id: string): NotificationPage {
  const item = page.items.find((n) => n.id === id);
  if (!item) return page;
  const section = item.category as keyof NotificationCounts;
  return {
    ...page,
    items: page.items.filter((n) => n.id !== id),
    counts: {
      ...page.counts,
      all: Math.max(0, page.counts.all - 1),
      unread: Math.max(0, page.counts.unread - (item.read ? 0 : 1)),
      ...(section === "tasks" || section === "ai" || section === "hr" ? { [section]: Math.max(0, page.counts[section] - 1) } : {}),
    },
    unread: Math.max(0, page.unread - (item.read ? 0 : 1)),
  };
}

export function applyReadAll(page: NotificationPage): NotificationPage {
  return { ...page, items: page.items.map((n) => (n.read ? n : { ...n, read: true, readAt: new Date().toISOString() })), counts: { ...page.counts, unread: 0 }, unread: 0 };
}

/** Новые сверху: элементы, которых не было в прошлой загрузке (подсветка «только что пришло»). */
export function freshIds(before: NotificationItem[], after: NotificationItem[]): string[] {
  const known = new Set(before.map((n) => n.id));
  return before.length === 0 ? [] : after.filter((n) => !known.has(n.id)).map((n) => n.id);
}
