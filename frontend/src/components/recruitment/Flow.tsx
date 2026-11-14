"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { CheckCircle2, Loader2, RefreshCw, Sparkles } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { InterviewSettingsCard } from "@/components/recruitment/Interviews";
import { CandidateScreening } from "@/components/recruitment/screening/CandidateScreening";
import { HhGuardCard } from "@/components/recruitment/screening/HhGuardCard";
import { Badge, Card, ErrorText, Label, textarea, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { stageLabel, when, type CandidateCard, type RecruitmentContext, type Stage, type Vacancy } from "@/lib/recruitment";
import {
  confirmationLabel,
  exceptionLabel,
  flowApi,
  formatLabel,
  hhApi,
  interviewStatusLabel,
  slotText,
  type AiSettings,
  type Analytics,
  type ExceptionRow,
  type HhNegotiation,
  type HhStatus,
  type HhVacancyLink,
  type InterviewRow,
  type Person,
  type RecruiterBrief,
  type Slot,
} from "@/lib/recruitment-flow";

// ---------- Исключения ----------

export function ExceptionsView({ ctx }: { ctx: RecruitmentContext }) {
  const t = useT();
  const [status, setStatus] = useState("Open");
  const [rows, setRows] = useState<ExceptionRow[] | null>(null);
  const [note, setNote] = useState<Record<string, string>>({});
  const { busy, error, run } = useAction();
  useEffect(() => {
    run("load", async () => setRows(await flowApi.exceptions(status)));
  }, [status]); // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <div className="space-y-3">
      <p className="text-sm text-ink-2">{t("Здесь всё, где AI или служба остановилась и ждёт человека. Останавливается только этот диалог — остальные работают.")}</p>
      <div className="flex gap-2">
        <button className={status === "Open" ? primary : button} onClick={() => setStatus("Open")}>
          {t("Открытые")}
        </button>
        <button className={status === "Resolved" ? primary : button} onClick={() => setStatus("Resolved")}>
          {t("Решённые")}
        </button>
      </div>
      <ErrorText error={error} />
      {!rows ? (
        <p className="text-sm text-ink-3">{t("Загрузка…")}</p>
      ) : rows.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line p-6 text-center text-sm text-ink-3">{t("Исключений нет.")}</p>
      ) : (
        <ul className="space-y-2">
          {rows.map((e) => (
            <li key={e.id} className="rounded-xl border border-line bg-surface p-3 text-sm">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <Badge className="bg-warn-soft text-warn">{t(exceptionLabel[e.kind] ?? e.kind)}</Badge>
                <span className="text-xs text-ink-3">{when(e.createdAt)}</span>
              </div>
              <p className="mt-1 text-ink">{t(e.message)}</p>
              <div className="mt-1 flex flex-wrap gap-3 text-xs">
                {e.candidateId && (
                  <Link href={`/hr/recruitment/candidates/${e.candidateId}`} className="text-accent-strong hover:underline">
                    {e.candidateName ?? t("кандидат")}
                  </Link>
                )}
                {e.vacancyId && (
                  <Link href={`/hr/recruitment/vacancies/${e.vacancyId}`} className="text-accent-strong hover:underline">
                    {e.vacancyCode}
                  </Link>
                )}
                {e.conversationId && (
                  <Link href="/hr/recruitment/telegram" className="text-accent-strong hover:underline">
                    {t("диалог")}
                  </Link>
                )}
                {e.resolvedAt && <span className="text-ink-3">{t("решено: {who}, {when}", { who: e.resolvedBy ?? "—", when: when(e.resolvedAt) })}{e.resolution && ` — ${e.resolution}`}</span>}
              </div>
              {ctx.canManage && e.status === "Open" && (
                <div className="mt-2 flex flex-wrap gap-2">
                  <input className={`${field} max-w-md`} placeholder={t("Что сделано (необязательно)")} value={note[e.id] ?? ""} onChange={(x) => setNote({ ...note, [e.id]: x.target.value })} />
                  <button className={button} disabled={!!busy} onClick={() => run("resolve", async () => setRows(await flowApi.resolve(e.id, note[e.id] || null, false)))}>
                    <CheckCircle2 className="size-4" />
                    {t("Решено")}
                  </button>
                  {e.conversationId && (
                    <button className={button} disabled={!!busy} onClick={() => run("resolve", async () => setRows(await flowApi.resolve(e.id, note[e.id] || null, true)))}>
                      {t("Решено, вернуть AI")}
                    </button>
                  )}
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

// ---------- Аналитика ----------

export function AnalyticsView() {
  const t = useT();
  const [a, setA] = useState<Analytics | null>(null);
  const { error, run } = useAction();
  useEffect(() => {
    run("load", async () => setA(await flowApi.analytics()));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  if (!a) return error ? <ErrorText error={error} /> : <p className="text-sm text-ink-3">{t("Загрузка…")}</p>;
  const tiles: [string, string | number][] = [
    ["Кандидатов в базе", a.candidates],
    ["Диалогов в Telegram", a.conversations],
    ["Ответов AI", a.aiReplies],
    ["Передано человеку", a.handoffs],
    ["Открытых исключений", a.openExceptions],
    ["Медиана ответа, мин", a.medianReplyMinutes ?? "—"],
  ];
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
        {tiles.map(([label, value]) => (
          <div key={label} className="rounded-xl border border-line bg-surface p-3">
            <div className="text-xs text-ink-3">{t(label)}</div>
            <div className="text-xl font-semibold tabular-nums text-ink">{value}</div>
          </div>
        ))}
      </div>
      <Card title="Воронка по вакансиям">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[720px] text-sm">
            <thead>
              <tr className="text-left text-xs text-ink-3">
                <th className="py-1 pr-2">{t("Вакансия")}</th>
                <th className="py-1 pr-2 text-right">{t("Всего")}</th>
                {(["New", "Screening", "Interview", "Offer", "Hired", "Rejected"] as Stage[]).map((s) => (
                  <th key={s} className="py-1 pr-2 text-right">
                    {t(stageLabel[s])}
                  </th>
                ))}
                <th className="py-1 text-right">{t("Дней до найма")}</th>
              </tr>
            </thead>
            <tbody>
              {a.vacancies.map((v) => (
                <tr key={v.vacancyId} className="border-t border-line">
                  <td className="py-1.5 pr-2">
                    <Link href={`/hr/recruitment/vacancies/${v.vacancyId}`} className="hover:text-accent-strong">
                      {v.code} {v.title}
                    </Link>
                  </td>
                  <td className="py-1.5 pr-2 text-right tabular-nums">{v.total}</td>
                  {(["New", "Screening", "Interview", "Offer", "Hired", "Rejected"] as Stage[]).map((s) => (
                    <td key={s} className="py-1.5 pr-2 text-right tabular-nums">
                      {v.stages[s] ?? 0}
                    </td>
                  ))}
                  <td className="py-1.5 text-right tabular-nums">{v.daysToHire ?? "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>
      <Card title="Источники">
        <ul className="text-sm">
          {a.sources.map((s) => (
            <li key={s.source} className="flex justify-between border-t border-line py-1 first:border-0">
              <span>{s.source}</span>
              <span className="tabular-nums text-ink-2">
                {t("кандидатов {n}, принято {h}", { n: s.candidates, h: s.hired })}
              </span>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}

// ---------- Настройки AI-рекрутера ----------

const DAYS = ["пн", "вт", "ср", "чт", "пт", "сб", "вс"];

export function AiSettingsCard({ ctx }: { ctx: RecruitmentContext }) {
  const t = useT();
  const [s, setS] = useState<AiSettings | null>(null);
  const [saved, setSaved] = useState(false);
  const { busy, error, run } = useAction();
  useEffect(() => {
    run("load", async () => setS(await flowApi.aiSettings()));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  if (!s) return error ? <ErrorText error={error} /> : null;
  const days = s.interviewDays.split(",").map(Number);
  return (
    <Card title="Собеседования и резюме">
      <div className="mb-4 flex max-w-3xl flex-wrap items-center gap-3 rounded-lg border border-line bg-muted/40 px-3 py-2 text-sm">
        <Sparkles className="size-4 text-accent-strong" />
        <span className="min-w-0 flex-1">
          <b>{t("AI-рекрутер")}</b> — {s.recruiterEnabled ? t("включён") : t("выключен")}.{" "}
          <span className="text-ink-3">
            {t("Как представляется и пишет, вопросы кандидатам, ограничения, рабочее время, напоминания и журнал ответов — в «Настройки → ИИ-агенты».")}
          </span>
        </span>
        {s.recruiterAgentId && (
          <Link className={button} href={`/settings/agents/${s.recruiterAgentId}`}>
            {t("Настроить AI-рекрутера")}
          </Link>
        )}
      </div>
      <div className="grid max-w-3xl gap-3 sm:grid-cols-2">
        <Label label="Собеседования: с">
          <input className={field} type="time" disabled={!ctx.canManage} value={s.interviewDayStart} onChange={(e) => setS({ ...s, interviewDayStart: e.target.value })} />
        </Label>
        <Label label="до">
          <input className={field} type="time" disabled={!ctx.canManage} value={s.interviewDayEnd} onChange={(e) => setS({ ...s, interviewDayEnd: e.target.value })} />
        </Label>
        <Label label="Длина собеседования, минут">
          <input className={field} type="number" min={15} max={240} disabled={!ctx.canManage} value={s.interviewSlotMinutes} onChange={(e) => setS({ ...s, interviewSlotMinutes: Number(e.target.value) })} />
        </Label>
        <div>
          <span className="mb-1 block text-xs font-medium text-ink-2">{t("Дни собеседований")}</span>
          <div className="flex flex-wrap gap-2 text-sm">
            {DAYS.map((d, i) => (
              <label key={d} className="flex items-center gap-1">
                <input
                  type="checkbox"
                  className="size-4 accent-[var(--color-accent)]"
                  disabled={!ctx.canManage}
                  checked={days.includes(i + 1)}
                  onChange={(e) => setS({ ...s, interviewDays: (e.target.checked ? [...days, i + 1] : days.filter((x) => x !== i + 1)).sort().join(",") })}
                />
                {t(d)}
              </label>
            ))}
          </div>
        </div>
        <label className="flex items-start gap-2 text-sm sm:col-span-2">
          <input type="checkbox" className="mt-0.5 size-4 accent-[var(--color-accent)]" disabled={!ctx.canManage} checked={s.autoScore} onChange={(e) => setS({ ...s, autoScore: e.target.checked })} />
          <span>
            {t("Оценивать резюме AI автоматически")}
            <span className="block text-xs text-ink-3">{t("Иначе — по кнопке «Оценить AI» в карточке кандидата. Платные вызовы.")}</span>
          </span>
        </label>
        <label className="flex items-start gap-2 text-sm sm:col-span-2">
          <input type="checkbox" className="mt-0.5 size-4 accent-[var(--color-accent)]" disabled={!ctx.canManage} checked={s.readScans} onChange={(e) => setS({ ...s, readScans: e.target.checked })} />
          <span>
            {t("Распознавать фото и сканы резюме")}
            <span className="block text-xs text-ink-3">
              {t("Фото резюме (JPG, PNG, WEBP) и PDF без текстового слоя читает AI (Claude) — текст попадает в карточку кандидата, его видят оценка и AI-рекрутер. Паспорт и ID-карту AI не переписывает. Платный вызов на каждый такой файл.")}
            </span>
          </span>
        </label>
        <label className="flex items-start gap-2 text-sm sm:col-span-2">
          <input type="checkbox" className="mt-0.5 size-4 accent-[var(--color-accent)]" disabled={!ctx.canManage} checked={s.analyzeResumes} onChange={(e) => setS({ ...s, analyzeResumes: e.target.checked })} />
          <span>
            {t("Разбирать резюме AI")}
            <span className="block text-xs text-ink-3">
              {t("ФИО, контакты, опыт, образование, навыки и сводка — с цитатой из резюме; что не найдено в тексте, помечается «не подтверждено». Затем — оценка под вакансию. Выключено — из резюме берутся только телефон и почта. Платный вызов на каждое резюме.")}
            </span>
          </span>
        </label>
        {ctx.canManage && (
          <div className="flex items-center gap-3 sm:col-span-2">
            <button className={primary} disabled={!!busy} onClick={() => run("save", async () => {
              setS(await flowApi.saveAiSettings(s));
              setSaved(true);
            })}>
              {t("Сохранить")}
            </button>
            {saved && <span className="text-sm text-ok">{t("Сохранено.")}</span>}
          </div>
        )}
      </div>
      <ErrorText error={error} />
    </Card>
  );
}

// ---------- HH.uz ----------

export function HhView({ ctx }: { ctx: RecruitmentContext }) {
  const t = useT();
  const [s, setS] = useState<HhStatus | null>(null);
  const [form, setForm] = useState({ clientId: "", clientSecret: "", redirectUri: "", contactEmail: "", enabled: true, aiOnHhData: false });
  const [vacancies, setVacancies] = useState<HhVacancyLink[] | null>(null);
  const [own, setOwn] = useState<{ id: string; code: string; title: string }[]>([]);
  const [result, setResult] = useState<string | null>(null);
  const { busy, error, run } = useAction();
  const apply = (x: HhStatus) => {
    setS(x);
    setForm({ clientId: x.clientId ?? "", clientSecret: "", redirectUri: x.redirectUri ?? `${window.location.origin}/hr/recruitment/hh/callback`, contactEmail: x.contactEmail ?? "", enabled: x.enabled, aiOnHhData: x.aiOnHhData });
  };
  useEffect(() => {
    run("load", async () => apply(await hhApi.status()));
    import("@/lib/recruitment").then(({ recruitmentApi }) => recruitmentApi.vacancies().then((v) => setOwn(v.filter((x) => x.status !== "Closed")), () => undefined));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  if (!s) return error ? <ErrorText error={error} /> : <p className="text-sm text-ink-3">{t("Загрузка…")}</p>;

  return (
    <div className="space-y-4">
      <Card title="Подключение HH.uz">
        <p className="mb-3 text-sm text-ink-2">
          {s.connected
            ? t("Подключено: {name}. Отклики по связанным вакансиям загружаются раз в 15 минут.", { name: s.employerName ?? "—" })
            : t("Работодатель регистрирует приложение на dev.hh.ru и указывает в нём redirect_uri ниже. Список откликов на свои вакансии в API HH бесплатный; резюме через API не загружаются — просмотр расходует лимит HH.")}
        </p>
        {s.lastSyncAt && (
          <p className={`mb-3 text-sm ${s.lastSyncOk ? "text-ok" : "text-bad"}`}>
            {t("Последняя синхронизация {when}: {message}", { when: when(s.lastSyncAt), message: s.lastSyncMessage ?? "" })}
          </p>
        )}
        {ctx.canManage && (
          <div className="grid max-w-3xl gap-3 sm:grid-cols-2">
            <Label label="client_id">
              <input className={field} value={form.clientId} onChange={(e) => setForm({ ...form, clientId: e.target.value })} />
            </Label>
            <Label label="client_secret" hint={s.hasSecret ? "Сохранён; пусто — оставить прежний." : undefined}>
              <input className={field} type="password" autoComplete="off" value={form.clientSecret} onChange={(e) => setForm({ ...form, clientSecret: e.target.value })} />
            </Label>
            <Label label="redirect_uri" hint="Укажите этот адрес в настройках приложения на dev.hh.ru.">
              <input className={field} value={form.redirectUri} onChange={(e) => setForm({ ...form, redirectUri: e.target.value })} />
            </Label>
            <Label label="Почта разработчика" hint="HH требует её в заголовке запросов (HH-User-Agent).">
              <input className={field} type="email" value={form.contactEmail} onChange={(e) => setForm({ ...form, contactEmail: e.target.value })} />
            </Label>
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" className="size-4 accent-[var(--color-accent)]" checked={form.enabled} onChange={(e) => setForm({ ...form, enabled: e.target.checked })} />
              {t("Загружать отклики")}
            </label>
            <label className="flex items-start gap-2 text-sm sm:col-span-2">
              <input type="checkbox" className="mt-0.5 size-4 accent-[var(--color-accent)]" checked={form.aiOnHhData} onChange={(e) => setForm({ ...form, aiOnHhData: e.target.checked })} />
              <span>
                {t("Передавать AI резюме, полученные через API HH")}
                <span className="block text-xs text-ink-3">
                  {t("По умолчанию выключено: соглашение разработчика HH (п. 4.6) запрещает передавать данные, полученные через сервис, третьим сервисам. Включайте после подтверждения юриста. Резюме, загруженные файлом, — не про это.")}
                </span>
              </span>
            </label>
            <div className="flex flex-wrap gap-2 sm:col-span-2">
              <button className={button} disabled={!!busy} onClick={() => run("save", async () => apply(await hhApi.save({ ...form, clientSecret: form.clientSecret || null })))}>
                {t("Сохранить")}
              </button>
              {s.configured && (
                <button className={primary} disabled={!!busy} onClick={() => run("connect", async () => {
                  window.location.href = (await hhApi.connect()).url;
                })}>
                  {s.connected ? t("Переподключить HH") : t("Подключить HH")}
                </button>
              )}
              {s.connected && (
                <>
                  <button className={button} disabled={!!busy} onClick={() => run("sync", async () => {
                    setResult((await hhApi.sync()).message);
                    apply(await hhApi.status());
                  })}>
                    {busy === "sync" ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
                    {t("Загрузить отклики сейчас")}
                  </button>
                  <button className={button} disabled={!!busy} onClick={() => run("pause", async () => apply(await hhApi.save({ ...form, clientSecret: null, enabled: !s.enabled })))}>
                    {s.enabled ? t("Остановить синхронизацию") : t("Возобновить синхронизацию")}
                  </button>
                  <button className={button} disabled={!!busy} onClick={() => run("disconnect", async () => apply(await hhApi.disconnect()))}>
                    {t("Отключить")}
                  </button>
                </>
              )}
            </div>
          </div>
        )}
        {result && <p className="mt-2 text-sm text-ink-2">{t(result)}</p>}
        <ErrorText error={error} />
      </Card>

      {ctx.canManage && <HhGuardCard />}

      {s.connected && ctx.canManage && (
        <Card title="Вакансии работодателя на HH" actions={<button className={button} onClick={() => run("hhv", async () => setVacancies(await hhApi.vacancies()))}>{t("Показать")}</button>}>
          {vacancies && (
            <ul className="divide-y divide-line text-sm">
              {vacancies.map((v) => (
                <li key={v.hhVacancyId} className="flex flex-wrap items-center justify-between gap-2 py-2">
                  <span>
                    {v.name} {v.area && <span className="text-ink-3">· {v.area}</span>}
                  </span>
                  <select
                    className={`${field} w-72`}
                    value={v.vacancyId ?? ""}
                    onChange={(e) =>
                      run("link", async () => {
                        if (e.target.value) await hhApi.link(e.target.value, v.hhVacancyId);
                        else if (v.vacancyId) await hhApi.link(v.vacancyId, null);
                        setVacancies(await hhApi.vacancies());
                      })
                    }
                  >
                    <option value="">{t("Не связана")}</option>
                    {own.map((o) => (
                      <option key={o.id} value={o.id}>
                        {o.code} · {o.title}
                      </option>
                    ))}
                  </select>
                </li>
              ))}
            </ul>
          )}
        </Card>
      )}
    </div>
  );
}

/** Возврат с hh.ru после входа: code и state → завершение подключения. */
export function HhCallback() {
  const t = useT();
  const [state, setState] = useState<"busy" | "ok" | "error">("busy");
  const [message, setMessage] = useState<string | null>(null);
  useEffect(() => {
    const p = new URLSearchParams(window.location.search);
    const code = p.get("code");
    const st = p.get("state");
    if (!code || !st) {
      setState("error");
      setMessage(p.get("error_description") ?? p.get("error") ?? "HH не вернул код входа.");
      return;
    }

    hhApi.complete(code, st).then(
      (s) => {
        setState("ok");
        setMessage(s.employerName);
      },
      (e: Error) => {
        setState("error");
        setMessage(e.message);
      },
    );
  }, []);
  return (
    <Card title="HH.uz">
      {state === "busy" && <p className="text-sm text-ink-3">{t("Завершаем подключение…")}</p>}
      {state === "ok" && <p className="text-sm text-ok">{t("HH.uz подключён: {name}.", { name: message ?? "" })}</p>}
      {state === "error" && <p className="text-sm text-bad">{t(message ?? "Ошибка")}</p>}
      <Link href="/hr/recruitment/hh" className={`${button} mt-3`}>
        {t("К настройкам HH")}
      </Link>
    </Card>
  );
}

// ---------- Вакансия: миссия AI и интервьюеры ----------

export function VacancyExtras({ ctx, vacancy, onChange }: { ctx: RecruitmentContext; vacancy: Vacancy; onChange: (v: Vacancy) => void }) {
  const t = useT();
  const [criteria, setCriteria] = useState(vacancy.criteria ?? "");
  const [questions, setQuestions] = useState(vacancy.screeningQuestions ?? "");
  const [agentId, setAgentId] = useState(vacancy.agentId ?? "");
  const [recruiters, setRecruiters] = useState<RecruiterBrief[]>([]);
  const [people, setPeople] = useState<Person[]>([]);
  const { busy, error, run } = useAction();
  useEffect(() => {
    flowApi.people().then(setPeople, () => undefined);
    flowApi.recruiters().then(setRecruiters, () => undefined);
  }, [vacancy.id]);
  const save = (aiRecruiter: boolean) =>
    flowApi.vacancyAi(vacancy.id, { aiRecruiter, criteria: criteria || null, agentId: agentId || null, screeningQuestions: questions || null });
  const agent = recruiters.find((r) => r.id === agentId) ?? recruiters.find((r) => r.isDefault);
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <Card title="AI-рекрутер вакансии">
        <p className="text-sm">
          {!agent
            ? t("AI-рекрутера ещё нет — создайте его в «Настройки → ИИ-агенты».")
            : agent.enabled
              ? t("Кандидатам этой вакансии в Telegram отвечает AI-рекрутер «{name}».", { name: agent.name })
              : t("AI-рекрутер «{name}» выключен — кандидатам отвечает HR. Включить — в «Настройки → ИИ-агенты».", { name: agent.name })}
          <span className="block text-xs text-ink-3">
            {t("Рекрутер знает все открытые вакансии OneBase — описание, требования, условия и зарплату. Списком их не предлагает: спрашивает, по какой вакансии пишет кандидат, а если тот не знает — про опыт, и по нему предлагает подходящую. Зарплату называет, только если спросят. Кандидата записывает на ту вакансию, что он выбрал.")}
          </span>
        </p>
        {recruiters.length > 1 && (
          <Label
            label="Какой AI-рекрутер ведёт вакансию"
            hint="На номере Telegram, привязанном к агенту, всегда отвечает агент этого номера; этот выбор — для номеров без своего агента."
            className="mt-3"
          >
            <select className={field} disabled={!ctx.canManage} value={agentId} onChange={(e) => setAgentId(e.target.value)}>
              <option value="">{t("По умолчанию")}</option>
              {recruiters.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name}
                  {r.enabled ? "" : ` (${t("выключен")})`}
                </option>
              ))}
            </select>
          </Label>
        )}
        <Label
          label="Вопросы кандидатам этой вакансии"
          hint="По одному в строке, например «Есть ли права категории C?». Рекрутер задаёт их по одному, вместе с общими вопросами агента, и записывает ответы в карточку кандидата."
          className="mt-3"
        >
          <textarea className={textarea} disabled={!ctx.canManage} value={questions} onChange={(e) => setQuestions(e.target.value)} maxLength={2000} />
        </Label>
        <Label label="Критерии оценки" hint="По одному в строке. Пусто — опыт, навыки, образование, достижения, зарплатные ожидания, соответствие требованиям." className="mt-3">
          <textarea className={textarea} disabled={!ctx.canManage} value={criteria} onChange={(e) => setCriteria(e.target.value)} />
        </Label>
        {ctx.canManage && (
          <button className={`${button} mt-2`} disabled={!!busy} onClick={() => run("criteria", async () => onChange(await save(vacancy.aiRecruiter)))}>
            {t("Сохранить")}
          </button>
        )}
        <ErrorText error={error} />
      </Card>
      <Card title="Собеседования" className="lg:col-span-2">
        <InterviewSettingsCard ctx={ctx} vacancyId={vacancy.id} people={people} />
      </Card>
    </div>
  );
}

// ---------- Кандидат: AI-анализ (AI-скрининг), собеседования, отклик HH ----------

export function CandidateExtras({ ctx, card }: { ctx: RecruitmentContext; card: CandidateCard }) {
  const t = useT();
  const [interviews, setInterviews] = useState<InterviewRow[]>([]);
  const [negotiations, setNegotiations] = useState<HhNegotiation[]>([]);
  const [slots, setSlots] = useState<Record<string, Slot[]>>({});
  const [confirmed, setConfirmed] = useState(false);
  const { busy, error, run } = useAction();
  useEffect(() => {
    flowApi.candidateInterviews(card.id).then(setInterviews, () => undefined);
    hhApi.negotiations(card.id).then(setNegotiations, () => undefined);
  }, [card.id]);
  const open = card.applications.filter((a) => !["Hired", "Rejected", "Withdrawn"].includes(a.stage));

  return (
    <>
      <CandidateScreening candidateId={card.id} canManage={ctx.canManage} hasResume={card.resumes.some((r) => r.hasText)} />

      <Card title="Собеседования">
        {interviews.map((i) => (
          <p key={i.id} className="text-sm">
            {i.vacancyCode}: {t(interviewStatusLabel[i.status] ?? i.status)}
            {i.startsAt && ` · ${slotText(i.startsAt)}`}
            {i.location && ` · ${i.location}`}
            {["Awaiting", "Confirmed", "Rescheduled"].includes(i.status) && <span className="text-xs text-ink-3"> · {t(confirmationLabel[i.candidateConfirmation])}</span>}
          </p>
        ))}
        {interviews.length > 0 && (
          <Link href="/hr/recruitment/interviews" className="text-xs text-accent-strong hover:underline">
            {t("Перенести, отменить, итоги — в «Собеседованиях»")}
          </Link>
        )}
        {ctx.canManage &&
          open.map((a) => (
            <div key={a.id} className="mt-2 flex flex-wrap items-center gap-2 text-sm">
              <span>{a.vacancyCode}:</span>
              <button className={button} disabled={!!busy} onClick={() => run("propose", async () => setInterviews(await flowApi.propose(a.id)))}>
                {t("Предложить кандидату время")}
              </button>
              <button className={button} onClick={() => run("slots", async () => setSlots({ ...slots, [a.id]: await flowApi.slots(a.vacancyId) }))}>
                {t("Назначить самому")}
              </button>
              {slots[a.id] && (
                <label className="flex items-center gap-1 text-xs text-ink-2">
                  <input type="checkbox" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} />
                  {t("кандидат уже подтвердил (по телефону)")}
                </label>
              )}
              {slots[a.id]?.map((s) => (
                <button key={s.startsAt} className={primary} disabled={!!busy} onClick={() => run("book", async () => setInterviews(await flowApi.book(a.id, s.startsAt, null, null, confirmed)))}>
                  {slotText(s.startsAt)}
                </button>
              ))}
            </div>
          ))}
        <ErrorText error={error} />
      </Card>

      {negotiations.length > 0 && (
        <Card title="Отклик на HH.uz">
          {negotiations.map((n) => (
            <div key={n.id} className="text-sm">
              <span className="text-ink-2">
                {t("Состояние на HH")}: {n.state ?? "—"} · {when(n.hhUpdatedAt)}
              </span>
              {n.lastError && <p className="text-xs text-warn">{t(n.lastError)}</p>}
              {ctx.canManage && (
                <div className="mt-2 flex flex-wrap gap-2">
                  {n.actions.map((a) => (
                    <button key={a.id} className={button} disabled={!!busy} onClick={() => run("hh", async () => setNegotiations(await hhApi.action(n.id, a.id, null)))}>
                      {a.name}
                    </button>
                  ))}
                </div>
              )}
            </div>
          ))}
        </Card>
      )}
    </>
  );
}

export const stageName = (s: Stage) => stageLabel[s];
