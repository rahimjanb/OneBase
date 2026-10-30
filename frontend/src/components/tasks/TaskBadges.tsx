"use client";

import { AlertTriangle, Ban, CalendarClock, FolderKanban } from "lucide-react";
import { useT } from "@/i18n/client";
import { shortDate, type WorkTask, type WorkTaskPriority, type WorkTaskStatus } from "@/lib/tasks";
import { PriorityPill, StatusPill } from "./TaskKit";

/** Статус и приоритет — из общего справочника вида задач (TaskKit): те же цвета и значки, что на канбане и в карточке. */
export function StatusBadge({ status }: { status: WorkTaskStatus }) {
  return <StatusPill status={status} />;
}

export function PriorityBadge({ priority }: { priority: WorkTaskPriority }) {
  return <PriorityPill priority={priority} />;
}

/** Процент выполнения: полоса и число. */
export function ProgressBar({ value, compact = false }: { value: number; compact?: boolean }) {
  const tone = value >= 100 ? "bg-ok" : value >= 50 ? "bg-accent" : "bg-warn";
  return (
    <span className="flex items-center gap-2" title={`${value}%`}>
      <span className={`block h-1.5 overflow-hidden rounded-full bg-muted ${compact ? "w-14" : "w-full min-w-16"}`}>
        <span className={`block h-full rounded-full ${tone}`} style={{ width: `${Math.max(0, Math.min(100, value))}%` }} />
      </span>
      <span className="text-xs tabular-nums text-ink-2">{value}%</span>
    </span>
  );
}

/** Признаки задачи: просрочена, заблокирована, проект — короткими значками. */
export function TaskFlags({ task }: { task: Pick<WorkTask, "overdue" | "isBlocked" | "blockedReason" | "projectName" | "dueDate"> }) {
  const t = useT();
  return (
    <span className="inline-flex flex-wrap items-center gap-1.5">
      {task.overdue && (
        <span className="inline-flex items-center gap-1 rounded-md bg-bad-soft px-1.5 py-0.5 text-[11px] font-semibold text-bad" title={t("Срок {date} прошёл", { date: shortDate(task.dueDate) })}>
          <CalendarClock className="size-3" /> {t("просрочена")}
        </span>
      )}
      {task.isBlocked && (
        <span className="inline-flex items-center gap-1 rounded-md bg-bad-soft px-1.5 py-0.5 text-[11px] font-semibold text-bad" title={task.blockedReason ?? undefined}>
          <Ban className="size-3" /> {t("блок")}
        </span>
      )}
      {task.projectName && (
        <span className="inline-flex max-w-40 items-center gap-1 truncate rounded-md bg-muted px-1.5 py-0.5 text-[11px] font-medium text-ink-2" title={task.projectName}>
          <FolderKanban className="size-3 shrink-0" /> <span className="truncate">{task.projectName}</span>
        </span>
      )}
    </span>
  );
}

export function ErrorNote({ text }: { text: string | null }) {
  if (!text) return null;
  return (
    <p role="alert" className="flex items-start gap-2 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">
      <AlertTriangle className="mt-0.5 size-4 shrink-0" /> {text}
    </p>
  );
}
