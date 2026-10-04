"use client";

import { useState } from "react";
import { Pencil, UserRoundCog } from "lucide-react";
import { customerStatusLabel, priorityLabel } from "@/lib/field/labels";
import type { FieldAgentOption, FieldCustomerStatus, FieldPriority } from "@/lib/field/types";
import { buttonClass, inputClass } from "./ui";
import { Sheet } from "./Sheet";
import { fieldApi, useAction } from "./hooks";

/** Сменить агента точки: точка уйдёт из маршрута прежнего агента и встанет в сегодняшний маршрут нового. */
export function ChangeAgentButton({ marketId, marketName, currentAgentId, options }: { marketId: number; marketName: string; currentAgentId: string | null; options: FieldAgentOption[] }) {
  const { busy, error, run } = useAction();
  const [open, setOpen] = useState(false);
  const [agent, setAgent] = useState("");
  const [done, setDone] = useState<string | null>(null);
  const choices = options.filter((o) => o.id !== currentAgentId);

  return (
    <>
      <button type="button" onClick={() => setOpen(true)} className={buttonClass.outline}>
        <UserRoundCog className="size-4" /> Изменить агента
      </button>
      <Sheet
        open={open}
        onClose={() => {
          setOpen(false);
          setDone(null);
        }}
        title="Изменить агента"
        footer={
          !done && (
            <button
              type="button"
              disabled={!agent || busy !== null}
              onClick={() =>
                run("change", () => fieldApi.post<{ toAgent: string; routesUpdated: number; addedToRoute: boolean }>(`customers/${marketId}/agent`, { agentId: agent }), {
                  onDone: (r) => setDone(`Точка передана: ${r.toAgent}. ${r.routesUpdated > 0 ? `Убрана из маршрутов прежнего агента (${r.routesUpdated}). ` : ""}${r.addedToRoute ? "Добавлена в сегодняшний маршрут." : ""}`),
                })
              }
              className={`${buttonClass.primary} w-full`}
            >
              {busy === "change" ? "Передаём…" : "Передать точку"}
            </button>
          )
        }
      >
        <p className="text-sm text-ink-2">«{marketName}»</p>
        {done ? (
          <p className="mt-3 rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">{done}</p>
        ) : (
          <>
            <label className="mt-3 block">
              <span className="mb-1.5 block text-sm text-ink-2">Новый агент</span>
              <select value={agent} onChange={(e) => setAgent(e.target.value)} className={inputClass}>
                <option value="">— выберите —</option>
                {choices.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.name}
                    {o.teamName ? ` · ${o.teamName}` : ""}
                  </option>
                ))}
              </select>
            </label>
            <p className="mt-2 text-xs text-ink-3">Маршруты пересчитаются, открытые задачи по точке перейдут к новому агенту, оба получат уведомление. В Linko ответственный не меняется.</p>
            {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
          </>
        )}
      </Sheet>
    </>
  );
}

/** Карточка точки: приоритет, статус, план (супервайзер/РМ), контакт, телефон, заметка (и агент). */
export function EditCustomerButton({
  marketId,
  canPlan,
  initial,
}: {
  marketId: number;
  canPlan: boolean;
  initial: { priority: FieldPriority; status: FieldCustomerStatus; monthlyTarget: number | null; contactPerson: string | null; phone: string | null; note: string | null };
}) {
  const { busy, error, run } = useAction();
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ ...initial, monthlyTarget: initial.monthlyTarget ? String(initial.monthlyTarget) : "" });

  return (
    <>
      <button type="button" onClick={() => setOpen(true)} className={buttonClass.outline}>
        <Pencil className="size-4" /> Изменить
      </button>
      <Sheet
        open={open}
        onClose={() => setOpen(false)}
        title="Карточка точки"
        footer={
          <button
            type="button"
            disabled={busy !== null}
            onClick={() =>
              run(
                "save",
                () =>
                  fieldApi.put(`customers/${marketId}`, {
                    priority: canPlan ? form.priority : null,
                    status: canPlan ? form.status : null,
                    monthlyTarget: canPlan ? (form.monthlyTarget ? Number(form.monthlyTarget) : 0) : null,
                    contactPerson: form.contactPerson || null,
                    phone: form.phone || null,
                    note: form.note ?? "",
                  }),
                { onDone: () => setOpen(false) },
              )
            }
            className={`${buttonClass.primary} w-full`}
          >
            {busy === "save" ? "Сохраняем…" : "Сохранить"}
          </button>
        }
      >
        <div className="space-y-3">
          {canPlan && (
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
                <span className="mb-1.5 block text-sm text-ink-2">Статус</span>
                <select value={form.status} onChange={(e) => setForm({ ...form, status: e.target.value as FieldCustomerStatus })} className={inputClass}>
                  {(["Active", "Problem", "Inactive"] as FieldCustomerStatus[]).map((s) => (
                    <option key={s} value={s}>
                      {customerStatusLabel[s][0]}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          )}
          {canPlan && (
            <label className="block">
              <span className="mb-1.5 block text-sm text-ink-2">План на месяц, сум</span>
              <input inputMode="numeric" value={form.monthlyTarget} onChange={(e) => setForm({ ...form, monthlyTarget: e.target.value.replace(/[^\d]/g, "") })} className={inputClass} placeholder="не задан" />
            </label>
          )}
          <label className="block">
            <span className="mb-1.5 block text-sm text-ink-2">Контактное лицо</span>
            <input value={form.contactPerson ?? ""} onChange={(e) => setForm({ ...form, contactPerson: e.target.value })} maxLength={200} className={inputClass} />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm text-ink-2">Телефон</span>
            <input type="tel" value={form.phone ?? ""} onChange={(e) => setForm({ ...form, phone: e.target.value })} maxLength={32} className={inputClass} />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm text-ink-2">Заметка</span>
            <textarea value={form.note ?? ""} onChange={(e) => setForm({ ...form, note: e.target.value })} rows={3} maxLength={2000} className={`${inputClass} h-auto py-2.5`} />
          </label>
          {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
        </div>
      </Sheet>
    </>
  );
}
