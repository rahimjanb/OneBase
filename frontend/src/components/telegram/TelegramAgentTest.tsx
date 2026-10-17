"use client";

import { useEffect, useRef, useState } from "react";
import { Bot, Loader2, RotateCcw, Send } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Note } from "@/components/sales/bits";
import {
  chatTypeLabel,
  modeLabel,
  telegramApi,
  type AgentTestResult,
  type AgentTestTurn,
  type TelegramAccount,
  type TelegramChat,
  type TelegramPermission,
} from "@/lib/telegram";

type Message = { role: "user"; text: string } | { role: "agent"; result: AgentTestResult };

const quick = ["Привет! Кто ты?", "Ты бот или человек?", "Что в отчёте?", "Насколько всё плохо?", "Когда будет следующий отчёт?"];

const sample = `Отчёт по продажам за сентябрь 2026 (вторичка, кг)
Регион | План | Факт | Выполнение | К августу
Хорезм | 12 000 | 13 100 | 109% | +6%
Бухоро | 10 000 | 8 200 | 82% | −4%
Самарканд | 15 000 | 14 300 | 95% | +2%
Итого | 37 000 | 35 600 | 96% | +1%
Лидер — кекс (41% продаж). Отстаёт вафля: 61% плана.`;

/**
 * Проверка агента: вопрос «как будто» от выбранного человека в выбранном чате — те же правила, права и материалы, что в Telegram.
 * Отвечает настоящий ИИ, но в Telegram ничего не отправляется и журнал агента не пишется.
 */
export function TelegramAgentTest({ account }: { account: TelegramAccount }) {
  const [people, setPeople] = useState<TelegramPermission[]>([]);
  const [chats, setChats] = useState<TelegramChat[]>([]);
  const [permissionId, setPermissionId] = useState("");
  const [chatId, setChatId] = useState("");
  const [material, setMaterial] = useState("");
  const [messages, setMessages] = useState<Message[]>([]);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [question, setQuestion] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const end = useRef<HTMLDivElement>(null);

  useEffect(() => {
    telegramApi.permissions(account.id).then(setPeople, () => setPeople([]));
    telegramApi.chats(account.id).then((list) => setChats(list.filter((c) => c.type !== "Channel")), () => setChats([]));
  }, [account.id]);

  // Фигурные скобки: в новых браузерах scrollIntoView возвращает Promise, а эффект может вернуть только функцию очистки.
  useEffect(() => {
    end.current?.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }, [messages]);

  const reset = () => {
    setMessages([]);
    setConversationId(null);
    setError(null);
  };

  const ask = async (text: string) => {
    const q = text.trim();
    if (!q || busy) return;
    setBusy(true);
    setError(null);
    setQuestion("");
    const history: AgentTestTurn[] = [];
    for (let i = 0; i < messages.length - 1; i++) {
      const m = messages[i];
      const next = messages[i + 1];
      if (m.role === "user" && next.role === "agent" && next.result.replies.length > 0) {
        history.push({ question: m.text, answer: next.result.replies.join("\n").replace(/<[^>]+>/g, "") });
      }
    }
    setMessages((list) => [...list, { role: "user", text: q }]);
    try {
      const result = await telegramApi.testAgent(account.id, {
        chatId: chatId || null,
        permissionId: permissionId || null,
        question: q,
        history,
        sampleMaterial: material.trim() || null,
        conversationId,
      });
      setConversationId(result.conversationId);
      setMessages((list) => [...list, { role: "agent", result }]);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const who = people.find((p) => p.id === permissionId);

  return (
    <div className="grid gap-5 lg:grid-cols-[320px_1fr]">
      <div className="space-y-4">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">Кто пишет</span>
          <select className={field} value={permissionId} onChange={(e) => (setPermissionId(e.target.value), reset())}>
            <option value="">Незнакомый человек (нет в списке)</option>
            {people.map((p) => (
              <option key={p.id} value={p.id}>
                {p.username ? `@${p.username}` : p.title} — {modeLabel[p.mode].toLowerCase()}
              </option>
            ))}
          </select>
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">В каком чате</span>
          <select className={field} value={chatId} onChange={(e) => (setChatId(e.target.value), reset())}>
            <option value="">Без чата (только пример отчёта ниже)</option>
            {chats.map((c) => (
              <option key={c.id} value={c.id}>
                {c.title} · {chatTypeLabel[c.type]}
              </option>
            ))}
          </select>
          <span className="mt-1 block text-xs text-ink-3">Агент видит то, что OneBase отправлял в этот чат, и его расписание рассылок.</span>
        </label>
        <label className="block">
          <span className="mb-1.5 flex items-baseline justify-between text-sm font-medium text-ink">
            Пример отчёта
            <button type="button" className="text-xs font-normal text-accent-strong hover:underline" onClick={() => setMaterial(sample)}>
              вставить пример
            </button>
          </span>
          <textarea
            className={`${field} h-40 py-2 text-xs`}
            value={material}
            onChange={(e) => setMaterial(e.target.value)}
            placeholder="Текст «как будто отправленного» отчёта — только для проверки, никуда не сохраняется"
          />
        </label>
        <Note>
          Отвечает настоящий ИИ по тем же правилам, что в Telegram, но в Telegram ничего не отправляется и журнал агента не пишется.
          {who?.mode === "Full" && " Полный доступ отвечает в чате AI-консультанта пользователя OneBase («Telegram · …»)."}
        </Note>
      </div>

      <div className="flex min-h-[460px] flex-col rounded-xl border border-line">
        <div className="flex items-center justify-between border-b border-line px-4 py-2.5">
          <span className="flex items-center gap-2 text-sm font-medium text-ink">
            <Bot className="size-4 text-accent-strong" />
            {account.displayName && account.displayName.trim() !== "- -" ? account.displayName : account.username ? `@${account.username}` : account.name}
          </span>
          <button className={button} onClick={reset} disabled={busy || messages.length === 0}>
            <RotateCcw className="size-4" />
            Заново
          </button>
        </div>
        <div className="flex-1 space-y-3 overflow-y-auto p-4">
          {messages.length === 0 && <p className="text-sm text-ink-3">Напишите вопрос или выберите готовый ниже.</p>}
          {messages.map((m, i) =>
            m.role === "user" ? (
              <div key={i} className="ml-auto max-w-[80%] rounded-2xl rounded-br-md bg-accent px-3.5 py-2 text-sm text-white">
                {m.text}
              </div>
            ) : (
              <AgentBubble key={i} result={m.result} />
            ),
          )}
          {busy && (
            <div className="flex items-center gap-2 text-sm text-ink-3">
              <Loader2 className="size-4 animate-spin" />
              агент печатает…
            </div>
          )}
          <div ref={end} />
        </div>
        {error && <p className="mx-4 mb-2 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
        <div className="flex flex-wrap gap-1.5 border-t border-line px-4 pt-3">
          {quick.map((q) => (
            <button key={q} className="rounded-full border border-line px-3 py-1 text-xs text-ink-2 hover:bg-muted" onClick={() => ask(q)} disabled={busy}>
              {q}
            </button>
          ))}
        </div>
        <form
          className="flex gap-2 p-4"
          onSubmit={(e) => {
            e.preventDefault();
            ask(question);
          }}
        >
          <input className={field} value={question} onChange={(e) => setQuestion(e.target.value)} placeholder="Сообщение…" disabled={busy} />
          <button type="submit" className={primary} disabled={busy || !question.trim()}>
            <Send className="size-4" />
          </button>
        </form>
      </div>
    </div>
  );
}

const statusText: Record<AgentTestResult["status"], string> = {
  answered: "ответил",
  no_info: "нет данных",
  refused: "отказ",
  ignored: "не ответил бы",
  error: "ошибка",
};

function AgentBubble({ result }: { result: AgentTestResult }) {
  return (
    <div className="max-w-[85%]">
      {result.replies.map((html, i) => (
        <div
          key={i}
          className="mb-1 whitespace-pre-wrap break-words rounded-2xl rounded-bl-md bg-muted px-3.5 py-2 text-sm text-ink [&_a]:text-accent-strong [&_a]:underline [&_pre]:overflow-x-auto [&_pre]:text-xs"
          dangerouslySetInnerHTML={{ __html: html }}
        />
      ))}
      {result.files.map((f) => (
        <div key={f} className="mb-1 rounded-xl border border-line px-3 py-2 text-xs text-ink-2">
          📎 {f}
        </div>
      ))}
      {result.replies.length === 0 && result.files.length === 0 && (
        <div className="rounded-2xl rounded-bl-md border border-dashed border-line px-3.5 py-2 text-sm text-ink-3">Агент промолчал бы.</div>
      )}
      <div className="mt-1 text-[11px] text-ink-3">
        {result.mode === "full" ? "полный доступ" : result.mode === "sent_only" ? "по отправленным данным" : "—"} · {statusText[result.status]}
        {result.materials > 0 && ` · материалов ${result.materials}`}
        {result.model && ` · ${result.model}`}
        {result.inputTokens + result.outputTokens > 0 && ` · ${result.inputTokens + result.outputTokens} ток.`}
        {` · ${(result.durationMs / 1000).toFixed(1)} с`}
      </div>
      {result.note && <div className="mt-1 rounded-lg bg-warn-soft px-2.5 py-1.5 text-xs text-warn">{result.note}</div>}
      {result.error && <div className="mt-1 rounded-lg bg-bad-soft px-2.5 py-1.5 text-xs text-bad">{result.error}</div>}
    </div>
  );
}
