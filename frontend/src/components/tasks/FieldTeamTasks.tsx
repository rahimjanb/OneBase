"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { CalendarDays, MapPin, Plus, Store, UserRound, X } from "lucide-react";
import { field as inputClass, primary as primaryClass, button as outlineClass } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { fieldApi, useAction } from "@/components/field/hooks";
import { actorLabel, priorityLabel, taskActionLabel, taskStatusLabel } from "@/lib/field/labels";
import type { FieldCustomerRow, FieldPriority, FieldTask, FieldTaskStatus, Page } from "@/lib/field/types";
import { shortDate } from "@/lib/tasks";

export type TeamMember = { id: string; name: string; teamName: string | null; role: string };

const toneClass: Record<string, string> = {
  accent: "bg-accent-soft text-accent-strong",
  ok: "bg-ok-soft text-ok",
  warn: "bg-warn-soft text-warn",
  bad: "bg-bad-soft text-bad",
  muted: "bg-muted text-ink-3",
  ink: "bg-muted text-ink",
};

type Draft = { assignedToId: string; title: string; description: string; priority: FieldPriority; dueDate: string; market: { id: number; name: string } | null };

/**
 * Задачи агентов и супервайзеров (Sales Base) из OneBase. Новая задача сразу приходит исполнителю уведомлением в Sales Base
 * и видна у него в «Задачах»; если указана точка и срок, система поставит её в маршрут агента на этот день.
 */
export function FieldTeamTasks({ items, members, today, canCreate }: { items: FieldTask[]; members: TeamMember[]; today: string; canCreate: boolean }) {
  const { busy, error, setError, run } = useAction();
  const [draft, setDraft] = useState<Draft | null>(null);
  const [search, setSearch] = useState("");
  const [found, setFound] = useState<FieldCustomerRow[]>([]);

  // Исполнители: супервайзеры, затем агенты по командам.
  const groups = useMemo(() => {
    const supervisors = members.filter((m) => m.role === "Supervisor");
    const byTeam = new Map<string, TeamMember[]>();
    for (const m of members.filter((x) => x.role === "Agent")) {
      const key = m.teamName ?? "Без команды";
      byTeam.set(key, [...(byTeam.get(key) ?? []), m]);
    }
    return { supervisors, teams: [...byTeam.entries()].sort((a, b) => a[0].localeCompare(b[0], "ru")) };
  }, [members]);

  // Поиск точки по названию — только для агента: у супервайзера нет своих точек.
  const assignee = members.find((m) => m.id === draft?.assignedToId);
  useEffect(() => {
    const text = search.trim();
    if (!draft || text.length < 2) {
      setFound([]);
      return;
    }
    const timer = setTimeout(() => {
      fieldApi
        .get<Page<FieldCustomerRow>>(`customers?search=${encodeURIComponent(text)}&pageSize=8${assignee?.role === "Agent" ? `&agentId=${assignee.id}` : ""}`)
        .then((r) => setFound(r.items))
        .catch(() => setFound([]));
    }, 300);
    return () => clearTimeout(timer);
  }, [search, draft, assignee]);

  const save = () => {
    if (!draft) return;
    run(
      "create",
      () =>
        fieldApi.post("tasks", {
          title: draft.title,
          description: draft.description || null,
          assignedToId: draft.assignedToId,
          marketId: draft.market?.id ?? null,
          priority: draft.priority,
          dueDate: draft.dueDate || null,
        }),
      {
        onDone: () => {
          setDraft(null);
          setSearch("");
        },
      },
    );
  };

  const changeStatus = (task: FieldTask, status: FieldTaskStatus) =>
    run(`status-${task.id}`, () => fieldApi.post(`tasks/${task.id}/status`, { status, result: null, postponeTo: null }));

  // Офис только подтверждает, отменяет или возвращает в работу — ход задачи ведёт исполнитель в Sales Base.
  const officeActions = (t: FieldTask) =>
    t.nextStatuses.filter((s) => s === "Verified" || s === "Cancelled" || (s === "InProgress" && t.status === "Completed"));

  return (
    <div className="space-y-3">
      {canCreate && (
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={() => {
              setError(null);
              setSearch("");
              setDraft({ assignedToId: "", title: "", description: "", priority: "Medium", dueDate: today, market: null });
            }}
            className={primaryClass}
          >
            <Plus className="size-4" /> Задача агенту или супервайзеру
          </button>
          <span className="text-xs text-ink-3">Исполнитель сразу получит уведомление в Sales Base.</span>
        </div>
      )}

      {error && !draft && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

      {items.length === 0 ? (
        <div className="rounded-xl border border-dashed border-line bg-surface/60 px-6 py-10 text-center text-sm text-ink-3">Задач нет</div>
      ) : (
        <ul className="space-y-2">
          {items.map((t) => {
            const [label, tone] = taskStatusLabel[t.status];
            const actions = officeActions(t);
            return (
              <li key={t.id} className="rounded-xl border border-line bg-surface p-4">
                <div className="flex flex-wrap items-start gap-x-3 gap-y-1.5">
                  <div className="min-w-0 flex-1">
                    <Link href={`/field/tasks/${t.id}`} className="font-medium text-ink hover:underline">
                      {t.title}
                    </Link>
                    {t.description && <p className="mt-1 line-clamp-3 whitespace-pre-line text-sm text-ink-2">{t.description}</p>}
                  </div>
                  <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-medium ${toneClass[tone] ?? toneClass.muted}`}>{label}</span>
                </div>
                <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-ink-3">
                  <span className="inline-flex items-center gap-1">
                    <UserRound className="size-3.5" /> {t.assignedTo}
                  </span>
                  {t.marketName && (
                    <span className="inline-flex items-center gap-1">
                      <Store className="size-3.5" /> {t.marketName}
                    </span>
                  )}
                  {t.dueDate && (
                    <span className={`inline-flex items-center gap-1 ${t.overdue ? "font-medium text-bad" : ""}`}>
                      <CalendarDays className="size-3.5" /> {t.overdue ? "просрочена, срок " : "до "}
                      {shortDate(t.dueDate)}
                    </span>
                  )}
                  <span>{priorityLabel[t.priority][0]}</span>
                  <span>от {t.createdBy ?? actorLabel[t.createdByType]}</span>
                  {t.result && <span className="text-ink-2">итог: {t.result}</span>}
                </div>
                {actions.length > 0 && (
                  <div className="mt-3 flex flex-wrap gap-2">
                    {actions.map((s) => (
                      <button
                        key={s}
                        type="button"
                        disabled={busy !== null}
                        onClick={() => {
                          if (s === "Cancelled" && !window.confirm(`Отменить задачу «${t.title}»?`)) return;
                          changeStatus(t, s);
                        }}
                        className={`${s === "Verified" ? primaryClass : outlineClass} h-9`}
                      >
                        {busy === `status-${t.id}` ? "Сохраняем…" : s === "InProgress" ? "Вернуть в работу" : taskActionLabel[s]}
                      </button>
                    ))}
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}

      <Sheet
        open={draft !== null}
        onClose={() => setDraft(null)}
        title="Задача в Sales Base"
        footer={
          <div className="flex gap-2">
            <button type="button" disabled={busy !== null || !draft?.title.trim() || !draft?.assignedToId} onClick={save} className={`${primaryClass} h-11 flex-1 justify-center`}>
              {busy === "create" ? "Отправляем…" : "Поставить задачу"}
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
              <span className="mb-1.5 block text-sm text-ink-2">Исполнитель</span>
              <select value={draft.assignedToId} onChange={(e) => setDraft({ ...draft, assignedToId: e.target.value, market: null })} className={inputClass}>
                <option value="">Выберите агента или супервайзера</option>
                {groups.supervisors.length > 0 && (
                  <optgroup label="Супервайзеры">
                    {groups.supervisors.map((m) => (
                      <option key={m.id} value={m.id}>
                        {m.name}
                        {m.teamName ? ` · ${m.teamName}` : ""}
                      </option>
                    ))}
                  </optgroup>
                )}
                {groups.teams.map(([team, agents]) => (
                  <optgroup key={team} label={`Агенты · ${team}`}>
                    {agents.map((m) => (
                      <option key={m.id} value={m.id}>
                        {m.name}
                      </option>
                    ))}
                  </optgroup>
                ))}
              </select>
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Что сделать</span>
              <input value={draft.title} onChange={(e) => setDraft({ ...draft, title: e.target.value })} maxLength={300} className={inputClass} placeholder="Например: проверить выкладку и остатки" />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Подробности</span>
              <textarea value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} rows={3} maxLength={4000} className={`${inputClass} h-auto py-2`} />
            </label>
            <div className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Точка (необязательно)</span>
              {draft.market ? (
                <div className="flex items-center gap-2 rounded-lg border border-line bg-muted px-3 py-2 text-sm text-ink">
                  <MapPin className="size-4 shrink-0 text-ink-3" />
                  <span className="min-w-0 flex-1 truncate">{draft.market.name}</span>
                  <button type="button" onClick={() => setDraft({ ...draft, market: null })} aria-label="Убрать точку" className="grid size-8 place-items-center rounded-full text-ink-3 hover:bg-surface">
                    <X className="size-4" />
                  </button>
                </div>
              ) : (
                <>
                  <input value={search} onChange={(e) => setSearch(e.target.value)} className={inputClass} placeholder="Название или номер точки" />
                  {found.length > 0 && (
                    <ul className="mt-1 max-h-48 overflow-y-auto rounded-lg border border-line bg-surface">
                      {found.map((c) => (
                        <li key={c.marketId}>
                          <button
                            type="button"
                            onClick={() => {
                              setDraft({ ...draft, market: { id: c.marketId, name: c.name } });
                              setSearch("");
                            }}
                            className="block w-full px-3 py-2 text-left text-sm hover:bg-muted"
                          >
                            <span className="block truncate text-ink">{c.name}</span>
                            <span className="block truncate text-xs text-ink-3">
                              {[c.address, c.agentName].filter(Boolean).join(" · ")}
                            </span>
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
                </>
              )}
              <span className="mt-1 block text-xs text-ink-3">С точкой и сроком задача попадёт в маршрут агента на этот день.</span>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <label className="block">
                <span className="mb-1.5 block text-sm text-ink-2">Приоритет</span>
                <select value={draft.priority} onChange={(e) => setDraft({ ...draft, priority: e.target.value as FieldPriority })} className={inputClass}>
                  {(["Urgent", "High", "Medium", "Low"] as FieldPriority[]).map((p) => (
                    <option key={p} value={p}>
                      {priorityLabel[p][0]}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block">
                <span className="mb-1.5 block text-sm text-ink-2">Срок</span>
                <input type="date" value={draft.dueDate} min={today} onChange={(e) => setDraft({ ...draft, dueDate: e.target.value })} className={inputClass} />
              </label>
            </div>
            {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
          </div>
        )}
      </Sheet>
    </div>
  );
}
