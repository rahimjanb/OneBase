"use client";

import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";
import { Bot, Loader2, RefreshCw, Send, Sparkles, UserRound } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Badge, Card, ErrorText, Label, textarea, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { recruitmentApi, when, type RecruitmentContext, type VacancyRow } from "@/lib/recruitment";
import {
  flowApi,
  outboxLabel,
  recruitmentTelegramApi as api,
  sourceCategoryLabel,
  type HrAccount,
  type HrConversation,
  type HrConversationRow,
  type Lead,
  type Publication,
  type PublicationChat,
  type Source,
} from "@/lib/recruitment-flow";

function AccountsBar({ accounts }: { accounts: HrAccount[] }) {
  const t = useT();
  if (accounts.length === 0) {
    return (
      <p className="rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">
        {t("Номер HR не подключён. Администратор подключает его в «Настройки → Telegram» и выбирает «Подразделения: HR».")}{" "}
        <Link className="underline" href="/settings/telegram">
          {t("Настройки → Telegram")}
        </Link>
      </p>
    );
  }

  return (
    <div className="flex flex-wrap gap-2">
      {accounts.map((a) => (
        <span key={a.id} className="rounded-lg border border-line bg-surface px-3 py-1.5 text-xs text-ink-2">
          <b className="text-ink">{a.name}</b>
          {a.username && ` @${a.username}`} ·{" "}
          {a.status !== "Connected" ? (
            <span className="text-bad">{t("не подключён")}</span>
          ) : a.pausedUntil && new Date(a.pausedUntil) > new Date() ? (
            <span className="text-bad">{t("отправка остановлена до {date}", { date: when(a.pausedUntil) })}</span>
          ) : (
            <span className={a.online ? "text-ok" : "text-warn"}>{a.online ? t("работает") : t("клиент не запущен")}</span>
          )}{" "}
          · {t("за сутки {n} из {limit}", { n: a.sentToday, limit: a.dailyLimit })}
        </span>
      ))}
    </div>
  );
}

/** Диалоги HR с кандидатами: список, история, ответ через очередь (пауза и лимит номера), AI или человек. */
export function TelegramDialogsView({ ctx }: { ctx: RecruitmentContext }) {
  const t = useT();
  const [accounts, setAccounts] = useState<HrAccount[]>([]);
  const [rows, setRows] = useState<HrConversationRow[] | null>(null);
  const [search, setSearch] = useState("");
  const [open, setOpen] = useState<HrConversation | null>(null);
  const [text, setText] = useState("");
  const keyRef = useRef<string>(crypto.randomUUID());
  const bottom = useRef<HTMLDivElement>(null);
  const { busy, error, run } = useAction();

  const load = () =>
    run("load", async () => {
      setAccounts(await api.accounts());
      setRows(await api.conversations(undefined, search));
    });
  useEffect(() => {
    const timer = setTimeout(load, 250);
    return () => clearTimeout(timer);
  }, [search]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    if (!open) return;
    const timer = setInterval(() => api.conversation(open.id).then(setOpen, () => undefined), 10_000);
    return () => clearInterval(timer);
  }, [open?.id]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => bottom.current?.scrollIntoView({ block: "end" }), [open?.messages.length]);

  const select = (id: string) => run("open", async () => setOpen(await api.conversation(id)));

  return (
    <div className="space-y-3">
      <AccountsBar accounts={accounts} />
      <div className="grid min-h-[60vh] gap-3 lg:grid-cols-[320px_1fr]">
        <div className="rounded-xl border border-line bg-surface p-2">
          <div className="mb-2 flex gap-2">
            <input className={field} placeholder={t("Поиск: имя или @username")} value={search} onChange={(e) => setSearch(e.target.value)} />
            <button className={button} title={t("Обновить")} onClick={load}>
              <RefreshCw className="size-4" />
            </button>
          </div>
          {rows === null ? (
            <p className="p-2 text-sm text-ink-3">{t("Загрузка…")}</p>
          ) : rows.length === 0 ? (
            <p className="p-2 text-sm text-ink-3">{t("Диалогов пока нет: кандидаты появятся, когда напишут на номер HR.")}</p>
          ) : (
            <ul className="max-h-[70vh] space-y-1 overflow-y-auto">
              {rows.map((r) => (
                <li key={r.id}>
                  <button
                    className={`w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-muted ${open?.id === r.id ? "bg-accent-soft" : ""}`}
                    onClick={() => select(r.id)}
                  >
                    <span className="flex items-center justify-between gap-2">
                      <span className="truncate font-medium text-ink">{r.name}</span>
                      {r.mode === "Ai" ? <Bot className="size-4 shrink-0 text-accent" aria-label={t("ведёт AI")} /> : <UserRound className="size-4 shrink-0 text-ink-3" aria-label={t("ведёт человек")} />}
                    </span>
                    <span className="block truncate text-xs text-ink-3">{r.lastText ?? "—"}</span>
                    <span className="block text-[11px] text-ink-3">{when(r.lastMessageAt)}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="flex min-h-[60vh] flex-col rounded-xl border border-line bg-surface">
          {!open ? (
            <p className="m-auto p-6 text-sm text-ink-3">{t("Выберите диалог слева.")}</p>
          ) : (
            <>
              <div className="flex flex-wrap items-center justify-between gap-2 border-b border-line px-4 py-2">
                <div>
                  <span className="font-semibold text-ink">{open.name}</span>
                  {open.username && <span className="ml-1 text-sm text-ink-3">@{open.username}</span>}
                  <span className="ml-2 text-xs text-ink-3">{open.accountName}</span>
                  {open.candidateId && (
                    <Link href={`/hr/recruitment/candidates/${open.candidateId}`} className="ml-2 text-xs text-accent-strong hover:underline">
                      {t("карточка кандидата")}
                    </Link>
                  )}
                </div>
                {ctx.canManage && (
                  <button className={button} disabled={!!busy} onClick={() => run("mode", async () => setOpen(await api.mode(open.id, open.mode === "Ai" ? "Manual" : "Ai")))}>
                    {open.mode === "Ai" ? <UserRound className="size-4" /> : <Bot className="size-4" />}
                    {open.mode === "Ai" ? t("Взять на себя (AI не отвечает)") : t("Вернуть AI")}
                  </button>
                )}
              </div>
              <div className="flex-1 space-y-2 overflow-y-auto p-4">
                {open.messages.map((m, i) => (
                  <div key={`${m.id ?? "p"}-${i}`} className={`flex ${m.direction === "In" ? "justify-start" : "justify-end"}`}>
                    <div className={`max-w-[80%] rounded-2xl px-3 py-2 text-sm ${m.direction === "In" ? "bg-muted text-ink" : "bg-accent-soft text-ink"}`}>
                      {m.text && <p className="whitespace-pre-line">{m.text}</p>}
                      {m.fileName && <p className="text-xs text-ink-2">📎 {m.fileName}{m.resumeId && ` · ${t("сохранено как резюме")}`}</p>}
                      <p className="mt-1 text-[11px] text-ink-3">
                        {m.direction === "Out" && `${m.author === "ai" ? "AI" : m.author === "phone" ? t("с телефона") : m.authorName ?? t("сотрудник")} · `}
                        {when(m.at)}
                        {m.status !== "Sent" && <span className={m.status === "Pending" ? "" : "text-bad"}> · {t(outboxLabel[m.status] ?? m.status)}</span>}
                        {m.error && <span className="text-bad"> · {t(m.error)}</span>}
                      </p>
                    </div>
                  </div>
                ))}
                <div ref={bottom} />
              </div>
              {ctx.canManage && (
                <form
                  className="flex gap-2 border-t border-line p-3"
                  onSubmit={(e) => {
                    e.preventDefault();
                    run("send", async () => {
                      setOpen(await api.send(open.id, text, keyRef.current));
                      setText("");
                      keyRef.current = crypto.randomUUID();
                    });
                  }}
                >
                  <textarea className={`${textarea} min-h-10`} rows={2} value={text} onChange={(e) => setText(e.target.value)} placeholder={t("Ответ кандидату…")} maxLength={4096} />
                  <button type="submit" className={primary} disabled={!!busy || !text.trim()}>
                    {busy === "send" ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
                  </button>
                </form>
              )}
            </>
          )}
          <div className="px-4 pb-2">
            <ErrorText error={error} />
            {ctx.canManage && open?.mode === "Ai" && <p className="text-xs text-ink-3">{t("После вашего сообщения AI в этом диалоге выключается — вернуть его можно кнопкой.")}</p>}
          </div>
        </div>
      </div>
    </div>
  );
}

/** Публикации вакансий в группы и каналы номера HR, источники поиска и лиды «ищу работу». */
export function PublicationsView({ ctx }: { ctx: RecruitmentContext }) {
  const t = useT();
  const [accounts, setAccounts] = useState<HrAccount[]>([]);
  const [vacancies, setVacancies] = useState<VacancyRow[]>([]);
  const [accountId, setAccountId] = useState("");
  const [vacancyId, setVacancyId] = useState("");
  const [chats, setChats] = useState<PublicationChat[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  const [text, setText] = useState("");
  const [audience, setAudience] = useState("jobs");
  const [scheduledAt, setScheduledAt] = useState("");
  const [publications, setPublications] = useState<Publication[]>([]);
  const [sources, setSources] = useState<Source[]>([]);
  const [leads, setLeads] = useState<Lead[]>([]);
  const [source, setSource] = useState({ chatId: "", category: "jobs" });
  const { busy, error, run } = useAction();

  useEffect(() => {
    run("load", async () => {
      const list = await api.accounts();
      setAccounts(list);
      setAccountId(list[0]?.id ?? "");
      setVacancies((await recruitmentApi.vacancies("Open")) ?? []);
      setPublications(await api.publications());
      setSources(await api.sources());
      setLeads(await api.leads("New"));
    });
  }, []); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    if (accountId) api.chats(accountId).then(setChats, () => setChats([]));
  }, [accountId]);
  useEffect(() => {
    if (vacancyId && accountId) api.template(vacancyId, accountId).then((r) => setText(r.text), () => undefined);
  }, [vacancyId, accountId]);

  const groups = useMemo(() => chats.filter((c) => c.type !== "Private"), [chats]);

  return (
    <div className="space-y-4">
      {accounts.length === 0 && <AccountsBar accounts={accounts} />}
      {ctx.canManage && accounts.length > 0 && (
        <Card title="Опубликовать вакансию">
          <div className="grid gap-3 lg:grid-cols-2">
            <div className="space-y-3">
              <Label label="Номер HR">
                <select className={field} value={accountId} onChange={(e) => setAccountId(e.target.value)}>
                  {accounts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name} {a.username && `@${a.username}`}
                    </option>
                  ))}
                </select>
              </Label>
              <Label label="Вакансия (открытая)">
                <select className={field} value={vacancyId} onChange={(e) => setVacancyId(e.target.value)}>
                  <option value="">—</option>
                  {vacancies.map((v) => (
                    <option key={v.id} value={v.id}>
                      {v.code} · {v.title}
                    </option>
                  ))}
                </select>
              </Label>
              <div>
                <span className="mb-1 block text-xs font-medium text-ink-2">{t("Группы и каналы")}</span>
                <ul className="max-h-56 space-y-1 overflow-y-auto rounded-lg border border-line p-2 text-sm">
                  {groups.length === 0 && <li className="text-ink-3">{t("Номер HR не состоит ни в одной группе или канале.")}</li>}
                  {groups.map((c) => (
                    <li key={c.id}>
                      <label className={`flex items-center gap-2 ${c.canSend ? "" : "text-ink-3"}`}>
                        <input
                          type="checkbox"
                          className="size-4 accent-[var(--color-accent)]"
                          disabled={!c.canSend}
                          checked={selected.includes(c.id)}
                          onChange={(e) => setSelected(e.target.checked ? [...selected, c.id] : selected.filter((x) => x !== c.id))}
                        />
                        {c.title} {c.username && <span className="text-xs text-ink-3">@{c.username}</span>}
                        {!c.canSend && <span className="text-xs">— {t(c.reason ?? "писать нельзя")}</span>}
                      </label>
                    </li>
                  ))}
                </ul>
              </div>
              <Label label="Когда" hint="Пусто — сразу. Время Ташкента.">
                <input className={field} type="datetime-local" value={scheduledAt} onChange={(e) => setScheduledAt(e.target.value)} />
              </Label>
            </div>
            <div className="space-y-2">
              <Label label="Текст объявления" hint="Код вакансии в конце нужен, чтобы ответы попадали к этой вакансии.">
                <textarea className={`${textarea} min-h-64`} value={text} onChange={(e) => setText(e.target.value)} maxLength={4096} />
              </Label>
              <div className="flex flex-wrap items-center gap-2">
                <select className={`${field} w-56`} value={audience} onChange={(e) => setAudience(e.target.value)}>
                  {Object.entries(sourceCategoryLabel)
                    .filter(([k]) => k !== "other")
                    .map(([k, label]) => (
                      <option key={k} value={k}>
                        {t(label)}
                      </option>
                    ))}
                </select>
                <button
                  className={button}
                  disabled={!!busy || !vacancyId}
                  onClick={() => run("ai", async () => setText((await flowApi.postText(vacancyId, accountId, audience === "jobs" ? "jobs" : audience)).text))}
                >
                  {busy === "ai" ? <Loader2 className="size-4 animate-spin" /> : <Sparkles className="size-4" />}
                  {t("Переписать AI под аудиторию")}
                </button>
              </div>
              <button
                className={primary}
                disabled={!!busy || !vacancyId || selected.length === 0 || !text.trim()}
                onClick={() =>
                  run("publish", async () => {
                    const at = scheduledAt ? new Date(`${scheduledAt}:00+05:00`).toISOString() : null;
                    await api.publish({ vacancyId, accountId, chatIds: selected, text, scheduledAt: at });
                    setPublications(await api.publications());
                    setSelected([]);
                  })
                }
              >
                <Send className="size-4" />
                {scheduledAt ? t("Запланировать") : t("Опубликовать")}
              </button>
            </div>
          </div>
          <ErrorText error={error} />
        </Card>
      )}

      <Card title="Публикации">
        {publications.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Публикаций пока нет.")}</p>
        ) : (
          <ul className="divide-y divide-line text-sm">
            {publications.map((p) => (
              <li key={p.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
                <span>
                  <b>{p.vacancyCode}</b> → {p.chatTitle} · {when(p.scheduledAt)}
                  {p.error && <span className="text-bad"> · {t(p.error)}</span>}
                </span>
                <span className="flex items-center gap-2">
                  <Badge className={p.status === "Sent" ? "bg-ok-soft text-ok" : p.status === "Failed" ? "bg-bad-soft text-bad" : "bg-muted text-ink-2"}>{t(p.status)}</Badge>
                  {p.link && (
                    <a href={p.link} target="_blank" rel="noreferrer" className="text-accent-strong hover:underline">
                      {t("открыть")}
                    </a>
                  )}
                  {ctx.canManage && p.status === "Scheduled" && (
                    <button className={button} onClick={() => run("cancel", async () => setPublications(await api.cancelPublication(p.id)))}>
                      {t("Отменить")}
                    </button>
                  )}
                </span>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card title="Источники поиска">
          <p className="mb-2 text-xs text-ink-3">
            {t("Группы, где состоит номер HR: посты «ищу работу» становятся лидами. Участники групп не выгружаются, незнакомым OneBase сам не пишет.")}
          </p>
          <ul className="space-y-1 text-sm">
            {sources.map((s) => (
              <li key={s.id} className="flex items-center justify-between gap-2">
                <span>
                  {s.title} · {t(sourceCategoryLabel[s.category] ?? s.category)} · {t("лидов: {n}", { n: s.leads })}
                </span>
                {ctx.canManage && (
                  <button className="text-xs text-ink-3 hover:text-bad" onClick={() => run("source", async () => setSources(await api.removeSource(s.id)))}>
                    {t("убрать")}
                  </button>
                )}
              </li>
            ))}
          </ul>
          {ctx.canManage && accountId && (
            <div className="mt-3 flex flex-wrap gap-2">
              <select className={`${field} w-56`} value={source.chatId} onChange={(e) => setSource({ ...source, chatId: e.target.value })}>
                <option value="">{t("Группа или канал…")}</option>
                {groups.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.title}
                  </option>
                ))}
              </select>
              <select className={`${field} w-48`} value={source.category} onChange={(e) => setSource({ ...source, category: e.target.value })}>
                {Object.entries(sourceCategoryLabel).map(([k, label]) => (
                  <option key={k} value={k}>
                    {t(label)}
                  </option>
                ))}
              </select>
              <button
                className={button}
                disabled={!!busy || !source.chatId}
                onClick={() => run("source", async () => setSources(await api.addSource({ accountId, chatId: source.chatId, category: source.category, collectLeads: true, note: null })))}
              >
                {t("Добавить")}
              </button>
            </div>
          )}
        </Card>

        <Card title="Лиды из групп">
          {leads.length === 0 ? (
            <p className="text-sm text-ink-3">{t("Новых лидов нет.")}</p>
          ) : (
            <ul className="space-y-2 text-sm">
              {leads.map((l) => (
                <li key={l.id} className="rounded-lg border border-line p-2">
                  <div className="text-xs text-ink-3">
                    {l.chatTitle} · {when(l.createdAt)} · {l.keywords}
                  </div>
                  <p className="mt-1 whitespace-pre-line">{l.excerpt}</p>
                  <div className="mt-1 flex flex-wrap items-center gap-2 text-xs">
                    <span>
                      {l.fromName ?? "—"}{" "}
                      {l.fromUsername && (
                        <a className="text-accent-strong hover:underline" href={`https://t.me/${l.fromUsername}`} target="_blank" rel="noreferrer">
                          @{l.fromUsername}
                        </a>
                      )}
                    </span>
                    {ctx.canManage && (
                      <>
                        <button
                          className={button}
                          onClick={() =>
                            run("lead", async () => {
                              const r = await api.convertLead(l.id, vacancyId || null);
                              window.location.href = `/hr/recruitment/candidates/${r.candidateId}`;
                            })
                          }
                        >
                          {t("Завести кандидата")}
                        </button>
                        <button className={button} onClick={() => run("lead", async () => setLeads(await api.ignoreLead(l.id)))}>
                          {t("Не подходит")}
                        </button>
                      </>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
          <p className="mt-2 text-xs text-ink-3">{t("Написать человеку — ваше решение: откройте его @username в Telegram. Лиды без решения удаляются по сроку хранения.")}</p>
        </Card>
      </div>
    </div>
  );
}
