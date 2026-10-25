"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { Crown, FolderKanban, Loader2, Pencil, Plus, Search, UserCog } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import type { DepartmentTasks, WorkDepartmentHeads, WorkProject, WorkUserOption } from "@/lib/tasks";
import { ErrorNote } from "./TaskBadges";

type Draft = { id?: string; name: string; description: string; department: string; members: { userId: string; isManager: boolean }[]; archived: boolean };

const label = "mb-1.5 block text-sm font-medium text-ink";

/** Выбор людей: поиск по имени, отметка «участник», для проекта — ещё «руководитель проекта». */
function PeoplePicker({
  users,
  selected,
  onChange,
  managers,
}: {
  users: WorkUserOption[];
  selected: { userId: string; isManager: boolean }[];
  onChange: (next: { userId: string; isManager: boolean }[]) => void;
  managers: boolean;
}) {
  const t = useT();
  const [query, setQuery] = useState("");
  const visible = useMemo(() => {
    const q = query.trim().toLowerCase();
    return users.filter((u) => !q || u.name.toLowerCase().includes(q) || (u.departmentName ?? "").toLowerCase().includes(q)).slice(0, 60);
  }, [users, query]);
  const has = (id: string) => selected.find((s) => s.userId === id);
  return (
    <div>
      <label className="relative mb-2 block">
        <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" />
        <input className={`${field} pl-9`} value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t("Найти сотрудника")} />
      </label>
      <ul className="max-h-64 space-y-1 overflow-y-auto rounded-lg border border-line p-1.5">
        {visible.map((u) => {
          const s = has(u.id);
          return (
            <li key={u.id} className="flex items-center gap-2 rounded-md px-2 py-1.5 hover:bg-muted">
              <input
                type="checkbox"
                checked={!!s}
                onChange={(e) => onChange(e.target.checked ? [...selected, { userId: u.id, isManager: false }] : selected.filter((x) => x.userId !== u.id))}
                className="size-4 accent-[var(--color-accent)]"
                aria-label={u.name}
              />
              <span className="min-w-0 flex-1 text-sm">
                <span className="text-ink">{u.name}</span>
                <span className="block truncate text-xs text-ink-3">{[u.position, u.departmentName].filter(Boolean).join(" · ")}</span>
              </span>
              {managers && s && (
                <label className="flex items-center gap-1 text-xs text-ink-2">
                  <input
                    type="checkbox"
                    checked={s.isManager}
                    onChange={(e) => onChange(selected.map((x) => (x.userId === u.id ? { ...x, isManager: e.target.checked } : x)))}
                    className="size-3.5 accent-[var(--color-accent)]"
                  />
                  {t("PM")}
                </label>
              )}
            </li>
          );
        })}
      </ul>
      <p className="mt-1 text-xs text-ink-3">{t("Выбрано: {n}", { n: selected.length })}</p>
    </div>
  );
}

/**
 * «Проекты»: проекты отделов с участниками и руководителями (PM управляют задачами проекта). Создаёт проект руководитель отдела или
 * руководство; меняет — они же и PM. Руководство (право на пользователей) назначает руководителей отделов.
 */
export function ProjectsView({
  projects,
  departments,
  heads,
  canSetHeads,
}: {
  projects: WorkProject[];
  departments: DepartmentTasks[];
  heads: WorkDepartmentHeads[];
  canSetHeads: boolean;
}) {
  const t = useT();
  const router = useRouter();
  const [draft, setDraft] = useState<Draft | null>(null);
  const [headsOf, setHeadsOf] = useState<WorkDepartmentHeads | null>(null);
  const [headIds, setHeadIds] = useState<{ userId: string; isManager: boolean }[]>([]);
  const [users, setUsers] = useState<WorkUserOption[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const managed = departments.filter((d) => d.isHead);

  useEffect(() => {
    if ((!draft && !headsOf) || users.length > 0) return;
    bff<WorkUserOption[]>("work/users").then(setUsers).catch((e) => setError((e as Error).message));
  }, [draft, headsOf, users.length]);

  const save = async () => {
    if (!draft) return;
    setBusy(true);
    setError(null);
    try {
      const body = JSON.stringify({ name: draft.name.trim(), description: draft.description.trim() || null, department: draft.department, members: draft.members, archived: draft.archived });
      if (draft.id) await bff(`work/projects/${draft.id}`, { method: "PUT", body });
      else await bff("work/projects", { method: "POST", body });
      setDraft(null);
      router.refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const saveHeads = async () => {
    if (!headsOf) return;
    setBusy(true);
    setError(null);
    try {
      await bff(`work/departments/${headsOf.code}/heads`, { method: "PUT", body: JSON.stringify({ userIds: headIds.map((h) => h.userId) }) });
      setHeadsOf(null);
      router.refresh();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6">
      <ErrorNote text={error} />
      <section>
        <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-sm font-semibold text-ink">{t("Проекты")}</h2>
          {managed.length > 0 && (
            <button
              type="button"
              className={primary}
              onClick={() => setDraft({ name: "", description: "", department: managed[0].code, members: [], archived: false })}
            >
              <Plus className="size-4" /> {t("Новый проект")}
            </button>
          )}
        </div>
        {projects.length === 0 ? (
          <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-8 text-center text-sm text-ink-3">
            {managed.length > 0 ? t("Проектов пока нет — создайте первый.") : t("Вы не участвуете в проектах. Проекты создают руководители отделов.")}
          </p>
        ) : (
          <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            {projects.map((p) => (
              <article key={p.id} className={`flex flex-col rounded-xl border bg-surface p-4 ${p.archived ? "border-dashed border-line opacity-70" : "border-line"}`}>
                <div className="flex items-start gap-3">
                  <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-accent-soft text-accent-strong">
                    <FolderKanban className="size-4" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <h3 className="font-semibold text-ink">{p.name}</h3>
                    <p className="text-xs text-ink-3">
                      {p.departmentName} · {t("открытых задач {n}", { n: p.openTasks })}
                      {p.archived && ` · ${t("в архиве")}`}
                    </p>
                  </div>
                  {p.canManage && (
                    <button
                      type="button"
                      className="rounded-lg p-1.5 text-ink-3 hover:bg-muted hover:text-ink"
                      aria-label={t("Изменить проект {name}", { name: p.name })}
                      onClick={() =>
                        setDraft({
                          id: p.id,
                          name: p.name,
                          description: p.description ?? "",
                          department: p.departmentCode,
                          members: p.members.map((m) => ({ userId: m.userId, isManager: m.isManager })),
                          archived: p.archived,
                        })
                      }
                    >
                      <Pencil className="size-4" />
                    </button>
                  )}
                </div>
                {p.description && <p className="mt-2 text-sm text-ink-2">{p.description}</p>}
                <ul className="mt-3 flex flex-wrap gap-1.5">
                  {p.members.map((m) => (
                    <li key={m.userId} className="inline-flex items-center gap-1 rounded-full bg-muted px-2 py-0.5 text-xs text-ink-2">
                      {m.isManager && <Crown className="size-3 text-warn" aria-label={t("Руководитель проекта")} />}
                      {m.name}
                    </li>
                  ))}
                  {p.members.length === 0 && <li className="text-xs text-ink-3">{t("Участников нет")}</li>}
                </ul>
                <Link href={`/tasks/all?projectId=${p.id}`} className="mt-3 text-sm text-accent-strong hover:underline">
                  {t("Задачи проекта")}
                </Link>
              </article>
            ))}
          </div>
        )}
      </section>

      <section>
        <h2 className="mb-1 text-sm font-semibold text-ink">{t("Руководители отделов")}</h2>
        <p className="mb-3 text-sm text-ink-3">{t("Руководитель отдела управляет всеми задачами отдела, создаёт проекты и получает предложения AI-диспетчера.")}</p>
        <ul className="divide-y divide-line rounded-xl border border-line bg-surface">
          {heads.map((d) => (
            <li key={d.code} className="flex flex-wrap items-center gap-3 px-4 py-3">
              <span className="w-40 shrink-0 font-medium text-ink">{d.name}</span>
              <span className="min-w-0 flex-1 text-sm text-ink-2">{d.heads.length > 0 ? d.heads.map((h) => h.name).join(", ") : <span className="text-ink-3">{t("не назначен")}</span>}</span>
              {canSetHeads && (
                <button
                  type="button"
                  className={button}
                  onClick={() => {
                    setHeadsOf(d);
                    setHeadIds(d.heads.map((h) => ({ userId: h.id, isManager: false })));
                  }}
                >
                  <UserCog className="size-4" /> {t("Назначить")}
                </button>
              )}
            </li>
          ))}
        </ul>
      </section>

      <Sheet
        open={draft !== null}
        onClose={() => setDraft(null)}
        title={draft?.id ? t("Изменить проект") : t("Новый проект")}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={button} onClick={() => setDraft(null)}>
              {t("Отмена")}
            </button>
            <button type="button" className={primary} onClick={save} disabled={busy || !draft?.name.trim()}>
              {busy && <Loader2 className="size-4 animate-spin" />}
              {t("Сохранить")}
            </button>
          </div>
        }
      >
        {draft && (
          <div className="space-y-4">
            <label className="block">
              <span className={label}>{t("Название")}</span>
              <input className={field} value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} maxLength={200} autoFocus />
            </label>
            <label className="block">
              <span className={label}>{t("Описание")}</span>
              <textarea className={`${field} h-auto min-h-16 py-2`} value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} maxLength={2000} />
            </label>
            <label className="block">
              <span className={label}>{t("Отдел")}</span>
              <select className={field} value={draft.department} disabled={!!draft.id} onChange={(e) => setDraft({ ...draft, department: e.target.value })}>
                {(draft.id ? departments : managed).map((d) => (
                  <option key={d.code} value={d.code}>
                    {d.name}
                  </option>
                ))}
              </select>
            </label>
            <div>
              <span className={label}>{t("Участники и руководители проекта (PM)")}</span>
              <PeoplePicker users={users} selected={draft.members} onChange={(members) => setDraft({ ...draft, members })} managers />
            </div>
            {draft.id && (
              <label className="flex items-center gap-2 text-sm text-ink">
                <input type="checkbox" checked={draft.archived} onChange={(e) => setDraft({ ...draft, archived: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
                {t("Проект в архиве (новые задачи в нём не ставятся)")}
              </label>
            )}
          </div>
        )}
      </Sheet>

      <Sheet
        open={headsOf !== null}
        onClose={() => setHeadsOf(null)}
        title={headsOf ? t("Руководители: {name}", { name: headsOf.name }) : ""}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" className={button} onClick={() => setHeadsOf(null)}>
              {t("Отмена")}
            </button>
            <button type="button" className={primary} onClick={saveHeads} disabled={busy}>
              {busy && <Loader2 className="size-4 animate-spin" />}
              {t("Сохранить")}
            </button>
          </div>
        }
      >
        <PeoplePicker users={users} selected={headIds} onChange={setHeadIds} managers={false} />
      </Sheet>
    </div>
  );
}
