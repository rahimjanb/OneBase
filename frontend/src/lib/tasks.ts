/** Раздел «Задачи» OneBase: отделы, задачи отделов (API /api/work/*). Задачи полевой команды — типы Sales Base (lib/field/types). */

export type DepartmentTasks = {
  id: string;
  code: string;
  name: string;
  /** Можно открыть раздел отдела. */
  canOpen: boolean;
  /** Отдел пользователя. */
  isMine: boolean;
  /** Видны задачи отдела (сотрудники и руководство). */
  hasTasks: boolean;
  /** Отдел «Продажи» с доступом к Sales Base: задачи и маршруты полевой команды. */
  hasField: boolean;
  openTasks: number;
  myTasks: number;
  overdueTasks: number;
};

export type WorkTaskStatus = "New" | "InProgress" | "Done" | "Cancelled";
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
};

export type WorkAssignee = { id: string; name: string; position: string | null };

export type PageResult<T> = { items: T[]; total: number; page: number; pageSize: number };

export const workStatusLabel: Record<WorkTaskStatus, string> = {
  New: "Новая",
  InProgress: "В работе",
  Done: "Выполнена",
  Cancelled: "Отменена",
};

/** Подпись кнопки перехода в статус. */
export const workStatusAction: Record<WorkTaskStatus, string> = {
  New: "Новая",
  InProgress: "В работу",
  Done: "Выполнена",
  Cancelled: "Отменить",
};

export const workStatusTone: Record<WorkTaskStatus, string> = {
  New: "bg-accent-soft text-accent-strong",
  InProgress: "bg-warn-soft text-warn",
  Done: "bg-ok-soft text-ok",
  Cancelled: "bg-muted text-ink-3",
};

export const workPriorityLabel: Record<WorkTaskPriority, string> = {
  Low: "Низкий",
  Medium: "Обычный",
  High: "Высокий",
  Urgent: "Срочно",
};

export const workPriorityTone: Record<WorkTaskPriority, string> = {
  Low: "text-ink-3",
  Medium: "text-ink-2",
  High: "text-warn",
  Urgent: "text-bad",
};

/** «12.10.2026» из «2026-10-12». */
export const shortDate = (iso: string | null | undefined) => (iso ? iso.slice(0, 10).split("-").reverse().join(".") : "");
