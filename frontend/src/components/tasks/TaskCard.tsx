"use client";

import { useRouter } from "next/navigation";
import { useRef, useState } from "react";
import { Archive, ArchiveRestore, Ban, Check, Download, History, Loader2, MessageSquare, Paperclip, Pencil, Plus, Trash2, Unlock } from "lucide-react";
import { button, danger, field, primary } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { dateTime } from "@/lib/sales/format";
import { fileSize, needsComment, shortDate, statusActionLabel, type DepartmentTasks, type WorkProject, type WorkTaskDetail, type WorkTaskStatus } from "@/lib/tasks";
import { ErrorNote, PriorityBadge, ProgressBar, StatusBadge, TaskFlags } from "./TaskBadges";
import { TaskForm } from "./TaskForm";

const card = "rounded-xl border border-line bg-surface p-4 sm:p-5";
const heading = "mb-3 flex items-center gap-2 text-sm font-semibold text-ink";

/**
 * Карточка задачи: что, кто, сроки, статус и процент; действия — только те, что разрешил сервер (nextStatuses, canManage, canWork):
 * в работу, на проверку, принять, вернуть на доработку (с причиной), отменить, вернуть в работу, архив; правка и перенос срока —
 * постановщику и руководителю. Чек-лист считает процент; комментарии, вложения и история изменений — ниже.
 */
export function TaskCard({ detail, departments, projects }: { detail: WorkTaskDetail; departments: DepartmentTasks[]; projects: WorkProject[] }) {
  const t = useT();
  const router = useRouter();
  const task = detail.task;
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const [asking, setAsking] = useState<WorkTaskStatus | null>(null);
  const [note, setNote] = useState("");
  const [blocking, setBlocking] = useState(false);
  const [blockReason, setBlockReason] = useState("");
  const [item, setItem] = useState("");
  const [comment, setComment] = useState("");
  const [progress, setProgress] = useState(task.progress);
  const fileInput = useRef<HTMLInputElement>(null);

  const run = async (key: string, fn: () => Promise<unknown>) => {
    setBusy(key);
    setError(null);
    try {
      await fn();
      router.refresh();
      return true;
    } catch (e) {
      setError((e as Error).message);
      return false;
    } finally {
      setBusy(null);
    }
  };

  const post = (path: string, body?: unknown) => bff(`work/${path}`, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });

  const changeStatus = (to: WorkTaskStatus, text?: string) => {
    if (needsComment(task.status, to) && text === undefined) {
      setNote("");
      setAsking(to);
      return;
    }
    void run(`status-${to}`, () => post(`tasks/${task.id}/status`, { status: to, comment: text || null }));
  };

  const upload = async () => {
    const file = fileInput.current?.files?.[0];
    if (!file) return;
    if (file.size > 20 * 1024 * 1024) {
      setError(t("Файл больше {n} МБ.", { n: 20 }));
      return;
    }
    const form = new FormData();
    form.append("file", file);
    await run("upload", async () => {
      // multipart — без Content-Type: браузер сам ставит границу.
      const response = await fetch(`/bff/api/work/tasks/${task.id}/attachments`, { method: "POST", body: form });
      if (!response.ok) {
        const json = await response.json().catch(() => ({}));
        throw new Error(json.error ? t(json.error) : t("Ошибка {status}", { status: response.status }));
      }
    });
    if (fileInput.current) fileInput.current.value = "";
  };

  const returning = asking !== null && task.status === "Review" && asking === "InProgress";

  return (
    <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_340px]">
      <div className="space-y-4">
        <ErrorNote text={error} />
        <section className={card}>
          <div className="flex flex-wrap items-center gap-2">
            <StatusBadge status={task.status} />
            <PriorityBadge priority={task.priority} />
            <TaskFlags task={task} />
            {task.archivedAt && <span className="rounded-md bg-muted px-1.5 py-0.5 text-[11px] font-semibold text-ink-3">{t("в архиве")}</span>}
          </div>
          <h1 className="mt-3 text-xl font-semibold text-ink">{task.title}</h1>
          {task.description ? (
            <p className="mt-2 whitespace-pre-wrap text-sm leading-relaxed text-ink-2">{task.description}</p>
          ) : (
            <p className="mt-2 text-sm text-ink-3">{t("Без описания")}</p>
          )}
          {task.isBlocked && (
            <p className="mt-3 flex items-start gap-2 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">
              <Ban className="mt-0.5 size-4 shrink-0" /> {t("Работа заблокирована: {reason}", { reason: task.blockedReason ?? "" })}
            </p>
          )}
          {task.result && <p className="mt-3 rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{t("Итог: {text}", { text: task.result })}</p>}

          <div className="mt-4 flex flex-wrap gap-2">
            {task.nextStatuses.map((s) => (
              <button
                key={s}
                type="button"
                className={s === "Cancelled" ? danger : s === "Done" || s === "Review" ? primary : button}
                disabled={busy !== null}
                onClick={() => changeStatus(s)}
              >
                {busy === `status-${s}` && <Loader2 className="size-4 animate-spin" />}
                {t(statusActionLabel(task.status, s))}
              </button>
            ))}
            {task.canManage && !task.archivedAt && (
              <button type="button" className={button} onClick={() => setEditing(true)} disabled={busy !== null}>
                <Pencil className="size-4" /> {t("Изменить")}
              </button>
            )}
            {task.canWork && !task.archivedAt && ["New", "InProgress", "Review"].includes(task.status) &&
              (task.isBlocked ? (
                <button type="button" className={button} disabled={busy !== null} onClick={() => void run("unblock", () => post(`tasks/${task.id}/block`, { blocked: false }))}>
                  <Unlock className="size-4" /> {t("Снять блокировку")}
                </button>
              ) : (
                <button type="button" className={button} disabled={busy !== null} onClick={() => (setBlockReason(""), setBlocking(true))}>
                  <Ban className="size-4" /> {t("Заблокировано")}
                </button>
              ))}
            {detail.canArchive && (
              <button type="button" className={button} disabled={busy !== null} onClick={() => void run("archive", () => post(`tasks/${task.id}/archive`))}>
                <Archive className="size-4" /> {t("В архив")}
              </button>
            )}
            {detail.canRestore && (
              <button type="button" className={button} disabled={busy !== null} onClick={() => void run("restore", () => post(`tasks/${task.id}/restore`))}>
                <ArchiveRestore className="size-4" /> {t("Вернуть из архива")}
              </button>
            )}
          </div>
        </section>

        <section className={card}>
          <h2 className={heading}>
            <Check className="size-4" /> {t("Чек-лист")}
            {task.checklistTotal > 0 && <span className="text-xs font-normal text-ink-3">{t("{done} из {total}", { done: task.checklistDone, total: task.checklistTotal })}</span>}
          </h2>
          <ul className="space-y-1.5">
            {detail.checklist.map((c) => (
              <li key={c.id} className="group flex items-center gap-2.5">
                <input
                  type="checkbox"
                  checked={c.isDone}
                  disabled={!task.canWork || !!task.archivedAt || busy !== null}
                  onChange={(e) => void run(`item-${c.id}`, () => bff(`work/tasks/${task.id}/checklist/${c.id}`, { method: "PUT", body: JSON.stringify({ done: e.target.checked }) }))}
                  className="size-4 accent-[var(--color-accent)]"
                  aria-label={c.title}
                />
                <span className={`flex-1 text-sm ${c.isDone ? "text-ink-3 line-through" : "text-ink"}`}>{c.title}</span>
                {task.canWork && !task.archivedAt && (
                  <button
                    type="button"
                    onClick={() => void run(`del-${c.id}`, () => bff(`work/tasks/${task.id}/checklist/${c.id}`, { method: "DELETE" }))}
                    className="rounded p-1 text-ink-3 opacity-0 hover:text-bad focus:opacity-100 group-hover:opacity-100"
                    aria-label={t("Удалить пункт")}
                  >
                    <Trash2 className="size-3.5" />
                  </button>
                )}
              </li>
            ))}
            {detail.checklist.length === 0 && <li className="text-sm text-ink-3">{t("Пунктов нет — процент выполнения указывается вручную.")}</li>}
          </ul>
          {task.canWork && !task.archivedAt && (
            <form
              className="mt-3 flex gap-2"
              onSubmit={async (e) => {
                e.preventDefault();
                if (!item.trim()) return;
                if (await run("item-add", () => post(`tasks/${task.id}/checklist`, { title: item.trim() }))) setItem("");
              }}
            >
              <input className={field} value={item} onChange={(e) => setItem(e.target.value)} maxLength={300} placeholder={t("Новый пункт")} aria-label={t("Новый пункт")} />
              <button type="submit" className={button} disabled={busy !== null || !item.trim()}>
                <Plus className="size-4" /> {t("Добавить")}
              </button>
            </form>
          )}
        </section>

        <section className={card}>
          <h2 className={heading}>
            <MessageSquare className="size-4" /> {t("Комментарии")}
          </h2>
          <ul className="space-y-3">
            {detail.comments.map((c) => (
              <li key={c.id} className="rounded-lg bg-muted/60 px-3 py-2">
                <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-ink-3">
                  <span className="font-semibold text-ink-2">{c.authorName}</span>
                  <span>{dateTime(c.createdAt)}</span>
                </div>
                <p className="mt-1 whitespace-pre-wrap text-sm text-ink">{c.text}</p>
              </li>
            ))}
            {detail.comments.length === 0 && <li className="text-sm text-ink-3">{t("Комментариев пока нет")}</li>}
          </ul>
          {detail.canComment && (
            <form
              className="mt-3 space-y-2"
              onSubmit={async (e) => {
                e.preventDefault();
                if (!comment.trim()) return;
                if (await run("comment", () => post(`tasks/${task.id}/comments`, { text: comment.trim() }))) setComment("");
              }}
            >
              <textarea className={`${field} h-auto min-h-20 py-2`} value={comment} onChange={(e) => setComment(e.target.value)} maxLength={4000} placeholder={t("Написать комментарий")} aria-label={t("Комментарий")} />
              <button type="submit" className={button} disabled={busy !== null || !comment.trim()}>
                {busy === "comment" && <Loader2 className="size-4 animate-spin" />}
                {t("Отправить")}
              </button>
            </form>
          )}
        </section>

        <section className={card}>
          <h2 className={heading}>
            <History className="size-4" /> {t("История изменений")}
          </h2>
          <ol className="space-y-2 text-sm">
            {detail.history.map((h) => (
              <li key={h.id} className="flex flex-wrap gap-x-2 border-l-2 border-line pl-3">
                <span className="text-ink">{h.text}</span>
                <span className="text-xs text-ink-3">
                  {h.actorName} · {dateTime(h.createdAt)}
                </span>
              </li>
            ))}
          </ol>
        </section>
      </div>

      <aside className="space-y-4">
        <section className={card}>
          <dl className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-3 gap-y-2 text-sm">
            <dt className="text-ink-3">{t("Отдел")}</dt>
            <dd className="text-ink">{task.departmentName}</dd>
            <dt className="text-ink-3">{t("Проект")}</dt>
            <dd className="text-ink">{task.projectName ?? "—"}</dd>
            <dt className="text-ink-3">{t("Постановщик")}</dt>
            <dd className="text-ink">{task.createdByName ?? "—"}</dd>
            <dt className="text-ink-3">{t("Исполнитель")}</dt>
            <dd className="text-ink">{task.assigneeName ?? t("любой сотрудник отдела")}</dd>
            <dt className="text-ink-3">{t("Создана")}</dt>
            <dd className="text-ink">{dateTime(task.createdAt)}</dd>
            <dt className="text-ink-3">{t("Начало")}</dt>
            <dd className="text-ink">{task.startDate ? shortDate(task.startDate) : "—"}</dd>
            <dt className="text-ink-3">{t("Срок")}</dt>
            <dd className={task.overdue ? "font-semibold text-bad" : "text-ink"}>{task.dueDate ? shortDate(task.dueDate) : "—"}</dd>
            {task.completedAt && (
              <>
                <dt className="text-ink-3">{t("Выполнена")}</dt>
                <dd className="text-ink">{dateTime(task.completedAt)}</dd>
              </>
            )}
          </dl>
          <div className="mt-4">
            <div className="mb-1.5 text-sm font-medium text-ink">{t("Выполнение")}</div>
            <ProgressBar value={task.progress} />
            {task.canWork && !task.archivedAt && task.checklistTotal === 0 && ["New", "InProgress", "Review"].includes(task.status) && (
              <div className="mt-2 flex items-center gap-2">
                <input type="range" min={0} max={100} step={5} value={progress} onChange={(e) => setProgress(Number(e.target.value))} className="flex-1 accent-[var(--color-accent)]" aria-label={t("Процент выполнения")} />
                <button
                  type="button"
                  className={button}
                  disabled={busy !== null || progress === task.progress}
                  onClick={() => void run("progress", () => post(`tasks/${task.id}/progress`, { progress }))}
                >
                  {t("{n}% — сохранить", { n: progress })}
                </button>
              </div>
            )}
          </div>
        </section>

        <section className={card}>
          <h2 className={heading}>
            <Paperclip className="size-4" /> {t("Вложения")}
          </h2>
          <ul className="space-y-2">
            {detail.attachments.map((a) => (
              <li key={a.id} className="flex items-center gap-2 text-sm">
                <a href={`/bff/api/work/attachments/${a.id}`} className="flex min-w-0 flex-1 items-center gap-2 text-ink hover:text-accent-strong">
                  <Download className="size-4 shrink-0 text-ink-3" />
                  <span className="truncate">{a.fileName}</span>
                  <span className="shrink-0 text-xs text-ink-3">{fileSize(a.sizeBytes)}</span>
                </a>
                {a.canDelete && (
                  <button
                    type="button"
                    onClick={() => void run(`file-${a.id}`, () => bff(`work/attachments/${a.id}`, { method: "DELETE" }))}
                    className="rounded p-1 text-ink-3 hover:text-bad"
                    aria-label={t("Удалить {name}", { name: a.fileName })}
                  >
                    <Trash2 className="size-3.5" />
                  </button>
                )}
              </li>
            ))}
            {detail.attachments.length === 0 && <li className="text-sm text-ink-3">{t("Вложений нет")}</li>}
          </ul>
          {detail.canAttach && !task.archivedAt && (
            <div className="mt-3">
              <input ref={fileInput} type="file" className="sr-only" id={`file-${task.id}`} onChange={() => void upload()} />
              <label htmlFor={`file-${task.id}`} className={`${button} cursor-pointer`}>
                {busy === "upload" ? <Loader2 className="size-4 animate-spin" /> : <Paperclip className="size-4" />}
                {t("Прикрепить файл")}
              </label>
              <p className="mt-1.5 text-xs text-ink-3">{t("До 20 МБ. Файл видят все, кто видит задачу.")}</p>
            </div>
          )}
        </section>
      </aside>

      {editing && <TaskForm open={editing} onClose={() => setEditing(false)} departments={departments} projects={projects} task={task} />}

      <Sheet
        open={asking !== null}
        onClose={() => setAsking(null)}
        title={asking ? t(statusActionLabel(task.status, asking)) : ""}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={button} onClick={() => setAsking(null)}>
              {t("Отмена")}
            </button>
            <button
              type="button"
              className={primary}
              disabled={returning && !note.trim()}
              onClick={() => {
                const to = asking;
                setAsking(null);
                if (to) changeStatus(to, note.trim());
              }}
            >
              {t("Подтвердить")}
            </button>
          </div>
        }
      >
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{returning ? t("Что доработать") : t("Комментарий (необязательно)")}</span>
          <textarea className={`${field} h-auto min-h-24 py-2`} value={note} onChange={(e) => setNote(e.target.value)} maxLength={4000} autoFocus />
        </label>
      </Sheet>

      <Sheet
        open={blocking}
        onClose={() => setBlocking(false)}
        title={t("Работа заблокирована")}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={button} onClick={() => setBlocking(false)}>
              {t("Отмена")}
            </button>
            <button
              type="button"
              className={primary}
              disabled={!blockReason.trim()}
              onClick={async () => {
                if (await run("block", () => post(`tasks/${task.id}/block`, { blocked: true, reason: blockReason.trim() }))) setBlocking(false);
              }}
            >
              {t("Заблокировать")}
            </button>
          </div>
        }
      >
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Что мешает работе")}</span>
          <textarea className={`${field} h-auto min-h-20 py-2`} value={blockReason} onChange={(e) => setBlockReason(e.target.value)} maxLength={500} autoFocus placeholder={t("Кого или что ждёт задача")} />
        </label>
      </Sheet>
    </div>
  );
}
