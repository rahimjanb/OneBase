"use client";

import Link from "next/link";
import { useState } from "react";
import { CheckCircle2, Eye, EyeOff, Loader2, PlugZap, RefreshCw, Save, XCircle } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { statusView } from "@/lib/integrations";
import { date, dateTime, num } from "@/lib/sales/format";
import { TAJIK_BASE, runStatusLabel, runTriggerLabel, type TajikIntegrationDetails } from "@/lib/sales/tajik";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const button = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

const runTone: Record<string, string> = { ok: "text-ok", partial: "text-warn", failed: "text-bad", running: "text-ink-2" };

type TestResult = { ok: boolean; message: string; salesLines: number | null; stockRows: number | null };

/**
 * «Настройки → Интеграции → Продажи → 1С Таджикистан»: адрес API и ключ доступа (сохранённый ключ не показывается — только последние
 * символы), включение, интервал, дата начала истории; проверка, синхронизация сейчас или всей истории, данные в OneBase и журнал запусков.
 */
export function TajikSettings({ initial }: { initial: TajikIntegrationDetails }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const s = data.settings;
  const [baseUrl, setBaseUrl] = useState(s.baseUrl);
  const [token, setToken] = useState("");
  const [showToken, setShowToken] = useState(false);
  const [enabled, setEnabled] = useState(s.enabled);
  const [interval, setIntervalMinutes] = useState(String(s.intervalMinutes));
  const [historyFrom, setHistoryFrom] = useState(s.historyFrom);
  const [busy, setBusy] = useState<string | null>(null);
  const [test, setTest] = useState<TestResult | null>(null);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  const insecure = baseUrl.trim().toLowerCase().startsWith("http://");
  const dirty =
    baseUrl.trim() !== s.baseUrl || token.trim() !== "" || enabled !== s.enabled || Number(interval) !== s.intervalMinutes || historyFrom !== s.historyFrom;

  const act = async (key: string, action: () => Promise<void>, ok?: string) => {
    setBusy(key);
    setMessage(null);
    try {
      await action();
      if (ok) setMessage({ ok: true, text: ok });
    } catch (e) {
      setMessage({ ok: false, text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const save = (tokenValue: string | null, ok: string) =>
    act(
      "save",
      async () => {
        const next = await bff<TajikIntegrationDetails>("integrations/tj1c", {
          method: "PUT",
          body: JSON.stringify({ baseUrl: baseUrl.trim(), token: tokenValue, enabled, intervalMinutes: Number(interval) || null, historyFrom: historyFrom || null }),
        });
        setData(next);
        setBaseUrl(next.settings.baseUrl);
        setIntervalMinutes(String(next.settings.intervalMinutes));
        setHistoryFrom(next.settings.historyFrom);
        setToken("");
      },
      ok,
    );

  const runTest = () =>
    act("test", async () => {
      setTest(await bff<TestResult>("integrations/tj1c/test", { method: "POST", body: JSON.stringify({ baseUrl: baseUrl.trim() || null, token: token.trim() || null }) }));
      setData(await bff<TajikIntegrationDetails>("integrations/tj1c"));
    });

  const sync = (full: boolean) =>
    act(
      full ? "full" : "sync",
      async () => {
        const next = await bff<TajikIntegrationDetails>("integrations/tj1c/sync", { method: "POST", body: JSON.stringify({ full }) });
        setData(next);
        const run = next.runs[0];
        if (run?.status === "failed") throw new Error(run.error ?? t("Синхронизация не прошла."));
      },
      t("Синхронизация завершена."),
    );

  const status = statusView[data.status];
  const tokenNote =
    s.tokenSource === "onebase" ? t("Ключ сохранён в OneBase: {hint}", { hint: s.tokenHint ?? "" }) : s.tokenSource === "env" ? t("Ключ взят из переменной окружения сервера: {hint}", { hint: s.tokenHint ?? "" }) : t("Ключ не задан");

  return (
    <div className="space-y-6">
      {message && (
        <div className={`rounded-lg px-4 py-2.5 text-sm ${message.ok ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`} role="status">
          {message.text}
        </div>
      )}

      <Section
        title={t("Подключение")}
        hint={
          <span className="inline-flex flex-wrap items-center gap-2">
            <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${status.className}`}>{t(status.label)}</span>
            {data.dataAsOf && <span>{t("данные на {when}", { when: dateTime(data.dataAsOf) })}</span>}
          </span>
        }
      >
        <div className="grid gap-4 md:grid-cols-2">
          <label className="block">
            <span className="text-sm font-medium text-ink">{t("Адрес API")}</span>
            <input className={`${field} mt-1.5`} value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} placeholder="http://62.60.246.93" autoComplete="off" />
            <span className="mt-1 block text-xs text-ink-3">{t("Без /sales и без ключа — OneBase добавит их сам.")}</span>
          </label>
          <label className="block">
            <span className="text-sm font-medium text-ink">{t("Ключ доступа (token)")}</span>
            <span className="relative mt-1.5 block">
              <input
                className={`${field} pr-10`}
                type={showToken ? "text" : "password"}
                value={token}
                onChange={(e) => setToken(e.target.value)}
                placeholder={s.hasToken ? t("оставьте пустым, чтобы не менять") : t("ключ из документа API")}
                autoComplete="new-password"
              />
              <button
                type="button"
                className="absolute right-2 top-2 rounded p-1 text-ink-3 hover:text-ink"
                onClick={() => setShowToken((v) => !v)}
                aria-label={showToken ? t("Скрыть ключ") : t("Показать ключ")}
              >
                {showToken ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
              </button>
            </span>
            <span className="mt-1 block text-xs text-ink-3">{tokenNote}</span>
          </label>
          <label className="block">
            <span className="text-sm font-medium text-ink">{t("Загружать каждые, минут")}</span>
            <input className={`${field} mt-1.5`} type="number" min={10} max={1440} value={interval} onChange={(e) => setIntervalMinutes(e.target.value)} />
            <span className="mt-1 block text-xs text-ink-3">{t("Окно — с 1-го числа прошлого месяца; раз в сутки ночью — вся история.")}</span>
          </label>
          <label className="block">
            <span className="text-sm font-medium text-ink">{t("История с")}</span>
            <input className={`${field} mt-1.5`} type="date" value={historyFrom} onChange={(e) => setHistoryFrom(e.target.value)} />
            <span className="mt-1 block text-xs text-ink-3">{t("В 1С Таджикистана продажи есть с 03.11.2026.")}</span>
          </label>
        </div>
        <label className="mt-4 inline-flex items-center gap-2 text-sm text-ink">
          <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />
          {t("Загружать данные по расписанию")}
        </label>

        {insecure && (
          <p className="mt-4 rounded-lg border border-warn/30 bg-warn-soft px-4 py-2.5 text-sm text-warn">
            {t("Адрес без HTTPS: ключ доступа и продажи идут по интернету открытым текстом. Попросите дилера включить HTTPS на сервере 1С и поменять ключ.")}
          </p>
        )}

        <div className="mt-5 flex flex-wrap gap-2">
          <button type="button" className={primary} disabled={busy !== null || !dirty} onClick={() => save(token.trim() || null, t("Настройки сохранены."))}>
            {busy === "save" ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
            {t("Сохранить")}
          </button>
          <button type="button" className={button} disabled={busy !== null} onClick={runTest}>
            {busy === "test" ? <Loader2 className="size-4 animate-spin" /> : <PlugZap className="size-4" />}
            {t("Проверить соединение")}
          </button>
          <button type="button" className={button} disabled={busy !== null || !s.configured || dirty} onClick={() => sync(false)} title={dirty ? t("Сначала сохраните изменения") : undefined}>
            {busy === "sync" ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
            {t("Синхронизировать сейчас")}
          </button>
          <button type="button" className={button} disabled={busy !== null || !s.configured || dirty} onClick={() => sync(true)}>
            {busy === "full" ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
            {t("Сверить всю историю")}
          </button>
          {s.tokenSource === "onebase" && (
            <button type="button" className={button} disabled={busy !== null} onClick={() => confirm(t("Удалить ключ доступа из OneBase?")) && save("", t("Ключ удалён из OneBase."))}>
              {t("Удалить ключ")}
            </button>
          )}
        </div>

        {(test || s.lastTestAt) && (
          <p className={`mt-4 flex items-start gap-2 text-sm ${(test?.ok ?? s.lastTestOk) ? "text-ok" : "text-bad"}`}>
            {(test?.ok ?? s.lastTestOk) ? <CheckCircle2 className="mt-0.5 size-4 shrink-0" /> : <XCircle className="mt-0.5 size-4 shrink-0" />}
            <span>
              {t(test?.message ?? s.lastTestMessage ?? "")}
              {!test && s.lastTestAt && <span className="text-ink-3"> · {dateTime(s.lastTestAt)}</span>}
            </span>
          </p>
        )}
      </Section>

      <Section title={t("Данные в OneBase")} actions={<Link href={TAJIK_BASE} className="text-sm font-medium text-accent-strong hover:underline">{t("Открыть «Экспорт → Таджикистан»")}</Link>}>
        <dl className="grid grid-cols-2 gap-x-6 gap-y-3 text-sm sm:grid-cols-4">
          <div>
            <dt className="text-xs text-ink-3">{t("Накладных")}</dt>
            <dd className="font-semibold tabular-nums text-ink">{num(data.stats.documents)}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-3">{t("Строк продаж")}</dt>
            <dd className="font-semibold tabular-nums text-ink">{num(data.stats.lines)}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-3">{t("Точек")}</dt>
            <dd className="font-semibold tabular-nums text-ink">{num(data.stats.stores)}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-3">{t("Период")}</dt>
            <dd className="font-semibold tabular-nums text-ink">{data.stats.from ? `${date(data.stats.from)} — ${date(data.stats.through)}` : "—"}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-3">{t("Остаток склада")}</dt>
            <dd className="font-semibold tabular-nums text-ink">{data.stats.stockDate ? t("на {date}", { date: date(data.stats.stockDate) }) : "—"}</dd>
          </div>
          <div>
            <dt className="text-xs text-ink-3">{t("Строк изменённых или пропавших накладных")}</dt>
            <dd className="font-semibold tabular-nums text-ink">{num(data.stats.removedLines)}</dd>
          </div>
        </dl>
        <Note>
          {t(
            "OneBase только читает 1С Таджикистана (GET /sales и /sklad). Изменённая или пропавшая в 1С накладная не удаляется: прежние строки остаются с отметкой и в отчёты не входят. Если 1С вернула меньше половины накладных окна, ничего не помечается.",
          )}
        </Note>
      </Section>

      <Section title={t("Журнал загрузок")} hint={t("последние 20 запусков")}>
        <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
          <table className="w-full min-w-max text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">{t("Начало")}</th>
                <th className="px-3 py-2 font-semibold">{t("Запуск")}</th>
                <th className="px-3 py-2 font-semibold">{t("Статус")}</th>
                <th className="px-3 py-2 font-semibold">{t("С даты")}</th>
                <th className="px-3 py-2 text-right font-semibold">{t("Накладных")}</th>
                <th className="px-3 py-2 text-right font-semibold">{t("Новых / изменено / пропало")}</th>
                <th className="px-3 py-2 text-right font-semibold">{t("Остаток, строк")}</th>
                <th className="py-2 pl-3 font-semibold">{t("Ошибка")}</th>
              </tr>
            </thead>
            <tbody>
              {data.runs.map((r) => (
                <tr key={r.id} className="border-b border-line/60 align-top">
                  <td className="py-2 pr-3 tabular-nums text-ink-2">{dateTime(r.startedAt)}</td>
                  <td className="px-3 py-2 text-ink-2">{t(runTriggerLabel[r.trigger] ?? r.trigger)}</td>
                  <td className={`px-3 py-2 font-medium ${runTone[r.status] ?? ""}`}>{t(runStatusLabel[r.status] ?? r.status)}</td>
                  <td className="px-3 py-2 tabular-nums text-ink-2">{date(r.from)}</td>
                  <td className="px-3 py-2 text-right tabular-nums">{num(r.documents)}</td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {num(r.added)} / {num(r.changed)} / {num(r.removed)}
                    {r.rejected > 0 && <span className="block text-xs text-warn">{t("не прочитано строк: {n}", { n: num(r.rejected) })}</span>}
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">{num(r.stockRows)}</td>
                  <td className="max-w-[360px] whitespace-normal py-2 pl-3 text-xs text-bad">{r.error ? t(r.error) : ""}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {data.runs.length === 0 && <p className="py-6 text-center text-sm text-ink-3">{t("Загрузок ещё не было.")}</p>}
        </div>
      </Section>
    </div>
  );
}
