"use client";

import { useEffect, useRef, useState } from "react";
import { ChevronDown, Clock3, Paperclip, Plus, Square, X, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import { fileSize, workPriorityLabel, type WorkTaskPriority } from "@/lib/tasks";
import { priorityLook } from "./TaskKit";

/**
 * Детали формы «Новая задача» (TaskForm): поля, сегменты приоритета, чипы срока, дата и время, чек-лист, файлы, переключатель.
 * Цвета — токены экрана задач (.tasks-ui): поле — фон page и рамка line-strong, фокус — рамка accent и кольцо, ошибка — рамка bad.
 */

/** Рамка поля: фон, обводка, скругление 12, фокус и ошибка. */
export const inputBox = (invalid?: boolean) =>
  `rounded-xl border bg-page outline-none transition-colors duration-150 focus:border-accent focus:ring-[3px] focus:ring-accent/20 ${
    invalid ? "border-bad" : "border-line-strong hover:border-ink-3/50"
  }`;

/** Подпись поля 13/600; обязательное — красная звёздочка; справа — например, счётчик символов. */
export function FieldLabel({ htmlFor, id, required, aside, children }: { htmlFor?: string; id?: string; required?: boolean; aside?: React.ReactNode; children: React.ReactNode }) {
  return (
    <div className="mb-2 flex items-baseline justify-between gap-3">
      <label htmlFor={htmlFor} id={id} className="text-[13px] font-semibold text-ink-2">
        {children}
        {required && (
          <span className="ml-0.5 text-bad" aria-hidden>
            *
          </span>
        )}
      </label>
      {aside}
    </div>
  );
}

export function FieldError({ id, text }: { id?: string; text?: string }) {
  if (!text) return null;
  return (
    <p id={id} className="mt-1.5 text-xs text-bad" role="alert">
      {text}
    </p>
  );
}

const priorities: WorkTaskPriority[] = ["Low", "Medium", "High", "Urgent"];
const priorityTone: Record<WorkTaskPriority, string> = { Low: "text-subtle", Medium: "text-ink", High: "text-warn", Urgent: "text-bad" };

/** Приоритет — сегменты на всю ширину (radiogroup), стрелки ←/→ переключают. stacked — значок над подписью (телефон). */
export function PrioritySegments({ value, onChange, labelId, stacked = false }: { value: WorkTaskPriority; onChange: (p: WorkTaskPriority) => void; labelId: string; stacked?: boolean }) {
  const t = useT();
  const refs = useRef<(HTMLButtonElement | null)[]>([]);
  const move = (step: number) => {
    const next = priorities[(priorities.indexOf(value) + step + priorities.length) % priorities.length];
    onChange(next);
    refs.current[priorities.indexOf(next)]?.focus();
  };
  return (
    <div
      role="radiogroup"
      aria-labelledby={labelId}
      className="grid grid-cols-4 gap-1 rounded-xl border border-line-strong bg-page p-1"
      onKeyDown={(e) => {
        if (e.key === "ArrowRight" || e.key === "ArrowDown") {
          e.preventDefault();
          move(1);
        } else if (e.key === "ArrowLeft" || e.key === "ArrowUp") {
          e.preventDefault();
          move(-1);
        }
      }}
    >
      {priorities.map((p, i) => {
        const Icon = priorityLook[p].icon;
        const active = value === p;
        return (
          <button
            key={p}
            ref={(el) => {
              refs.current[i] = el;
            }}
            type="button"
            role="radio"
            aria-checked={active}
            tabIndex={active ? 0 : -1}
            onClick={() => onChange(p)}
            className={`flex items-center justify-center rounded-[9px] border text-sm transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
              stacked ? "h-14 flex-col gap-1" : "h-10 gap-2"
            } ${active ? "border-line-strong bg-surface-2 font-semibold text-ink" : "border-transparent font-medium text-ink-2 hover:text-ink"}`}
          >
            <Icon className={`size-4 shrink-0 ${priorityTone[p]}`} aria-hidden />
            {t(workPriorityLabel[p])}
          </button>
        );
      })}
    </div>
  );
}

export type ChipOption = { key: string; label: string };

/** Чипы быстрого выбора (срок): активный — accent-soft и рамка accent. scroll — в одну строку с прокруткой (телефон). */
export function Chips({ label, options, active, onPick, scroll = false }: { label?: string; options: ChipOption[]; active: string | null; onPick: (key: string) => void; scroll?: boolean }) {
  return (
    <div className={scroll ? "-mx-4 flex gap-2 overflow-x-auto px-4 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden" : "flex flex-wrap items-center gap-2"}>
      {label && <span className="text-[13px] text-subtle">{label}</span>}
      {options.map((o) => (
        <button
          key={o.key}
          type="button"
          aria-pressed={active === o.key}
          onClick={() => onPick(o.key)}
          className={`shrink-0 rounded-full border px-3 font-medium transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${scroll ? "h-11 px-4 text-[15px]" : "h-8 text-[13px]"} ${
            active === o.key ? "border-accent/35 bg-accent-soft text-link" : "border-line bg-surface-2 text-ink-2 hover:text-ink"
          }`}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}

/** Открыть системный выбор даты/времени по нажатию на поле (Chrome открывает его только по значку); где не умеет — обычное поведение. */
const openPicker = (e: React.MouseEvent<HTMLInputElement>) => {
  try {
    e.currentTarget.showPicker?.();
  } catch {
    // нет showPicker или запрещён — браузер откроет выбор сам
  }
};

/**
 * Дата — поле-кнопка со значком и подписью («Сегодня, 2 ноября»), пусто — плейсхолдер. Поверх лежит прозрачный нативный input:
 * нажатие открывает системный календарь (на телефоне — колесо), клавиатура и экранный диктор работают с ним напрямую.
 */
export function DateField({
  value,
  text,
  placeholder,
  ariaLabel,
  icon: Icon,
  onChange,
  onClear,
  min,
  invalid,
  describedBy,
  large = false,
  chevron = true,
}: {
  value: string;
  text: string;
  placeholder: string;
  ariaLabel: string;
  icon: LucideIcon;
  onChange: (value: string) => void;
  onClear?: () => void;
  min?: string;
  invalid?: boolean;
  describedBy?: string;
  large?: boolean;
  /** Стрелка справа (у поля срока её нет: рядом поле времени, подписи нужно место). */
  chevron?: boolean;
}) {
  const t = useT();
  return (
    <div
      className={`relative flex items-center gap-2.5 rounded-xl border bg-page px-3.5 transition-colors duration-150 has-[input:focus-visible]:border-accent has-[input:focus-visible]:ring-[3px] has-[input:focus-visible]:ring-accent/20 ${
        large ? "h-14" : "h-[52px]"
      } ${invalid ? "border-bad" : "border-line-strong hover:border-ink-3/50"}`}
    >
      <Icon className="size-4 shrink-0 text-ink-3" aria-hidden />
      <span className={`min-w-0 flex-1 truncate font-medium ${large ? "text-base" : "text-sm"} ${value ? "text-ink" : "text-subtle"}`}>{value ? text : placeholder}</span>
      {value && onClear ? (
        <button type="button" onClick={onClear} aria-label={t("Очистить: {field}", { field: ariaLabel })} className="relative z-10 grid size-7 shrink-0 place-items-center rounded-md text-ink-3 hover:bg-surface-2 hover:text-ink">
          <X className="size-3.5" />
        </button>
      ) : (
        chevron && <ChevronDown className="size-4 shrink-0 text-ink-3" aria-hidden />
      )}
      <input
        type="date"
        aria-label={ariaLabel}
        aria-invalid={invalid || undefined}
        aria-describedby={describedBy}
        value={value}
        min={min}
        onChange={(e) => onChange(e.target.value)}
        onClick={openPicker}
        className="absolute inset-0 size-full cursor-pointer appearance-none opacity-0"
      />
    </div>
  );
}

/** Время срока — то же поле-кнопка с часами; без даты недоступно. */
export function TimeField({ value, onChange, disabled, large = false }: { value: string; onChange: (value: string) => void; disabled: boolean; large?: boolean }) {
  const t = useT();
  return (
    <div
      className={`relative flex items-center gap-2 rounded-xl border border-line-strong bg-page px-3 transition-colors duration-150 has-[input:focus-visible]:border-accent has-[input:focus-visible]:ring-[3px] has-[input:focus-visible]:ring-accent/20 ${
        large ? "h-14" : "h-[52px]"
      } ${disabled ? "opacity-50" : "hover:border-ink-3/50"}`}
    >
      <Clock3 className="size-4 shrink-0 text-ink-3" aria-hidden />
      <span className={`flex-1 font-medium tabular-nums ${large ? "text-base" : "text-sm"} ${value ? "text-ink" : "text-subtle"}`}>{value || t("Время")}</span>
      <input
        type="time"
        aria-label={t("Время срока")}
        value={value}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        onClick={openPicker}
        className="absolute inset-0 size-full cursor-pointer appearance-none opacity-0 disabled:cursor-not-allowed"
      />
    </div>
  );
}

/**
 * Чек-лист новой задачи: пункты списком (удаление — крестиком), новый — в строке ввода по Enter. Незаконченный ввод учитывается при
 * отправке формы (text хранит родитель). Пункт — до 300 символов, как на сервере.
 */
export function ChecklistEditor({ items, onChange, text, onText, large = false }: { items: string[]; onChange: (items: string[]) => void; text: string; onText: (text: string) => void; large?: boolean }) {
  const t = useT();
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => input.current?.focus(), []);
  const add = () => {
    const value = text.trim();
    if (!value) return;
    onChange([...items, value.slice(0, 300)]);
    onText("");
  };
  return (
    <div className="rounded-xl border border-line-strong bg-page p-1.5">
      {items.length > 0 && (
        <ul className="mb-1">
          {items.map((item, i) => (
            <li key={`${i}-${item}`} className="flex items-start gap-2.5 rounded-lg px-2 py-1.5">
              <Square className="mt-0.5 size-4 shrink-0 text-subtle" aria-hidden />
              <span className={`min-w-0 flex-1 break-words text-ink ${large ? "text-[15px]" : "text-sm"}`}>{item}</span>
              <button
                type="button"
                onClick={() => onChange(items.filter((_, j) => j !== i))}
                aria-label={t("Удалить пункт «{text}»", { text: item })}
                className={`grid shrink-0 place-items-center rounded-md text-ink-3 hover:bg-surface-2 hover:text-ink ${large ? "size-9" : "size-6"}`}
              >
                <X className="size-3.5" />
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className="flex items-center gap-2.5 px-2">
        <Plus className="size-4 shrink-0 text-subtle" aria-hidden />
        <input
          ref={input}
          value={text}
          maxLength={300}
          onChange={(e) => onText(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.metaKey && !e.ctrlKey) {
              e.preventDefault();
              add();
            }
          }}
          onBlur={add}
          aria-label={t("Пункт чек-листа")}
          placeholder={t("Новый пункт — Enter, чтобы добавить")}
          className={`min-w-0 flex-1 bg-transparent text-ink outline-none placeholder:text-subtle ${large ? "h-11 text-base" : "h-9 text-sm"}`}
        />
      </div>
    </div>
  );
}

/** Файлы к новой задаче: имя, размер, удаление. Загрузятся после создания задачи. */
export function FileList({ files, onRemove, large = false }: { files: File[]; onRemove: (index: number) => void; large?: boolean }) {
  const t = useT();
  if (files.length === 0) return null;
  return (
    <ul className="divide-y divide-line rounded-xl border border-line-strong bg-page">
      {files.map((file, i) => (
        <li key={`${i}-${file.name}`} className="flex items-center gap-2.5 px-3 py-2">
          <Paperclip className="size-4 shrink-0 text-ink-3" aria-hidden />
          <span className={`min-w-0 flex-1 truncate text-ink ${large ? "text-[15px]" : "text-sm"}`}>{file.name}</span>
          <span className="shrink-0 text-xs tabular-nums text-subtle">{fileSize(file.size, t)}</span>
          <button
            type="button"
            onClick={() => onRemove(i)}
            aria-label={t("Убрать файл «{name}»", { name: file.name })}
            className={`grid shrink-0 place-items-center rounded-md text-ink-3 hover:bg-surface-2 hover:text-ink ${large ? "size-9" : "size-6"}`}
          >
            <X className="size-3.5" />
          </button>
        </li>
      ))}
    </ul>
  );
}

/** Переключатель (role="switch") с подписью. */
export function Toggle({ checked, onChange, label }: { checked: boolean; onChange: (checked: boolean) => void; label: string }) {
  return (
    <button type="button" role="switch" aria-checked={checked} onClick={() => onChange(!checked)} className="group inline-flex items-center gap-2.5 rounded-md text-[13px] text-ink-2 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent">
      <span className={`relative h-5 w-9 shrink-0 rounded-full transition-colors duration-150 ${checked ? "bg-accent" : "bg-line-strong"}`}>
        <span className={`absolute left-0 top-0.5 size-4 rounded-full bg-white shadow transition-transform duration-150 ${checked ? "translate-x-[18px]" : "translate-x-0.5"}`} />
      </span>
      {label}
    </button>
  );
}

/** Совпадает ли медиазапрос (компьютер ≥ 768 px). До монтирования — false: форма открывается только в браузере. */
export function useMediaQuery(query: string) {
  const [matches, setMatches] = useState(() => typeof window !== "undefined" && window.matchMedia(query).matches);
  useEffect(() => {
    const mq = window.matchMedia(query);
    const on = () => setMatches(mq.matches);
    on();
    mq.addEventListener("change", on);
    return () => mq.removeEventListener("change", on);
  }, [query]);
  return matches;
}

/** Высота экранной клавиатуры (visualViewport): шторка поднимает над ней кнопку отправки. */
export function useKeyboardInset(active: boolean) {
  const [inset, setInset] = useState(0);
  useEffect(() => {
    const vv = window.visualViewport;
    if (!active || !vv) return;
    const on = () => setInset(Math.max(0, Math.round(window.innerHeight - vv.height - vv.offsetTop)));
    on();
    vv.addEventListener("resize", on);
    vv.addEventListener("scroll", on);
    return () => {
      vv.removeEventListener("resize", on);
      vv.removeEventListener("scroll", on);
      setInset(0);
    };
  }, [active]);
  return inset;
}

/** Tab и Shift+Tab не выходят за пределы окна (фокус-ловушка). */
export function trapTab(e: React.KeyboardEvent, root: HTMLElement | null) {
  if (e.key !== "Tab" || !root) return;
  const nodes = [...root.querySelectorAll<HTMLElement>('a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])')].filter(
    (n) => n.getClientRects().length > 0,
  );
  if (nodes.length === 0) return;
  const first = nodes[0];
  const last = nodes[nodes.length - 1];
  if (e.shiftKey && document.activeElement === first) {
    e.preventDefault();
    last.focus();
  } else if (!e.shiftKey && document.activeElement === last) {
    e.preventDefault();
    first.focus();
  }
}
