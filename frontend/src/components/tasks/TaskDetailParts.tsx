"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import {
  Activity,
  AtSign,
  Ban,
  Building2,
  CalendarCheck,
  CalendarClock,
  CalendarPlus,
  Check,
  ChevronRight,
  CircleCheckBig,
  Download,
  File,
  FileArchive,
  FileImage,
  FileSpreadsheet,
  FileText,
  Flag,
  Folder,
  GripVertical,
  Info,
  ListChecks,
  Loader2,
  LockOpen,
  MessageSquare,
  OctagonX,
  Paperclip,
  Pencil,
  Plus,
  Repeat2,
  Send,
  Smile,
  Square,
  Trash2,
  Undo2,
  Upload,
  UserRound,
  UserRoundPen,
  type LucideIcon,
} from "lucide-react";
import { Markdown } from "@/components/ai/Markdown";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { plural } from "@/lib/notifications";
import { activity, insertMention, mentionAt, mentionedIds, splitMentions, stamp, type ActivityFilter, type EventLook } from "@/lib/task-board";
import { fileSize, shortDate, todayInTashkent, workPriorities, workPriorityLabel, type WorkAssignee, type WorkProject, type WorkTask, type WorkTaskDetail, type WorkTaskPriority } from "@/lib/tasks";
import { AssigneePicker } from "./AssigneePicker";
import { PriorityPill, UserAvatar } from "./TaskKit";

/** Карточка блока: на компьютере padding 22, на телефоне 18. */
export const card = "rounded-2xl border border-line bg-surface p-[18px] lg:p-[22px]";
const cardTitle = "flex items-center gap-2.5 text-[15px] font-semibold text-ink";

/** Поля задачи для PUT /api/work/tasks/{id} (правка по месту отправляет все, меняя одно). */
export type TaskInput = {
  title: string;
  description: string | null;
  assigneeId: string | null;
  priority: WorkTaskPriority;
  startDate: string | null;
  dueDate: string | null;
  projectId: string | null;
};

export const inputOf = (task: WorkTask): TaskInput => ({
  title: task.title,
  description: task.description,
  assigneeId: task.assigneeId,
  priority: task.priority,
  startDate: task.startDate,
  dueDate: task.dueDate,
  projectId: task.projectId,
});

// ---------- Описание ----------

export function DescriptionCard({ task, canEdit, onSave, className = "" }: { task: WorkTask; canEdit: boolean; onSave: (text: string | null) => Promise<boolean>; className?: string }) {
  const t = useT();
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState(task.description ?? "");
  const [saving, setSaving] = useState(false);
  useEffect(() => setText(task.description ?? ""), [task.description]);

  const save = async () => {
    setSaving(true);
    if (await onSave(text.trim() || null)) setEditing(false);
    setSaving(false);
  };

  return (
    <section className={`${card} ${className}`} aria-labelledby="task-description">
      <div className="flex items-center justify-between gap-3">
        <h2 id="task-description" className={cardTitle}>
          <FileText className="size-[18px] text-ink-2 max-lg:hidden" aria-hidden />
          {t("Описание")}
        </h2>
        {canEdit && !editing && (
          <button type="button" onClick={() => setEditing(true)} aria-label={t("Изменить описание")} className="inline-flex items-center gap-1.5 text-[13px] font-semibold text-link hover:underline">
            <Pencil className="size-4" aria-hidden />
            <span className="max-lg:hidden">{t("Изменить")}</span>
          </button>
        )}
      </div>
      {editing ? (
        <div className="mt-3 space-y-2">
          <textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            maxLength={4000}
            autoFocus
            rows={6}
            onKeyDown={(e) => {
              if (e.key === "Escape") setEditing(false);
              if (e.key === "Enter" && (e.metaKey || e.ctrlKey)) void save();
            }}
            placeholder={t("Подробности, результат, ссылки. Можно Markdown: **жирный**, списки через «-».")}
            className="w-full rounded-xl border border-line bg-ai-bg px-3 py-2.5 text-sm leading-relaxed text-ink focus:border-accent focus:outline-none"
          />
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => (setEditing(false), setText(task.description ?? ""))} className="h-9 rounded-lg px-3 text-sm font-medium text-ink-3 hover:text-ink">
              {t("Отмена")}
            </button>
            <button type="button" onClick={() => void save()} disabled={saving} className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white disabled:opacity-60">
              {saving && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {t("Сохранить")}
            </button>
          </div>
        </div>
      ) : task.description ? (
        <div className="mt-3">
          <Markdown text={task.description} className="space-y-2.5 text-sm leading-[1.6] text-ink-2" />
        </div>
      ) : canEdit ? (
        <button type="button" onClick={() => setEditing(true)} className="mt-3 inline-flex items-center gap-1.5 text-sm font-medium text-link">
          <Plus className="size-4" aria-hidden />
          {t("Добавить описание")}
        </button>
      ) : (
        <p className="mt-3 text-sm text-subtle">{t("Без описания")}</p>
      )}
      {task.result && <p className="mt-3 rounded-xl bg-surface-2 px-3 py-2 text-sm text-ink-2">{t("Итог: {text}", { text: task.result })}</p>}
    </section>
  );
}

// ---------- Чек-лист ----------

type ChecklistItem = WorkTaskDetail["checklist"][number];

export function ChecklistCard({
  task,
  items,
  canWork,
  run,
  className = "",
}: {
  task: WorkTask;
  items: ChecklistItem[];
  canWork: boolean;
  run: (key: string, fn: () => Promise<unknown>) => Promise<boolean>;
  className?: string;
}) {
  const t = useT();
  const [list, setList] = useState(items);
  const [text, setText] = useState("");
  const [adding, setAdding] = useState(false);
  const [drag, setDrag] = useState<string | null>(null);
  useEffect(() => setList(items), [items]);
  const done = list.filter((i) => i.isDone).length;
  const post = (path: string, body: unknown) => bff(`work/tasks/${task.id}/${path}`, { method: "POST", body: JSON.stringify(body) });

  const add = async () => {
    const title = text.trim();
    if (!title) return;
    if (await run("item-add", () => post("checklist", { title }))) setText("");
  };

  const toggle = (item: ChecklistItem) => {
    setList((l) => l.map((x) => (x.id === item.id ? { ...x, isDone: !x.isDone } : x)));
    void run(`item-${item.id}`, () => bff(`work/tasks/${task.id}/checklist/${item.id}`, { method: "PUT", body: JSON.stringify({ done: !item.isDone }) })).then(
      (ok) => !ok && setList(items),
    );
  };

  const drop = (target: string) => {
    if (!drag || drag === target) return;
    const order = list.map((x) => x.id).filter((id) => id !== drag);
    order.splice(order.indexOf(target), 0, drag);
    setList(order.map((id) => list.find((x) => x.id === id)!));
    setDrag(null);
    void run("order", () => post("checklist/order", { ids: order })).then((ok) => !ok && setList(items));
  };

  const addRow = (
    <form
      className="mt-3 flex gap-2"
      onSubmit={(e) => {
        e.preventDefault();
        void add();
      }}
    >
      <label className="relative flex-1">
        <Square className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-subtle" aria-hidden />
        <input
          value={text}
          onChange={(e) => setText(e.target.value)}
          maxLength={300}
          placeholder={t("Новый пункт…")}
          aria-label={t("Новый пункт")}
          autoFocus={adding}
          className="h-11 w-full rounded-xl border border-line bg-ai-bg pl-9 pr-3 text-sm text-ink placeholder:text-subtle focus:border-accent focus:outline-none lg:h-10"
        />
      </label>
      <button type="submit" disabled={!text.trim()} className="inline-flex h-11 shrink-0 items-center gap-1.5 rounded-xl border border-line bg-surface-2 px-3.5 text-sm font-semibold text-ink disabled:opacity-50 lg:h-10">
        <Plus className="size-4" aria-hidden />
        {t("Добавить")}
      </button>
    </form>
  );

  return (
    <section className={`${card} ${className}`} aria-labelledby="task-checklist">
      <div className="flex items-center justify-between gap-3">
        <h2 id="task-checklist" className={cardTitle}>
          <ListChecks className="size-[18px] text-ink-2 max-lg:hidden" aria-hidden />
          {t("Чек-лист")}
        </h2>
        <span className="text-[13px] tabular-nums text-subtle">
          <span className="max-lg:hidden">{t("{done} из {total}", { done, total: list.length })}</span>
          <span className="lg:hidden">{t(plural(list.length, "{n} пункт", "{n} пункта", "{n} пунктов"), { n: list.length })}</span>
        </span>
      </div>

      {list.length === 0 && (
        <p className="mt-3 flex items-start gap-2 rounded-xl border border-line bg-ai-bg px-3 py-2.5 text-[13px] text-ink-3 max-lg:hidden">
          <Info className="mt-px size-4 shrink-0" aria-hidden />
          {t("Пунктов нет — процент выполнения указывается вручную. Добавьте пункты, и он будет считаться автоматически.")}
        </p>
      )}

      {list.length > 0 && (
        <ul className="mt-3 space-y-0.5">
          {list.map((item) => (
            <li
              key={item.id}
              draggable={canWork}
              onDragStart={(e) => {
                e.dataTransfer.effectAllowed = "move";
                setDrag(item.id);
              }}
              onDragOver={(e) => drag && e.preventDefault()}
              onDrop={(e) => {
                e.preventDefault();
                drop(item.id);
              }}
              onDragEnd={() => setDrag(null)}
              className={`group flex min-h-10 items-center gap-2.5 rounded-lg px-1 hover:bg-surface-2 ${drag === item.id ? "opacity-40" : ""}`}
            >
              {canWork && <GripVertical className="size-4 shrink-0 cursor-grab text-subtle opacity-0 group-hover:opacity-100 max-lg:hidden" aria-hidden />}
              <button
                type="button"
                role="checkbox"
                aria-checked={item.isDone}
                aria-label={item.title}
                disabled={!canWork}
                onClick={() => toggle(item)}
                className={`grid size-5 shrink-0 place-items-center rounded-md border ${item.isDone ? "border-ok bg-ok text-white" : "border-line-strong"} disabled:opacity-60`}
              >
                {item.isDone && <Check className="size-3.5" strokeWidth={3} aria-hidden />}
              </button>
              <span className={`flex-1 py-1.5 text-sm ${item.isDone ? "text-subtle line-through" : "text-ink"}`}>{item.title}</span>
              {canWork && (
                <button
                  type="button"
                  onClick={() => void run(`del-${item.id}`, () => bff(`work/tasks/${task.id}/checklist/${item.id}`, { method: "DELETE" }))}
                  aria-label={t("Удалить пункт «{name}»", { name: item.title })}
                  className="grid size-8 place-items-center rounded-md text-subtle hover:text-bad focus-visible:opacity-100 lg:opacity-0 lg:group-hover:opacity-100"
                >
                  <Trash2 className="size-4" aria-hidden />
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {canWork && (
        <>
          <div className="max-lg:hidden">{addRow}</div>
          <div className="lg:hidden">
            {adding ? (
              addRow
            ) : (
              <button type="button" onClick={() => setAdding(true)} className="mt-3 flex h-11 w-full items-center gap-2 rounded-xl border border-line bg-ai-bg px-3.5 text-sm font-semibold text-link">
                <Plus className="size-4" aria-hidden />
                {t("Добавить пункт")}
              </button>
            )}
          </div>
        </>
      )}
    </section>
  );
}

// ---------- Активность ----------

const eventIcon: Record<EventLook | "comment", { icon: LucideIcon; tone: string }> = {
  created: { icon: Plus, tone: "bg-accent-soft text-accent-strong" },
  done: { icon: CircleCheckBig, tone: "bg-ok-soft text-ok" },
  returned: { icon: Undo2, tone: "bg-warn-soft text-warn" },
  review: { icon: Send, tone: "bg-violet-soft text-violet" },
  blocked: { icon: OctagonX, tone: "bg-bad-soft text-bad" },
  unblocked: { icon: LockOpen, tone: "bg-ok-soft text-ok" },
  cancelled: { icon: Ban, tone: "bg-surface-2 text-subtle" },
  status: { icon: Repeat2, tone: "bg-surface-2 text-ink-3" },
  changed: { icon: Pencil, tone: "bg-surface-2 text-ink-3" },
  comment: { icon: MessageSquare, tone: "bg-surface-2 text-ink-3" },
};

function CommentText({ text, names }: { text: string; names: string[] }) {
  return (
    <>
      {splitMentions(text, names).map((p, i) =>
        p.mention ? (
          <span key={i} className="font-semibold text-link">
            {p.text}
          </span>
        ) : (
          <span key={i}>{p.text}</span>
        ),
      )}
    </>
  );
}

/**
 * Лента: комментарии и история одной лентой, новые сверху. Компьютер — «Активность» с сегментами «Всё · Комментарии · История» и полем
 * комментария сверху; телефон — компактная «История» (поле комментария закреплено внизу экрана, Composer variant="mobile").
 */
export function ActivityCard({
  detail,
  names,
  composer,
  className = "",
}: {
  detail: WorkTaskDetail;
  names: string[];
  composer: React.ReactNode;
  className?: string;
}) {
  const t = useT();
  const [filter, setFilter] = useState<ActivityFilter>("all");
  const entries = activity(detail, filter);
  const all = activity(detail, "all");
  const segments: { key: ActivityFilter; label: string }[] = [
    { key: "all", label: t("Всё") },
    { key: "comments", label: `${t("Комментарии")} ${detail.comments.length}` },
    { key: "history", label: `${t("История")} ${detail.history.length}` },
  ];

  return (
    <section className={`${card} ${className}`} aria-labelledby="task-activity">
      {/* Компьютер */}
      <div className="max-lg:hidden">
        <div className="flex items-center justify-between gap-3">
          <h2 id="task-activity" className={cardTitle}>
            <Activity className="size-[18px] text-ink-2" aria-hidden />
            {t("Активность")}
          </h2>
          <div role="tablist" aria-label={t("Что показывать")} className="inline-flex rounded-xl border border-line bg-ai-bg p-1">
            {segments.map((s) => (
              <button
                key={s.key}
                type="button"
                role="tab"
                aria-selected={filter === s.key}
                onClick={() => setFilter(s.key)}
                className={`h-8 rounded-lg px-3 text-[13px] font-medium tabular-nums ${filter === s.key ? "bg-surface-2 text-ink shadow-sm" : "text-ink-3 hover:text-ink"}`}
              >
                {s.label}
              </button>
            ))}
          </div>
        </div>
        {composer}
        <Timeline entries={entries} names={names} size={28} />
        {entries.length === 0 && <p className="mt-4 text-sm text-subtle">{filter === "comments" ? t("Комментариев пока нет") : t("Пока пусто")}</p>}
      </div>

      {/* Телефон */}
      <div className="lg:hidden">
        <div className="flex items-center justify-between gap-3">
          <h2 className={cardTitle}>{t("История")}</h2>
          <span className="text-[13px] text-subtle">
            {detail.comments.length === 0 ? t("комментариев нет") : t(plural(detail.comments.length, "{n} комментарий", "{n} комментария", "{n} комментариев"), { n: detail.comments.length })}
          </span>
        </div>
        <Timeline entries={all} names={names} size={26} compact />
      </div>
    </section>
  );
}

function Timeline({ entries, names, size, compact = false }: { entries: ReturnType<typeof activity>; names: string[]; size: number; compact?: boolean }) {
  return (
    <ol className={compact ? "mt-3" : "mt-5"}>
      {entries.map((e, i) => {
        const look = eventIcon[e.kind === "comment" ? "comment" : e.look];
        const last = i === entries.length - 1;
        return (
          <li key={`${e.kind}-${e.id}`} className="relative flex gap-3 pb-4 last:pb-0">
            {!last && <span aria-hidden className="absolute bottom-0 w-px bg-line-strong" style={{ left: size / 2, top: size + 4 }} />}
            <span className={`relative grid shrink-0 place-items-center rounded-full ${look.tone}`} style={{ width: size, height: size }}>
              <look.icon className="size-3.5" aria-hidden />
            </span>
            <div className="min-w-0 flex-1 pt-0.5">
              {e.kind === "comment" ? (
                <>
                  <p className="text-[13px]">
                    <span className="font-semibold text-ink">{e.who}</span>
                    <span className="text-subtle"> · {stamp(e.at, !compact)}</span>
                  </p>
                  <p className="mt-1.5 whitespace-pre-wrap rounded-xl rounded-tl-sm bg-surface-2 px-3 py-2 text-sm leading-relaxed text-ink-2">
                    <CommentText text={e.text} names={names} />
                  </p>
                </>
              ) : compact ? (
                <>
                  <p className="text-[13px] font-semibold text-ink">{e.text}</p>
                  <p className="text-[11px] text-subtle">
                    {e.who} · {stamp(e.at, false)}
                  </p>
                </>
              ) : (
                <>
                  <p className="text-[13px]">
                    <span className="font-semibold text-ink">{e.text}</span>
                    <span className="text-ink-3"> · {e.who}</span>
                  </p>
                  <p className="text-[11px] tabular-nums text-subtle">{stamp(e.at)}</p>
                </>
              )}
            </div>
          </li>
        );
      })}
    </ol>
  );
}

const emojis = ["👍", "👌", "🙏", "✅", "❌", "🔥", "🎉", "👀", "💡", "⚠️", "📌", "📎", "🙂", "😊", "😂", "😉", "🤝", "💪", "🚀", "⏰", "📅", "✍️", "❓", "❗"];

/**
 * Поле комментария: @ — подсказка тех, кто видит задачу (↑↓, Enter, Esc), эмодзи, скрепка — вложение к задаче. Ctrl/⌘+Enter отправляет.
 * Компьютер — аватар и поле в ленте; телефон — капсула, закреплённая над нижней панелью.
 */
export function Composer({
  variant,
  meName,
  people,
  onSend,
  onAttach,
  canAttach,
}: {
  variant: "desktop" | "mobile";
  meName: string | null;
  people: WorkAssignee[];
  onSend: (text: string, mentions: string[]) => Promise<boolean>;
  onAttach: (files: FileList) => void;
  canAttach: boolean;
}) {
  const t = useT();
  const [text, setText] = useState("");
  const [sending, setSending] = useState(false);
  const [mention, setMention] = useState<{ start: number; query: string } | null>(null);
  const [cursor, setCursor] = useState(0);
  const [emoji, setEmoji] = useState(false);
  const area = useRef<HTMLTextAreaElement>(null);
  const file = useRef<HTMLInputElement>(null);

  const matches = useMemo(() => {
    if (!mention) return [];
    const q = mention.query.toLocaleLowerCase("ru");
    return people.filter((p) => p.name.toLocaleLowerCase("ru").includes(q)).slice(0, 6);
  }, [mention, people]);

  const sync = (value: string, caret: number) => {
    setText(value);
    setMention(mentionAt(value, caret));
    setCursor(0);
  };

  const insertAt = (piece: string) => {
    const el = area.current;
    const start = el?.selectionStart ?? text.length;
    const end = el?.selectionEnd ?? text.length;
    const value = text.slice(0, start) + piece + text.slice(end);
    sync(value, start + piece.length);
    requestAnimationFrame(() => {
      el?.focus();
      el?.setSelectionRange(start + piece.length, start + piece.length);
    });
  };

  const pick = (person: WorkAssignee) => {
    if (!mention) return;
    const caret = area.current?.selectionStart ?? text.length;
    const r = insertMention(text, mention.start, caret, person.name);
    setText(r.text);
    setMention(null);
    requestAnimationFrame(() => {
      area.current?.focus();
      area.current?.setSelectionRange(r.caret, r.caret);
    });
  };

  const send = async () => {
    const body = text.trim();
    if (!body || sending) return;
    setSending(true);
    if (await onSend(body, mentionedIds(body, people))) {
      setText("");
      setMention(null);
    }
    setSending(false);
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (mention && matches.length > 0) {
      if (e.key === "ArrowDown" || e.key === "ArrowUp") {
        e.preventDefault();
        setCursor((c) => (c + (e.key === "ArrowDown" ? 1 : matches.length - 1)) % matches.length);
        return;
      }
      if (e.key === "Enter" || e.key === "Tab") {
        e.preventDefault();
        pick(matches[cursor]);
        return;
      }
      if (e.key === "Escape") {
        e.preventDefault();
        setMention(null);
        return;
      }
    }
    if (e.key === "Enter" && (e.metaKey || e.ctrlKey)) {
      e.preventDefault();
      void send();
    }
  };

  const suggestions = mention && matches.length > 0 && (
    <ul role="listbox" aria-label={t("Кого упомянуть")} className="absolute bottom-full left-0 z-30 mb-2 w-72 max-w-full overflow-hidden rounded-xl border border-line-strong bg-surface-solid p-1 shadow-xl">
      {matches.map((p, i) => (
        <li key={p.id}>
          <button
            type="button"
            role="option"
            aria-selected={i === cursor}
            onMouseDown={(e) => e.preventDefault()}
            onClick={() => pick(p)}
            className={`flex w-full items-center gap-2.5 rounded-lg px-2 py-1.5 text-left ${i === cursor ? "bg-surface-2" : ""}`}
          >
            <UserAvatar name={p.name} size={26} />
            <span className="min-w-0">
              <span className="block truncate text-sm font-medium text-ink">{p.name}</span>
              {(p.position || p.department) && <span className="block truncate text-[11px] text-subtle">{p.position ?? p.department}</span>}
            </span>
          </button>
        </li>
      ))}
    </ul>
  );

  const fileInput = canAttach && (
    <input
      ref={file}
      type="file"
      multiple
      className="sr-only"
      tabIndex={-1}
      onChange={(e) => {
        if (e.target.files?.length) onAttach(e.target.files);
        e.target.value = "";
      }}
    />
  );

  if (variant === "mobile") {
    return (
      <div className="relative">
        {suggestions}
        <div className="flex items-end gap-2 rounded-[26px] border border-line-strong bg-surface p-1.5 shadow-[0_10px_30px_rgb(0_0_0/0.35)]">
          {canAttach && (
            <button type="button" onClick={() => file.current?.click()} aria-label={t("Прикрепить файл к задаче")} className="grid size-10 shrink-0 place-items-center rounded-full bg-surface-2 text-ink-2">
              <Paperclip className="size-[18px]" aria-hidden />
            </button>
          )}
          <textarea
            ref={area}
            value={text}
            rows={1}
            onChange={(e) => sync(e.target.value, e.target.selectionStart)}
            onKeyDown={onKeyDown}
            placeholder={t("Комментарий… @ — упомянуть")}
            aria-label={t("Комментарий")}
            maxLength={4000}
            className="max-h-32 min-h-10 flex-1 resize-none bg-transparent px-1 py-2.5 text-[15px] text-ink placeholder:text-subtle focus:outline-none [field-sizing:content]"
          />
          <button
            type="button"
            onClick={() => void send()}
            disabled={!text.trim() || sending}
            aria-label={t("Отправить")}
            className={`grid size-10 shrink-0 place-items-center rounded-full ${text.trim() ? "bg-accent text-white" : "bg-surface-2 text-subtle"}`}
          >
            {sending ? <Loader2 className="size-[18px] animate-spin" aria-hidden /> : <Send className="size-[18px]" aria-hidden />}
          </button>
        </div>
        {fileInput}
      </div>
    );
  }

  return (
    <div className="mt-4 flex gap-3">
      <UserAvatar name={meName} size={32} tone="brand" />
      <div className="relative min-w-0 flex-1">
        {suggestions}
        <div className="rounded-xl border border-line bg-ai-bg focus-within:border-accent/60">
          <textarea
            ref={area}
            value={text}
            rows={2}
            onChange={(e) => sync(e.target.value, e.target.selectionStart)}
            onKeyDown={onKeyDown}
            placeholder={t("Написать комментарий… Используйте @, чтобы упомянуть коллегу")}
            aria-label={t("Комментарий")}
            maxLength={4000}
            className="block max-h-60 min-h-14 w-full resize-none bg-transparent px-3.5 pt-3 text-sm text-ink placeholder:text-subtle focus:outline-none [field-sizing:content]"
          />
          <div className="flex items-center gap-1 px-2.5 pb-2.5">
            {canAttach && (
              <button type="button" onClick={() => file.current?.click()} aria-label={t("Прикрепить файл к задаче")} title={t("Прикрепить файл к задаче")} className="grid size-8 place-items-center rounded-lg text-ink-3 hover:bg-surface-2 hover:text-ink">
                <Paperclip className="size-[18px]" aria-hidden />
              </button>
            )}
            <button type="button" onClick={() => insertAt(text && !/\s$/.test(text) ? " @" : "@")} aria-label={t("Упомянуть коллегу")} title={t("Упомянуть коллегу")} className="grid size-8 place-items-center rounded-lg text-ink-3 hover:bg-surface-2 hover:text-ink">
              <AtSign className="size-[18px]" aria-hidden />
            </button>
            <div className="relative">
              <button type="button" onClick={() => setEmoji((v) => !v)} aria-expanded={emoji} aria-label={t("Эмодзи")} title={t("Эмодзи")} className="grid size-8 place-items-center rounded-lg text-ink-3 hover:bg-surface-2 hover:text-ink">
                <Smile className="size-[18px]" aria-hidden />
              </button>
              {emoji && (
                <div className="absolute bottom-full left-0 z-30 mb-2 grid w-64 grid-cols-8 gap-0.5 rounded-xl border border-line-strong bg-surface-solid p-1.5 shadow-xl" onMouseLeave={() => setEmoji(false)}>
                  {emojis.map((e) => (
                    <button
                      key={e}
                      type="button"
                      onClick={() => {
                        insertAt(e);
                        setEmoji(false);
                      }}
                      className="grid size-7 place-items-center rounded-md text-base hover:bg-surface-2"
                    >
                      {e}
                    </button>
                  ))}
                </div>
              )}
            </div>
            <span className="flex-1" />
            <button
              type="button"
              onClick={() => void send()}
              disabled={!text.trim() || sending}
              className={`inline-flex h-8 items-center gap-1.5 rounded-lg px-3.5 text-[13px] font-semibold ${text.trim() ? "bg-accent text-white" : "bg-surface-2 text-subtle"}`}
            >
              {sending && <Loader2 className="size-3.5 animate-spin" aria-hidden />}
              {t("Отправить")}
            </button>
          </div>
        </div>
        {fileInput}
      </div>
    </div>
  );
}

// ---------- Детали ----------

type Field = "project" | "assignee" | "priority" | "start" | "due";

/**
 * Детали задачи. Редактируемое поле (постановщику и руководителю) — по нажатию: на компьютере поповер у строки (карандаш по наведению),
 * на телефоне — шторка. Значение сохраняется сразу; ошибка возвращает прежнее и пишется под строкой. Отдел, постановщик и дата
 * создания не меняются.
 */
export function DetailsCard({
  task,
  projects,
  canEdit,
  onSave,
  error,
  className = "",
}: {
  task: WorkTask;
  projects: WorkProject[];
  canEdit: boolean;
  onSave: (patch: Partial<TaskInput>, field: Field) => Promise<boolean>;
  error: { field: string; text: string } | null;
  className?: string;
}) {
  const t = useT();
  const [open, setOpen] = useState<{ field: Field; sheet: boolean } | null>(null);
  const [assignees, setAssignees] = useState<WorkAssignee[]>([]);
  const today = todayInTashkent();

  useEffect(() => {
    if (open?.field !== "assignee") return;
    let alive = true;
    bff<WorkAssignee[]>(`work/assignees?department=${encodeURIComponent(task.departmentCode)}${task.projectId ? `&projectId=${task.projectId}` : ""}`)
      .then((list) => alive && setAssignees(list))
      .catch(() => alive && setAssignees([]));
    return () => {
      alive = false;
    };
  }, [open?.field, task.departmentCode, task.projectId]);

  const save = async (patch: Partial<TaskInput>, field: Field) => {
    setOpen(null);
    await onSave(patch, field);
  };

  const editor = (field: Field) => {
    switch (field) {
      case "project": {
        const options = projects.filter((p) => p.departmentCode === task.departmentCode && (!p.archived || p.id === task.projectId));
        return (
          <OptionList
            options={[{ value: "", label: t("Без проекта") }, ...options.map((p) => ({ value: p.id, label: p.name }))]}
            value={task.projectId ?? ""}
            onPick={(v) => void save({ projectId: v || null }, "project")}
          />
        );
      }
      case "priority":
        return (
          <OptionList
            options={[...workPriorities].reverse().map((p) => ({ value: p, label: t(workPriorityLabel[p]), node: <PriorityPill priority={p} /> }))}
            value={task.priority}
            onPick={(v) => void save({ priority: v as WorkTaskPriority }, "priority")}
          />
        );
      case "assignee":
        return (
          <AssigneePicker
            value={task.assigneeId ?? ""}
            onChange={(id) => void save({ assigneeId: id || null }, "assignee")}
            assignees={assignees}
            current={task.assigneeId ? { id: task.assigneeId, name: task.assigneeName } : null}
          />
        );
      case "start":
      case "due": {
        const key = field === "start" ? "startDate" : "dueDate";
        const value = field === "start" ? task.startDate : task.dueDate;
        return (
          <div className="space-y-2">
            <input
              type="date"
              defaultValue={value ?? ""}
              min={field === "due" && !(task.dueDate && task.dueDate < today) ? today : undefined}
              autoFocus
              onChange={(e) => e.target.value && void save({ [key]: e.target.value }, field)}
              className="h-11 w-full rounded-xl border border-line bg-ai-bg px-3 text-sm text-ink focus:border-accent focus:outline-none"
              aria-label={field === "start" ? t("Начало") : t("Срок")}
            />
            {value && (
              <button type="button" onClick={() => void save({ [key]: null }, field)} className="h-10 w-full rounded-xl text-sm font-medium text-ink-3 hover:bg-surface-2 hover:text-ink">
                {field === "start" ? t("Убрать дату начала") : t("Убрать срок")}
              </button>
            )}
          </div>
        );
      }
    }
  };

  const empty = (text: string) => <span className="text-subtle">{t(text)}</span>;
  const rows: { field?: Field; key: string; icon: LucideIcon; label: string; value: React.ReactNode; mobileOrder: number }[] = [
    { key: "department", icon: Building2, label: t("Отдел"), value: task.departmentName, mobileOrder: 99 },
    { key: "project", field: "project", icon: Folder, label: t("Проект"), value: task.projectName ?? empty("Не выбран"), mobileOrder: 5 },
    {
      key: "author",
      icon: UserRoundPen,
      label: t("Постановщик"),
      value: task.createdByName ? <Person name={task.createdByName} /> : empty("Не указан"),
      mobileOrder: 2,
    },
    {
      key: "assignee",
      field: "assignee",
      icon: UserRound,
      label: t("Исполнитель"),
      value: task.assigneeName ? <Person name={task.assigneeName} /> : empty("Любой сотрудник отдела"),
      mobileOrder: 1,
    },
    { key: "priority", field: "priority", icon: Flag, label: t("Приоритет"), value: t(workPriorityLabel[task.priority]), mobileOrder: 6 },
    { key: "created", icon: CalendarPlus, label: t("Создана"), value: <span className="tabular-nums">{stamp(task.createdAt)}</span>, mobileOrder: 7 },
    { key: "start", field: "start", icon: CalendarClock, label: t("Начало"), value: task.startDate ? <span className="tabular-nums">{shortDate(task.startDate)}</span> : empty("Не задано"), mobileOrder: 4 },
    {
      key: "due",
      field: "due",
      icon: CalendarCheck,
      label: t("Срок"),
      value: task.dueDate ? (
        <span className={`tabular-nums ${task.overdue ? "font-semibold text-bad" : ""}`}>{shortDate(task.dueDate)}</span>
      ) : (
        <span className="font-semibold text-warn">{t("Не задан")}</span>
      ),
      mobileOrder: 3,
    },
  ];
  const editable = (field?: Field) => !!field && canEdit;
  const mobileRows = rows.filter((r) => r.key !== "department").sort((a, b) => a.mobileOrder - b.mobileOrder);

  return (
    <section className={`${card} ${className}`} aria-labelledby="task-details">
      <h2 id="task-details" className={`${cardTitle} mb-2 max-lg:sr-only`}>
        {t("Детали")}
      </h2>

      {/* Компьютер: строки с карандашом и поповером. */}
      <dl className="max-lg:hidden">
        {rows.map((r) => (
          <div key={r.key} className="group relative border-b border-line last:border-0">
            <div className="flex min-h-[52px] items-center gap-3 py-2">
              <r.icon className="size-[15px] shrink-0 text-subtle" aria-hidden />
              <dt className="w-[104px] shrink-0 text-[13px] text-ink-3">{r.label}</dt>
              <dd className="min-w-0 flex-1 truncate text-[13px] text-ink">{r.value}</dd>
              {r.key === "due" && !task.dueDate && canEdit ? (
                <button type="button" onClick={() => setOpen({ field: "due", sheet: false })} className="h-7 shrink-0 rounded-md bg-warn-soft px-2.5 text-xs font-semibold text-warn">
                  {t("Указать")}
                </button>
              ) : (
                editable(r.field) && (
                  <button
                    type="button"
                    onClick={() => setOpen(open?.field === r.field ? null : { field: r.field!, sheet: false })}
                    aria-label={t("Изменить: {field}", { field: r.label })}
                    className="grid size-7 shrink-0 place-items-center rounded-md text-subtle opacity-0 hover:bg-surface-2 hover:text-ink focus-visible:opacity-100 group-hover:opacity-100"
                  >
                    <Pencil className="size-3.5" aria-hidden />
                  </button>
                )
              )}
            </div>
            {error && r.field && error.field === r.field && <p className="-mt-1 pb-2 pl-[131px] text-xs text-bad">{error.text}</p>}
            {open && !open.sheet && open.field === r.field && <Popover onClose={() => setOpen(null)}>{editor(open.field)}</Popover>}
          </div>
        ))}
      </dl>

      {/* Телефон: строки с шевроном, выбор — в шторке. */}
      <ul className="lg:hidden">
        {mobileRows.map((r) => {
          const tap = editable(r.field) ? () => setOpen({ field: r.field!, sheet: true }) : undefined;
          const content = (
            <>
              <r.icon className="size-[18px] shrink-0 text-subtle" aria-hidden />
              <span className="shrink-0 text-sm text-ink-3">{r.label}</span>
              <span className="min-w-0 flex-1 truncate text-right text-sm text-ink">
                {r.key === "due" && !task.dueDate && canEdit ? (
                  <span className="inline-flex items-center gap-1 rounded-md bg-warn-soft px-2 py-1 text-xs font-semibold text-warn">
                    <Plus className="size-3.5" aria-hidden />
                    {t("Указать срок")}
                  </span>
                ) : (
                  r.value
                )}
              </span>
              {tap && !(r.key === "due" && !task.dueDate) && <ChevronRight className="size-4 shrink-0 text-subtle" aria-hidden />}
            </>
          );
          return (
            <li key={r.key} className="border-b border-line last:border-0">
              {tap ? (
                <button type="button" onClick={tap} className="flex min-h-[52px] w-full items-center gap-3 py-3 text-left">
                  {content}
                </button>
              ) : (
                <div className="flex min-h-[52px] items-center gap-3 py-3">{content}</div>
              )}
              {error && r.field && error.field === r.field && <p className="pb-2 text-xs text-bad">{error.text}</p>}
            </li>
          );
        })}
      </ul>

      <Sheet open={!!open?.sheet} onClose={() => setOpen(null)} title={open ? (rows.find((r) => r.field === open.field)?.label ?? "") : ""}>
        {open?.sheet && editor(open.field)}
      </Sheet>
    </section>
  );
}

function Person({ name }: { name: string }) {
  return (
    <span className="inline-flex min-w-0 max-w-full items-center gap-2 align-middle">
      <UserAvatar name={name} size={22} tone="brand" />
      <span className="truncate">{name}</span>
    </span>
  );
}

function Popover({ onClose, children }: { onClose: () => void; children: React.ReactNode }) {
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    // Нажатие в своей строке (карандаш) переключает поповер само — закрываем только по нажатию вне строки.
    const onDown = (e: PointerEvent) => !box.current?.parentElement?.contains(e.target as Node) && onClose();
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [onClose]);
  return (
    <div ref={box} role="dialog" className="absolute right-0 top-full z-30 -mt-1 w-[300px] rounded-xl border border-line-strong bg-surface-solid p-2 shadow-[0_16px_40px_rgb(0_0_0/0.35)]">
      {children}
    </div>
  );
}

function OptionList({ options, value, onPick }: { options: { value: string; label: string; node?: React.ReactNode }[]; value: string; onPick: (v: string) => void }) {
  return (
    <ul role="listbox" className="max-h-72 overflow-y-auto">
      {options.map((o) => (
        <li key={o.value}>
          <button
            type="button"
            role="option"
            aria-selected={o.value === value}
            onClick={() => onPick(o.value)}
            className="flex min-h-11 w-full items-center gap-2.5 rounded-lg px-2.5 text-left text-sm text-ink hover:bg-surface-2 lg:min-h-10"
          >
            <span className="min-w-0 flex-1 truncate">{o.node ?? o.label}</span>
            {o.value === value && <Check className="size-4 shrink-0 text-accent-strong" aria-hidden />}
          </button>
        </li>
      ))}
    </ul>
  );
}

// ---------- Выполнение ----------

/** Выполнение: значение, полоса, быстрые значения (сохраняются сразу). С чек-листом процент считается сам — сегменты неактивны. */
export function ProgressCard({ task, canSet, onSet, className = "" }: { task: WorkTask; canSet: boolean; onSet: (value: number) => void; className?: string }) {
  const t = useT();
  const full = task.progress >= 100;
  const auto = task.checklistTotal > 0;
  return (
    <section className={`${card} ${className}`} aria-labelledby="task-progress">
      <div className="flex items-center justify-between gap-3">
        <h2 id="task-progress" className={cardTitle}>
          {t("Выполнение")}
        </h2>
        <span className={`text-lg font-bold tabular-nums ${full ? "text-ok" : "text-accent-strong"}`}>{task.progress}%</span>
      </div>
      <div className="mt-3 h-2 overflow-hidden rounded-full bg-surface-2" role="progressbar" aria-valuemin={0} aria-valuemax={100} aria-valuenow={task.progress} aria-label={t("Выполнение")}>
        <div className={`h-full rounded-full transition-[width] ${full ? "bg-ok" : "bg-accent"}`} style={{ width: `${task.progress}%` }} />
      </div>
      <div className="mt-3 grid grid-cols-5 gap-1 rounded-xl border border-line bg-ai-bg p-1" role="radiogroup" aria-label={t("Процент выполнения")}>
        {[0, 25, 50, 75, 100].map((v) => {
          const on = task.progress === v;
          return (
            <button
              key={v}
              type="button"
              role="radio"
              aria-checked={on}
              disabled={!canSet || auto}
              onClick={() => !on && onSet(v)}
              className={`h-9 rounded-lg text-[13px] font-semibold tabular-nums transition-colors disabled:cursor-default ${
                on ? (v === 100 ? "border border-ok/40 bg-ok-soft text-ok" : "border border-accent/40 bg-accent-soft text-accent-strong") : "text-ink-3 enabled:hover:bg-surface-2 enabled:hover:text-ink"
              }`}
            >
              {v}%
            </button>
          );
        })}
      </div>
      <p className="mt-2.5 text-[11px] text-subtle max-lg:hidden">
        {auto ? t("Считается по чек-листу: {done} из {total} пунктов", { done: task.checklistDone, total: task.checklistTotal }) : t("Указывается вручную, пока в чек-листе нет пунктов")}
      </p>
    </section>
  );
}

// ---------- Вложения ----------

const fileIcon = (type: string): LucideIcon =>
  type.startsWith("image/")
    ? FileImage
    : type.includes("sheet") || type.includes("excel") || type === "text/csv"
      ? FileSpreadsheet
      : type.includes("zip")
        ? FileArchive
        : type.includes("pdf") || type.startsWith("text/") || type.includes("word")
          ? FileText
          : File;

export function AttachmentsCard({
  detail,
  uploading,
  onUpload,
  onDelete,
  className = "",
}: {
  detail: WorkTaskDetail;
  uploading: boolean;
  onUpload: (files: FileList) => void;
  onDelete: (id: string, name: string) => void;
  className?: string;
}) {
  const t = useT();
  const input = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);
  const can = detail.canAttach && !detail.task.archivedAt;

  return (
    <section className={`${card} ${className}`} aria-labelledby="task-files">
      <div className="flex items-center justify-between gap-3">
        <h2 id="task-files" className={cardTitle}>
          <Paperclip className="size-[18px] text-ink-2 max-lg:hidden" aria-hidden />
          {t("Вложения")}
        </h2>
        <span className="text-[13px] tabular-nums text-subtle">
          <span className="max-lg:hidden">{detail.attachments.length}</span>
          <span className="lg:hidden">{t("до 20 МБ")}</span>
        </span>
      </div>

      {detail.attachments.length > 0 && (
        <ul className="mt-3 space-y-1">
          {detail.attachments.map((a) => {
            const Icon = fileIcon(a.contentType);
            const href = `/bff/api/work/attachments/${a.id}`;
            return (
              <li key={a.id} className="group flex items-center gap-3 rounded-xl px-1 py-1.5 hover:bg-surface-2">
                {a.contentType.startsWith("image/") ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={href} alt="" loading="lazy" className="size-10 shrink-0 rounded-lg border border-line object-cover" />
                ) : (
                  <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-surface-2 text-ink-3">
                    <Icon className="size-5" aria-hidden />
                  </span>
                )}
                <a href={href} className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium text-ink group-hover:text-link">{a.fileName}</span>
                  <span className="block truncate text-[11px] text-subtle">
                    {fileSize(a.sizeBytes)}
                    {a.uploadedByName ? ` · ${a.uploadedByName}` : ""} · {stamp(a.createdAt, false)}
                  </span>
                </a>
                <a href={href} aria-label={t("Скачать {name}", { name: a.fileName })} className="grid size-8 shrink-0 place-items-center rounded-md text-subtle hover:text-ink">
                  <Download className="size-4" aria-hidden />
                </a>
                {a.canDelete && (
                  <button type="button" onClick={() => onDelete(a.id, a.fileName)} aria-label={t("Удалить {name}", { name: a.fileName })} className="grid size-8 shrink-0 place-items-center rounded-md text-subtle hover:text-bad">
                    <Trash2 className="size-4" aria-hidden />
                  </button>
                )}
              </li>
            );
          })}
        </ul>
      )}

      {can && (
        <>
          <input
            ref={input}
            type="file"
            multiple
            className="sr-only"
            tabIndex={-1}
            onChange={(e) => {
              if (e.target.files?.length) onUpload(e.target.files);
              e.target.value = "";
            }}
          />
          <div
            onDragOver={(e) => {
              e.preventDefault();
              setOver(true);
            }}
            onDragLeave={() => setOver(false)}
            onDrop={(e) => {
              e.preventDefault();
              setOver(false);
              if (e.dataTransfer.files.length) onUpload(e.dataTransfer.files);
            }}
            className={`mt-3 flex flex-col items-center gap-1 rounded-xl border p-5 text-center transition-colors max-lg:hidden ${over ? "border-accent bg-accent-soft" : "border-line-strong bg-[var(--tasks-ghost)]"}`}
          >
            {uploading ? <Loader2 className="size-5 animate-spin text-ink-2" aria-hidden /> : <Upload className="size-5 text-ink-2" aria-hidden />}
            <p className="mt-1 text-sm text-ink">
              {t("Перетащите файл или")}{" "}
              <button type="button" onClick={() => input.current?.click()} className="font-semibold text-link hover:underline">
                {t("выберите")}
              </button>
            </p>
            <p className="text-[11px] text-subtle">{t("до 20 МБ · файл видят все, кто видит задачу")}</p>
          </div>
          <button type="button" onClick={() => input.current?.click()} className="mt-3 flex h-11 w-full items-center gap-2 rounded-xl border border-line bg-ai-bg px-3.5 text-sm font-semibold text-link lg:hidden">
            {uploading ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <Paperclip className="size-4" aria-hidden />}
            {t("Прикрепить файл или фото")}
          </button>
        </>
      )}
      {!can && detail.attachments.length === 0 && <p className="mt-3 text-sm text-subtle">{t("Вложений нет")}</p>}
    </section>
  );
}

