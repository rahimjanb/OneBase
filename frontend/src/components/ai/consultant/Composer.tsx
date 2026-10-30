"use client";

import { useEffect, useRef } from "react";
import { ArrowUp, Building2, CalendarDays, Database, Loader2, Mic, Square, X } from "lucide-react";
import { useT } from "@/i18n/client";
import type { ChatContext } from "@/lib/consultant-view";
import { useMedia } from "@/lib/media";

const months = ["Январь", "Февраль", "Март", "Апрель", "Май", "Июнь", "Июль", "Август", "Сентябрь", "Октябрь", "Ноябрь", "Декабрь"];

/** Строк в поле до прокрутки. */
const maxRows = 6;
const lineHeight = 22;

/**
 * Поле вопроса: на компьютере — карточка 840px с контекстом (период, отдел, источники; крестик убирает чип — контекст уходит в запрос),
 * полем до 6 строк, микрофоном и «Отправить» / «Стоп»; на телефоне — капсула над нижней панелью, чипы — одной строкой над ней, только если
 * контекст задан не по умолчанию. Enter — отправить, Shift + Enter — новая строка.
 */
export function Composer({
  value,
  onChange,
  onSubmit,
  onStop,
  busy,
  disabled,
  inChat,
  context,
  defaultPeriod,
  onContext,
  mic,
  textareaRef,
}: {
  value: string;
  onChange: (v: string) => void;
  onSubmit: () => void;
  onStop: () => void;
  busy: boolean;
  disabled: boolean;
  inChat: boolean;
  context: ChatContext;
  defaultPeriod: string;
  onContext: (c: ChatContext) => void;
  mic: { available: boolean; recording: boolean; transcribing: boolean; onClick: () => void } | null;
  textareaRef: React.RefObject<HTMLTextAreaElement | null>;
}) {
  const t = useT();
  // Телефон и планшет — короткая подсказка в поле: длинная переносилась на две строки.
  const compact = useMedia("(width < 64rem)");
  const local = useRef<HTMLTextAreaElement>(null);
  const area = textareaRef ?? local;

  useEffect(() => {
    const el = area.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, maxRows * lineHeight + 16)}px`;
  }, [value, area]);

  const period = context.period ? /^(\d{4})-(\d{2})$/.exec(context.period) : null;
  const chips: { key: string; icon: typeof X; label: string; remove?: () => void }[] = [];
  if (period) chips.push({ key: "period", icon: CalendarDays, label: `${t(months[Number(period[2]) - 1])} ${period[1]}`, remove: () => onContext({ ...context, period: null }) });
  if (context.department)
    chips.push({ key: "dept", icon: Building2, label: t(context.departmentName ?? context.department), remove: () => onContext({ ...context, department: null, departmentName: null }) });
  chips.push({ key: "sources", icon: Database, label: t("Все источники") });
  // На телефоне чипы — только если контекст задан не по умолчанию (другой месяц или отдел).
  const custom = context.department !== null || (context.period !== null && context.period !== defaultPeriod);

  const chipList = (
    <ul className="flex min-w-0 flex-wrap items-center gap-1.5 max-lg:flex-nowrap max-lg:overflow-x-auto max-lg:no-scrollbar">
      {chips.map((c) => (
        <li key={c.key} className="inline-flex shrink-0 items-center gap-1.5 rounded-[7px] bg-surface-2 px-2 py-1 text-[12px] text-ink-2">
          <c.icon className="size-3.5 text-ink-3" aria-hidden />
          {c.label}
          {c.remove && (
            <button type="button" onClick={c.remove} aria-label={t("Убрать из контекста: {label}", { label: c.label })} className="-mr-0.5 grid size-4 place-items-center rounded text-ink-3 hover:text-ink">
              <X className="size-3" aria-hidden />
            </button>
          )}
        </li>
      ))}
    </ul>
  );

  const empty = value.trim() === "";
  const iconButton = "grid size-[38px] shrink-0 place-items-center rounded-full disabled:opacity-40";

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        if (!busy) onSubmit();
      }}
      className="mx-auto w-full max-w-[840px]"
    >
      {custom && <div className="mb-2 lg:hidden">{chipList}</div>}
      <div className="rounded-[20px] border border-line-strong bg-surface p-3 pl-4 shadow-[var(--ai-shadow)] focus-within:border-accent max-lg:rounded-[26px] max-lg:border-line max-lg:p-1.5 max-lg:pl-1.5">
        <div className="mb-2 flex items-center gap-2 max-lg:hidden">
          <span className="shrink-0 text-[12px] text-subtle">{t("Контекст:")}</span>
          {chipList}
        </div>
        <div className="flex items-end gap-2">
          {mic?.available && (
            <button
              type="button"
              onClick={mic.onClick}
              disabled={disabled || busy || mic.transcribing}
              aria-label={mic.recording ? t("Закончить запись") : t("Задать вопрос голосом")}
              className={`${iconButton} lg:order-2 ${mic.recording ? "bg-bad text-white" : "bg-surface-2 text-ink-2 hover:text-ink"}`}
            >
              {mic.transcribing ? <Loader2 className="size-4 animate-spin" aria-hidden /> : mic.recording ? <Square className="size-3.5 fill-current" aria-hidden /> : <Mic className="size-[18px]" aria-hidden />}
            </button>
          )}
          <textarea
            ref={area}
            value={value}
            onChange={(e) => onChange(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
                e.preventDefault();
                if (!busy && !empty) onSubmit();
              }
            }}
            rows={1}
            maxLength={4000}
            disabled={disabled}
            aria-label={t("Вопрос консультанту")}
            placeholder={
              disabled
                ? t("Консультант не настроен")
                : inChat
                  ? t("Уточните вопрос…")
                  : compact
                    ? t("Спросите о продажах, складе…")
                    : t("Спросите о продажах, складе, финансах или сотрудниках…")
            }
            style={{ lineHeight: `${lineHeight}px` }}
            className="min-h-[38px] flex-1 resize-none bg-transparent py-2 text-[15px] text-ink placeholder:text-ink-3 focus:outline-none disabled:opacity-60 lg:order-1 lg:text-sm max-lg:px-1 max-lg:text-base max-lg:placeholder:text-[15px]"
          />
          {busy ? (
            <button type="button" onClick={onStop} aria-label={t("Остановить ответ")} className={`${iconButton} order-3 bg-surface-2 text-ink hover:bg-bad-soft hover:text-bad`}>
              <Square className="size-3.5 fill-current" aria-hidden />
            </button>
          ) : (
            <button type="submit" disabled={disabled || empty} aria-label={t("Отправить")} className={`${iconButton} order-3 bg-accent-fill text-white`}>
              <ArrowUp className="size-[18px]" aria-hidden />
            </button>
          )}
        </div>
      </div>
      <p className="mt-2 text-center text-[11px] text-subtle max-lg:hidden">
        {t("Enter — отправить · Shift + Enter — новая строка · ответы опираются только на данные, к которым у вас есть доступ")}
      </p>
    </form>
  );
}
