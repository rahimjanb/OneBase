"use client";

import Link from "next/link";
import { useState } from "react";
import { Bot, Check, Pencil, Sparkles, X } from "lucide-react";
import { priorityLabel, recommendationKindLabel } from "@/lib/field/labels";
import type { FieldAgentOption, FieldPriority, FieldRecommendation } from "@/lib/field/types";
import { Chip, Empty, buttonClass, inputClass } from "./ui";
import { Sheet } from "./Sheet";
import { companyToday, fieldApi, useAction } from "./hooks";

/** Запустить анализ сейчас (обычно раз в день в 6 утра). */
export function GenerateButton() {
  const { busy, error, run } = useAction();
  const [message, setMessage] = useState<string | null>(null);
  return (
    <div className="flex flex-col items-end gap-1">
      <button
        type="button"
        disabled={busy !== null}
        onClick={() =>
          run("gen", () => fieldApi.post<{ created: number; skipped: number; autoApproved: number }>("ai/recommendations/generate"), {
            onDone: (r) => setMessage(r.created > 0 ? `Новых рекомендаций: ${r.created}${r.autoApproved ? `, подтверждено автоматически: ${r.autoApproved}` : ""}` : "Новых рекомендаций нет — всё уже предложено"),
          })
        }
        className={buttonClass.primary}
      >
        <Sparkles className="size-4" /> {busy === "gen" ? "Анализируем…" : "Проанализировать сейчас"}
      </button>
      {message && <span className="text-xs text-ink-3">{message}</span>}
      {error && <span className="text-xs text-bad">{error}</span>}
    </div>
  );
}

/** Карточки рекомендаций: причина, цифры, уверенность; подтвердить (можно с правкой) или отклонить. */
export function RecommendationList({ items, agents, compact = false }: { items: FieldRecommendation[]; agents: FieldAgentOption[]; compact?: boolean }) {
  const { busy, error, run } = useAction();
  const [edit, setEdit] = useState<FieldRecommendation | null>(null);
  const [reject, setReject] = useState<FieldRecommendation | null>(null);
  const [note, setNote] = useState("");
  const [form, setForm] = useState<{ title: string; agentId: string; priority: FieldPriority; dueDate: string }>({ title: "", agentId: "", priority: "Medium", dueDate: "" });

  if (items.length === 0) return <Empty>Рекомендаций нет. AI анализирует продажи, визиты и планы каждое утро.</Empty>;

  return (
    <>
      {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      <ul className="space-y-2">
        {items.map((r) => {
          const decided = r.status !== "Pending";
          return (
            <li key={r.id} className={`rounded-xl border bg-surface p-4 ${decided ? "border-line opacity-80" : "border-line"}`}>
              <div className="flex flex-wrap items-center gap-1.5 text-xs">
                <Chip tone="accent">
                  <Bot className="size-3" /> {recommendationKindLabel[r.kind]}
                </Chip>
                <Chip tone={priorityLabel[r.priority][1]}>{priorityLabel[r.priority][0]}</Chip>
                <span className="text-ink-3">уверенность {Math.round(r.confidence * 100)}%</span>
                {r.status === "Approved" && <Chip tone="ok">подтверждена{r.decidedBy ? ` · ${r.decidedBy}` : ""}</Chip>}
                {r.status === "Rejected" && <Chip tone="muted">отклонена{r.decisionNote ? `: ${r.decisionNote}` : ""}</Chip>}
                {r.status === "Expired" && <Chip tone="muted">устарела</Chip>}
              </div>
              <div className="mt-2 font-medium text-ink">{r.title}</div>
              <p className="mt-1 text-sm text-ink-2">{r.reason}</p>
              {!compact && (
                <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-ink-3">
                  {r.agentName && <span>Агент: {r.agentName}</span>}
                  {r.marketName && (
                    <Link href={`/field/customers/${r.marketId}`} prefetch={false} className="hover:text-ink hover:underline">
                      Точка: {r.marketName}
                    </Link>
                  )}
                  {r.dueDate && <span>Срок: {new Date(`${r.dueDate}T00:00:00Z`).toLocaleDateString("ru-RU", { timeZone: "UTC" })}</span>}
                  {r.taskId && (
                    <Link href={`/field/tasks/${r.taskId}`} className="text-accent-strong hover:underline">
                      Задача создана →
                    </Link>
                  )}
                </div>
              )}
              {r.canDecide && (
                <div className="mt-3 flex flex-wrap gap-2">
                  <button type="button" disabled={busy !== null} onClick={() => run(r.id, () => fieldApi.post(`ai/recommendations/${r.id}/approve`, {}))} className="inline-flex h-10 items-center gap-1.5 rounded-lg bg-ok px-3 text-sm font-medium text-white disabled:opacity-50">
                    <Check className="size-4" /> {r.kind === "Reassign" ? "Назначить" : "Создать задачу"}
                  </button>
                  {r.kind !== "Reassign" && (
                    <button
                      type="button"
                      onClick={() => {
                        setForm({ title: r.title, agentId: r.agentId ?? "", priority: r.priority, dueDate: r.dueDate ?? companyToday() });
                        setEdit(r);
                      }}
                      className="inline-flex h-10 items-center gap-1.5 rounded-lg border border-line px-3 text-sm text-ink-2 hover:bg-muted"
                    >
                      <Pencil className="size-4" /> Изменить
                    </button>
                  )}
                  <button
                    type="button"
                    onClick={() => {
                      setNote("");
                      setReject(r);
                    }}
                    className="inline-flex h-10 items-center gap-1.5 rounded-lg border border-line px-3 text-sm text-ink-2 hover:bg-muted"
                  >
                    <X className="size-4" /> Отклонить
                  </button>
                </div>
              )}
            </li>
          );
        })}
      </ul>

      <Sheet
        open={edit !== null}
        onClose={() => setEdit(null)}
        title="Подтвердить с правкой"
        footer={
          <button
            type="button"
            disabled={busy !== null}
            onClick={() => edit && run("edit", () => fieldApi.post(`ai/recommendations/${edit.id}/approve`, { title: form.title, agentId: form.agentId || null, priority: form.priority, dueDate: form.dueDate || null }), { onDone: () => setEdit(null) })}
            className={`${buttonClass.primary} w-full`}
          >
            Создать задачу
          </button>
        }
      >
        <div className="space-y-3">
          <label className="block">
            <span className="mb-1.5 block text-sm text-ink-2">Задача</span>
            <input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} className={inputClass} />
          </label>
          {agents.length > 0 && (
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Исполнитель</span>
              <select value={form.agentId} onChange={(e) => setForm({ ...form, agentId: e.target.value })} className={inputClass}>
                <option value="">как предложил AI</option>
                {agents.map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name}
                  </option>
                ))}
              </select>
            </label>
          )}
          <div className="grid grid-cols-2 gap-3">
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Приоритет</span>
              <select value={form.priority} onChange={(e) => setForm({ ...form, priority: e.target.value as FieldPriority })} className={inputClass}>
                {(["Low", "Medium", "High", "Urgent"] as FieldPriority[]).map((p) => (
                  <option key={p} value={p}>
                    {priorityLabel[p][0]}
                  </option>
                ))}
              </select>
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Срок</span>
              <input type="date" value={form.dueDate} onChange={(e) => setForm({ ...form, dueDate: e.target.value })} className={inputClass} />
            </label>
          </div>
          <p className="text-xs text-ink-3">Точка встанет в маршрут исполнителя на этот день, если маршрут уже есть.</p>
        </div>
      </Sheet>

      <Sheet
        open={reject !== null}
        onClose={() => setReject(null)}
        title="Отклонить рекомендацию"
        footer={
          <button type="button" disabled={busy !== null} onClick={() => reject && run("reject", () => fieldApi.post(`ai/recommendations/${reject.id}/reject`, { note: note || null }), { onDone: () => setReject(null) })} className={`${buttonClass.danger} w-full`}>
            Отклонить
          </button>
        }
      >
        <p className="text-sm text-ink-2">{reject?.title}</p>
        <div className="mt-3 flex flex-wrap gap-2">
          {["Уже в работе", "Точка закрыта", "Сезонное снижение", "Неверные данные"].map((t) => (
            <button key={t} type="button" onClick={() => setNote(t)} className={`rounded-full px-3 py-2 text-sm ${note === t ? "bg-ink text-surface" : "bg-muted text-ink-2"}`}>
              {t}
            </button>
          ))}
        </div>
        <input value={note} onChange={(e) => setNote(e.target.value)} placeholder="Причина (AI учтёт: такая же рекомендация не появится 14 дней)" className={`${inputClass} mt-3`} />
      </Sheet>
    </>
  );
}
