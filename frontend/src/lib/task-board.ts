import type { WorkTask, WorkTaskDetail, WorkTaskStatus } from "./tasks";

/**
 * Канбан и карточка задачи (ТЗ tasks-kanban-detail): чистые правила без React — срок на карточке, фильтры в адресе, этапы, лента
 * активности, @-упоминания, соседние задачи списка. Проверяются тестами (task-board.test.mjs).
 */

/** Колонки канбана слева направо. */
export const boardStatuses: WorkTaskStatus[] = ["New", "InProgress", "Review", "Done"];

/** Группы на телефоне: сначала то, где ждут действия пользователя. */
export const mobileOrder: WorkTaskStatus[] = ["Review", "InProgress", "New", "Done"];

/** Срок на карточке: просрочен — красный, сегодня или завтра — жёлтый, позже — обычный, нет срока — «без срока». */
export type DueState = "overdue" | "soon" | "later" | "none";

export const addDays = (iso: string, days: number) => {
  const d = new Date(`${iso.slice(0, 10)}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};

export function dueState(task: Pick<WorkTask, "dueDate" | "status" | "overdue">, today: string): DueState {
  if (!task.dueDate) return "none";
  const open = task.status === "New" || task.status === "InProgress" || task.status === "Review";
  if (!open) return "later";
  if (task.overdue || task.dueDate < today) return "overdue";
  return task.dueDate <= addDays(today, 1) ? "soon" : "later";
}

/** «12.11» из «2026-11-12». */
export const dayMonth = (iso: string | null | undefined) => (iso ? `${iso.slice(8, 10)}.${iso.slice(5, 7)}` : "");

/** Полоса выполнения на карточке — только между 0 и 100: у выполненных её роль играет галочка. */
export const showProgress = (progress: number) => progress > 0 && progress < 100;

/** Проверяющий: задача на проверке и ему можно и принять, и вернуть. */
export const canReview = (task: Pick<WorkTask, "status" | "nextStatuses">) =>
  task.status === "Review" && task.nextStatuses.includes("Done") && task.nextStatuses.includes("InProgress");

/** Значение фильтра из адреса: «a,b» → ["a", "b"]. */
export const listOf = (value: string | null | undefined) =>
  (value ?? "")
    .split(",")
    .map((s) => s.trim())
    .filter(Boolean);

export const toggleIn = (list: string[], value: string) => (list.includes(value) ? list.filter((x) => x !== value) : [...list, value]);

/** Период «Создана»: пресеты вместо двух полей дат. */
export type DatePreset = "today" | "7d" | "30d" | "custom";

export function presetRange(preset: Exclude<DatePreset, "custom">, today: string) {
  const days = preset === "today" ? 0 : preset === "7d" ? 6 : 29;
  return { from: addDays(today, -days), to: today };
}

/** Какой пресет выбран (по from/to в адресе); null — период не задан. */
export function presetOf(from: string | null | undefined, to: string | null | undefined, today: string): DatePreset | null {
  if (!from && !to) return null;
  for (const p of ["today", "7d", "30d"] as const) {
    const r = presetRange(p, today);
    if (from === r.from && (to === r.to || !to)) return p;
  }
  return "custom";
}

/** Ключи фильтров канбана и таблицы в адресе (без поиска). */
export const filterKeys = ["priority", "department", "assigneeId", "status", "from", "mine"] as const;

/** Сколько фильтров включено — число на кнопке «Фильтры» (поиск не считается: он виден в поле). */
export function activeFilterCount(get: (key: string) => string | null, fixedDepartment?: string) {
  return filterKeys.filter((k) => {
    if (k === "department" && fixedDepartment) return false;
    if (k === "from") return !!(get("from") || get("to"));
    return !!get(k);
  }).length;
}

/** Этап полосы: пройден, текущий, впереди; at — когда задача в него пришла (последний раз). */
export type StageStep = { status: WorkTaskStatus; state: "done" | "current" | "future"; at: string | null; actor: string | null };

export function stageSteps(status: WorkTaskStatus, stages: WorkTaskDetail["stages"], createdAt: string): StageStep[] {
  const current = boardStatuses.indexOf(status);
  return boardStatuses.map((s, i) => {
    const entry = stages.find((x) => x.status === s);
    const at = entry?.at ?? (s === "New" ? createdAt : null);
    const state = current < 0 ? "future" : i < current ? "done" : i === current ? "current" : "future";
    return { status: s, state, at: state === "future" ? null : at, actor: entry?.actorName ?? null };
  });
}

/** Значок события ленты по виду записи истории (WorkTaskService.Event). */
export type EventLook = "created" | "done" | "returned" | "review" | "blocked" | "unblocked" | "cancelled" | "status" | "changed";

export function eventLook(kind: string, text: string): EventLook {
  switch (kind) {
    case "created":
      return "created";
    case "accepted":
      return "done";
    case "returned":
      return "returned";
    case "review":
      return "review";
    case "blocked":
      return text.startsWith("Блокировка снята") ? "unblocked" : "blocked";
    case "status":
      if (text.startsWith("Возвращена в работу")) return "returned";
      if (text.startsWith("Задача отменена")) return "cancelled";
      return "status";
    default:
      return "changed";
  }
}

export type ActivityFilter = "all" | "comments" | "history";

export type ActivityEntry =
  | { kind: "comment"; id: string; at: string; who: string; authorId: string | null; text: string }
  | { kind: "event"; id: string; at: string; who: string; text: string; look: EventLook };

/** Комментарии и история одной лентой, новые сверху; фильтр — сегменты «Всё · Комментарии · История». */
export function activity(detail: Pick<WorkTaskDetail, "comments" | "history">, filter: ActivityFilter): ActivityEntry[] {
  const comments: ActivityEntry[] =
    filter === "history" ? [] : detail.comments.map((c) => ({ kind: "comment", id: c.id, at: c.createdAt, who: c.authorName, authorId: c.authorId, text: c.text }));
  const events: ActivityEntry[] =
    filter === "comments" ? [] : detail.history.map((h) => ({ kind: "event", id: h.id, at: h.createdAt, who: h.actorName, text: h.text, look: eventLook(h.kind, h.text) }));
  return [...comments, ...events].sort((a, b) => b.at.localeCompare(a.at) || (a.kind === "comment" ? -1 : 1));
}

/** Набирается упоминание: «@» в начале строки или после пробела и до курсора — имя без переноса строки. */
export function mentionAt(text: string, caret: number): { start: number; query: string } | null {
  const before = text.slice(0, caret);
  const at = before.lastIndexOf("@");
  if (at < 0) return null;
  if (at > 0 && !/\s/.test(before[at - 1])) return null;
  const query = before.slice(at + 1);
  if (query.length > 40 || /[\n@]/.test(query) || /\s{2}/.test(query) || query.split(" ").length > 3) return null;
  return { start: at, query };
}

/** Вставить выбранного человека вместо набранного «@запрос»: «@Имя Фамилия » и курсор после пробела. */
export function insertMention(text: string, start: number, caret: number, name: string) {
  const inserted = `@${name} `;
  return { text: text.slice(0, start) + inserted + text.slice(caret), caret: start + inserted.length };
}

/** Кого упомянули: люди, чьё «@Имя» осталось в тексте (стёртое упоминание уведомления не шлёт). */
export function mentionedIds(text: string, people: { id: string; name: string }[]) {
  return people.filter((p) => text.includes(`@${p.name}`)).map((p) => p.id);
}

/** Текст комментария частями: упоминания (известные имена — целиком, иначе «@Слово») подсвечиваются. */
export function splitMentions(text: string, names: string[] = []): { text: string; mention: boolean }[] {
  const known = [...names].filter(Boolean).sort((a, b) => b.length - a.length);
  const parts: { text: string; mention: boolean }[] = [];
  let rest = "";
  let i = 0;
  while (i < text.length) {
    const prevOk = i === 0 || /\s|[(«"]/.test(text[i - 1]);
    if (text[i] === "@" && prevOk) {
      const name = known.find((n) => text.startsWith(n, i + 1));
      const generic = name ? null : /^[\p{L}][\p{L}\p{N}._-]*/u.exec(text.slice(i + 1))?.[0];
      const len = name?.length ?? generic?.length ?? 0;
      if (len > 0) {
        if (rest) parts.push({ text: rest, mention: false });
        rest = "";
        parts.push({ text: text.slice(i, i + 1 + len), mention: true });
        i += 1 + len;
        continue;
      }
    }
    rest += text[i];
    i += 1;
  }
  if (rest) parts.push({ text: rest, mention: false });
  return parts;
}

/** Место задачи в списке, откуда её открыли: «1 из 5» и соседи (↑↓, J/K). */
export function neighbors(ids: string[], id: string): { index: number; total: number; prev: string | null; next: string | null } | null {
  const index = ids.indexOf(id);
  if (index < 0) return null;
  return { index, total: ids.length, prev: index > 0 ? ids[index - 1] : null, next: index < ids.length - 1 ? ids[index + 1] : null };
}

/** Порядок задач на доске, как их видно: колонки слева направо, в колонке — сверху вниз (выполненные — свежие первыми). */
export function boardOrder(tasks: WorkTask[]): WorkTask[] {
  return boardStatuses.flatMap((s) => columnTasks(tasks, s));
}

export function columnTasks(tasks: WorkTask[], status: WorkTaskStatus): WorkTask[] {
  const list = tasks.filter((t) => t.status === status);
  return status === "Done" ? [...list].sort((a, b) => (b.completedAt ?? "").localeCompare(a.completedAt ?? "")) : list;
}

/** Время по Ташкенту: «29.10.2026, 16:23» или без года — «29.10, 16:23»; parts — день и время отдельно («29.10», «16:23»). */
export function stampParts(iso: string) {
  const parts = Object.fromEntries(
    new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit", hourCycle: "h23" })
      .formatToParts(new Date(iso))
      .map((p) => [p.type, p.value]),
  );
  return { day: `${parts.day}.${parts.month}`, year: parts.year, time: `${parts.hour}:${parts.minute}` };
}

export function stamp(iso: string, withYear = true) {
  const p = stampParts(iso);
  return `${p.day}${withYear ? `.${p.year}` : ""}, ${p.time}`;
}

/** Инициалы для аватара: «Admin Admin» → «AA», «Карим» → «КА». */
export function initialsOf(name: string | null | undefined) {
  const words = (name ?? "").trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return "?";
  const letters = words.length > 1 ? words[0][0] + words[1][0] : words[0].slice(0, 2);
  return letters.toUpperCase();
}
