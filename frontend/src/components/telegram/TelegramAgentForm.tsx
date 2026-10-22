"use client";

import { useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Note } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { telegramApi, type TelegramAccount, type TelegramAgentSettings } from "@/lib/telegram";

/** Настройки AI-агента аккаунта: применяются сразу. Права пользователей проверяет сервер — инструкция их не расширяет. */
export function TelegramAgentForm({ account, onSaved }: { account: TelegramAccount; onSaved: (enabled: boolean) => void }) {
  const t = useT();
  const [s, setS] = useState<TelegramAgentSettings | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    telegramApi.settings(account.id).then(setS, (e: Error) => setError(e.message));
  }, [account.id]);

  if (!s) return error ? <p className="rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p> : <Loader2 className="size-5 animate-spin text-ink-3" />;

  const set = <K extends keyof TelegramAgentSettings>(key: K, value: TelegramAgentSettings[K]) => {
    setS({ ...s, [key]: value });
    setMessage(null);
  };

  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const saved = await telegramApi.saveSettings(account.id, s);
      setS(saved);
      onSaved(saved.enabled);
      setMessage(t("Сохранено — действует сразу."));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="max-w-4xl">
      <div className="grid gap-3 sm:grid-cols-2">
        <Check label={t("Агент включён")} hint={t("Отвечает на входящие этого аккаунта")} checked={s.enabled} onChange={(v) => set("enabled", v)} />
        <Check label={t("Отвечать автоматически")} hint={t("Выключено — сообщения только записываются")} checked={s.autoReply} onChange={(v) => set("autoReply", v)} />
      </div>
      <p className="mt-2 text-xs text-ink-3">{t("Кому отвечать (всем по отправленным данным или только списку) — во вкладке «Пользователи».")}</p>

      <div className="mt-5">
        <Field
          label={t("Как агент представляется")}
          hint={t("На «кто ты?» агент отвечает от лица компании. На прямой вопрос «ты человек или бот?» он не выдаёт себя за человека — отвечает, что он виртуальный сотрудник.")}
        >
          <input className={field} value={s.persona ?? ""} onChange={(e) => set("persona", e.target.value || null)} maxLength={1000} placeholder="сотрудник компании Candy Gold" />
        </Field>
      </div>

      <div className="mt-5 grid gap-4 sm:grid-cols-2">
        <Field label={t("Модель ответов")} hint={t("«провайдер/модель», например anthropic/claude-opus-5-5; пусто — основная модель консультанта («Настройки → AI»)")}>
          <input
            className={field}
            value={s.modelProvider && s.model ? `${s.modelProvider}/${s.model}` : (s.model ?? "")}
            onChange={(e) => setS({ ...s, model: e.target.value || null, modelProvider: null })}
            placeholder={t("основная")}
            spellCheck={false}
          />
        </Field>
        <Field
          label={t("Модель генерации")}
          hint={t("Для просьб собрать отчёт, таблицу, документ, письмо. Пусто — Claude из AI Data Manager, если подключён ключ Anthropic.")}
        >
          <input
            className={field}
            value={s.generationModelProvider && s.generationModel ? `${s.generationModelProvider}/${s.generationModel}` : (s.generationModel ?? "")}
            onChange={(e) => setS({ ...s, generationModel: e.target.value || null, generationModelProvider: null })}
            placeholder="anthropic/claude-opus-5-5"
            spellCheck={false}
          />
        </Field>
        <Field label={t("Стиль ответа")}>
          <select className={field} value={s.responseStyle} onChange={(e) => set("responseStyle", e.target.value as TelegramAgentSettings["responseStyle"])}>
            <option value="short">{t("Коротко")}</option>
            <option value="normal">{t("Обычно")}</option>
            <option value="detailed">{t("Подробно")}</option>
          </select>
        </Field>
        <Field label={t("Язык ответа")}>
          <select className={field} value={s.language} onChange={(e) => set("language", e.target.value as TelegramAgentSettings["language"])}>
            <option value="auto">{t("На языке вопроса")}</option>
            <option value="ru">{t("Русский")}</option>
            <option value="uz">{t("Узбекский")}</option>
            <option value="en">{t("Английский")}</option>
          </select>
        </Field>
        <Field label={t("Ответов одному пользователю в час")}>
          <input className={field} type="number" min={1} max={500} value={s.maxMessagesPerHour} onChange={(e) => set("maxMessagesPerHour", Number(e.target.value))} />
        </Field>
        <Field label={t("Длина ответа, токенов")}>
          <input className={field} type="number" min={200} max={8000} step={100} value={s.maxOutputTokens} onChange={(e) => set("maxOutputTokens", Number(e.target.value))} />
        </Field>
        <Field label={t("Пауза перед ответом, секунд")}>
          <input className={field} type="number" min={0} max={60} value={s.replyDelaySeconds} onChange={(e) => set("replyDelaySeconds", Number(e.target.value))} />
        </Field>
        <Field label={t("Принимаемые файлы")} hint={t("Расширения через запятую")}>
          <input className={field} value={s.allowedAttachments} onChange={(e) => set("allowedAttachments", e.target.value)} />
        </Field>
        <Field label={t("Размер файла, МБ")}>
          <input className={field} type="number" min={1} max={50} value={s.maxFileMb} onChange={(e) => set("maxFileMb", Number(e.target.value))} />
        </Field>
      </div>

      <div className="mt-4 grid gap-4">
        <Check
          label={t("Отчёты и документы файлом")}
          hint={t("На просьбу подготовить отчёт, письмо или другой документ агент пишет «минутку» и присылает файл (Excel или Word) без текста — тем, у кого в списке разрешены файлы и полный доступ. Остальным — текстом.")}
          checked={s.allowReports}
          onChange={(v) => set("allowReports", v)}
        />
        <Field label={t("Инструкция агента")} hint={t("Кто он, как обращаться, что важно компании. Права доступа инструкция не расширяет.")}>
          <textarea className={`${field} h-28 py-2`} value={s.systemPrompt ?? ""} onChange={(e) => set("systemPrompt", e.target.value || null)} maxLength={8000} />
        </Field>
        <Field label={t("Ограничения")} hint={t("О чём не говорить, чего не обещать. Дополняют проверки сервера, но не заменяют их.")}>
          <textarea className={`${field} h-24 py-2`} value={s.restrictions ?? ""} onChange={(e) => set("restrictions", e.target.value || null)} maxLength={8000} />
        </Field>
        <Field label={t("Ответ, если данных нет")}>
          <input className={field} value={s.noInfoReply} onChange={(e) => set("noInfoReply", e.target.value)} maxLength={1000} />
        </Field>
      </div>

      {error && <p className="mt-4 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      {message && <p className="mt-4 rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">{message}</p>}
      <div className="mt-4 flex gap-2">
        <button className={primary} onClick={save} disabled={busy}>
          {busy && <Loader2 className="size-4 animate-spin" />}
          {t("Сохранить")}
        </button>
        <button className={button} onClick={() => telegramApi.settings(account.id).then(setS)} disabled={busy}>
          {t("Отменить изменения")}
        </button>
      </div>
      <Note>
        {t(
          "Полный доступ — ответы по данным OneBase с правами пользователя, выбранного во вкладке «Пользователи». «Только отправленные данные» — ответы лишь по сообщениям и отчётам, отправленным в этот чат через OneBase: агент объясняет, что в отчёте и насколько всё хорошо или плохо, говорит, когда следующая отправка; чего в материалах нет — так и отвечает. В группах агент отвечает, только если включён для группы во вкладке «Чаты» и к нему обратились. Как он ответит — можно проверить во вкладке «Проверка».",
        )}
      </Note>
    </div>
  );
}

function Field({ label, hint, children }: { label: string; hint?: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-medium text-ink">{label}</span>
      {children}
      {hint && <span className="mt-1 block text-xs text-ink-3">{hint}</span>}
    </label>
  );
}

function Check({ label, hint, checked, onChange }: { label: string; hint: string; checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <label className="flex items-start gap-2.5 rounded-lg border border-line p-3 text-sm">
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} className="mt-0.5 size-4 accent-[var(--color-accent)]" />
      <span>
        <span className="block font-medium text-ink">{label}</span>
        <span className="block text-xs text-ink-3">{hint}</span>
      </span>
    </label>
  );
}
