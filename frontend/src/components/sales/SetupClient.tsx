"use client";

import { useCallback, useEffect, useState } from "react";
import { RefreshCw, Trash2 } from "lucide-react";
import { useT } from "@/i18n/client";
import { CancelSyncButton } from "./CancelSyncButton";
import { SyncProgressBar } from "./SyncProgressBar";
import { Note, Section } from "./bits";
import { dateTime, num } from "@/lib/sales/format";
import { linkoEntityLabels } from "@/lib/integrations";
import type { SyncStatus } from "@/lib/sales/types";

type Direction = { id: string; name: string; kind: "RegionalManager" | "Channel"; managerName: string | null; description: string | null; sortOrder: number };
/** note — у региона вне вторички («Завод», «К К Мерч») и старого филиала: их направление, СВР и дилер в отчётах не используются. */
type Region = {
  id: string;
  linkoBranchId: number;
  name: string;
  directionId: string | null;
  supervisorName: string | null;
  dealerName: string | null;
  note: string | null;
};
type Agent = { linkoUserId: number; name: string; isActive: boolean; job: string | null; hasSales: boolean; isSalesRep: boolean; isVacancy: boolean };
/** owners — дилер и СВР, которые ведёт привязка Google Sheets (поле → правило): здесь они только для просмотра. */
type Setup = { directions: Direction[]; regions: Region[]; agents: Agent[]; categories: { id: number; name: string }[]; targets: Record<string, number>; owners?: Record<string, string> };

const control = "h-8 rounded-md border border-line bg-surface px-2 text-sm text-ink focus:border-accent focus:outline-none disabled:cursor-not-allowed disabled:opacity-60";
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

// Подписи целей — русские ключи словаря, переводятся при выводе (TargetsSection).
const targetLabels: Record<string, { label: string; hint: string; percent?: boolean }> = {
  visit_conversion: { label: "Конверсия визита", hint: "заказы ТП ÷ выполненные визиты", percent: true },
  revenue_per_outlet: { label: "Выручка на ТТ, сум", hint: "выручка / АКБ" },
  akb_per_agent: { label: "АКБ на агента", hint: "АКБ / ТП месяца без вакансий" },
  categories_per_outlet: { label: "Категорий на ТТ", hint: "порог «узкого ассортимента»" },
};

export function SetupClient() {
  const t = useT();
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
        <div className={`rounded-lg px-4 py-2.5 text-sm ${message.tone === "ok" ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>{t(message.text)}</div>
      )}
      <SyncSection status={status} run={run} />
      <DirectionsSection directions={setup.directions} run={run} />
      <RegionsSection regions={setup.regions} directions={setup.directions} owners={setup.owners ?? {}} run={run} />
      <AgentsSection agents={setup.agents} />
      <TargetsSection targets={setup.targets} run={run} />
    </>
  );
}

type Run = (action: () => Promise<unknown>, ok: string) => Promise<void>;

function SyncSection({ status, run }: { status: SyncStatus | null; run: Run }) {
  const t = useT();
  return (
    <Section
      title={t("Синхронизация с Linko")}
      hint={status?.dataAsOf ? t("загружено {date}", { date: dateTime(status.dataAsOf) }) : t("данных ещё нет")}
      actions={
        <>
          <button className={primary} disabled={!status?.configured || status?.isRunning} onClick={() => run(() => bff("sync", { method: "POST" }), "Синхронизация запущена")}>
            <RefreshCw className={`size-3.5 ${status?.isRunning ? "animate-spin" : ""}`} />
            {status?.isRunning ? t("Обновляется…") : t("Обновить")}
          </button>
          <button className={button} disabled={!status?.configured || status?.isRunning} onClick={() => run(() => bff("sync?full=true", { method: "POST" }), "Полная перезагрузка запущена")}>
            {t("Полная перезагрузка")}
          </button>
          {status?.isRunning && <CancelSyncButton cancelling={status.isCancelling} />}
        </>
      }
    >
      {status?.isRunning && (
        <div className="mb-4 rounded-lg border border-line p-3">
          <SyncProgressBar progress={status.progress} />
        </div>
      )}
      <p className={`mb-2 text-sm ${status?.configured ? "text-ink-2" : "text-warn"}`}>
        {status?.configured ? t("Адрес, токен и проверка подключения — в ") : t("Linko не настроен или выключен. Задайте адрес и токен в ")}
        <a href="/settings/integrations/sales/linko" className="font-medium text-accent-strong hover:underline">
          {t("Настройки → Интеграции → Продажи → Linko")}
        </a>
        .
      </p>
      <div className="overflow-x-auto">
        <table className="w-full min-w-max text-sm">
          <thead>
            <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
              <th className="py-2 pr-3 font-semibold">{t("Сущность")}</th>
              <th className="py-2 pr-3 font-semibold">{t("Последний успех")}</th>
              <th className="py-2 pr-3 text-right font-semibold" title={t("Сколько записей пришло при последней загрузке: при обычном обновлении — только изменения")}>
                {t("Строк за раз")}
              </th>
              <th className="py-2 font-semibold">{t("Ошибка")}</th>
            </tr>
          </thead>
          <tbody>
            {status?.entities.map((e) => (
              <tr key={e.entity} className="border-b border-line last:border-b-0">
                <td className="py-2 pr-3 font-medium text-ink">{t(linkoEntityLabels[e.entity] ?? e.entity)}</td>
                <td className="py-2 pr-3 tabular-nums text-ink-2">{dateTime(e.lastSuccessAt)}</td>
                <td className="py-2 pr-3 text-right tabular-nums">{num(e.lastRows)}</td>
                <td className="py-2 text-xs text-bad">{e.lastError ? t(e.lastError) : ""}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Note>{t("Автоматически данные обновляются в 11:00 и 17:00 (Ташкент). «Обновить» подтягивает изменения сразу, «Полная перезагрузка» — заново всё окно истории.")}</Note>
    </Section>
  );
}

/** Направления — РМ и отдельные каналы (Базар, ключевые клиенты): в Linko их нет, ведутся здесь. */
function DirectionsSection({ directions, run }: { directions: Direction[]; run: Run }) {
  const t = useT();
  const empty = { name: "", kind: "RegionalManager" as Direction["kind"], managerName: "", description: "" };
  const [draft, setDraft] = useState(empty);
  return (
    <Section title={t("Направления")} hint={t("региональные менеджеры (РМ 1–4) и отдельные каналы (Базар, ключевые клиенты)")}>
      {directions.length > 0 && (
        <div className="space-y-2">
          {/* key — все поля: после сохранения строка заново берёт значения с сервера (обрезанные пробелы, пустое — null). */}
          {directions.map((d) => (
            <DirectionRow key={JSON.stringify(d)} direction={d} run={run} />
          ))}
        </div>
      )}
      <div className="mt-4 grid gap-2 rounded-lg border border-dashed border-line p-3 sm:grid-cols-[1fr_150px_1fr_1fr_auto]">
        <input className={input} placeholder={t("Название (РМ 1, Базар…)")} value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
        <select className={input} aria-label={t("Тип направления")} value={draft.kind} onChange={(e) => setDraft({ ...draft, kind: e.target.value as Direction["kind"] })}>
          <option value="RegionalManager">{t("РМ")}</option>
          <option value="Channel">{t("Канал")}</option>
        </select>
        <input className={input} placeholder={t("Руководитель")} value={draft.managerName} onChange={(e) => setDraft({ ...draft, managerName: e.target.value })} />
        <input className={input} placeholder={t("Описание (зона)")} value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} />
        <button
          className={primary}
          disabled={!draft.name.trim()}
          onClick={() =>
            run(async () => {
              await bff("setup/directions", { method: "POST", body: JSON.stringify({ ...draft, sortOrder: directions.length + 1 }) });
              setDraft(empty);
            }, "Направление добавлено")
          }
        >
          {t("Добавить")}
        </button>
      </div>
      <Note>
        {t(
          "Направление объединяет регионы: карточки РМ в республике, страница РМ, охват РМ в «Ассортименте» и фильтр в «Проблемных агентах». Каналы показываются после РМ. Удалённое направление снимается с регионов, сами регионы остаются.",
        )}
      </Note>
    </Section>
  );
}

function DirectionRow({ direction, run }: { direction: Direction; run: Run }) {
  const t = useT();
  const [d, setD] = useState(direction);
  const dirty = JSON.stringify(d) !== JSON.stringify(direction);
  return (
    <div className="grid items-center gap-2 sm:grid-cols-[1fr_150px_1fr_1fr_70px_auto_auto]">
      <input className={input} aria-label={t("Название")} value={d.name} onChange={(e) => setD({ ...d, name: e.target.value })} />
      <select className={input} aria-label={t("Тип направления")} value={d.kind} onChange={(e) => setD({ ...d, kind: e.target.value as Direction["kind"] })}>
        <option value="RegionalManager">{t("РМ")}</option>
        <option value="Channel">{t("Канал")}</option>
      </select>
      <input className={input} placeholder={t("Руководитель")} value={d.managerName ?? ""} onChange={(e) => setD({ ...d, managerName: e.target.value })} />
      <input className={input} placeholder={t("Описание (зона)")} value={d.description ?? ""} onChange={(e) => setD({ ...d, description: e.target.value })} />
      <input className={input} type="number" title={t("Порядок")} aria-label={t("Порядок")} value={d.sortOrder} onChange={(e) => setD({ ...d, sortOrder: Number(e.target.value) })} />
      <button className={primary} disabled={!dirty || !d.name.trim()} onClick={() => run(() => bff(`setup/directions/${d.id}`, { method: "PUT", body: JSON.stringify(d) }), "Сохранено")}>
        {t("Сохранить")}
      </button>
      <button
        className={button}
        aria-label={t("Удалить направление")}
        onClick={() =>
          confirm(t("Удалить «{name}»? Регионы останутся без направления.", { name: direction.name })) &&
          run(() => bff(`setup/directions/${d.id}`, { method: "DELETE" }), "Удалено")
        }
      >
        <Trash2 className="size-3.5" />
      </button>
    </div>
  );
}

/**
 * Регионы — филиалы Linko (создаются сами при синхронизации): направление, СВР и дилер. Название — из Linko (последний заказ филиала),
 * здесь не меняется.
 */
function RegionsSection({ regions, directions, owners, run }: { regions: Region[]; directions: Direction[]; owners: Record<string, string>; run: Run }) {
  const t = useT();
  const fromSheets = <span className="ml-1 font-normal normal-case tracking-normal text-accent-strong">· Google Sheets</span>;
  return (
    <Section title={t("Регионы")} hint={t("филиалы Linko · направление, СВР и дилер — данные OneBase")}>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[760px] text-sm">
          <thead>
            <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
              <th className="py-2 pr-2 font-semibold">{t("Регион")}</th>
              <th className="py-2 pr-2 font-semibold">{t("Направление")}</th>
              <th className="py-2 pr-2 font-semibold" title={owners.supervisor}>
                {t("СВР")}
                {owners.supervisor && fromSheets}
              </th>
              <th className="py-2 pr-2 font-semibold" title={owners.dealer}>
                {t("Дилер")}
                {owners.dealer && fromSheets}
              </th>
              <th />
            </tr>
          </thead>
          <tbody>
            {regions.map((r) => (
              <RegionRow key={JSON.stringify(r)} region={r} directions={directions} owners={owners} run={run} />
            ))}
          </tbody>
        </table>
      </div>
      <Note>
        {t(
          "Регион создаётся сам из филиала Linko и при «Очистить данные и загрузить заново» не удаляется — направление, СВР, дилер и планы РОП и «Завод» остаются. Название — как в последнем заказе филиала: переименуют филиал в Linko — после синхронизации переименуется и регион. Дилер региона виден в «Первичке» и «Аутстоке». «Завод», «К К Мерч» и старые филиалы («эски») регионами вторички не являются.",
        )}
      </Note>
      {(owners.dealer || owners.supervisor) && (
        <Note>
          {[owners.supervisor && t("СВР: {source}", { source: owners.supervisor }), owners.dealer && t("дилер: {source}", { source: owners.dealer })]
            .filter(Boolean)
            .join("; ")}{" "}
          {t("— здесь только просмотр, меняйте в таблице (или отключите привязку в «Настройки → Источники данных»).")}
        </Note>
      )}
    </Section>
  );
}

function RegionRow({ region, directions, owners, run }: { region: Region; directions: Direction[]; owners: Record<string, string>; run: Run }) {
  const t = useT();
  const [r, setR] = useState(region);
  const dirty = r.directionId !== region.directionId || (r.supervisorName ?? "") !== (region.supervisorName ?? "") || (r.dealerName ?? "") !== (region.dealerName ?? "");
  const inactive = region.note != null;
  return (
    <tr className={`border-b border-line last:border-b-0 ${inactive ? "text-ink-3" : ""}`}>
      <td className="py-1.5 pr-2">
        <span className={`block font-medium ${inactive ? "" : "text-ink"}`}>{region.name}</span>
        <span className="text-[11px] text-ink-3">
          {t("филиал Linko {id}", { id: region.linkoBranchId })}
          {region.note ? ` · ${t(region.note)}` : ""}
        </span>
      </td>
      <td className="py-1.5 pr-2">
        <select className={input} aria-label={t("Направление")} disabled={inactive} value={r.directionId ?? ""} onChange={(e) => setR({ ...r, directionId: e.target.value || null })}>
          <option value="">{t("— без направления —")}</option>
          {directions.map((d) => (
            <option key={d.id} value={d.id}>
              {d.name}
            </option>
          ))}
        </select>
      </td>
      <td className="py-1.5 pr-2">
        <input
          className={input}
          aria-label={t("СВР")}
          disabled={inactive || !!owners.supervisor}
          title={owners.supervisor}
          value={r.supervisorName ?? ""}
          onChange={(e) => setR({ ...r, supervisorName: e.target.value })}
        />
      </td>
      <td className="py-1.5 pr-2">
        <input
          className={input}
          aria-label={t("Дилер")}
          disabled={inactive || !!owners.dealer}
          title={owners.dealer}
          value={r.dealerName ?? ""}
          onChange={(e) => setR({ ...r, dealerName: e.target.value })}
        />
      </td>
      <td className="py-1.5 text-right">
        {!inactive && (
          <button
            className={primary}
            disabled={!dirty}
            onClick={() =>
              run(
                () => bff(`setup/regions/${r.id}`, { method: "PUT", body: JSON.stringify({ directionId: r.directionId, supervisorName: r.supervisorName, dealerName: r.dealerName }) }),
                "Регион сохранён",
              )
            }
          >
            {t("Сохранить")}
          </button>
        )}
      </td>
    </tr>
  );
}

/** Справочник ТП — из Linko, только для просмотра: ТП — по должности, вакансия — «вакан» в имени («вакант», «Вакан …») или ID 0. */
function AgentsSection({ agents }: { agents: Agent[] }) {
  const t = useT();
  const [all, setAll] = useState(false);
  const visible = agents.filter((a) => all || a.isSalesRep || a.hasSales);
  return (
    <Section
      title={t("Справочник ТП")}
      hint={t("из Linko · только просмотр")}
      actions={
        <label className="flex items-center gap-2 text-xs text-ink-2">
          <input type="checkbox" checked={all} onChange={(e) => setAll(e.target.checked)} />
          {t("все пользователи Linko")}
        </label>
      }
    >
      <div className="overflow-x-auto">
        <table className="w-full min-w-[640px] text-sm">
          <thead>
            <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
              <th className="py-2 pr-2 font-semibold">{t("Пользователь Linko")}</th>
              <th className="py-2 pr-2 font-semibold">{t("Должность")}</th>
              <th className="py-2 pr-2 font-semibold">{t("ТП")}</th>
              <th className="py-2 pr-2 font-semibold">{t("Вакансия")}</th>
              <th className="py-2 font-semibold">{t("Продажи")}</th>
            </tr>
          </thead>
          <tbody>
            {visible.map((a) => (
              <tr key={a.linkoUserId} className="border-b border-line last:border-b-0">
                <td className="py-1.5 pr-2">
                  <span className="block font-medium text-ink">{a.name || t("Пользователь {id}", { id: a.linkoUserId })}</span>
                  <span className="text-[11px] text-ink-3">
                    ID {a.linkoUserId}
                    {!a.isActive ? ` · ${t("неактивен в Linko")}` : ""}
                  </span>
                </td>
                <td className="py-1.5 pr-2 text-ink-2">{a.job ?? "—"}</td>
                <td className="py-1.5 pr-2">{a.isSalesRep ? t("да") : <span className="text-ink-3">{t("нет")}</span>}</td>
                <td className="py-1.5 pr-2">{a.isVacancy ? <span className="text-warn">{t("вакансия")}</span> : <span className="text-ink-3">—</span>}</td>
                <td className="py-1.5 text-ink-2">{a.hasSales ? t("есть") : "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Note>
        {t(
          "Справочник ведётся в Linko. ТП — пользователи с должностью «Агент»; продающие без этой должности показываются в регионе отдельным списком «Продают, но не заведены в справочнике». Вакансия — «вакан» в имени («вакант», «Вакан …») или ID 0: в рейтинг и численность ТП не входит. Регион ТП — по филиалу, где у него больше продаж.",
        )}
      </Note>
    </Section>
  );
}

function TargetsSection({ targets, run }: { targets: Record<string, number>; run: Run }) {
  const t = useT();
  const [values, setValues] = useState(targets);
  return (
    <Section
      title={t("Цели")}
      actions={
        <button className={primary} onClick={() => run(() => bff("setup/targets", { method: "PUT", body: JSON.stringify(values) }), "Цели сохранены")}>
          {t("Сохранить")}
        </button>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        {Object.entries(targetLabels).map(([key, target]) => (
          <label key={key} className="block">
            <span className="block text-sm text-ink">{t(target.label)}</span>
            <span className="mb-1 block text-[11px] text-ink-3">{t(target.hint)}</span>
            <input
              className={input}
              type="number"
              step="any"
              value={target.percent ? Math.round((values[key] ?? 0) * 1000) / 10 : (values[key] ?? 0)}
              onChange={(e) => setValues({ ...values, [key]: target.percent ? Number(e.target.value) / 100 : Number(e.target.value) })}
            />
            {target.percent && <span className="text-[11px] text-ink-3">{t("в процентах")}</span>}
          </label>
        ))}
      </div>
    </Section>
  );
}
