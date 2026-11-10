"use client";

import Link from "next/link";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import {
  CalendarCheck,
  Check,
  ChevronLeft,
  CircleAlert,
  Clock,
  EllipsisVertical,
  FileText,
  Hand,
  IdCard,
  Info,
  Loader2,
  Paperclip,
  RotateCcw,
  Send,
  SendHorizontal,
  Sparkles,
  UserRound,
  X,
} from "lucide-react";
import { useT } from "@/i18n/client";
import type { Dialog, DialogItem } from "@/lib/hr-dialogs";
import { clock, dayLabel, fileSize, lastSeen, sameDay, type DialogMode } from "@/lib/hr-dialogs-view";
import { recruitmentApi } from "@/lib/recruitment";
import { Avatar, FeedSkeleton } from "./parts";

export type ChatActions = {
  send: (text: string, file: File | null) => Promise<void>;
  mode: (mode: "Ai" | "Manual") => Promise<void>;
  retry: (outgoingId: string) => Promise<void>;
  draft: () => Promise<string>;
  refresh: () => void;
};

/** Переключатель «Ведёт AI / Веду я» (radiogroup «Кто ведёт диалог»). «Нужен HR» — уже у человека. */
export function ModeSwitch({ mode, onChange, disabled, wide = false }: { mode: DialogMode; onChange: (m: "Ai" | "Manual") => void; disabled: boolean; wide?: boolean }) {
  const t = useT();
  const ai = mode === "ai";
  // В узкой колонке переписки (компьютер с меню приложения) переключатель — отдельной строкой во всю ширину, как на телефоне.
  const segment = (active: boolean) =>
    `flex items-center justify-center gap-1.5 rounded-lg px-3 text-xs ${wide ? "h-[34px] flex-1" : "h-7 @max-[600px]:h-[34px] @max-[600px]:flex-1"} ${
      active ? "border border-line-strong bg-muted font-semibold text-ink" : "border border-transparent font-medium text-ink-3 hover:text-ink"
    }`;
  return (
    <div role="radiogroup" aria-label={t("Кто ведёт диалог")} className={`flex gap-1 border border-line p-[3px] ${wide ? "w-full rounded-xl bg-surface" : "rounded-[10px] bg-page @max-[600px]:order-last @max-[600px]:w-full"}`}>
      <button type="button" role="radio" aria-checked={ai} disabled={disabled || ai} onClick={() => onChange("Ai")} className={segment(ai)}>
        <Sparkles className="size-3.5" aria-hidden />
        {t("Ведёт AI")}
      </button>
      <button type="button" role="radio" aria-checked={!ai} disabled={disabled || !ai} onClick={() => onChange("Manual")} className={segment(!ai)}>
        <UserRound className="size-3.5" aria-hidden />
        {t("Веду я")}
      </button>
    </div>
  );
}

/** Шапка чата на компьютере: аватар, имя, @username · вакансия · когда писал(а), переключатель режима, меню. */
export function ChatHeader({ dialog, actions, onCandidate }: { dialog: Dialog; actions: ChatActions; onCandidate?: () => void }) {
  const t = useT();
  const [menu, setMenu] = useState(false);
  const seen = lastSeen(dialog.lastInboundAt);
  const meta = [
    dialog.username ? `@${dialog.username}` : null,
    dialog.candidate?.vacancyTitle,
    seen ? t(seen.key, { n: seen.n ?? 0, when: seen.when ? t(seen.when).toLowerCase() : "" }) : null,
  ]
    .filter(Boolean)
    .join(" · ");
  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-2.5 border-b border-line px-4 py-3">
      <Avatar id={dialog.candidate?.id ?? dialog.id} name={dialog.name} size={40} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-[15px] font-bold text-ink">{dialog.name}</p>
        <p className="flex min-w-0 items-center gap-1.5 text-xs text-ink-3">
          <Send className="size-3 shrink-0 text-accent-strong" aria-hidden />
          <span className="truncate">{meta}</span>
        </p>
      </div>
      <ModeSwitch mode={dialog.mode} onChange={actions.mode} disabled={!dialog.canManage} />
      {onCandidate && (
        <button type="button" onClick={onCandidate} aria-label={t("Карточка кандидата")} className="grid size-[34px] place-items-center rounded-[10px] border border-line text-ink-2 hover:text-ink">
          <IdCard className="size-4" />
        </button>
      )}
      <div className="relative">
        <button type="button" onClick={() => setMenu(!menu)} aria-label={t("Ещё")} aria-expanded={menu} className="grid size-[34px] place-items-center rounded-[10px] border border-line text-ink-2 hover:text-ink">
          <EllipsisVertical className="size-4" />
        </button>
        {menu && (
          <div role="menu" className="absolute right-0 top-10 z-20 w-56 rounded-xl border border-line bg-surface-solid p-1 shadow-lg" onMouseLeave={() => setMenu(false)}>
            {dialog.candidate && (
              <Link role="menuitem" href={`/hr/recruitment/candidates/${dialog.candidate.id}`} className="block rounded-lg px-3 py-2 text-sm text-ink hover:bg-muted">
                {t("Открыть карточку кандидата")}
              </Link>
            )}
            <button role="menuitem" type="button" onClick={() => { setMenu(false); actions.refresh(); }} className="block w-full rounded-lg px-3 py-2 text-left text-sm text-ink hover:bg-muted">
              {t("Обновить")}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

/** Шапка чата на телефоне: назад, аватар, имя, @username · вакансия, карточка кандидата. */
export function MobileChatHeader({ dialog, onBack, onCandidate }: { dialog: Dialog; onBack: () => void; onCandidate: () => void }) {
  const t = useT();
  return (
    <div className="flex items-center gap-2.5 border-b border-line px-3 pb-2.5 pt-1.5">
      <button type="button" onClick={onBack} aria-label={t("Назад")} className="grid size-[40px] shrink-0 place-items-center rounded-full bg-surface text-ink">
        <ChevronLeft className="size-5" />
      </button>
      <Avatar id={dialog.candidate?.id ?? dialog.id} name={dialog.name} size={38} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-[15px] font-bold text-ink">{dialog.name}</p>
        <p className="truncate text-[11px] text-ink-3">{[dialog.username ? `@${dialog.username}` : null, dialog.candidate?.vacancyTitle].filter(Boolean).join(" · ")}</p>
      </div>
      <button type="button" onClick={onCandidate} aria-label={t("Карточка кандидата")} className="grid size-[40px] shrink-0 place-items-center rounded-full bg-surface text-ink-2">
        <IdCard className="size-5" />
      </button>
    </div>
  );
}

/** Лента (role=log): разделители дней, пузыри кандидата / AI / HR, системные события; открывается на последнем сообщении. */
export function Feed({ dialog, loading, mobile = false, onRetry }: { dialog: Dialog | null; loading: boolean; mobile?: boolean; onRetry: (id: string) => void }) {
  const t = useT();
  const box = useRef<HTMLDivElement>(null);
  const stick = useRef(true);
  const count = dialog?.items.length ?? 0;
  const id = dialog?.id;

  useEffect(() => {
    stick.current = true; // новый диалог — к последнему сообщению
  }, [id]);
  useLayoutEffect(() => {
    const el = box.current;
    if (el && stick.current) el.scrollTop = el.scrollHeight;
  }, [count, id]);

  if (!dialog) return loading ? <FeedSkeleton /> : <div className="flex-1" />;
  const onScroll = () => {
    const el = box.current;
    if (el) stick.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80;
  };

  return (
    <div ref={box} onScroll={onScroll} role="log" aria-live="polite" aria-label={t("Переписка")} className={`flex min-h-0 flex-1 flex-col overflow-y-auto ${mobile ? "gap-2 px-3.5 py-3" : "gap-2 px-5 py-4"}`}>
      <div className="mt-auto" />
      {dialog.items.map((item, i) => (
        <div key={`${item.at}-${i}`} className="flex flex-col gap-2">
          {(i === 0 || !sameDay(dialog.items[i - 1].at, item.at)) && (
            <span className={`self-center rounded-full bg-muted px-2.5 py-1 font-semibold text-ink-3 ${mobile ? "text-[11px]" : "text-[11px]"}`}>{t(dayLabel(item.at))}</span>
          )}
          <ItemView item={item} mobile={mobile} onRetry={onRetry} />
        </div>
      ))}
      {dialog.items.length === 0 && <p className="self-center text-xs text-ink-3">{t("Сообщений пока нет")}</p>}
    </div>
  );
}

const eventView: Record<string, { icon: typeof Hand; className: string }> = {
  handoff_to_hr: { icon: Hand, className: "bg-warn-soft text-warn" },
  interview_scheduled: { icon: CalendarCheck, className: "bg-ok-soft text-ok" },
  returned_to_ai: { icon: Sparkles, className: "bg-violet-soft text-violet" },
  hr_took_over: { icon: UserRound, className: "bg-accent-soft text-accent-strong" },
};

function ItemView({ item, mobile, onRetry }: { item: DialogItem; mobile: boolean; onRetry: (id: string) => void }) {
  const t = useT();
  if (item.kind === "system") {
    const view = eventView[item.event ?? ""] ?? eventView.hr_took_over;
    const Icon = view.icon;
    return (
      <span className={`flex max-w-full items-center gap-1.5 self-center rounded-full px-3 py-[5px] font-medium ${mobile ? "text-[11px]" : "text-xs"} ${view.className}`}>
        <Icon className="size-3.5 shrink-0" aria-hidden />
        <span className="truncate">{t(item.text ?? "")}</span>
      </span>
    );
  }

  const mine = item.kind !== "candidate";
  const label =
    item.kind === "ai" ? (
      <span className="flex items-center gap-1 text-violet"><Sparkles className="size-3" aria-hidden />{t("AI-ассистент")}</span>
    ) : item.kind === "auto" ? (
      <span className="flex items-center gap-1 text-violet"><Sparkles className="size-3" aria-hidden />{t("Автосообщение OneBase")}</span>
    ) : item.kind === "hr" ? (
      <span className="flex items-center gap-1 text-accent-strong">
        <UserRound className="size-3" aria-hidden />
        {item.self ? `${t("Вы")}${item.author ? ` · ${item.author.split(" ")[0]}${mobile ? "" : ", HR"}` : ""}` : `HR${item.author ? ` · ${item.author}` : ""}`}
      </span>
    ) : null;
  const bubble =
    item.kind === "candidate"
      ? "rounded-[4px_14px_14px_14px] bg-muted text-ink"
      : item.kind === "hr"
        ? "rounded-[14px_4px_14px_14px] bg-accent text-white"
        : "rounded-[14px_4px_14px_14px] border border-violet/20 bg-violet-soft text-ink";
  const meta = item.kind === "hr" ? "text-white/70" : "text-ink-4";
  return (
    <div className={`flex flex-col gap-1 ${mine ? "items-end" : "items-start"}`}>
      {label && <span className={`font-semibold ${mobile ? "text-[10px]" : "text-[11px]"}`}>{label}</span>}
      <div className={`px-3 py-[9px] ${mobile ? "max-w-[280px] text-sm" : "max-w-[420px] text-[13px]"} leading-[1.45] [overflow-wrap:anywhere] ${bubble}`}>
        {item.attachment && <Attachment a={item.attachment} light={item.kind === "hr"} />}
        {item.text && <p className="whitespace-pre-wrap">{item.text}</p>}
        <span className={`mt-1 flex items-center justify-end gap-1 text-[10px] tabular-nums ${meta}`}>
          {item.status === "pending" ? <Clock className="size-3" aria-label={t("Отправляется")} /> : clock(item.at)}
          {mine && item.status === "sent" && <Check className="size-3" aria-label={t("Отправлено")} />}
        </span>
      </div>
      {(item.status === "failed" || item.status === "unknown") && (
        <span className="flex items-center gap-1 text-[11px] text-bad" title={item.error ?? undefined}>
          <CircleAlert className="size-3.5" aria-hidden />
          {t(item.status === "failed" ? "Не отправлено" : "Неизвестно, дошло ли")}
          {item.outgoingId && (
            <button type="button" onClick={() => onRetry(item.outgoingId!)} className="ml-1 inline-flex items-center gap-1 font-semibold underline">
              <RotateCcw className="size-3" aria-hidden />
              {t("Повторить")}
            </button>
          )}
        </span>
      )}
    </div>
  );
}

function Attachment({ a, light }: { a: NonNullable<DialogItem["attachment"]>; light: boolean }) {
  const t = useT();
  if (a.image && a.canPreview && a.resumeId) {
    // eslint-disable-next-line @next/next/no-img-element
    return <img src={recruitmentApi.previewUrl(a.resumeId)} alt={a.fileName} loading="lazy" className="mb-1.5 max-h-60 rounded-[10px] object-cover" />;
  }

  const body = (
    <span className={`mb-1.5 flex items-center gap-2 rounded-lg px-2 py-1.5 text-xs ${light ? "bg-white/15" : "bg-surface"}`}>
      <FileText className="size-4 shrink-0" aria-hidden />
      <span className="min-w-0 truncate font-medium">{a.fileName}</span>
      {a.size != null && <span className="shrink-0 opacity-70">{fileSize(a.size, [t("КБ"), t("МБ")])}</span>}
    </span>
  );
  return a.resumeId ? (
    <a href={recruitmentApi.resumeUrl(a.resumeId)} className="block hover:opacity-90">
      {body}
    </a>
  ) : (
    body
  );
}

/**
 * Поле ввода: шаблоны (подставляют текст, не отправляют), файл, «Черновик AI» (текст в поле, не отправляется), отправка. Enter — отправить,
 * Shift+Enter — перенос (на компьютере). Подсказка — что будет с AI.
 */
export function Composer({ dialog, actions, mobile = false }: { dialog: Dialog; actions: ChatActions; mobile?: boolean }) {
  const t = useT();
  const [text, setText] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState<"send" | "draft" | null>(null);
  const [error, setError] = useState<string | null>(null);
  const area = useRef<HTMLTextAreaElement>(null);
  const picker = useRef<HTMLInputElement>(null);

  useLayoutEffect(() => {
    const el = area.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, mobile ? 120 : 5 * 19 + 20)}px`;
  }, [text, mobile]);

  if (!dialog.canManage) {
    return <p className="border-t border-line px-4 py-3 text-xs text-ink-3">{t("Писать кандидатам может HR с правом «Подбор персонала: управление».")}</p>;
  }

  const run = async (what: "send" | "draft", action: () => Promise<void>) => {
    setBusy(what);
    setError(null);
    try {
      await action();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };
  const send = () => {
    if (busy || (!text.trim() && !file)) return;
    void run("send", async () => {
      await actions.send(text.trim(), file);
      setText("");
      setFile(null);
    });
  };
  const draft = () => run("draft", async () => setText(await actions.draft()));
  const hint =
    dialog.mode === "hr"
      ? t("Вы ведёте диалог — AI не отвечает. Переключите на «Ведёт AI», чтобы вернуть автоответы.")
      : dialog.mode === "need_hr"
        ? t("AI передал диалог вам ({reason}). Ответьте — AI не будет вмешиваться.", { reason: t(dialog.handoffReason ?? "нужен человек") })
        : t("Сейчас отвечает AI. Если напишете сами, диалог перейдёт к вам и AI замолчит.");

  return (
    <div className={`border-t border-line ${mobile ? "bg-surface px-3 pb-[calc(12px+env(safe-area-inset-bottom))] pt-2.5" : "px-4 pb-4 pt-3"}`}>
      {dialog.templates.length > 0 && (
        <div className={`mb-2.5 flex items-center gap-1.5 ${mobile ? "-mx-3 overflow-x-auto px-3 [scrollbar-width:none]" : "flex-wrap"}`}>
          {!mobile && <span className="text-xs text-ink-4">{t("Шаблоны:")}</span>}
          {dialog.templates.map((tpl) => (
            <button
              key={tpl.key}
              type="button"
              onClick={() => {
                setText(tpl.text);
                area.current?.focus();
              }}
              title={tpl.text}
              className={`shrink-0 rounded-lg bg-muted font-medium text-ink-2 hover:text-ink ${mobile ? "px-2.5 py-[5px] text-xs" : "px-2.5 py-1 text-xs"}`}
            >
              {t(tpl.label)}
            </button>
          ))}
        </div>
      )}
      {file && (
        <div className="mb-2 flex items-center gap-2 rounded-lg bg-muted px-2.5 py-1.5 text-xs text-ink-2">
          <FileText className="size-4 shrink-0" aria-hidden />
          <span className="min-w-0 flex-1 truncate">{file.name}</span>
          <span className="shrink-0 text-ink-4">{fileSize(file.size, [t("КБ"), t("МБ")])}</span>
          <button type="button" onClick={() => setFile(null)} aria-label={t("Убрать файл")} className="text-ink-3 hover:text-ink">
            <X className="size-4" />
          </button>
        </div>
      )}
      <div className="flex items-end gap-2.5">
        <input
          ref={picker}
          type="file"
          className="hidden"
          accept=".pdf,.doc,.docx,.xls,.xlsx,.txt,.jpg,.jpeg,.png"
          onChange={(e) => {
            setFile(e.target.files?.[0] ?? null);
            e.target.value = "";
          }}
        />
        <button
          type="button"
          onClick={() => picker.current?.click()}
          aria-label={t("Прикрепить файл")}
          className={`grid shrink-0 place-items-center border border-line bg-surface text-ink-2 hover:text-ink ${mobile ? "size-[40px] rounded-full" : "size-[42px] rounded-xl"}`}
        >
          <Paperclip className="size-[18px]" />
        </button>
        <div className={`flex min-w-0 flex-1 items-end border border-line-strong bg-page ${mobile ? "rounded-[20px] pr-1" : "rounded-xl"}`}>
          <textarea
            ref={area}
            rows={1}
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={(e) => {
              if (!mobile && e.key === "Enter" && !e.shiftKey) {
                e.preventDefault();
                send();
              }
            }}
            placeholder={mobile ? t("Сообщение от HR…") : t("Ответ кандидату от имени HR…")}
            aria-label={t("Сообщение кандидату")}
            className={`min-h-0 flex-1 resize-none bg-transparent text-ink outline-none placeholder:text-ink-4 ${mobile ? "px-4 py-[9px] text-sm" : "px-3.5 py-[11px] text-[13px] leading-[19px]"}`}
          />
          {/* «Черновик AI» внутри поля — на телефоне и в узкой колонке переписки; в широкой — кнопка с подписью рядом. */}
          <button
            type="button"
            onClick={draft}
            disabled={!!busy}
            aria-label={t("Черновик AI")}
            className={`mb-[5px] size-[30px] shrink-0 place-items-center rounded-full bg-violet-soft text-violet ${mobile ? "grid" : "mr-1 hidden @max-[600px]:grid"}`}
          >
            {busy === "draft" ? <Loader2 className="size-4 animate-spin" /> : <Sparkles className="size-4" />}
          </button>
        </div>
        {!mobile && (
          <button
            type="button"
            onClick={draft}
            disabled={!!busy}
            className="flex h-[42px] shrink-0 items-center gap-1.5 rounded-xl border border-violet/25 bg-violet-soft px-3.5 text-[13px] font-semibold text-violet disabled:opacity-60 @max-[600px]:hidden"
          >
            {busy === "draft" ? <Loader2 className="size-4 animate-spin" /> : <Sparkles className="size-4" aria-hidden />}
            {t("Черновик AI")}
          </button>
        )}
        <button
          type="button"
          onClick={send}
          disabled={!!busy || (!text.trim() && !file)}
          aria-label={t("Отправить")}
          className={`grid shrink-0 place-items-center bg-accent text-white disabled:opacity-50 ${mobile ? "size-[40px] rounded-full" : "size-[42px] rounded-xl"}`}
        >
          {busy === "send" ? <Loader2 className="size-[18px] animate-spin" /> : <SendHorizontal className="size-[18px]" />}
        </button>
      </div>
      {error && <p className="mt-2 text-xs text-bad">{t(error)}</p>}
      {!mobile && (
        <p className="mt-2 flex items-center gap-1.5 text-[11px] text-ink-4">
          <Info className="size-3.5 shrink-0" aria-hidden />
          {hint}
        </p>
      )}
    </div>
  );
}
