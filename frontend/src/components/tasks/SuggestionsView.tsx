"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { AlertTriangle, Check, ExternalLink, Loader2, Sparkles, X } from "lucide-react";
import { button, danger, field, primary } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { dateTime } from "@/lib/sales/format";
import {
  shortDate,
  suggestionSourceLabel,
  todayInTashkent,
  workPriorities,
  workPriorityLabel,
  type WorkAssignee,
  type WorkSuggestion,
  type WorkSuggestionRun,
  type WorkTaskPriority,
} from "@/lib/tasks";
import { ErrorNote, PriorityBadge } from "./TaskBadges";
import { AssigneePicker } from "./AssigneePicker";

type Edit = { title: string; problem: string; rationale: string; assigneeId: string; priority: WorkTaskPriority; dueDate: string };

const label = "mb-1.5 block text-sm font-medium text-ink";

/**
 * Предложения AI-диспетчера: «Найти задачи» ищет поводы в данных OneBase по отделам руководителя; каждое предложение ждёт решения —
 * подтвердить (можно поправить название, исполнителя, приоритет, срок) или отклонить с причиной. Задача создаётся только при подтверждении.
 */
export function SuggestionsView({ initial }: { initial: WorkSuggestion[] }) {
  const t = useT();
  const router = useRouter();
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [run, setRun] = useState<WorkSuggestionRun | null>(null);
  const [accepting, setAccepting] = useState<WorkSuggestion | null>(null);
  const [edit, setEdit] = useState<Edit | null>(null);
  const [assignees, setAssignees] = useState<WorkAssignee[]>([]);
  const [rejecting, setRejecting] = useState<WorkSuggestion | null>(null);
  const [reason, setReason] = useState("");
  const pending = initial.filter((s) => s.status === "Pending");
  const decided = initial.filter((s) => s.status !== "Pending");

  useEffect(() => {
    if (!accepting) return;
    let alive = true;
    bff<WorkAssignee[]>(`work/assignees?department=${encodeURIComponent(accepting.departmentCode)}`)
      .then((list) => alive && setAssignees(list))
      .catch(() => alive && setAssignees([]));
    return () => {
      alive = false;
    };
  }, [accepting]);

  const generate = async () => {
    setBusy("generate");
    setError(null);
    try {
      setRun(await bff<WorkSuggestionRun>("work/suggestions/generate", { method: "POST" }));
      router.refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  const openAccept = (s: WorkSuggestion) => {
    setAccepting(s);
    setEdit({ title: s.title, problem: s.problem, rationale: s.rationale, assigneeId: s.assigneeId ?? "", priority: s.priority, dueDate: s.dueDate ?? "" });
  };

  const accept = async () => {
    if (!accepting || !edit) return;
    setBusy("accept");
    setError(null);
    try {
      const result = await bff<{ taskId: string }>(`work/suggestions/${accepting.id}/accept`, {
        method: "POST",
        body: JSON.stringify({
          title: edit.title.trim(),
          problem: edit.problem.trim(),
          rationale: edit.rationale.trim(),
          assigneeId: edit.assigneeId || null,
          clearAssignee: !edit.assigneeId,
          priority: edit.priority,
          dueDate: edit.dueDate || null,
          clearDueDate: !edit.dueDate,
        }),
      });
      setAccepting(null);
      router.push(`/tasks/task/${result.taskId}`);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  const reject = async () => {
    if (!rejecting) return;
    setBusy("reject");
    setError(null);
    try {
      await bff(`work/suggestions/${rejecting.id}/reject`, { method: "POST", body: JSON.stringify({ note: reason.trim() || null }) });
      setRejecting(null);
      router.refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-3 rounded-xl border border-line bg-surface p-4">
        <span className="grid size-10 place-items-center rounded-xl bg-violet/15 text-violet-light">
          <Sparkles className="size-5" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="font-semibold text-ink">{t("AI-диспетчер задач")}</p>
          <p className="text-sm text-ink-3">
            {t("Ищет поводы в данных OneBase: находки анализа продаж, плана и остатков, просрочки, задачи на проверке. Задачи не создаёт сам — только предлагает вам.")}
          </p>
        </div>
        <button type="button" className={primary} onClick={generate} disabled={busy !== null}>
          {busy === "generate" ? <Loader2 className="size-4 animate-spin" /> : <Sparkles className="size-4" />}
          {t("Найти задачи")}
        </button>
      </div>
      {run && (
        <p className="mb-4 rounded-lg bg-muted px-4 py-3 text-sm text-ink-2">
          {run.created > 0 ? t("Новых предложений: {n}.", { n: run.created }) : t("Новых предложений нет.")}{" "}
          {run.duplicates > 0 && t("Уже предлагалось раньше: {n}.", { n: run.duplicates })} {run.note && t(run.note)}
        </p>
      )}
      <ErrorNote text={error} />

      <h2 className="mb-2 mt-2 text-sm font-semibold text-ink">{t("Ожидают подтверждения")}</h2>
      {pending.length === 0 && <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-8 text-center text-sm text-ink-3">{t("Предложений, ждущих решения, нет.")}</p>}
      <div className="grid gap-3 lg:grid-cols-2">
        {pending.map((s) => (
          <article key={s.id} className="flex flex-col rounded-xl border border-line bg-surface p-4">
            <div className="flex flex-wrap items-center gap-2 text-xs">
              <span className="rounded-full bg-warn-soft px-2 py-0.5 font-semibold text-warn">{t("Ожидает подтверждения")}</span>
              <span className="text-ink-3">{t(suggestionSourceLabel[s.sourceKind] ?? s.sourceKind)}</span>
              <span className="text-ink-3">· {s.departmentName}</span>
              <PriorityBadge priority={s.priority} />
            </div>
            <h3 className="mt-2 font-semibold text-ink">{s.title}</h3>
            <p className="mt-1 text-sm text-ink-2">{s.problem}</p>
            <div className="mt-2 rounded-lg bg-muted/60 px-3 py-2 text-sm text-ink-2">
              <span className="font-medium text-ink">{t("Обоснование по данным:")}</span> {s.rationale}
            </div>
            {s.insufficientData && (
              <p className="mt-2 flex items-start gap-1.5 text-sm text-warn">
                <AlertTriangle className="mt-0.5 size-4 shrink-0" /> {t("Данных недостаточно для уверенного предложения — проверьте перед подтверждением.")}
              </p>
            )}
            <dl className="mt-3 grid grid-cols-[auto_minmax(0,1fr)] gap-x-3 gap-y-1 text-xs">
              <dt className="text-ink-3">{t("Исполнитель")}</dt>
              <dd className="text-ink">{s.assigneeName ?? t("не предложен — выберите при подтверждении")}</dd>
              <dt className="text-ink-3">{t("Срок")}</dt>
              <dd className="text-ink">{s.dueDate ? shortDate(s.dueDate) : t("не предложен")}</dd>
              <dt className="text-ink-3">{t("Сформулировал")}</dt>
              <dd className="text-ink">{s.model ?? t("правило по данным (без модели)")}</dd>
            </dl>
            <details className="mt-2 text-xs text-ink-3">
              <summary className="cursor-pointer hover:text-ink">{t("Факты источника")}</summary>
              <p className="mt-1 whitespace-pre-wrap">{s.facts}</p>
            </details>
            <div className="mt-3 flex flex-wrap items-center gap-2 pt-1">
              <button type="button" className={primary} onClick={() => openAccept(s)} disabled={busy !== null}>
                <Check className="size-4" /> {t("Подтвердить")}
              </button>
              <button type="button" className={danger} onClick={() => (setReason(""), setRejecting(s))} disabled={busy !== null}>
                <X className="size-4" /> {t("Отклонить")}
              </button>
              {s.sourceHref && (
                <Link href={s.sourceHref} className="ml-auto inline-flex items-center gap-1 text-sm text-accent-strong hover:underline">
                  {t("Данные")} <ExternalLink className="size-3.5" />
                </Link>
              )}
            </div>
          </article>
        ))}
      </div>

      {decided.length > 0 && (
        <>
          <h2 className="mb-2 mt-6 text-sm font-semibold text-ink">{t("Решения")}</h2>
          <ul className="divide-y divide-line rounded-xl border border-line bg-surface">
            {decided.map((s) => (
              <li key={s.id} className="flex flex-wrap items-center gap-x-3 gap-y-1 px-4 py-2.5 text-sm">
                <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${s.status === "Accepted" ? "bg-ok-soft text-ok" : "bg-muted text-ink-3"}`}>
                  {s.status === "Accepted" ? t("Подтверждено") : t("Отклонено")}
                </span>
                <span className="min-w-0 flex-1 text-ink">{s.title}</span>
                <span className="text-xs text-ink-3">
                  {s.decidedByName} · {s.decidedAt ? dateTime(s.decidedAt) : ""}
                  {s.decisionNote ? ` · ${s.decisionNote}` : ""}
                </span>
                {s.createdTaskId && (
                  <Link href={`/tasks/task/${s.createdTaskId}`} className="text-xs text-accent-strong hover:underline">
                    {t("Задача")}
                  </Link>
                )}
              </li>
            ))}
          </ul>
        </>
      )}

      <Sheet
        open={accepting !== null}
        onClose={() => setAccepting(null)}
        title={t("Подтвердить и поставить задачу")}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={button} onClick={() => setAccepting(null)}>
              {t("Отмена")}
            </button>
            <button type="button" className={primary} onClick={accept} disabled={busy !== null || !edit?.title.trim()}>
              {busy === "accept" && <Loader2 className="size-4 animate-spin" />}
              {t("Поставить задачу")}
            </button>
          </div>
        }
      >
        {edit && accepting && (
          <div className="space-y-4">
            <p className="text-sm text-ink-3">{t("Отдел: {name}. Задача появится от вашего имени.", { name: accepting.departmentName })}</p>
            <label className="block">
              <span className={label}>{t("Название")}</span>
              <input className={field} value={edit.title} onChange={(e) => setEdit({ ...edit, title: e.target.value })} maxLength={300} />
            </label>
            <label className="block">
              <span className={label}>{t("Проблема")}</span>
              <textarea className={`${field} h-auto min-h-16 py-2`} value={edit.problem} onChange={(e) => setEdit({ ...edit, problem: e.target.value })} maxLength={2000} />
            </label>
            <label className="block">
              <span className={label}>{t("Обоснование")}</span>
              <textarea className={`${field} h-auto min-h-20 py-2`} value={edit.rationale} onChange={(e) => setEdit({ ...edit, rationale: e.target.value })} maxLength={4000} />
            </label>
            <div className="grid gap-4 sm:grid-cols-3">
              <div className="block sm:col-span-3">
                <span className={label}>{t("Исполнитель")}</span>
                <AssigneePicker value={edit.assigneeId} onChange={(id) => setEdit({ ...edit, assigneeId: id })} assignees={assignees} />
              </div>
              <label className="block">
                <span className={label}>{t("Приоритет")}</span>
                <select className={field} value={edit.priority} onChange={(e) => setEdit({ ...edit, priority: e.target.value as WorkTaskPriority })}>
                  {workPriorities.map((p) => (
                    <option key={p} value={p}>
                      {t(workPriorityLabel[p])}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block">
                <span className={label}>{t("Срок")}</span>
                <input type="date" className={field} value={edit.dueDate} min={todayInTashkent()} onChange={(e) => setEdit({ ...edit, dueDate: e.target.value })} />
              </label>
            </div>
          </div>
        )}
      </Sheet>

      <Sheet
        open={rejecting !== null}
        onClose={() => setRejecting(null)}
        title={t("Отклонить предложение")}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={button} onClick={() => setRejecting(null)}>
              {t("Отмена")}
            </button>
            <button type="button" className={danger} onClick={reject} disabled={busy !== null}>
              {busy === "reject" && <Loader2 className="size-4 animate-spin" />}
              {t("Отклонить")}
            </button>
          </div>
        }
      >
        <label className="block">
          <span className={label}>{t("Причина (необязательно)")}</span>
          <textarea className={`${field} h-auto min-h-20 py-2`} value={reason} onChange={(e) => setReason(e.target.value)} maxLength={1000} autoFocus />
        </label>
        <p className="mt-2 text-xs text-ink-3">{t("Отклонённое предложение по тому же поводу больше не появится.")}</p>
      </Sheet>
    </>
  );
}
