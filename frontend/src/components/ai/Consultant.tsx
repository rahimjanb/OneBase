"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  AlertTriangle,
  AudioLines,
  Building2,
  Check,
  ChevronLeft,
  Copy,
  CornerDownRight,
  FileDown,
  History,
  LayoutDashboard,
  Lightbulb,
  ListChecks,
  Loader2,
  MoreHorizontal,
  PanelLeftClose,
  PanelLeftOpen,
  Pencil,
  RefreshCw,
  Search,
  Share2,
  Sparkles,
  Square,
  SquarePen,
  ThumbsDown,
  ThumbsUp,
  Trash2,
  Volume2,
  X,
} from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { TaskForm } from "@/components/tasks/TaskForm";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { streamChat, type AiStatus, type ChatMessage, type ConsultantProgress, type ConversationSummary, type ConversationView, type DraftEvent } from "@/lib/consultant";
import {
  answerText,
  chatTitle,
  contextPayload,
  currentStep,
  departmentNames,
  formatNumber,
  initialContext,
  plural,
  quickQuestions,
  sourceLabels,
  taskDraft,
  tashkentMonth,
  type ChatContext,
  type RecommendationItem,
} from "@/lib/consultant-view";
import type { DepartmentTasks, WorkProject } from "@/lib/tasks";
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
import { ReportCard } from "./ReportCard";
import { AnswerCard, type CreatedTasks } from "./consultant/AnswerCard";
import { DetailsPanel, Grounding } from "./consultant/AnswerDetails";
import { ChatSidebar, MenuItem, RenameInput } from "./consultant/ChatSidebar";
import { Composer } from "./consultant/Composer";
import { StartScreen } from "./consultant/StartScreen";

export { AgentCard } from "./consultant/AnswerDetails";

const voiceModeKey = "onebase-voice-mode";
const pinnedKey = "onebase-ai-pinned";
const collapsedKey = "onebase-ai-sidebar-collapsed";
const tasksKey = "onebase-ai-tasks";
const feedbackKey = "onebase-ai-feedback";

/** Ответ сервера, когда модель вернула пустой текст. */
const emptyAnswer = "Модель вернула пустой ответ.";

/** Черновик ответа, пока он пишется: текст по мере генерации и что с ним сейчас (пишется, проверяется, переписывается). */
type Draft = { text: string; state: "writing" | "checking" | "revising" };

/** Следующее состояние черновика по событию draft. */
function nextDraft(prev: Draft | null, e: DraftEvent): Draft {
  const draft = prev ?? { text: "", state: "writing" };
  switch (e.kind) {
    case "text":
      return { text: draft.text + (e.text ?? ""), state: draft.state === "checking" ? "writing" : draft.state };
    case "reset":
      return { ...draft, text: "" };
    case "checking":
      return { ...draft, state: "checking" };
    case "revising":
      return { text: "", state: "revising" };
  }
}

const draftLabel: Record<Draft["state"], string> = {
  writing: "Черновик ответа — числа ещё не проверены",
  checking: "Сверяю числа ответа с данными OneBase…",
  revising: "Числа не подтвердились данными — переписываю ответ…",
};

type Props = {
  status: AiStatus;
  conversations: ConversationSummary[];
  conversation: ConversationView | null;
  canConfigure: boolean;
  /** Вопрос, подставленный в поле ввода (например, из находки AI Dashboard). */
  initialQuestion?: string;
  /** Права пользователя: быстрые вопросы, источники под названием чата, кнопки задач. */
  permissions: string[];
  /** Контекст из адреса: ?period=2026-10&dept=sales. */
  period?: string | null;
  department?: string | null;
};

function readJson<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
}

function writeJson(key: string, value: unknown) {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    // хранилище недоступно — состояние не сохранится до перезагрузки
  }
}

/** Печать (PDF) только выбранного: ответа или всей переписки; на время печати — светлая тема. */
function printElement(el: HTMLElement | null) {
  if (!el) return;
  const root = document.documentElement;
  const theme = root.getAttribute("data-theme");
  el.setAttribute("data-ai-print-target", "");
  root.setAttribute("data-ai-print", "");
  root.setAttribute("data-theme", "light");
  const done = () => {
    el.removeAttribute("data-ai-print-target");
    root.removeAttribute("data-ai-print");
    if (theme === null) root.removeAttribute("data-theme");
    else root.setAttribute("data-theme", theme);
    window.removeEventListener("afterprint", done);
  };
  window.addEventListener("afterprint", done);
  window.print();
}

/**
 * AI-консультант (ТЗ ai-consultant): слева — чаты (на телефоне — шторка «История»), справа — шапка чата, лента и поле вопроса снизу.
 * Ответ — карточка отчёта из блоков с сервера (answer), простой ответ — Markdown. Пока ответ готовится — скелетон, строка статуса и «Стоп».
 */
export function Consultant({ status, conversations: initialList, conversation, canConfigure, initialQuestion, permissions, period, department }: Props) {
  const t = useT();
  const router = useRouter();
  const [list, setList] = useState(initialList);
  const [activeId, setActiveId] = useState<string | null>(conversation?.id ?? null);
  const [title, setTitle] = useState(conversation?.title ?? t("Новый чат"));
  const [messages, setMessages] = useState<ChatMessage[]>(conversation?.messages ?? []);
  const [input, setInput] = useState(initialQuestion ?? "");
  const [pending, setPending] = useState<string | null>(null);
  const [progress, setProgress] = useState<ConsultantProgress[]>([]);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [streaming, setStreaming] = useState(false);
  const [failure, setFailure] = useState<{ question: string; error: string } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [context, setContext] = useState<ChatContext>(() => initialContext(period, department));
  const defaultPeriod = useMemo(() => tashkentMonth(), []);
  const [pinned, setPinned] = useState<Set<string>>(() => new Set());
  const [collapsed, setCollapsed] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [editingTitle, setEditingTitle] = useState(false);
  const [headerMenu, setHeaderMenu] = useState(false);
  const [created, setCreated] = useState<Record<string, CreatedTasks>>({});
  const [feedback, setFeedback] = useState<Record<string, "up" | "down">>({});
  const [work, setWork] = useState<{ departments: DepartmentTasks[]; projects: WorkProject[] } | null>(null);
  const [taskForm, setTaskForm] = useState<{ messageId: string; index: number; draft: ReturnType<typeof taskDraft> } | null>(null);
  const [bulk, setBulk] = useState<{ messageId: string; items: RecommendationItem[] } | null>(null);
  const [composerHeight, setComposerHeight] = useState(150);
  const abort = useRef<AbortController | null>(null);
  const scroller = useRef<HTMLDivElement>(null);
  const feed = useRef<HTMLDivElement>(null);
  const composerBox = useRef<HTMLDivElement>(null);
  const textarea = useRef<HTMLTextAreaElement>(null);
  const headerMenuRef = useRef<HTMLDivElement>(null);

  const questions = useMemo(() => quickQuestions(permissions), [permissions]);
  const sources = useMemo(() => sourceLabels(permissions), [permissions]);
  const canTasks = permissions.includes("work.tasks");

  // Состояние, которое помнит браузер: закреплённые чаты, свёрнутая колонка, созданные задачи и оценки ответов.
  useEffect(() => {
    setPinned(new Set(readJson<string[]>(pinnedKey, [])));
    setCollapsed(readJson<boolean>(collapsedKey, false));
    setCreated(readJson<Record<string, CreatedTasks>>(tasksKey, {}));
    setFeedback(readJson<Record<string, "up" | "down">>(feedbackKey, {}));
  }, []);

  // Высота поля вопроса — отступ ленты снизу: последний блок ответа не прячется под полем.
  useEffect(() => {
    const el = composerBox.current;
    if (!el) return;
    const observer = new ResizeObserver(() => setComposerHeight(el.offsetHeight));
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  // ---------- Голос: вопрос голосом и озвучка ответов (как раньше) ----------
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
    const saved = readJson<number>(voiceModeKey, 0) === 1;
    setVoiceMode(saved);
    voiceModeRef.current = saved;
    return () => {
      player.current?.stop();
      recorder.current?.cancel();
    };
  }, [serverVoice]);

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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [recording]);

  /** Озвучить ответ (у отчёта — «Главное» и рекомендации); повторное нажатие на тот же ответ — остановить. */
  const speak = async (id: string) => {
    const p = (player.current ??= new SpeechPlayer(t));
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
    if (next) (player.current ??= new SpeechPlayer(t)).unlock();
    else {
      player.current?.stop();
      setSpeaking(null);
    }
    setVoiceMode(next);
    voiceModeRef.current = next;
    writeJson(voiceModeKey, next ? 1 : 0);
  };

  const heard = (text: string) => {
    if (cancelled.current) return;
    if (!text) {
      setError(t("Речь не распознана — попробуйте ещё раз."));
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
    (player.current ??= new SpeechPlayer(t)).unlock();
    setError(null);
    cancelled.current = false;
    if (serverVoice) {
      const r = new VoiceRecorder();
      try {
        await r.start(t);
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
      t,
    );
  };

  const finishRecording = async () => {
    if (listening.current) {
      listening.current.stop();
      return;
    }
    const r = recorder.current;
    recorder.current = null;
    setRecording(false);
    if (!r) return;
    const blob = await r.stop();
    if (!blob) {
      setError(t("Запись пустая — проверьте микрофон."));
      return;
    }
    setTranscribing(true);
    try {
      heard(await transcribe(blob, t));
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

  // ---------- Вопрос и поток ответа ----------
  // Новый ответ (и открытый чат) — к началу последнего ответа: отчёт читают с «Главного», а не с конца. Пока ответ готовится — вниз,
  // с учётом отступа под полем вопроса (прокрутка контейнера, а не метки в конце ленты).
  const lastId = messages.length > 0 ? messages[messages.length - 1].id : null;
  const lastRole = messages.length > 0 ? messages[messages.length - 1].role : null;
  useEffect(() => {
    const box = scroller.current;
    if (!box || lastRole !== "assistant" || !lastId) return;
    const el = box.querySelector<HTMLElement>(`[data-message-id="${lastId}"]`);
    // С вопросом над ответом, если он помещается: видно, на что ответ.
    const question = el?.previousElementSibling instanceof HTMLElement && el.previousElementSibling.dataset.messageId ? el.previousElementSibling : null;
    const target = question && el && el.offsetTop - question.offsetTop < box.clientHeight / 3 ? question : el;
    if (target) box.scrollTo({ top: Math.max(0, target.offsetTop - 16), behavior: "smooth" });
  }, [lastId, lastRole]);
  useEffect(() => {
    const box = scroller.current;
    if (!box || (!streaming && pending === null)) return;
    box.scrollTo({ top: box.scrollHeight, behavior: "smooth" });
    // и пока пишется черновик ответа — примерно каждые 200 символов, чтобы текст оставался на экране
  }, [pending, streaming, progress.length, lastId, Math.floor((draft?.text.length ?? 0) / 200)]);

  // Заметка внизу (скопировано, оценка, задачи) исчезает сама.
  useEffect(() => {
    if (!notice) return;
    const timer = setTimeout(() => setNotice(null), 4000);
    return () => clearTimeout(timer);
  }, [notice]);

  const send = async (text: string) => {
    const question = text.trim();
    if (!question || streaming) return;
    setInput("");
    setError(null);
    setFailure(null);
    setNotice(null);
    setPending(question);
    setProgress([]);
    setDraft(null);
    setStreaming(true);
    const controller = new AbortController();
    abort.current = controller;
    let started = false;
    try {
      await streamChat(
        activeId,
        question,
        (e) => {
          switch (e.type) {
            case "start":
              started = true;
              if (!activeId) {
                setActiveId(e.data.conversationId);
                setTitle(e.data.title);
                window.history.replaceState(null, "", `/consultant/${e.data.conversationId}`);
              }
              setList((prev) => [
                { id: e.data.conversationId, title: e.data.title, createdAt: e.data.userMessage.createdAt, lastMessageAt: e.data.userMessage.createdAt, source: "web" },
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
            case "draft":
              setDraft((prev) => nextDraft(prev, e.data));
              break;
            case "done":
              // Проверенный ответ (отчёт из блоков) заменяет черновик.
              setDraft(null);
              setMessages((prev) => [...prev, e.data.message]);
              if (voiceModeRef.current && e.data.message.status === "completed") void speak(e.data.message.id);
              break;
            case "error":
              setDraft(null);
              if (e.data.message) setMessages((prev) => [...prev, e.data.message!]);
              else setFailure({ question, error: e.data.error });
              break;
          }
        },
        t,
        { context: contextPayload(context), signal: controller.signal },
      );
    } catch (e) {
      if ((e as Error).name === "AbortError") {
        setNotice(t("Ответ остановлен. Сервер может дописать его — тогда он появится в чате после обновления страницы."));
      } else {
        setFailure({ question, error: (e as Error).message });
        if (!started) setInput(question);
      }
    } finally {
      abort.current = null;
      setStreaming(false);
      setPending(null);
      setProgress([]);
      setDraft(null);
    }
  };

  const stop = () => abort.current?.abort();
  const waiting = streaming && (messages.length === 0 || messages[messages.length - 1].role === "user");
  const empty = messages.length === 0 && !streaming && pending === null;

  /** Вопрос, на который дан ответ: последнее сообщение пользователя перед ним. */
  const questionOf = (id: string) => {
    const i = messages.findIndex((m) => m.id === id);
    for (let j = i - 1; j >= 0; j--) if (messages[j].role === "user") return messages[j].content;
    return null;
  };

  // ---------- Чаты: переименовать, закрепить, удалить ----------
  const rename = async (id: string, next: string) => {
    try {
      await bff(`ai/conversations/${id}`, { method: "PATCH", body: JSON.stringify({ title: next }) });
      setList((prev) => prev.map((c) => (c.id === id ? { ...c, title: next } : c)));
      if (id === activeId) setTitle(next);
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const remove = async (id: string) => {
    if (!confirm(t("Убрать чат из истории?"))) return;
    try {
      await bff(`ai/conversations/${id}`, { method: "DELETE" });
      setList((prev) => prev.filter((c) => c.id !== id));
      if (id === activeId) router.push("/consultant");
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const togglePin = (id: string) =>
    setPinned((prev) => {
      const next = new Set(prev);
      if (!next.delete(id)) next.add(id);
      writeJson(pinnedKey, [...next]);
      return next;
    });

  const toggleCollapsed = () =>
    setCollapsed((v) => {
      writeJson(collapsedKey, !v);
      return !v;
    });

  useEffect(() => {
    if (!headerMenu) return;
    const close = (e: Event) => {
      if (e instanceof KeyboardEvent ? e.key === "Escape" : !headerMenuRef.current?.contains(e.target as Node)) setHeaderMenu(false);
    };
    document.addEventListener("pointerdown", close);
    document.addEventListener("keydown", close);
    return () => {
      document.removeEventListener("pointerdown", close);
      document.removeEventListener("keydown", close);
    };
  }, [headerMenu]);

  // ---------- Копировать, поделиться, оценка ----------
  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setNotice(t("Скопировано"));
    } catch {
      setError(t("Не удалось скопировать — браузер не дал доступ к буферу обмена."));
    }
  };

  const share = async (text: string) => {
    if (typeof navigator.share === "function") {
      try {
        await navigator.share({ title: chatTitle(title), text });
        return;
      } catch (e) {
        if ((e as Error).name === "AbortError") return;
      }
    }
    await copy(text);
  };

  const lastAnswer = [...messages].reverse().find((m) => m.role === "assistant" && m.status === "completed");

  const rate = async (id: string, rating: "up" | "down", comment?: string) => {
    try {
      await bff(`ai/messages/${id}/feedback`, { method: "POST", body: JSON.stringify({ rating, comment: comment ?? null }) });
      setFeedback((prev) => {
        const next = { ...prev, [id]: rating };
        writeJson(feedbackKey, next);
        return next;
      });
      setNotice(t("Спасибо за оценку"));
    } catch (e) {
      setError((e as Error).message);
    }
  };

  // ---------- Задачи из рекомендаций ----------
  const loadWork = useCallback(async () => {
    if (work) return work;
    const [departments, projects] = await Promise.all([bff<DepartmentTasks[]>("work/departments"), bff<WorkProject[]>("work/projects")]);
    const loaded = { departments, projects };
    setWork(loaded);
    return loaded;
  }, [work]);

  const markCreated = (messageId: string, index: number, taskId: string) =>
    setCreated((prev) => {
      const next = { ...prev, [messageId]: { ...prev[messageId], [index]: taskId } };
      writeJson(tasksKey, next);
      return next;
    });

  const chatLink = () => (activeId ? `${window.location.origin}/consultant/${activeId}` : window.location.href);

  const createTask = async (messageId: string, item: RecommendationItem, index: number) => {
    setError(null);
    try {
      await loadWork();
      setTaskForm({ messageId, index, draft: taskDraft(item, { url: chatLink(), title: chatTitle(title) }) });
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const openBulk = async (messageId: string, items: RecommendationItem[]) => {
    setError(null);
    try {
      await loadWork();
      setBulk({ messageId, items });
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const titleText = activeId ? chatTitle(title) : t("AI-консультант");

  // ---------- Разметка ----------
  const sidebar = (overlay: boolean) => (
    <ChatSidebar
      list={list}
      activeId={activeId}
      pinned={pinned}
      onTogglePin={togglePin}
      onRename={rename}
      onDelete={(id) => void remove(id)}
      onNavigate={overlay ? () => setHistoryOpen(false) : undefined}
      onClose={overlay ? () => setHistoryOpen(false) : undefined}
    />
  );

  const voiceButton = (caps.listen || caps.speak) && (
    <button
      type="button"
      onClick={toggleVoiceMode}
      aria-pressed={voiceMode}
      title={voiceMode ? t("Голосовой режим включён: вопрос голосом отправляется сразу, ответ читается вслух") : t("Включить голосовой режим")}
      className={`inline-flex items-center gap-2 rounded-[10px] border px-[13px] py-2 text-[13px] font-medium max-lg:size-10 max-lg:justify-center max-lg:rounded-full max-lg:p-0 ${
        voiceMode ? "border-accent bg-accent-soft text-link" : "border-line bg-surface text-ink-2 hover:text-ink"
      }`}
    >
      <AudioLines className="size-4" aria-hidden />
      <span className="max-lg:sr-only">{t("Голос")}</span>
    </button>
  );

  return (
    <div className="ai-chat relative flex h-full overflow-hidden">
      {/* Колонка чатов — на компьютере; свёрнута или телефон — шторка «История». */}
      {!collapsed && (
        <aside className="hidden w-[300px] shrink-0 border-r border-line bg-[var(--ai-sidebar)] backdrop-blur-xl lg:block" aria-label={t("Чаты консультанта")}>
          {sidebar(false)}
        </aside>
      )}
      {historyOpen && (
        <div className="fixed inset-0 z-50">
          <button type="button" aria-label={t("Закрыть список чатов")} className="absolute inset-0 bg-black/50 max-lg:hidden" onClick={() => setHistoryOpen(false)} />
          <div className="ai-chat absolute inset-y-0 left-0 w-full bg-ai-bg pt-[env(safe-area-inset-top)] shadow-2xl lg:w-[320px] lg:border-r lg:border-line">{sidebar(true)}</div>
        </div>
      )}

      <section className="relative flex min-w-0 flex-1 flex-col">
        {/* Шапка чата: на компьютере — 68px с кнопками; на телефоне — вместо глобальной шапки, 56px и круглые кнопки. */}
        <header className="flex shrink-0 items-center gap-3 border-b border-line px-7 lg:h-[68px] max-lg:gap-2.5 max-lg:px-3 max-lg:pb-2 max-lg:pt-[calc(env(safe-area-inset-top)+8px)]">
          {activeId ? (
            <Link href="/consultant" aria-label={t("Назад к новому чату")} className="grid size-10 shrink-0 place-items-center rounded-full border border-line bg-surface text-ink-2 lg:hidden">
              <ChevronLeft className="size-5" aria-hidden />
            </Link>
          ) : (
            <button type="button" onClick={() => setHistoryOpen(true)} aria-label={t("История чатов")} className="grid size-10 shrink-0 place-items-center rounded-full border border-line bg-surface text-ink-2 lg:hidden">
              <History className="size-[18px]" aria-hidden />
            </button>
          )}
          <button
            type="button"
            onClick={() => (collapsed ? (historyOpen ? setHistoryOpen(false) : toggleCollapsed()) : toggleCollapsed())}
            aria-label={collapsed ? t("Показать список чатов") : t("Свернуть список чатов")}
            className="hidden size-9 shrink-0 place-items-center rounded-[10px] text-ink-3 hover:bg-surface hover:text-ink lg:grid"
          >
            {collapsed ? <PanelLeftOpen className="size-[18px]" aria-hidden /> : <PanelLeftClose className="size-[18px]" aria-hidden />}
          </button>
          {collapsed && (
            <button type="button" onClick={() => setHistoryOpen(true)} aria-label={t("История чатов")} className="hidden size-9 shrink-0 place-items-center rounded-[10px] text-ink-3 hover:bg-surface hover:text-ink lg:grid">
              <Search className="size-4" aria-hidden />
            </button>
          )}
          <span className="hidden size-[38px] shrink-0 place-items-center rounded-[10px] bg-violet-soft text-violet lg:grid">
            <Sparkles className="size-[18px]" aria-hidden />
          </span>
          <div className="min-w-0 flex-1">
            {editingTitle && activeId ? (
              <RenameInput
                initial={title}
                label={t("Название чата")}
                onCancel={() => setEditingTitle(false)}
                onDone={(value) => {
                  setEditingTitle(false);
                  if (value !== title) void rename(activeId, value);
                }}
                className="w-full rounded-md bg-surface-2 px-1.5 text-base font-bold text-ink focus:outline-none"
              />
            ) : (
              <h1 className="truncate text-base font-bold text-ink">
                {activeId ? (
                  <button type="button" onClick={() => setEditingTitle(true)} title={t("Переименовать чат")} className="max-w-full truncate text-left hover:text-link">
                    {titleText}
                  </button>
                ) : (
                  titleText
                )}
              </h1>
            )}
            <div className="mt-0.5 flex min-w-0 items-center gap-1.5 text-[12px] max-lg:text-[11px]">
              <span className={`size-[7px] shrink-0 rounded-full ${status.ready ? "bg-ok" : "bg-warn"}`} aria-hidden />
              <span className="shrink-0 text-ink-3 max-lg:shrink max-lg:truncate">{status.ready ? t("подключён к данным OneBase") : t("не настроен")}</span>
              {status.ready && sources.length > 0 && <span className="hidden min-w-0 truncate text-subtle lg:inline">· {sources.map((s) => t(s)).join(", ")}</span>}
            </div>
          </div>

          <div className="flex shrink-0 items-center gap-2">
            {voiceButton}
            <Link href="/ai" className="hidden items-center gap-2 rounded-[10px] border border-line bg-surface px-[13px] py-2 text-[13px] font-medium text-ink-2 hover:text-ink lg:inline-flex">
              <LayoutDashboard className="size-4" aria-hidden />
              AI Dashboard
            </Link>
            <button
              type="button"
              disabled={!lastAnswer}
              onClick={() => lastAnswer && void share(answerText(lastAnswer.answer, lastAnswer.content))}
              className="hidden items-center gap-2 rounded-[10px] border border-line bg-surface px-[13px] py-2 text-[13px] font-medium text-ink-2 hover:text-ink disabled:opacity-50 lg:inline-flex"
            >
              <Share2 className="size-4" aria-hidden />
              {t("Поделиться")}
            </button>
            <div ref={headerMenuRef} className="relative hidden lg:block">
              <button
                type="button"
                aria-label={t("Ещё действия с чатом")}
                aria-haspopup="menu"
                aria-expanded={headerMenu}
                onClick={() => setHeaderMenu((v) => !v)}
                className="grid size-[38px] place-items-center rounded-[10px] border border-line bg-surface text-ink-2 hover:text-ink"
              >
                <MoreHorizontal className="size-4" aria-hidden />
              </button>
              {headerMenu && (
                <div role="menu" className="absolute right-0 top-11 z-30 w-56 rounded-xl border border-line-strong bg-surface-solid p-1 shadow-xl">
                  <MenuItem
                    icon={FileDown}
                    label={t("Экспорт чата в PDF")}
                    onClick={() => {
                      setHeaderMenu(false);
                      printElement(feed.current);
                    }}
                  />
                  {activeId && (
                    <MenuItem
                      icon={Pencil}
                      label={t("Переименовать")}
                      onClick={() => {
                        setHeaderMenu(false);
                        setEditingTitle(true);
                      }}
                    />
                  )}
                  {activeId && (
                    <MenuItem
                      icon={Trash2}
                      label={t("Удалить чат")}
                      danger
                      onClick={() => {
                        setHeaderMenu(false);
                        void remove(activeId);
                      }}
                    />
                  )}
                </div>
              )}
            </div>
            <Link href="/consultant" aria-label={t("Новый чат")} className="grid size-10 place-items-center rounded-full border border-line bg-surface text-ink-2 lg:hidden">
              <SquarePen className="size-[18px]" aria-hidden />
            </Link>
          </div>
        </header>

        <div ref={scroller} className="min-h-0 flex-1 overflow-y-auto overscroll-contain">
          <div
            ref={feed}
            role="log"
            aria-live="polite"
            aria-label={t("Переписка с консультантом")}
            className={`relative mx-auto flex min-h-full w-full max-w-[888px] flex-col gap-5 px-4 pt-7 lg:px-6 ${empty ? "justify-center max-lg:justify-start" : ""}`}
            style={{ paddingBottom: composerHeight + 28 }}
          >
            {!status.ready && (
              <div className="flex items-start gap-3 rounded-xl bg-warn-soft px-4 py-3 text-sm" data-ai-no-print>
                <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warn" aria-hidden />
                <div>
                  <div className="font-semibold text-ink">{t("Консультант пока не работает")}</div>
                  <div className="mt-0.5 text-ink-2">{status.message && t(status.message)}</div>
                  {canConfigure && (
                    <Link href="/settings/ai" className="mt-1 inline-block font-medium text-link hover:underline">
                      {t("Открыть «Настройки → AI»")}
                    </Link>
                  )}
                </div>
              </div>
            )}

            {empty && <StartScreen questions={questions} disabled={!status.ready} onAsk={(q) => void send(q)} />}

            {messages.map((m, i) =>
              m.role === "user" ? (
                <UserBubble key={m.id} id={m.id} text={m.content} />
              ) : (
                <AssistantMessage
                  key={m.id}
                  message={m}
                  last={i === messages.length - 1 && !streaming}
                  speech={caps.speak ? (speaking?.id === m.id ? speaking.state : "idle") : null}
                  onSpeak={() => void speak(m.id)}
                  onCopy={() => void copy(answerText(m.answer, m.content))}
                  onShare={() => void share(answerText(m.answer, m.content))}
                  onRetry={() => {
                    const q = questionOf(m.id);
                    if (q) void send(q);
                  }}
                  onAsk={(q) => void send(q)}
                  rating={feedback[m.id] ?? null}
                  onRate={(r, c) => void rate(m.id, r, c)}
                  created={created[m.id] ?? {}}
                  canTasks={canTasks}
                  onCreateTask={(item, index) => void createTask(m.id, item, index)}
                  onCreateAll={(items) => void openBulk(m.id, items)}
                  busy={streaming}
                />
              ),
            )}
            {pending && <UserBubble text={pending} />}
            {waiting && <PendingAnswer steps={progress} draft={draft} />}
            {failure && (
              <ErrorCard
                text={failure.error}
                onRetry={() => {
                  const q = failure.question;
                  setFailure(null);
                  void send(q);
                }}
              />
            )}
          </div>
        </div>

        {/* Поле вопроса — закреплено снизу; под ним градиент в цвет фона (не ловит нажатия). */}
        <div className="pointer-events-none absolute inset-x-0 bottom-0 z-10" data-ai-no-print>
          <div aria-hidden className="absolute inset-x-0 bottom-0 h-[150px] bg-gradient-to-b from-transparent to-ai-bg max-lg:h-[230px]" />
          <div ref={composerBox} className="pointer-events-auto relative px-3 pb-3 lg:px-6 max-lg:pb-[22px]">
            <div className="mx-auto max-w-[840px] space-y-2">
              {(recording || transcribing) && (
                <div className="flex items-center gap-3 rounded-xl bg-bad-soft px-3 py-2 text-sm text-ink" role="status">
                  {recording ? (
                    <>
                      <span className="size-2.5 animate-pulse rounded-full bg-bad" aria-hidden />
                      <span className="flex-1">
                        {t("Слушаю… {time}", { time: `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}` })}
                        <span className="ml-2 text-ink-3">
                          {t("нажмите ■, когда закончите")}
                          {voiceMode ? ` — ${t("вопрос уйдёт сразу")}` : ""}
                        </span>
                      </span>
                      <button type="button" onClick={cancelRecording} className="text-ink-3 hover:text-bad" aria-label={t("Отменить запись")}>
                        <X className="size-4" aria-hidden />
                      </button>
                    </>
                  ) : (
                    <>
                      <Loader2 className="size-4 animate-spin text-link" aria-hidden />
                      <span>{t("Распознаю речь…")}</span>
                    </>
                  )}
                </div>
              )}
              {error && (
                <div className="flex items-start gap-2 rounded-xl bg-bad-soft px-3 py-2 text-sm text-bad" role="alert">
                  <span className="flex-1">{t(error)}</span>
                  <button type="button" aria-label={t("Скрыть")} onClick={() => setError(null)}>
                    <X className="size-4" aria-hidden />
                  </button>
                </div>
              )}
              {notice && (
                <div className="rounded-xl border border-line bg-surface-2 px-3 py-2 text-[13px] text-ink-2" role="status">
                  {notice}
                </div>
              )}
            </div>
            <div className="mt-2">
              <Composer
                value={input}
                onChange={setInput}
                onSubmit={() => void send(input)}
                onStop={stop}
                busy={streaming}
                disabled={!status.ready}
                inChat={!empty}
                context={context}
                defaultPeriod={defaultPeriod}
                onContext={setContext}
                textareaRef={textarea}
                mic={caps.listen ? { available: true, recording, transcribing, onClick: () => void (recording ? finishRecording() : startRecording()) } : null}
              />
            </div>
          </div>
        </div>
      </section>

      {work && taskForm && (
        <TaskForm
          open
          onClose={() => setTaskForm(null)}
          departments={work.departments}
          projects={work.projects}
          prefill={taskForm.draft}
          onCreated={(id) => {
            markCreated(taskForm.messageId, taskForm.index, id);
            setNotice(t("Задача создана"));
          }}
        />
      )}
      {work && bulk && (
        <BulkTasks
          items={bulk.items}
          created={created[bulk.messageId] ?? {}}
          departments={work.departments.filter((d) => d.hasTasks)}
          onClose={() => setBulk(null)}
          chat={{ url: chatLink(), title: chatTitle(title) }}
          onCreated={(index, id) => markCreated(bulk.messageId, index, id)}
          onDone={(n) => setNotice(t("Создано задач: {n}", { n }))}
        />
      )}
    </div>
  );
}

function UserBubble({ text, id }: { text: string; id?: string }) {
  return (
    <div className="flex justify-end" data-message-id={id}>
      <div className="max-w-[85%] whitespace-pre-wrap rounded-[18px_18px_4px_18px] bg-accent-fill px-4 py-2.5 text-sm font-medium text-white">{text}</div>
    </div>
  );
}

function Avatar({ small, danger }: { small?: boolean; danger?: boolean }) {
  return (
    <span
      className={`grid shrink-0 place-items-center rounded-full text-white ${danger ? "bg-bad" : "ai-gradient"} ${small ? "size-[26px]" : "size-[34px]"}`}
      aria-hidden
    >
      {danger ? <AlertTriangle className={small ? "size-3.5" : "size-4"} /> : <Sparkles className={small ? "size-3.5" : "size-4"} />}
    </span>
  );
}

/**
 * Пока ответ готовится: строка статуса (какой шаг идёт), шаги AI-сотрудников и скелетон карточки отчёта; когда консультант пишет ответ —
 * его черновик потоком (приглушён и подписан: числа ещё не сверены с данными). Ответ (done) заменяет черновик карточкой отчёта.
 */
function PendingAnswer({ steps, draft }: { steps: ConsultantProgress[]; draft: Draft | null }) {
  const t = useT();
  const step = currentStep(steps);
  const agents = steps.filter((s) => s.agentName);
  return (
    <div className="flex gap-3" aria-busy="true">
      <span className="max-lg:hidden">
        <Avatar />
      </span>
      <div className="min-w-0 flex-1 space-y-3">
        <div className="flex items-center gap-2 text-[13px] text-ink-2" role="status">
          <Loader2 className="size-4 animate-spin text-link" aria-hidden />
          {t(step.key, { agent: step.agentName ? t(step.agentName) : "" })}…
        </div>
        {agents.length > 0 && (
          <ul className="flex flex-wrap gap-1.5">
            {agents.map((s) => (
              <li
                key={`${s.stage}:${s.agent}`}
                className={`inline-flex items-center gap-1.5 rounded-[14px] border border-line px-2.5 py-1 text-[12px] ${s.status === "Running" ? "text-ink" : "text-ink-3"}`}
              >
                {s.status === "Running" ? <Loader2 className="size-3 animate-spin" aria-hidden /> : s.status === "Done" ? <Check className="size-3 text-ok" aria-hidden /> : <span className="size-1.5 rounded-full bg-subtle" aria-hidden />}
                {t(s.agentName!)}
              </li>
            ))}
          </ul>
        )}
        {draft ? (
          <div className="rounded-[18px] border border-dashed border-line-strong bg-surface p-5 max-lg:p-4">
            <div className="mb-2 flex items-center gap-1.5 text-[12px] font-medium text-warn">
              {draft.state === "writing" ? <AlertTriangle className="size-3.5" aria-hidden /> : <Loader2 className="size-3.5 animate-spin" aria-hidden />}
              {t(draftLabel[draft.state])}
            </div>
            {draft.text && (
              <div className="text-ink-2 opacity-80">
                <Markdown text={draft.text} />
              </div>
            )}
          </div>
        ) : (
        <div className="space-y-4 rounded-[18px] border border-line bg-surface p-6 max-lg:p-4">
          <div className="h-24 animate-pulse rounded-[14px] bg-surface-2" />
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            {[0, 1, 2, 3].map((i) => (
              <div key={i} className="h-[74px] animate-pulse rounded-xl bg-surface-2" />
            ))}
          </div>
          <div className="space-y-2">
            {[0, 1, 2].map((i) => (
              <div key={i} className="h-9 animate-pulse rounded-lg bg-surface-2" />
            ))}
          </div>
        </div>
        )}
      </div>
    </div>
  );
}

function ErrorCard({ text, onRetry, id }: { text: string | null; onRetry: (() => void) | null; id?: string }) {
  const t = useT();
  return (
    <div className="flex gap-3" data-message-id={id}>
      <span className="max-lg:hidden">
        <Avatar danger />
      </span>
      <div className="min-w-0 flex-1 rounded-[18px] bg-bad-soft px-4 py-3 text-sm" role="alert">
        <div className="font-semibold text-bad">{t("Не удалось получить ответ")}</div>
        {text && <div className="mt-1 text-ink-2">{t(text)}</div>}
        {onRetry && (
          <button type="button" onClick={onRetry} className="mt-2.5 inline-flex items-center gap-1.5 rounded-lg border border-bad/30 px-3 py-1.5 text-[13px] font-medium text-bad hover:bg-bad/10">
            <RefreshCw className="size-3.5" aria-hidden />
            {t("Повторить")}
          </button>
        )}
      </div>
    </div>
  );
}

function AssistantMessage({
  message,
  last,
  speech,
  onSpeak,
  onCopy,
  onShare,
  onRetry,
  onAsk,
  rating,
  onRate,
  created,
  canTasks,
  onCreateTask,
  onCreateAll,
  busy,
}: {
  message: ChatMessage;
  last: boolean;
  speech: SpeechState | null;
  onSpeak: () => void;
  onCopy: () => void;
  onShare: () => void;
  onRetry: () => void;
  onAsk: (q: string) => void;
  rating: "up" | "down" | null;
  onRate: (rating: "up" | "down", comment?: string) => void;
  created: CreatedTasks;
  canTasks: boolean;
  onCreateTask: (item: RecommendationItem, index: number) => void;
  onCreateAll: (items: RecommendationItem[]) => void;
  busy: boolean;
}) {
  const t = useT();
  const [details, setDetails] = useState(false);
  const [complaint, setComplaint] = useState(false);
  const self = useRef<HTMLElement>(null);

  if (message.status === "failed") return <ErrorCard id={message.id} text={message.error} onRetry={busy ? null : onRetry} />;

  const answer = message.answer;
  const report = !!answer?.report;
  const meta = answer?.meta;
  const recommendations = answer?.blocks.flatMap((b) => (b.type === "recommendations" ? b.items : [])) ?? [];
  const open = recommendations.filter((_, i) => !created[i]);
  const seconds = `${formatNumber(message.durationMs / 1000, 1)} ${t("с")}`;
  const asOf = meta?.dataAsOf ? /^(\d{4})-(\d{2})-(\d{2})$/.exec(meta.dataAsOf) : null;
  const metaLine = [meta?.model ?? message.details?.model, seconds, asOf ? t("данные на {date}", { date: `${asOf[3]}.${asOf[2]}` }) : null].filter(Boolean).join(" · ");
  const chips = report && meta
    ? [
        meta.departments > 0 && { icon: Building2, text: t(plural(meta.departments, "{n} подразделение", "{n} подразделения", "{n} подразделений"), { n: meta.departments }) },
        meta.facts > 0 && { icon: Search, text: t(plural(meta.facts, "{n} факт и проблема", "{n} факта и проблемы", "{n} фактов и проблем"), { n: meta.facts }) },
        meta.recommendations > 0 && { icon: Lightbulb, text: t(plural(meta.recommendations, "{n} рекомендация", "{n} рекомендации", "{n} рекомендаций"), { n: meta.recommendations }) },
      ].filter((x): x is { icon: typeof Search; text: string } => !!x)
    : [];
  const iconButton = "inline-flex items-center gap-1.5 rounded-lg px-2 py-1.5 text-[13px] font-medium text-ink-2 hover:bg-surface-2 hover:text-ink";
  const round = "grid size-[34px] place-items-center rounded-full border border-line bg-surface text-ink-2";

  return (
    <article ref={self} data-message-id={message.id} className="flex gap-3" aria-label={t("Ответ консультанта")}>
      <span className="max-lg:hidden">
        <Avatar />
      </span>
      <div className="min-w-0 flex-1 space-y-3">
        {/* Компьютер: чипы «подразделения · факты · рекомендации»; телефон: строка «AI-консультант · …». */}
        {chips.length > 0 && (
          <ul className="flex flex-wrap gap-2 max-lg:hidden">
            {chips.map((c) => (
              <li key={c.text} className="inline-flex items-center gap-1.5 rounded-[14px] border border-line bg-surface px-3 py-1 text-[12px] text-ink-2">
                <c.icon className="size-[13px] text-ink-3" aria-hidden />
                {c.text}
              </li>
            ))}
          </ul>
        )}
        <div className="flex items-center gap-2 lg:hidden">
          <Avatar small />
          <span className="text-[13px] font-semibold text-ink">{t("AI-консультант")}</span>
          {report && meta && (meta.facts > 0 || meta.recommendations > 0) && (
            <span className="truncate text-[12px] text-subtle">
              {meta.facts > 0 && ` · ${t(plural(meta.facts, "{n} факт", "{n} факта", "{n} фактов"), { n: meta.facts })}`}
              {meta.recommendations > 0 && ` · ${t(plural(meta.recommendations, "{n} рекомендация", "{n} рекомендации", "{n} рекомендаций"), { n: meta.recommendations })}`}
            </span>
          )}
        </div>

        {report && answer ? (
          <AnswerCard answer={answer} created={created} onCreateTask={canTasks ? onCreateTask : null} />
        ) : (
          <div className="rounded-[18px] border border-line bg-surface px-4 py-3 lg:px-5 lg:py-4">
            {/* Ответ модели — как есть (он уже на языке интерфейса); служебная пометка сервера — переводится. */}
            <Markdown text={message.content === emptyAnswer ? t(emptyAnswer) : message.content} />
          </div>
        )}

        {message.details?.reports?.map((r) => <ReportCard key={r.id} report={r} />)}
        {message.details?.grounding?.warned && (
          <div className="rounded-xl bg-warn-soft px-3 py-2 text-[12px]">
            <Grounding details={message.details} />
          </div>
        )}

        {/* Действия: на компьютере — подписи и справа модель, время, дата данных; на телефоне — круглые кнопки. */}
        <div className="flex flex-wrap items-center gap-x-1 gap-y-1 max-lg:hidden" data-ai-no-print>
          <button type="button" className={iconButton} onClick={onCopy} aria-label={t("Копировать ответ")}>
            <Copy className="size-4" aria-hidden />
          </button>
          {speech && (
            <button type="button" className={`${iconButton} ${speech !== "idle" ? "bg-accent-soft text-link" : ""}`} onClick={onSpeak}>
              {speech === "loading" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : speech === "playing" ? <Square className="size-3.5 fill-current" aria-hidden /> : <Volume2 className="size-4" aria-hidden />}
              {speech === "playing" ? t("Остановить") : t("Озвучить")}
            </button>
          )}
          <button type="button" className={`${iconButton} ${rating === "up" ? "text-ok" : ""}`} aria-pressed={rating === "up"} aria-label={t("Хороший ответ")} onClick={() => onRate("up")}>
            <ThumbsUp className="size-4" aria-hidden />
          </button>
          <button type="button" className={`${iconButton} ${rating === "down" ? "text-bad" : ""}`} aria-pressed={rating === "down"} aria-label={t("Плохой ответ")} onClick={() => setComplaint((v) => !v)}>
            <ThumbsDown className="size-4" aria-hidden />
          </button>
          <button type="button" className={iconButton} aria-label={t("Повторить вопрос")} onClick={onRetry} disabled={busy}>
            <RefreshCw className="size-4" aria-hidden />
          </button>
          <button type="button" className={iconButton} onClick={() => printElement(self.current)}>
            <FileDown className="size-4" aria-hidden />
            PDF
          </button>
          {canTasks && report && open.length > 0 && (
            <button type="button" className={iconButton} onClick={() => onCreateAll(recommendations)}>
              <ListChecks className="size-4" aria-hidden />
              {t(plural(open.length, "Создать {n} задачу", "Создать {n} задачи", "Создать {n} задач"), { n: open.length })}
            </button>
          )}
          <button type="button" onClick={() => setDetails((v) => !v)} aria-expanded={details} className="ml-auto truncate text-[11px] text-subtle hover:text-ink-2" title={t("Как получен ответ")}>
            {metaLine}
          </button>
        </div>
        <div className="flex items-center gap-2 lg:hidden" data-ai-no-print>
          <button type="button" className={round} onClick={onCopy} aria-label={t("Копировать ответ")}>
            <Copy className="size-4" aria-hidden />
          </button>
          {speech && (
            <button type="button" className={`${round} ${speech !== "idle" ? "border-accent text-link" : ""}`} onClick={onSpeak} aria-label={speech === "idle" ? t("Озвучить ответ") : t("Остановить озвучку")}>
              {speech === "loading" ? <Loader2 className="size-4 animate-spin" aria-hidden /> : speech === "playing" ? <Square className="size-3 fill-current" aria-hidden /> : <Volume2 className="size-4" aria-hidden />}
            </button>
          )}
          <button type="button" className={`${round} ${rating === "up" ? "text-ok" : ""}`} aria-pressed={rating === "up"} aria-label={t("Хороший ответ")} onClick={() => onRate("up")}>
            <ThumbsUp className="size-4" aria-hidden />
          </button>
          <button type="button" className={`${round} ${rating === "down" ? "text-bad" : ""}`} aria-pressed={rating === "down"} aria-label={t("Плохой ответ")} onClick={() => setComplaint((v) => !v)}>
            <ThumbsDown className="size-4" aria-hidden />
          </button>
          <button type="button" className={round} onClick={onShare} aria-label={t("Поделиться ответом")}>
            <Share2 className="size-4" aria-hidden />
          </button>
          <button type="button" onClick={() => setDetails((v) => !v)} aria-expanded={details} className="ml-auto text-[12px] tabular-nums text-subtle" title={t("Как получен ответ")}>
            {seconds}
          </button>
        </div>

        {complaint && (
          <Complaint
            onCancel={() => setComplaint(false)}
            onSend={(comment) => {
              setComplaint(false);
              onRate("down", comment);
            }}
          />
        )}
        {details && message.details && <DetailsPanel details={message.details} />}

        {last && answer && answer.followUps.length > 0 && (
          <ul className="flex flex-wrap gap-2 max-lg:-mx-4 max-lg:flex-nowrap max-lg:overflow-x-auto max-lg:px-4 max-lg:no-scrollbar" aria-label={t("Уточняющие вопросы")} data-ai-no-print>
            {answer.followUps.map((q) => (
              <li key={q} className="shrink-0">
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => onAsk(q)}
                  className="inline-flex items-center gap-1.5 whitespace-nowrap rounded-[14px] border border-accent/25 bg-accent-soft px-3 py-1.5 text-[12px] font-medium text-link hover:border-accent/50 disabled:opacity-50"
                >
                  <CornerDownRight className="size-3.5" aria-hidden />
                  {q}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </article>
  );
}

/** 👎 — короткая форма «Что не так?»: комментарий уходит вместе с оценкой (журнал аудита). */
function Complaint({ onSend, onCancel }: { onSend: (comment: string) => void; onCancel: () => void }) {
  const t = useT();
  const [text, setText] = useState("");
  return (
    <form
      className="rounded-2xl border border-line bg-surface p-3"
      onSubmit={(e) => {
        e.preventDefault();
        onSend(text.trim());
      }}
    >
      <label className="text-[13px] font-semibold text-ink" htmlFor="ai-complaint">
        {t("Что не так?")}
      </label>
      <textarea
        id="ai-complaint"
        autoFocus
        rows={2}
        maxLength={1000}
        value={text}
        onChange={(e) => setText(e.target.value)}
        placeholder={t("Например: цифра не совпадает с отчётом, не тот период, не ответил на вопрос")}
        className="mt-2 w-full resize-none rounded-lg border border-line bg-surface-2 px-3 py-2 text-[13px] text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none"
      />
      <div className="mt-2 flex justify-end gap-2">
        <button type="button" onClick={onCancel} className="rounded-lg px-3 py-1.5 text-[13px] text-ink-2 hover:bg-surface-2">
          {t("Отмена")}
        </button>
        <button type="submit" className="rounded-lg bg-accent-fill px-3 py-1.5 text-[13px] font-semibold text-white">
          {t("Отправить")}
        </button>
      </div>
    </form>
  );
}

/** «Создать N задач»: подтверждение со списком; отдел — из рекомендации, если он доступен, иначе выбранный. Создаются по очереди. */
function BulkTasks({
  items,
  created,
  departments,
  chat,
  onClose,
  onCreated,
  onDone,
}: {
  items: RecommendationItem[];
  created: CreatedTasks;
  departments: DepartmentTasks[];
  chat: { url: string; title: string };
  onClose: () => void;
  onCreated: (index: number, id: string) => void;
  onDone: (n: number) => void;
}) {
  const t = useT();
  const codes = new Set(departments.map((d) => d.code));
  const [fallback, setFallback] = useState(departments.find((d) => d.isMine)?.code ?? departments[0]?.code ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const todo = items.map((item, index) => ({ item, index })).filter((x) => !created[x.index]);
  const needsFallback = todo.some((x) => !x.item.department || !codes.has(x.item.department));

  const run = async () => {
    setBusy(true);
    setError(null);
    let n = 0;
    try {
      for (const { item, index } of todo) {
        const draft = taskDraft(item, chat);
        const department = draft.department && codes.has(draft.department) ? draft.department : fallback;
        const { id } = await bff<{ id: string }>("work/tasks", {
          method: "POST",
          body: JSON.stringify({ department, title: draft.title, description: draft.description, priority: "Medium", dueDate: draft.dueDate }),
        });
        onCreated(index, id);
        n++;
      }
      onDone(n);
      onClose();
    } catch (e) {
      setError(`${(e as Error).message}${n > 0 ? ` ${t("Создано до ошибки: {n}.", { n })}` : ""}`);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet
      open
      onClose={onClose}
      title={t(plural(todo.length, "Создать {n} задачу", "Создать {n} задачи", "Создать {n} задач"), { n: todo.length })}
      subtitle={t("По рекомендациям ответа консультанта")}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} disabled={busy} className="rounded-lg px-3 py-2 text-sm text-ink-2 hover:bg-muted">
            {t("Отмена")}
          </button>
          <button type="button" onClick={() => void run()} disabled={busy || todo.length === 0 || (needsFallback && !fallback)} className="inline-flex items-center gap-2 rounded-lg bg-accent-fill px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("Создать")}
          </button>
        </div>
      }
    >
      <ol className="space-y-2">
        {todo.map(({ item, index }) => (
          <li key={index} className="rounded-xl border border-line px-3 py-2">
            <div className="text-sm font-semibold text-ink">{item.title}</div>
            {item.text && <div className="mt-0.5 text-[13px] text-ink-3">{item.text}</div>}
            <div className="mt-1 text-[12px] text-ink-3">
              {t("Отдел")}: {item.department && codes.has(item.department) ? t(departmentNames[item.department] ?? item.department) : t("выбранный ниже")}
              {item.dueDate ? ` · ${t("срок")} ${item.dueDate.split("-").reverse().join(".")}` : ""}
            </div>
          </li>
        ))}
      </ol>
      {needsFallback && (
        <label className="mt-3 block text-sm">
          <span className="mb-1 block font-medium text-ink">{t("Отдел для задач без отдела")}</span>
          <select value={fallback} onChange={(e) => setFallback(e.target.value)} className="w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink">
            {departments.map((d) => (
              <option key={d.code} value={d.code}>
                {d.name}
              </option>
            ))}
          </select>
        </label>
      )}
      {departments.length === 0 && <p className="mt-3 text-sm text-bad">{t("Нет отделов, где вы можете ставить задачи.")}</p>}
      {error && <p className="mt-3 text-sm text-bad">{error}</p>}
    </Sheet>
  );
}
