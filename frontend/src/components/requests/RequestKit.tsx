"use client";

import { ArrowDown, ArrowUp, BadgeCheck, Ban, CircleCheckBig, FilePen, Flame, Hourglass, Minus, OctagonX, PackageOpen, Send, Undo2, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import { requestPriorityLabel, requestStatusLabel, type RequestPriority, type RequestStatus } from "@/lib/procurement";

/** Вид статусов заявки — один для таблицы, карточек, обзора и карточки заявки: цвет и значок (токены темы). */
export const requestStatusLook: Record<RequestStatus, { icon: LucideIcon; tone: string; dot: string }> = {
  Draft: { icon: FilePen, tone: "bg-muted text-ink-2", dot: "bg-ink-3" },
  Submitted: { icon: Send, tone: "bg-accent-soft text-accent-strong", dot: "bg-accent" },
  DirectorApproval: { icon: Hourglass, tone: "bg-violet-soft text-violet-light", dot: "bg-violet" },
  RevisionRequired: { icon: Undo2, tone: "bg-warn-soft text-warn", dot: "bg-warn" },
  Approved: { icon: BadgeCheck, tone: "bg-ok-soft text-ok", dot: "bg-ok" },
  PartiallyFulfilled: { icon: PackageOpen, tone: "bg-accent-soft text-accent-strong", dot: "bg-accent" },
  Fulfilled: { icon: CircleCheckBig, tone: "bg-ok-soft text-ok", dot: "bg-ok" },
  Rejected: { icon: OctagonX, tone: "bg-bad-soft text-bad", dot: "bg-bad" },
  Cancelled: { icon: Ban, tone: "bg-muted text-ink-3", dot: "bg-ink-4" },
};

const priorityLook: Record<RequestPriority, { icon: LucideIcon; tone: string }> = {
  Urgent: { icon: Flame, tone: "bg-bad-soft text-bad" },
  High: { icon: ArrowUp, tone: "bg-warn-soft text-warn" },
  Normal: { icon: Minus, tone: "bg-muted text-ink-3" },
  Low: { icon: ArrowDown, tone: "bg-muted text-ink-4" },
};

export function RequestStatusPill({ status, className = "" }: { status: RequestStatus; className?: string }) {
  const t = useT();
  const look = requestStatusLook[status];
  return (
    <span className={`inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-md px-2 py-0.5 text-xs font-semibold leading-5 ${look.tone} ${className}`}>
      <look.icon className="size-3.5" aria-hidden />
      {t(requestStatusLabel[status])}
    </span>
  );
}

export function RequestPriorityPill({ priority, className = "" }: { priority: RequestPriority; className?: string }) {
  const t = useT();
  const look = priorityLook[priority];
  return (
    <span className={`inline-flex shrink-0 items-center gap-1 whitespace-nowrap rounded-md px-[7px] py-0.5 text-[11px] font-semibold leading-4 ${look.tone} ${className}`}>
      <look.icon className="size-[11px]" strokeWidth={2.25} aria-hidden />
      {t(requestPriorityLabel[priority])}
    </span>
  );
}
