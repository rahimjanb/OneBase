"use client";

import {
  ArrowDown,
  ArrowUp,
  Ban,
  Building2,
  CalendarDays,
  CircleCheckBig,
  CircleDot,
  Eye,
  Flame,
  Loader,
  Minus,
  OctagonX,
  type LucideIcon,
} from "lucide-react";
import { useT } from "@/i18n/client";
import { dayMonth, dueState, initialsOf } from "@/lib/task-board";
import { todayInTashkent, workPriorityLabel, workStatusLabel, type WorkTask, type WorkTaskPriority, type WorkTaskStatus } from "@/lib/tasks";

/**
 * Справочник вида задач (ТЗ tasks-kanban-detail) — один для канбана, таблицы и карточки: цвет и значок статуса, пилюля приоритета,
 * аватар исполнителя, срок. Цвета — токены темы (внутри .tasks-ui — палитра макета, тёмная и светлая).
 */
export type StatusKey = WorkTaskStatus | "Blocked";

export const statusLook: Record<
  StatusKey,
  { label: string; column: string; short: string; icon: LucideIcon; text: string; dot: string; soft: string; border: string; over: string }
> = {
  New: { label: workStatusLabel.New, column: "Новые", short: "Новые", icon: CircleDot, text: "text-accent-strong", dot: "bg-accent", soft: "bg-accent-soft", border: "border-accent", over: "bg-accent/[0.06]" },
  InProgress: { label: workStatusLabel.InProgress, column: "В работе", short: "В работе", icon: Loader, text: "text-warn", dot: "bg-warn", soft: "bg-warn-soft", border: "border-warn", over: "bg-warn/[0.06]" },
  Review: { label: workStatusLabel.Review, column: "На проверке", short: "Проверка", icon: Eye, text: "text-violet", dot: "bg-violet", soft: "bg-violet-soft", border: "border-violet", over: "bg-violet/[0.06]" },
  Done: { label: workStatusLabel.Done, column: "Выполнены", short: "Готово", icon: CircleCheckBig, text: "text-ok", dot: "bg-ok", soft: "bg-ok-soft", border: "border-ok", over: "bg-ok/[0.06]" },
  Blocked: { label: "Заблокирована", column: "Заблокированы", short: "Блок", icon: OctagonX, text: "text-bad", dot: "bg-bad", soft: "bg-bad-soft", border: "border-bad", over: "bg-bad/[0.06]" },
  Cancelled: { label: workStatusLabel.Cancelled, column: "Отменены", short: "Отменена", icon: Ban, text: "text-subtle", dot: "bg-subtle", soft: "bg-surface-2", border: "border-line-strong", over: "bg-surface-2" },
};

export const priorityLook: Record<WorkTaskPriority, { icon: LucideIcon; tone: string }> = {
  Urgent: { icon: Flame, tone: "bg-bad-soft text-bad" },
  High: { icon: ArrowUp, tone: "bg-warn-soft text-warn" },
  Medium: { icon: Minus, tone: "bg-surface-2 text-ink-3" },
  Low: { icon: ArrowDown, tone: "bg-surface-2 text-subtle" },
};

/** Статус задачи с учётом блокировки (блокировка — признак открытой задачи). */
export const statusKey = (task: Pick<WorkTask, "status" | "isBlocked">): StatusKey =>
  task.isBlocked && (task.status === "New" || task.status === "InProgress" || task.status === "Review") ? "Blocked" : task.status;

export function PriorityPill({ priority, className = "" }: { priority: WorkTaskPriority; className?: string }) {
  const t = useT();
  const look = priorityLook[priority];
  return (
    <span className={`inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-md px-[7px] py-0.5 text-[11px] font-semibold leading-4 ${look.tone} ${className}`}>
      <look.icon className="size-[11px]" strokeWidth={2.25} aria-hidden />
      {t(workPriorityLabel[priority])}
    </span>
  );
}

export function StatusPill({ status, className = "" }: { status: StatusKey; className?: string }) {
  const t = useT();
  const look = statusLook[status];
  return (
    <span className={`inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-md px-2 py-0.5 text-xs font-semibold leading-5 ${look.soft} ${look.text} ${className}`}>
      <look.icon className="size-3.5" aria-hidden />
      {t(look.label)}
    </span>
  );
}

/** Аватар: инициалы, полное имя — во всплывающей подсказке и для экранного диктора. */
export function UserAvatar({ name, size = 24, tone = "soft" }: { name: string | null | undefined; size?: number; tone?: "soft" | "brand" }) {
  const t = useT();
  const label = name ?? t("не назначен");
  return (
    <span
      title={label}
      aria-label={label}
      role="img"
      style={{ width: size, height: size, fontSize: Math.max(9, Math.round(size * 0.38)) }}
      className={`grid shrink-0 place-items-center rounded-full font-bold leading-none ${
        tone === "brand" ? "bg-gradient-to-br from-violet to-accent text-white" : name ? "bg-accent-soft text-accent-strong" : "border border-dashed border-line-strong text-subtle"
      }`}
    >
      {name ? initialsOf(name) : "?"}
    </span>
  );
}

/** Срок карточки: «📅 12.11»; просрочен — красный, сегодня или завтра — жёлтый; нет срока — «без срока». prefix — «до 12.11» на телефоне. */
export function DueLabel({ task, prefix = false, icon = true }: { task: Pick<WorkTask, "dueDate" | "status" | "overdue">; prefix?: boolean; icon?: boolean }) {
  const t = useT();
  const state = dueState(task, todayInTashkent());
  const tone = state === "overdue" ? "font-semibold text-bad" : state === "soon" ? "font-semibold text-warn" : state === "none" ? "text-subtle" : "text-ink-3";
  return (
    <span className={`inline-flex shrink-0 items-center gap-1 whitespace-nowrap tabular-nums ${tone}`}>
      {icon && <CalendarDays className="size-3.5" aria-hidden />}
      {state === "none" ? t("без срока") : prefix ? t("до {date}", { date: dayMonth(task.dueDate) }) : dayMonth(task.dueDate)}
    </span>
  );
}

/** Отдел задачи — чипом. */
export function DepartmentChip({ name }: { name: string }) {
  return (
    <span className="inline-flex min-w-0 items-center gap-1 rounded-md bg-surface-2 px-1.5 py-0.5 text-[11px] font-medium text-ink-2">
      <Building2 className="size-3 shrink-0" aria-hidden />
      <span className="truncate">{name}</span>
    </span>
  );
}
