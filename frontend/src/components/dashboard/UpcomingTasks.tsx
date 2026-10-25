"use client";

import Link from "next/link";
import { useState } from "react";
import { ArrowRight, Calendar, Flag } from "lucide-react";
import { useT } from "@/i18n/client";
import type { Priority, UpcomingTask } from "@/lib/dashboard.mock";
import { Avatar, CardHeader, DashCard, linkClass } from "./blocks";

const priorityOf: Record<Priority, { label: string; className: string }> = {
  high: { label: "Высокий", className: "text-bad" },
  medium: { label: "Средний", className: "text-warn" },
  low: { label: "Низкий", className: "text-ink-3" },
};

const dueChip: Record<UpcomingTask["dueTone"], string> = { bad: "bg-bad/10 text-bad", warn: "bg-warn/10 text-warn", muted: "bg-muted text-ink-3" };

/**
 * «Ближайшие дедлайны»: открытые задачи отделов со сроком (реальные, /api/work/tasks) с приоритетом, сроком и исполнителем; чекбокс —
 * отметка на странице, статус меняется в разделе «Задачи». Нет задач или раздел недоступен — пустое состояние.
 */
export function UpcomingTasks({ items, total, available }: { items: UpcomingTask[]; total: number; available: boolean }) {
  const t = useT();
  const [done, setDone] = useState<Record<string, boolean>>({});

  return (
    <DashCard className="flex flex-col p-5">
      <CardHeader
        title={t("Ближайшие дедлайны")}
        subtitle={available ? t("{n} открытых задач со сроком", { n: total }) : t("Раздел «Задачи» недоступен")}
        action={
          <Link href="/tasks" className={linkClass}>
            {t("Все задачи")} <ArrowRight className="size-3.5" />
          </Link>
        }
      />
      {items.length === 0 ? (
        <div className="flex flex-1 items-center justify-center px-4 py-10 text-center text-[13px] text-ink-4">
          {available ? t("Открытых задач со сроком пока нет") : t("Задачи появятся, когда будет доступ к разделу «Задачи»")}
        </div>
      ) : (
        <ul className="mt-2 flex flex-col">
          {items.map((task, index) => {
            const checked = done[task.id] ?? !!task.done;
            const p = priorityOf[task.priority];
            return (
              <li key={task.id} className={`flex items-start gap-3 py-3 ${index < items.length - 1 ? "border-b border-line" : ""}`}>
                <input
                  type="checkbox"
                  checked={checked}
                  onChange={(e) => setDone((d) => ({ ...d, [task.id]: e.target.checked }))}
                  aria-label={t("Выполнено: {title}", { title: task.title })}
                  className="mt-0.5 size-[18px] shrink-0 cursor-pointer appearance-none rounded-[5px] border-[1.5px] border-line-strong bg-transparent transition-colors duration-150 checked:border-accent checked:bg-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
                />
                <span className="min-w-0 flex-1">
                  <Link href={task.href} className={`block text-[13.5px] font-medium leading-snug hover:underline ${checked ? "text-ink-4 line-through" : "text-ink"}`}>
                    {task.title}
                  </Link>
                  <span className="mt-1 flex flex-wrap items-center gap-1.5 text-[11.5px]">
                    <span className="text-ink-4">{t(task.department)}</span>
                    <span className="size-[3px] rounded-full bg-ink-4" aria-hidden />
                    <span className={`inline-flex items-center gap-1 ${p.className}`}>
                      <Flag className="size-[11px]" strokeWidth={2} />
                      {t(p.label)}
                    </span>
                  </span>
                </span>
                <span className={`inline-flex shrink-0 items-center gap-1 rounded-md px-2 py-1 text-[11.5px] font-semibold ${dueChip[task.dueTone]}`}>
                  <Calendar className="size-3" strokeWidth={2} />
                  {task.due}
                </span>
                {task.assignee && <Avatar initials={task.assignee.initials} tone={task.assignee.tone} size={26} />}
              </li>
            );
          })}
        </ul>
      )}
    </DashCard>
  );
}
