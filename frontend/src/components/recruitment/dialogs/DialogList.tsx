"use client";

import { useRef } from "react";
import { ChevronRight, Hand, MessagesSquare, RefreshCw, Search } from "lucide-react";
import { useT } from "@/i18n/client";
import type { DialogRow } from "@/lib/hr-dialogs";
import { plural } from "@/lib/format";
import { authorPrefix, listTime, type DialogFilter } from "@/lib/hr-dialogs-view";
import { Avatar, ListSkeleton, ModeChip } from "./parts";

const filters: { key: DialogFilter; label: string; dot?: string }[] = [
  { key: "all", label: "Все" },
  { key: "ai", label: "AI", dot: "bg-violet" },
  { key: "need_hr", label: "Нужен HR", dot: "bg-warn" },
  { key: "new", label: "Новые" },
];

/** Поиск по имени и @username и кнопка «Обновить». */
export function SearchBox({ value, onChange, onRefresh, refreshing, mobile = false }: {
  value: string;
  onChange: (v: string) => void;
  onRefresh?: () => void;
  refreshing?: boolean;
  mobile?: boolean;
}) {
  const t = useT();
  return (
    <div className="flex gap-2">
      <label className={`flex min-w-0 flex-1 items-center gap-2 border border-line bg-page px-3 ${mobile ? "h-[44px] rounded-xl" : "h-[38px] rounded-[10px]"}`}>
        <Search className="size-4 shrink-0 text-ink-4" aria-hidden />
        <input
          type="search"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={t("Поиск: имя или @username")}
          aria-label={t("Поиск: имя или @username")}
          className={`min-w-0 flex-1 bg-transparent text-ink outline-none placeholder:text-ink-4 ${mobile ? "text-sm" : "text-[13px]"}`}
        />
      </label>
      {onRefresh && (
        <button
          type="button"
          onClick={onRefresh}
          aria-label={t("Обновить")}
          className="grid size-[38px] shrink-0 place-items-center rounded-[10px] border border-line bg-surface text-ink-2 hover:text-ink"
        >
          <RefreshCw className={`size-4 ${refreshing ? "animate-spin" : ""}`} />
        </button>
      )}
    </div>
  );
}

/** Чипы «Все / AI / Нужен HR / Новые» со счётчиками; без диалогов — приглушены. */
export function FilterChips({ value, onChange, counts, disabled = false, mobile = false }: {
  value: DialogFilter;
  onChange: (f: DialogFilter) => void;
  counts: Record<DialogFilter, number>;
  disabled?: boolean;
  mobile?: boolean;
}) {
  const t = useT();
  return (
    <div role="radiogroup" aria-label={t("Фильтр диалогов")} className={`flex gap-1.5 ${mobile ? "-mx-4 overflow-x-auto px-4 [scrollbar-width:none]" : "flex-wrap"} ${disabled ? "opacity-45" : ""}`}>
      {filters.map((f) => {
        const active = value === f.key;
        return (
          <button
            key={f.key}
            type="button"
            role="radio"
            aria-checked={active}
            disabled={disabled}
            onClick={() => onChange(f.key)}
            className={`flex shrink-0 items-center gap-1.5 font-semibold ${mobile ? "rounded-[10px] px-[11px] py-[7px] text-[13px]" : "rounded-lg px-[9px] py-[5px] text-xs"} ${
              active ? "bg-accent text-white" : "border border-line bg-surface text-ink-2 hover:text-ink"
            }`}
          >
            {f.dot && !active && <span aria-hidden className={`size-1.5 rounded-full ${f.dot}`} />}
            {t(f.label)}
            <span className={`tabular-nums ${mobile ? "text-[13px]" : "text-[11px]"} ${active ? "text-white/70" : "text-ink-4"}`}>{counts[f.key]}</span>
          </button>
        );
      })}
    </div>
  );
}

/** Подсказка на телефоне: «3 кандидата ждут ответа HR» (склонение по числу) — включает фильтр «Нужен HR». */
export function NeedHrHint({ count, onClick }: { count: number; onClick: () => void }) {
  const t = useT();
  if (count === 0) return null;
  return (
    <button type="button" onClick={onClick} className="flex w-full items-center gap-3 rounded-xl border border-warn/20 bg-warn-soft px-3.5 py-3 text-left text-xs font-medium text-warn">
      <Hand className="size-5 shrink-0" aria-hidden />
      <span className="flex-1">
        {t(
          plural(count, [
            "{n} кандидат ждёт ответа HR — AI не смог ответить сам",
            "{n} кандидата ждут ответа HR — AI не смог ответить сам",
            "{n} кандидатов ждут ответа HR — AI не смог ответить сам",
          ]),
          { n: count },
        )}
      </span>
      <ChevronRight className="size-4 shrink-0" aria-hidden />
    </button>
  );
}

/**
 * Список диалогов (role=listbox): стрелки ↑/↓ двигают фокус, Enter открывает. Строка: аватар, имя и время, последнее сообщение с префиксом
 * «AI:» / «Вы:» и непрочитанными, режим и вакансия. На компьютере — карточки с выделением выбранной, на телефоне — строки с разделителями.
 */
export function DialogList({ rows, selectedId, onOpen, loading, mobile = false, emptyText, onReset }: {
  rows: DialogRow[] | null;
  selectedId: string | null;
  onOpen: (id: string) => void;
  loading: boolean;
  mobile?: boolean;
  /** Почему пусто: нет диалогов вообще или ничего не найдено (тогда есть «Сбросить фильтры»). */
  emptyText: "none" | "nothing";
  onReset: () => void;
}) {
  const t = useT();
  const list = useRef<HTMLUListElement>(null);
  if (!rows && loading) return <ListSkeleton />;
  if (!rows || rows.length === 0) {
    return emptyText === "none" ? (
      <div className="flex flex-1 flex-col items-center justify-center gap-2 p-6 text-center">
        <span className="grid size-12 place-items-center rounded-xl bg-muted text-ink-3">
          <MessagesSquare className="size-6" aria-hidden />
        </span>
        <p className="text-sm font-semibold text-ink">{t("Диалогов пока нет")}</p>
        <p className="max-w-[240px] text-xs text-ink-3">{t("Кандидаты появятся здесь, когда напишут на номер HR в Telegram")}</p>
      </div>
    ) : (
      <div className="flex flex-1 flex-col items-center justify-center gap-2 p-6 text-center">
        <p className="text-sm font-semibold text-ink">{t("Ничего не найдено")}</p>
        <button type="button" onClick={onReset} className="text-xs font-semibold text-accent-strong hover:underline">
          {t("Сбросить фильтры")}
        </button>
      </div>
    );
  }

  const onKey = (e: React.KeyboardEvent<HTMLUListElement>) => {
    const items = [...(list.current?.querySelectorAll<HTMLElement>("[role=option]") ?? [])];
    const i = items.indexOf(document.activeElement as HTMLElement);
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      items[Math.min(items.length - 1, Math.max(0, i + (e.key === "ArrowDown" ? 1 : -1)))]?.focus();
    } else if (e.key === "Enter" && i >= 0) {
      e.preventDefault();
      onOpen(items[i].dataset.id!);
    }
  };

  return (
    <ul ref={list} role="listbox" aria-label={t("Диалоги")} onKeyDown={onKey} className={mobile ? "divide-y divide-line" : "space-y-0.5 p-2"}>
      {rows.map((r, index) => {
        const selected = r.id === selectedId;
        const unread = r.unread > 0;
        return (
          <li
            key={r.id}
            role="option"
            aria-selected={selected}
            data-id={r.id}
            tabIndex={selected || (!selectedId && index === 0) ? 0 : -1}
            onClick={() => onOpen(r.id)}
            className={`flex cursor-pointer gap-3 outline-none focus-visible:ring-2 focus-visible:ring-accent/40 ${
              mobile ? "px-3.5 py-3" : `rounded-xl border p-2.5 ${selected ? "border-line-strong bg-muted" : "border-transparent hover:bg-muted"}`
            }`}
          >
            <Avatar id={r.candidateId ?? r.id} name={r.name} size={mobile ? 46 : 42} />
            <div className="min-w-0 flex-1">
              <div className="flex items-baseline justify-between gap-2">
                <span className={`truncate font-semibold text-ink ${mobile ? "text-sm" : "text-[13px]"}`}>{r.name}</span>
                <span className={`shrink-0 text-[11px] font-medium tabular-nums ${unread ? "text-accent-strong" : "text-ink-4"}`}>{t(listTime(r.lastAt))}</span>
              </div>
              <div className="mt-0.5 flex h-[18px] items-center justify-between gap-2">
                <span className={`truncate ${mobile ? "text-[13px]" : "text-xs"} ${unread ? "font-medium text-ink-2" : "text-ink-3"}`}>
                  {r.lastText ? `${t(authorPrefix(r.lastAuthor))}${r.lastText}` : "—"}
                </span>
                {unread && (
                  <span className="grid h-[18px] min-w-[18px] shrink-0 place-items-center rounded-full bg-accent px-1.5 text-[10px] font-bold text-white tabular-nums">
                    {r.unread}
                  </span>
                )}
              </div>
              <div className="mt-1 flex min-w-0 items-center gap-1.5">
                <ModeChip mode={r.mode} />
                {r.vacancyTitle && <span className="truncate text-[11px] text-ink-4">{r.vacancyTitle}</span>}
              </div>
            </div>
          </li>
        );
      })}
    </ul>
  );
}
