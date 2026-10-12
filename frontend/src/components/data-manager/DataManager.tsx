"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useRef, useState } from "react";
import {
  AlertTriangle,
  ArrowUp,
  Check,
  ChevronDown,
  DatabaseZap,
  FileSpreadsheet,
  History,
  Info,
  ListChecks,
  Loader2,
  Paperclip,
  Pencil,
  Plus,
  Trash2,
  X,
} from "lucide-react";
import { Markdown } from "@/components/ai/Markdown";
import { bff } from "@/lib/bff";
import { historySection } from "@/lib/consultant";
import {
  exampleTasks,
  streamDataManager,
  uploadReport,
  when,
  type ChangeSet,
  type DataConversation,
  type DataConversationSummary,
  type DataEvent,
  type DataFile,
  type DataMessage,
  type DataStatus,
  type DataStep,
} from "@/lib/data-manager";
import { fileSize } from "@/lib/format";
import { ChangeSetCard } from "./ChangeSetCard";
import { OperationsJournal } from "./OperationsJournal";

type Props = {
  status: DataStatus;
  conversations: DataConversationSummary[];
  conversation: DataConversation | null;
  canConfigure: boolean;
  canChange: boolean;
};

type Attachment = { key: string; name: string; size: number; progress: number; file?: DataFile; error?: string };

const base = "/data-manager";

export function DataManager({ status, conversations: initialList, conversation, canConfigure, canChange }: Props) {
  const router = useRouter();
  const [list, setList] = useState(initialList);
  const [activeId, setActiveId] = useState<string | null>(conversation?.id ?? null);
  const [title, setTitle] = useState(conversation?.title ?? "Новый чат");
  const [messages, setMessages] = useState<DataMessage[]>(conversation?.messages ?? []);
  const [files, setFiles] = useState<DataFile[]>(conversation?.files ?? []);
  const [sets, setSets] = useState<Record<string, ChangeSet>>(() => Object.fromEntries((conversation?.changeSets ?? []).map((s) => [s.id, s])));
  const [attachments, setAttachments] = useState<Attachment[]>([]);
  const [input, setInput] = useState("");
  const [pending, setPending] = useState<string | null>(null);
  const [steps, setSteps] = useState<DataStep[]>([]);
  const [running, setRunning] = useState(false);
  const [acting, setActing] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [tab, setTab] = useState<"chat" | "journal">("chat");
  const [journalVersion, setJournalVersion] = useState(0);
  const bottom = useRef<HTMLDivElement>(null);
  const textarea = useRef<HTMLTextAreaElement>(null);
  const picker = useRef<HTMLInputElement>(null);

  useEffect(() => {
    // Пустой чат не прокручивается: на телефоне подсказки выше экрана, и прокрутка вниз срезала бы заголовок.
    if (messages.length === 0 && pending === null && Object.keys(sets).length === 0) return;
    bottom.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages.length, pending, steps.length, Object.keys(sets).length]);

  useEffect(() => {
    const el = textarea.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, 200)}px`;
  }, [input]);

  const upsertSet = (set: ChangeSet) => setSets((prev) => ({ ...prev, [set.id]: set }));
  const addMessage = (m: DataMessage) => setMessages((prev) => (prev.some((x) => x.id === m.id) ? prev : [...prev, m]));

  /** События хода модели: чат, подтверждение, откат. conversationId — чат, к которому относится действие. */
  const handle = (e: DataEvent, conversationId: string | null) => {
    const here = conversationId === null || conversationId === activeId;
    switch (e.type) {
      case "start":
        if (!activeId) {
          setActiveId(e.data.conversationId);
          setTitle(e.data.title);
          window.history.replaceState(null, "", `${base}/${e.data.conversationId}`);
        }
        setList((prev) => [
          { id: e.data.conversationId, title: e.data.title, createdAt: e.data.userMessage.createdAt, lastMessageAt: e.data.userMessage.createdAt },
          ...prev.filter((c) => c.id !== e.data.conversationId),
        ]);
        addMessage(e.data.userMessage);
        setPending(null);
        break;
      case "step":
        if (!here) break;
        setSteps((prev) => {
          if (e.data.status === "running") return [...prev, e.data];
          const i = prev.findLastIndex((s) => s.tool === e.data.tool && s.status === "running");
          return i < 0 ? [...prev, e.data] : prev.map((s, j) => (j === i ? e.data : s));
        });
        break;
      case "changeset":
        upsertSet(e.data);
        break;
      case "event":
        if (here) addMessage(e.data.message);
        break;
      case "done":
        if (here) addMessage(e.data.message);
        e.data.changeSets.forEach(upsertSet);
        break;
      case "error":
        if (here && e.data.message) addMessage(e.data.message);
        else setError(e.data.error);
        break;
      case "outcome":
        if (e.data.status !== "Applied" && e.data.status !== "RolledBack" && e.data.status !== "PartiallyRolledBack") setError(e.data.message);
        break;
    }
  };

  const upload = async (picked: FileList | null) => {
    if (!picked?.length) return;
    for (const file of Array.from(picked)) {
      const key = `${file.name}-${file.size}-${Date.now()}-${Math.random()}`;
      if (file.size > status.maxFileBytes) {
        setAttachments((prev) => [...prev, { key, name: file.name, size: file.size, progress: 0, error: `Больше ${fileSize(status.maxFileBytes)}` }]);
        continue;
      }

      setAttachments((prev) => [...prev, { key, name: file.name, size: file.size, progress: 0 }]);
      try {
        const uploaded = await uploadReport(file, activeId, (loaded, total) =>
          setAttachments((prev) => prev.map((a) => (a.key === key ? { ...a, progress: total ? loaded / total : 0 } : a))),
        );
        setAttachments((prev) => prev.map((a) => (a.key === key ? { ...a, progress: 1, file: uploaded } : a)));
      } catch (e) {
        setAttachments((prev) => prev.map((a) => (a.key === key ? { ...a, error: (e as Error).message } : a)));
      }
    }

    if (picker.current) picker.current.value = "";
  };

  const ready = attachments.filter((a) => a.file);
  const uploading = attachments.some((a) => !a.file && !a.error);

  const send = async (text: string) => {
    const message = text.trim();
    if (!message || running || uploading) return;
    const sending = ready.map((a) => a.file!);
    setInput("");
    setError(null);
    setPending(message);
    setSteps([]);
    setRunning(true);
    setAttachments([]);
    setFiles((prev) => [...prev, ...sending.filter((f) => !prev.some((p) => p.id === f.id))]);
    try {
      await streamDataManager("chat", { conversationId: activeId, message, fileIds: sending.map((f) => f.id) }, (e) => handle(e, null));
    } catch (e) {
      setError((e as Error).message);
      setInput(message);
    } finally {
      setPending(null);
      setSteps([]);
      setRunning(false);
    }
  };

  const act = async (set: ChangeSet, kind: "apply" | "rollback") => {
    const counts = set.summary.counts;
    const question =
      kind === "apply"
        ? `Применить план «${set.title}»?\n\nСоздать: ${counts.create}, изменить: ${counts.update}, удалить: ${counts.delete}. Конфликтные строки (${counts.conflict}) не применяются.`
        : `Откатить план «${set.title}»?\n\nВернутся прежние значения. Строки, которые с тех пор кто-то изменил, останутся как есть.`;
    if (!confirm(question)) return;
    setError(null);
    setActing(set.id);
    setRunning(true);
    setSteps([]);
    try {
      await streamDataManager(`change-sets/${set.id}/${kind}`, {}, (e) => handle(e, set.conversationId));
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setActing(null);
      setRunning(false);
      setSteps([]);
      setJournalVersion((v) => v + 1);
    }
  };

  const cancel = async (set: ChangeSet) => {
    if (!confirm(`Отменить план «${set.title}»? Он не будет применён.`)) return;
    setError(null);
    setActing(set.id);
    try {
      upsertSet(await bff<ChangeSet>(`data-manager/change-sets/${set.id}/cancel`, { method: "POST" }));
      if (activeId && set.conversationId === activeId) {
        const fresh = await bff<DataConversation>(`data-manager/conversations/${activeId}`);
        setMessages(fresh.messages);
      }
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setActing(null);
      setJournalVersion((v) => v + 1);
    }
  };

  const rename = async () => {
    if (!activeId) return;
    const next = prompt("Название чата", title)?.trim();
    if (!next || next === title) return;
    try {
      await bff(`data-manager/conversations/${activeId}`, { method: "PATCH", body: JSON.stringify({ title: next }) });
      setTitle(next);
      setList((prev) => prev.map((c) => (c.id === activeId ? { ...c, title: next } : c)));
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const archive = async (id: string) => {
    if (!confirm("Убрать чат из истории? Применённые изменения и журнал операций останутся.")) return;
    try {
      await bff(`data-manager/conversations/${id}`, { method: "DELETE" });
      setList((prev) => prev.filter((c) => c.id !== id));
      if (id === activeId) router.push(base);
    } catch (e) {
      setError((e as Error).message);
    }
  };

  // История: разделы Сегодня / Вчера / Ранее (список уже отсортирован по последнему сообщению).
  const groups = useMemo(() => {
    const out: { label: string; items: DataConversationSummary[] }[] = [];
    for (const c of list) {
      const label = historySection(c.lastMessageAt);
      const group = out.find((g) => g.label === label);
      if (group) group.items.push(c);
      else out.push({ label, items: [c] });
    }
    return out;
  }, [list]);

  // Планы, подготовленные в текущем ходе, — до того как ход закончился и появилось сообщение с ними.
  const shownInMessages = new Set(messages.filter((m) => m.role === "Assistant").flatMap((m) => m.changeSetIds));
  const loose = Object.values(sets).filter((s) => s.conversationId === activeId && !shownInMessages.has(s.id));
  const waiting = running && acting === null && (pending !== null || messages.length === 0 || messages[messages.length - 1].role !== "Assistant");
  const fileById = new Map(files.map((f) => [f.id, f]));

  const card = (set: ChangeSet) => (
    <ChangeSetCard
      key={set.id}
      set={set}
      canChange={canChange}
      acting={acting === set.id}
      locked={running || acting !== null}
      onApply={(s) => act(s, "apply")}
      onCancel={cancel}
      onRollback={(s) => act(s, "rollback")}
    />
  );

  const historyPanel = (
    <div className="flex h-full flex-col">
      <div className="p-4">
        <Link
          href={base}
          className="flex h-11 w-full items-center justify-center gap-2 rounded-full bg-accent text-sm font-semibold text-white hover:bg-accent-strong"
          onClick={() => setHistoryOpen(false)}
        >
          <Plus className="size-4" />
          Новый чат
        </Link>
      </div>
      <div className="min-h-0 flex-1 overflow-y-auto px-3 pb-4">
        {list.length === 0 && <p className="px-3 py-6 text-center text-sm text-ink-3">Чатов пока нет</p>}
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
                      href={`${base}/${c.id}`}
                      onClick={() => setHistoryOpen(false)}
                      aria-current={active ? "page" : undefined}
                      className={`min-w-0 flex-1 truncate px-3 py-2.5 text-[13px] group-hover:pr-10 ${active ? "font-medium text-ink" : "text-ink-2 group-hover:text-ink"}`}
                      title={c.title}
                    >
                      {c.title}
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
        {/*
          Шапка: на компьютере — значок и название слева, переключатель «Чат / Журнал операций» справа;
          на телефоне — кнопка истории и одна стеклянная полоса: название (обрезается) и переключатель.
        */}
        <header className="flex items-center gap-2 border-b border-line px-3 py-3 sm:px-6 lg:gap-3">
          <button
            type="button"
            onClick={() => setHistoryOpen(true)}
            aria-label="История чатов"
            className="grid size-10 shrink-0 place-items-center rounded-xl border border-line bg-muted text-ink-2 backdrop-blur-md hover:text-ink lg:hidden"
          >
            <History className="size-4" />
          </button>
          <div className="flex min-w-0 flex-1 items-center gap-2 rounded-2xl border border-line bg-muted p-1 backdrop-blur-md lg:gap-3 lg:rounded-none lg:border-0 lg:bg-transparent lg:p-0 lg:backdrop-blur-none">
            <DatabaseZap className="hidden size-5 shrink-0 text-accent-strong lg:block" strokeWidth={1.75} />
            <h1 className="min-w-0 flex-1 truncate pl-2 text-[13px] font-semibold text-ink lg:pl-0 lg:text-base">{activeId ? title : "AI Data Manager"}</h1>
            <div role="tablist" aria-label="Раздел" className="flex shrink-0 gap-0.5 rounded-full lg:border lg:border-line lg:bg-muted lg:p-1 lg:backdrop-blur-md">
              {(
                [
                  ["chat", "Чат", null],
                  ["journal", "Журнал операций", ListChecks],
                ] as const
              ).map(([key, label, Icon]) => (
                <button
                  key={key}
                  type="button"
                  role="tab"
                  aria-selected={tab === key}
                  onClick={() => setTab(key)}
                  className={`inline-flex h-8 items-center gap-1.5 whitespace-nowrap rounded-full px-3 text-xs transition-colors lg:px-4 lg:text-[13px] ${
                    tab === key ? "bg-accent font-medium text-white hover:bg-accent-strong" : "text-ink-2 hover:bg-white/5 hover:text-ink"
                  }`}
                >
                  {Icon && <Icon className="hidden size-3.5 lg:block" strokeWidth={1.75} />}
                  {label}
                </button>
              ))}
            </div>
          </div>
          {activeId && tab === "chat" && (
            <button type="button" onClick={rename} aria-label="Переименовать чат" className="grid size-9 shrink-0 place-items-center rounded-full text-ink-3 hover:bg-muted hover:text-ink">
              <Pencil className="size-3.5" />
            </button>
          )}
        </header>

        {tab === "journal" ? (
          <div className="min-h-0 flex-1 overflow-y-auto">
            {error && <div className="mx-auto mt-4 max-w-5xl rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</div>}
            <OperationsJournal
              canChange={canChange}
              version={journalVersion}
              acting={acting}
              onApply={(s) => act(s, "apply")}
              onCancel={cancel}
              onRollback={(s) => act(s, "rollback")}
            />
          </div>
        ) : (
          <>
            <div className="min-h-0 flex-1 overflow-y-auto">
              <div className="mx-auto flex min-h-full max-w-5xl flex-col px-4 py-6 sm:px-6">
                {!status.ready && (
                  <div className="mb-6 flex items-start gap-3 rounded-lg bg-warn-soft px-4 py-3 text-sm">
                    <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warn" />
                    <div>
                      <div className="font-semibold text-ink">Claude API не подключён</div>
                      <div className="mt-0.5 text-ink-2">{status.message}</div>
                      {canConfigure && (
                        <Link href="/settings/ai/providers" className="mt-1 inline-block font-medium text-accent-strong hover:underline">
                          Открыть «Настройки → AI → Провайдеры»
                        </Link>
                      )}
                    </div>
                  </div>
                )}

                {files.length > 0 && (
                  <div className="mb-5 flex flex-wrap gap-2">
                    {files.map((f) => (
                      <FileChip key={f.id} file={f} />
                    ))}
                  </div>
                )}

                {messages.length === 0 && !running && (
                  <div className="my-auto py-8 text-center">
                    {/* Светящийся стеклянный круг со значком. */}
                    <div className="mx-auto grid size-16 place-items-center rounded-full bg-muted text-accent-strong shadow-[0_0_48px_-6px_rgb(46_107_255/0.55)] ring-1 ring-inset ring-white/15 backdrop-blur-md">
                      <DatabaseZap className="size-7" strokeWidth={1.5} />
                    </div>
                    <h2 className="mt-5 text-2xl font-bold tracking-tight text-ink">Обновление данных по отчёту</h2>
                    <p className="mx-auto mt-2 max-w-2xl text-sm leading-relaxed text-ink-2">
                      Приложите отчёт Excel или CSV и напишите, что сделать. Claude ({status.model}) разберёт файл, сверит его с данными OneBase и подготовит план
                      изменений «было → стало». Данные меняются только после вашего подтверждения; факт продаж из Linko только сверяется — он не правится.
                    </p>
                    {/* Подсказки — столбиком по центру; на телефоне — на всю ширину, текст переносится. */}
                    <div className="mx-auto mt-7 flex max-w-xl flex-col items-center gap-2.5">
                      {exampleTasks.map((q) => (
                        <button
                          key={q}
                          type="button"
                          disabled={!status.ready}
                          onClick={() => setInput(q)}
                          className="rounded-full border border-line bg-muted px-5 py-2.5 text-center text-[13px] leading-snug text-ink-2 backdrop-blur-md transition-colors hover:border-accent/50 hover:text-ink active:bg-accent-soft disabled:opacity-50 max-lg:w-full max-lg:rounded-2xl max-lg:py-3"
                        >
                          {q}
                        </button>
                      ))}
                    </div>
                  </div>
                )}

                <div className="space-y-6">
                  {messages.map((m) =>
                    m.role === "User" ? (
                      <UserBubble key={m.id} message={m} files={fileById} />
                    ) : m.role === "Event" ? (
                      <EventNote key={m.id} message={m} />
                    ) : (
                      <div key={m.id} className="space-y-3">
                        <AssistantMessage message={m} />
                        {m.changeSetIds.map((id) => sets[id]).filter(Boolean).map(card)}
                      </div>
                    ),
                  )}
                  {pending && <UserBubble message={{ content: pending, fileIds: [] }} files={fileById} />}
                  {loose.map(card)}
                  {(waiting || (running && steps.length > 0)) && <StepsCard steps={steps} title={acting ? "Применяю и проверяю…" : "Анализирую…"} />}
                </div>
                <div ref={bottom} />
              </div>
            </div>

            <div className="px-4 pb-4 pt-2 sm:px-6">
              <div className="mx-auto max-w-5xl">
                {error && (
                  <div className="mb-2 flex items-start gap-2 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">
                    <span className="flex-1">{error}</span>
                    <button type="button" aria-label="Скрыть" onClick={() => setError(null)}>
                      <X className="size-4" />
                    </button>
                  </div>
                )}
                {attachments.length > 0 && (
                  <div className="mb-2 flex flex-wrap gap-2">
                    {attachments.map((a) => (
                      <span
                        key={a.key}
                        className={`inline-flex max-w-full items-center gap-2 rounded-lg border px-2.5 py-1.5 text-xs ${a.error ? "border-bad/40 bg-bad-soft text-bad" : "border-line bg-surface text-ink-2"}`}
                      >
                        {a.file ? <Check className="size-3.5 text-ok" /> : a.error ? <AlertTriangle className="size-3.5" /> : <Loader2 className="size-3.5 animate-spin" />}
                        <span className="truncate">{a.name}</span>
                        <span className="text-ink-3">{a.error ?? (a.file ? fileSize(a.size) : `${Math.round(a.progress * 100)}%`)}</span>
                        {a.file?.duplicates.length ? <span className="text-warn">уже загружался</span> : null}
                        <button type="button" aria-label="Убрать файл" onClick={() => setAttachments((prev) => prev.filter((x) => x.key !== a.key))}>
                          <X className="size-3.5" />
                        </button>
                      </span>
                    ))}
                  </div>
                )}
                <form
                  className="flex items-end gap-2 rounded-[20px] border border-line bg-muted p-2 shadow-[0_16px_48px_-24px_rgb(0_0_0/0.6)] backdrop-blur-xl focus-within:border-accent/60 focus-within:ring-2 focus-within:ring-accent/20 lg:rounded-[28px]"
                  onSubmit={(e) => {
                    e.preventDefault();
                    void send(input);
                  }}
                >
                  <input
                    ref={picker}
                    type="file"
                    multiple
                    hidden
                    accept={status.extensions.join(",")}
                    onChange={(e) => void upload(e.target.files)}
                  />
                  <button
                    type="button"
                    aria-label="Приложить отчёт"
                    title={`Приложить отчёт (${status.extensions.join(", ")}, до ${fileSize(status.maxFileBytes)})`}
                    disabled={!status.ready || running}
                    onClick={() => picker.current?.click()}
                    className="grid size-10 shrink-0 place-items-center rounded-xl text-ink-2 hover:bg-white/5 hover:text-ink disabled:opacity-40 max-lg:size-11 lg:rounded-full"
                  >
                    <Paperclip className="size-[18px]" strokeWidth={1.75} />
                  </button>
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
                    maxLength={8000}
                    placeholder={status.ready ? "Что сделать с отчётом? Например: «Обнови план РОП на октябрь»" : "Claude API не подключён"}
                    disabled={!status.ready}
                    aria-label="Сообщение AI Data Manager"
                    className="max-h-[200px] min-h-[40px] flex-1 resize-none bg-transparent px-2 py-2.5 text-sm text-ink placeholder:text-ink-3 focus:outline-none disabled:opacity-60"
                  />
                  <button
                    type="submit"
                    aria-label="Отправить"
                    disabled={!status.ready || running || uploading || input.trim() === ""}
                    className="grid size-10 shrink-0 place-items-center rounded-xl bg-accent text-white hover:bg-accent-strong disabled:opacity-40 max-lg:size-11 lg:rounded-full"
                  >
                    {running ? <Loader2 className="size-4 animate-spin" /> : <ArrowUp className="size-4" />}
                  </button>
                </form>
                <p className="mt-2 text-center text-[11px] leading-relaxed text-ink-3">
                  Enter — отправить, Shift+Enter — новая строка. Изменения применяются только кнопкой «Подтвердить»; каждое действие пишется в журнал.
                </p>
              </div>
            </div>
          </>
        )}
      </section>
    </div>
  );
}

function FileChip({ file }: { file: DataFile }) {
  const [open, setOpen] = useState(false);
  return (
    <div className="rounded-lg border border-line bg-surface text-xs">
      <button type="button" onClick={() => setOpen(!open)} className="flex items-center gap-2 px-2.5 py-1.5 text-ink-2 hover:text-ink">
        <FileSpreadsheet className="size-3.5 text-accent-strong" />
        <span className="max-w-[18rem] truncate font-medium text-ink">{file.fileName}</span>
        <span className="text-ink-3">{fileSize(file.sizeBytes)}</span>
        {file.duplicates.length > 0 && <span className="text-warn">повтор</span>}
        <ChevronDown className={`size-3.5 transition-transform ${open ? "" : "-rotate-90"}`} />
      </button>
      {open && (
        <div className="border-t border-line px-2.5 py-2 text-ink-2">
          {file.sheets.map((s) => (
            <div key={s.name}>
              Лист «{s.name}»: {s.rows} строк × {s.columns} столбцов{s.truncated ? " (прочитана часть)" : ""}
            </div>
          ))}
          <div className="mt-1 text-ink-3">Загружен {when(file.createdAt)} · SHA-256 {file.sha256.slice(0, 12)}…</div>
          {file.duplicates.map((d) => (
            <div key={d.fileId} className="mt-1 text-warn">
              Этот же файл уже загружали {when(d.uploadedAt)} («{d.fileName}»){d.appliedChangeSets.length > 0 ? " и применяли" : ""}.
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function UserBubble({ message, files }: { message: Pick<DataMessage, "content" | "fileIds">; files: Map<string, DataFile> }) {
  return (
    <div className="flex flex-col items-end gap-1.5">
      {message.fileIds.length > 0 && (
        <div className="flex flex-wrap justify-end gap-1.5">
          {message.fileIds.map((id) => (
            <span key={id} className="inline-flex items-center gap-1.5 rounded-lg border border-line bg-surface px-2 py-1 text-xs text-ink-2">
              <FileSpreadsheet className="size-3.5 text-accent-strong" />
              {files.get(id)?.fileName ?? "файл"}
            </span>
          ))}
        </div>
      )}
      <div className="max-w-[85%] whitespace-pre-wrap rounded-2xl rounded-br-md bg-accent px-4 py-2.5 text-sm text-white">{message.content}</div>
    </div>
  );
}

function EventNote({ message }: { message: DataMessage }) {
  return (
    <div className="flex justify-center">
      <div className="inline-flex max-w-[90%] items-start gap-2 rounded-full bg-muted px-3.5 py-1.5 text-xs text-ink-2">
        <Info className="mt-0.5 size-3.5 shrink-0 text-ink-3" />
        <span>
          {message.content} <span className="text-ink-3">· {when(message.createdAt)}</span>
        </span>
      </div>
    </div>
  );
}

function AssistantMessage({ message }: { message: DataMessage }) {
  const [open, setOpen] = useState(false);
  const failed = message.status === "Failed";
  return (
    <div className="flex gap-3">
      <span className="grid size-8 shrink-0 place-items-center rounded-full bg-accent-soft text-accent-strong">
        <DatabaseZap className="size-4" />
      </span>
      <div className="min-w-0 flex-1">
        <div className={`rounded-2xl rounded-tl-md border px-4 py-3 ${failed ? "border-bad/30 bg-bad-soft" : "border-line bg-surface"}`}>
          {failed && <div className="mb-1 text-sm font-semibold text-bad">Ответ не получен</div>}
          <Markdown text={message.content} />
          {message.error && <p className="mt-2 text-sm text-bad">{message.error}</p>}
        </div>
        {message.steps.length > 0 && (
          <div className="mt-1.5">
            <button type="button" onClick={() => setOpen(!open)} className="inline-flex items-center gap-1 text-xs text-ink-3 hover:text-ink">
              <ChevronDown className={`size-3.5 transition-transform ${open ? "" : "-rotate-90"}`} />
              Инструменты: {message.steps.length}
            </button>
            {open && (
              <ul className="mt-1 space-y-1 text-xs text-ink-2">
                {message.steps.map((s, i) => (
                  <li key={i} className="flex items-start gap-2">
                    <StepIcon status={s.status} />
                    <span>
                      <span className="font-medium text-ink">{s.title}</span>
                      {s.note && <span className="text-ink-3"> — {s.note}</span>}
                    </span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

function StepIcon({ status }: { status: DataStep["status"] }) {
  if (status === "done") return <Check className="mt-0.5 size-3.5 shrink-0 text-ok" />;
  if (status === "failed") return <X className="mt-0.5 size-3.5 shrink-0 text-bad" />;
  return <Loader2 className="mt-0.5 size-3.5 shrink-0 animate-spin text-accent-strong" />;
}

function StepsCard({ steps, title }: { steps: DataStep[]; title: string }) {
  return (
    <div className="flex gap-3">
      <span className="grid size-8 shrink-0 place-items-center rounded-full bg-accent-soft text-accent-strong">
        <Loader2 className="size-4 animate-spin" />
      </span>
      <div className="rounded-2xl rounded-tl-md border border-line bg-surface px-4 py-3 text-sm">
        <div className="font-medium text-ink">{title}</div>
        {steps.length > 0 && (
          <ul className="mt-2 space-y-1 text-ink-2">
            {steps.map((s, i) => (
              <li key={i} className="flex items-start gap-2">
                <StepIcon status={s.status} />
                <span>
                  {s.title}
                  {s.note && <span className="text-ink-3"> — {s.note}</span>}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
