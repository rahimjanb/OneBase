"use client";

import { useEffect, useState } from "react";
import { Bot, Loader2, RefreshCw, Send, ThumbsDown, ThumbsUp, Trash2, User } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Badge, Card, ErrorText, Label, textarea, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import {
  actionLabel,
  agentsApi,
  replyStatusLabel,
  type AgentConversation,
  type AgentConversationRow,
  type AgentRepliesPage,
  type AgentReply,
  type AgentTestMessage,
  type AgentTestResult,
  type AgentView,
} from "@/lib/agents";
import { when } from "@/lib/recruitment";

// ---------- Готовые ответы ----------

/** Готовые ответы: утверждённые факты и формулировки — на похожий вопрос агент отвечает по их смыслу. */
export function AgentAnswers({ agent, onChange }: { agent: AgentView; onChange: (a: AgentView) => void }) {
  const t = useT();
  const [draft, setDraft] = useState({ question: "", answer: "" });
  const [editing, setEditing] = useState<{ id: string; question: string; answer: string; enabled: boolean } | null>(null);
  const { busy, error, run } = useAction();
  return (
    <Card title="Готовые ответы">
      <p className="mb-3 max-w-3xl text-xs text-ink-3">
        {t("На похожий вопрос агент отвечает по смыслу готового ответа, своими словами, не меняя фактов. Сюда же попадают исправления из вкладки «Ответы».")}
      </p>
      <ul className="space-y-2">
        {agent.answers.map((a) =>
          editing?.id === a.id ? (
            <li key={a.id} className="space-y-2 rounded-lg border border-accent/40 p-3">
              <input className={field} value={editing.question} maxLength={500} onChange={(e) => setEditing({ ...editing, question: e.target.value })} />
              <textarea className={textarea} value={editing.answer} maxLength={2000} onChange={(e) => setEditing({ ...editing, answer: e.target.value })} />
              <div className="flex flex-wrap items-center gap-2">
                <label className="flex items-center gap-1 text-sm">
                  <input type="checkbox" className="size-4 accent-[var(--color-accent)]" checked={editing.enabled} onChange={(e) => setEditing({ ...editing, enabled: e.target.checked })} />
                  {t("Действует")}
                </label>
                <button
                  className={primary}
                  disabled={!!busy}
                  onClick={() =>
                    run("save", async () => {
                      onChange(await agentsApi.updateAnswer(agent.id, a.id, editing));
                      setEditing(null);
                    })
                  }
                >
                  {t("Сохранить")}
                </button>
                <button className={button} onClick={() => setEditing(null)}>
                  {t("Отмена")}
                </button>
              </div>
            </li>
          ) : (
            <li key={a.id} className={`rounded-lg border border-line p-3 text-sm ${a.enabled ? "" : "opacity-60"}`}>
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <p className="font-medium text-ink">{a.question}</p>
                  <p className="mt-1 whitespace-pre-line text-ink-2">{a.answer}</p>
                  {!a.enabled && <Badge className="mt-1">{t("выключен")}</Badge>}
                </div>
                <div className="flex shrink-0 gap-1">
                  <button className="text-xs text-accent-strong hover:underline" onClick={() => setEditing({ ...a })}>
                    {t("Изменить")}
                  </button>
                  <button className="text-ink-3 hover:text-bad" title={t("Удалить")} onClick={() => run("delete", async () => onChange(await agentsApi.deleteAnswer(agent.id, a.id)))}>
                    <Trash2 className="size-4" />
                  </button>
                </div>
              </div>
            </li>
          ),
        )}
      </ul>
      <div className="mt-4 grid max-w-3xl gap-2 rounded-lg border border-dashed border-line p-3">
        <Label label="Вопрос (как его обычно задают)">
          <input className={field} value={draft.question} maxLength={500} onChange={(e) => setDraft({ ...draft, question: e.target.value })} />
        </Label>
        <Label label="Ответ">
          <textarea className={textarea} value={draft.answer} maxLength={2000} onChange={(e) => setDraft({ ...draft, answer: e.target.value })} />
        </Label>
        <div>
          <button
            className={primary}
            disabled={!!busy || !draft.question.trim() || !draft.answer.trim()}
            onClick={() =>
              run("add", async () => {
                onChange(await agentsApi.addAnswer(agent.id, { ...draft, enabled: true }));
                setDraft({ question: "", answer: "" });
              })
            }
          >
            {t("Добавить ответ")}
          </button>
        </div>
      </div>
      <ErrorText error={error} />
    </Card>
  );
}

// ---------- Журнал ответов ----------

const FILTERS = [
  { key: "", label: "Все" },
  { key: "drafts", label: "Ждут проверки" },
  { key: "blocked", label: "Остановлены" },
  { key: "bad", label: "Плохие" },
];

/** Журнал ответов агента: черновики на проверку (одобрить с правкой, отклонить), остановленные ограничением, оценка и «как надо было». */
export function AgentReplies({ agent }: { agent: AgentView }) {
  const t = useT();
  const [filter, setFilter] = useState(agent.drafts > 0 ? "drafts" : "");
  const [page, setPage] = useState<AgentRepliesPage | null>(null);
  const { busy, error, run } = useAction();
  const load = (f = filter) => run("load", async () => setPage(await agentsApi.replies(agent.id, f)));
  useEffect(() => {
    load(filter);
  }, [filter]); // eslint-disable-line react-hooks/exhaustive-deps
  const update = (p: AgentRepliesPage) => run("load", async () => setPage(filter ? await agentsApi.replies(agent.id, filter) : p));

  return (
    <Card
      title="Ответы агента"
      actions={
        <button className={button} onClick={() => load()} disabled={!!busy}>
          <RefreshCw className={`size-4 ${busy === "load" ? "animate-spin" : ""}`} /> {t("Обновить")}
        </button>
      }
    >
      <div className="mb-3 flex flex-wrap gap-2">
        {FILTERS.map((f) => (
          <button
            key={f.key}
            onClick={() => setFilter(f.key)}
            className={`rounded-full border px-3 py-1 text-xs ${filter === f.key ? "border-accent bg-accent-soft text-accent-strong" : "border-line text-ink-2"}`}
          >
            {t(f.label)}
            {f.key === "drafts" && page?.drafts ? ` · ${page.drafts}` : ""}
            {f.key === "blocked" && page?.blocked ? ` · ${page.blocked}` : ""}
            {f.key === "bad" && page?.bad ? ` · ${page.bad}` : ""}
          </button>
        ))}
      </div>
      {page && page.items.length === 0 && <p className="text-sm text-ink-3">{t("Ответов нет.")}</p>}
      <ul className="space-y-3">
        {page?.items.map((r) => <ReplyItem key={r.id} reply={r} busy={!!busy} onDone={update} run={run} />)}
      </ul>
      <ErrorText error={error} />
    </Card>
  );
}

function ReplyItem({
  reply: r,
  busy,
  onDone,
  run,
}: {
  reply: AgentReply;
  busy: boolean;
  onDone: (p: AgentRepliesPage) => void;
  run: (what: string, action: () => Promise<void>) => Promise<void>;
}) {
  const t = useT();
  const pending = r.status === "Draft" || r.status === "Blocked";
  const [text, setText] = useState(r.text);
  const [correcting, setCorrecting] = useState(false);
  const [correction, setCorrection] = useState(r.correction ?? "");
  const [toAnswers, setToAnswers] = useState(true);
  const tone =
    r.status === "Draft" ? "bg-warn-soft text-warn" : r.status === "Blocked" ? "bg-bad-soft text-bad" : r.status === "Rejected" || r.status === "Superseded" ? "bg-muted text-ink-3" : "bg-ok-soft text-ok";
  return (
    <li className={`rounded-lg border p-3 text-sm ${pending ? "border-warn/40" : "border-line"}`}>
      <div className="flex flex-wrap items-center gap-2 text-xs text-ink-3">
        <span className="font-medium text-ink-2">{r.peerName ?? t("Собеседник")}</span>
        <span>{when(r.createdAt)}</span>
        <Badge className={tone}>{t(replyStatusLabel[r.status])}</Badge>
        <Badge>{t(actionLabel[r.action] ?? r.action)}</Badge>
        {r.rating === "good" && <ThumbsUp className="size-3.5 text-ok" />}
        {r.rating === "bad" && <ThumbsDown className="size-3.5 text-bad" />}
        {r.reviewedBy && <span>· {r.reviewedBy}</span>}
      </div>
      {r.incoming && (
        <p className="mt-2 flex gap-2 text-ink-2">
          <User className="mt-0.5 size-4 shrink-0 text-ink-3" />
          <span className="whitespace-pre-line">{r.incoming}</span>
        </p>
      )}
      {pending ? (
        <div className="mt-2">
          {r.blockReason && <p className="mb-1 text-xs text-bad">{t(r.blockReason)}</p>}
          <textarea className={textarea} value={text} maxLength={4000} onChange={(e) => setText(e.target.value)} />
          <p className="mt-1 text-xs text-ink-3">{t("Пустая строка между абзацами — отдельные сообщения.")}</p>
          <div className="mt-2 flex flex-wrap gap-2">
            <button className={primary} disabled={busy || !text.trim()} onClick={() => run("approve", async () => onDone(await agentsApi.approve(r.id, text === r.text ? null : text)))}>
              <Send className="size-4" /> {t(text === r.text ? "Отправить" : "Отправить с правкой")}
            </button>
            <button className={button} disabled={busy} onClick={() => run("reject", async () => onDone(await agentsApi.reject(r.id)))}>
              {t("Не отправлять")}
            </button>
          </div>
        </div>
      ) : (
        <div className="mt-2 flex gap-2 text-ink">
          <Bot className="mt-0.5 size-4 shrink-0 text-accent-strong" />
          <span className="whitespace-pre-line">{r.text}</span>
        </div>
      )}
      {r.correction && !correcting && (
        <p className="mt-2 rounded-lg bg-ok-soft/50 px-2 py-1 text-xs text-ink-2">
          {t("Как надо было")}: {r.correction}
        </p>
      )}
      {!pending && (
        <div className="mt-2 flex flex-wrap items-center gap-3 text-xs">
          <button className="flex items-center gap-1 text-ink-3 hover:text-ok" disabled={busy} onClick={() => run("rate", async () => onDone(await agentsApi.rate(r.id, "good", r.correction, false)))}>
            <ThumbsUp className="size-3.5" /> {t("Хороший ответ")}
          </button>
          <button className="flex items-center gap-1 text-ink-3 hover:text-bad" onClick={() => setCorrecting(true)}>
            <ThumbsDown className="size-3.5" /> {t("Плохой — исправить")}
          </button>
        </div>
      )}
      {correcting && (
        <div className="mt-2 space-y-2 rounded-lg border border-line p-2">
          <Label label="Как надо было ответить">
            <textarea className={textarea} value={correction} maxLength={2000} onChange={(e) => setCorrection(e.target.value)} />
          </Label>
          <label className="flex items-center gap-2 text-xs">
            <input type="checkbox" className="size-4 accent-[var(--color-accent)]" checked={toAnswers} onChange={(e) => setToAnswers(e.target.checked)} />
            {t("Добавить в готовые ответы — дальше агент будет отвечать так")}
          </label>
          <div className="flex gap-2">
            <button
              className={primary}
              disabled={busy}
              onClick={() =>
                run("rate", async () => {
                  onDone(await agentsApi.rate(r.id, "bad", correction || null, toAnswers && !!correction.trim()));
                  setCorrecting(false);
                })
              }
            >
              {t("Сохранить")}
            </button>
            <button className={button} onClick={() => setCorrecting(false)}>
              {t("Отмена")}
            </button>
          </div>
        </div>
      )}
    </li>
  );
}

// ---------- Диалоги отдела ----------

/** Диалоги отдела в Telegram: история, ответ сотрудника (AI в диалоге замолкает), «Вернуть AI». */
export function AgentDialogs({ agent }: { agent: AgentView }) {
  const t = useT();
  const [rows, setRows] = useState<AgentConversationRow[] | null>(null);
  const [open, setOpen] = useState<AgentConversation | null>(null);
  const [reply, setReply] = useState("");
  const { busy, error, run } = useAction();
  useEffect(() => {
    run("load", async () => setRows(await agentsApi.conversations(agent.id)));
  }, [agent.id]); // eslint-disable-line react-hooks/exhaustive-deps
  const select = (id: string) => run("open", async () => setOpen(await agentsApi.conversation(agent.id, id)));

  return (
    <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,2fr)]">
      <Card title="Диалоги отдела">
        {rows && rows.length === 0 && <p className="text-sm text-ink-3">{t("Диалогов пока нет.")}</p>}
        <ul className="divide-y divide-line">
          {rows?.map((r) => (
            <li key={r.id}>
              <button className={`w-full py-2 text-left text-sm ${open?.id === r.id ? "text-accent-strong" : ""}`} onClick={() => select(r.id)}>
                <span className="flex items-center justify-between gap-2">
                  <span className="truncate font-medium">{r.name}</span>
                  <span className="flex shrink-0 gap-1">
                    {r.waitsForAnswer && <Badge className="bg-warn-soft text-warn">{t("ждёт ответа")}</Badge>}
                    <Badge className={r.mode === "Ai" ? "bg-accent-soft text-accent-strong" : ""}>{r.mode === "Ai" ? "AI" : t("сотрудник")}</Badge>
                  </span>
                </span>
                <span className="block truncate text-xs text-ink-3">{r.lastText ?? "—"}</span>
              </button>
            </li>
          ))}
        </ul>
      </Card>
      <Card
        title={open ? open.name : "Диалог"}
        actions={
          open && (
            <button
              className={button}
              disabled={!!busy}
              onClick={() => run("mode", async () => setOpen(await agentsApi.mode(agent.id, open.id, open.mode === "Ai" ? "Manual" : "Ai")))}
            >
              {open.mode === "Ai" ? t("Взять на себя (AI не отвечает)") : t("Вернуть AI")}
            </button>
          )
        }
      >
        {!open ? (
          <p className="text-sm text-ink-3">{t("Выберите диалог слева.")}</p>
        ) : (
          <>
            <ul className="max-h-[28rem] space-y-2 overflow-y-auto">
              {open.messages.map((m, i) => (
                <li key={i} className={`flex ${m.direction === "Out" ? "justify-end" : ""}`}>
                  <div className={`max-w-[80%] rounded-xl px-3 py-2 text-sm ${m.direction === "Out" ? "bg-accent-soft text-ink" : "bg-muted text-ink"}`}>
                    <p className="whitespace-pre-line">{m.text ?? (m.fileName ? `📎 ${m.fileName}` : "—")}</p>
                    <p className="mt-1 text-[11px] text-ink-3">
                      {when(m.at)}
                      {m.direction === "Out" && ` · ${m.author === "ai" ? "AI" : m.authorName ?? t(m.author === "phone" ? "с телефона" : "сотрудник")}`}
                      {m.status !== "Sent" && ` · ${t(m.status === "Pending" ? "в очереди" : m.status === "Cancelled" ? "отменено" : "не отправлено")}`}
                    </p>
                  </div>
                </li>
              ))}
            </ul>
            <div className="mt-3 flex gap-2">
              <textarea className={`${textarea} min-h-12 flex-1`} value={reply} maxLength={4000} placeholder={t("Ответ…")} onChange={(e) => setReply(e.target.value)} />
              <button
                className={primary}
                disabled={!!busy || !reply.trim()}
                onClick={() =>
                  run("send", async () => {
                    setOpen(await agentsApi.send(agent.id, open.id, reply.trim(), crypto.randomUUID()));
                    setReply("");
                  })
                }
              >
                <Send className="size-4" />
              </button>
            </div>
          </>
        )}
        <ErrorText error={error} />
      </Card>
    </div>
  );
}

// ---------- Проверка ----------

/** Проверка: сотрудник пишет за собеседника — видно, что ответил бы агент; ничего не отправляется (платный вызов модели). */
export function AgentTest({ agent }: { agent: AgentView }) {
  const t = useT();
  const [messages, setMessages] = useState<AgentTestMessage[]>([]);
  const [input, setInput] = useState("");
  const [vacancyId, setVacancyId] = useState(agent.vacancies[0]?.id ?? "");
  const [last, setLast] = useState<AgentTestResult | null>(null);
  const { busy, error, run } = useAction();
  const recruiter = agent.kind === "recruiter";

  const send = () =>
    run("test", async () => {
      const next = [...messages, { role: "user" as const, text: input.trim() }];
      const result = await agentsApi.test(agent.id, next, recruiter && vacancyId ? vacancyId : null);
      setLast(result);
      setMessages(result.messages.length > 0 && !result.blocked ? [...next, { role: "agent", text: result.messages.join("\n\n") }] : next);
      setInput("");
    });

  return (
    <Card
      title="Проверка агента"
      actions={
        messages.length > 0 && (
          <button
            className={button}
            onClick={() => {
              setMessages([]);
              setLast(null);
            }}
          >
            {t("Начать заново")}
          </button>
        )
      }
    >
      <p className="mb-3 max-w-3xl text-xs text-ink-3">
        {t("Напишите сообщение так, как написал бы {who}. Агент ответит с текущими настройками — ничего не отправляется. Каждый ответ — платный вызов модели.", {
          who: recruiter ? t("кандидат") : t("клиент или поставщик"),
        })}
      </p>
      {recruiter && agent.vacancies.length > 0 && (
        <Label label="Вакансия" className="mb-3 max-w-md">
          <select className={field} value={vacancyId} onChange={(e) => setVacancyId(e.target.value)}>
            <option value="">{t("Кандидат ещё не выбрал вакансию")}</option>
            {agent.vacancies.map((v) => (
              <option key={v.id} value={v.id}>
                {v.code} · {v.title}
              </option>
            ))}
          </select>
        </Label>
      )}
      <ul className="max-h-[26rem] space-y-2 overflow-y-auto">
        {messages.map((m, i) => (
          <li key={i} className={`flex ${m.role === "agent" ? "justify-end" : ""}`}>
            <div className={`max-w-[80%] whitespace-pre-line rounded-xl px-3 py-2 text-sm ${m.role === "agent" ? "bg-accent-soft" : "bg-muted"}`}>
              {m.role === "agent"
                ? m.text.split("\n\n").map((p, j) => (
                    <p key={j} className={j > 0 ? "mt-2 border-t border-accent/20 pt-2" : ""}>
                      {p}
                    </p>
                  ))
                : m.text}
            </div>
          </li>
        ))}
      </ul>
      {last && (
        <div className="mt-3 space-y-1 rounded-lg border border-line p-3 text-xs">
          <p>
            <b>{t("Действие")}:</b> {t(actionLabel[last.action] ?? last.action)}
            {last.model && <span className="text-ink-3"> · {last.model} · {last.inputTokens}+{last.outputTokens} {t("токенов")}</span>}
          </p>
          {last.handoffReason && <p className="text-warn">{t(last.handoffReason)}</p>}
          {last.blocked && <p className="text-bad">{t(last.blocked)}</p>}
          {last.blocked && last.messages.length > 0 && <p className="whitespace-pre-line text-ink-3">{last.messages.join("\n\n")}</p>}
          {last.notes.map((n, i) => (
            <p key={i} className="text-ink-3">
              {t(n)}
            </p>
          ))}
        </div>
      )}
      <div className="mt-3 flex gap-2">
        <textarea
          className={`${textarea} min-h-12 flex-1`}
          value={input}
          maxLength={2000}
          placeholder={t("Сообщение собеседника…")}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey && input.trim() && !busy) {
              e.preventDefault();
              send();
            }
          }}
        />
        <button className={primary} disabled={!!busy || !input.trim()} onClick={send}>
          {busy === "test" ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
        </button>
      </div>
      <ErrorText error={error} />
    </Card>
  );
}
