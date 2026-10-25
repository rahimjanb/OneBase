"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { Ban, CalendarClock, Loader2, UserRound } from "lucide-react";
import { field, primary, button } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { needsComment, shortDate, statusActionLabel, workStatusLabel, type WorkTask, type WorkTaskStatus } from "@/lib/tasks";
import { ErrorNote, PriorityBadge, ProgressBar } from "./TaskBadges";

const columns: { status: WorkTaskStatus; label: string; tone: string }[] = [
  { status: "New", label: "Новые", tone: "bg-accent" },
  { status: "InProgress", label: "В работе", tone: "bg-warn" },
  { status: "Review", label: "На проверке", tone: "bg-violet" },
  { status: "Done", label: "Выполнены", tone: "bg-ok" },
];

/** Сколько выполненных показывать в колонке — остальные в «Архиве и завершённых». */
const DoneLimit = 30;

/**
 * Канбан: колонки «Новые», «В работе», «На проверке», «Выполнены»; заблокированные — красная метка с причиной на карточке. Те же задачи
 * и та же база, что у таблицы. Перетаскивание (на компьютере) или меню «Перевести» (на телефоне) меняет статус на сервере; разрешены только
 * переходы, которые сервер вернул для задачи (nextStatuses). Карточка переезжает сразу, ошибка сервера возвращает её назад с причиной.
 * Возврат на доработку и отмена спрашивают комментарий.
 */
export function TaskBoard({ items }: { items: WorkTask[] }) {
  const t = useT();
  const router = useRouter();
  const [tasks, setTasks] = useState(items);
  const [dragging, setDragging] = useState<string | null>(null);
  const [over, setOver] = useState<WorkTaskStatus | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [asking, setAsking] = useState<{ task: WorkTask; to: WorkTaskStatus } | null>(null);
  const [comment, setComment] = useState("");

  // Сервер прислал свежие данные (после действия или фильтра) — доска их показывает.
  useEffect(() => setTasks(items), [items]);

  const move = async (task: WorkTask, to: WorkTaskStatus, note?: string) => {
    if (task.status === to) return;
    if (!task.nextStatuses.includes(to)) {
      setError(t("«{title}»: перевести в «{status}» нельзя — нет прав или такой переход не предусмотрен.", { title: task.title, status: t(workStatusLabel[to]) }));
      return;
    }
    if (needsComment(task.status, to) && note === undefined) {
      setComment("");
      setAsking({ task, to });
      return;
    }
    const before = tasks;
    setError(null);
    setBusy(task.id);
    setTasks((list) => list.map((x) => (x.id === task.id ? { ...x, status: to, nextStatuses: [] } : x)));
    try {
      await bff(`work/tasks/${task.id}/status`, { method: "POST", body: JSON.stringify({ status: to, comment: note || null }) });
      router.refresh();
    } catch (e) {
      setTasks(before);
      setError(`«${task.title}»: ${(e as Error).message}`);
    } finally {
      setBusy(null);
    }
  };

  return (
    <>
      <ErrorNote text={error} />
      <div className="mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        {columns.map((col) => {
          const list = tasks
            .filter((x) => x.status === col.status)
            .sort((a, b) => (col.status === "Done" ? (b.completedAt ?? "").localeCompare(a.completedAt ?? "") : 0));
          const shown = col.status === "Done" ? list.slice(0, DoneLimit) : list;
          const draggedTask = tasks.find((x) => x.id === dragging);
          const allowed = !!draggedTask && draggedTask.nextStatuses.includes(col.status);
          return (
            <section
              key={col.status}
              aria-label={t(col.label)}
              onDragOver={(e) => {
                if (!dragging) return;
                e.preventDefault();
                e.dataTransfer.dropEffect = allowed ? "move" : "none";
                setOver(col.status);
              }}
              onDragLeave={() => setOver((o) => (o === col.status ? null : o))}
              onDrop={(e) => {
                e.preventDefault();
                setOver(null);
                const task = tasks.find((x) => x.id === e.dataTransfer.getData("text/plain"));
                setDragging(null);
                if (task) void move(task, col.status);
              }}
              className={`flex min-h-48 flex-col rounded-xl border bg-muted/40 transition-colors ${
                over === col.status ? (allowed ? "border-accent bg-accent-soft/40" : "border-bad/50") : "border-line"
              }`}
            >
              <header className="flex items-center gap-2 border-b border-line px-3 py-2.5">
                <span aria-hidden className={`size-2 rounded-full ${col.tone}`} />
                <h2 className="text-sm font-semibold text-ink">{t(col.label)}</h2>
                <span className="ml-auto rounded-full bg-surface px-2 text-xs tabular-nums text-ink-2">{list.length}</span>
              </header>
              <ul className="flex flex-1 flex-col gap-2 p-2">
                {shown.map((task) => (
                  <li
                    key={task.id}
                    draggable={task.nextStatuses.length > 0 && busy !== task.id}
                    onDragStart={(e) => {
                      e.dataTransfer.setData("text/plain", task.id);
                      e.dataTransfer.effectAllowed = "move";
                      setDragging(task.id);
                    }}
                    onDragEnd={() => {
                      setDragging(null);
                      setOver(null);
                    }}
                    className={`rounded-lg border bg-surface p-3 shadow-sm ${task.isBlocked ? "border-bad/50" : "border-line"} ${
                      task.nextStatuses.length > 0 ? "cursor-grab active:cursor-grabbing" : ""
                    } ${busy === task.id ? "opacity-60" : ""}`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <Link href={`/tasks/task/${task.id}`} className="text-sm font-medium text-ink hover:text-accent-strong">
                        {task.title}
                      </Link>
                      {busy === task.id && <Loader2 className="size-4 shrink-0 animate-spin text-ink-3" />}
                    </div>
                    {task.isBlocked && (
                      <p className="mt-1.5 flex items-start gap-1 text-xs text-bad">
                        <Ban className="mt-0.5 size-3 shrink-0" /> {task.blockedReason ?? t("Заблокирована")}
                      </p>
                    )}
                    <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-3">
                      <PriorityBadge priority={task.priority} />
                      <span className="inline-flex items-center gap-1">
                        <UserRound className="size-3" /> {task.assigneeName ?? t("не назначен")}
                      </span>
                      {task.dueDate && (
                        <span className={`inline-flex items-center gap-1 ${task.overdue ? "font-semibold text-bad" : ""}`}>
                          <CalendarClock className="size-3" /> {shortDate(task.dueDate)}
                        </span>
                      )}
                    </div>
                    <div className="mt-2 flex items-center justify-between gap-2">
                      <span className="truncate text-[11px] text-ink-3">{task.projectName ?? task.departmentName}</span>
                      <ProgressBar value={task.progress} compact />
                    </div>
                    {task.nextStatuses.length > 0 && (
                      <select
                        className={`${field} mt-2 h-8 text-xs md:hidden`}
                        value=""
                        onChange={(e) => e.target.value && void move(task, e.target.value as WorkTaskStatus)}
                        aria-label={t("Перевести задачу")}
                      >
                        <option value="">{t("Перевести…")}</option>
                        {task.nextStatuses.map((s) => (
                          <option key={s} value={s}>
                            {t(statusActionLabel(task.status, s))}
                          </option>
                        ))}
                      </select>
                    )}
                  </li>
                ))}
                {list.length === 0 && <li className="px-2 py-6 text-center text-xs text-ink-3">{t("Пусто")}</li>}
                {list.length > shown.length && (
                  <li className="px-2 py-1 text-center text-xs">
                    <Link href="/tasks/archive" className="text-accent-strong hover:underline">
                      {t("Ещё {n} — в «Архиве и завершённых»", { n: list.length - shown.length })}
                    </Link>
                  </li>
                )}
              </ul>
            </section>
          );
        })}
      </div>

      <Sheet
        open={asking !== null}
        onClose={() => setAsking(null)}
        title={asking ? t(statusActionLabel(asking.task.status, asking.to)) : ""}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={button} onClick={() => setAsking(null)}>
              {t("Отмена")}
            </button>
            <button
              type="button"
              className={primary}
              disabled={asking?.task.status === "Review" && asking.to === "InProgress" && !comment.trim()}
              onClick={() => {
                const a = asking;
                setAsking(null);
                if (a) void move(a.task, a.to, comment.trim());
              }}
            >
              {t("Подтвердить")}
            </button>
          </div>
        }
      >
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {asking?.task.status === "Review" && asking.to === "InProgress" ? t("Что доработать") : t("Комментарий (необязательно)")}
          </span>
          <textarea className={`${field} h-auto min-h-24 py-2`} value={comment} onChange={(e) => setComment(e.target.value)} maxLength={4000} autoFocus />
        </label>
      </Sheet>
    </>
  );
}
