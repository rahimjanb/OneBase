"use client";

import Link from "next/link";
import { useState } from "react";
import { CheckCheck, Plus } from "lucide-react";
import type { FieldJointVisit, FieldNotification, FieldSettings } from "@/lib/field/types";
import { Chip, Empty, buttonClass, inputClass } from "./ui";
import { Sheet } from "./Sheet";
import { companyToday, fieldApi, useAction } from "./hooks";

/** Настройки Sales Base (РМ). */
export function SettingsForm({ initial }: { initial: FieldSettings }) {
  const { busy, error, run } = useAction();
  const [form, setForm] = useState(initial);
  const [saved, setSaved] = useState(false);
  const field = (key: keyof FieldSettings, label: string, hint: string, suffix: string) => (
    <label className="block">
      <span className="mb-1 block text-sm font-medium text-ink">{label}</span>
      <span className="mb-1.5 block text-xs text-ink-3">{hint}</span>
      <span className="flex items-center gap-2">
        <input
          inputMode="numeric"
          value={String(form[key] ?? "")}
          onChange={(e) => {
            setSaved(false);
            setForm({ ...form, [key]: Number(e.target.value.replace(/[^\d]/g, "") || 0) });
          }}
          className={`${inputClass} max-w-[160px]`}
        />
        <span className="text-sm text-ink-3">{suffix}</span>
      </span>
    </label>
  );

  return (
    <div className="space-y-4">
      <section className="space-y-4 rounded-xl border border-line bg-surface p-4 sm:p-5">
        <h2 className="text-base font-semibold text-ink">Визиты и геолокация</h2>
        <div className="grid gap-4 sm:grid-cols-2">
          {field("geoRadiusM", "Радиус точки", "В его пределах визит отмечается «на месте».", "м")}
          {field("gpsToleranceM", "Допуск на неточность GPS", "Сколько неточности телефона прощается сверх радиуса.", "м")}
        </div>
      </section>
      <section className="space-y-4 rounded-xl border border-line bg-surface p-4 sm:p-5">
        <h2 className="text-base font-semibold text-ink">Маршруты</h2>
        <div className="grid gap-4 sm:grid-cols-2">
          {field("maxRoutePoints", "Точек в автоматическом маршруте", "Сколько точек подбирать, если нет плана визитов Linko.", "шт")}
          {field("visitMinutes", "Длительность визита", "Для расчёта времени маршрута.", "мин")}
          {field("travelSpeedKmh", "Средняя скорость в пути", "С учётом города и пробок.", "км/ч")}
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-ink">Начало дня</span>
            <span className="mb-1.5 block text-xs text-ink-3">От него считается плановое время точек.</span>
            <input type="time" value={form.dayStart.slice(0, 5)} onChange={(e) => setForm({ ...form, dayStart: `${e.target.value}:00` })} className={`${inputClass} max-w-[160px]`} />
          </label>
        </div>
      </section>
      <section className="space-y-4 rounded-xl border border-line bg-surface p-4 sm:p-5">
        <h2 className="text-base font-semibold text-ink">AI-планирование</h2>
        <div className="grid gap-4 sm:grid-cols-2">
          {field("declinePct", "Падение продаж точки", "За 30 дней к прошлым 30 — тогда AI предложит визит.", "%")}
          {field("declineMinSales", "Минимальные продажи для анализа", "Точки меньше этого (за 30 дней) не рассматриваются.", "сум")}
          {field("notVisitedDays", "Давно не посещали", "Точка с продажами без визита дольше этого.", "дн.")}
          {field("behindPlanPct", "Отставание агента от темпа плана", "Тогда супервайзер получит рекомендацию.", "п.п.")}
        </div>
        <label className="flex items-start gap-3 rounded-lg bg-muted p-3">
          <input type="checkbox" checked={form.autoPlanning} onChange={(e) => setForm({ ...form, autoPlanning: e.target.checked })} className="mt-0.5 size-5" />
          <span>
            <span className="block text-sm font-medium text-ink">Автопланирование</span>
            <span className="block text-xs text-ink-3">Рекомендации AI сразу становятся задачами без подтверждения супервайзера (кроме переназначения точек). По умолчанию выключено.</span>
          </span>
        </label>
      </section>
      {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      {saved && <p className="rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">Сохранено</p>}
      <button type="button" disabled={busy !== null} onClick={() => run("save", () => fieldApi.put("settings", form), { onDone: () => setSaved(true) })} className={buttonClass.primary}>
        {busy === "save" ? "Сохраняем…" : "Сохранить настройки"}
      </button>
    </div>
  );
}

/** Уведомления: отметить прочитанным при переходе, «прочитать все». */
export function NotificationList({ items }: { items: FieldNotification[] }) {
  const { busy, run } = useAction();
  if (items.length === 0) return <Empty>Уведомлений пока нет.</Empty>;
  return (
    <div className="space-y-3">
      {items.some((n) => !n.read) && (
        <button type="button" disabled={busy !== null} onClick={() => run("all", () => fieldApi.post("notifications/read-all"))} className={buttonClass.outline}>
          <CheckCheck className="size-4" /> Прочитать все
        </button>
      )}
      <ul className="overflow-hidden rounded-xl border border-line bg-surface">
        {items.map((n) => {
          const body = (
            <>
              <div className="flex items-start gap-2">
                {!n.read && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-accent" />}
                <div className="min-w-0 flex-1">
                  <div className={`text-sm ${n.read ? "text-ink-2" : "font-medium text-ink"}`}>{n.title}</div>
                  {n.body && <div className="mt-0.5 text-xs text-ink-3">{n.body}</div>}
                </div>
                <span className="shrink-0 text-xs text-ink-3">{new Date(n.createdAt).toLocaleString("ru-RU", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}</span>
              </div>
            </>
          );
          return (
            <li key={n.id} className="border-b border-line last:border-0">
              {n.link ? (
                <Link href={n.link} onClick={() => !n.read && fieldApi.post(`notifications/${n.id}/read`).catch(() => undefined)} className="block px-4 py-3 hover:bg-muted">
                  {body}
                </Link>
              ) : (
                <div className="px-4 py-3">{body}</div>
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}

/** Совместные выезды: список и создание (супервайзер/РМ). */
export function JointVisits({ items, members, canPlan }: { items: FieldJointVisit[]; members: { id: string; name: string; role: string }[]; canPlan: boolean }) {
  const { busy, error, run } = useAction();
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ date: companyToday(), time: "10:00", objective: "", participantIds: [] as string[] });
  const [result, setResult] = useState<{ visit: FieldJointVisit; text: string } | null>(null);

  return (
    <div className="space-y-3">
      {canPlan && (
        <button type="button" onClick={() => setOpen(true)} className={buttonClass.primary}>
          <Plus className="size-4" /> Совместный выезд
        </button>
      )}
      {error && <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      {items.length === 0 ? (
        <Empty>Совместных выездов нет.</Empty>
      ) : (
        <ul className="space-y-2">
          {items.map((j) => (
            <li key={j.id} className="rounded-xl border border-line bg-surface p-4">
              <div className="flex flex-wrap items-center gap-2 text-sm">
                <span className="font-medium text-ink">
                  {new Date(`${j.date}T00:00:00Z`).toLocaleDateString("ru-RU", { day: "numeric", month: "long", timeZone: "UTC" })}
                  {j.time ? `, ${j.time.slice(0, 5)}` : ""}
                </span>
                <Chip tone={j.status === "Done" ? "ok" : j.status === "Cancelled" ? "muted" : "accent"}>{j.status === "Done" ? "Проведён" : j.status === "Cancelled" ? "Отменён" : "Запланирован"}</Chip>
              </div>
              <div className="mt-1 text-sm text-ink">{j.objective}</div>
              <div className="mt-1 text-xs text-ink-3">
                {j.participants.map((p) => p.name).join(", ")}
                {j.marketName ? ` · ${j.marketName}` : ""}
              </div>
              {j.result && <div className="mt-1 text-xs text-ink-2">Итог: {j.result}</div>}
              {j.canEdit && j.status === "Planned" && (
                <div className="mt-2 flex gap-2">
                  <button type="button" onClick={() => setResult({ visit: j, text: "" })} className="rounded-lg bg-ok px-3 py-2 text-xs font-medium text-white">
                    Записать итог
                  </button>
                  <button type="button" disabled={busy !== null} onClick={() => run(j.id, () => fieldApi.put(`visits/joint/${j.id}`, { status: "Cancelled" }))} className="rounded-lg border border-line px-3 py-2 text-xs text-ink-2">
                    Отменить
                  </button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}

      <Sheet
        open={open}
        onClose={() => setOpen(false)}
        title="Совместный выезд"
        footer={
          <button
            type="button"
            disabled={busy !== null || !form.objective.trim() || form.participantIds.length < 2}
            onClick={() => run("create", () => fieldApi.post("visits/joint", { ...form, time: form.time ? `${form.time}:00` : null, marketId: null }), { onDone: () => setOpen(false) })}
            className={`${buttonClass.primary} w-full`}
          >
            Назначить
          </button>
        }
      >
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-3">
            <input type="date" min={companyToday()} value={form.date} onChange={(e) => setForm({ ...form, date: e.target.value })} className={inputClass} />
            <input type="time" value={form.time} onChange={(e) => setForm({ ...form, time: e.target.value })} className={inputClass} />
          </div>
          <input value={form.objective} onChange={(e) => setForm({ ...form, objective: e.target.value })} placeholder="Цель: обучение, проблемная точка, переговоры…" className={inputClass} />
          <div>
            <div className="mb-1.5 text-sm text-ink-2">Участники (2–6)</div>
            <div className="flex max-h-56 flex-wrap gap-1.5 overflow-y-auto">
              {members.map((m) => {
                const on = form.participantIds.includes(m.id);
                return (
                  <button
                    key={m.id}
                    type="button"
                    onClick={() => setForm({ ...form, participantIds: on ? form.participantIds.filter((x) => x !== m.id) : [...form.participantIds, m.id] })}
                    className={`rounded-full px-3 py-2 text-xs ${on ? "bg-accent text-white" : "bg-muted text-ink-2"}`}
                  >
                    {m.name}
                    {m.role === "Supervisor" ? " (СВ)" : ""}
                  </button>
                );
              })}
            </div>
          </div>
        </div>
      </Sheet>

      <Sheet
        open={result !== null}
        onClose={() => setResult(null)}
        title="Итог совместного выезда"
        footer={
          <button type="button" disabled={busy !== null || !result?.text.trim()} onClick={() => result && run("result", () => fieldApi.put(`visits/joint/${result.visit.id}`, { status: "Done", result: result.text }), { onDone: () => setResult(null) })} className={`${buttonClass.primary} w-full`}>
            Сохранить
          </button>
        }
      >
        <textarea value={result?.text ?? ""} onChange={(e) => result && setResult({ ...result, text: e.target.value })} rows={4} className={`${inputClass} h-auto py-2.5`} placeholder="Что сделали, договорённости, что поручено агенту" />
      </Sheet>
    </div>
  );
}
