"use client";

import { useMemo, useState } from "react";
import { CalendarClock, Eye, History, Link2, Loader2, Pencil, Plus, Search, Send, Trash2, Users } from "lucide-react";
import { button, danger, field, primary } from "@/components/ai/form";
import { Alert, Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { chatTypeLabel } from "@/lib/telegram";
import {
  deliveryStatus,
  reportRoles,
  reportTypes,
  reportsApi,
  runStatus,
  telegramStatus,
  timeZones,
  weekDays,
  type ReportJob,
  type ReportOverview,
  type ReportRecipient,
  type ReportRecipientInput,
  type ReportRole,
} from "@/lib/telegramReports";
import { ReportHistoryPanel, ReportPreviewPanel, RoleChip, ScopePicker, defaultScope, when, type Scope } from "./TelegramReportsPreview";

type Tab = "schedule" | "recipients" | "preview" | "history";

const tabs: { key: Tab; label: string; icon: typeof Users }[] = [
  { key: "schedule", label: "Расписание", icon: CalendarClock },
  { key: "recipients", label: "Получатели", icon: Users },
  { key: "preview", label: "Предпросмотр и тест", icon: Eye },
  { key: "history", label: "История", icon: History },
];

/**
 * «Настройки → Telegram → Автоматические отчёты»: аккаунт, язык и параметры; расписание 13 запусков шести отчётов (каждый включается
 * отдельно, сохранение ничего не отправляет); получатели «роль → область → чаты → отчёты»; предпросмотр и тест; история отправок и запусков.
 * Права и области проверяет сервер.
 */
export function TelegramReports({ initial }: { initial: ReportOverview }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [tab, setTab] = useState<Tab>("schedule");
  const reload = async () => setData(await reportsApi.overview());
  const problems = data.recipients.filter((r) => r.enabled && r.problem).length;

  return (
    <>
      {!data.telegramEnabled && (
        <Alert tone="info">
          {t("Telegram выключен в этой среде (Telegram:Enabled=false): аккаунт работает на сервере OneBase. Здесь можно настроить отчёты и посмотреть картинки, по расписанию отсюда ничего не уходит.")}
        </Alert>
      )}
      {!data.canUseSales && <Alert tone="warn">{t("Нет права на данные продаж (sales.read): регионы, получатели и отчёты на реальных данных недоступны.")}</Alert>}

      <GeneralSettings data={data} onSaved={reload} />
      <Metrics data={data} />

      <section className="mt-6 rounded-xl border border-line bg-surface">
        <div className="flex flex-wrap gap-1 border-b border-line px-3 pt-3">
          {tabs.map((item) => (
            <button
              key={item.key}
              onClick={() => setTab(item.key)}
              className={`-mb-px flex items-center gap-2 rounded-t-lg border-b-2 px-3 py-2 text-sm font-medium ${
                tab === item.key ? "border-accent text-ink" : "border-transparent text-ink-3 hover:text-ink"
              }`}
            >
              <item.icon className="size-4" />
              {t(item.label)}
              {item.key === "recipients" && (
                <span className={`text-xs ${problems > 0 ? "text-warn" : "text-ink-3"}`}>
                  {data.recipients.length}
                  {problems > 0 ? ` · ${t("проблем: {n}", { n: problems })}` : ""}
                </span>
              )}
              {item.key === "schedule" && <span className="text-xs text-ink-3">{t("{n} из {total}", { n: data.jobs.filter((j) => j.enabled).length, total: data.jobs.length })}</span>}
            </button>
          ))}
        </div>
        <div className="p-4 sm:p-5">
          {tab === "schedule" && <Schedule data={data} onSaved={(job) => setData((d) => ({ ...d, jobs: d.jobs.map((j) => (j.code === job.code ? job : j)) }))} />}
          {tab === "recipients" && <Recipients data={data} onChanged={reload} />}
          {tab === "preview" && <ReportPreviewPanel data={data} />}
          {tab === "history" && <ReportHistoryPanel data={data} onChanged={reload} />}
        </div>
      </section>
    </>
  );
}

/** Аккаунт, язык картинок, порог «без закупа» и обновление данных Linko перед отправкой. */
function GeneralSettings({ data, onSaved }: { data: ReportOverview; onSaved: () => Promise<void> }) {
  const t = useT();
  const s = data.settings;
  const [form, setForm] = useState({ accountId: s.accountId ?? "", language: s.language, noPurchaseDays: s.noPurchaseDays, refreshBeforeSend: s.refreshBeforeSend });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const dirty =
    form.accountId !== (s.accountId ?? "") || form.language !== s.language || form.noPurchaseDays !== s.noPurchaseDays || form.refreshBeforeSend !== s.refreshBeforeSend;
  const account = data.accounts.find((a) => a.id === form.accountId);
  const changesAccount = !!s.accountId && form.accountId !== s.accountId;
  const edit = (patch: Partial<typeof form>) => (setForm((f) => ({ ...f, ...patch })), setSaved(false));

  const save = async () => {
    if (changesAccount && data.recipients.length > 0 && !window.confirm(t("Чаты другого аккаунта уберутся у получателей. Сменить аккаунт?"))) return;
    setBusy(true);
    setError(null);
    try {
      await reportsApi.saveSettings({ ...form, accountId: form.accountId || null });
      await onSaved();
      setSaved(true);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Section title="Отправка" hint={account?.username ? `@${account.username}` : undefined}>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-[minmax(0,1fr)_200px_170px_minmax(0,1fr)_auto] xl:items-end">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Аккаунт Telegram")}</span>
          <select className={field} value={form.accountId} onChange={(e) => edit({ accountId: e.target.value })}>
            <option value="">{t("Не выбран")}</option>
            {data.accounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.name}
                {a.username ? ` · @${a.username}` : ""}
                {a.status !== "Connected" ? ` · ${t("не подключён")}` : ""}
              </option>
            ))}
          </select>
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Язык картинок")}</span>
          <select className={field} value={form.language} onChange={(e) => edit({ language: e.target.value as "ru" | "uz" })}>
            <option value="ru">{t("Русский")}</option>
            <option value="uz">{t("Узбекский (латиница)")}</option>
          </select>
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Без закупа, дней")}</span>
          <input type="number" min={1} max={365} className={field} value={form.noPurchaseDays} onChange={(e) => edit({ noPurchaseDays: Math.min(365, Math.max(1, Number(e.target.value) || 1)) })} />
        </label>
        <label className="flex items-center gap-2 pb-2 text-sm text-ink">
          <input type="checkbox" checked={form.refreshBeforeSend} onChange={(e) => edit({ refreshBeforeSend: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
          {t("Обновлять данные Linko перед отправкой")}
        </label>
        <button className={primary} onClick={save} disabled={busy || !dirty}>
          {busy && <Loader2 className="size-4 animate-spin" />}
          {t("Сохранить")}
        </button>
      </div>
      {account && account.status !== "Connected" && <p className="mt-3 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">{t("Аккаунт не подключён — отчёты не уйдут, пока его не подключить на странице Telegram.")}</p>}
      {saved && !dirty && <p className="mt-3 text-sm text-ok">{t("Сохранено.")}</p>}
      {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      <Note>
        {t(
          "Отчёты уходят с подключённого аккаунта OneBase. Картинки PNG 16:9 (1920×1080) рисует сервер, полоса и метка роли: РМ — синие, СВ — зелёные, ТП — оранжевые; ниже 80% плана — красным. Перед запуском данные Linko обновляются (если последнее обновление старше 10 минут); не обновились — отчёт уходит с пометкой о времени данных.",
        )}
      </Note>
    </Section>
  );
}

/** Отправки за 7 дней — что дошло, что нет. */
function Metrics({ data }: { data: ReportOverview }) {
  const t = useT();
  const m = data.metrics;
  const tiles = [
    { label: "Отправлено", value: m.sent, className: "text-ok" },
    { label: "Ошибок", value: m.failed, className: m.failed > 0 ? "text-bad" : "text-ink" },
    { label: "Неизвестно, дошёл ли", value: m.unknown, className: m.unknown > 0 ? "text-warn" : "text-ink" },
    { label: "Пропущено", value: m.skipped, className: "text-ink" },
    { label: "В очереди", value: m.pending, className: "text-ink" },
  ];
  return (
    <div className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-5">
      {tiles.map((tile) => (
        <div key={tile.label} className="rounded-xl border border-line bg-surface px-4 py-3">
          <div className="text-xs text-ink-3">
            {t(tile.label)} · {t("7 дней")}
          </div>
          <div className={`mt-1 text-xl font-semibold ${tile.className}`}>{tile.value}</div>
        </div>
      ))}
    </div>
  );
}

// ---------- Расписание ----------

function Schedule({ data, onSaved }: { data: ReportOverview; onSaved: (job: ReportJob) => void }) {
  const t = useT();
  return (
    <div className="space-y-4">
      {reportTypes.map((type) => (
        <div key={type.key} className="rounded-xl border border-line">
          <div className="border-b border-line px-4 py-3">
            <h3 className="font-semibold text-ink">{t(type.title)}</h3>
            <p className="text-xs text-ink-3">{t(type.hint)}</p>
          </div>
          <div className="divide-y divide-line">
            {data.jobs
              .filter((j) => j.report === type.key)
              .map((job) => (
                <JobRow key={job.code} job={job} onSaved={onSaved} />
              ))}
          </div>
        </div>
      ))}
      <Note>
        {t(
          "Каждый запуск включается отдельно. Включение и смена времени действуют с этого момента: время, которое сегодня уже прошло, не догоняется. Сервер не работал во время запуска — отчёт отправляется, если с тех пор прошло не больше «догона», иначе запуск помечается «пропущен» в истории. Один и тот же отчёт одной области в один чат за день уходит один раз — даже при перезапуске и двух экземплярах сервера.",
        )}
      </Note>
    </div>
  );
}

/** Один запуск: включён ли, время, дни, пояс, догон пропущенного; следующий и последний запуск. Сохранение ничего не отправляет. */
function JobRow({ job, onSaved }: { job: ReportJob; onSaved: (job: ReportJob) => void }) {
  const t = useT();
  const pick = (j: ReportJob) => ({ enabled: j.enabled, time: j.time, timeZone: j.timeZone, days: j.days, catchUpMinutes: j.catchUpMinutes });
  const [form, setForm] = useState(() => pick(job));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const dirty = JSON.stringify(form) !== JSON.stringify(pick(job));
  const zones = timeZones.includes(form.timeZone) ? timeZones : [form.timeZone, ...timeZones];

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const saved = await reportsApi.saveJob(job.code, form);
      setForm(pick(saved));
      onSaved(saved);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const toggleDay = (day: number) =>
    setForm((f) => ({ ...f, days: f.days.includes(day) ? f.days.filter((d) => d !== day) : [...f.days, day].sort((a, b) => a - b) }));
  const last = job.lastRun;

  return (
    <div className={`px-4 py-3 ${form.enabled ? "bg-accent-soft/15" : ""}`}>
      <div className="flex flex-wrap items-center gap-x-5 gap-y-3">
        <div className="flex min-w-[220px] flex-1 items-center gap-3">
          <Switch checked={form.enabled} onChange={(v) => setForm((f) => ({ ...f, enabled: v }))} label={t("Включить автоматическую отправку")} />
          <div className="min-w-0">
            <div className="font-medium text-ink">
              {job.time} — {t(job.slotTitle)}
            </div>
            <div className="mt-0.5 flex flex-wrap items-center gap-1">
              {job.roles.map((r) => (
                <RoleChip key={r} role={r} />
              ))}
              <span className="ml-1 text-xs text-ink-3">{t("получателей: {n}", { n: job.recipients })}</span>
            </div>
          </div>
        </div>
        <label className="block w-[130px]">
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Время")}</span>
          <input type="time" className={field} value={form.time} onChange={(e) => setForm((f) => ({ ...f, time: e.target.value }))} />
        </label>
        <div>
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Дни")}</span>
          <div className="flex gap-1">
            {weekDays.map((d) => {
              const on = form.days.includes(d.day);
              return (
                <button
                  key={d.day}
                  type="button"
                  onClick={() => toggleDay(d.day)}
                  aria-pressed={on}
                  className={`h-9 min-w-8 rounded-lg px-1.5 text-xs font-semibold ${on ? "bg-accent text-white" : "border border-line bg-surface text-ink-3 hover:bg-muted"}`}
                >
                  {t(d.label)}
                </button>
              );
            })}
          </div>
        </div>
        <label className="block w-[170px]">
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Часовой пояс")}</span>
          <select className={field} value={form.timeZone} onChange={(e) => setForm((f) => ({ ...f, timeZone: e.target.value }))}>
            {zones.map((z) => (
              <option key={z} value={z}>
                {z}
              </option>
            ))}
          </select>
        </label>
        <label className="block w-[120px]" title={t("Сколько минут после времени запуска отчёт ещё отправляется, если сервер был выключен")}>
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Догон, мин")}</span>
          <input
            type="number"
            min={0}
            max={1440}
            className={field}
            value={form.catchUpMinutes}
            onChange={(e) => setForm((f) => ({ ...f, catchUpMinutes: Math.min(1440, Math.max(0, Number(e.target.value) || 0)) }))}
          />
        </label>
        <div className="flex gap-2 self-end">
          <button className={primary} onClick={save} disabled={busy || !dirty}>
            {busy && <Loader2 className="size-4 animate-spin" />}
            {t("Сохранить")}
          </button>
          {dirty && (
            <button className={button} onClick={() => setForm(pick(job))} disabled={busy}>
              {t("Отмена")}
            </button>
          )}
        </div>
      </div>
      <div className="mt-2 flex flex-wrap gap-x-5 gap-y-1 text-xs text-ink-2">
        <span>
          {job.enabled && job.nextRunAt
            ? t("Следующий запуск: {time}", { time: when(job.nextRunAt) })
            : job.enabled
              ? t("Дни не выбраны — отчёт не уходит.")
              : t("Выключен — сохранение ничего не отправляет.")}
        </span>
        {last && (
          <span>
            {t("Последний: {time}", { time: when(last.startedAt) })} · <span className={runStatus[last.status].className}>{t(runStatus[last.status].label)}</span>
            {last.planned > 0 && ` · ${t("отправлено {sent} из {planned}", { sent: last.sent, planned: last.planned })}`}
            {last.error && <span className="text-ink-3"> · {t(last.error)}</span>}
          </span>
        )}
        {form.enabled && !job.enabled && <span className="text-warn">{t("После сохранения отчёт уйдёт в ближайшее время по расписанию, а не сразу.")}</span>}
      </div>
      {error && <p className="mt-2 rounded-lg bg-bad-soft px-3 py-2 text-xs text-bad">{error}</p>}
    </div>
  );
}

export function Switch({ checked, onChange, disabled, label }: { checked: boolean; onChange: (v: boolean) => void; disabled?: boolean; label: string }) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={() => onChange(!checked)}
      className={`relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors disabled:opacity-40 ${checked ? "bg-accent" : "bg-muted ring-1 ring-inset ring-line"}`}
    >
      <span className={`inline-block size-4 rounded-full bg-white shadow transition-transform ${checked ? "translate-x-[18px]" : "translate-x-0.5"}`} />
    </button>
  );
}

// ---------- Получатели ----------

const inputOf = (r: ReportRecipient): ReportRecipientInput => ({
  role: r.role,
  title: r.name !== r.scope ? r.name : null,
  directionId: r.directionId,
  regionId: r.regionId,
  supervisorId: r.supervisorId,
  agentId: r.agentId,
  chatIds: r.chats.map((c) => c.id),
  reports: r.reports,
  enabled: r.enabled,
});

/** Отчёты роли по умолчанию: всё, что роли положено, кроме недели в понедельник (её включают отдельно). */
const defaultReports = (data: ReportOverview, role: ReportRole) =>
  data.jobs.filter((j) => j.roles.includes(role) && j.code !== "weekly.monday" && (role === "tp" || j.code !== "no-orders.reminder")).map((j) => j.code);

const emptyInput = (data: ReportOverview, role: ReportRole = "sv"): ReportRecipientInput => {
  const scope = defaultScope(data, role);
  return { ...scope, title: null, chatIds: [], reports: defaultReports(data, role), enabled: true };
};

/** Получатели: роль и область из оргструктуры «Продаж», чаты Telegram, отчёты; отправить сейчас; почему отчёт не уйдёт. */
function Recipients({ data, onChanged }: { data: ReportOverview; onChanged: () => Promise<void> }) {
  const t = useT();
  const [editing, setEditing] = useState<{ id: string | null; input: ReportRecipientInput } | null>(null);
  const [role, setRole] = useState<"" | ReportRole>("");
  const [regionId, setRegionId] = useState("");
  const [state, setState] = useState<"" | "problem" | "off" | "linked">("");
  const [search, setSearch] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const rows = useMemo(() => {
    const q = search.trim().toLowerCase();
    return data.recipients.filter(
      (r) =>
        (!role || r.role === role) &&
        (!regionId || r.regionId === regionId || (r.role === "rm" && data.regions.find((x) => x.id === regionId)?.directionId === r.directionId)) &&
        (!state || (state === "problem" ? !!r.problem : state === "off" ? !r.enabled : r.telegramStatus === "linked")) &&
        (!q || r.name.toLowerCase().includes(q) || r.scope.toLowerCase().includes(q) || r.chats.some((c) => c.title.toLowerCase().includes(q))),
    );
  }, [data, role, regionId, state, search]);

  const save = async (r: ReportRecipient, patch: Partial<ReportRecipientInput>) => {
    setBusyId(r.id);
    setError(null);
    try {
      await reportsApi.saveRecipient(r.id, { ...inputOf(r), ...patch });
      await onChanged();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusyId(null);
    }
  };

  const remove = async (r: ReportRecipient) => {
    if (!window.confirm(t("Удалить получателя «{name}»? История отправок сохранится.", { name: r.name }))) return;
    setBusyId(r.id);
    try {
      await reportsApi.deleteRecipient(r.id);
      await onChanged();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusyId(null);
    }
  };

  const send = async (r: ReportRecipient, job: string) => {
    setBusyId(r.id);
    setError(null);
    setMessage(null);
    try {
      let result = await reportsApi.send(r.id, job, false);
      if ("conflict" in result) {
        if (!window.confirm(result.conflict)) return;
        result = await reportsApi.send(r.id, job, true);
        if ("conflict" in result) throw new Error(result.conflict);
      }

      const info = data.jobs.find((j) => j.code === job)!;
      const sent = result.filter((x) => x.status === "sent").length;
      const lines = result
        .filter((x) => x.status !== "sent")
        .map((x) =>
          x.status === "pending"
            ? t("{chat}: временная ошибка Telegram — повторим в {time}", { chat: x.recipientTitle, time: when(x.nextAttemptAt) })
            : `${x.recipientTitle}: ${t(deliveryStatus[x.status].label)} — ${t(x.error ?? "")}`,
        );
      setMessage([t("«{report}»: отправлено в {sent} из {total} чатов.", { report: `${t(info.reportTitle)} · ${info.time}`, sent, total: result.length }), ...lines].join(" "));
      await onChanged();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusyId(null);
    }
  };

  const narrow = field.replace("w-full", "w-auto");
  return (
    <div>
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative min-w-[200px] flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" />
          <input className={`${field} pl-9`} value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Имя, область или чат")} />
        </div>
        <select className={narrow} value={role} onChange={(e) => setRole(e.target.value as "" | ReportRole)} aria-label={t("Роль")}>
          <option value="">{t("Все роли")}</option>
          {reportRoles.map((r) => (
            <option key={r.key} value={r.key}>
              {t(r.label)}
            </option>
          ))}
        </select>
        <select className={narrow} value={regionId} onChange={(e) => setRegionId(e.target.value)} aria-label={t("Регион")}>
          <option value="">{t("Все регионы")}</option>
          {data.regions.map((r) => (
            <option key={r.id} value={r.id}>
              {r.name}
            </option>
          ))}
        </select>
        <select className={narrow} value={state} onChange={(e) => setState(e.target.value as typeof state)} aria-label={t("Статус")}>
          <option value="">{t("Все")}</option>
          <option value="problem">{t("С проблемой")}</option>
          <option value="linked">{t("Telegram привязан")}</option>
          <option value="off">{t("Выключены")}</option>
        </select>
        <button className={primary} onClick={() => setEditing({ id: null, input: emptyInput(data) })} disabled={!data.canUseSales || !data.settings.accountId}>
          <Plus className="size-4" />
          {t("Добавить получателя")}
        </button>
      </div>
      {!data.settings.accountId && <p className="mt-3 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">{t("Сначала выберите аккаунт Telegram выше.")}</p>}
      {editing && (
        <RecipientEditor
          data={data}
          id={editing.id}
          initial={editing.input}
          onCancel={() => setEditing(null)}
          onSaved={async () => {
            setEditing(null);
            await onChanged();
          }}
        />
      )}
      {message && <p className="mt-3 rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">{message}</p>}
      {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

      <div className="mt-4 space-y-3">
        {rows.length === 0 ? (
          <p className="text-sm text-ink-3">{data.recipients.length === 0 ? t("Получателей пока нет.") : t("Никого не найдено.")}</p>
        ) : (
          rows.map((r) => (
            <div key={r.id} className={`rounded-xl border border-line p-4 ${r.enabled ? "bg-surface" : "bg-muted/40 opacity-70"}`}>
              <div className="flex flex-wrap items-start gap-x-6 gap-y-3">
                <div className="min-w-[240px] flex-1">
                  <div className="flex items-center gap-2">
                    <RoleChip role={r.role} />
                    <span className="font-semibold text-ink">{r.name}</span>
                  </div>
                  {r.name !== r.scope && <div className="text-sm text-ink-2">{r.scope}</div>}
                  <div className="text-xs text-ink-3">{r.role === "tp" ? r.region : t("ТП в отчёте: {n}", { n: r.agents })}</div>
                  {r.problem && <div className="mt-1 max-w-[480px] text-xs text-warn">{t(r.problem)}</div>}
                </div>
                <div className="min-w-[220px] flex-1">
                  <div className="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wide text-ink-3">
                    {t("Куда")}
                    <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium normal-case tracking-normal ${telegramStatus[r.telegramStatus].className}`}>
                      {t(telegramStatus[r.telegramStatus].label)}
                    </span>
                  </div>
                  <div className="mt-1 flex flex-wrap gap-1.5">
                    {r.chats.map((c) => (
                      <span key={c.id} className={`rounded-full px-2.5 py-0.5 text-xs ${c.canSend ? "bg-accent-soft text-accent-strong" : "bg-bad-soft text-bad"}`} title={c.linked ? t("Telegram подтверждён: {name}", { name: c.linked }) : undefined}>
                        {c.linked && <Link2 className="mr-1 inline size-3" />}
                        {c.title}
                        <span className="opacity-70"> · {t(chatTypeLabel[c.type])}</span>
                      </span>
                    ))}
                  </div>
                </div>
                <div className="min-w-[240px] flex-1">
                  <div className="text-[11px] font-semibold uppercase tracking-wide text-ink-3">{t("Отчёты")}</div>
                  <div className="mt-1 flex flex-wrap gap-1">
                    {data.jobs
                      .filter((j) => r.reports.includes(j.code))
                      .map((j) => (
                        <span key={j.code} className="rounded-md bg-muted px-2 py-0.5 text-xs text-ink-2" title={t(j.slotTitle)}>
                          {t(j.reportTitle).replace(/^\d+\.\s*/, "")} · {j.time}
                        </span>
                      ))}
                  </div>
                </div>
                <div className="flex items-center gap-2 self-center">
                  <Switch checked={r.enabled} disabled={busyId === r.id} onChange={(v) => save(r, { enabled: v })} label={t("Включён")} />
                </div>
              </div>
              <div className="mt-3 flex flex-wrap items-center justify-between gap-2 border-t border-line pt-3">
                <div className="text-xs text-ink-3">
                  {r.last.length === 0
                    ? t("Ещё не отправлялось.")
                    : r.last.slice(0, 3).map((d, i) => {
                        const job = data.jobs.find((j) => j.code === d.report);
                        return (
                          <span key={i} className="mr-3 inline-block" title={d.error ? t(d.error) : undefined}>
                            {job ? `${t(job.reportTitle).replace(/^\d+\.\s*/, "")} ${job.time}` : d.report} → {d.chat}:{" "}
                            <span className={deliveryStatus[d.status].className}>{t(deliveryStatus[d.status].label)}</span> {when(d.at)}
                          </span>
                        );
                      })}
                </div>
                <div className="flex flex-nowrap gap-1">
                  <SendNow data={data} recipient={r} disabled={busyId === r.id || !!r.problem || !data.canUseSales} busy={busyId === r.id} onSend={(job) => send(r, job)} />
                  <button className={button} onClick={() => setEditing({ id: r.id, input: inputOf(r) })} aria-label={t("Изменить")} title={t("Изменить")}>
                    <Pencil className="size-4" />
                  </button>
                  <button className={danger} onClick={() => remove(r)} disabled={busyId === r.id} aria-label={t("Удалить")} title={t("Удалить")}>
                    <Trash2 className="size-4" />
                  </button>
                </div>
              </div>
            </div>
          ))
        )}
      </div>
      <Note>
        {t(
          "Получатель — роль и область из оргструктуры «Продаж»: РМ — направление или вся страна, СВ — регион и супервайзер, ТП — торговый представитель (только свои цифры). Чаты — личные чаты и группы аккаунта; «Telegram привязан» — человек подтвердил свой Telegram кодом в OneBase. Пользователи Sales Base здесь не участвуют.",
        )}
      </Note>
    </div>
  );
}

/** «Отправить сейчас»: какой запуск — и кнопка. */
function SendNow({ data, recipient, disabled, busy, onSend }: { data: ReportOverview; recipient: ReportRecipient; disabled: boolean; busy: boolean; onSend: (job: string) => void }) {
  const t = useT();
  const jobs = data.jobs.filter((j) => j.roles.includes(recipient.role));
  const [job, setJob] = useState(recipient.reports[0] ?? jobs[0]?.code ?? "");
  return (
    <span className="inline-flex">
      <select className={`${field.replace("w-full", "w-auto")} h-9 max-w-[240px] rounded-r-none border-r-0 text-xs`} value={job} onChange={(e) => setJob(e.target.value)} aria-label={t("Какой отчёт")}>
        {jobs.map((j) => (
          <option key={j.code} value={j.code}>
            {t(j.reportTitle)} · {j.time}
          </option>
        ))}
      </select>
      <button className={`${button} rounded-l-none`} onClick={() => onSend(job)} disabled={disabled || !job} title={t("Отправить сейчас во все чаты получателя (реальные данные)")}>
        {busy ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
        {t("Отправить")}
      </button>
    </span>
  );
}

/** Новый получатель или правка: роль → область → чаты → отчёты роли. */
function RecipientEditor({
  data,
  id,
  initial,
  onCancel,
  onSaved,
}: {
  data: ReportOverview;
  id: string | null;
  initial: ReportRecipientInput;
  onCancel: () => void;
  onSaved: () => Promise<void>;
}) {
  const t = useT();
  const [input, setInput] = useState(initial);
  const [search, setSearch] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const scope: Scope = { role: input.role, directionId: input.directionId, regionId: input.regionId, supervisorId: input.supervisorId, agentId: input.agentId };
  const chats = useMemo(() => {
    const q = search.trim().toLowerCase();
    const list = data.chats.filter((c) => !q || c.title.toLowerCase().includes(q) || (c.username ?? "").toLowerCase().includes(q) || (c.linkedUser ?? "").toLowerCase().includes(q));
    // Выбранные и подтверждённые — сверху.
    return [...list].sort((a, b) => Number(input.chatIds.includes(b.id)) - Number(input.chatIds.includes(a.id)) || Number(!!b.linkedUser) - Number(!!a.linkedUser));
  }, [data.chats, search, input.chatIds]);
  const jobs = data.jobs.filter((j) => j.roles.includes(input.role));

  const changeScope = (s: Scope) =>
    setInput((i) => ({ ...i, ...s, reports: s.role !== i.role ? defaultReports(data, s.role) : i.reports.filter((code) => data.jobs.find((j) => j.code === code)?.roles.includes(s.role)) }));
  const toggleChat = (chatId: string) => setInput((i) => ({ ...i, chatIds: i.chatIds.includes(chatId) ? i.chatIds.filter((c) => c !== chatId) : [...i.chatIds, chatId] }));
  const toggleJob = (code: string) => setInput((i) => ({ ...i, reports: i.reports.includes(code) ? i.reports.filter((c) => c !== code) : [...i.reports, code] }));

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      await reportsApi.saveRecipient(id, input);
      await onSaved();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const ready = input.chatIds.length > 0 && input.reports.length > 0 && (input.role === "rm" || input.regionId) && (input.role !== "tp" || input.agentId);

  return (
    <div className="mt-4 rounded-xl border border-accent/30 bg-accent-soft/20 p-4">
      <h3 className="font-semibold text-ink">{id ? t("Получатель") : t("Новый получатель")}</h3>
      <div className="mt-4 grid gap-5 lg:grid-cols-3">
        <div className="space-y-3">
          <ScopePicker data={data} scope={scope} onChange={changeScope} />
          <label className="block">
            <span className="mb-1 block text-xs font-medium text-ink-2">{t("Подпись (необязательно)")}</span>
            <input className={field} value={input.title ?? ""} onChange={(e) => setInput((i) => ({ ...i, title: e.target.value || null }))} placeholder={t("По умолчанию — область")} maxLength={200} />
          </label>
        </div>

        <div>
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Чаты")} <span className="font-normal text-ink-3">· {t("выбрано: {n}", { n: input.chatIds.length })}</span>
          </span>
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" />
            <input className={`${field} pl-9`} value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Название, @username или имя")} />
          </div>
          <div className="mt-2 max-h-[340px] overflow-y-auto rounded-lg border border-line bg-surface">
            {chats.length === 0 ? (
              <p className="px-3 py-3 text-sm text-ink-3">{t("Чатов нет — обновите их на странице Telegram, вкладка «Чаты».")}</p>
            ) : (
              chats.map((c) => (
                <label key={c.id} className={`flex items-center gap-3 border-b border-line px-3 py-2 text-sm last:border-b-0 ${c.canSend ? "cursor-pointer hover:bg-muted" : "opacity-50"}`}>
                  <input type="checkbox" checked={input.chatIds.includes(c.id)} disabled={!c.canSend} onChange={() => toggleChat(c.id)} className="size-4 accent-[var(--color-accent)]" />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-ink">{c.title}</span>
                    <span className="text-xs text-ink-3">
                      {t(chatTypeLabel[c.type])}
                      {c.username ? ` · @${c.username}` : ""}
                      {c.linkedUser ? ` · ${t("привязан: {name}", { name: c.linkedUser })}` : ""}
                      {!c.canSend && c.reason ? ` · ${t(c.reason)}` : ""}
                    </span>
                  </span>
                </label>
              ))
            )}
          </div>
        </div>

        <div>
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Отчёты")}</span>
          <div className="space-y-3">
            {reportTypes.map((type) => {
              const own = jobs.filter((j) => j.report === type.key);
              if (own.length === 0) return null;
              return (
                <div key={type.key}>
                  <div className="text-xs font-semibold text-ink-2">{t(type.title)}</div>
                  <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1">
                    {own.map((j) => (
                      <label key={j.code} className="flex items-center gap-1.5 text-sm text-ink">
                        <input type="checkbox" checked={input.reports.includes(j.code)} onChange={() => toggleJob(j.code)} className="size-4 accent-[var(--color-accent)]" />
                        {j.time}
                        <span className="text-xs text-ink-3">{t(j.slotTitle)}</span>
                      </label>
                    ))}
                  </div>
                </div>
              );
            })}
          </div>
          <label className="mt-4 flex items-center gap-2 text-sm text-ink">
            <Switch checked={input.enabled} onChange={(v) => setInput((i) => ({ ...i, enabled: v }))} label={t("Включён")} />
            {t("Включён")}
          </label>
        </div>
      </div>

      {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      <div className="mt-4 flex gap-2">
        <button className={primary} onClick={save} disabled={busy || !ready}>
          {busy && <Loader2 className="size-4 animate-spin" />}
          {t("Сохранить")}
        </button>
        <button className={button} onClick={onCancel} disabled={busy}>
          {t("Отмена")}
        </button>
      </div>
    </div>
  );
}

