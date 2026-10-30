/** Раздел «Задачи» OneBase: отделы, задачи отделов и проектов, проекты, предложения AI-диспетчера (API /api/work/*). Задачи полевой команды — типы Sales Base (lib/field/types). */

export type DepartmentTasks = {
  id: string;
  code: string;
  name: string;
  /** Можно открыть раздел отдела. */
  canOpen: boolean;
  /** Отдел пользователя. */
  isMine: boolean;
  /** Видны задачи отдела (сотрудники, руководитель отдела и руководство). */
  hasTasks: boolean;
  /** Отдел «Продажи» с доступом к Sales Base: задачи и маршруты полевой команды. */
  hasField: boolean;
  openTasks: number;
  myTasks: number;
  overdueTasks: number;
  /** Пользователь управляет задачами отдела (руководитель отдела или руководство). */
  isHead: boolean;
};

export type WorkTaskStatus = "New" | "InProgress" | "Review" | "Done" | "Cancelled";
export type WorkTaskPriority = "Low" | "Medium" | "High" | "Urgent";

export type WorkTask = {
  id: string;
  title: string;
  description: string | null;
  priority: WorkTaskPriority;
  status: WorkTaskStatus;
  dueDate: string | null;
  overdue: boolean;
  assigneeId: string | null;
  assigneeName: string | null;
  createdById: string | null;
  createdByName: string | null;
  createdAt: string;
  completedAt: string | null;
  result: string | null;
  canEdit: boolean;
  nextStatuses: WorkTaskStatus[];
  departmentId: string;
  departmentCode: string;
  departmentName: string;
  projectId: string | null;
  projectName: string | null;
  startDate: string | null;
  progress: number;
  isBlocked: boolean;
  blockedReason: string | null;
  archivedAt: string | null;
  checklistDone: number;
  checklistTotal: number;
  canManage: boolean;
  canWork: boolean;
};

export type WorkTaskDetail = {
  task: WorkTask;
  checklist: { id: string; title: string; isDone: boolean; sortOrder: number; doneAt: string | null }[];
  comments: { id: string; authorId: string | null; authorName: string; text: string; createdAt: string }[];
  attachments: { id: string; fileName: string; contentType: string; sizeBytes: number; uploadedByName: string | null; createdAt: string; canDelete: boolean }[];
  history: { id: string; kind: string; text: string; actorName: string; createdAt: string }[];
  canArchive: boolean;
  canRestore: boolean;
  canComment: boolean;
  canAttach: boolean;
  /** Когда задача последний раз пришла в статус и кто её перевёл (полоса этапов, «сдал результат»). */
  stages: { status: WorkTaskStatus; at: string; actorName: string }[];
};

export type WorkOverview = {
  open: number;
  inProgress: number;
  review: number;
  overdue: number;
  blocked: number;
  dueThisWeek: number;
  doneThisMonth: number;
  myOpen: number;
  myOverdue: number;
  awaitingMyReview: number;
  pendingSuggestions: number;
  departments: { code: string; name: string; open: number; overdue: number; review: number; blocked: number }[];
  myUpcoming: WorkTask[];
};

export type WorkProject = {
  id: string;
  name: string;
  description: string | null;
  departmentCode: string;
  departmentName: string;
  archived: boolean;
  canManage: boolean;
  openTasks: number;
  members: { userId: string; name: string; isManager: boolean }[];
};

export type WorkUserOption = { id: string; name: string; position: string | null; departmentName: string | null };

export type WorkDepartmentHeads = { code: string; name: string; heads: WorkAssignee[] };

export type WorkSuggestionStatus = "Pending" | "Accepted" | "Rejected";

export type WorkSuggestion = {
  id: string;
  status: WorkSuggestionStatus;
  sourceKind: "alert" | "overdue" | "review" | string;
  title: string;
  problem: string;
  rationale: string;
  facts: string;
  insufficientData: boolean;
  departmentCode: string;
  departmentName: string;
  assigneeId: string | null;
  assigneeName: string | null;
  priority: WorkTaskPriority;
  dueDate: string | null;
  sourceHref: string | null;
  model: string | null;
  createdAt: string;
  decidedAt: string | null;
  decidedByName: string | null;
  decisionNote: string | null;
  createdTaskId: string | null;
};

export type WorkSuggestionRun = { candidates: number; created: number; duplicates: number; usedModel: boolean; note: string | null };

/** Кого можно назначить: любой действующий пользователь OneBase; inDepartment — сотрудник отдела задачи или участник проекта (первыми). */
export type WorkAssignee = { id: string; name: string; position: string | null; department: string | null; inDepartment: boolean };

export type PageResult<T> = { items: T[]; total: number; page: number; pageSize: number };

export const workStatuses: WorkTaskStatus[] = ["New", "InProgress", "Review", "Done", "Cancelled"];
export const workPriorities: WorkTaskPriority[] = ["Low", "Medium", "High", "Urgent"];

export const workStatusLabel: Record<WorkTaskStatus, string> = {
  New: "Новая",
  InProgress: "В работе",
  Review: "На проверке",
  Done: "Выполнена",
  Cancelled: "Отменена",
};

/** Подпись кнопки перехода в статус (зависит от того, откуда переходим — см. statusActionLabel). */
export const workStatusAction: Record<WorkTaskStatus, string> = {
  New: "Новая",
  InProgress: "В работу",
  Review: "На проверку",
  Done: "Выполнена",
  Cancelled: "Отменить",
};

/** Кнопка перехода: из «На проверке» в работу — «Вернуть на доработку», в «Выполнена» — «Принять»; выполненную в работу — «Вернуть в работу». */
export function statusActionLabel(from: WorkTaskStatus, to: WorkTaskStatus): string {
  if (from === "Review" && to === "InProgress") return "Вернуть на доработку";
  if (from === "Review" && to === "Done") return "Принять результат";
  if ((from === "Done" || from === "Cancelled") && to === "InProgress") return "Вернуть в работу";
  if (to === "Review") return "Отправить на проверку";
  if (to === "Done") return "Завершить";
  return workStatusAction[to];
}

/** Переход, для которого нужен комментарий: возврат на доработку (обязательно) и отмена (желательно). */
export const needsComment = (from: WorkTaskStatus, to: WorkTaskStatus) => (from === "Review" && to === "InProgress") || to === "Cancelled";

export const workPriorityLabel: Record<WorkTaskPriority, string> = {
  Low: "Низкий",
  Medium: "Обычный",
  High: "Высокий",
  Urgent: "Критический",
};

export const suggestionSourceLabel: Record<string, string> = {
  alert: "Находка анализа данных",
  overdue: "Просроченные задачи",
  review: "Задачи ждут проверки",
};

/** «12.10.2026» из «2026-10-12». */
export const shortDate = (iso: string | null | undefined) => (iso ? iso.slice(0, 10).split("-").reverse().join(".") : "");

/** Сегодня по времени компании (Ташкент), «2026-10-25». */
export const todayInTashkent = () => new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Tashkent" }).format(new Date());

/** Строка запроса без пустых значений. */
export const qs = (params: Record<string, string | number | boolean | undefined | null>) => {
  const p = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== false && v !== "") p.set(k, String(v));
  const s = p.toString();
  return s ? `?${s}` : "";
};

/** Параметры списка задач из адреса страницы (фильтры таблицы) — то же, что принимает /api/work/tasks. */
export const taskFilterKeys = ["search", "status", "priority", "department", "assigneeId", "projectId", "from", "to", "dateField", "sort", "desc", "page", "createdByMe", "mine"] as const;

export type TaskFilters = Partial<Record<(typeof taskFilterKeys)[number], string>>;

export const fileSize = (bytes: number) =>
  bytes < 1024 ? `${bytes} Б` : bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(0)} КБ` : `${(bytes / 1024 / 1024).toFixed(1)} МБ`;
