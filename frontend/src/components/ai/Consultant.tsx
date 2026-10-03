"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useRef, useState } from "react";
import {
  AlertTriangle,
  ArrowUp,
  Check,
  ChevronDown,
  Circle,
  History,
  Loader2,
  Minus,
  Pencil,
  Plus,
  Sparkle,
  Trash2,
  X,
} from "lucide-react";
import { bff } from "@/lib/bff";
import {
  exampleQuestions,
  historyGroup,
  streamChat,
  type AiStatus,
  type ChatMessage,
  type ConsultantDetails,
  type ConsultantProgress,
  type ConversationSummary,
  type ConversationView,
} from "@/lib/consultant";
import { Markdown } from "./Markdown";

type Props = {
  status: AiStatus;
  conversations: ConversationSummary[];
  conversation: ConversationView | null;
  canConfigure: boolean;
  /** Вопрос, подставленный в поле ввода (например, из находки AI Dashboard). */
  initialQuestion?: string;
};

const stageLabel: Record<string, string> = {
  routing: "Определяю, каких AI-сотрудников привлечь",
  compose: "Формирую ответ",
  memory: "Вспоминаю прошлые анализы",
};

export function Consultant({ status, conversations: initialList, conversation, canConfigure, initialQuestion }: Props) {
  const router = useRouter();
  const [list, setList] = useState(initialList);
  const [activeId, setActiveId] = useState<string | null>(conversation?.id ?? null);
  const [title, setTitle] = useState(conversation?.title ?? "Новый чат");
  const [messages, setMessages] = useState<ChatMessage[]>(conversation?.messages ?? []);
  const [input, setInput] = useState(initialQuestion ?? "");
  const [pending, setPending] = useState<string | null>(null);
  const [progress, setProgress] = useState<ConsultantProgress[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const bottom = useRef<HTMLDivElement>(null);
  const textarea = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    bottom.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages.length, pending, progress.length]);

  useEffect(() => {
    const el = textarea.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, 200)}px`;
  }, [input]);

  const send = async (text: string) => {
    const question = text.trim();
    if (!question || pending) return;
    setInput("");
    setError(null);
    setPending(question);
    setProgress([]);
    try {
      await streamChat(activeId, question, (e) => {
        switch (e.type) {
          case "start":
            if (!activeId) {
              setActiveId(e.data.conversationId);
              setTitle(e.data.title);
              window.history.replaceState(null, "", `/consultant/${e.data.conversationId}`);
            }
            setList((prev) => [
              { id: e.data.conversationId, title: e.data.title, createdAt: e.data.userMessage.createdAt, lastMessageAt: e.data.userMessage.createdAt },
              ...prev.filter((c) => c.id !== e.data.conversationId),
            ]);
            setMessages((prev) => [...prev, e.data.userMessage]);
            setPending(null);
            break;
          case "progress":
            setProgress((prev) => {
              const key = (p: ConsultantProgress) => `${p.stage}:${p.agent ?? ""}`;
              const i = prev.findIndex((p) => key(p) === key(e.data));
              return i < 0 ? [...prev, e.data] : prev.map((p, j) => (j === i ? e.data : p));
            });
            break;
          case "done":
            setMessages((prev) => [...prev, e.data.message]);
            break;
          case "error":
            if (e.data.message) setMessages((prev) => [...prev, e.data.message!]);
            else setError(e.data.error);
            break;
        }
      });
    } catch (e) {
      setError((e as Error).message);
      setInput(question);
    } finally {
      setPending(null);
      setProgress([]);
    }
  };

  const busy = pending !== null || progress.length > 0;
  const waiting = busy && (messages.length === 0 || messages[messages.length - 1].role === "user");

  const rename = async () => {
    if (!activeId) return;
    const next = prompt("Название чата", title)?.trim();
    if (!next || next === title) return;
    try {
      await bff(`ai/conversations/${activeId}`, { method: "PATCH", body: JSON.stringify({ title: next }) });
      setTitle(next);
      setList((prev) => prev.map((c) => (c.id === activeId ? { ...c, title: next } : c)));
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const archive = async (id: string) => {
    if (!confirm("Убрать чат из истории?")) return;
    try {
      await bff(`ai/conversations/${id}`, { method: "DELETE" });
      setList((prev) => prev.filter((c) => c.id !== id));
      if (id === activeId) router.push("/consultant");
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const groups = useMemo(() => {
    const out: { label: string; items: ConversationSummary[] }[] = [];
    for (const c of list) {
      const label = historyGroup(c.lastMessageAt);
      const last = out[out.length - 1];
      if (last?.label === label) last.items.push(c);
      else out.push({ label, items: [c] });
    }
    return out;
  }, [list]);

  const historyPanel = (
    <div className="flex h-full flex-col">
      <div className="p-3">
        <Link
          href="/consultant"
          className="flex h-10 w-full items-center justify-center gap-2 rounded-lg bg-accent text-sm font-semibold text-white hover:bg-accent-strong"
          onClick={() => setHistoryOpen(false)}
        >
          <Plus className="size-4" />
          Новый чат
        </Link>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto px-2 pb-4">
        {list.length === 0 && <p className="px-3 py-6 text-center text-sm text-ink-3">Чатов пока нет</p>}
        {groups.map((g) => (
          <div key={g.label} className="mt-3">
            <div className="px-3 pb-1 text-[11px] font-semibold uppercase tracking-wide text-ink-3">{g.label}</div>
            {g.items.map((c) => (
              <div key={c.id} className={`group flex items-center rounded-lg ${c.id === activeId ? "bg-accent-soft" : "hover:bg-muted"}`}>
                <Link
                  href={`/consultant/${c.id}`}
                  onClick={() => setHistoryOpen(false)}
                  className={`min-w-0 flex-1 truncate px-3 py-2 text-sm ${c.id === activeId ? "font-medium text-accent-strong" : "text-ink"}`}
                  title={c.title}
                >
                  {c.title}
                </Link>
                <button
                  type="button"
                  aria-label="Убрать чат из истории"
                  onClick={() => archive(c.id)}
                  className="mr-1 hidden size-7 shrink-0 place-items-center rounded-md text-ink-3 hover:text-bad group-hover:grid"
                >
                  <Trash2 className="size-3.5" />
                </button>
              </div>
            ))}
          </div>
        ))}
      </div>
    </div>
  );

  return (
    <div className="mx-auto flex h-[calc(100dvh-7rem-env(safe-area-inset-top)-env(safe-area-inset-bottom))] max-w-[1800px] lg:h-[calc(100dvh-4rem)]">
      <aside className="hidden w-72 shrink-0 border-r border-line bg-surface lg:block">{historyPanel}</aside>

      {historyOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <button type="button" aria-label="Закрыть историю" className="absolute inset-0 bg-black/30" onClick={() => setHistoryOpen(false)} />
          <div className="absolute inset-y-0 left-0 w-80 max-w-[85vw] bg-surface shadow-xl">{historyPanel}</div>
        </div>
      )}

      <section className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center gap-2 border-b border-line px-4 py-3 sm:px-6">
          <button
            type="button"
            onClick={() => setHistoryOpen(true)}
            aria-label="История чатов"
            className="grid size-10 place-items-center rounded-lg border border-line text-ink-2 hover:bg-muted lg:hidden"
          >
            <History className="size-4" />
          </button>
          <Sparkle className="hidden size-4 text-accent-strong sm:block" />
          <h1 className="min-w-0 flex-1 truncate text-base font-semibold text-ink">{activeId ? title : "Консультант"}</h1>
          <Link href="/ai" className="rounded-lg border border-line px-3 py-1.5 text-xs font-medium text-ink-2 hover:bg-muted hover:text-ink max-lg:py-2.5">
            AI Dashboard
          </Link>
          {activeId && (
            <button type="button" onClick={rename} aria-label="Переименовать чат" className="grid size-8 place-items-center rounded-md text-ink-3 hover:bg-muted hover:text-ink">
              <Pencil className="size-3.5" />
            </button>
          )}
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto">
          <div className="mx-auto max-w-4xl px-4 py-6 sm:px-6">
            {!status.ready && (
              <div className="mb-6 flex items-start gap-3 rounded-lg bg-warn-soft px-4 py-3 text-sm">
                <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warn" />
                <div>
                  <div className="font-semibold text-ink">Консультант пока не работает</div>
                  <div className="mt-0.5 text-ink-2">{status.message}</div>
                  {canConfigure && (
                    <Link href="/settings/ai" className="mt-1 inline-block font-medium text-accent-strong hover:underline">
                      Открыть «Настройки → AI»
                    </Link>
                  )}
                </div>
              </div>
            )}

            {messages.length === 0 && !busy && (
              <div className="py-8 text-center">
                <div className="mx-auto grid size-12 place-items-center rounded-full bg-accent-soft text-accent-strong">
                  <Sparkle className="size-5" />
                </div>
                <h2 className="mt-4 text-xl font-semibold text-ink">Чем помочь?</h2>
                <p className="mx-auto mt-2 max-w-xl text-sm text-ink-2">
                  Задайте вопрос обычным языком. Консультант сам решит, каких AI-сотрудников привлечь, возьмёт цифры из данных OneBase и отделит факты от
                  рекомендаций.
                </p>
                <div className="mx-auto mt-6 flex max-w-2xl flex-wrap justify-center gap-2">
                  {exampleQuestions.map((q) => (
                    <button
                      key={q}
                      type="button"
                      disabled={!status.ready}
                      onClick={() => send(q)}
                      className="rounded-full border border-line bg-surface px-3.5 py-1.5 text-sm text-ink-2 hover:border-accent/40 hover:text-ink max-lg:py-2.5 disabled:opacity-50"
                    >
                      {q}
                    </button>
                  ))}
                </div>
              </div>
            )}

            <div className="space-y-6">
              {messages.map((m) => (m.role === "user" ? <UserBubble key={m.id} text={m.content} /> : <AssistantMessage key={m.id} message={m} />))}
              {pending && <UserBubble text={pending} />}
              {waiting && <ProgressCard steps={progress} />}
            </div>
            <div ref={bottom} />
          </div>
        </div>

        <div className="border-t border-line bg-page px-4 py-3 sm:px-6">
          <div className="mx-auto max-w-4xl">
            {error && (
              <div className="mb-2 flex items-start gap-2 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">
                <span className="flex-1">{error}</span>
                <button type="button" aria-label="Скрыть" onClick={() => setError(null)}>
                  <X className="size-4" />
                </button>
              </div>
            )}
            <form
              className="flex items-end gap-2 rounded-xl border border-line bg-surface p-2 shadow-sm focus-within:border-accent focus-within:ring-2 focus-within:ring-accent/20"
              onSubmit={(e) => {
                e.preventDefault();
                void send(input);
              }}
            >
              <textarea
                ref={textarea}
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
                    e.preventDefault();
                    void send(input);
                  }
                }}
                rows={1}
                maxLength={4000}
                placeholder={status.ready ? "Напишите вопрос…" : "Консультант не настроен"}
                disabled={!status.ready}
                aria-label="Вопрос консультанту"
                className="max-h-[200px] min-h-[40px] flex-1 resize-none bg-transparent px-2 py-2 text-sm text-ink placeholder:text-ink-3 focus:outline-none disabled:opacity-60"
              />
              <button
                type="submit"
                aria-label="Отправить"
                disabled={!status.ready || busy || input.trim() === ""}
                className="grid size-10 shrink-0 place-items-center rounded-lg bg-accent text-white hover:bg-accent-strong disabled:opacity-40 max-lg:size-11"
              >
                {busy ? <Loader2 className="size-4 animate-spin" /> : <ArrowUp className="size-4" />}
              </button>
            </form>
            <p className="mt-1.5 text-center text-[11px] text-ink-3">
              Enter — отправить, Shift+Enter — новая строка. Консультант видит только данные, к которым у вас есть доступ.
            </p>
          </div>
        </div>
      </section>
    </div>
  );
}

function UserBubble({ text }: { text: string }) {
  return (
    <div className="flex justify-end">
      <div className="max-w-[85%] whitespace-pre-wrap rounded-2xl rounded-br-md bg-accent px-4 py-2.5 text-sm text-white">{text}</div>
    </div>
  );
}

function StepIcon({ status }: { status: ConsultantProgress["status"] }) {
  switch (status) {
    case "Done":
      return <Check className="size-4 text-ok" />;
    case "Running":
      return <Loader2 className="size-4 animate-spin text-accent-strong" />;
    case "Failed":
      return <X className="size-4 text-bad" />;
    case "Skipped":
      return <Minus className="size-4 text-ink-3" />;
    default:
      return <Circle className="size-3.5 text-ink-3" />;
  }
}

function ProgressCard({ steps }: { steps: ConsultantProgress[] }) {
  return (
    <div className="flex gap-3">
      <div className="grid size-8 shrink-0 place-items-center rounded-full bg-accent-soft text-accent-strong">
        <Sparkle className="size-4" />
      </div>
      <div className="min-w-0 flex-1 rounded-2xl rounded-tl-md border border-line bg-surface px-4 py-3">
        <div className="flex items-center gap-2 text-sm font-medium text-ink">
          <Loader2 className="size-4 animate-spin text-accent-strong" />
          Анализирую…
        </div>
        {steps.length > 0 && (
          <ul className="mt-2 space-y-1.5">
            {steps.map((s) => (
              <li key={`${s.stage}:${s.agent ?? ""}`} className="flex items-center gap-2 text-sm text-ink-2">
                <StepIcon status={s.status} />
                <span className={s.status === "Running" ? "text-ink" : ""}>{s.agentName ?? stageLabel[s.stage] ?? s.stage}</span>
                {s.note && <span className="truncate text-xs text-ink-3">— {s.note}</span>}
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function AssistantMessage({ message }: { message: ChatMessage }) {
  const failed = message.status === "failed";
  return (
    <div className="flex gap-3">
      <div className={`grid size-8 shrink-0 place-items-center rounded-full ${failed ? "bg-bad-soft text-bad" : "bg-accent-soft text-accent-strong"}`}>
        {failed ? <AlertTriangle className="size-4" /> : <Sparkle className="size-4" />}
      </div>
      <div className="min-w-0 flex-1">
        {failed ? (
          <div className="rounded-2xl rounded-tl-md bg-bad-soft px-4 py-3 text-sm text-bad">
            <div className="font-semibold">Ответ не получен</div>
            <div className="mt-1 text-ink-2">{message.error}</div>
          </div>
        ) : (
          <>
            {message.details && <Summary details={message.details} />}
            <div className="rounded-2xl rounded-tl-md border border-line bg-surface px-4 py-3">
              <Markdown text={message.content} />
            </div>
            {message.details && <Details details={message.details} durationMs={message.durationMs} />}
          </>
        )}
      </div>
    </div>
  );
}

function Summary({ details }: { details: ConsultantDetails }) {
  const agents = details.agents.filter((a) => a.status !== "disabled" && a.status !== "forbidden");
  if (agents.length === 0) return null;
  const findings = agents.reduce((n, a) => n + a.findings.length + a.problems.length, 0);
  const recommendations = agents.reduce((n, a) => n + a.recommendations.length, 0);
  return (
    <div className="mb-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-ink-3">
      <span>Проанализировано подразделений: {agents.length}</span>
      <span>Найдено фактов и проблем: {findings}</span>
      <span>Рекомендаций: {recommendations}</span>
    </div>
  );
}

function Details({ details, durationMs }: { details: ConsultantDetails; durationMs: number }) {
  const [open, setOpen] = useState(false);
  const sources = details.sources;
  const hasMore = details.agents.length > 0 || sources.length > 0;
  return (
    <div className="mt-2">
      {sources.length > 0 && (
        <div className="mb-2 flex flex-wrap items-center gap-1.5 text-xs">
          <span className="text-ink-3">Источники:</span>
          {sources.map((s, i) =>
            s.href ? (
              <Link key={i} href={s.href} className="rounded-full border border-line bg-surface px-2.5 py-0.5 text-ink-2 hover:border-accent/40 hover:text-accent-strong">
                {s.title}
                {s.period ? ` · ${s.period}` : ""}
              </Link>
            ) : (
              <span key={i} className="rounded-full border border-line bg-surface px-2.5 py-0.5 text-ink-2">
                {s.title}
                {s.period ? ` · ${s.period}` : ""}
              </span>
            ),
          )}
        </div>
      )}
      <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-[11px] text-ink-3">
        {details.model && <span>Модель: {details.model}{details.usedFallback ? " (резервная)" : ""}</span>}
        <span>{(durationMs / 1000).toFixed(1)} с</span>
        {(details.memoryUsed ?? 0) > 0 && <span>учтены прошлые анализы: {details.memoryUsed}</span>}
        {hasMore && details.agents.length > 0 && (
          <button type="button" onClick={() => setOpen((v) => !v)} className="inline-flex items-center gap-1 font-medium text-accent-strong hover:underline">
            Как получен ответ
            <ChevronDown className={`size-3 transition-transform ${open ? "rotate-180" : ""}`} />
          </button>
        )}
      </div>
      {open && (
        <div className="mt-3 space-y-3">
          {details.routingReason && <p className="text-xs text-ink-2">Почему эти AI-сотрудники: {details.routingReason}</p>}
          {details.agents.map((a) => (
            <AgentCard key={a.agent} result={a} />
          ))}
        </div>
      )}
    </div>
  );
}

const agentStatus: Record<string, { label: string; className: string }> = {
  completed: { label: "готово", className: "bg-ok-soft text-ok" },
  partial: { label: "частично", className: "bg-warn-soft text-warn" },
  no_data: { label: "нет данных", className: "bg-warn-soft text-warn" },
  forbidden: { label: "нет доступа", className: "bg-muted text-ink-2" },
  disabled: { label: "выключен", className: "bg-muted text-ink-2" },
  failed: { label: "ошибка", className: "bg-bad-soft text-bad" },
};

export function AgentCard({ result }: { result: ConsultantDetails["agents"][number] }) {
  const section = (title: string, items: string[]) =>
    items.length > 0 && (
      <div className="mt-2">
        <div className="text-[11px] font-semibold uppercase tracking-wide text-ink-3">{title}</div>
        <ul className="mt-1 list-disc space-y-0.5 pl-5 text-sm text-ink-2 marker:text-ink-3">
          {items.map((x, i) => (
            <li key={i}>{x}</li>
          ))}
        </ul>
      </div>
    );
  return (
    <div className="rounded-xl border border-line bg-surface px-4 py-3">
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-semibold text-ink">{result.agentName}</span>
        <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${(agentStatus[result.status] ?? agentStatus.failed).className}`}>
          {(agentStatus[result.status] ?? agentStatus.failed).label}
        </span>
      </div>
      {result.summary && <p className="mt-1 text-sm text-ink-2">{result.summary}</p>}
      {result.error && <p className="mt-1 text-sm text-bad">{result.error}</p>}
      {result.metrics.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-2">
          {result.metrics.map((m, i) => (
            <span key={i} className="rounded-lg bg-muted px-2.5 py-1 text-xs text-ink-2">
              {m.name}: <b className="font-semibold text-ink">{m.value}</b>
              {m.unit ? ` ${m.unit}` : ""}
            </span>
          ))}
        </div>
      )}
      {section("Факты", result.findings)}
      {section("Проблемы", result.problems)}
      {section("Рекомендации", result.recommendations)}
      {result.toolsUsed.length > 0 && <p className="mt-2 text-[11px] text-ink-3">Инструменты: {result.toolsUsed.join(", ")}</p>}
    </div>
  );
}
