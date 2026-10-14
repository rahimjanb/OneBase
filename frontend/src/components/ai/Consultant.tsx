"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useRef, useState } from "react";
import {
  AlertTriangle,
  ArrowUp,
  AudioLines,
  Check,
  ChevronDown,
  Circle,
  GitCompareArrows,
  History,
  LayoutDashboard,
  Lightbulb,
  Loader2,
  MessageSquare,
  Mic,
  Minus,
  PackageSearch,
  Pencil,
  Plus,
  Search,
  Sparkle,
  Sparkles,
  Square,
  Target,
  Trash2,
  TrendingDown,
  TrendingUp,
  Volume2,
  X,
  type LucideIcon,
} from "lucide-react";
import { bff } from "@/lib/bff";
import { phoneQuery, useMedia } from "@/lib/media";
import {
  exampleQuestions,
  historySection,
  historyTime,
  streamChat,
  type AiStatus,
  type ChatMessage,
  type ConsultantDetails,
  type ConsultantProgress,
  type ConversationSummary,
  type ConversationView,
} from "@/lib/consultant";
import {
  canRecognizeInBrowser,
  canRecord,
  canSpeakInBrowser,
  MaxRecordSeconds,
  recognizeInBrowser,
  SpeechPlayer,
  transcribe,
  VoiceRecorder,
  type SpeechState,
} from "@/lib/voice";
import { Markdown } from "./Markdown";

const voiceModeKey = "onebase-voice-mode";

type Props = {
  status: AiStatus;
  conversations: ConversationSummary[];
  conversation: ConversationView | null;
  canConfigure: boolean;
  /** Вопрос, подставленный в поле ввода (например, из находки AI Dashboard). */
  initialQuestion?: string;
};

/** Стеклянная кнопка-пилюля в шапке чата (цвета — по состоянию). */
const pill = "inline-flex h-9 shrink-0 items-center gap-1.5 rounded-full border px-3.5 text-xs font-medium backdrop-blur-md transition-colors max-lg:h-10";

/** Значки подсказок пустого чата — по порядку exampleQuestions. */
const suggestionIcons: LucideIcon[] = [TrendingUp, Target, TrendingDown, GitCompareArrows, PackageSearch, Lightbulb];

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
  // Телефон: короткая подсказка в поле — длинная переносилась и растягивала поле на две строки.
  const phone = useMedia(phoneQuery);
  const [pending, setPending] = useState<string | null>(null);
  const [progress, setProgress] = useState<ConsultantProgress[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const bottom = useRef<HTMLDivElement>(null);
  const textarea = useRef<HTMLTextAreaElement>(null);

  // Голос: вопрос голосом (запись → распознавание на сервере или голос браузера) и озвучка ответов.
  const serverVoice = !!status.voice?.ready;
  const [caps, setCaps] = useState({ listen: false, speak: false });
  const [voiceMode, setVoiceMode] = useState(false);
  const [recording, setRecording] = useState(false);
  const [seconds, setSeconds] = useState(0);
  const [transcribing, setTranscribing] = useState(false);
  const [speaking, setSpeaking] = useState<{ id: string; state: SpeechState } | null>(null);
  const recorder = useRef<VoiceRecorder | null>(null);
  const listening = useRef<{ stop: () => void } | null>(null);
  const cancelled = useRef(false);
  const player = useRef<SpeechPlayer | null>(null);
  const voiceModeRef = useRef(false);

  useEffect(() => {
    setCaps({ listen: serverVoice ? canRecord() : canRecognizeInBrowser(), speak: serverVoice || canSpeakInBrowser() });
    try {
      const saved = localStorage.getItem(voiceModeKey) === "1";
      setVoiceMode(saved);
      voiceModeRef.current = saved;
    } catch {
      // хранилище недоступно — режим выключен
    }

    return () => {
      player.current?.stop();
      recorder.current?.cancel();
    };
  }, [serverVoice]);

  // Таймер записи; длинная запись останавливается сама.
  useEffect(() => {
    if (!recording) return;
    const started = Date.now();
    setSeconds(0);
    const timer = setInterval(() => {
      const s = Math.floor((Date.now() - started) / 1000);
      setSeconds(s);
      if (s >= MaxRecordSeconds) void finishRecording();
    }, 250);
    return () => clearInterval(timer);
    // finishRecording берёт запись из ref — перезапускать таймер при её смене не нужно
  }, [recording]);

  /** Озвучить ответ; повторное нажатие на тот же ответ — остановить. */
  const speak = async (id: string) => {
    const p = (player.current ??= new SpeechPlayer());
    if (speaking?.id === id) {
      p.stop();
      setSpeaking(null);
      return;
    }

    setError(null);
    try {
      await p.speak(id, serverVoice, (state) => setSpeaking((prev) => (state === "idle" ? (prev?.id === id ? null : prev) : { id, state })));
    } catch (e) {
      setSpeaking(null);
      setError((e as Error).message);
    }
  };

  const toggleVoiceMode = () => {
    const next = !voiceMode;
    if (next) (player.current ??= new SpeechPlayer()).unlock();
    else {
      player.current?.stop();
      setSpeaking(null);
    }

    setVoiceMode(next);
    voiceModeRef.current = next;
    try {
      localStorage.setItem(voiceModeKey, next ? "1" : "0");
    } catch {
      // не сохранится до перезагрузки — не страшно
    }
  };

  /** Распознанный вопрос: в голосовом режиме — сразу в чат, иначе — в поле ввода, чтобы поправить и отправить. */
  const heard = (text: string) => {
    if (cancelled.current) return;
    if (!text) {
      setError("Речь не распознана — попробуйте ещё раз.");
      return;
    }

    if (voiceModeRef.current) {
      void send(text);
      return;
    }

    setInput((prev) => (prev.trim() ? `${prev.trim()} ${text}` : text));
    textarea.current?.focus();
  };

  const startRecording = async () => {
    player.current?.stop();
    setSpeaking(null);
    (player.current ??= new SpeechPlayer()).unlock();
    setError(null);
    cancelled.current = false;
    if (serverVoice) {
      const r = new VoiceRecorder();
      try {
        await r.start();
      } catch (e) {
        setError((e as Error).message);
        return;
      }

      recorder.current = r;
      setRecording(true);
      return;
    }

    setRecording(true);
    listening.current = recognizeInBrowser(
      (text) => {
        listening.current = null;
        setRecording(false);
        heard(text);
      },
      (message) => {
        listening.current = null;
        setRecording(false);
        if (!cancelled.current) setError(message);
      },
    );
  };

  const finishRecording = async () => {
    if (listening.current) {
      listening.current.stop(); // текст придёт в heard
      return;
    }

    const r = recorder.current;
    recorder.current = null;
    setRecording(false);
    if (!r) return;
    const blob = await r.stop();
    if (!blob) {
      setError("Запись пустая — проверьте микрофон.");
      return;
    }

    setTranscribing(true);
    try {
      heard(await transcribe(blob));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setTranscribing(false);
    }
  };

  const cancelRecording = () => {
    cancelled.current = true;
    recorder.current?.cancel();
    recorder.current = null;
    listening.current?.stop();
    listening.current = null;
    setRecording(false);
  };

  useEffect(() => {
    // Пустой чат не прокручивается: на телефоне подсказки выше экрана, и прокрутка вниз срезала бы заголовок.
    if (messages.length === 0 && pending === null) return;
    bottom.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages.length, pending, progress.length]);

  useEffect(() => {
    const el = textarea.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, 200)}px`;
    // и при смене подсказки: на телефоне она короче — поле в одну строку
  }, [input, phone]);

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
            if (voiceModeRef.current && e.data.message.status === "completed") void speak(e.data.message.id);
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

  // История: поиск по названию, разделы Сегодня / Вчера / Ранее (список уже отсортирован по последнему сообщению).
  const [query, setQuery] = useState("");
  const groups = useMemo(() => {
    const needle = query.trim().toLowerCase();
    const out: { label: string; items: ConversationSummary[] }[] = [];
    for (const c of list) {
      if (needle && !c.title.toLowerCase().includes(needle)) continue;
      const label = historySection(c.lastMessageAt);
      const group = out.find((g) => g.label === label);
      if (group) group.items.push(c);
      else out.push({ label, items: [c] });
    }
    return out;
  }, [list, query]);

  const historyPanel = (
    <div className="flex h-full flex-col">
      <div className="space-y-3 p-4">
        <Link
          href="/consultant"
          className="flex h-11 w-full items-center justify-center gap-2 rounded-full bg-accent text-sm font-semibold text-white hover:bg-accent-strong"
          onClick={() => setHistoryOpen(false)}
        >
          <Plus className="size-4" />
          Новый чат
        </Link>
        <label className="flex h-10 items-center gap-2 rounded-full border border-line bg-muted px-3.5 text-ink-3 backdrop-blur-md focus-within:border-accent/60">
          <Search className="size-4 shrink-0" strokeWidth={1.75} />
          <input
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Поиск по чатам"
            aria-label="Поиск по чатам"
            className="min-w-0 flex-1 bg-transparent text-[13px] text-ink placeholder:text-ink-3 focus:outline-none"
          />
        </label>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto px-3 pb-4">
        {list.length === 0 && <p className="px-3 py-6 text-center text-sm text-ink-3">Чатов пока нет</p>}
        {list.length > 0 && groups.length === 0 && <p className="px-3 py-6 text-center text-sm text-ink-3">Ничего не найдено</p>}
        {groups.map((g) => (
          <div key={g.label} className="mt-2 first:mt-0">
            <div className="px-3 pb-1.5 pt-2 text-[10px] font-semibold uppercase tracking-[1px] text-ink-3">{g.label}</div>
            <div className="space-y-0.5">
              {g.items.map((c) => {
                const active = c.id === activeId;
                return (
                  <div
                    key={c.id}
                    className={`group relative flex items-center rounded-xl border ${
                      active ? "border-line bg-muted shadow-sm backdrop-blur-md" : "border-transparent hover:bg-muted/60"
                    }`}
                  >
                    <Link
                      href={`/consultant/${c.id}`}
                      onClick={() => setHistoryOpen(false)}
                      aria-current={active ? "page" : undefined}
                      className="flex min-w-0 flex-1 items-center gap-2.5 px-3 py-2.5"
                      title={c.title}
                    >
                      <MessageSquare className={`size-4 shrink-0 ${active ? "text-accent-strong" : "text-ink-3"}`} strokeWidth={1.75} />
                      <span className={`min-w-0 flex-1 truncate text-[13px] ${active ? "font-medium text-ink" : "text-ink-2"}`}>{c.title}</span>
                      <span className="shrink-0 text-[11px] tabular-nums text-ink-3 group-focus-within:invisible group-hover:invisible">
                        {historyTime(c.lastMessageAt)}
                      </span>
                    </Link>
                    <button
                      type="button"
                      aria-label="Убрать чат из истории"
                      onClick={() => archive(c.id)}
                      className="absolute right-2 hidden size-7 place-items-center rounded-lg text-ink-3 hover:bg-bad-soft hover:text-bad focus:grid group-hover:grid"
                    >
                      <Trash2 className="size-3.5" />
                    </button>
                  </div>
                );
              })}
            </div>
          </div>
        ))}
      </div>
    </div>
  );

  return (
    <div className="mx-auto flex h-full max-w-[1800px]">
      <aside className="hidden w-72 shrink-0 border-r border-line bg-surface lg:block">{historyPanel}</aside>

      {historyOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <button type="button" aria-label="Закрыть историю" className="absolute inset-0 bg-black/30" onClick={() => setHistoryOpen(false)} />
          <div className="absolute inset-y-0 left-0 w-80 max-w-[85vw] bg-surface-solid shadow-xl">{historyPanel}</div>
        </div>
      )}

      <section className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center gap-3 border-b border-line px-4 py-3 sm:px-6">
          <button
            type="button"
            onClick={() => setHistoryOpen(true)}
            aria-label="История чатов"
            className="grid size-10 shrink-0 place-items-center rounded-full border border-line bg-muted text-ink-2 hover:text-ink lg:hidden"
          >
            <History className="size-4" />
          </button>
          <Sparkles className="hidden size-5 shrink-0 text-accent-strong sm:block" strokeWidth={1.75} />
          <div className="min-w-0 flex-1">
            <h1 className="truncate text-base font-semibold text-ink">{activeId ? title : "AI-консультант"}</h1>
            <div className="mt-0.5 flex items-center gap-1.5 text-xs text-ink-3">
              <span className={`size-2 shrink-0 rounded-full ${status.ready ? "bg-ok shadow-[0_0_8px_var(--color-ok)]" : "bg-warn"}`} aria-hidden />
              <span className="truncate">{status.ready ? "подключён к данным OneBase" : "не настроен"}</span>
            </div>
          </div>
          {(caps.listen || caps.speak) && (
            <button
              type="button"
              onClick={toggleVoiceMode}
              aria-pressed={voiceMode}
              title={voiceMode ? "Голосовой режим включён: вопрос голосом отправляется сразу, ответ читается вслух" : "Включить голосовой режим"}
              className={`${pill} ${voiceMode ? "border-accent/60 bg-accent-soft text-accent-strong" : "border-line bg-muted text-ink-2 hover:text-ink"}`}
            >
              <AudioLines className="size-4" strokeWidth={1.75} />
              <span className="hidden sm:inline">Голос</span>
            </button>
          )}
          <Link href="/ai" className={`${pill} border-line bg-muted text-ink-2 hover:text-ink`} aria-label="AI Dashboard">
            <LayoutDashboard className="size-4" strokeWidth={1.75} />
            <span className="hidden sm:inline">AI Dashboard</span>
          </Link>
          {activeId && (
            <button type="button" onClick={rename} aria-label="Переименовать чат" className="grid size-9 shrink-0 place-items-center rounded-full text-ink-3 hover:bg-muted hover:text-ink">
              <Pencil className="size-3.5" />
            </button>
          )}
        </header>

        <div className="min-h-0 flex-1 overflow-y-auto">
          {/* min-h-full и flex — пустое состояние по центру области чата. */}
          <div className={`mx-auto flex min-h-full flex-col px-4 py-6 sm:px-6 ${messages.length === 0 && !busy ? "max-w-6xl" : "max-w-4xl"}`}>
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
              <div className="my-auto py-8 text-center">
                {/* Светящийся стеклянный круг со значком. */}
                <div className="mx-auto grid size-16 place-items-center rounded-full bg-muted text-accent-strong shadow-[0_0_48px_-6px_rgb(46_107_255/0.55)] ring-1 ring-inset ring-white/15 backdrop-blur-md">
                  <Sparkles className="size-7" strokeWidth={1.5} />
                </div>
                <h2 className="mt-5 text-xl font-bold tracking-tight text-ink sm:text-2xl">Чем помочь вашему бизнесу?</h2>
                <p className="mx-auto mt-2 max-w-xl text-sm leading-relaxed text-ink-2">
                  Спросите обычным языком: консультант привлечёт нужных AI-сотрудников, возьмёт цифры из данных OneBase, к которым у вас есть доступ,
                  и отделит факты от рекомендаций.
                </p>
                <div className="mx-auto mt-7 grid w-full max-w-[1120px] gap-2.5 sm:grid-cols-2 lg:grid-cols-3">
                  {exampleQuestions.map((q, i) => {
                    const Icon = suggestionIcons[i] ?? Sparkle;
                    return (
                      <button
                        key={q}
                        type="button"
                        disabled={!status.ready}
                        onClick={() => send(q)}
                        className="flex items-center gap-2.5 rounded-full border border-line bg-muted px-4 py-2.5 text-left text-xs text-ink-2 backdrop-blur-md transition-colors hover:border-accent/50 hover:text-ink disabled:opacity-50 max-lg:py-3"
                      >
                        <Icon className="size-4 shrink-0 text-accent-strong" strokeWidth={1.75} />
                        <span className="min-w-0 leading-snug">{q}</span>
                      </button>
                    );
                  })}
                </div>
              </div>
            )}

            <div className="space-y-6">
              {messages.map((m) =>
                m.role === "user" ? (
                  <UserBubble key={m.id} text={m.content} />
                ) : (
                  <AssistantMessage
                    key={m.id}
                    message={m}
                    speech={caps.speak ? (speaking?.id === m.id ? speaking.state : "idle") : null}
                    onSpeak={() => void speak(m.id)}
                  />
                ),
              )}
              {pending && <UserBubble text={pending} />}
              {waiting && <ProgressCard steps={progress} />}
            </div>
            <div ref={bottom} />
          </div>
        </div>

        <div className="px-4 pb-4 pt-2 sm:px-6">
          <div className="mx-auto max-w-4xl">
            {error && (
              <div className="mb-2 flex items-start gap-2 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">
                <span className="flex-1">{error}</span>
                <button type="button" aria-label="Скрыть" onClick={() => setError(null)}>
                  <X className="size-4" />
                </button>
              </div>
            )}
            {(recording || transcribing) && (
              <div className="mb-2 flex items-center gap-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-ink" role="status">
                {recording ? (
                  <>
                    <span className="size-2.5 animate-pulse rounded-full bg-bad" />
                    <span className="flex-1">
                      Слушаю… {Math.floor(seconds / 60)}:{String(seconds % 60).padStart(2, "0")}
                      <span className="ml-2 text-ink-3">нажмите ■, когда закончите{voiceMode ? " — вопрос уйдёт сразу" : ""}</span>
                    </span>
                    <button type="button" onClick={cancelRecording} className="text-ink-3 hover:text-bad" aria-label="Отменить запись">
                      <X className="size-4" />
                    </button>
                  </>
                ) : (
                  <>
                    <Loader2 className="size-4 animate-spin text-accent-strong" />
                    <span>Распознаю речь…</span>
                  </>
                )}
              </div>
            )}
            <form
              className="flex items-end gap-2 rounded-[28px] border border-line bg-muted p-2 shadow-[0_16px_48px_-24px_rgb(0_0_0/0.6)] backdrop-blur-xl focus-within:border-accent/60 focus-within:ring-2 focus-within:ring-accent/20"
              onSubmit={(e) => {
                e.preventDefault();
                void send(input);
              }}
            >
              {caps.listen && (
                <button
                  type="button"
                  aria-label={recording ? "Закончить запись" : "Задать вопрос голосом"}
                  title={recording ? "Закончить запись" : "Задать вопрос голосом"}
                  disabled={!status.ready || busy || transcribing}
                  onClick={() => void (recording ? finishRecording() : startRecording())}
                  className={`grid size-10 shrink-0 place-items-center rounded-full max-lg:size-11 disabled:opacity-40 ${
                    recording ? "bg-bad text-white" : "text-ink-2 hover:bg-muted hover:text-ink"
                  }`}
                >
                  {transcribing ? <Loader2 className="size-4 animate-spin" /> : recording ? <Square className="size-3.5 fill-current" /> : <Mic className="size-[18px]" strokeWidth={1.75} />}
                </button>
              )}
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
                placeholder={!status.ready ? "Консультант не настроен" : phone ? "Спросите о продажах…" : "Спросите о продажах, складе или клиентах…"}
                disabled={!status.ready}
                aria-label="Вопрос консультанту"
                className="max-h-[200px] min-h-[40px] flex-1 resize-none bg-transparent px-2 py-2.5 text-base text-ink placeholder:text-ink-3 focus:outline-none disabled:opacity-60 sm:text-sm"
              />
              <button
                type="submit"
                aria-label="Отправить"
                disabled={!status.ready || busy || input.trim() === ""}
                className="grid size-10 shrink-0 place-items-center rounded-full bg-accent text-white hover:bg-accent-strong disabled:opacity-40 max-lg:size-11"
              >
                {busy ? <Loader2 className="size-4 animate-spin" /> : <ArrowUp className="size-4" />}
              </button>
            </form>
            {/* Подсказка про клавиши — только при мыши и клавиатуре: на сенсорном экране Shift + Enter нет. */}
            <p className="mt-2 text-center text-[11px] text-ink-3 pointer-coarse:hidden">Enter — отправить · Shift + Enter — новая строка</p>
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

/** speech — состояние озвучки этого ответа (null — озвучка недоступна в этом браузере). */
function AssistantMessage({ message, speech, onSpeak }: { message: ChatMessage; speech: SpeechState | null; onSpeak: () => void }) {
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
            {speech && (
              <button
                type="button"
                onClick={onSpeak}
                aria-label={speech === "idle" ? "Озвучить ответ" : "Остановить озвучку"}
                className={`mt-1.5 inline-flex items-center gap-1.5 rounded-md px-2 py-1 text-xs font-medium max-lg:py-2 ${
                  speech === "idle" ? "text-ink-3 hover:bg-muted hover:text-ink" : "bg-accent-soft text-accent-strong"
                }`}
              >
                {speech === "loading" ? (
                  <Loader2 className="size-3.5 animate-spin" />
                ) : speech === "playing" ? (
                  <Square className="size-3 fill-current" />
                ) : (
                  <Volume2 className="size-3.5" />
                )}
                {speech === "loading" ? "Готовлю звук…" : speech === "playing" ? "Остановить" : "Озвучить"}
              </button>
            )}
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
