"use client";

import { useCallback, useEffect, useState } from "react";
import { RefreshCw, Trash2 } from "lucide-react";
import { Note, Section } from "./bits";
import { dateTime, num } from "@/lib/sales/format";
import { linkoEntityLabels } from "@/lib/integrations";
import type { SyncStatus } from "@/lib/sales/types";

type Direction = { id: string; name: string; kind: "RegionalManager" | "Channel"; managerName: string | null; description: string | null; sortOrder: number };
type Region = { id: string; linkoBranchId: number; name: string; directionId: string | null; supervisorName: string | null; dealerName: string | null };
type Agent = { linkoUserId: number; name: string; isActive: boolean; job: string | null; hasSales: boolean; inDirectory: boolean; regionId: string | null; isVacancy: boolean; note: string | null };
type Setup = { directions: Direction[]; regions: Region[]; agents: Agent[]; categories: { id: number; name: string }[]; targets: Record<string, number> };

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
      <DirectionsSection directions={setup.directions} run={run} />
      <RegionsSection regions={setup.regions} directions={setup.directions} run={run} />
      <AgentsSection agents={setup.agents} regions={setup.regions} run={run} />
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

function DirectionsSection({ directions, run }: { directions: Direction[]; run: Run }) {
  const [draft, setDraft] = useState({ name: "", kind: "RegionalManager", managerName: "", description: "" });
  return (
    <Section title="Направления" hint="региональные менеджеры и отдельные каналы (Базар, ключевые клиенты)">
      <div className="space-y-2">
        {directions.map((d) => (
          <DirectionRow key={d.id} direction={d} run={run} />
        ))}
      </div>
      <div className="mt-4 grid gap-2 rounded-lg border border-dashed border-line p-3 sm:grid-cols-[1fr_150px_1fr_1fr_auto]">
        <input className={input} placeholder="Название (РМ 1, Базар…)" value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
        <select className={input} value={draft.kind} onChange={(e) => setDraft({ ...draft, kind: e.target.value })}>
          <option value="RegionalManager">РМ</option>
          <option value="Channel">Канал</option>
        </select>
        <input className={input} placeholder="Руководитель" value={draft.managerName} onChange={(e) => setDraft({ ...draft, managerName: e.target.value })} />
        <input className={input} placeholder="Описание" value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} />
        <button
          className={primary}
          disabled={!draft.name.trim()}
          onClick={() =>
            run(async () => {
              await bff("setup/directions", { method: "POST", body: JSON.stringify({ ...draft, sortOrder: directions.length + 1 }) });
              setDraft({ name: "", kind: "RegionalManager", managerName: "", description: "" });
            }, "Направление добавлено")
          }
        >
          Добавить
        </button>
      </div>
    </Section>
  );
}

function DirectionRow({ direction, run }: { direction: Direction; run: Run }) {
  const [d, setD] = useState(direction);
  const dirty = JSON.stringify(d) !== JSON.stringify(direction);
  return (
    <div className="grid items-center gap-2 sm:grid-cols-[1fr_150px_1fr_1fr_70px_auto_auto]">
      <input className={input} value={d.name} onChange={(e) => setD({ ...d, name: e.target.value })} />
      <select className={input} value={d.kind} onChange={(e) => setD({ ...d, kind: e.target.value as Direction["kind"] })}>
        <option value="RegionalManager">РМ</option>
        <option value="Channel">Канал</option>
      </select>
      <input className={input} placeholder="Руководитель" value={d.managerName ?? ""} onChange={(e) => setD({ ...d, managerName: e.target.value })} />
      <input className={input} placeholder="Описание" value={d.description ?? ""} onChange={(e) => setD({ ...d, description: e.target.value })} />
      <input className={input} type="number" title="Порядок" value={d.sortOrder} onChange={(e) => setD({ ...d, sortOrder: Number(e.target.value) })} />
      <button className={primary} disabled={!dirty} onClick={() => run(() => bff(`setup/directions/${d.id}`, { method: "PUT", body: JSON.stringify(d) }), "Сохранено")}>
        Сохранить
      </button>
      <button
        className={button}
        aria-label="Удалить направление"
        onClick={() => confirm(`Удалить «${direction.name}»? Регионы останутся без направления.`) && run(() => bff(`setup/directions/${d.id}`, { method: "DELETE" }), "Удалено")}
      >
        <Trash2 className="size-3.5" />
      </button>
    </div>
  );
}

function RegionsSection({ regions, directions, run }: { regions: Region[]; directions: Direction[]; run: Run }) {
  return (
    <Section title="Регионы" hint="создаются автоматически из филиалов (branch) Linko">
      <div className="overflow-x-auto">
        <table className="w-full min-w-[760px] text-sm">
          <thead>
            <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
              <th className="py-2 pr-2 font-semibold">Регион</th>
              <th className="py-2 pr-2 font-semibold">Направление</th>
              <th className="py-2 pr-2 font-semibold">СВР</th>
              <th className="py-2 pr-2 font-semibold">Дилер</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {regions.map((r) => (
              <RegionRow key={r.id} region={r} directions={directions} run={run} />
            ))}
          </tbody>
        </table>
      </div>
    </Section>
  );
}

function RegionRow({ region, directions, run }: { region: Region; directions: Direction[]; run: Run }) {
  const [r, setR] = useState(region);
  const dirty = JSON.stringify(r) !== JSON.stringify(region);
  return (
    <tr className="border-b border-line last:border-b-0">
      <td className="py-1.5 pr-2">
        <input className={input} value={r.name} onChange={(e) => setR({ ...r, name: e.target.value })} />
        <span className="text-[11px] text-ink-3">branch {r.linkoBranchId}</span>
      </td>
      <td className="py-1.5 pr-2">
        <select className={input} value={r.directionId ?? ""} onChange={(e) => setR({ ...r, directionId: e.target.value || null })}>
          <option value="">— без направления —</option>
          {directions.map((d) => (
            <option key={d.id} value={d.id}>
              {d.name}
            </option>
          ))}
        </select>
      </td>
      <td className="py-1.5 pr-2">
        <input className={input} value={r.supervisorName ?? ""} onChange={(e) => setR({ ...r, supervisorName: e.target.value })} />
      </td>
      <td className="py-1.5 pr-2">
        <input className={input} value={r.dealerName ?? ""} onChange={(e) => setR({ ...r, dealerName: e.target.value })} />
      </td>
      <td className="py-1.5 text-right">
        <button
          className={primary}
          disabled={!dirty}
          onClick={() =>
            run(
              () => bff(`setup/regions/${r.id}`, { method: "PUT", body: JSON.stringify({ name: r.name, directionId: r.directionId, supervisorName: r.supervisorName, dealerName: r.dealerName }) }),
              "Регион сохранён",
            )
          }
        >
          Сохранить
        </button>
      </td>
    </tr>
  );
}

function AgentsSection({ agents, regions, run }: { agents: Agent[]; regions: Region[]; run: Run }) {
  const [onlySelling, setOnlySelling] = useState(true);
  const visible = agents.filter((a) => !onlySelling || a.hasSales || a.inDirectory);
  const missing = agents.filter((a) => a.hasSales && !a.inDirectory);

  return (
    <Section
      title="Агенты (ТП)"
      hint="регион и вакансии — для рейтинга и медиан"
      actions={
        <>
          <label className="flex items-center gap-2 text-xs text-ink-2">
            <input type="checkbox" checked={onlySelling} onChange={(e) => setOnlySelling(e.target.checked)} />
            только с продажами или в справочнике
          </label>
          {missing.length > 0 && (
            <button
              className={primary}
              onClick={() =>
                run(
                  () => Promise.all(missing.map((a) => bff(`setup/agents/${a.linkoUserId}`, { method: "PUT", body: JSON.stringify({ regionId: null, isVacancy: false, note: null }) }))),
                  `Добавлено в справочник: ${missing.length}`,
                )
              }
            >
              Добавить всех продающих ({missing.length})
            </button>
          )}
        </>
      }
    >
      <div className="overflow-x-auto">
        <table className="w-full min-w-[760px] text-sm">
          <thead>
            <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
              <th className="py-2 pr-2 font-semibold">Агент</th>
              <th className="py-2 pr-2 font-semibold">В справочнике</th>
              <th className="py-2 pr-2 font-semibold">Регион</th>
              <th className="py-2 pr-2 font-semibold">Вакансия</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {visible.map((a) => (
              <AgentRow key={a.linkoUserId} agent={a} regions={regions} run={run} />
            ))}
          </tbody>
        </table>
      </div>
      <Note>Регион «по продажам» — определяется автоматически по филиалу, где у агента больше всего выручки. Вакансии не участвуют в рейтинге и медианах.</Note>
    </Section>
  );
}

function AgentRow({ agent, regions, run }: { agent: Agent; regions: Region[]; run: Run }) {
  const [a, setA] = useState(agent);
  const dirty = a.regionId !== agent.regionId || a.isVacancy !== agent.isVacancy;
  const save = () => run(() => bff(`setup/agents/${a.linkoUserId}`, { method: "PUT", body: JSON.stringify({ regionId: a.regionId, isVacancy: a.isVacancy, note: a.note }) }), "Агент сохранён");

  return (
    <tr className="border-b border-line last:border-b-0">
      <td className="py-1.5 pr-2">
        <span className="block font-medium text-ink">{a.name || `Агент ${a.linkoUserId}`}</span>
        <span className="text-[11px] text-ink-3">
          ID {a.linkoUserId}
          {a.job ? ` · ${a.job}` : ""}
          {a.hasSales ? " · есть продажи" : ""}
          {!a.isActive ? " · неактивен в Linko" : ""}
        </span>
      </td>
      <td className="py-1.5 pr-2">
        {agent.inDirectory ? (
          <button className={button} onClick={() => run(() => bff(`setup/agents/${a.linkoUserId}`, { method: "DELETE" }), "Убран из справочника")}>
            Убрать
          </button>
        ) : (
          <button className={button} onClick={save}>
            Добавить
          </button>
        )}
      </td>
      <td className="py-1.5 pr-2">
        <select className={input} value={a.regionId ?? ""} onChange={(e) => setA({ ...a, regionId: e.target.value || null })} disabled={!agent.inDirectory}>
          <option value="">по продажам</option>
          {regions.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </select>
      </td>
      <td className="py-1.5 pr-2">
        <input type="checkbox" checked={a.isVacancy} disabled={!agent.inDirectory} onChange={(e) => setA({ ...a, isVacancy: e.target.checked })} />
      </td>
      <td className="py-1.5 text-right">
        {agent.inDirectory && (
          <button className={primary} disabled={!dirty} onClick={save}>
            Сохранить
          </button>
        )}
      </td>
    </tr>
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
