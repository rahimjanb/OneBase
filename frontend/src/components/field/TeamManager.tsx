"use client";

import { useMemo, useState } from "react";
import { Download, KeyRound, Pencil, Plus, UserPlus } from "lucide-react";
import { roleLabel } from "@/lib/field/labels";
import type { FieldMemberView, FieldRole, FieldStructure, FieldTeamView } from "@/lib/field/types";
import { Chip, Empty, buttonClass, inputClass } from "./ui";
import { Sheet } from "./Sheet";
import { fieldApi, useAction } from "./hooks";

/**
 * Состав Sales Base: команды (супервайзер + агенты, филиал) и участники. РМ: импорт из Linko, правка, выдача входа.
 * Супервайзер видит свою команду без правки.
 */
export function TeamManager({ data }: { data: FieldStructure }) {
  const { busy, error, run } = useAction();
  const [team, setTeam] = useState<Partial<FieldTeamView> | null>(null);
  const [member, setMember] = useState<Partial<FieldMemberView> | null>(null);
  const [access, setAccess] = useState<FieldMemberView | null>(null);
  const [credentials, setCredentials] = useState<{ login: string; password: string } | null>(null);
  const [login, setLogin] = useState("");
  const [filter, setFilter] = useState("");
  const [imported, setImported] = useState<string | null>(null);

  const supervisors = data.members.filter((m) => m.role === "Supervisor" && m.isActive);
  const byTeam = useMemo(() => {
    const text = filter.trim().toLowerCase();
    const visible = data.members.filter((m) => !text || m.fullName.toLowerCase().includes(text) || (m.login ?? "").includes(text));
    return {
      teams: data.teams.map((t) => ({ team: t, agents: visible.filter((m) => m.teamId === t.id && m.role === "Agent") })),
      noTeam: visible.filter((m) => m.role === "Agent" && !m.teamId),
      leads: visible.filter((m) => m.role !== "Agent"),
    };
  }, [data, filter]);

  const memberRow = (m: FieldMemberView) => (
    <li key={m.id} className="flex flex-wrap items-center gap-2 px-4 py-2.5">
      <div className="min-w-0 flex-1">
        <div className="truncate text-sm font-medium text-ink">
          {m.fullName} {!m.isActive && <Chip tone="muted">отключён</Chip>}
        </div>
        <div className="truncate text-xs text-ink-3">
          {roleLabel[m.role]}
          {m.linkoPosition ? ` · ${m.linkoPosition}` : ""}
          {m.markets ? ` · точек: ${m.markets}` : ""}
          {m.login ? ` · вход: ${m.login}` : " · входа нет"}
        </div>
      </div>
      {m.editable && (
        <div className="flex gap-1.5">
          <button type="button" onClick={() => setMember(m)} className="grid size-9 place-items-center rounded-lg border border-line text-ink-2 hover:bg-muted" aria-label="Изменить">
            <Pencil className="size-4" />
          </button>
          <button
            type="button"
            onClick={() => {
              setCredentials(null);
              setLogin("");
              setAccess(m);
            }}
            className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line px-2.5 text-xs text-ink-2 hover:bg-muted"
          >
            <KeyRound className="size-3.5" /> {m.userId ? "Пароль" : "Выдать вход"}
          </button>
        </div>
      )}
    </li>
  );

  return (
    <div className="space-y-4">
      {data.canManage && (
        <div className="flex flex-wrap items-center gap-2">
          {data.canManageOrg && (
          <button
            type="button"
            disabled={busy !== null}
            onClick={() =>
              run("import", () => fieldApi.post<{ agents: number; supervisors: number; teamsCreated: number; supervisorsLinked: number }>("structure/import"), {
                onDone: (r) => setImported(`Добавлено агентов: ${r.agents}, супервайзеров: ${r.supervisors}, команд: ${r.teamsCreated}, супервайзеров во главе команд: ${r.supervisorsLinked}.`),
              })
            }
            className={buttonClass.primary}
          >
            <Download className="size-4" /> {busy === "import" ? "Импорт…" : `Импорт из Linko${data.candidates.length ? ` (${data.candidates.length})` : ""}`}
          </button>
          )}
          <button type="button" onClick={() => setTeam({ isActive: true })} className={buttonClass.outline}>
            <Plus className="size-4" /> Команда
          </button>
          <button type="button" onClick={() => setMember({ role: "Agent", isActive: true })} className={buttonClass.outline}>
            <UserPlus className="size-4" /> Участник
          </button>
          <input value={filter} onChange={(e) => setFilter(e.target.value)} placeholder="Поиск по имени или логину" className={`${inputClass} sm:ml-auto sm:max-w-xs`} />
        </div>
      )}
      {imported && <p className="rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">{imported}</p>}
      {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

      {data.members.length === 0 && (
        <Empty>В Sales Base ещё никого нет. Импорт из Linko добавит агентов и супервайзеров и соберёт команды по филиалам.</Empty>
      )}

      {byTeam.leads.length > 0 && (
        <section className="overflow-hidden rounded-xl border border-line bg-surface">
          <div className="border-b border-line px-4 py-3 text-sm font-semibold text-ink">Руководители</div>
          <ul className="divide-y divide-line">{byTeam.leads.map(memberRow)}</ul>
        </section>
      )}

      <div className="grid gap-4 xl:grid-cols-2">
        {byTeam.teams.map(({ team: t, agents }) => (
          <section key={t.id} className="overflow-hidden rounded-xl border border-line bg-surface">
            <div className="flex items-center gap-2 border-b border-line px-4 py-3">
              <div className="min-w-0 flex-1">
                <div className="truncate text-sm font-semibold text-ink">
                  {t.name} {!t.isActive && <Chip tone="muted">неактивна</Chip>}
                </div>
                <div className="truncate text-xs text-ink-3">
                  {t.supervisorName ? `Супервайзер: ${t.supervisorName}` : "Супервайзер не назначен"}
                  {t.branchName ? ` · филиал ${t.branchName}` : ""} · агентов: {t.agents}
                </div>
              </div>
              {data.canManage && (
                <button type="button" onClick={() => setTeam(t)} className="grid size-9 place-items-center rounded-lg border border-line text-ink-2 hover:bg-muted" aria-label="Изменить команду">
                  <Pencil className="size-4" />
                </button>
              )}
            </div>
            {agents.length > 0 ? <ul className="divide-y divide-line">{agents.map(memberRow)}</ul> : <p className="px-4 py-4 text-sm text-ink-3">Агентов нет</p>}
          </section>
        ))}
      </div>

      {byTeam.noTeam.length > 0 && (
        <section className="overflow-hidden rounded-xl border border-warn/40 bg-surface">
          <div className="border-b border-line px-4 py-3 text-sm font-semibold text-ink">Агенты без команды</div>
          <ul className="divide-y divide-line">{byTeam.noTeam.map(memberRow)}</ul>
        </section>
      )}

      {/* Команда */}
      <Sheet
        open={team !== null}
        onClose={() => setTeam(null)}
        title={team?.id ? "Команда" : "Новая команда"}
        footer={
          <button
            type="button"
            disabled={busy !== null || !team?.name?.trim()}
            onClick={() =>
              team &&
              run("team", () => (team.id ? fieldApi.put(`teams/${team.id}`, team) : fieldApi.post("teams", team)), { onDone: () => setTeam(null) })
            }
            className={`${buttonClass.primary} w-full`}
          >
            Сохранить
          </button>
        }
      >
        {team && (
          <div className="space-y-3">
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Название</span>
              <input value={team.name ?? ""} onChange={(e) => setTeam({ ...team, name: e.target.value })} className={inputClass} />
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Супервайзер</span>
              <select value={team.supervisorId ?? ""} onChange={(e) => setTeam({ ...team, supervisorId: e.target.value || null })} className={inputClass}>
                <option value="">— не назначен —</option>
                {supervisors.map((s) => (
                  <option key={s.id} value={s.id}>
                    {s.fullName}
                  </option>
                ))}
              </select>
            </label>
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Филиал Linko</span>
              <select value={team.branchId ?? ""} onChange={(e) => setTeam({ ...team, branchId: e.target.value ? Number(e.target.value) : null })} className={inputClass}>
                {(data.canManageOrg || !team.branchId) && <option value="">{data.canManageOrg ? "— без филиала —" : "— выберите филиал —"}</option>}
                {data.branches.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="flex items-center gap-2 text-sm text-ink">
              <input type="checkbox" checked={team.isActive ?? true} onChange={(e) => setTeam({ ...team, isActive: e.target.checked })} className="size-5" /> Команда работает
            </label>
          </div>
        )}
      </Sheet>

      {/* Участник */}
      <Sheet
        open={member !== null}
        onClose={() => setMember(null)}
        title={member?.id ? "Участник" : "Новый участник"}
        footer={
          <button
            type="button"
            disabled={busy !== null || !member?.fullName?.trim()}
            onClick={() =>
              member &&
              run(
                "member",
                () => {
                  const body = { fullName: member.fullName, role: member.role, phone: member.phone ?? null, teamId: member.teamId ?? null, linkoUserId: member.linkoUserId ?? null, branchIds: member.branchIds ?? [], isActive: member.isActive ?? true };
                  return member.id ? fieldApi.put(`members/${member.id}`, body) : fieldApi.post("members", body);
                },
                { onDone: () => setMember(null) },
              )
            }
            className={`${buttonClass.primary} w-full`}
          >
            Сохранить
          </button>
        }
      >
        {member && (
          <div className="space-y-3">
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Имя</span>
              <input value={member.fullName ?? ""} onChange={(e) => setMember({ ...member, fullName: e.target.value })} className={inputClass} />
            </label>
            <div className="grid grid-cols-2 gap-3">
              <label className="block">
                <span className="mb-1.5 block text-sm text-ink-2">Роль</span>
                <select value={member.role} onChange={(e) => setMember({ ...member, role: e.target.value as FieldRole })} className={inputClass}>
                  {((data.canManageOrg || member.role === "Rm" ? ["Agent", "Supervisor", "Rm"] : ["Agent", "Supervisor"]) as FieldRole[]).map((r) => (
                    <option key={r} value={r}>
                      {roleLabel[r]}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block">
                <span className="mb-1.5 block text-sm text-ink-2">Телефон</span>
                <input type="tel" value={member.phone ?? ""} onChange={(e) => setMember({ ...member, phone: e.target.value })} className={inputClass} />
              </label>
            </div>
            {member.role === "Agent" && (
              <label className="block">
                <span className="mb-1.5 block text-sm text-ink-2">Команда</span>
                <select value={member.teamId ?? ""} onChange={(e) => setMember({ ...member, teamId: e.target.value || null })} className={inputClass}>
                  <option value="">— без команды —</option>
                  {data.teams.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.name}
                    </option>
                  ))}
                </select>
              </label>
            )}
            {member.role === "Rm" && (
              <fieldset>
                <legend className="mb-1.5 text-sm text-ink-2">Филиалы РМ (пусто — вся организация)</legend>
                <div className="flex max-h-40 flex-wrap gap-1.5 overflow-y-auto">
                  {data.branches.map((b) => {
                    const on = (member.branchIds ?? []).includes(b.id);
                    return (
                      <button
                        key={b.id}
                        type="button"
                        onClick={() => setMember({ ...member, branchIds: on ? (member.branchIds ?? []).filter((x) => x !== b.id) : [...(member.branchIds ?? []), b.id] })}
                        className={`rounded-full px-3 py-1.5 text-xs ${on ? "bg-accent text-white" : "bg-muted text-ink-2"}`}
                      >
                        {b.name}
                      </button>
                    );
                  })}
                </div>
              </fieldset>
            )}
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">Сотрудник Linko (id)</span>
              <input inputMode="numeric" value={member.linkoUserId ?? ""} onChange={(e) => setMember({ ...member, linkoUserId: e.target.value ? Number(e.target.value.replace(/\D/g, "")) : null })} className={inputClass} placeholder="продажи, визиты и планы — по нему" />
            </label>
            <label className="flex items-center gap-2 text-sm text-ink">
              <input type="checkbox" checked={member.isActive ?? true} onChange={(e) => setMember({ ...member, isActive: e.target.checked })} className="size-5" /> Работает (отключённый не входит и не виден в планах)
            </label>
          </div>
        )}
      </Sheet>

      {/* Доступ */}
      <Sheet
        open={access !== null}
        onClose={() => setAccess(null)}
        title={access?.userId ? "Новый пароль" : "Выдать вход в Sales Base"}
        footer={
          !credentials && (
            <button
              type="button"
              disabled={busy !== null}
              onClick={() => access && run("access", () => fieldApi.post<{ login: string; password: string }>(`members/${access.id}/access`, { login: login || null }), { onDone: (r) => setCredentials(r) })}
              className={`${buttonClass.primary} w-full`}
            >
              {access?.userId ? "Задать новый пароль" : "Создать вход"}
            </button>
          )
        }
      >
        <p className="text-sm text-ink-2">
          {access?.fullName} · {access ? roleLabel[access.role] : ""}
        </p>
        {credentials ? (
          <div className="mt-3 space-y-2 rounded-xl bg-ok-soft p-4 text-sm">
            <div>
              Логин: <b className="select-all font-mono">{credentials.login}</b>
            </div>
            <div>
              Пароль: <b className="select-all font-mono">{credentials.password}</b>
            </div>
            <p className="text-xs text-ink-2">Пароль показывается один раз — передайте его сотруднику. Вход: sales.1base.uz с телефона.</p>
          </div>
        ) : (
          !access?.userId && (
            <label className="mt-3 block">
              <span className="mb-1.5 block text-sm text-ink-2">Логин (пусто — из Linko)</span>
              <input value={login} onChange={(e) => setLogin(e.target.value.toLowerCase())} className={inputClass} placeholder="латиница, цифры, точка" />
            </label>
          )
        )}
      </Sheet>
    </div>
  );
}
