"use client";

import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { CheckCircle2, Copy, History, KeyRound, Loader2, Pause, Play, PlugZap, RefreshCw, RotateCcw, ShieldAlert, TestTube2, XCircle } from "lucide-react";
import { Modal } from "@/components/files/Modal";
import { Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  changeActionLabel,
  commandLabel,
  date,
  entityLabel,
  failureKindLabel,
  full,
  moment,
  runKindLabel,
  runStatusView,
  stateLabel,
  stateTone,
  streamLabel,
  todayIso,
  type OneCChange,
  type OneCFailure,
  type OneCRun,
  type OneCStatus,
  type Page,
  type RunStep,
} from "@/lib/finance";

const button = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";
const field = "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const pill = "inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold";

type Tab = "state" | "runs" | "failures" | "changes";

/**
 * «Настройки → Интеграции → Финансы → 1С». Учётные данные 1С здесь не вводятся: они только у службы интеграции (переменные окружения
 * сервера), страница показывает состояние со слов службы — адрес, HTTPS, сертификат, авторизацию — без пароля. Действия уходят службе
 * командами; страница обновляется сама, пока что-то идёт.
 */
export function OneCSettings({ initial }: { initial: OneCStatus }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [tab, setTab] = useState<Tab>("state");
  const [busy, setBusy] = useState<string | null>(null);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [clearOpen, setClearOpen] = useState(false);

  const reload = useCallback(async () => {
    try {
      setData(await bff<OneCStatus>("integrations/onec"));
    } catch {
      // нет связи — покажем последнее состояние
    }
  }, []);

  // Пока идёт запуск, команда ждёт службу или идёт история — обновляем каждые 4 с, иначе — раз в 30 с.
  const active = !!data.running || data.commands.some((c) => c.status === "Pending" || c.status === "Taken") || data.historical?.status === "Running" || data.historical?.status === "Pending";
  useEffect(() => {
    const id = setInterval(reload, active ? 4000 : 30000);
    return () => clearInterval(id);
  }, [active, reload]);

  const act = async (key: string, run: () => Promise<unknown>, ok: string) => {
    setBusy(key);
    setMessage(null);
    try {
      await run();
      setMessage({ tone: "ok", text: t(ok) });
      await reload();
    } catch (e) {
      setMessage({ tone: "bad", text: e instanceof Error ? e.message : String(e) });
    } finally {
      setBusy(null);
    }
  };

  const command = (key: string, path: string, ok: string) => act(key, () => bff(`integrations/onec/${path}`, { method: "POST" }), ok);
  const s = data.settings;
  const w = data.worker;
  const c = w?.connection;
  const tabs: { key: Tab; label: string; badge?: number }[] = [
    { key: "state", label: "Подключение и синхронизация" },
    { key: "runs", label: "Журнал синхронизации" },
    { key: "failures", label: "Ошибки", badge: data.openFailures + data.rejectedRecords },
    { key: "changes", label: "Изменения из 1С" },
  ];

  return (
    <div className="space-y-4">
      <nav aria-label={t("Разделы интеграции")} className="-mx-4 flex gap-1 overflow-x-auto border-b border-line px-4 sm:mx-0 sm:px-0">
        {tabs.map((x) => (
          <button
            key={x.key}
            type="button"
            onClick={() => setTab(x.key)}
            aria-current={tab === x.key ? "page" : undefined}
            className={`-mb-px inline-flex shrink-0 items-center gap-1.5 border-b-2 px-3 py-2.5 text-sm font-medium ${tab === x.key ? "border-accent text-accent-strong" : "border-transparent text-ink-2 hover:text-ink"}`}
          >
            {t(x.label)}
            {!!x.badge && <span className="rounded-full bg-bad-soft px-1.5 text-xs text-bad">{x.badge}</span>}
          </button>
        ))}
      </nav>

      {message && <p className={`text-sm ${message.tone === "ok" ? "text-ok" : "text-bad"}`}>{message.text}</p>}

      {tab === "state" && (
        <>
          <Section title="Состояние" actions={<span className={`rounded-full px-2.5 py-1 text-xs font-semibold ${stateTone[data.state]}`}>{t(stateLabel[data.state])}</span>}>
            <p className={`text-sm ${data.state === "connected" ? "text-ink" : data.state === "error" || data.state === "mismatch" ? "text-bad" : "text-ink-2"}`}>{t(data.stateText)}</p>
            <dl className="mt-4 grid grid-cols-2 gap-x-6 gap-y-3 text-sm sm:grid-cols-3 lg:grid-cols-4">
              <Item label="Источник" value={w ? (w.provider === "mock" ? t("Тестовый источник (демо)") : "1С (HTTP API)") : "—"} />
              <Item label="Адрес 1С" value={w?.baseUrl ?? "—"} />
              <Item label="Сервер и порт" value={c?.host ? `${c.host}:${c.port}` : "—"} />
              <Item label="HTTPS" value={c ? (c.https ? t("да") : t("нет")) : "—"} tone={c && !c.https ? "bad" : undefined} />
              <Item label="Сертификат" value={c?.tlsValid == null ? "—" : c.tlsValid ? `${t("проверен")}${c.certificateExpires ? ` · ${t("до")} ${date(c.certificateExpires)}` : ""}` : t("не прошёл проверку")} tone={c?.tlsValid === false ? "bad" : undefined} />
              <Item label="Авторизация" value={c?.authOk == null ? "—" : c.authOk ? t("принята") : t("отклонена")} tone={c?.authOk === false ? "bad" : undefined} />
              <Item label="База 1С" value={s.acceptedInfobaseName ?? c?.infobaseName ?? "—"} />
              <Item label="Версия API" value={c?.apiVersion ?? "—"} />
              <Item label="Служба интеграции" value={w ? `${w.online ? t("на связи") : t("не на связи")} · ${moment(w.lastSeenAt)}${w.version ? ` · v${w.version}` : ""}` : t("ещё не выходила на связь")} tone={w && !w.online ? "bad" : undefined} />
              <Item label="Загрузка данных у службы" value={w ? (w.enabled ? t("включена") : t("выключена (OneC__Enabled=false)")) : "—"} />
            </dl>
            {c && c.steps.length > 0 && (
              <ul className="mt-4 space-y-1.5 border-t border-line pt-3 text-sm">
                {c.steps.map((step) => (
                  <li key={step.code} className="flex items-start gap-2">
                    {step.ok ? <CheckCircle2 aria-hidden className="mt-0.5 size-4 shrink-0 text-ok" /> : <XCircle aria-hidden className="mt-0.5 size-4 shrink-0 text-bad" />}
                    <span className="text-ink-2">{t(step.message)}</span>
                  </li>
                ))}
                <li className="text-xs text-ink-3">{t("Проверено {at}", { at: moment(c.checkedAt) })}</li>
              </ul>
            )}
          </Section>

          <Section title="Синхронизация" hint={`${t("каждые {n} мин", { n: s.intervalMinutes })} · ${t("пакет {n} записей", { n: s.batchSize })}`}>
            <dl className="grid grid-cols-2 gap-x-6 gap-y-3 text-sm sm:grid-cols-3 lg:grid-cols-4">
              <Item label="Последняя успешная" value={data.lastSuccess ? moment(data.lastSuccess.finishedAt) : t("не было")} />
              <Item label="Следующая" value={s.paused ? t("пауза") : data.nextSyncAt ? moment(data.nextSyncAt) : "—"} />
              <Item label="Длительность последней" value={data.lastRun?.durationSeconds != null ? t("{n} с", { n: data.lastRun.durationSeconds }) : "—"} />
              <Item label="Последний запуск" value={data.lastRun ? `${t(runStatusView[data.lastRun.status]?.label ?? data.lastRun.status)} · ${moment(data.lastRun.startedAt)}` : "—"} tone={data.lastRun?.status === "Failed" ? "bad" : undefined} />
              <Item label="Загружено (последний запуск)" value={data.lastRun ? full(data.lastRun.created) : "—"} />
              <Item label="Обновлено (последний запуск)" value={data.lastRun ? full(data.lastRun.updated) : "—"} />
              <Item label="Ошибок к повтору" value={full(data.openFailures)} tone={data.openFailures > 0 ? "bad" : undefined} />
              <Item label="Отклонённых записей" value={full(data.rejectedRecords)} tone={data.rejectedRecords > 0 ? "bad" : undefined} />
            </dl>
            {data.running && (
              <p className="mt-3 flex items-center gap-2 text-sm text-accent-strong">
                <Loader2 aria-hidden className="size-4 animate-spin" />
                {t("Идёт: {kind}", { kind: t(runKindLabel[data.running.kind] ?? data.running.kind) })} {data.running.step && `· ${streamLabel(data.running.step)}`} · {t("новых {n}", { n: full(data.running.created) })}
              </p>
            )}
            {data.lastRun?.message && <p className="mt-3 text-sm text-ink-2">{data.lastRun.message}</p>}
            <div className="mt-4 flex flex-wrap gap-2">
              <button type="button" className={button} disabled={busy !== null} onClick={() => command("test", "test", "Служба проверит подключение в течение нескольких секунд.")}>
                {busy === "test" ? <Loader2 className="size-4 animate-spin" /> : <PlugZap className="size-4" />} {t("Проверить соединение")}
              </button>
              <button type="button" className={button} disabled={busy !== null} onClick={() => command("testSync", "test-sync", "Тестовая синхронизация запущена: проверка без записи данных.")}>
                {busy === "testSync" ? <Loader2 className="size-4 animate-spin" /> : <TestTube2 className="size-4" />} {t("Тестовая синхронизация")}
              </button>
              <button type="button" className={primary} disabled={busy !== null || !!data.running} onClick={() => command("sync", "sync", "Синхронизация запущена.")}>
                {busy === "sync" ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />} {t("Синхронизировать сейчас")}
              </button>
              {s.paused ? (
                <button type="button" className={button} disabled={busy !== null} onClick={() => command("resume", "resume", "Синхронизация возобновлена.")}>
                  <Play className="size-4" /> {t("Возобновить")}
                </button>
              ) : (
                <button type="button" className={button} disabled={busy !== null} onClick={() => command("pause", "pause", "Автоматическая синхронизация приостановлена.")}>
                  <Pause className="size-4" /> {t("Приостановить")}
                </button>
              )}
              {data.openFailures + data.rejectedRecords > 0 && (
                <button type="button" className={button} disabled={busy !== null} onClick={() => act("retry", () => bff("integrations/onec/failures/retry", { method: "POST", body: JSON.stringify({}) }), "Служба повторит неуспешные операции.")}>
                  <RotateCcw className="size-4" /> {t("Повторить неуспешные")}
                </button>
              )}
            </div>
            <Commands data={data} />
          </Section>

          <HistoricalSection data={data} busy={busy} onStart={() => setHistoryOpen(true)} onCancel={() => act("cancelHistory", () => bff("integrations/onec/historical/cancel", { method: "POST" }), "Историческая загрузка остановлена — загруженное сохранено.")} />

          <Section title="Загружено из 1С">
            <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
              {(
                [
                  ["Организации", data.counts.organizations],
                  ["Счета", data.counts.accounts],
                  ["Контрагенты", data.counts.counterparties],
                  ["Договоры", data.counts.contracts],
                  ["Номенклатура", data.counts.items],
                  ["Справочники", data.counts.references],
                  ["Курсы валют", data.counts.rates],
                  ["Документы", data.counts.documents],
                  ["Проводки", data.counts.entries],
                  ["Строки остатков", data.counts.balances],
                  ["Изменений в журнале", data.counts.changes],
                ] as const
              ).map(([label, value]) => (
                <div key={label} className="rounded-lg border border-line p-3">
                  <div className="text-lg font-semibold tabular-nums text-ink">{full(value)}</div>
                  <div className="text-xs text-ink-3">{t(label)}</div>
                </div>
              ))}
            </div>
            {data.counts.entries > 0 && (
              <Link href="/finance" className="mt-3 inline-block text-sm font-medium text-accent-strong hover:underline">
                {t("Открыть «Финансы»")}
              </Link>
            )}
          </Section>

          <SettingsSection data={data} onSaved={reload} />

          <Section title="Ключ службы интеграции" hint="Служба входит во внутренний API OneBase только с этим ключом">
            <p className="text-sm text-ink-2">
              {s.hasToken ? t("Ключ создан {at}, заканчивается на {hint}.", { at: moment(s.tokenCreatedAt), hint: s.tokenHint ?? "" }) : t("Ключа ещё нет — без него служба не подключится к OneBase.")}
            </p>
            {token && (
              <div className="mt-3 rounded-lg border border-warn/40 bg-warn-soft p-3 text-sm">
                <p className="font-medium text-warn">{t("Скопируйте ключ сейчас — больше он показан не будет.")}</p>
                <div className="mt-2 flex items-center gap-2">
                  <code className="min-w-0 flex-1 truncate rounded bg-surface px-2 py-1 font-mono text-xs text-ink">{token}</code>
                  <button type="button" className={button} onClick={() => navigator.clipboard?.writeText(token)}>
                    <Copy className="size-4" /> {t("Копировать")}
                  </button>
                </div>
                <p className="mt-2 text-ink-2">{t("На сервере OneBase: в .env — ONEC_INTEGRATION_TOKEN=<ключ>, затем перезапустить службу: docker compose --profile onec up -d onec-worker.")}</p>
              </div>
            )}
            <button
              type="button"
              className={`${button} mt-3`}
              disabled={busy !== null}
              onClick={() => {
                if (!s.hasToken || confirm(t("Создать новый ключ? Прежний перестанет действовать сразу — службу нужно будет перезапустить с новым ключом.")))
                  void act("token", async () => setToken((await bff<{ token: string }>("integrations/onec/token", { method: "POST" })).token), "Ключ создан.");
              }}
            >
              <KeyRound className="size-4" /> {t(s.hasToken ? "Создать новый ключ" : "Создать ключ")}
            </button>
          </Section>

          {(data.state === "mismatch" || s.demoData) && (
            <Section title="Подключить другую базу 1С" hint={data.state === "mismatch" ? "служба подключена к другой базе" : "сейчас загружены демо-данные"}>
              <p className="flex items-start gap-2 text-sm text-ink-2">
                <ShieldAlert aria-hidden className="mt-0.5 size-4 shrink-0 text-bad" />
                {t("Данные разных баз 1С не смешиваются. Чтобы загрузить другую базу (например, рабочую после тестовой или демо-данных), загруженные данные прежней базы удаляются; журнал синхронизаций остаётся.")}
              </p>
              <button type="button" className={`${button} mt-3 text-bad`} onClick={() => setClearOpen(true)}>
                {t("Очистить данные 1С…")}
              </button>
            </Section>
          )}

          <Note>
            {t(
              "Только чтение: OneBase ничего не записывает в 1С. Пароль 1С хранится только у службы интеграции на сервере (переменные OneC__*), в OneBase и браузер не передаётся. Доступ AI к данным 1С включается отдельно, ниже в настройках.",
            )}
          </Note>
        </>
      )}

      {tab === "runs" && <RunsTab />}
      {tab === "failures" && <FailuresTab onChanged={reload} />}
      {tab === "changes" && <ChangesTab />}

      {historyOpen && <HistoricalDialog batch={s.batchSize} onClose={() => setHistoryOpen(false)} onStarted={async () => { setHistoryOpen(false); setMessage({ tone: "ok", text: t("Историческая загрузка поставлена в очередь службы.") }); await reload(); }} />}
      {clearOpen && <ClearDialog onClose={() => setClearOpen(false)} onDone={async (n) => { setClearOpen(false); setMessage({ tone: "ok", text: t("Удалено записей 1С: {n}. Служба загрузит базу, к которой подключена.", { n: full(n) }) }); await reload(); }} />}
    </div>
  );
}

function Item({ label, value, tone }: { label: string; value: React.ReactNode; tone?: "bad" }) {
  const t = useT();
  return (
    <div className="min-w-0">
      <dt className="text-xs text-ink-3">{t(label)}</dt>
      <dd className={`truncate ${tone === "bad" ? "text-bad" : "text-ink"}`} title={typeof value === "string" ? value : undefined}>
        {value}
      </dd>
    </div>
  );
}

function Commands({ data }: { data: OneCStatus }) {
  const t = useT();
  if (data.commands.length === 0) return null;
  return (
    <details className="mt-4 text-sm">
      <summary className="cursor-pointer text-ink-2">{t("Последние команды службе")}</summary>
      <ul className="mt-2 space-y-1">
        {data.commands.map((c) => (
          <li key={c.id} className="flex flex-wrap items-baseline gap-2">
            <span className="text-ink">{t(commandLabel[c.kind] ?? c.kind)}</span>
            <span className={`${pill} ${c.status === "Done" ? "bg-ok-soft text-ok" : c.status === "Failed" ? "bg-bad-soft text-bad" : c.status === "Expired" ? "bg-muted text-ink-2" : "bg-accent-soft text-accent-strong"}`}>
              {t({ Pending: "ждёт службу", Taken: "выполняется", Done: "выполнена", Failed: "ошибка", Expired: "не выполнена" }[c.status] ?? c.status)}
            </span>
            <span className="text-xs text-ink-3">{moment(c.requestedAt)}</span>
            {c.message && <span className="w-full text-xs text-ink-2">{c.message}</span>}
          </li>
        ))}
      </ul>
    </details>
  );
}

function HistoricalSection({ data, busy, onStart, onCancel }: { data: OneCStatus; busy: string | null; onStart: () => void; onCancel: () => void }) {
  const t = useT();
  const h = data.historical;
  const running = h && (h.status === "Pending" || h.status === "Running");
  const bad = h?.checks.filter((c) => !c.ok) ?? [];
  const pct = h && h.periodsTotal > 0 ? Math.round((h.periodsDone / h.periodsTotal) * 100) : 0;
  return (
    <Section title="Историческая загрузка" hint="история из 1С по месяцам; повторная загрузка не создаёт дублей">
      {h ? (
        <div className="space-y-3 text-sm">
          <div className="flex flex-wrap items-center gap-2">
            <span className={`${pill} ${runStatusView[h.status]?.className ?? "bg-muted text-ink-2"}`}>{t(runStatusView[h.status]?.label ?? h.status)}</span>
            <span className="text-ink-2">
              {date(h.from)} — {date(h.to)} · {t("пакет {n}", { n: h.batchSize })}
            </span>
            <span className="text-ink-3">{t("запрошена {at}", { at: moment(h.requestedAt) })}</span>
          </div>
          <div>
            <div className="h-2 rounded-full bg-muted">
              <div className="h-2 rounded-full bg-accent transition-all" style={{ width: `${pct}%` }} />
            </div>
            <p className="mt-1 text-xs text-ink-3">
              {t("Месяцев {done} из {total} · записей {records}", { done: h.periodsDone, total: h.periodsTotal, records: full(h.records) })}
              {running && h.currentPeriod && ` · ${t("сейчас")}: ${h.currentPeriod.slice(0, 7)} · ${streamLabel(`history:${h.currentStream}`)}`}
            </p>
          </div>
          {h.checks.length > 0 && (
            <p className={bad.length ? "text-bad" : "text-ok"}>
              {bad.length ? t("Сверка с итогами 1С: расхождений {n} из {total}", { n: bad.length, total: h.checks.length }) : t("Сверка с итогами 1С: все {n} совпали", { n: h.checks.length })}
            </p>
          )}
          {bad.length > 0 && (
            <ul className="space-y-1 text-xs text-bad">
              {bad.slice(0, 10).map((c) => (
                <li key={`${c.period}-${c.organizationId}-${c.stream}`}>
                  {c.period.slice(0, 7)} · {streamLabel(`history:${c.stream}`)} · {c.note}
                </li>
              ))}
            </ul>
          )}
          {h.message && <p className="text-ink-2">{h.message}</p>}
        </div>
      ) : (
        <p className="text-sm text-ink-2">{t("Историю ещё не загружали. Обычная синхронизация берёт изменения с момента подключения — прошлые месяцы загружаются здесь.")}</p>
      )}
      <div className="mt-4 flex flex-wrap gap-2">
        <button type="button" className={button} disabled={busy !== null || !!running} onClick={onStart}>
          <History className="size-4" /> {t("Запустить историческую загрузку…")}
        </button>
        {running && (
          <button type="button" className={button} disabled={busy !== null} onClick={onCancel}>
            {t("Остановить загрузку")}
          </button>
        )}
      </div>
    </Section>
  );
}

function HistoricalDialog({ batch, onClose, onStarted }: { batch: number; onClose: () => void; onStarted: () => void }) {
  const t = useT();
  const today = todayIso();
  const [form, setForm] = useState({ from: `${Number(today.slice(0, 4)) - 1}-01-01`, to: today, batchSize: String(batch) });
  const [agree, setAgree] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      await bff("integrations/onec/historical", { method: "POST", body: JSON.stringify({ from: form.from, to: form.to, batchSize: Number(form.batchSize) || batch, confirm: true }) });
      onStarted();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };
  return (
    <Modal title={t("Историческая загрузка из 1С")} onClose={onClose}>
      <div className="space-y-4 text-sm">
        <p className="text-ink-2">
          {t("Служба загрузит из 1С курсы, документы, проводки и остатки за период — по месяцам, пакетами. Это нагрузка на сервер 1С: лучше запускать вне рабочих часов бухгалтерии. После сбоя загрузка продолжится с того же места, повтор не создаёт дублей.")}
        </p>
        <div className="grid gap-3 sm:grid-cols-3">
          <label className="block">
            <span className="mb-1 block font-medium text-ink">{t("С")}</span>
            <input type="date" className={field} value={form.from} max={form.to} onChange={(e) => setForm({ ...form, from: e.target.value })} />
          </label>
          <label className="block">
            <span className="mb-1 block font-medium text-ink">{t("По")}</span>
            <input type="date" className={field} value={form.to} max={today} onChange={(e) => setForm({ ...form, to: e.target.value })} />
          </label>
          <label className="block">
            <span className="mb-1 block font-medium text-ink">{t("Записей в пакете")}</span>
            <input className={field} inputMode="numeric" value={form.batchSize} onChange={(e) => setForm({ ...form, batchSize: e.target.value })} />
          </label>
        </div>
        <label className="flex items-start gap-2 text-ink">
          <input type="checkbox" checked={agree} onChange={(e) => setAgree(e.target.checked)} className="mt-0.5 size-4 accent-[var(--color-accent)]" />
          {t("Понимаю, что загрузка нагружает 1С и может идти долго; запускаю её как администратор интеграций.")}
        </label>
        {error && <p className="text-bad">{error}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" className={button} onClick={onClose}>
            {t("Отмена")}
          </button>
          <button type="button" className={primary} disabled={!agree || busy || !form.from || !form.to || form.from > form.to} onClick={submit}>
            {busy && <Loader2 className="size-4 animate-spin" />} {t("Запустить загрузку")}
          </button>
        </div>
      </div>
    </Modal>
  );
}

function ClearDialog({ onClose, onDone }: { onClose: () => void; onDone: (removed: number) => void }) {
  const t = useT();
  const [word, setWord] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  return (
    <Modal title={t("Очистить данные 1С")} onClose={onClose}>
      <div className="space-y-3 text-sm">
        <p className="text-ink-2">{t("Будут удалены все загруженные из 1С данные: справочники, документы, проводки, остатки, журнал изменений, курсоры и очередь ошибок. В 1С ничего не меняется. Действие нельзя отменить — данные загрузятся заново из подключённой базы.")}</p>
        <label className="block">
          <span className="mb-1 block font-medium text-ink">{t("Для подтверждения введите слово ОЧИСТИТЬ")}</span>
          <input className={field} value={word} onChange={(e) => setWord(e.target.value)} autoComplete="off" />
        </label>
        {error && <p className="text-bad">{error}</p>}
        <div className="flex justify-end gap-2">
          <button type="button" className={button} onClick={onClose}>
            {t("Отмена")}
          </button>
          <button
            type="button"
            className={`${primary} !bg-bad`}
            disabled={busy || word.trim().toUpperCase() !== "ОЧИСТИТЬ"}
            onClick={async () => {
              setBusy(true);
              setError(null);
              try {
                const result = await bff<{ removed: number }>("integrations/onec/clear", { method: "POST", body: JSON.stringify({ confirmation: word }) });
                onDone(result.removed);
              } catch (e) {
                setError(e instanceof Error ? e.message : String(e));
              } finally {
                setBusy(false);
              }
            }}
          >
            {t("Очистить")}
          </button>
        </div>
      </div>
    </Modal>
  );
}

function SettingsSection({ data, onSaved }: { data: OneCStatus; onSaved: () => Promise<void> }) {
  const t = useT();
  const s = data.settings;
  const [form, setForm] = useState({ interval: String(s.intervalMinutes), batch: String(s.batchSize), ai: s.aiEnabled });
  const [state, setState] = useState<{ busy: boolean; message: string | null; bad?: boolean }>({ busy: false, message: null });
  const dirty = Number(form.interval) !== s.intervalMinutes || Number(form.batch) !== s.batchSize || form.ai !== s.aiEnabled;
  return (
    <Section title="Настройки">
      <div className="grid gap-4 sm:grid-cols-2">
        <label className="block text-sm">
          <span className="mb-1 block font-medium text-ink">{t("Синхронизировать каждые, минут")}</span>
          <input className={field} inputMode="numeric" value={form.interval} onChange={(e) => setForm({ ...form, interval: e.target.value })} />
          <span className="mt-1 block text-xs text-ink-3">{t("5–1440, по умолчанию 30")}</span>
        </label>
        <label className="block text-sm">
          <span className="mb-1 block font-medium text-ink">{t("Записей в пакете")}</span>
          <input className={field} inputMode="numeric" value={form.batch} onChange={(e) => setForm({ ...form, batch: e.target.value })} />
          <span className="mt-1 block text-xs text-ink-3">{t("50–2000, по умолчанию 500")}</span>
        </label>
      </div>
      <label className="mt-4 flex items-start gap-2 text-sm text-ink">
        <input type="checkbox" checked={form.ai} onChange={(e) => setForm({ ...form, ai: e.target.checked })} className="mt-0.5 size-4 accent-[var(--color-accent)]" />
        <span>
          {t("AI-консультант может отвечать по данным 1С")}
          <span className="block text-xs text-ink-3">
            {t("Только пользователям с правом «Финансы». Для ответа итоги и документы 1С передаются во внешнюю AI-модель (как и остальные данные консультанта); данные сотрудников и физлиц не передаются. По умолчанию выключено.")}
          </span>
        </span>
      </label>
      <div className="mt-4 flex flex-wrap items-center gap-3">
        <button
          type="button"
          className={primary}
          disabled={!dirty || state.busy}
          onClick={async () => {
            setState({ busy: true, message: null });
            try {
              await bff("integrations/onec/settings", { method: "PUT", body: JSON.stringify({ intervalMinutes: Number(form.interval), batchSize: Number(form.batch), aiEnabled: form.ai }) });
              await onSaved();
              setState({ busy: false, message: t("Сохранено.") });
            } catch (e) {
              setState({ busy: false, message: e instanceof Error ? e.message : String(e), bad: true });
            }
          }}
        >
          {state.busy && <Loader2 className="size-4 animate-spin" />} {t("Сохранить")}
        </button>
        {state.message && <span className={`text-sm ${state.bad ? "text-bad" : "text-ok"}`}>{state.message}</span>}
      </div>
    </Section>
  );
}

function RunsTab() {
  const t = useT();
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Page<OneCRun> | null>(null);
  const [open, setOpen] = useState<string | null>(null);
  const [steps, setSteps] = useState<RunStep[]>([]);
  useEffect(() => {
    void bff<Page<OneCRun>>(`integrations/onec/runs?page=${page}&pageSize=30`).then(setData).catch(() => setData({ items: [], total: 0, page, pageSize: 30 }));
  }, [page]);
  const toggle = async (id: string) => {
    if (open === id) return setOpen(null);
    setOpen(id);
    setSteps((await bff<{ steps: RunStep[] }>(`integrations/onec/runs/${id}`)).steps);
  };
  if (!data) return <Loader2 className="size-5 animate-spin text-ink-3" />;
  return (
    <Section title="Журнал синхронизации" hint={t("всего {n}", { n: data.total })}>
      {data.items.length === 0 ? (
        <p className="text-sm text-ink-3">{t("Синхронизаций ещё не было.")}</p>
      ) : (
        <ul className="divide-y divide-line">
          {data.items.map((r) => (
            <li key={r.id} className="py-2.5 text-sm">
              <button type="button" onClick={() => toggle(r.id)} className="flex w-full flex-wrap items-center gap-x-3 gap-y-1 text-left">
                <span className={`${pill} ${runStatusView[r.status]?.className ?? "bg-muted"}`}>{t(runStatusView[r.status]?.label ?? r.status)}</span>
                <span className="font-medium text-ink">{t(runKindLabel[r.kind] ?? r.kind)}</span>
                <span className="text-ink-3">{moment(r.startedAt)}</span>
                {r.durationSeconds != null && <span className="text-ink-3">{t("{n} с", { n: r.durationSeconds })}</span>}
                <span className="text-ink-2">{t("новых {a}, изменено {b}, удалено {c}, отклонено {d}", { a: full(r.created), b: full(r.updated), c: full(r.deleted), d: full(r.rejected) })}</span>
                {r.message && <span className="w-full text-xs text-ink-2">{r.message}</span>}
              </button>
              {open === r.id && (
                <ul className="mt-2 space-y-1 rounded-lg bg-muted/50 p-3 text-xs">
                  {steps.length === 0 && <li className="text-ink-3">{t("Шагов нет.")}</li>}
                  {steps.map((s, i) => (
                    <li key={i} className="flex flex-wrap gap-2">
                      {s.error ? <XCircle aria-hidden className="size-3.5 text-bad" /> : <CheckCircle2 aria-hidden className="size-3.5 text-ok" />}
                      <span className="text-ink">{streamLabel(s.stream)}</span>
                      <span className="text-ink-3">{t("{n} записей · {ms} мс", { n: full(s.rows), ms: full(s.durationMs) })}</span>
                      {s.error && <span className="w-full text-bad">{s.error}</span>}
                    </li>
                  ))}
                </ul>
              )}
            </li>
          ))}
        </ul>
      )}
      <Pager page={page} total={data.total} size={30} onPage={setPage} />
    </Section>
  );
}

function FailuresTab({ onChanged }: { onChanged: () => Promise<void> }) {
  const t = useT();
  const [status, setStatus] = useState<"open" | "resolved">("open");
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Page<OneCFailure> | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const load = useCallback(() => bff<Page<OneCFailure>>(`integrations/onec/failures?status=${status}&page=${page}&pageSize=30`).then(setData), [status, page]);
  useEffect(() => {
    void load().catch(() => setData({ items: [], total: 0, page, pageSize: 30 }));
  }, [load, page]);
  const retry = async (ids: string[] | null) => {
    await bff("integrations/onec/failures/retry", { method: "POST", body: JSON.stringify({ ids }) });
    setSelected([]);
    await load();
    await onChanged();
  };
  if (!data) return <Loader2 className="size-5 animate-spin text-ink-3" />;
  return (
    <Section
      title="Ошибки синхронизации"
      hint="ничего не теряется: неполученная страница, незаписанный пакет или отклонённая запись хранятся целиком и повторяются"
      actions={
        <div className="flex gap-1">
          {(["open", "resolved"] as const).map((s) => (
            <button key={s} type="button" onClick={() => { setStatus(s); setPage(1); }} className={`h-8 rounded-full border px-3 text-xs ${status === s ? "border-accent bg-accent-soft text-accent-strong" : "border-line text-ink-2"}`}>
              {t(s === "open" ? "Открытые" : "Решённые")}
            </button>
          ))}
        </div>
      }
    >
      {data.items.length === 0 ? (
        <p className="text-sm text-ink-3">{t(status === "open" ? "Открытых ошибок нет." : "Решённых ошибок нет.")}</p>
      ) : (
        <ul className="divide-y divide-line">
          {data.items.map((f) => (
            <li key={f.id} className="flex items-start gap-3 py-2.5 text-sm">
              {status === "open" && (
                <input
                  type="checkbox"
                  aria-label={t("Выбрать")}
                  checked={selected.includes(f.id)}
                  onChange={(e) => setSelected(e.target.checked ? [...selected, f.id] : selected.filter((x) => x !== f.id))}
                  className="mt-1 size-4 accent-[var(--color-accent)]"
                />
              )}
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="font-medium text-ink">{t(failureKindLabel[f.kind] ?? f.kind)}</span>
                  <span className="text-ink-3">{streamLabel(f.stream)}</span>
                  {f.entityType && <span className="text-ink-3">· {t(entityLabel[f.entityType] ?? f.entityType)} {f.sourceId}</span>}
                  {f.period && <span className="text-ink-3">· {f.period.slice(0, 7)}</span>}
                </div>
                <div className="text-ink-2">{f.error}</div>
                <div className="text-xs text-ink-3">
                  {t("Попыток {n} · последняя {at}", { n: f.attempts, at: moment(f.lastAt) })}
                  {f.status !== "Open" && ` · ${t({ Retrying: "повтор запрошен", Resolved: "решено", Ignored: "отложено" }[f.status] ?? f.status)}`}
                  {f.hasPayload && ` · ${t("данные сохранены")}`}
                </div>
              </div>
              {status === "open" && f.status === "Open" && (
                <button type="button" className="shrink-0 text-xs text-ink-3 hover:text-ink" onClick={async () => { await bff(`integrations/onec/failures/${f.id}/ignore`, { method: "POST" }); await load(); await onChanged(); }}>
                  {t("Отложить")}
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
      {status === "open" && data.items.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-2">
          <button type="button" className={button} disabled={selected.length === 0} onClick={() => retry(selected)}>
            <RotateCcw className="size-4" /> {t("Повторить выбранные")}
          </button>
          <button type="button" className={button} onClick={() => retry(null)}>
            {t("Повторить все")}
          </button>
        </div>
      )}
      <Pager page={page} total={data.total} size={30} onPage={setPage} />
    </Section>
  );
}

function ChangesTab() {
  const t = useT();
  const [page, setPage] = useState(1);
  const [data, setData] = useState<Page<OneCChange> | null>(null);
  useEffect(() => {
    void bff<Page<OneCChange>>(`integrations/onec/changes?page=${page}&pageSize=30`).then(setData).catch(() => setData({ items: [], total: 0, page, pageSize: 30 }));
  }, [page]);
  if (!data) return <Loader2 className="size-5 animate-spin text-ink-3" />;
  const fields = (json: string | null) => {
    if (!json) return null;
    try {
      const parsed = JSON.parse(json) as Record<string, [unknown, unknown]>;
      return Object.entries(parsed).slice(0, 6).map(([k, [a, b]]) => `${k}: ${a ?? "—"} → ${b ?? "—"}`).join("; ");
    } catch {
      return null;
    }
  };
  return (
    <Section title="Изменения из 1С" hint="исправления, перепроведение, отмена проведения и удаления загруженных записей — новые записи сюда не пишутся">
      {data.items.length === 0 ? (
        <p className="text-sm text-ink-3">{t("Изменений ещё не было.")}</p>
      ) : (
        <ul className="divide-y divide-line">
          {data.items.map((c, i) => (
            <li key={i} className="py-2 text-sm">
              <div className="flex flex-wrap items-center gap-2">
                <span className={`${pill} ${c.action === "deleted" || c.action === "cancelled" ? "bg-bad-soft text-bad" : "bg-muted text-ink-2"}`}>{t(changeActionLabel[c.action] ?? c.action)}</span>
                <span className="text-ink">{t(entityLabel[c.entityType] ?? c.entityType)}</span>
                <span className="font-mono text-xs text-ink-3">{c.sourceEntityId}</span>
                <span className="text-xs text-ink-3">{moment(c.at)}</span>
              </div>
              {fields(c.changes) && <div className="mt-0.5 text-xs text-ink-2">{fields(c.changes)}</div>}
            </li>
          ))}
        </ul>
      )}
      <Pager page={page} total={data.total} size={30} onPage={setPage} />
    </Section>
  );
}

function Pager({ page, total, size, onPage }: { page: number; total: number; size: number; onPage: (p: number) => void }) {
  const t = useT();
  const pages = Math.ceil(total / size);
  if (pages <= 1) return null;
  return (
    <div className="mt-3 flex items-center justify-end gap-2 text-sm">
      <button type="button" className={button} disabled={page <= 1} onClick={() => onPage(page - 1)}>
        {t("Назад")}
      </button>
      <span className="text-ink-3">{t("{page} из {pages}", { page, pages })}</span>
      <button type="button" className={button} disabled={page >= pages} onClick={() => onPage(page + 1)}>
        {t("Дальше")}
      </button>
    </div>
  );
}
