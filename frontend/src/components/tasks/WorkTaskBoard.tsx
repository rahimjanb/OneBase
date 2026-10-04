"use client";

import { useState } from "react";
import { CalendarDays, Pencil, Plus, UserRound } from "lucide-react";
import { field as inputClass, primary as primaryClass, button as outlineClass } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { useAction } from "@/components/field/hooks";
import { bff } from "@/lib/bff";
import {
  shortDate,
  workPriorityLabel,
  workPriorityTone,
  workStatusAction,
  workStatusLabel,
  workStatusTone,
  type WorkAssignee,
  type WorkTask,
  type WorkTaskPriority,
  type WorkTaskStatus,
} from "@/lib/tasks";

type Draft = { id?: string; title: string; description: string; assigneeId: string; assigneeName?: string; priority: WorkTaskPriority; dueDate: string };

const emptyDraft: Draft = { title: "", description: "", assigneeId: "", priority: "Medium", dueDate: "" };

/**
 * Задачи отдела: список с действиями по статусу, новая задача и правка. Исполнитель — сотрудник отдела или «любой сотрудник»
 * (тогда задачу берёт тот, кто начнёт её). После каждого действия страница перечитывается с сервера.
 */
export function WorkTaskBoard({ department, items, assignees, today }: { department: string; items: WorkTask[]; assignees: WorkAssignee[]; today: string }) {
  const { busy, error, setError, run } = useAction();
  const [draft, setDraft] = useState<Draft | null>(null);
  const [closing, setClosing] = useState<{ task: WorkTask; status: WorkTaskStatus } | null>(null);
  const [result, setResult] = useState("");

  const save = () => {
    if (!draft) return;
    const body = {
      title: draft.title,
      description: draft.description || null,
      assigneeId: draft.assigneeId || null,
      priority: draft.priority,
      dueDate: draft.dueDate || null,
    };
    run(
      "save",
      () =>
        draft.id
          ? bff(`work/tasks/${draft.id}`, { method: "PUT", body: JSON.stringify(body) })
          : bff("work/tasks", { method: "POST", body: JSON.stringify({ department, ...body }) }),
      { onDone: () => setDraft(null) },
    );
  };

  const changeStatus = (task: WorkTask, status: WorkTaskStatus, text?: string) =>
    run(`status-${task.id}`, () => bff(`work/tasks/${task.id}/status`, { method: "POST", body: JSON.stringify({ status, result: text || null }) }), {
      onDone: () => {
        setClosing(null);
        setResult("");
      },
    });

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <button
          type="button"
          onClick={() => {
            setError(null);
            setDraft({ ...emptyDraft });
          }}
          className={primaryClass}
        >
          <Plus className="size-4" /> Новая задача
        </button>
        {assignees.length === 0 && <span className="text-xs text-ink-3">В отделе пока нет сотрудников с входом — задачу возьмёт любой, кто откроет отдел.</span>}
      </div>

      {error && !draft && !closing && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

      {items.length === 0 ? (
        <div className="rounded-xl border border-dashed border-line bg-surface/60 px-6 py-10 text-center text-sm text-ink-3">Задач нет</div>
      ) : (
        <ul className="space-y-2">
          {items.map((t) => (
            <li key={t.id} className="rounded-xl border border-line bg-surface p-4">
              <div className="flex flex-wrap items-start gap-x-3 gap-y-1.5">
                <div className="min-w-0 flex-1">
                  <div className="font-medium text-ink">{t.title}</div>
                  {t.description && <p className="mt-1 whitespace-pre-line text-sm text-ink-2">{t.description}</p>}
                </div>
                <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${workStatusTone[t.status]}`}>{workStatusLabel[t.status]}</span>
              </div>
              <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-ink-3">
                <span className="inline-flex items-center gap-1">
                  <UserRound className="size-3.5" /> {t.assigneeName ?? "любой сотрудник отдела"}
                </span>
                {t.dueDate && (
                  <span className={`inline-flex items-center gap-1 ${t.overdue ? "font-medium text-bad" : ""}`}>
                    <CalendarDays className="size-3.5" /> {t.overdue ? "просрочена, срок " : "до "}
                    {shortDate(t.dueDate)}
                  </span>
                )}
                <span className={workPriorityTone[t.priority]}>{workPriorityLabel[t.priority]}</span>
                {t.createdByName && <span>от {t.createdByName}</span>}
                {t.result && <span className="text-ink-2">итог: {t.result}</span>}
              </div>
              {(t.nextStatuses.length > 0 || t.canEdit) && (
                <div className="mt-3 flex flex-wrap gap-2">
                  {t.nextStatuses.map((s) => (
                    <button
                      key={s}
                      type="button"
                      disabled={busy !== null}
                      onClick={() => (s === "Done" || s === "Cancelled" ? (setError(null), setResult(""), setClosing({ task: t, status: s })) : changeStatus(t, s))}
                      className={`${s === "Done" ? primaryClass : outlineClass} h-9`}
                    >
                      {busy === `status-${t.id}` ? "Сохраняем…" : workStatusAction[s]}
                    </button>
                  ))}
                  {t.canEdit && (t.status === "New" || t.status === "InProgress") && (
                    <button
                      type="button"
                      onClick={() => {
                        setError(null);
                        setDraft({
                          id: t.id,
                          title: t.title,
                          description: t.description ?? "",
                          assigneeId: t.assigneeId ?? "",
                          assigneeName: t.assigneeName ?? undefined,
                          priority: t.priority,
                          dueDate: t.dueDate ?? "",
                        });
                      }}
                      className={`${outlineClass} h-9`}
                    >
                      <Pencil className="size-4" /> Изменить
                    </button>
                  )}
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      <Sheet
        open={draft !== null}
        onClose={() => setDraft(null)}
        title={draft?.id ? "Изменить задачу" : "Новая задача отдела"}
        footer={
          <div className="flex gap-2">
            <button type="button" disabled={busy !== null || !draft?.title.trim()} onClick={save} className={`${primaryClass} h-11 flex-1 justify-center`}>
              {busy === "save" ? "Сохраняем…" : draft?.id ? "Сохранить" : "Поставить задачу"}
            </button>
            <button type="button" onClick={() => setDraft(null)} className={`${outlineClass} h-11`}>
              Отмена
            </button>
          </div>
        }
      >
        {draft && (
          <div className="space-y-3">
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Что сделать</span>
              <input value={draft.title} onChange={(e) => setDraft({ ...draft, title: e.target.value })} maxLength={300} autoFocus className={inputClass} />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Подробности</span>
              <textarea value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} rows={4} maxLength={4000} className={`${inputClass} h-auto py-2`} />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Исполнитель</span>
              <select value={draft.assigneeId} onChange={(e) => setDraft({ ...draft, assigneeId: e.target.value })} className={inputClass}>
                <option value="">Любой сотрудник отдела</option>
                {/* Исполнитель ушёл из отдела: показываем его, чтобы было видно, за кем задача, и можно было передать её другому. */}
                {draft.assigneeId && !assignees.some((a) => a.id === draft.assigneeId) && (
                  <option value={draft.assigneeId}>{draft.assigneeName ?? "Прежний исполнитель"} · уже не в отделе</option>
                )}
                {assignees.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name}
                    {a.position ? ` · ${a.position}` : ""}
                  </option>
                ))}
              </select>
            </label>
            <div className="grid grid-cols-2 gap-3">
              <label className="block">
                <span className="mb-1.5 block text-sm text-ink-2">Приоритет</span>
                <select value={draft.priority} onChange={(e) => setDraft({ ...draft, priority: e.target.value as WorkTaskPriority })} className={inputClass}>
                  {(["Urgent", "High", "Medium", "Low"] as WorkTaskPriority[]).map((p) => (
                    <option key={p} value={p}>
                      {workPriorityLabel[p]}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block">
                <span className="mb-1.5 block text-sm text-ink-2">Срок</span>
                <input type="date" value={draft.dueDate} min={draft.id ? undefined : today} onChange={(e) => setDraft({ ...draft, dueDate: e.target.value })} className={inputClass} />
              </label>
            </div>
            {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
          </div>
        )}
      </Sheet>

      <Sheet
        open={closing !== null}
        onClose={() => setClosing(null)}
        title={closing?.status === "Done" ? "Задача выполнена" : "Отменить задачу"}
        footer={
          <div className="flex gap-2">
            <button
              type="button"
              disabled={busy !== null}
              onClick={() => closing && changeStatus(closing.task, closing.status, result)}
              className={`${closing?.status === "Done" ? primaryClass : outlineClass} h-11 flex-1 justify-center`}
            >
              {busy ? "Сохраняем…" : closing?.status === "Done" ? "Выполнена" : "Отменить задачу"}
            </button>
            <button type="button" onClick={() => setClosing(null)} className={`${outlineClass} h-11`}>
              Назад
            </button>
          </div>
        }
      >
        {closing && (
          <div className="space-y-3">
            <p className="text-sm text-ink">{closing.task.title}</p>
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">{closing.status === "Done" ? "Итог (необязательно)" : "Причина (необязательно)"}</span>
              <textarea value={result} onChange={(e) => setResult(e.target.value)} rows={3} maxLength={2000} className={`${inputClass} h-auto py-2`} />
            </label>
            {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
          </div>
        )}
      </Sheet>
    </div>
  );
}
