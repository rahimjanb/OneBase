"use client";

import Link from "next/link";
import { useState } from "react";
import { CheckCircle2, Eye, EyeOff, Loader2, PlugZap, RefreshCw, XCircle } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { aiProviderStatus, type AiProviderTestResult, type AiProviderView } from "@/lib/ai";
import { bff } from "@/lib/bff";
import { dateTime, num } from "@/lib/sales/format";
import { button, danger, field, primary } from "./form";

const sourceLabel = { OneBase: "сохранён в OneBase", Environment: "взят из .env сервера", None: "" } as const;

const keyPlaceholder: Record<string, string> = {
  openai: "Ключ из platform.openai.com → API keys",
  anthropic: "Ключ из console.anthropic.com → API Keys",
};

function ProviderCard({ initial, onSaved }: { initial: AiProviderView; onSaved: (list: AiProviderView[]) => void }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [apiKey, setApiKey] = useState("");
  const [showKey, setShowKey] = useState(false);
  const [baseUrl, setBaseUrl] = useState(initial.baseUrl ?? "");
  const [enabled, setEnabled] = useState(initial.enabled);
  const [busy, setBusy] = useState<"test" | "save" | "clear" | "refresh" | null>(null);
  const [test, setTest] = useState<AiProviderTestResult | null>(null);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);

  const dirty = apiKey.trim() !== "" || baseUrl.trim() !== (data.baseUrl ?? "") || enabled !== data.enabled;

  const apply = (list: AiProviderView[]) => {
    const own = list.find((p) => p.code === data.code);
    if (own) setData(own);
    onSaved(list);
  };

  const run = async (kind: NonNullable<typeof busy>, action: () => Promise<void>) => {
    setBusy(kind);
    setMessage(null);
    try {
      await action();
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const save = (key: string | null, kind: "save" | "clear") =>
    run(kind, async () => {
      const list = await bff<AiProviderView[]>(`ai/providers/${data.code}`, {
        method: "PUT",
        body: JSON.stringify({ baseUrl: baseUrl.trim() || null, apiKey: key, enabled }),
      });
      apply(list);
      setApiKey("");
      setMessage({ tone: "ok", text: kind === "clear" ? t("Ключ удалён из OneBase.") : t("Настройки сохранены.") });
    });

  const runTest = () =>
    run("test", async () => {
      setTest(await bff<AiProviderTestResult>(`ai/providers/${data.code}/test`, {
        method: "POST",
        body: JSON.stringify({ apiKey: apiKey.trim() || null, baseUrl: baseUrl.trim() || null }),
      }));
      if (!apiKey.trim() && !baseUrl.trim()) apply(await bff<AiProviderView[]>("ai/providers"));
    });

  const refresh = () =>
    run("refresh", async () => {
      const result = await bff<{ count: number }>(`ai/providers/${data.code}/models/refresh`, { method: "POST" });
      apply(await bff<AiProviderView[]>("ai/providers"));
      setMessage({ tone: "ok", text: t("Список моделей обновлён: {n}. Отметьте нужные в разделе «Модели».", { n: num(result.count) }) });
    });

  const status = aiProviderStatus[data.status];

  return (
    <Section
      title={data.name}
      hint={data.supportsEmbeddings ? t("чат, инструменты, эмбеддинги") : t("чат и инструменты")}
      actions={<span className={`rounded-full px-3 py-1 text-xs font-medium ${status.className}`}>{t(status.label)}</span>}
    >
      {message && (
        <div className={`mb-4 rounded-lg px-4 py-2.5 text-sm ${message.tone === "ok" ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>{message.text}</div>
      )}

      <div className="grid gap-5 lg:grid-cols-2">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Ключ API")}</span>
          <span className="relative block">
            <input
              className={`${field} pr-10`}
              type={showKey ? "text" : "password"}
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
              placeholder={
                data.hasKey
                  ? t("{hint} — оставьте пустым, чтобы не менять", { hint: data.keyHint ?? "" })
                  : keyPlaceholder[data.code]
                    ? t(keyPlaceholder[data.code])
                    : t("Ключ API")
              }
              autoComplete="off"
              spellCheck={false}
            />
            <button
              type="button"
              onClick={() => setShowKey((v) => !v)}
              aria-label={showKey ? t("Скрыть ключ") : t("Показать ключ")}
              className="absolute right-2 top-1/2 grid size-7 -translate-y-1/2 place-items-center rounded-md text-ink-3 hover:text-ink"
            >
              {showKey ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
            </button>
          </span>
          <span className="mt-1 block text-xs text-ink-3">
            {data.hasKey
              ? t("Ключ {hint} {source}. Сам ключ не показывается.", { hint: data.keyHint ?? "", source: sourceLabel[data.keySource] ? t(sourceLabel[data.keySource]) : "" })
              : t("Ключ не задан — модели этого провайдера недоступны.")}
          </span>
        </label>

        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Адрес API")}</span>
          <input className={field} value={baseUrl} onChange={(e) => setBaseUrl(e.target.value)} placeholder={data.defaultBaseUrl} spellCheck={false} />
          <span className="mt-1 block text-xs text-ink-3">{t("Пусто — {url}. Свой адрес — для прокси или совместимого сервера.", { url: data.defaultBaseUrl })}</span>
        </label>
      </div>

      <label className="mt-5 flex items-center gap-2.5 text-sm text-ink">
        <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
        {t("Провайдер включён")}
      </label>

      <div className="mt-5 flex flex-wrap items-center gap-2">
        <button className={button} onClick={runTest} disabled={busy !== null}>
          {busy === "test" ? <Loader2 className="size-4 animate-spin" /> : <PlugZap className="size-4" />}
          {t("Проверить ключ")}
        </button>
        <button className={primary} onClick={() => save(apiKey.trim() || null, "save")} disabled={busy !== null || !dirty}>
          {busy === "save" && <Loader2 className="size-4 animate-spin" />}
          {t("Сохранить")}
        </button>
        <button className={button} onClick={refresh} disabled={busy !== null || !data.hasKey}>
          <RefreshCw className={`size-4 ${busy === "refresh" ? "animate-spin" : ""}`} />
          {t("Обновить список моделей")}
        </button>
        {data.keySource === "OneBase" && (
          <button
            className={danger}
            disabled={busy !== null}
            onClick={() =>
              confirm(t("Удалить ключ {name}, сохранённый в OneBase? Если ключ есть в .env сервера, будет использоваться он.", { name: data.name })) && save("", "clear")
            }
          >
            {t("Удалить ключ")}
          </button>
        )}
      </div>

      {test && (
        <div className={`mt-4 flex items-start gap-3 rounded-lg px-4 py-3 text-sm ${test.ok ? "bg-ok-soft" : "bg-bad-soft"}`}>
          {test.ok ? <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-ok" /> : <XCircle className="mt-0.5 size-5 shrink-0 text-bad" />}
          <div>
            <div className={`font-semibold ${test.ok ? "text-ok" : "text-bad"}`}>{t(test.message)}</div>
            <div className="mt-0.5 text-ink-2">{test.ok ? t("Ответ за {ms} мс. Ничего не сохранено.", { ms: num(test.elapsedMs) }) : t("Настройки не изменены.")}</div>
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

      <p className="mt-4 text-sm text-ink-2">
        {t("Моделей у провайдера: {available}, разрешено в OneBase: {enabled}", { available: num(data.modelsAvailable), enabled: num(data.modelsEnabled) })}
        {data.modelsRefreshedAt ? ` · ${t("список обновлён {date}", { date: dateTime(data.modelsRefreshedAt) })}` : ` · ${t("список ещё не загружался")}`}.{" "}
        <Link href="/settings/ai/models" className="font-medium text-accent-strong hover:underline">
          {t("Модели →")}
        </Link>
      </p>
    </Section>
  );
}

export function ProvidersSettings({ initial }: { initial: AiProviderView[] }) {
  const t = useT();
  const [, setProviders] = useState(initial);
  return (
    <>
      {initial.map((p) => (
        <ProviderCard key={p.code} initial={p} onSaved={setProviders} />
      ))}
      <Note>
        {t(
          "«Проверить ключ» запрашивает у провайдера список моделей — генерации нет, токены не тратятся. Ключ хранится на сервере зашифрованным, в браузер не передаётся и в журнал не пишется. Все запросы к OpenAI и Anthropic идут только с сервера OneBase.",
        )}
      </Note>
    </>
  );
}
