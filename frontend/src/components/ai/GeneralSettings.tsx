"use client";

import Link from "next/link";
import { useState } from "react";
import { CheckCircle2, Loader2, XCircle } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { modelLabel, type AiModelRef, type AiModelTestResult, type AiModelView, type AiProviderView, type AiSettingsView } from "@/lib/ai";
import { bff } from "@/lib/bff";
import { dateTime, num } from "@/lib/sales/format";
import { button, field, primary } from "./form";

type Slot = "primary" | "fallback" | "router" | "embedding";

const slots: { key: Slot; label: string; hint: string; embeddings?: boolean; test?: boolean }[] = [
  { key: "primary", label: "Основная модель", hint: "Главный консультант и AI-сотрудники, у которых своя модель не задана.", test: true },
  { key: "fallback", label: "Резервная модель", hint: "Отвечает, если основная вернула ошибку или не ответила. Лучше — другого провайдера.", test: true },
  { key: "router", label: "Модель маршрутизации", hint: "Быстрая модель: решает, каких AI-сотрудников привлечь к вопросу. Пусто — основная." },
  { key: "embedding", label: "Модель эмбеддингов", hint: "Семантический поиск по базе знаний. Эмбеддинги строит только OpenAI.", embeddings: true },
];

const encode = (r: AiModelRef | null) => (r ? `${r.provider}|${r.model}` : "");
const decode = (v: string): AiModelRef | null => {
  const [provider, ...rest] = v.split("|");
  return provider && rest.length ? { provider, model: rest.join("|") } : null;
};

export function GeneralSettings({ initial, models, providers }: { initial: AiSettingsView; models: AiModelView[]; providers: AiProviderView[] }) {
  const [data, setData] = useState(initial);
  const [enabled, setEnabled] = useState(initial.enabled);
  const [values, setValues] = useState<Record<Slot, string>>({
    primary: initial.primaryFromEnvironment ? "" : encode(initial.primary),
    fallback: encode(initial.fallback),
    router: encode(initial.router),
    embedding: encode(initial.embedding),
  });
  const [temperature, setTemperature] = useState(initial.temperature?.toString() ?? "");
  const [maxTokens, setMaxTokens] = useState(initial.maxOutputTokens.toString());
  const [busy, setBusy] = useState<string | null>(null);
  const [message, setMessage] = useState<{ tone: "ok" | "bad"; text: string } | null>(null);
  const [tests, setTests] = useState<Partial<Record<Slot, AiModelTestResult>>>({});

  const providerName = (code: string) => providers.find((p) => p.code === code)?.name ?? code;
  const options = (embeddings?: boolean) =>
    providers
      .filter((p) => !embeddings || p.supportsEmbeddings)
      .map((p) => ({ provider: p, models: models.filter((m) => m.provider === p.code && m.enabled) }))
      .filter((g) => g.models.length > 0);

  const save = async () => {
    const t = temperature.trim().replace(",", ".");
    const body = {
      enabled,
      primary: decode(values.primary),
      fallback: decode(values.fallback),
      router: decode(values.router),
      embedding: decode(values.embedding),
      temperature: t === "" ? null : Number(t),
      maxOutputTokens: Number(maxTokens),
    };
    if (body.temperature !== null && !Number.isFinite(body.temperature)) {
      setMessage({ tone: "bad", text: "Температура — число от 0 до 1." });
      return;
    }
    setBusy("save");
    setMessage(null);
    try {
      setData(await bff<AiSettingsView>("ai/settings", { method: "PUT", body: JSON.stringify(body) }));
      setMessage({ tone: "ok", text: "Настройки сохранены." });
    } catch (e) {
      setMessage({ tone: "bad", text: (e as Error).message });
    } finally {
      setBusy(null);
    }
  };

  const test = async (slot: Slot) => {
    const ref = decode(values[slot]);
    if (!ref) return;
    setBusy(`test-${slot}`);
    try {
      const result = await bff<AiModelTestResult>("ai/settings/test", { method: "POST", body: JSON.stringify(ref) });
      setTests((prev) => ({ ...prev, [slot]: result }));
    } catch (e) {
      setTests((prev) => ({ ...prev, [slot]: { ok: false, message: (e as Error).message, reply: null, usage: null, elapsedMs: 0, usedFallback: false, model: null } }));
    } finally {
      setBusy(null);
    }
  };

  const noModels = models.every((m) => !m.enabled);

  return (
    <>
      <div className={`mb-5 flex items-start gap-3 rounded-lg px-4 py-3 text-sm ${data.ready ? "bg-ok-soft" : "bg-warn-soft"}`}>
        {data.ready ? <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-ok" /> : <XCircle className="mt-0.5 size-5 shrink-0 text-warn" />}
        <div>
          <div className={`font-semibold ${data.ready ? "text-ok" : "text-warn"}`}>
            {data.ready ? "AI готов к работе" : "AI пока не работает"}
          </div>
          <div className="mt-0.5 text-ink-2">
            {data.ready
              ? `Основная модель: ${data.primary ? `${providerName(data.primary.provider)} · ${data.primary.model}` : "—"}${data.primaryFromEnvironment ? " (из .env сервера)" : ""}.`
              : data.readinessMessage}
            {data.updatedAt && ` Настройки изменены ${dateTime(data.updatedAt)}.`}
          </div>
        </div>
      </div>

      {message && (
        <div className={`mb-4 rounded-lg px-4 py-2.5 text-sm ${message.tone === "ok" ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>{message.text}</div>
      )}

      <Section title="Модели для задач">
        {noModels && (
          <p className="mb-4 rounded-lg bg-muted px-4 py-3 text-sm text-ink-2">
            Нет разрешённых моделей. Добавьте ключ в{" "}
            <Link href="/settings/ai/providers" className="font-medium text-accent-strong hover:underline">
              Провайдерах
            </Link>
            , загрузите список и отметьте модели в разделе{" "}
            <Link href="/settings/ai/models" className="font-medium text-accent-strong hover:underline">
              Модели
            </Link>
            .
          </p>
        )}
        <div className="grid gap-5 lg:grid-cols-2">
          {slots.map((slot) => {
            const groups = options(slot.embeddings);
            const current = decode(values[slot.key]);
            const known = !current || groups.some((g) => g.models.some((m) => m.provider === current.provider && m.model === current.model));
            const result = tests[slot.key];
            return (
              <div key={slot.key}>
                <label className="block">
                  <span className="mb-1.5 block text-sm font-medium text-ink">{slot.label}</span>
                  <select
                    className={field}
                    value={values[slot.key]}
                    onChange={(e) => setValues((prev) => ({ ...prev, [slot.key]: e.target.value }))}
                  >
                    <option value="">
                      {slot.key === "primary" && data.primaryFromEnvironment && data.primary ? `Из .env: ${data.primary.model}` : "Не выбрана"}
                    </option>
                    {!known && current && <option value={values[slot.key]}>{`${providerName(current.provider)} · ${current.model} (не разрешена)`}</option>}
                    {groups.map((g) => (
                      <optgroup key={g.provider.code} label={g.provider.name}>
                        {g.models.map((m) => (
                          <option key={m.id} value={`${m.provider}|${m.model}`}>
                            {modelLabel(m)}
                          </option>
                        ))}
                      </optgroup>
                    ))}
                  </select>
                  <span className="mt-1 block text-xs text-ink-3">{slot.hint}</span>
                </label>
                {slot.test && current && (
                  <div className="mt-2 flex flex-wrap items-center gap-2">
                    <button className={button} onClick={() => test(slot.key)} disabled={busy !== null}>
                      {busy === `test-${slot.key}` && <Loader2 className="size-4 animate-spin" />}
                      Проверить модель
                    </button>
                    {result && (
                      <span className={`text-sm ${result.ok ? "text-ok" : "text-bad"}`}>
                        {result.message}
                        {result.ok && result.usage && ` ${num(result.elapsedMs)} мс, токенов ${num(result.usage.inputTokens + result.usage.outputTokens)}.`}
                      </span>
                    )}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </Section>

      <Section title="Параметры ответа">
        <div className="grid gap-5 lg:grid-cols-2">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Температура</span>
            <input className={field} value={temperature} onChange={(e) => setTemperature(e.target.value)} placeholder="по умолчанию провайдера" inputMode="decimal" />
            <span className="mt-1 block text-xs text-ink-3">0 — точные, повторяемые ответы; 1 — разнообразнее. Для аналитики подходит 0–0,3. Пусто — не передаётся.</span>
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">Максимум токенов ответа</span>
            <input className={field} value={maxTokens} onChange={(e) => setMaxTokens(e.target.value)} inputMode="numeric" />
            <span className="mt-1 block text-xs text-ink-3">Ограничивает длину и стоимость одного ответа модели (256–64 000).</span>
          </label>
        </div>
        <label className="mt-5 flex items-center gap-2.5 text-sm text-ink">
          <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
          AI включён для пользователей
        </label>
        <div className="mt-5">
          <button className={primary} onClick={save} disabled={busy !== null}>
            {busy === "save" && <Loader2 className="size-4 animate-spin" />}
            Сохранить
          </button>
        </div>
      </Section>

      <Note>
        «Проверить модель» отправляет модели короткий запрос — провайдер берёт оплату за несколько десятков токенов. Резервная модель включается автоматически
        при ошибке основной: сбой сервера или сети (после одного повтора), лимит запросов, недоступный ключ.
      </Note>
    </>
  );
}
