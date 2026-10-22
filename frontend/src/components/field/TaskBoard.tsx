"use client";

import { formatDate } from "@/i18n/core";
import Link from "next/link";
import { useState } from "react";
import { Bot, CalendarClock, Plus, Store } from "lucide-react";
import { useLocale, useT } from "@/i18n/client";
import { actorLabel, priorityLabel, taskActionLabel, taskStatusLabel, taskTitle } from "@/lib/field/labels";
import type { FieldAgentOption, FieldPriority, FieldTask, FieldTaskStatus } from "@/lib/field/types";
import { Chip, Empty, buttonClass, inputClass } from "./ui";
import { Sheet } from "./Sheet";
import { companyToday, fieldApi, useAction } from "./hooks";

const dateLabel = (iso: string | null, locale: string) => (iso ? formatDate(new Date(`${iso}T00:00:00Z`), locale, { day: "numeric", month: "short", timeZone: "UTC" }) : null);

/** Список задач с действиями по статусу; переход «выполнено» спрашивает результат, «перенести» — дату. emptyText — по-русски, переводится здесь. */
export function TaskList({ tasks, emptyText = "Задач нет" }: { tasks: FieldTask[]; emptyText?: string }) {
  const t = useT();
  const locale = useLocale();
  const { busy, error, run } = useAction();
  const [dialog, setDialog] = useState<{ task: FieldTask; status: FieldTaskStatus } | null>(null);
  const [result, setResult] = useState("");
  const [postpone, setPostpone] = useState("");

  if (tasks.length === 0) return <Empty>{t(emptyText)}</Empty>;

  const change = (task: FieldTask, status: FieldTaskStatus) => {
    if (status === "Completed" || status === "Postponed" || (status === "InProgress" && task.status === "Completed")) {
      setResult("");
      setPostpone("");
      setDialog({ task, status });
      return;
    }
    if (status === "Cancelled" && !confirm(t("Отменить задачу «{title}»?", { title: taskTitle(t, task) }))) return;
    run(`${task.id}:${status}`, () => fieldApi.post(`tasks/${task.id}/status`, { status }));
  };

  return (
    <>
      {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      <ul className="overflow-hidden rounded-xl border border-line bg-surface">
        {tasks.map((task) => {
          const [statusText, statusTone] = taskStatusLabel[task.status];
          const [priorityText, priorityTone] = priorityLabel[task.priority];
          return (
            <li key={task.id} className="border-b border-line px-4 py-3 last:border-0">
              <div className="flex items-start gap-3">
                <div className="min-w-0 flex-1">
                  <Link href={`/field/tasks/${task.id}`} prefetch={false} className="text-sm font-medium text-ink hover:underline">
                    {taskTitle(t, task)}
                  </Link>
                  <div className="mt-1 flex flex-wrap items-center gap-1.5 text-xs text-ink-3">
                    <Chip tone={statusTone}>{t(statusText)}</Chip>
                    {task.priority !== "Medium" && <Chip tone={priorityTone}>{t(priorityText)}</Chip>}
                    {task.dueDate && (
                      <span className={`inline-flex items-center gap-1 ${task.overdue ? "font-medium text-bad" : ""}`}>
                        <CalendarClock className="size-3.5" />
                        {task.overdue ? `${t("просрочена")} · ` : ""}
                        {dateLabel(task.dueDate, locale)}
                      </span>
                    )}
                    {task.marketName && (
                      <Link href={`/field/customers/${task.marketId}`} prefetch={false} className="inline-flex max-w-[220px] items-center gap-1 truncate hover:text-ink">
                        <Store className="size-3.5 shrink-0" />
                        <span className="truncate">{task.marketName}</span>
                      </Link>
                    )}
                    <span className="inline-flex items-center gap-1">
                      {task.createdByType === "Ai" && <Bot className="size-3.5 text-accent-strong" />}
                      {task.assignedTo} · {t("от {who}", { who: t(task.createdBy ?? actorLabel[task.createdByType]) })}
                    </span>
                  </div>
                  {task.result && <div className="mt-1 text-xs text-ink-2">{t("Результат: {result}", { result: task.result })}</div>}
                </div>
              </div>
              {task.nextStatuses.length > 0 && (
                <div className="mt-2.5 flex flex-wrap gap-2">
                  {task.nextStatuses.map((s) => (
                    <button
                      key={s}
                      type="button"
                      disabled={busy !== null}
                      onClick={() => change(task, s)}
                      className={`rounded-lg px-3 py-2 text-xs font-medium lg:py-1.5 ${s === "Completed" || s === "Verified" ? "bg-ok text-white" : s === "Cancelled" ? "border border-bad/30 text-bad" : "border border-line text-ink-2 hover:bg-muted"}`}
                    >
                      {s === "InProgress" && task.status === "Completed" ? t("Вернуть в работу") : t(taskActionLabel[s])}
                    </button>
                  ))}
                </div>
              )}
            </li>
          );
        })}
      </ul>
      <Sheet
        open={dialog !== null}
        onClose={() => setDialog(null)}
        title={dialog?.status === "Postponed" ? t("Перенести задачу") : dialog?.status === "Completed" ? t("Задача выполнена") : t("Вернуть в работу")}
        footer={
          <button
            type="button"
            disabled={busy !== null || (dialog?.status === "Postponed" && !postpone)}
            onClick={() =>
              dialog &&
              run("dialog", () => fieldApi.post(`tasks/${dialog.task.id}/status`, { status: dialog.status, result: result || null, postponeTo: postpone || null }), { onDone: () => setDialog(null) })
            }
            className={`${buttonClass.primary} w-full`}
          >
            {busy === "dialog" ? t("Сохраняем…") : t("Сохранить")}
          </button>
        }
      >
        <p className="text-sm text-ink-2">{dialog && taskTitle(t, dialog.task)}</p>
        {dialog?.status === "Postponed" ? (
          <label className="mt-3 block">
            <span className="mb-1.5 block text-sm text-ink-2">{t("Новый срок")}</span>
            <input type="date" min={companyToday()} value={postpone} onChange={(e) => setPostpone(e.target.value)} className={inputClass} />
          </label>
        ) : null}
        <label className="mt-3 block">
          <span className="mb-1.5 block text-sm text-ink-2">{dialog?.status === "Completed" ? t("Что сделано") : t("Комментарий")}</span>
          <textarea value={result} onChange={(e) => setResult(e.target.value)} rows={3} className={`${inputClass} h-auto py-2.5`} />
        </label>
      </Sheet>
    </>
  );
}

/** Поставить задачу: исполнитель, точка (по id из карточки), приоритет, срок. label — по-русски, переводится здесь. */
export function NewTaskButton({ agents, marketId, marketName, defaultAgentId, label = "Задача" }: { agents: FieldAgentOption[]; marketId?: number; marketName?: string; defaultAgentId?: string | null; label?: string }) {
  const t = useT();
  const { busy, error, run } = useAction();
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [assignee, setAssignee] = useState(defaultAgentId ?? agents[0]?.id ?? "");
  const [priority, setPriority] = useState<FieldPriority>("Medium");
  const [due, setDue] = useState(companyToday());

  return (
    <>
      <button type="button" onClick={() => setOpen(true)} className={buttonClass.primary}>
        <Plus className="size-4" /> {t(label)}
      </button>
      <Sheet
        open={open}
        onClose={() => setOpen(false)}
        title={t("Новая задача")}
        footer={
          <button
            type="button"
            disabled={busy !== null || !title.trim()}
            onClick={() =>
              run(
                "create",
                () => fieldApi.post("tasks", { title, description: description || null, assignedToId: assignee || null, marketId: marketId ?? null, priority, dueDate: due || null }),
                {
                  onDone: () => {
                    setOpen(false);
                    setTitle("");
                    setDescription("");
                  },
                },
              )
            }
            className={`${buttonClass.primary} w-full`}
          >
            {busy === "create" ? t("Создаём…") : t("Поставить задачу")}
          </button>
        }
      >
        <div className="space-y-3">
          {marketName && <p className="text-sm text-ink-2">{t("Точка: {name}", { name: marketName })}</p>}
          <label className="block">
            <span className="mb-1.5 block text-sm text-ink-2">{t("Что сделать")}</span>
            <input value={title} onChange={(e) => setTitle(e.target.value)} maxLength={300} placeholder={t("Например, проверить выкладку Кекса")} className={inputClass} autoFocus />
          </label>
          <div className="flex flex-wrap gap-2">
            {/* Заголовок — свободный текст: быстрый выбор подставляет его на языке интерфейса. */}
            {["Посетить точку", "Проверить выкладку", "Собрать заказ", "Вернуть клиента", "Проверить остатки"].map((text) => (
              <button key={text} type="button" onClick={() => setTitle(t(text))} className="rounded-full bg-muted px-3 py-1.5 text-xs text-ink-2">
                {t(text)}
              </button>
            ))}
          </div>
          <label className="block">
            <span className="mb-1.5 block text-sm text-ink-2">{t("Подробности")}</span>
            <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={2} className={`${inputClass} h-auto py-2.5`} />
          </label>
          {agents.length > 0 && (
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">{t("Исполнитель")}</span>
              <select value={assignee} onChange={(e) => setAssignee(e.target.value)} className={inputClass}>
                {agents.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name}
                    {a.teamName ? ` · ${a.teamName}` : ""}
                  </option>
                ))}
              </select>
            </label>
          )}
          <div className="grid grid-cols-2 gap-3">
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">{t("Приоритет")}</span>
              <select value={priority} onChange={(e) => setPriority(e.target.value as FieldPriority)} className={inputClass}>
                {(["Low", "Medium", "High", "Urgent"] as FieldPriority[]).map((p) => (
                  <option key={p} value={p}>
                    {t(priorityLabel[p][0])}
                  </option>
                ))}
              </select>
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">{t("Срок")}</span>
              <input type="date" min={companyToday()} value={due} onChange={(e) => setDue(e.target.value)} className={inputClass} />
            </label>
          </div>
          {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
        </div>
      </Sheet>
    </>
  );
}
