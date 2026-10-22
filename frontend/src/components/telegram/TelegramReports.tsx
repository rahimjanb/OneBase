"use client";

import { useMemo, useState } from "react";
import { CalendarClock, Eye, Loader2, Pencil, Plus, ScrollText, Search, Send, Trash2, Users } from "lucide-react";
import { button, danger, field, primary } from "@/components/ai/form";
import { Alert, Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import {
  deliveryStatus,
  reportKinds,
  reportsApi,
  timeZones,
  weekDays,
  type ReportKind,
  type ReportOverview,
  type ReportSchedule,
  type ReportSubscription,
  type ReportSubscriptionInput,
} from "@/lib/telegramReports";
import { chatTypeLabel } from "@/lib/telegram";
import { ReportJournal, ReportPreviewPanel } from "./TelegramReportsPreview";

type Tab = "schedule" | "subscriptions" | "preview" | "journal";

const tabs: { key: Tab; label: string; icon: typeof Users }[] = [
  { key: "schedule", label: "Расписание", icon: CalendarClock },
  { key: "subscriptions", label: "Автоотправки", icon: Users },
  { key: "preview", label: "Предпросмотр и тест", icon: Eye },
  { key: "journal", label: "Журнал отправок", icon: ScrollText },
];

const when = (iso: string | null) => (iso ? new Date(iso).toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" }) : "—");

/**
 * «Настройки → Telegram → Автоматические отчёты»: аккаунт и язык картинок, расписания утро / обед / вечер (каждое включается отдельно),
 * автоотправки «регион → супервайзер → чаты и группы» с видами отчётов, предпросмотр и тестовая отправка, журнал. Права проверяет сервер.
 */
export function TelegramReports({ initial }: { initial: ReportOverview }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [tab, setTab] = useState<Tab>("schedule");

  const reload = async () => setData(await reportsApi.overview());

  return (
    <>
      {!data.telegramEnabled && (
        <Alert tone="info">
          {t("Telegram выключен в этой среде (Telegram:Enabled=false): аккаунт работает на сервере OneBase. Здесь можно настроить отчёты и посмотреть картинки, по расписанию отсюда ничего не уходит.")}
        </Alert>
      )}
      {!data.canUseSales && (
        <Alert tone="warn">{t("Нет права на данные продаж (sales.read): регионы и реальные цифры недоступны, предпросмотр и тест — на демонстрационных данных.")}</Alert>
      )}

      <GeneralSettings data={data} onSaved={reload} />

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
              {item.key === "subscriptions" && <span className="text-xs text-ink-3">{data.subscriptions.length}</span>}
            </button>
          ))}
        </div>
        <div className="p-4 sm:p-5">
          {tab === "schedule" && (
            <div className="grid gap-4 lg:grid-cols-3">
              {reportKinds.map((k) => (
                <ScheduleCard
                  key={k.key}
                  schedule={data.schedules.find((s) => s.kind === k.key)!}
                  recipients={data.subscriptions.filter((r) => r.enabled && r[k.field]).reduce((n, r) => n + r.chats.length, 0)}
                  onSaved={(s) => setData((d) => ({ ...d, schedules: d.schedules.map((x) => (x.kind === s.kind ? s : x)) }))}
                />
              ))}
            </div>
          )}
          {tab === "subscriptions" && <Subscriptions data={data} onChanged={reload} />}
          {tab === "preview" && <ReportPreviewPanel data={data} />}
          {tab === "journal" && <ReportJournal />}
        </div>
      </section>
    </>
  );
}

/** Аккаунт, с которого уходят отчёты, и язык подписей на картинках. */
function GeneralSettings({ data, onSaved }: { data: ReportOverview; onSaved: () => Promise<void> }) {
  const t = useT();
  const [accountId, setAccountId] = useState(data.settings.accountId ?? "");
  const [language, setLanguage] = useState<string>(data.settings.language);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const dirty = accountId !== (data.settings.accountId ?? "") || language !== data.settings.language;
  const account = data.accounts.find((a) => a.id === accountId);
  const changesAccount = !!data.settings.accountId && accountId !== data.settings.accountId;

  const save = async () => {
    if (changesAccount && data.subscriptions.length > 0 && !window.confirm(t("Чаты другого аккаунта уберутся из автоотправок. Сменить аккаунт?"))) return;
    setBusy(true);
    setError(null);
    try {
      await reportsApi.saveSettings(accountId || null, language);
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
      <div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_200px_auto] sm:items-end">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Аккаунт Telegram")}</span>
          <select className={field} value={accountId} onChange={(e) => (setAccountId(e.target.value), setSaved(false))}>
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
          <select className={field} value={language} onChange={(e) => (setLanguage(e.target.value), setSaved(false))}>
            <option value="ru">{t("Русский")}</option>
            <option value="uz">{t("Узбекский (латиница)")}</option>
          </select>
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
          "Отчёты уходят с уже подключённого аккаунта OneBase (не бот и не второй аккаунт). Картинки PNG 16:9 (1920×1080) рисует сервер: регион, супервайзер, РМ, агенты с планом и фактом, итог; длинный отчёт — несколько пронумерованных картинок одним альбомом.",
        )}
      </Note>
    </Section>
  );
}

/** Расписание одного вида отчёта: включён ли, время, часовой пояс, дни, показатель. Сохранение ничего не отправляет. */
function ScheduleCard({ schedule, recipients, onSaved }: { schedule: ReportSchedule; recipients: number; onSaved: (s: ReportSchedule) => void }) {
  const t = useT();
  const kind = reportKinds.find((k) => k.key === schedule.kind)!;
  const [form, setForm] = useState(schedule);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const dirty = JSON.stringify({ ...form, nextRunAt: null, updatedAt: null }) !== JSON.stringify({ ...schedule, nextRunAt: null, updatedAt: null });
  const zones = timeZones.includes(form.timeZone) ? timeZones : [form.timeZone, ...timeZones];

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const saved = await reportsApi.saveSchedule(schedule.kind, form);
      setForm(saved);
      onSaved(saved);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const toggleDay = (day: number) =>
    setForm((f) => ({ ...f, days: f.days.includes(day) ? f.days.filter((d) => d !== day) : [...f.days, day].sort((a, b) => a - b) }));

  return (
    <div className={`flex flex-col rounded-xl border p-4 ${form.enabled ? "border-accent/40 bg-accent-soft/20" : "border-line bg-surface"}`}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <h3 className="font-semibold text-ink">{t(kind.label)}</h3>
          <p className="mt-0.5 min-h-12 text-xs text-ink-3">{t(kind.hint)}</p>
        </div>
        <Switch checked={form.enabled} onChange={(v) => setForm((f) => ({ ...f, enabled: v }))} label={t("Включить автоматическую отправку")} />
      </div>

      <div className="mt-4 grid grid-cols-2 gap-3">
        <label className="block">
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Время")}</span>
          <input type="time" className={field} value={form.time} onChange={(e) => setForm((f) => ({ ...f, time: e.target.value }))} />
        </label>
        <label className="block">
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Показатель")}</span>
          <select className={field} value={form.metric} onChange={(e) => setForm((f) => ({ ...f, metric: e.target.value as "sum" | "kg" }))}>
            <option value="sum">{t("Сумы")}</option>
            <option value="kg">{t("Килограммы")}</option>
          </select>
        </label>
        <label className="col-span-2 block">
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Часовой пояс")}</span>
          <select className={field} value={form.timeZone} onChange={(e) => setForm((f) => ({ ...f, timeZone: e.target.value }))}>
            {zones.map((z) => (
              <option key={z} value={z}>
                {z}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="mt-3">
        <span className="mb-1 block text-xs font-medium text-ink-2">{t("Дни отправки")}</span>
        <div className="flex flex-wrap gap-1">
          {weekDays.map((d) => {
            const on = form.days.includes(d.day);
            return (
              <button
                key={d.day}
                type="button"
                onClick={() => toggleDay(d.day)}
                aria-pressed={on}
                className={`h-8 min-w-9 rounded-lg px-2 text-xs font-semibold ${on ? "bg-accent text-white" : "border border-line bg-surface text-ink-3 hover:bg-muted"}`}
              >
                {t(d.label)}
              </button>
            );
          })}
        </div>
      </div>

      <p className="mt-3 text-xs text-ink-2">
        {schedule.enabled && schedule.nextRunAt
          ? t("Следующая отправка: {time} · чатов: {n}", { time: when(schedule.nextRunAt), n: recipients })
          : schedule.enabled
            ? t("Дни не выбраны — отчёт не уходит.")
            : t("Выключен — сохранение ничего не отправляет.")}
      </p>
      {form.enabled && !schedule.enabled && <p className="mt-1 text-xs text-warn">{t("После сохранения отчёт уйдёт в ближайшее время по расписанию, а не сразу.")}</p>}
      {error && <p className="mt-2 rounded-lg bg-bad-soft px-3 py-2 text-xs text-bad">{error}</p>}
      <div className="mt-auto flex gap-2 pt-4">
        <button className={primary} onClick={save} disabled={busy || !dirty}>
          {busy && <Loader2 className="size-4 animate-spin" />}
          {t("Сохранить")}
        </button>
        {dirty && (
          <button className={button} onClick={() => setForm(schedule)} disabled={busy}>
            {t("Отмена")}
          </button>
        )}
      </div>
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

// ---------- Автоотправки ----------

const emptyInput = (data: ReportOverview): ReportSubscriptionInput => ({
  regionId: data.regions[0]?.id ?? "",
  supervisorId: data.regions[0]?.supervisors[0]?.id ?? null,
  chatIds: [],
  morning: true,
  midday: true,
  evening: true,
  enabled: true,
});

const inputOf = (s: ReportSubscription): ReportSubscriptionInput => ({
  regionId: s.regionId,
  supervisorId: s.supervisorId,
  chatIds: s.chats.map((c) => c.id),
  morning: s.morning,
  midday: s.midday,
  evening: s.evening,
  enabled: s.enabled,
});

/** Автоотправки: регион → супервайзер → чаты, какие отчёты; «Отправить сейчас» во все чаты; почему отправить нельзя. */
function Subscriptions({ data, onChanged }: { data: ReportOverview; onChanged: () => Promise<void> }) {
  const t = useT();
  const [editing, setEditing] = useState<{ id: string | null; input: ReportSubscriptionInput } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  const toggle = async (s: ReportSubscription, patch: Partial<ReportSubscriptionInput>) => {
    setBusyId(s.id);
    setError(null);
    try {
      await reportsApi.saveSubscription(s.id, { ...inputOf(s), ...patch });
      await onChanged();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusyId(null);
    }
  };

  const remove = async (s: ReportSubscription) => {
    if (!window.confirm(t("Удалить автоотправку «{name}»?", { name: `${s.region} · ${s.supervisor}` }))) return;
    setBusyId(s.id);
    try {
      await reportsApi.deleteSubscription(s.id);
      await onChanged();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusyId(null);
    }
  };

  const send = async (s: ReportSubscription, kind: ReportKind) => {
    setBusyId(s.id);
    setError(null);
    setMessage(null);
    try {
      let result = await reportsApi.send(s.id, kind, false);
      if ("conflict" in result) {
        if (!window.confirm(result.conflict)) return;
        result = await reportsApi.send(s.id, kind, true);
        if ("conflict" in result) throw new Error(result.conflict);
      }

      const report = t(reportKinds.find((k) => k.key === kind)!.label);
      const sent = result.filter((r) => r.status === "sent").length;
      const lines = result
        .filter((r) => r.status !== "sent")
        .map((r) =>
          r.status === "pending"
            ? t("{chat}: временная ошибка Telegram — повторим в {time}", { chat: r.recipientTitle, time: when(r.nextAttemptAt) })
            : `${r.recipientTitle}: ${t(r.error ?? "")}`,
        );
      setMessage([t("«{report}»: отправлено в {sent} из {total} чатов.", { report, sent, total: result.length }), ...lines].join(" "));
      await onChanged();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="text-sm text-ink-2">
          {t("Регион, супервайзер и чаты, куда уходят отчёты. Агенты на картинке — команда региона в «Продажах», подставляются сами.")}
        </p>
        <button className={primary} onClick={() => setEditing({ id: null, input: emptyInput(data) })} disabled={!data.canUseSales || !data.settings.accountId}>
          <Plus className="size-4" />
          {t("Добавить автоотправку")}
        </button>
      </div>
      {!data.settings.accountId && <p className="mt-3 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">{t("Сначала выберите аккаунт Telegram выше.")}</p>}
      {editing && (
        <SubscriptionEditor
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
        {data.subscriptions.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Автоотправок пока нет.")}</p>
        ) : (
          data.subscriptions.map((s) => (
            <div key={s.id} className={`rounded-xl border border-line p-4 ${s.enabled ? "bg-surface" : "bg-muted/40 opacity-70"}`}>
              <div className="flex flex-wrap items-start gap-x-6 gap-y-3">
                <div className="min-w-[220px] flex-1">
                  <div className="font-semibold text-ink">{s.region}</div>
                  <div className="text-sm text-ink-2">
                    {t("Супервайзер")}: {s.supervisor}
                  </div>
                  <div className="text-xs text-ink-3">{t("Агентов на картинке: {n}", { n: s.agents })}</div>
                  {s.problem && <div className="mt-1 max-w-[460px] text-xs text-warn">{t(s.problem)}</div>}
                </div>
                <div className="min-w-[220px] flex-1">
                  <div className="text-[11px] font-semibold uppercase tracking-wide text-ink-3">{t("Куда")}</div>
                  <div className="mt-1 flex flex-wrap gap-1.5">
                    {s.chats.map((c) => (
                      <span key={c.id} className={`rounded-full px-2.5 py-0.5 text-xs ${c.canSend ? "bg-accent-soft text-accent-strong" : "bg-bad-soft text-bad"}`}>
                        {c.title}
                        <span className="opacity-70"> · {t(chatTypeLabel[c.type])}</span>
                      </span>
                    ))}
                  </div>
                </div>
                <div>
                  <div className="text-[11px] font-semibold uppercase tracking-wide text-ink-3">{t("Отчёты")}</div>
                  <div className="mt-1 flex flex-wrap gap-3">
                    {reportKinds.map((k) => (
                      <label key={k.key} className="flex items-center gap-1.5 text-sm text-ink">
                        <input
                          type="checkbox"
                          checked={s[k.field]}
                          disabled={busyId === s.id}
                          onChange={() => toggle(s, { [k.field]: !s[k.field] })}
                          className="size-4 accent-[var(--color-accent)]"
                        />
                        {t(k.label)}
                      </label>
                    ))}
                  </div>
                </div>
                <div className="flex items-center gap-2 self-center">
                  <Switch checked={s.enabled} disabled={busyId === s.id} onChange={(v) => toggle(s, { enabled: v })} label={t("Включена")} />
                </div>
              </div>
              <div className="mt-3 flex flex-wrap items-center justify-between gap-2 border-t border-line pt-3">
                <div className="text-xs text-ink-3">
                  {s.last.length === 0
                    ? t("Ещё не отправлялась.")
                    : s.last.slice(0, 3).map((d, i) => (
                        <span key={i} className="mr-3 inline-block" title={d.error ? t(d.error) : undefined}>
                          {t(reportKinds.find((k) => k.key === d.kind)!.label)} → {d.chat}:{" "}
                          <span className={deliveryStatus[d.status].className}>{t(deliveryStatus[d.status].label)}</span> {when(d.at)}
                        </span>
                      ))}
                </div>
                <div className="flex flex-nowrap gap-1">
                  <SendNow disabled={busyId === s.id || !!s.problem} busy={busyId === s.id} onSend={(kind) => send(s, kind)} />
                  <button className={button} onClick={() => setEditing({ id: s.id, input: inputOf(s) })} aria-label={t("Изменить")} title={t("Изменить")}>
                    <Pencil className="size-4" />
                  </button>
                  <button className={danger} onClick={() => remove(s)} disabled={busyId === s.id} aria-label={t("Удалить")} title={t("Удалить")}>
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
          "Одна автоотправка — один регион и один супервайзер; отчёт уходит во все её чаты и группы. Таких строк можно добавить сколько угодно. Регионы, супервайзеры и агенты — реальные данные раздела «Продажи»: агенты — те, кто работает в регионе (продажи, визиты, план, профиль). По расписанию один и тот же отчёт за день в один чат уходит один раз; вручную повторно — только с подтверждением.",
        )}
      </Note>
    </div>
  );
}

/** «Отправить сейчас»: какой отчёт — и кнопка. */
function SendNow({ disabled, busy, onSend }: { disabled: boolean; busy: boolean; onSend: (kind: ReportKind) => void }) {
  const t = useT();
  const [kind, setKind] = useState<ReportKind>("Morning");
  return (
    <span className="inline-flex">
      <select className={`${field.replace("w-full", "w-auto")} h-9 rounded-r-none border-r-0 text-xs`} value={kind} onChange={(e) => setKind(e.target.value as ReportKind)} aria-label={t("Какой отчёт")}>
        {reportKinds.map((k) => (
          <option key={k.key} value={k.key}>
            {t(k.label)}
          </option>
        ))}
      </select>
      <button className={`${button} rounded-l-none`} onClick={() => onSend(kind)} disabled={disabled} title={t("Отправить сейчас во все чаты (реальные данные)")}>
        {busy ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
        {t("Отправить")}
      </button>
    </span>
  );
}

/** Новая автоотправка или правка: регион → супервайзер (агенты подставятся сами) → чаты и группы → какие отчёты. */
function SubscriptionEditor({
  data,
  id,
  initial,
  onCancel,
  onSaved,
}: {
  data: ReportOverview;
  id: string | null;
  initial: ReportSubscriptionInput;
  onCancel: () => void;
  onSaved: () => Promise<void>;
}) {
  const t = useT();
  const [input, setInput] = useState(initial);
  const [search, setSearch] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const region = data.regions.find((r) => r.id === input.regionId);
  const chats = useMemo(() => {
    const q = search.trim().toLowerCase();
    return data.chats.filter((c) => !q || c.title.toLowerCase().includes(q) || (c.username ?? "").toLowerCase().includes(q));
  }, [data.chats, search]);

  const pickRegion = (regionId: string) => {
    const r = data.regions.find((x) => x.id === regionId);
    setInput((i) => ({ ...i, regionId, supervisorId: r?.supervisors[0]?.id ?? null }));
  };

  const toggleChat = (chatId: string) =>
    setInput((i) => ({ ...i, chatIds: i.chatIds.includes(chatId) ? i.chatIds.filter((c) => c !== chatId) : [...i.chatIds, chatId] }));

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      await reportsApi.saveSubscription(id, input);
      await onSaved();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const ready = input.regionId && input.chatIds.length > 0 && (input.morning || input.midday || input.evening);

  return (
    <div className="mt-4 rounded-xl border border-accent/30 bg-accent-soft/20 p-4">
      <h3 className="font-semibold text-ink">{id ? t("Автоотправка") : t("Новая автоотправка")}</h3>
      <div className="mt-4 grid gap-4 lg:grid-cols-2">
        <div className="space-y-4">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("1. Регион")}</span>
            <select className={field} value={input.regionId} onChange={(e) => pickRegion(e.target.value)}>
              {!region && <option value="">{t("Выберите регион")}</option>}
              {data.regions.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                </option>
              ))}
            </select>
            {region?.manager && <span className="mt-1 block text-xs text-ink-3">{t("РМ")}: {region.manager}</span>}
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("2. Супервайзер")}</span>
            <select
              className={field}
              value={input.supervisorId ?? ""}
              onChange={(e) => setInput((i) => ({ ...i, supervisorId: e.target.value ? Number(e.target.value) : null }))}
              disabled={!region}
            >
              {region?.supervisors.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
              <option value="">
                {region?.supervisorName ? t("СВР из оргструктуры: {name}", { name: region.supervisorName }) : t("Без супервайзера")}
              </option>
            </select>
          </label>

          <div>
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Агенты на картинке")}</span>
            {region ? (
              region.agents.length > 0 ? (
                <div className="flex flex-wrap gap-1.5">
                  {region.agents.map((a) => (
                    <span key={a} className="rounded-full bg-muted px-2.5 py-0.5 text-xs text-ink-2">
                      {a}
                    </span>
                  ))}
                </div>
              ) : (
                <p className="text-xs text-warn">{t("В регионе нет агентов за этот месяц.")}</p>
              )
            ) : (
              <p className="text-xs text-ink-3">{t("Выберите регион.")}</p>
            )}
            <p className="mt-1.5 text-xs text-ink-3">{t("Подставляются сами — команда региона в «Продажах» на день отчёта.")}</p>
          </div>

          <div>
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("4. Какие отчёты")}</span>
            <div className="flex flex-wrap gap-4">
              {reportKinds.map((k) => (
                <label key={k.key} className="flex items-center gap-2 text-sm text-ink">
                  <input type="checkbox" checked={input[k.field]} onChange={() => setInput((i) => ({ ...i, [k.field]: !i[k.field] }))} className="size-4 accent-[var(--color-accent)]" />
                  {t(k.label)}
                </label>
              ))}
            </div>
          </div>
        </div>

        <div>
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("3. Чаты и группы")} <span className="font-normal text-ink-3">· {t("выбрано: {n}", { n: input.chatIds.length })}</span>
          </span>
          <div className="relative">
            <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" />
            <input className={`${field} pl-9`} value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Название или @username")} />
          </div>
          <div className="mt-2 max-h-[300px] overflow-y-auto rounded-lg border border-line bg-surface">
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
                      {!c.canSend && c.reason ? ` · ${t(c.reason)}` : ""}
                    </span>
                  </span>
                </label>
              ))
            )}
          </div>
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
