"use client";

import { useCallback, useEffect, useState } from "react";
import { RefreshCw, Trash2 } from "lucide-react";
import { useT, tNow } from "@/i18n/client";
import { CancelSyncButton } from "./CancelSyncButton";
import { SyncProgressBar } from "./SyncProgressBar";
import { Note, Section } from "./bits";
import { dateTime, kg, monthLabel, num, pct } from "@/lib/sales/format";
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
  /** Другие имена дилера — прежние дилеры и второй дилер склада: по ним план и отчёты из таблиц находят регион. */
  dealerAliases: string[];
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
    throw new Error(response.status === 403 ? tNow("Недостаточно прав (нужно sales.manage)") : message || tNow("Ошибка {status}", { status: response.status }));
  }
  return (response.status === 204 || response.status === 202 ? undefined : await response.json()) as T;
}

// Подписи целей — русские ключи словаря, переводятся при выводе (TargetsSection).
const targetLabels: Record<string, { label: string; hint: string; percent?: boolean }> = {
  visit_conversion: { label: "Конверсия визита", hint: "визиты ТП с заказом ÷ выполненные визиты", percent: true },
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
      <FactoryPlanSection />
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
      <Note>{t("Автоматически данные обновляются каждые 30 минут, ночью в 03:00 — сверка с Linko. «Обновить» подтягивает изменения сразу, «Полная перезагрузка» — заново всё окно истории.")}</Note>
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
  const [aliases, setAliases] = useState((region.dealerAliases ?? []).join(", "));
  const aliasList = aliases.split(/[,;]/).map((a) => a.trim()).filter(Boolean);
  const dirty =
    r.directionId !== region.directionId ||
    (r.supervisorName ?? "") !== (region.supervisorName ?? "") ||
    (r.dealerName ?? "") !== (region.dealerName ?? "") ||
    aliasList.join("|") !== (region.dealerAliases ?? []).join("|");
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
        <input
          className={`${input} mt-1 h-7 text-xs`}
          aria-label={t("Другие имена дилера")}
          placeholder={t("другие имена — через запятую")}
          title={t("Прежние дилеры и второй дилер склада: по ним план и отчёты из таблиц находят регион. При смене дилера прежнее имя добавляется само.")}
          disabled={inactive}
          value={aliases}
          onChange={(e) => setAliases(e.target.value)}
        />
      </td>
      <td className="py-1.5 text-right">
        {!inactive && (
          <button
            className={primary}
            disabled={!dirty}
            onClick={() =>
              run(
                () => bff(`setup/regions/${r.id}`, { method: "PUT", body: JSON.stringify({ directionId: r.directionId, supervisorName: r.supervisorName, dealerName: r.dealerName, dealerAliases: aliasList }) }),
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

/** План «Завод» месяца (GET setup/factory-plan): итоги завода и план РОП по категориям, раскладка по регионам. Ключи словарей — id категорий. */
type FactoryPlan = {
  year: number;
  month: number;
  categories: { id: number; name: string }[];
  totals: Record<string, number>;
  ropTotals: Record<string, number>;
  /** План «Завод» на месяц загружен по регионам (таблица, Data Manager): итоги не используются. */
  explicit: boolean;
  /** Месяц плана РОП, по которому разложено; null — раскладывать не по чему. */
  baseYear: number | null;
  baseMonth: number | null;
  regions: { id: string; name: string; factory: Record<string, number>; rop: Record<string, number> }[];
  updatedAt: string | null;
};

const sumOf = (values: Record<string, number>) => Object.values(values).reduce((a, v) => a + (v || 0), 0);
const kgInput = (text: string | undefined) => (text?.trim() ? Number(text.replace(/\s/g, "").replace(",", ".")) : null);

/**
 * План «Завод»: завод задаёт план итогом страны по категориям — по регионам OneBase раскладывает его сам, по их доле в плане РОП той же
 * категории (как «Полевой контроль»). Переключатель «План «Завод»» во «Вторичке» появляется на месяц, у которого есть итоги и план РОП.
 */
function FactoryPlanSection() {
  const t = useT();
  const [today] = useState(() => new Date());
  const [period, setPeriod] = useState({ year: today.getFullYear(), month: today.getMonth() + 1 });
  const [plan, setPlan] = useState<FactoryPlan | null>(null);
  const [values, setValues] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);

  const show = (view: FactoryPlan) => {
    setPlan(view);
    setValues(Object.fromEntries(view.categories.map((c) => [String(c.id), view.totals[c.id] ? String(view.totals[c.id]) : ""])));
  };

  useEffect(() => {
    let alive = true;
    bff<FactoryPlan>(`setup/factory-plan?year=${period.year}&month=${period.month}`)
      .then((view) => {
        if (alive) show(view);
      })
      .catch((e) => {
        if (alive) setMessage({ tone: "bad", text: (e as Error).message });
      });
    return () => {
      alive = false;
    };
  }, [period.year, period.month]);

  const pick = (next: { year: number; month: number }) => {
    setPlan(null);
    setMessage(null);
    setPeriod(next);
  };

  const save = async () => {
    if (!plan) return;
    setBusy(true);
    setMessage(null);
    try {
      const totals = Object.fromEntries(plan.categories.map((c) => [c.id, kgInput(values[c.id])]));
      show(await bff<FactoryPlan>("setup/factory-plan", { method: "PUT", body: JSON.stringify({ year: plan.year, month: plan.month, totals }) }));
      setMessage({ tone: "ok", text: "План «Завод» сохранён" });
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(false);
    }
  };

  const entered = Object.values(values).reduce((a, v) => a + (kgInput(v) || 0), 0);
  const years = [today.getFullYear() - 1, today.getFullYear(), today.getFullYear() + 1];
  const cats = plan?.categories ?? [];
  // В таблице — только категории с планом «Завод» хотя бы у одного региона: столбцы из одних прочерков не нужны.
  const tableCats = cats.filter((c) => plan?.regions.some((r) => r.factory[c.id]));
  const ownBase = plan != null && plan.baseYear === plan.year && plan.baseMonth === plan.month;
  const hasTotals = plan != null && sumOf(plan.totals) > 0;

  return (
    <Section
      title={t("План «Завод»")}
      hint={t("итог страны по категориям · по регионам раскладывается по плану РОП")}
      actions={
        <div className="flex flex-wrap items-center gap-2">
          <select className={control} aria-label={t("Месяц")} value={period.month} onChange={(e) => pick({ ...period, month: Number(e.target.value) })}>
            {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
              <option key={m} value={m}>
                {monthLabel(period.year, m, t).split(" ")[0]}
              </option>
            ))}
          </select>
          <select className={control} aria-label={t("Год")} value={period.year} onChange={(e) => pick({ ...period, year: Number(e.target.value) })}>
            {years.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
          <button className={primary} disabled={!plan || busy || plan.explicit} onClick={save}>
            {t("Сохранить")}
          </button>
        </div>
      }
    >
      {message && (
        <div className={`mb-3 rounded-lg px-3 py-2 text-sm ${message.tone === "ok" ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>{t(message.text)}</div>
      )}
      {!plan ? (
        <div className="h-24 animate-pulse rounded-lg bg-muted" />
      ) : (
        <>
          {plan.explicit && (
            <p className="mb-3 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">
              {t("На {month} план «Завод» загружен по регионам (таблица или Data Manager) — действует он, итоги по категориям не используются.", {
                month: monthLabel(plan.year, plan.month, t).toLowerCase(),
              })}
            </p>
          )}
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            {cats.map((c) => (
              <label key={c.id} className="block">
                <span className="block text-sm text-ink">{c.name}</span>
                <span className="mb-1 block text-[11px] text-ink-3">
                  {plan.ropTotals[c.id] ? t("план РОП {kg} кг", { kg: kg(plan.ropTotals[c.id]) }) : t("в плане РОП нет")}
                </span>
                <input
                  className={input}
                  inputMode="decimal"
                  placeholder={t("нет плана")}
                  aria-label={t("{category}, кг", { category: c.name })}
                  disabled={plan.explicit}
                  value={values[c.id] ?? ""}
                  onChange={(e) => setValues({ ...values, [c.id]: e.target.value })}
                />
              </label>
            ))}
          </div>
          <p className="mt-3 text-sm text-ink-2">
            {t("Итого {kg} кг", { kg: kg(entered) })}
            {sumOf(plan.ropTotals) > 0 && <> · {t("план РОП {kg} кг", { kg: kg(sumOf(plan.ropTotals)) })}</>}
            {plan.updatedAt && <span className="text-ink-3"> · {t("сохранено {at}", { at: dateTime(plan.updatedAt) })}</span>}
          </p>
          {hasTotals && !plan.explicit && plan.baseMonth == null && (
            <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">
              {t("Плана РОП на этот и прошлые месяцы нет — разложить план «Завод» по регионам не по чему, во «Вторичке» его не будет.")}
            </p>
          )}
          {hasTotals && !plan.explicit && plan.baseYear != null && plan.baseMonth != null && !ownBase && (
            <p className="mt-3 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">
              {t("Плана РОП на этот месяц ещё нет — по регионам разложено по плану РОП за {month}. Когда план РОП месяца загрузится, раскладка пересчитается сама.", {
                month: monthLabel(plan.baseYear, plan.baseMonth, t).toLowerCase(),
              })}
            </p>
          )}
          {plan.regions.some((r) => sumOf(r.factory) > 0) && (
            <div className="mt-4 overflow-x-auto">
              <table className="w-full min-w-[720px] text-sm">
                <thead>
                  <tr className="border-b border-line text-right text-[11px] uppercase tracking-wide text-ink-3">
                    <th className="py-2 pr-2 text-left font-semibold">{t("Регион")}</th>
                    {tableCats.map((c) => (
                      <th key={c.id} className="py-2 pr-2 font-semibold">
                        {c.name}
                      </th>
                    ))}
                    <th className="py-2 pr-2 font-semibold">{t("Завод, кг")}</th>
                    <th className="py-2 font-semibold">{t("План РОП, кг")}</th>
                  </tr>
                </thead>
                <tbody>
                  {plan.regions.map((r) => (
                    <tr key={r.id} className="border-b border-line text-right tabular-nums last:border-b-0">
                      <td className="py-1.5 pr-2 text-left font-medium text-ink">{r.name}</td>
                      {tableCats.map((c) => (
                        <td key={c.id} className="py-1.5 pr-2 text-ink-2" title={r.rop[c.id] ? t("план РОП {kg} кг", { kg: kg(r.rop[c.id]) }) : undefined}>
                          {r.factory[c.id] ? kg(r.factory[c.id]) : <span className="text-ink-3">—</span>}
                        </td>
                      ))}
                      <td className="py-1.5 pr-2 font-semibold text-ink">{sumOf(r.factory) > 0 ? kg(sumOf(r.factory)) : <span className="text-ink-3">—</span>}</td>
                      <td className="py-1.5 text-ink-3">
                        {sumOf(r.rop) > 0 ? kg(sumOf(r.rop)) : "—"}
                        {sumOf(r.rop) > 0 && sumOf(r.factory) > 0 && <span className="ml-1 text-[11px]">({pct(sumOf(r.factory) / sumOf(r.rop))})</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <Note>
            {t(
              "Завод задаёт план итогом по стране в каждой категории. По регионам он делится пропорционально их плану РОП в этой категории (у категории, которой нет в плане РОП, — пропорционально всему плану РОП региона), в целых кг: сумма регионов равна итогу завода. Пока плана РОП месяца нет — по последнему месяцу с ним. Регион без плана РОП плана «Завод» не получает: его продажи идут в республику без плана. Пустое поле — у категории плана «Завод» нет. Если на месяц план «Завод» загружен по регионам (таблица, Data Manager), действует он. Планы ТП остаются из Linko. Во «Вторичке» план выбирается переключателем «План РОП / План «Завод»», под выполнением виден второй план.",
            )}
          </Note>
        </>
      )}
    </Section>
  );
}
