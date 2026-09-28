"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { CheckCircle2, Eye, EyeOff, Loader2, PlugZap, RefreshCw, XCircle } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { linkoEntityLabels, progressText, statusView, type LinkoDetails, type LinkoTestResult } from "@/lib/integrations";
import { dateTime, num } from "@/lib/sales/format";

const field =
  "h-10 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";
const button =
  "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";
const danger =
  "inline-flex h-9 items-center gap-2 rounded-lg border border-bad/30 bg-surface px-4 text-sm font-medium text-bad hover:bg-bad-soft disabled:opacity-50";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-4 text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-50";

async function call<T>(path: string, init?: RequestInit): Promise<T> {
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
    throw new Error(response.status === 403 ? "Недостаточно прав для изменения интеграций." : message || `Ошибка ${response.status}`);
  }
  return (response.status === 202 || response.status === 204 ? undefined : await response.json()) as T;
}

const sourceLabel = { OneBase: "сохранён в OneBase", Environment: "взят из .env сервера", None: "" } as const;

export function LinkoSettings({ initial }: { initial: LinkoDetails }) {
  const [data, setData] = useState(initial);
  const [baseUrl, setBaseUrl] = useState(initial.baseUrl);
  const [token, setToken] = useState("");
  const [showToken, setShowToken] = useState(false);
  const [enabled, setEnabled] = useState(initial.enabled);
  const [busy, setBusy] = useState<"test" | "save" | "sync" | "clear" | "reset" | "verify" | null>(null);
  const [verify, setVerify] = useState<{ from: string; to: string; ok: boolean; rows: { entity: string; linko: number; oneBase: number; ok: boolean }[] } | null>(null);
  const [test, setTest] = useState<LinkoTestResult | null>(null);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);

  const dirty = baseUrl.trim() !== data.baseUrl || token.trim() !== "" || enabled !== data.enabled;

  const reload = async () => setData(await call<LinkoDetails>("integrations/linko"));

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
      setTest(await call<LinkoTestResult>("integrations/linko/test", { method: "POST", body: JSON.stringify({ baseUrl: baseUrl.trim(), token: token.trim() || null }) }));
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
      const saved = await call<LinkoDetails>("integrations/linko", {
        method: "PUT",
        body: JSON.stringify({ baseUrl: baseUrl.trim(), token: tokenValue, enabled }),
      });
      setData(saved);
      setBaseUrl(saved.baseUrl);
      setToken("");
      setMessage({ tone: "ok", text: kind === "clear" ? "Токен удалён из OneBase." : "Настройки сохранены." });
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const reset = async () => {
    const ok = confirm(
      `Удалить все данные, загруженные из Linko, и загрузить их заново с ${data.baseUrl}?\n\n` +
        "Будут удалены: заказы, возвраты, визиты, торговые точки, агенты, товары, а также регионы, профили агентов и планы в OneBase " +
        "(они привязаны к данным Linko).\nСохранятся: направления, цели и настройки подключения.",
    );
    if (!ok) return;
    setBusy("reset");
    setMessage(null);
    try {
      await call("integrations/linko/reset", { method: "POST" });
      setMessage({ tone: "ok", text: "Старые данные удаляются, идёт загрузка с сервера. Сначала загрузится текущий и прошлый месяц, потом история." });
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
      setVerify(await call("integrations/linko/verify", { method: "POST" }));
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
      await call("sales/sync", { method: "POST" });
      setMessage({ tone: "ok", text: "Синхронизация запущена — данные обновятся через минуту-две." });
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
        <span className={`rounded-full px-3 py-1 text-sm font-medium ${status.className}`}>{status.label}</span>
        <span className="text-sm text-ink-3">
          {data.sync.dataAsOf ? `данные по ${dateTime(data.sync.dataAsOf)}` : "данные ещё не загружались"}
          {data.updatedAt ? ` · настройки изменены ${dateTime(data.updatedAt)}` : ""}
        </span>
      </div>

      {message && (
        <div className={`mt-4 rounded-lg px-4 py-2.5 text-sm ${message.tone === "ok" ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>{message.text}</div>
      )}

      <Section title="Подключение">
        <div className="grid gap-5 lg:grid-cols-2">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Адрес сервера Linko</span>
            <input className={field} value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} placeholder="https://имя.linko.uz" spellCheck={false} />
            <span className="mt-1 block text-xs text-ink-3">
              {data.baseUrlFromEnvironment ? "Сейчас адрес взят из .env сервера. Сохраните, чтобы задать его в OneBase." : "Для проверки можно использовать демо-сервер https://sfademo.linko.uz"}
            </span>
          </label>

          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Токен Linko</span>
            <span className="relative block">
              <input
                className={`${field} pr-10`}
                type={showToken ? "text" : "password"}
                value={token}
                onChange={(e) => setToken(e.target.value)}
                placeholder={data.hasToken ? `${data.tokenHint} — оставьте пустым, чтобы не менять` : "Вставьте токен от техподдержки Linko"}
                autoComplete="off"
                spellCheck={false}
              />
              <button
                type="button"
                onClick={() => setShowToken((v) => !v)}
                aria-label={showToken ? "Скрыть токен" : "Показать токен"}
                className="absolute right-2 top-1/2 grid size-7 -translate-y-1/2 place-items-center rounded-md text-ink-3 hover:text-ink"
              >
                {showToken ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
              </button>
            </span>
            <span className="mt-1 block text-xs text-ink-3">
              {data.hasToken ? `Токен ${data.tokenHint} ${sourceLabel[data.tokenSource]}. Сам токен не показывается.` : "Токен не задан — синхронизация не работает."}
            </span>
          </label>
        </div>

        <label className="mt-5 flex items-center gap-2.5 text-sm text-ink">
          <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
          Интеграция включена — данные загружаются автоматически каждые 20 минут
        </label>

        <div className="mt-5 flex flex-wrap items-center gap-2">
          <button className={button} onClick={runTest} disabled={busy !== null}>
            {busy === "test" ? <Loader2 className="size-4 animate-spin" /> : <PlugZap className="size-4" />}
            Проверить подключение
          </button>
          <button className={primary} onClick={() => save(token.trim() || null, "save")} disabled={busy !== null || !dirty}>
            {busy === "save" && <Loader2 className="size-4 animate-spin" />}
            Сохранить
          </button>
          {data.tokenSource === "OneBase" && (
            <button
              className={danger}
              disabled={busy !== null}
              onClick={() => confirm("Удалить токен, сохранённый в OneBase? Если в .env сервера есть LINKO_TOKEN, будет использоваться он.") && save("", "clear")}
            >
              Удалить токен
            </button>
          )}
        </div>

        {test && (
          <div className={`mt-4 flex items-start gap-3 rounded-lg px-4 py-3 text-sm ${test.ok ? "bg-ok-soft" : "bg-bad-soft"}`}>
            {test.ok ? <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-ok" /> : <XCircle className="mt-0.5 size-5 shrink-0 text-bad" />}
            <div>
              <div className={`font-semibold ${test.ok ? "text-ok" : "text-bad"}`}>{test.message}</div>
              {test.ok && (
                <div className="mt-0.5 text-ink-2">
                  Пользователей {num(test.users)} · торговых точек {num(test.markets)} · заказов {num(test.orders)} · ответ за {num(test.elapsedMs)} мс
                </div>
              )}
              {!test.ok && <div className="mt-0.5 text-ink-2">Настройки не изменены. Исправьте адрес или токен и проверьте ещё раз.</div>}
            </div>
          </div>
        )}
        {!test && data.lastTest && (
          <p className="mt-4 text-xs text-ink-3">
            Последняя проверка {dateTime(data.lastTest.at)}: {data.lastTest.ok ? "успешно" : "ошибка"} — {data.lastTest.message}
          </p>
        )}

        <Note>
          «Проверить подключение» проверяет то, что сейчас введено в полях (пустой токен — сохранённый), и ничего не сохраняет. Токен хранится на сервере
          зашифрованным и в браузер не передаётся.
        </Note>
      </Section>

      <Section
        title="Синхронизация"
        hint={data.sync.isRunning ? progressText(data.sync.progress) : undefined}
        actions={
          <>
            <button className={danger} onClick={reset} disabled={busy !== null || data.sync.isRunning || data.status === "not_configured" || !data.enabled}>
              Очистить данные и загрузить заново
            </button>
            <button className={button} onClick={runVerify} disabled={busy !== null || data.status === "not_configured"}>
              {busy === "verify" && <Loader2 className="size-4 animate-spin" />}
              Сверить с Linko
            </button>
            <Link href="/sales/setup" className={button}>
              Оргструктура и планы
            </Link>
            <button className={primary} onClick={sync} disabled={busy !== null || data.sync.isRunning || data.status === "not_configured" || !data.enabled}>
              <RefreshCw className={`size-4 ${data.sync.isRunning || busy === "sync" ? "animate-spin" : ""}`} />
              Синхронизировать сейчас
            </button>
          </>
        }
      >
        {data.sync.isRunning && (
          <div className="mb-3 flex items-center gap-2 rounded-lg bg-accent-soft px-3 py-2 text-sm text-accent-strong">
            <Loader2 className="size-4 animate-spin" />
            Загрузка: {progressText(data.sync.progress)}
          </div>
        )}
        {data.sync.entities.find((e) => e.entity === "source" && e.lastError) && (
          <div className="mb-3 rounded-lg bg-warn-soft px-3 py-2 text-sm text-ink">{data.sync.entities.find((e) => e.entity === "source")?.lastError}</div>
        )}
        {verify && (
          <div className="mb-4 rounded-lg border border-line p-3">
            <div className={`mb-2 text-sm font-semibold ${verify.ok ? "text-ok" : "text-warn"}`}>
              {verify.ok ? "Всё сходится: в OneBase столько же записей, сколько в Linko." : "Есть расхождения — если загрузка ещё идёт, это нормально; иначе нажмите «Синхронизировать сейчас»."}
            </div>
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="py-1 pr-3 font-semibold">Данные</th>
                  <th className="py-1 pr-3 text-right font-semibold">В Linko</th>
                  <th className="py-1 pr-3 text-right font-semibold">В OneBase</th>
                  <th className="py-1 font-semibold" />
                </tr>
              </thead>
              <tbody>
                {verify.rows.map((r) => (
                  <tr key={r.entity} className="border-t border-line">
                    <td className="py-1.5 pr-3">{linkoEntityLabels[r.entity] ?? r.entity}</td>
                    <td className="py-1.5 pr-3 text-right tabular-nums">{num(r.linko)}</td>
                    <td className="py-1.5 pr-3 text-right tabular-nums">{num(r.oneBase)}</td>
                    <td className={`py-1.5 text-xs font-medium ${r.ok ? "text-ok" : "text-warn"}`}>{r.ok ? "сходится" : "расхождение"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <p className="mt-2 text-xs text-ink-3">Заказы, возвраты и визиты сверяются за окно истории {verify.from} – {verify.to}.</p>
          </div>
        )}
        <div className="overflow-x-auto">
          <table className="w-full min-w-max text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">Данные</th>
                <th className="py-2 pr-3 font-semibold">Последняя загрузка</th>
                <th className="py-2 pr-3 text-right font-semibold" title="Сколько записей пришло при последней загрузке: при обычном обновлении — только изменения">Строк за раз</th>
                <th className="py-2 font-semibold">Ошибка</th>
              </tr>
            </thead>
            <tbody>
              {data.sync.entities.map((e) => (
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
        {data.sync.entities.length === 0 && <p className="py-4 text-center text-sm text-ink-3">Синхронизаций ещё не было</p>}
      </Section>
    </>
  );
}
