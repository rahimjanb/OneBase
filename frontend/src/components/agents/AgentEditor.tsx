"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { Ban, BookOpen, Bot, Clock, FlaskConical, MessageSquare, MessagesSquare, PenLine, ShieldCheck } from "lucide-react";
import { button, danger, field, primary } from "@/components/ai/form";
import { AgentAnswers, AgentDialogs, AgentReplies, AgentTest } from "@/components/agents/AgentTabs";
import { Badge, Card, ErrorText, Label, textarea, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { agentsApi, kindLabel, settingsOf, type AgentSettingsInput, type AgentView } from "@/lib/agents";

type Tab = "main" | "style" | "limits" | "schedule" | "answers" | "replies" | "dialogs" | "test";

const DAYS = ["пн", "вт", "ср", "чт", "пт", "сб", "вс"];

/** Страница ИИ-агента: все его настройки, готовые ответы, журнал ответов, диалоги отдела и проверка — в одном месте. */
export function AgentEditor({ initial }: { initial: AgentView }) {
  const t = useT();
  const router = useRouter();
  const params = useSearchParams();
  const [agent, setAgent] = useState(initial);
  const [s, setS] = useState<AgentSettingsInput>(settingsOf(initial));
  const [saved, setSaved] = useState(false);
  const [tab, setTab] = useState<Tab>((params.get("tab") as Tab) || "main");
  const { busy, error, run } = useAction();
  const recruiter = agent.kind === "recruiter";
  const dirty = JSON.stringify(s) !== JSON.stringify(settingsOf(agent));

  const set = <K extends keyof AgentSettingsInput>(key: K, value: AgentSettingsInput[K]) => {
    setS((x) => ({ ...x, [key]: value }));
    setSaved(false);
  };
  const text = (key: keyof AgentSettingsInput) => (e: { target: { value: string } }) => set(key, (e.target.value || null) as never);
  const num = (key: keyof AgentSettingsInput) => (e: { target: { value: string } }) => set(key, Number(e.target.value) as never);
  const check = (key: keyof AgentSettingsInput) => (e: { target: { checked: boolean } }) => set(key, e.target.checked as never);

  const save = () =>
    run("save", async () => {
      const v = await agentsApi.save(agent.id, s);
      setAgent(v);
      setS(settingsOf(v));
      setSaved(true);
    });

  const tabs: { key: Tab; label: string; icon: typeof Bot; hidden?: boolean; count?: number }[] = [
    { key: "main", label: "Основное", icon: Bot },
    { key: "style", label: "Как пишет", icon: PenLine },
    { key: "limits", label: "Ограничения", icon: ShieldCheck },
    { key: "schedule", label: "Когда отвечает", icon: Clock },
    { key: "answers", label: "Готовые ответы", icon: BookOpen, count: agent.answers.length },
    { key: "replies", label: "Ответы", icon: MessageSquare, count: agent.drafts },
    { key: "dialogs", label: "Диалоги", icon: MessagesSquare },
    { key: "test", label: "Проверка", icon: FlaskConical },
  ];
  const settingsTab = tab === "main" || tab === "style" || tab === "limits" || tab === "schedule";
  const days = s.workDays.split(",").map(Number);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <Badge>{t(kindLabel[agent.kind])}</Badge>
        <Badge>{agent.departmentName}</Badge>
        <Badge className={agent.enabled ? "bg-ok-soft text-ok" : "bg-muted text-ink-3"}>{agent.enabled ? t("включён") : t("выключен")}</Badge>
        {agent.isDefault && <Badge>{t("агент отдела по умолчанию")}</Badge>}
        {agent.telegramAccounts === 0 && <Badge className="bg-warn-soft text-warn">{t("у отдела нет номера Telegram")}</Badge>}
      </div>

      <nav className="flex flex-wrap gap-x-1 border-b border-line">
        {tabs
          .filter((x) => !x.hidden)
          .map((x) => (
            <button
              key={x.key}
              onClick={() => setTab(x.key)}
              className={`flex shrink-0 items-center gap-1.5 border-b-2 px-3 py-2 text-sm ${tab === x.key ? "border-accent text-ink" : "border-transparent text-ink-3 hover:text-ink"}`}
            >
              <x.icon className="size-4" />
              {t(x.label)}
              {!!x.count && <span className="rounded-full bg-muted px-1.5 text-xs">{x.count}</span>}
            </button>
          ))}
      </nav>

      {tab === "main" && (
        <Card>
          <div className="grid max-w-3xl gap-4 sm:grid-cols-2">
            <Label label="Название в OneBase">
              <input className={field} value={s.name} maxLength={100} onChange={(e) => set("name", e.target.value)} />
            </Label>
            <Label label="Модель" hint="Пусто — основная модель из «Настройки → AI».">
              <select
                className={field}
                value={s.model ? `${s.modelProvider}|${s.model}` : ""}
                onChange={(e) => {
                  const [provider, model] = e.target.value ? e.target.value.split("|") : [null, null];
                  setS((x) => ({ ...x, modelProvider: provider, model }));
                  setSaved(false);
                }}
              >
                <option value="">{t("Основная")}</option>
                {agent.models.map((m) => (
                  <option key={`${m.provider}|${m.model}`} value={`${m.provider}|${m.model}`}>
                    {m.name}
                  </option>
                ))}
              </select>
            </Label>
            <label className="flex items-start gap-2 text-sm sm:col-span-2">
              <input type="checkbox" className="mt-0.5 size-4 accent-[var(--color-accent)]" checked={s.enabled} onChange={check("enabled")} />
              <span>
                <b>{t("Агент включён")}</b>
                <span className="block text-xs text-ink-3">
                  {recruiter
                    ? t("Отвечает кандидатам по вакансиям, где включена «Миссия AI-рекрутера». Сначала просит согласие на обработку данных. Ответы — платные вызовы модели.")
                    : t("Отвечает в Telegram на сообщения, которые пришли в отдел (номер с «Подразделения: {name}»). Ответы — платные вызовы модели.", { name: agent.departmentName })}
                </span>
              </span>
            </label>
            <fieldset className="sm:col-span-2">
              <legend className="mb-1 text-xs font-medium text-ink-2">{t("Как отправляет ответы")}</legend>
              <div className="grid gap-2 sm:grid-cols-2">
                {[
                  { value: "Auto", title: "Отвечает сам", text: "Ответ уходит после паузы «как у человека»." },
                  { value: "Approval", title: "С проверкой", text: "Агент пишет черновик, сотрудник одобряет или правит его во вкладке «Ответы»." },
                ].map((o) => (
                  <label key={o.value} className={`flex cursor-pointer items-start gap-2 rounded-lg border p-3 text-sm ${s.mode === o.value ? "border-accent bg-accent-soft/40" : "border-line"}`}>
                    <input type="radio" className="mt-0.5 accent-[var(--color-accent)]" checked={s.mode === o.value} onChange={() => set("mode", o.value as "Auto" | "Approval")} />
                    <span>
                      <b>{t(o.title)}</b>
                      <span className="block text-xs text-ink-3">{t(o.text)}</span>
                    </span>
                  </label>
                ))}
              </div>
            </fieldset>
          </div>
          {recruiter && agent.vacancies.length > 0 && (
            <div className="mt-4 text-sm">
              <p className="mb-1 text-xs font-medium text-ink-2">{t("Вакансии этого рекрутера")}</p>
              <ul className="flex flex-wrap gap-2">
                {agent.vacancies.map((v) => (
                  <li key={v.id}>
                    <Link href={`/hr/recruitment/vacancies/${v.id}`} className="rounded-full border border-line px-2 py-0.5 text-xs hover:border-accent">
                      {v.code} · {v.title} {v.aiRecruiter ? "· AI" : `· ${t("AI выключен")}`}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          )}
          <div className="mt-4 flex flex-wrap gap-2 border-t border-line pt-4">
            {!agent.isDefault && (
              <button className={button} disabled={!!busy} onClick={() => run("default", async () => setAgent(await agentsApi.makeDefault(agent.id)))}>
                {t("Сделать агентом отдела по умолчанию")}
              </button>
            )}
            <button
              className={danger}
              disabled={!!busy}
              onClick={() => {
                if (window.confirm(t("Удалить агента «{name}»? Его готовые ответы и журнал тоже удалятся.", { name: agent.name }))) {
                  run("delete", async () => {
                    await agentsApi.remove(agent.id);
                    router.push("/settings/agents");
                  });
                }
              }}
            >
              {t("Удалить агента")}
            </button>
          </div>
        </Card>
      )}

      {tab === "style" && (
        <Card>
          <div className="grid max-w-3xl gap-4 sm:grid-cols-2">
            <Label label="Имя в переписке" hint="Например «Дилноза». Пусто — без имени.">
              <input className={field} value={s.displayName ?? ""} maxLength={60} onChange={text("displayName")} />
            </Label>
            <Label label="Кем представляется" hint={recruiter ? "Например «рекрутер компании Candy Gold»." : "Например «менеджер отдела снабжения Candy Gold»."}>
              <input className={field} value={s.intro ?? ""} maxLength={300} onChange={text("intro")} />
            </Label>
            <Label label="Тон">
              <select className={field} value={s.tone} onChange={(e) => set("tone", e.target.value as AgentSettingsInput["tone"])}>
                <option value="friendly">{t("Дружелюбный")}</option>
                <option value="neutral">{t("Спокойный, по делу")}</option>
                <option value="formal">{t("Официальный")}</option>
              </select>
            </Label>
            <Label label="Язык">
              <select className={field} value={s.language} onChange={(e) => set("language", e.target.value as AgentSettingsInput["language"])}>
                <option value="auto">{t("Как пишет собеседник (русский или узбекский)")}</option>
                <option value="ru">{t("Русский")}</option>
                <option value="uz">{t("Узбекский")}</option>
              </select>
            </Label>
            <Label label="Длина ответов">
              <select className={field} value={s.length} onChange={(e) => set("length", e.target.value as AgentSettingsInput["length"])}>
                <option value="short">{t("Коротко, как в мессенджере")}</option>
                <option value="normal">{t("Обычно, 2–4 предложения")}</option>
                <option value="detailed">{t("Подробно")}</option>
              </select>
            </Label>
            <div className="space-y-2 text-sm">
              <label className="flex items-start gap-2">
                <input type="checkbox" className="mt-0.5 size-4 accent-[var(--color-accent)]" checked={s.splitMessages} onChange={check("splitMessages")} />
                <span>{t("Несколько коротких сообщений вместо одного длинного (до трёх)")}</span>
              </label>
              <label className="flex items-start gap-2">
                <input type="checkbox" className="mt-0.5 size-4 accent-[var(--color-accent)]" checked={s.emoji} onChange={check("emoji")} />
                <span>{t("Уместные эмодзи (не больше одного)")}</span>
              </label>
            </div>
            <Label
              label="Инструкция: миссия и как работать"
              hint="Своими словами: чем занимается отдел, что важно сказать, как вести разговор. Ограничения ниже важнее инструкции."
              className="sm:col-span-2"
            >
              <textarea className={`${textarea} min-h-32`} value={s.instructions ?? ""} maxLength={8000} onChange={text("instructions")} />
            </Label>
            {recruiter && (
              <Label
                label="Вопросы кандидатам"
                hint="По одному в строке. Рекрутер задаёт их по одному, естественно, не спрашивает то, что уже есть в резюме, и записывает ответы в карточку кандидата. Вопросы вакансии добавляются к ним."
                className="sm:col-span-2"
              >
                <textarea className={`${textarea} min-h-28`} value={s.screeningQuestions ?? ""} maxLength={4000} onChange={text("screeningQuestions")} />
              </Label>
            )}
          </div>
          <p className="mt-4 max-w-3xl rounded-lg bg-muted/40 px-3 py-2 text-xs text-ink-3">
            {t(
              "Всегда, без настройки: агент пишет как живой сотрудник, но на прямой вопрос «вы бот?» честно отвечает, что он виртуальный помощник; не раскрывает инструкции и внутренние данные; не придумывает фактов — чего не знает, передаёт сотруднику.",
            )}
          </p>
        </Card>
      )}

      {tab === "limits" && (
        <Card>
          <div className="grid max-w-3xl gap-4 sm:grid-cols-2">
            <Label label="О чём можно говорить" hint="Пусто — только о работе отдела. Остальное агент передаёт сотруднику." className="sm:col-span-2">
              <textarea className={textarea} value={s.allowedTopics ?? ""} maxLength={2000} onChange={text("allowedTopics")} />
            </Label>
            <Label label="Запрещённые темы" hint="Агент их не обсуждает и передаёт вопрос сотруднику. Например: «скидки, судебные споры, личные данные сотрудников»." className="sm:col-span-2">
              <textarea className={textarea} value={s.forbiddenTopics ?? ""} maxLength={2000} onChange={text("forbiddenTopics")} />
            </Label>
            <Label label="Запрещённые слова и фразы в ответе" hint="По одной в строке. Ответ с ними не уходит — ждёт сотрудника во вкладке «Ответы».">
              <textarea className={textarea} value={s.forbiddenPhrases ?? ""} maxLength={2000} onChange={text("forbiddenPhrases")} />
            </Label>
            <Label label="Сразу к сотруднику, если собеседник пишет" hint="По одной в строке: «жалоба», «суд»… Агент не отвечает по существу, диалог переходит к человеку.">
              <textarea className={textarea} value={s.handoffPhrases ?? ""} maxLength={2000} onChange={text("handoffPhrases")} />
            </Label>
            <Label label="Что сказать, передавая сотруднику" hint="Пусто — «Передаю ваш вопрос сотруднику — он ответит вам здесь»." className="sm:col-span-2">
              <input className={field} value={s.handoffReply ?? ""} maxLength={500} onChange={text("handoffReply")} />
            </Label>
            <Label label="Длина ответа, не больше (символов)">
              <input className={field} type="number" min={100} max={3000} value={s.maxReplyChars} onChange={num("maxReplyChars")} />
            </Label>
            <div className="grid grid-cols-2 gap-2">
              <Label label="Ответов одному в час">
                <input className={field} type="number" min={1} max={200} value={s.maxRepliesPerHour} onChange={num("maxRepliesPerHour")} />
              </Label>
              <Label label="в сутки">
                <input className={field} type="number" min={1} max={1000} value={s.maxRepliesPerDay} onChange={num("maxRepliesPerDay")} />
              </Label>
            </div>
            {recruiter && (
              <div className="space-y-2 text-sm sm:col-span-2">
                <label className="flex items-start gap-2">
                  <input type="checkbox" className="mt-0.5 size-4 accent-[var(--color-accent)]" checked={s.canDiscussSalary} onChange={check("canDiscussSalary")} />
                  <span>
                    {t("Может называть вилку зарплаты из вакансии")}
                    <span className="block text-xs text-ink-3">{t("Иначе на вопросы о деньгах отвечает сотрудник HR.")}</span>
                  </span>
                </label>
                <label className="flex items-start gap-2">
                  <input type="checkbox" className="mt-0.5 size-4 accent-[var(--color-accent)]" checked={s.canScheduleInterviews} onChange={check("canScheduleInterviews")} />
                  <span>
                    {t("Сам приглашает на собеседование")}
                    <span className="block text-xs text-ink-3">{t("Время — только свободное по календарю интервьюеров. Иначе — передаёт кандидата HR.")}</span>
                  </span>
                </label>
              </div>
            )}
          </div>
        </Card>
      )}

      {tab === "schedule" && (
        <Card>
          <div className="grid max-w-3xl gap-4 sm:grid-cols-2">
            <div className="grid grid-cols-2 gap-2">
              <Label label="Рабочее время: с" hint="Ташкент. Пусто — круглосуточно.">
                <input className={field} type="time" value={s.workFrom ?? ""} onChange={text("workFrom")} />
              </Label>
              <Label label="до">
                <input className={field} type="time" value={s.workTo ?? ""} onChange={text("workTo")} />
              </Label>
            </div>
            <div>
              <span className="mb-1 block text-xs font-medium text-ink-2">{t("Рабочие дни")}</span>
              <div className="flex flex-wrap gap-2 text-sm">
                {DAYS.map((d, i) => (
                  <label key={d} className="flex items-center gap-1">
                    <input
                      type="checkbox"
                      className="size-4 accent-[var(--color-accent)]"
                      checked={days.includes(i + 1)}
                      onChange={(e) => set("workDays", (e.target.checked ? [...days, i + 1] : days.filter((x) => x !== i + 1)).sort().join(","))}
                    />
                    {t(d)}
                  </label>
                ))}
              </div>
            </div>
            <Label label="Вне рабочего времени" className="sm:col-span-2">
              <select className={field} value={s.offHours} onChange={(e) => set("offHours", e.target.value as AgentSettingsInput["offHours"])}>
                <option value="morning">{t("Ответ уходит в начале рабочего времени")}</option>
                <option value="reply">{t("Отвечать сразу, в любое время")}</option>
              </select>
            </Label>
            <div className="grid grid-cols-2 gap-2">
              <Label label="Пауза перед ответом: от, сек">
                <input className={field} type="number" min={0} max={600} value={s.replyDelayMinSeconds} onChange={num("replyDelayMinSeconds")} />
              </Label>
              <Label label="до, сек">
                <input className={field} type="number" min={0} max={600} value={s.replyDelayMaxSeconds} onChange={num("replyDelayMaxSeconds")} />
              </Label>
            </div>
            <label className="flex items-start gap-2 text-sm">
              <input type="checkbox" className="mt-0.5 size-4 accent-[var(--color-accent)]" checked={s.typing} onChange={check("typing")} />
              <span>
                {t("Показывать «печатает…» перед ответом")}
                <span className="block text-xs text-ink-3">{t("Если собеседник пишет ещё до ответа, агент ответит на всё сразу.")}</span>
              </span>
            </label>
            {recruiter && (
              <Label label="Напомнить не ответившему кандидату через, часов" hint="Одно напоминание, в рабочее время; 0 — не напоминать.">
                <input className={field} type="number" min={0} max={168} value={s.followUpHours} onChange={num("followUpHours")} />
              </Label>
            )}
          </div>
        </Card>
      )}

      {settingsTab && (
        <div className="sticky bottom-3 z-10 flex flex-wrap items-center gap-3 rounded-xl border border-line bg-surface/95 px-4 py-3 shadow-sm backdrop-blur">
          <button className={primary} disabled={!!busy || !dirty} onClick={save}>
            {t("Сохранить")}
          </button>
          {dirty ? <span className="text-sm text-warn">{t("Есть несохранённые изменения.")}</span> : saved && <span className="text-sm text-ok">{t("Сохранено.")}</span>}
          <ErrorText error={error} />
        </div>
      )}

      {tab === "answers" && <AgentAnswers agent={agent} onChange={setAgent} />}
      {tab === "replies" && <AgentReplies agent={agent} />}
      {tab === "dialogs" &&
        (recruiter ? (
          <Card>
            <p className="text-sm text-ink-2">
              {t("Переписка рекрутера с кандидатами — в разделе подбора: там же карточки кандидатов, резюме и собеседования.")}{" "}
              <Link className="text-accent-strong hover:underline" href="/hr/recruitment/telegram">
                {t("HR → Подбор → Диалоги Telegram")}
              </Link>
            </p>
          </Card>
        ) : (
          <AgentDialogs agent={agent} />
        ))}
      {tab === "test" && <AgentTest agent={agent} />}
      {!settingsTab && !agent.enabled && (
        <p className="flex items-center gap-2 text-xs text-ink-3">
          <Ban className="size-3.5" /> {t("Агент выключен — в Telegram он не отвечает. Включить — во вкладке «Основное».")}
        </p>
      )}
    </div>
  );
}
