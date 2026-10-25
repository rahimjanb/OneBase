"use client";

import Link from "next/link";
import { useState } from "react";
import { CheckCircle2, Loader2, XCircle } from "lucide-react";
import { Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { modelLabel, type AiModelRef, type AiModelTestResult, type AiModelView, type AiProviderView, type AiSettingsView } from "@/lib/ai";
import { bff } from "@/lib/bff";
import { dateTime, num } from "@/lib/sales/format";
import { button, field, primary } from "./form";

type Slot = "primary" | "fallback" | "router" | "embedding" | "extract";
type SlotDef = { key: Slot; label: string; hint: string; embeddings?: boolean; test?: boolean };

const slots: SlotDef[] = [
  { key: "primary", label: "Основная модель", hint: "Главный консультант и AI-сотрудники, у которых своя модель не задана.", test: true },
  { key: "fallback", label: "Резервная модель", hint: "Отвечает, если основная вернула ошибку или не ответила. Лучше — другого провайдера.", test: true },
  { key: "embedding", label: "Модель эмбеддингов", hint: "Семантический поиск по базе знаний. Эмбеддинги строит только OpenAI.", embeddings: true },
];

/** Категории простых задач: своя модель (обычно Haiku 5.5) и режим. Цикл AI-сотрудника, итоговый ответ и Data Manager — всегда основной. */
const taskSlots: (SlotDef & { mode: "routerMode" | "extractMode" })[] = [
  { key: "router", mode: "routerMode", label: "Модель маршрутизации", hint: "Выбор AI-сотрудников и задач им по вопросу (классификация по отделам). Пусто — основная.", test: true },
  { key: "extract", mode: "extractMode", label: "Модель извлечения JSON", hint: "Подбор колонок Google Sheets к полям OneBase (ответ — JSON). Пусто — основная.", test: true },
];

const modes: { value: string; label: string }[] = [
  { value: "off", label: "Выкл — основная модель" },
  { value: "shadow", label: "Сравнение — ответ основной, модель задачи в фоне" },
  { value: "on", label: "Включено — модель задачи, при сбое основная" },
];

const encode = (r: AiModelRef | null) => (r ? `${r.provider}|${r.model}` : "");
const decode = (v: string): AiModelRef | null => {
  const [provider, ...rest] = v.split("|");
  return provider && rest.length ? { provider, model: rest.join("|") } : null;
};

export function GeneralSettings({ initial, models, providers }: { initial: AiSettingsView; models: AiModelView[]; providers: AiProviderView[] }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [enabled, setEnabled] = useState(initial.enabled);
  const [values, setValues] = useState<Record<Slot, string>>({
    primary: initial.primaryFromEnvironment ? "" : encode(initial.primary),
    fallback: encode(initial.fallback),
    router: encode(initial.router),
    embedding: encode(initial.embedding),
    extract: encode(initial.extract),
  });
  const [routingEnabled, setRoutingEnabled] = useState(initial.taskRoutingEnabled);
  const [taskModes, setTaskModes] = useState({ routerMode: initial.routerMode, extractMode: initial.extractMode });
  const [temperature, setTemperature] = useState(initial.temperature?.toString() ?? "");
  const [maxTokens, setMaxTokens] = useState(initial.maxOutputTokens.toString());
  const [reasoning, setReasoning] = useState(initial.reasoningEffort ?? "");
  const [dailyBudget, setDailyBudget] = useState(initial.dailyBudgetUsd?.toString() ?? "");
  const [monthlyBudget, setMonthlyBudget] = useState(initial.monthlyBudgetUsd?.toString() ?? "");
  const [warnPercent, setWarnPercent] = useState(initial.budgetWarnPercent.toString());
  const [concurrent, setConcurrent] = useState(initial.maxConcurrentCalls.toString());
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
    const temp = temperature.trim().replace(",", ".");
    const money = (v: string) => (v.trim() === "" ? null : Number(v.trim().replace(",", ".")));
    const body = {
      enabled,
      primary: decode(values.primary),
      fallback: decode(values.fallback),
      router: decode(values.router),
      embedding: decode(values.embedding),
      extract: decode(values.extract),
      taskRoutingEnabled: routingEnabled,
      routerMode: taskModes.routerMode,
      extractMode: taskModes.extractMode,
      temperature: temp === "" ? null : Number(temp),
      maxOutputTokens: Number(maxTokens),
      reasoningEffort: reasoning === "" ? null : reasoning,
      dailyBudgetUsd: money(dailyBudget),
      monthlyBudgetUsd: money(monthlyBudget),
      budgetWarnPercent: Number(warnPercent),
      maxConcurrentCalls: Number(concurrent),
    };
    if (body.temperature !== null && !Number.isFinite(body.temperature)) {
      setMessage({ tone: "bad", text: t("Температура — число от 0 до 1.") });
      return;
    }
    if ([body.dailyBudgetUsd, body.monthlyBudgetUsd].some((v) => v !== null && !(Number.isFinite(v) && v > 0))) {
      setMessage({ tone: "bad", text: t("Бюджет — положительная сумма в долларах; пусто — без ограничения.") });
      return;
    }
    if (!Number.isInteger(body.budgetWarnPercent) || !Number.isInteger(body.maxConcurrentCalls)) {
      setMessage({ tone: "bad", text: t("Порог и число вызовов — целые числа.") });
      return;
    }
    setBusy("save");
    setMessage(null);
    try {
      setData(await bff<AiSettingsView>("ai/settings", { method: "PUT", body: JSON.stringify(body) }));
      setMessage({ tone: "ok", text: t("Настройки сохранены.") });
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

  const slotField = (slot: SlotDef) => {
    const groups = options(slot.embeddings);
    const current = decode(values[slot.key]);
    const known = !current || groups.some((g) => g.models.some((m) => m.provider === current.provider && m.model === current.model));
    const result = tests[slot.key];
    return (
      <div key={slot.key}>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t(slot.label)}</span>
          <select
            className={field}
            value={values[slot.key]}
            onChange={(e) => setValues((prev) => ({ ...prev, [slot.key]: e.target.value }))}
          >
            <option value="">
              {slot.key === "primary" && data.primaryFromEnvironment && data.primary ? t("Из .env: {model}", { model: data.primary.model }) : t("Не выбрана")}
            </option>
            {!known && current && <option value={values[slot.key]}>{`${providerName(current.provider)} · ${current.model} ${t("(не разрешена)")}`}</option>}
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
          <span className="mt-1 block text-xs text-ink-3">{t(slot.hint)}</span>
        </label>
        {slot.test && current && (
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <button className={button} onClick={() => test(slot.key)} disabled={busy !== null}>
              {busy === `test-${slot.key}` && <Loader2 className="size-4 animate-spin" />}
              {t("Проверить модель")}
            </button>
            {result && (
              <span className={`text-sm ${result.ok ? "text-ok" : "text-bad"}`}>
                {t(result.message)}
                {result.ok &&
                  result.usage &&
                  ` ${t("{ms} мс, токенов {tokens}.", { ms: num(result.elapsedMs), tokens: num(result.usage.inputTokens + result.usage.outputTokens) })}`}
              </span>
            )}
          </div>
        )}
      </div>
    );
  };

  return (
    <>
      <div className={`mb-5 flex items-start gap-3 rounded-lg px-4 py-3 text-sm ${data.ready ? "bg-ok-soft" : "bg-warn-soft"}`}>
        {data.ready ? <CheckCircle2 className="mt-0.5 size-5 shrink-0 text-ok" /> : <XCircle className="mt-0.5 size-5 shrink-0 text-warn" />}
        <div>
          <div className={`font-semibold ${data.ready ? "text-ok" : "text-warn"}`}>
            {data.ready ? t("AI готов к работе") : t("AI пока не работает")}
          </div>
          <div className="mt-0.5 text-ink-2">
            {data.ready
              ? `${t("Основная модель: {model}", { model: data.primary ? `${providerName(data.primary.provider)} · ${data.primary.model}` : "—" })}${data.primaryFromEnvironment ? ` ${t("(из .env сервера)")}` : ""}.`
              : data.readinessMessage && t(data.readinessMessage)}
            {data.updatedAt && ` ${t("Настройки изменены {date}.", { date: dateTime(data.updatedAt) })}`}
          </div>
        </div>
      </div>

      {message && (
        <div className={`mb-4 rounded-lg px-4 py-2.5 text-sm ${message.tone === "ok" ? "bg-ok-soft text-ok" : "bg-bad-soft text-bad"}`}>{message.text}</div>
      )}

      <Section title={t("Модели для задач")}>
        {noModels && (
          <p className="mb-4 rounded-lg bg-muted px-4 py-3 text-sm text-ink-2">
            {t("Нет разрешённых моделей. Добавьте ключ в")}{" "}
            <Link href="/settings/ai/providers" className="font-medium text-accent-strong hover:underline">
              {t("Провайдерах")}
            </Link>
            {t(", загрузите список и отметьте модели в разделе")}{" "}
            <Link href="/settings/ai/models" className="font-medium text-accent-strong hover:underline">
              {t("Модели")}
            </Link>
            .
          </p>
        )}
        <div className="grid gap-5 lg:grid-cols-2">
          {slots.map(slotField)}
        </div>
      </Section>

      <Section title={t("Простые задачи — отдельная модель")}>
        <label className="mb-4 flex items-start gap-2.5 text-sm text-ink">
          <input type="checkbox" checked={routingEnabled} onChange={(e) => setRoutingEnabled(e.target.checked)} className="mt-0.5 size-4 accent-[var(--color-accent)]" />
          <span>
            <span className="font-medium">{t("Отдельные модели для простых задач")}</span>
            <span className="mt-0.5 block text-xs text-ink-3">
              {t("Выключено — все задачи идут основной моделью, сравнение не запускается: так одним переключателем возвращается прежнее поведение.")}
            </span>
          </span>
        </label>
        <div className={`grid gap-5 lg:grid-cols-2 ${routingEnabled ? "" : "opacity-60"}`}>
          {taskSlots.map((slot) => (
            <div key={slot.key} className="space-y-3">
              {slotField(slot)}
              <label className="block">
                <span className="mb-1.5 block text-sm font-medium text-ink">{t("Режим")}</span>
                <select className={field} value={taskModes[slot.mode]} onChange={(e) => setTaskModes((prev) => ({ ...prev, [slot.mode]: e.target.value }))}>
                  {modes.map((m) => (
                    <option key={m.value} value={m.value}>
                      {t(m.label)}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          ))}
        </div>
        <p className="mt-4 text-xs text-ink-3">
          {t(
            "Цикл AI-сотрудника с инструментами, итоговый ответ консультанта и AI Data Manager всегда идут основной моделью. Telegram-агент — своей моделью из настроек аккаунта. Сначала включите «Сравнение» и посмотрите итоги в",
          )}{" "}
          <Link href="/settings/ai/routing" className="font-medium text-accent-strong hover:underline">
            {t("Модели по задачам")}
          </Link>
          .
        </p>
      </Section>

      <Section title={t("Параметры ответа")}>
        <div className="grid gap-5 lg:grid-cols-2">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Температура")}</span>
            <input className={field} value={temperature} onChange={(e) => setTemperature(e.target.value)} placeholder={t("по умолчанию провайдера")} inputMode="decimal" />
            <span className="mt-1 block text-xs text-ink-3">{t("0 — точные, повторяемые ответы; 1 — разнообразнее. Для аналитики подходит 0–0,3. Пусто — не передаётся. Модели Claude 5 температуру не принимают — им она не отправляется.")}</span>
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Максимум токенов ответа")}</span>
            <input className={field} value={maxTokens} onChange={(e) => setMaxTokens(e.target.value)} inputMode="numeric" />
            <span className="mt-1 block text-xs text-ink-3">{t("Ограничивает длину и стоимость одного ответа модели (256–64 000). У Claude сюда входят и рассуждения — для него нужно не меньше 16 000.")}</span>
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Глубина рассуждений")}</span>
            <select className={field} value={reasoning} onChange={(e) => setReasoning(e.target.value)}>
              <option value="low">{t("Низкая — быстрее и дешевле (рекомендуется)")}</option>
              <option value="medium">{t("Средняя")}</option>
              <option value="high">{t("Высокая — дольше и дороже")}</option>
              <option value="">{t("По умолчанию модели")}</option>
            </select>
            <span className="mt-1 block text-xs text-ink-3">
              {t(
                "Сколько модель «думает» перед ответом: у Claude — effort, у reasoning-моделей OpenAI — reasoning_effort. Выбор AI-сотрудников всегда идёт с низкой. Моделям, которые параметр не принимают, он не отправляется.",
              )}
            </span>
          </label>
        </div>
        <label className="mt-5 flex items-center gap-2.5 text-sm text-ink">
          <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
          {t("AI включён для пользователей")}
        </label>
      </Section>

      <Section title={t("Бюджет и лимиты")}>
        <div className="grid gap-5 lg:grid-cols-2">
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Дневной бюджет, $")}</span>
            <input className={field} value={dailyBudget} onChange={(e) => setDailyBudget(e.target.value)} placeholder={t("без ограничения")} inputMode="decimal" />
            <span className="mt-1 block text-xs text-ink-3">{t("Потрачено за день (по Ташкенту) больше — новые запросы к AI до завтра не выполняются, пользователи видят причину.")}</span>
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Месячный бюджет, $")}</span>
            <input className={field} value={monthlyBudget} onChange={(e) => setMonthlyBudget(e.target.value)} placeholder={t("без ограничения")} inputMode="decimal" />
            <span className="mt-1 block text-xs text-ink-3">{t("То же за календарный месяц. Считаются вызовы моделей с заданной ценой в «Модели».")}</span>
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Предупредить при, % бюджета")}</span>
            <input className={field} value={warnPercent} onChange={(e) => setWarnPercent(e.target.value)} inputMode="numeric" />
            <span className="mt-1 block text-xs text-ink-3">{t("Администраторам AI придёт уведомление (колокольчик) — при этой доле и когда бюджет исчерпан.")}</span>
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Одновременных вызовов моделей")}</span>
            <input className={field} value={concurrent} onChange={(e) => setConcurrent(e.target.value)} inputMode="numeric" />
            <span className="mt-1 block text-xs text-ink-3">{t("Сверх лимита вызовы ждут очереди, а не получают отказ провайдера. 0 — без ограничения.")}</span>
          </label>
        </div>
        <div className="mt-5">
          <button className={primary} onClick={save} disabled={busy !== null}>
            {busy === "save" && <Loader2 className="size-4 animate-spin" />}
            {t("Сохранить")}
          </button>
        </div>
      </Section>

      <Note>
        {t(
          "«Проверить модель» отправляет модели короткий запрос — провайдер берёт оплату за несколько десятков токенов. Резервная модель включается автоматически при ошибке основной: сбой сервера или сети (после одного повтора), лимит запросов, недоступный ключ, отказ модели отвечать по правилам безопасности.",
        )}
      </Note>
    </>
  );
}
