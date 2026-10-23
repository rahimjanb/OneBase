"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { CheckCircle2, Eye, EyeOff, Loader2, PlugZap, RefreshCw, XCircle } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { CancelSyncButton } from "@/components/sales/CancelSyncButton";
import { SyncProgressBar } from "@/components/sales/SyncProgressBar";
import { useT } from "@/i18n/client";
import { makeT } from "@/i18n/core";
import { linkoEntityLabels, progressText, statusView, type LinkoDetails, type LinkoTestResult } from "@/lib/integrations";
import { dateTime, num } from "@/lib/sales/format";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const button =
  "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const danger =
  "inline-flex h-9 items-center gap-2 rounded-lg border border-bad/30 bg-surface px-4 text-sm font-medium text-bad hover:bg-bad-soft disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

type Translate = ReturnType<typeof useT>;

async function call<T>(path: string, init?: RequestInit, t: Translate = makeT(null)): Promise<T> {
  const response = await fetch(`/bff/api/${path}`, { ...init, headers: { "Content-Type": "application/json" } });
  if (!response.ok) {
    const text = await response.text();
    let message = text;
    try {
      const json = JSON.parse(text);
      message = json.error ?? json.detail ?? json.title ?? text;
    } catch {
      // не JSON
    }
    throw new Error(response.status === 403 ? t("Недостаточно прав для изменения интеграций.") : message ? t(message) : t("Ошибка {status}", { status: response.status }));
  }
  return (response.status === 202 || response.status === 204 ? undefined : await response.json()) as T;
}

const sourceLabel = { OneBase: "сохранён в OneBase", Environment: "взят из .env сервера", None: "" } as const;

export function LinkoSettings({ initial }: { initial: LinkoDetails }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [baseUrl, setBaseUrl] = useState(initial.baseUrl);
  const [token, setToken] = useState("");
  const [showToken, setShowToken] = useState(false);
  const [planToken, setPlanToken] = useState("");
  const [enabled, setEnabled] = useState(initial.enabled);
  const [busy, setBusy] = useState<"test" | "save" | "sync" | "clear" | "reset" | "verify" | null>(null);
  const [verify, setVerify] = useState<{ from: string; to: string; ok: boolean; rows: { entity: string; linko: number; oneBase: number; ok: boolean }[] } | null>(null);
  const [test, setTest] = useState<LinkoTestResult | null>(null);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);

  const dirty = baseUrl.trim() !== data.baseUrl || token.trim() !== "" || planToken.trim() !== "" || enabled !== data.enabled;

  const reload = async () => setData(await call<LinkoDetails>("integrations/linko", undefined, t));

  // Пока идёт синхронизация — обновляем карточку.
  useEffect(() => {
    if (!data.sync.isRunning) return;
    const timer = setInterval(() => void reload().catch(() => undefined), 3000);
    return () => clearInterval(timer);
  }, [data.sync.isRunning]);

  const runTest = async () => {
    setBusy("test");
    setMessage(null);
    try {
      setTest(
        await call<LinkoTestResult>(
          "integrations/linko/test",
          { method: "POST", body: JSON.stringify({ baseUrl: baseUrl.trim(), token: token.trim() || null, planToken: planToken.trim() || null }) },
          t,
        ),
      );
      await reload();
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const save = async (tokenValue: string | null, kind: "save" | "clear") => {
    setBusy(kind);
    setMessage(null);
    try {
      const saved = await call<LinkoDetails>(
        "integrations/linko",
        {
          method: "PUT",
          body: JSON.stringify({ baseUrl: baseUrl.trim(), token: tokenValue, planToken: kind === "save" ? planToken.trim() || null : null, enabled }),
        },
        t,
      );
      setData(saved);
      setBaseUrl(saved.baseUrl);
      setToken("");
      setPlanToken("");
      setMessage({ tone: "ok", text: kind === "clear" ? t("Токен удалён из OneBase.") : t("Настройки сохранены.") });
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const reset = async () => {
    const ok = confirm(
      `${t("Удалить все данные, загруженные из Linko, и загрузить их заново с {url}?", { url: data.baseUrl })}\n\n` +
        `${t("Будут удалены: заказы, возвраты, визиты, торговые точки, агенты, товары, остатки и планы ТП из Linko.")}\n` +
        t("Сохранятся данные OneBase: регионы с направлением, СВР и дилером, направления, планы РОП и «Завод», цели и настройки подключения."),
    );
    if (!ok) return;
    setBusy("reset");
    setMessage(null);
    try {
      await call("integrations/linko/reset", { method: "POST" }, t);
      setMessage({ tone: "ok", text: t("Старые данные удаляются, идёт загрузка с сервера. Сначала загрузится текущий и прошлый месяц, потом история.") });
      await reload();
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const runVerify = async () => {
    setBusy("verify");
    setMessage(null);
    try {
      setVerify(await call("integrations/linko/verify", { method: "POST" }, t));
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const sync = async () => {
    setBusy("sync");
    setMessage(null);
    try {
      await call("sales/sync", { method: "POST" }, t);
      setMessage({ tone: "ok", text: t("Синхронизация запущена — данные обновятся через минуту-две.") });
      await reload();
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const status = statusView[data.status];

  return (
    <>
      <div className="flex flex-wrap items-center gap-3">
        <span className={`rounded-full px-3 py-1 text-sm font-medium ${status.className}`}>{t(status.label)}</span>
        <span className="text-sm text-ink-3">
          {data.sync.dataAsOf ? t("загружено {date}", { date: dateTime(data.sync.dataAsOf) }) : t("данные ещё не загружались")}
          {data.updatedAt ? ` · ${t("настройки изменены {date}", { date: dateTime(data.updatedAt) })}` : ""}
        </span>
      </div>

      {message && (
        <div className={`mt-4 rounded-lg px-4 py-2.5 text-sm ${message.tone === "ok" ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>{message.text}</div>
      )}

      <Section title={t("Подключение")}>
        <div className="grid gap-5 lg:grid-cols-2">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Адрес сервера Linko")}</span>
            <input className={field} value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} placeholder={t("https://имя.linko.uz")} spellCheck={false} />
            <span className="mt-1 block text-xs text-ink-3">
              {data.baseUrlFromEnvironment
                ? t("Сейчас адрес взят из .env сервера. Сохраните, чтобы задать его в OneBase.")
                : t("Для проверки можно использовать демо-сервер https://sfademo.linko.uz")}
            </span>
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Токен Linko")}</span>
            <span className="relative block">
              <input
                className={`${field} pr-10`}
                type={showToken ? "text" : "password"}
                value={token}
                onChange={(e) => setToken(e.target.value)}
                placeholder={data.hasToken ? t("{hint} — оставьте пустым, чтобы не менять", { hint: data.tokenHint ?? "" }) : t("Вставьте токен от техподдержки Linko")}
                autoComplete="off"
                spellCheck={false}
              />
              <button
                type="button"
                onClick={() => setShowToken((v) => !v)}
                aria-label={showToken ? t("Скрыть токен") : t("Показать токен")}
                className="absolute right-2 top-1/2 grid size-7 -translate-y-1/2 place-items-center rounded-md text-ink-3 hover:text-ink"
              >
                {showToken ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
              </button>
            </span>
            <span className="mt-1 block text-xs text-ink-3">
              {data.hasToken
                ? t("Токен {hint} {source}. Сам токен не показывается.", { hint: data.tokenHint ?? "", source: t(sourceLabel[data.tokenSource]) })
                : t("Токен не задан — синхронизация не работает.")}
            </span>
          </label>
        </div>

        <label className="mt-5 block lg:max-w-[calc(50%-10px)]">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Токен планов (staff_balance)")}</span>
          <input
            className={field}
            type="password"
            value={planToken}
            onChange={(e) => setPlanToken(e.target.value)}
            placeholder={data.hasPlanToken ? t("{hint} — оставьте пустым, чтобы не менять", { hint: data.planTokenHint ?? "" }) : t("Токен API пересчёта планов Linko")}
            autoComplete="off"
            spellCheck={false}
          />
          <span className="mt-1 block text-xs text-ink-3">
            {data.hasPlanToken
              ? t("Токен {hint} {source}. Планы агентов загружаются автоматически после каждого пересчёта в Linko.", {
                  hint: data.planTokenHint ?? "",
                  source: t(sourceLabel[data.planTokenSource]),
                })
              : t("Без него планы не загружаются — выполнение плана будет «—».")}
          </span>
        </label>

        <label className="mt-5 flex items-center gap-2.5 text-sm text-ink">
          <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
          {t("Интеграция включена — данные загружаются автоматически каждые 30 минут")}
        </label>

        <div className="mt-5 flex flex-wrap items-center gap-2">
          <button className={button} onClick={runTest} disabled={busy !== null}>
            {busy === "test" ? <Loader2 className="size-4 animate-spin" /> : <PlugZap className="size-4" />}
            {t("Проверить подключение")}
          </button>
          <button className={primary} onClick={() => save(token.trim() || null, "save")} disabled={busy !== null || !dirty}>
            {busy === "save" && <Loader2 className="size-4 animate-spin" />}
            {t("Сохранить")}
          </button>
          {data.tokenSource === "OneBase" && (
            <button
              className={danger}
              disabled={busy !== null}
              onClick={() => confirm(t("Удалить токен, сохранённый в OneBase? Если в .env сервера есть LINKO_TOKEN, будет использоваться он.")) && save("", "clear")}
            >
              {t("Удалить токен")}
            </button>
          )}
        </div>

        {test && (
          <div className={`mt-4 flex items-start gap-3 rounded-lg px-4 py-3 text-sm ${test.ok ? "bg-ok-soft" : "bg-bad-soft"}`}>
            {test.ok ? <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-ok" /> : <XCircle className="mt-0.5 size-5 shrink-0 text-bad" />}
            <div>
              <div className={`font-semibold ${test.ok ? "text-ok" : "text-bad"}`}>{t(test.message)}</div>
              {test.ok && (
                <div className="mt-0.5 text-ink-2">
                  {t("Пользователей {users} · торговых точек {markets} · заказов {orders} · ответ за {ms} мс", {
                    users: num(test.users),
                    markets: num(test.markets),
                    orders: num(test.orders),
                    ms: num(test.elapsedMs),
                  })}
                </div>
              )}
              {!test.ok && <div className="mt-0.5 text-ink-2">{t("Настройки не изменены. Исправьте адрес или токен и проверьте ещё раз.")}</div>}
              {test.plansOk != null && (
                <div className={`mt-1 ${test.plansOk ? "text-ok" : "text-bad"}`}>
                  {t("Планы: {message}", { message: test.plansMessage ? t(test.plansMessage) : "" })}
                  {test.plansOk && test.plansLastDate && ` ${t("Последний пересчёт в Linko — {date}.", { date: dateTime(test.plansLastDate) })}`}
                </div>
              )}
            </div>
          </div>
        )}
        {!test && data.lastTest && (
          <p className="mt-4 text-xs text-ink-3">
            {t("Последняя проверка {date}: {result} — {message}", {
              date: dateTime(data.lastTest.at),
              result: data.lastTest.ok ? t("успешно") : t("ошибка"),
              message: data.lastTest.message ? t(data.lastTest.message) : "",
            })}
          </p>
        )}

        <Note>
          {t(
            "«Проверить подключение» проверяет то, что сейчас введено в полях (пустой токен — сохранённый), и ничего не сохраняет. Токен хранится на сервере зашифрованным и в браузер не передаётся.",
          )}
        </Note>
      </Section>

      <Section
        title={t("Синхронизация")}
        hint={data.sync.isRunning ? progressText(data.sync.progress, t) : undefined}
        actions={
          <>
            <button className={danger} onClick={reset} disabled={busy !== null || data.sync.isRunning || data.status === "not_configured" || !data.enabled}>
              {t("Очистить данные и загрузить заново")}
            </button>
            <button className={button} onClick={runVerify} disabled={busy !== null || data.status === "not_configured"}>
              {busy === "verify" && <Loader2 className="size-4 animate-spin" />}
              {t("Сверить с Linko")}
            </button>
            <Link href="/sales/setup" className={button}>
              {t("Оргструктура и планы")}
            </Link>
            <button className={primary} onClick={sync} disabled={busy !== null || data.sync.isRunning || data.status === "not_configured" || !data.enabled}>
              <RefreshCw className={`size-4 ${data.sync.isRunning || busy === "sync" ? "animate-spin" : ""}`} />
              {t("Синхронизировать сейчас")}
            </button>
          </>
        }
      >
        {data.sync.isRunning && (
          <div className="mb-3 flex flex-wrap items-center gap-3 rounded-lg border border-line px-3 py-3">
            <div className="min-w-0 flex-1">
              <SyncProgressBar progress={data.sync.progress} />
            </div>
            <CancelSyncButton size="sm" cancelling={data.sync.isCancelling} onCancelled={() => void reload()} />
          </div>
        )}
        {data.sync.entities.find((e) => e.entity === "source" && e.lastError) && (
          <div className="mb-3 rounded-lg bg-warn-soft px-3 py-2 text-sm text-ink">{t(data.sync.entities.find((e) => e.entity === "source")?.lastError ?? "")}</div>
        )}
        {verify && (
          <div className="mb-4 rounded-lg border border-line p-3">
            <div className={`mb-2 text-sm font-semibold ${verify.ok ? "text-ok" : "text-warn"}`}>
              {verify.ok
                ? t("Всё сходится: в OneBase столько же записей, сколько в Linko.")
                : t("Есть расхождения — если загрузка ещё идёт, это нормально; иначе нажмите «Синхронизировать сейчас».")}
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="py-1 pr-3 font-semibold">{t("Данные")}</th>
                  <th className="py-1 pr-3 text-right font-semibold">{t("В Linko")}</th>
                  <th className="py-1 pr-3 text-right font-semibold">{t("В OneBase")}</th>
                  <th className="py-1 font-semibold" />
                </tr>
              </thead>
              <tbody>
                {verify.rows.map((r) => (
                  <tr key={r.entity} className="border-t border-line">
                    <td className="py-1.5 pr-3">{t(linkoEntityLabels[r.entity] ?? r.entity)}</td>
                    <td className="py-1.5 pr-3 text-right tabular-nums">{num(r.linko)}</td>
                    <td className="py-1.5 pr-3 text-right tabular-nums">{num(r.oneBase)}</td>
                    <td className={`py-1.5 text-xs font-medium ${r.ok ? "text-ok" : "text-warn"}`}>{r.ok ? t("сходится") : t("расхождение")}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <p className="mt-2 text-xs text-ink-3">{t("Заказы, возвраты и визиты сверяются за окно истории {from} – {to}.", { from: verify.from, to: verify.to })}</p>
          </div>
        )}
        <div className="overflow-x-auto">
          <table className="w-full min-w-max text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">{t("Данные")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Последняя загрузка")}</th>
                <th className="py-2 pr-3 text-right font-semibold" title={t("Сколько записей пришло при последней загрузке: при обычном обновлении — только изменения")}>
                  {t("Строк за раз")}
                </th>
                <th className="py-2 font-semibold">{t("Ошибка")}</th>
              </tr>
            </thead>
            <tbody>
              {data.sync.entities.map((e) => (
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
        {data.sync.entities.length === 0 && <p className="py-4 text-center text-sm text-ink-3">{t("Синхронизаций ещё не было")}</p>}
      </Section>
    </>
  );
}
