import type {
  AgentDayStatus,
  FieldActorType,
  FieldCustomerStatus,
  FieldGeoStatus,
  FieldPointStatus,
  FieldPriority,
  FieldRecommendationKind,
  FieldRole,
  FieldRouteSource,
  FieldTaskStatus,
  FieldVisitResult,
} from "./types";

export type Tone = "ok" | "warn" | "bad" | "accent" | "muted" | "ink";

/** Классы «чипа» статуса: фон + текст. */
export const chipTone: Record<Tone, string> = {
  ok: "bg-ok-soft text-ok",
  warn: "bg-warn-soft text-warn",
  bad: "bg-bad-soft text-bad",
  accent: "bg-accent-soft text-accent-strong",
  muted: "bg-muted text-ink-3",
  ink: "bg-muted text-ink-2",
};

export const roleLabel: Record<FieldRole, string> = { Rm: "РМ", Supervisor: "Супервайзер", Agent: "Агент" };

export const priorityLabel: Record<FieldPriority, [string, Tone]> = {
  Low: ["Низкий", "muted"],
  Medium: ["Средний", "ink"],
  High: ["Высокий", "warn"],
  Urgent: ["Срочно", "bad"],
};

export const customerStatusLabel: Record<FieldCustomerStatus, [string, Tone]> = {
  Active: ["Активная", "ok"],
  Problem: ["Проблемная", "bad"],
  Inactive: ["Неактивная", "muted"],
};

export const pointStatusLabel: Record<FieldPointStatus, [string, Tone]> = {
  Planned: ["Запланирована", "ink"],
  InProgress: ["Идёт визит", "accent"],
  Visited: ["Посещена", "ok"],
  Skipped: ["Пропущена", "bad"],
  Cancelled: ["Отменена", "muted"],
};

export const routeSourceLabel: Record<FieldRouteSource, string> = { Manual: "вручную", Linko: "план Linko", Ai: "AI-планирование" };

export const visitResultLabel: Record<FieldVisitResult, [string, Tone]> = {
  Order: ["Заказ", "ok"],
  Sale: ["Продажа", "ok"],
  Refusal: ["Отказ", "bad"],
  Revisit: ["Повторный визит", "warn"],
  Closed: ["Точка закрыта", "bad"],
  NoDecisionMaker: ["Нет ЛПР", "warn"],
  Other: ["Другое", "muted"],
};

export const geoLabel: Record<FieldGeoStatus, [string, Tone]> = {
  Ok: ["На месте", "ok"],
  Far: ["Далеко от точки", "warn"],
  NoGps: ["Нет GPS", "muted"],
  NoTarget: ["У точки нет координат", "muted"],
};

export const taskStatusLabel: Record<FieldTaskStatus, [string, Tone]> = {
  New: ["Новая", "accent"],
  Accepted: ["Принята", "ink"],
  InProgress: ["В работе", "accent"],
  Completed: ["Выполнена", "ok"],
  Verified: ["Подтверждена", "ok"],
  Cancelled: ["Отменена", "muted"],
  Postponed: ["Отложена", "warn"],
};

/** Подпись кнопки перехода задачи. */
export const taskActionLabel: Record<FieldTaskStatus, string> = {
  New: "Новая",
  Accepted: "Принять",
  InProgress: "В работу",
  Completed: "Выполнено",
  Verified: "Подтвердить",
  Cancelled: "Отменить",
  Postponed: "Перенести",
};

export const actorLabel: Record<FieldActorType, string> = { Rm: "РМ", Supervisor: "Супервайзер", Agent: "Агент", Ai: "AI", System: "Система" };

export const dayStatusLabel: Record<AgentDayStatus, [string, Tone]> = {
  NotStarted: ["Не начал", "muted"],
  OnRoute: ["На маршруте", "accent"],
  OnVisit: ["На визите", "accent"],
  Finished: ["Завершил", "ok"],
  Problem: ["Проблема", "bad"],
};

export const recommendationKindLabel: Record<FieldRecommendationKind, string> = {
  SalesDecline: "Падение продаж",
  NotVisited: "Давно не посещали",
  LostCustomer: "Клиент перестал покупать",
  AgentBehindPlan: "Отставание от плана",
  SkippedPoint: "Пропущенная точка",
  HighPotential: "Высокий потенциал",
  Reassign: "Точка без агента",
};

/** Цвет точки на карте по состоянию. */
export const mapStateStyle: Record<string, { color: string; label: string }> = {
  visited: { color: "#1f9d6b", label: "Посещена" },
  planned: { color: "#3b76f6", label: "Запланирована" },
  skipped: { color: "#dc4c4c", label: "Пропущена" },
  problem: { color: "#b5179e", label: "Проблемная" },
  priority: { color: "#e0922f", label: "Высокий приоритет" },
  new: { color: "#14b8a6", label: "Новая" },
  normal: { color: "#8793a7", label: "Точка" },
};

export const plural = (n: number, one: string, few: string, many: string) => {
  const a = Math.abs(Math.round(n)) % 100;
  const b = a % 10;
  if (a > 10 && a < 20) return many;
  if (b > 1 && b < 5) return few;
  if (b === 1) return one;
  return many;
};
