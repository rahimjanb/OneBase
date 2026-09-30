"use client";

import { useCallback, useEffect, useState } from "react";
import { RefreshCw, Trash2 } from "lucide-react";
import { Note, Section } from "./bits";
import { dateTime, num } from "@/lib/sales/format";
import { linkoEntityLabels } from "@/lib/integrations";
import type { SyncStatus } from "@/lib/sales/types";

type Region = { id: string; linkoBranchId: number; name: string };
type Agent = { linkoUserId: number; name: string; isActive: boolean; job: string | null; hasSales: boolean; isSalesRep: boolean; isVacancy: boolean };
type Setup = { regions: Region[]; agents: Agent[]; categories: { id: number; name: string }[]; targets: Record<string, number> };

const control = "h-8 rounded-md border border-line bg-surface px-2 text-sm text-ink focus:border-accent focus:outline-none";
const input = `${control} w-full`;
const button = "inline-flex h-8 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-xs font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-8 items-center gap-1.5 rounded-lg bg-accent px-3 text-xs font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

async function bff<T = unknown>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/bff/api/sales/${path}`, {
    ...init,
    headers: init?.body instanceof FormData ? init.headers : { "Content-Type": "application/json", ...init?.headers },
  });
  if (!response.ok) {
    const text = await response.text();
    let message = text;
    try {
      const json = JSON.parse(text);
      message = json.error ?? json.detail ?? json.title ?? text;
    } catch {
      // не JSON
    }
    throw new Error(response.status === 403 ? "Недостаточно прав (нужно sales.manage)" : message || `Ошибка ${response.status}`);
  }
  return (response.status === 204 || response.status === 202 ? undefined : await response.json()) as T;
}

const targetLabels: Record<string, { label: string; hint: string; percent?: boolean }> = {
  visit_conversion: { label: "Конверсия визита", hint: "доля визитов с заказом", percent: true },
  revenue_per_outlet: { label: "Выручка на ТТ, сум", hint: "выручка / АКБ" },
  akb_per_agent: { label: "АКБ на агента", hint: "АКБ / действующие ТП" },
  categories_per_outlet: { label: "Категорий на ТТ", hint: "порог «узкого ассортимента»" },
};

export function SetupClient() {
  const [setup, setSetup] = useState<Setup | null>(null);
  const [status, setStatus] = useState<SyncStatus | null>(null);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);

  const load = useCallback(async () => {
    try {
      const [s, st] = await Promise.all([bff<Setup>("setup"), bff<SyncStatus>("status")]);
      setSetup(s);
      setStatus(st);
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  // Пока идёт синхронизация — обновляем статус.
  useEffect(() => {
    if (!status?.isRunning) return;
    const timer = setInterval(async () => {
      const st = await bff<SyncStatus>("status").catch(() => null);
      if (st) setStatus(st);
      if (st && !st.isRunning) void load();
    }, 3000);
    return () => clearInterval(timer);
  }, [status?.isRunning, load]);

  const run = async (action: () => Promise<unknown>, ok: string) => {
    setMessage(null);
    try {
      await action();
      setMessage({ tone: "ok", text: ok });
      await load();
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    }
  };

  if (!setup) return <div className="h-40 animate-pulse rounded-xl bg-muted" />;

  return (
    <>
      {message && (
        <div className={`rounded-lg px-4 py-2.5 text-sm ${message.tone === "ok" ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>{message.text}</div>
      )}
      <SyncSection status={status} run={run} />
      <AgentsSection agents={setup.agents} />
      <TargetsSection targets={setup.targets} run={run} />
    </>
  );
}

type Run = (action: () => Promise<unknown>, ok: string) => Promise<void>;

function SyncSection({ status, run }: { status: SyncStatus | null; run: Run }) {
  return (
    <Section
      title="Синхронизация с Linko"
      hint={status?.dataAsOf ? `данные по ${dateTime(status.dataAsOf)}` : "данных ещё нет"}
      actions={
        <>
          <button className={primary} disabled={!status?.configured || status?.isRunning} onClick={() => run(() => bff("sync", { method: "POST" }), "Синхронизация запущена")}>
            <RefreshCw className={`size-3.5 ${status?.isRunning ? "animate-spin" : ""}`} />
            {status?.isRunning ? "Обновляется…" : "Обновить"}
          </button>
          <button className={button} disabled={!status?.configured || status?.isRunning} onClick={() => run(() => bff("sync?full=true", { method: "POST" }), "Полная перезагрузка запущена")}>
            Полная перезагрузка
          </button>
        </>
      }
    >
      <p className={`mb-2 text-sm ${status?.configured ? "text-ink-2" : "text-warn"}`}>
        {status?.configured ? "Адрес, токен и проверка подключения — в " : "Linko не настроен или выключен. Задайте адрес и токен в "}
        <a href="/settings/integrations/sales/linko" className="font-medium text-accent-strong hover:underline">
          Настройки → Интеграции → Продажи → Linko
        </a>
        .
      </p>
      <div className="overflow-x-auto">
        <table className="w-full min-w-max text-sm">
          <thead>
            <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
              <th className="py-2 pr-3 font-semibold">Сущность</th>
              <th className="py-2 pr-3 font-semibold">Последний успех</th>
              <th className="py-2 pr-3 text-right font-semibold" title="Сколько записей пришло при последней загрузке: при обычном обновлении — только изменения">Строк за раз</th>
              <th className="py-2 font-semibold">Ошибка</th>
            </tr>
          </thead>
          <tbody>
            {status?.entities.map((e) => (
              <tr key={e.entity} className="border-b border-line last:border-b-0">
                <td className="py-2 pr-3 font-medium text-ink">{linkoEntityLabels[e.entity] ?? e.entity}</td>
                <td className="py-2 pr-3 tabular-nums text-ink-2">{dateTime(e.lastSuccessAt)}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{num(e.lastRows)}</td>
                <td className="py-2 text-xs text-bad">{e.lastError ?? ""}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Note>Автоматически данные обновляются каждые 20 минут. «Обновить» подтягивает изменения, «Полная перезагрузка» — заново всё окно истории.</Note>
    </Section>
  );
}

/** Справочник ТП — из Linko, только для просмотра: ТП — по должности, вакансия — «вакант» в имени или ID 0. */
function AgentsSection({ agents }: { agents: Agent[] }) {
  const [all, setAll] = useState(false);
  const visible = agents.filter((a) => all || a.isSalesRep || a.hasSales);
  return (
    <Section
      title="Справочник ТП"
      hint="из Linko · только просмотр"
      actions={
        <label className="flex items-center gap-2 text-xs text-ink-2">
          <input type="checkbox" checked={all} onChange={(e) => setAll(e.target.checked)} />
          все пользователи Linko
        </label>
      }
    >
      <div className="overflow-x-auto">
        <table className="w-full min-w-[640px] text-sm">
          <thead>
            <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
              <th className="py-2 pr-2 font-semibold">Пользователь Linko</th>
              <th className="py-2 pr-2 font-semibold">Должность</th>
              <th className="py-2 pr-2 font-semibold">ТП</th>
              <th className="py-2 pr-2 font-semibold">Вакансия</th>
              <th className="py-2 font-semibold">Продажи</th>
            </tr>
          </thead>
          <tbody>
            {visible.map((a) => (
              <tr key={a.linkoUserId} className="border-b border-line last:border-b-0">
                <td className="py-1.5 pr-2">
                  <span className="block font-medium text-ink">{a.name || `Пользователь ${a.linkoUserId}`}</span>
                  <span className="text-[11px] text-ink-3">
                    ID {a.linkoUserId}
                    {!a.isActive ? " · неактивен в Linko" : ""}
                  </span>
                </td>
                <td className="py-1.5 pr-2 text-ink-2">{a.job ?? "—"}</td>
                <td className="py-1.5 pr-2">{a.isSalesRep ? "да" : <span className="text-ink-3">нет</span>}</td>
                <td className="py-1.5 pr-2">{a.isVacancy ? <span className="text-warn">вакансия</span> : <span className="text-ink-3">—</span>}</td>
                <td className="py-1.5 text-ink-2">{a.hasSales ? "есть" : "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Note>
        Справочник ведётся в Linko. ТП — пользователи с должностью «Агент»; продающие без этой должности показываются в регионе отдельным списком «Продают, но
        не заведены в справочнике». Вакансия — «вакант» в имени или ID 0: в рейтинг и численность ТП не входит. Регион ТП — по филиалу, где у него больше продаж.
      </Note>
    </Section>
  );
}

function TargetsSection({ targets, run }: { targets: Record<string, number>; run: Run }) {
  const [values, setValues] = useState(targets);
  return (
    <Section
      title="Цели"
      actions={
        <button className={primary} onClick={() => run(() => bff("setup/targets", { method: "PUT", body: JSON.stringify(values) }), "Цели сохранены")}>
          Сохранить
        </button>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {Object.entries(targetLabels).map(([key, t]) => (
          <label key={key} className="block">
            <span className="block text-sm text-ink">{t.label}</span>
            <span className="mb-1 block text-[11px] text-ink-3">{t.hint}</span>
            <input
              className={input}
              type="number"
              step="any"
              value={t.percent ? Math.round((values[key] ?? 0) * 1000) / 10 : (values[key] ?? 0)}
              onChange={(e) => setValues({ ...values, [key]: t.percent ? Number(e.target.value) / 100 : Number(e.target.value) })}
            />
            {t.percent && <span className="text-[11px] text-ink-3">в процентах</span>}
          </label>
        ))}
      </div>
    </Section>
  );
}
