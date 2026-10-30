"use client";

import Link from "next/link";
import { useEffect, useMemo, useRef, useState } from "react";
import { Loader2, MessageSquare, MoreHorizontal, Pencil, Pin, PinOff, Plus, Search, Send, Sparkles, Trash2, X } from "lucide-react";
import { LinkTelegramButton } from "@/components/telegram/LinkTelegramButton";
import { useLocale, useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { historyTime, type ConversationSummary } from "@/lib/consultant";
import { chatSource, chatTitle, filterChats, groupChats, type SourceFilter } from "@/lib/consultant-view";
import { myTelegramApi } from "@/lib/telegram";

const filters: { key: SourceFilter; label: string }[] = [
  { key: "all", label: "Все" },
  { key: "web", label: "Веб" },
  { key: "telegram", label: "Telegram" },
];

/**
 * Список чатов консультанта: «Новый чат», поиск по названию и тексту сообщений (на сервере), фильтр «Все · Веб · Telegram», группы
 * «Закреплённые / Сегодня / Вчера / Ранее», меню строки (переименовать, закрепить, удалить), внизу — карточка Telegram-бота.
 * На компьютере — колонка 300px, на телефоне и при свёрнутой колонке — тот же список в шторке.
 */
export function ChatSidebar({
  list,
  activeId,
  pinned,
  onTogglePin,
  onRename,
  onDelete,
  onNavigate,
  onClose,
}: {
  list: ConversationSummary[];
  activeId: string | null;
  pinned: ReadonlySet<string>;
  onTogglePin: (id: string) => void;
  onRename: (id: string, title: string) => Promise<void>;
  onDelete: (id: string) => void;
  /** Переход в чат или новый чат (закрыть шторку). */
  onNavigate?: () => void;
  /** Шторка: кнопка закрытия. */
  onClose?: () => void;
}) {
  const t = useT();
  const locale = useLocale();
  const [query, setQuery] = useState("");
  const [found, setFound] = useState<ConversationSummary[] | null>(null);
  const [searching, setSearching] = useState(false);
  const [filter, setFilter] = useState<SourceFilter>("all");
  const [menu, setMenu] = useState<string | null>(null);
  const [editing, setEditing] = useState<string | null>(null);

  // Поиск по тексту сообщений — на сервере, через 300 мс после ввода; пустой запрос — весь список.
  useEffect(() => {
    const q = query.trim();
    if (!q) {
      setFound(null);
      setSearching(false);
      return;
    }
    let alive = true;
    setSearching(true);
    const timer = setTimeout(() => {
      bff<ConversationSummary[]>(`ai/conversations?q=${encodeURIComponent(q)}`)
        .then((r) => alive && setFound(r))
        .catch(() => alive && setFound(list.filter((c) => c.title.toLowerCase().includes(q.toLowerCase()))))
        .finally(() => alive && setSearching(false));
    }, 300);
    return () => {
      alive = false;
      clearTimeout(timer);
    };
  }, [query, list]);

  const source = found ?? list;
  const groups = useMemo(() => groupChats(filterChats(source, filter), pinned), [source, filter, pinned]);

  return (
    <div className="flex h-full flex-col gap-3.5 px-3.5 pb-3.5 pt-[18px]">
      <div className="flex items-center gap-2">
        <Link
          href="/consultant"
          onClick={onNavigate}
          className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-[#3d7bff] to-[#5b8cff] p-3 text-sm font-semibold text-white shadow-[0_6px_18px_rgb(61_123_255/0.25)] hover:brightness-110"
        >
          <Plus className="size-4" aria-hidden />
          {t("Новый чат")}
        </Link>
        {onClose && (
          <button type="button" onClick={onClose} aria-label={t("Закрыть список чатов")} className="grid size-11 shrink-0 place-items-center rounded-xl border border-line bg-surface text-ink-2">
            <X className="size-5" aria-hidden />
          </button>
        )}
      </div>

      <label className="flex h-10 items-center gap-2 rounded-[10px] border border-line bg-surface px-3 text-ink-3 focus-within:border-accent">
        {searching ? <Loader2 className="size-4 shrink-0 animate-spin" aria-hidden /> : <Search className="size-4 shrink-0" aria-hidden />}
        <input
          type="search"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder={t("Поиск по чатам")}
          aria-label={t("Поиск по чатам: по названию и тексту сообщений")}
          className="min-w-0 flex-1 bg-transparent text-[13px] text-ink placeholder:text-ink-3 focus:outline-none"
        />
      </label>

      <div role="radiogroup" aria-label={t("Источник чатов")} className="grid grid-cols-3 gap-1 rounded-[10px] border border-line bg-surface p-1">
        {filters.map((f) => (
          <button
            key={f.key}
            type="button"
            role="radio"
            aria-checked={filter === f.key}
            onClick={() => setFilter(f.key)}
            className={`rounded-[7px] py-1.5 text-[12px] font-medium transition-colors ${filter === f.key ? "border border-line-strong bg-surface-2 text-ink" : "border border-transparent text-ink-3 hover:text-ink"}`}
          >
            {t(f.label)}
          </button>
        ))}
      </div>

      <nav aria-label={t("Чаты")} className="-mx-1 min-h-0 flex-1 overflow-y-auto px-1">
        {source.length === 0 && !searching && <p className="px-2 py-6 text-center text-[13px] text-ink-3">{query.trim() ? t("Ничего не найдено") : t("Чатов пока нет")}</p>}
        {source.length > 0 && groups.length === 0 && <p className="px-2 py-6 text-center text-[13px] text-ink-3">{t("В этом источнике чатов нет")}</p>}
        {groups.map((g) => (
          <div key={g.label} className="mb-2">
            <div className="px-2.5 pb-1.5 pt-2 text-[11px] font-semibold uppercase tracking-[0.8px] text-subtle">{t(g.label)}</div>
            <ul className="space-y-0.5">
              {g.items.map((c) => (
                <ChatRow
                  key={c.id}
                  chat={c}
                  active={c.id === activeId}
                  pinned={pinned.has(c.id)}
                  time={historyTime(c.lastMessageAt, undefined, locale)}
                  menuOpen={menu === c.id}
                  editing={editing === c.id}
                  onMenu={(open) => setMenu(open ? c.id : null)}
                  onEdit={(on) => {
                    setMenu(null);
                    setEditing(on ? c.id : null);
                  }}
                  onRename={async (title) => {
                    setEditing(null);
                    if (title && title !== c.title) await onRename(c.id, title);
                  }}
                  onTogglePin={() => {
                    setMenu(null);
                    onTogglePin(c.id);
                  }}
                  onDelete={() => {
                    setMenu(null);
                    onDelete(c.id);
                  }}
                  onNavigate={onNavigate}
                />
              ))}
            </ul>
          </div>
        ))}
      </nav>

      <TelegramCard />
    </div>
  );
}

function ChatRow({
  chat,
  active,
  pinned,
  time,
  menuOpen,
  editing,
  onMenu,
  onEdit,
  onRename,
  onTogglePin,
  onDelete,
  onNavigate,
}: {
  chat: ConversationSummary;
  active: boolean;
  pinned: boolean;
  time: string;
  menuOpen: boolean;
  editing: boolean;
  onMenu: (open: boolean) => void;
  onEdit: (on: boolean) => void;
  onRename: (title: string) => Promise<void>;
  onTogglePin: () => void;
  onDelete: () => void;
  onNavigate?: () => void;
}) {
  const t = useT();
  const menuRef = useRef<HTMLDivElement>(null);
  const telegram = chatSource(chat) === "telegram";
  const Icon = active ? Sparkles : telegram ? Send : MessageSquare;
  const title = chatTitle(chat.title);

  // Меню закрывается щелчком мимо и Esc.
  useEffect(() => {
    if (!menuOpen) return;
    const close = (e: Event) => {
      if (e instanceof KeyboardEvent ? e.key === "Escape" : !menuRef.current?.contains(e.target as Node)) onMenu(false);
    };
    document.addEventListener("pointerdown", close);
    document.addEventListener("keydown", close);
    return () => {
      document.removeEventListener("pointerdown", close);
      document.removeEventListener("keydown", close);
    };
  }, [menuOpen, onMenu]);

  if (editing) {
    return (
      <li className="rounded-[9px] border border-accent bg-surface-2 px-2.5 py-1.5">
        <RenameInput initial={chat.title} onDone={(value) => void onRename(value)} onCancel={() => onEdit(false)} label={t("Название чата")} />
      </li>
    );
  }

  return (
    <li className={`group relative rounded-[9px] border ${active ? "border-line-strong bg-surface-2" : "border-transparent hover:bg-surface-2/60"}`}>
      <Link
        href={`/consultant/${chat.id}`}
        onClick={onNavigate}
        aria-current={active ? "page" : undefined}
        className="flex items-center gap-2.5 px-2.5 py-[9px]"
        title={title}
      >
        <Icon className={`size-[15px] shrink-0 ${active ? "text-accent" : telegram ? "text-telegram" : "text-subtle"}`} strokeWidth={1.75} aria-hidden />
        {telegram && <span className="sr-only">Telegram:</span>}
        <span className={`min-w-0 flex-1 truncate text-[13px] ${active ? "font-semibold text-ink" : "text-ink-2"}`}>{title}</span>
        {pinned && <Pin className="size-3 shrink-0 text-subtle" aria-label={t("Закреплён")} />}
        <span className={`shrink-0 text-[11px] tabular-nums text-subtle ${menuOpen ? "invisible" : "group-hover:invisible group-focus-within:invisible"}`}>{time}</span>
      </Link>
      <div ref={menuRef} className="absolute right-1.5 top-1/2 -translate-y-1/2">
        <button
          type="button"
          aria-label={t("Действия с чатом «{title}»", { title })}
          aria-haspopup="menu"
          aria-expanded={menuOpen}
          onClick={() => onMenu(!menuOpen)}
          className={`size-7 place-items-center rounded-md text-ink-3 hover:bg-surface hover:text-ink ${menuOpen ? "grid" : "hidden group-hover:grid group-focus-within:grid max-lg:grid"}`}
        >
          <MoreHorizontal className="size-4" aria-hidden />
        </button>
        {menuOpen && (
          <div role="menu" className="absolute right-0 top-8 z-30 w-48 rounded-xl border border-line-strong bg-surface-solid p-1 shadow-xl">
            <MenuItem icon={Pencil} label={t("Переименовать")} onClick={() => onEdit(true)} />
            <MenuItem icon={pinned ? PinOff : Pin} label={pinned ? t("Открепить") : t("Закрепить")} onClick={onTogglePin} />
            <MenuItem icon={Trash2} label={t("Удалить")} onClick={onDelete} danger />
          </div>
        )}
      </div>
    </li>
  );
}

export function MenuItem({ icon: Icon, label, onClick, danger }: { icon: typeof Pin; label: string; onClick: () => void; danger?: boolean }) {
  return (
    <button
      type="button"
      role="menuitem"
      onClick={onClick}
      className={`flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-left text-[13px] ${danger ? "text-bad hover:bg-bad-soft" : "text-ink hover:bg-surface-2"}`}
    >
      <Icon className="size-4 shrink-0" aria-hidden />
      {label}
    </button>
  );
}

/** Поле переименования: Enter — сохранить, Esc — отменить, уход фокуса — сохранить. */
export function RenameInput({ initial, onDone, onCancel, label, className }: { initial: string; onDone: (value: string) => void; onCancel: () => void; label: string; className?: string }) {
  const [value, setValue] = useState(initial);
  const done = useRef(false);
  const finish = (save: boolean) => {
    if (done.current) return;
    done.current = true;
    if (save && value.trim()) onDone(value.trim());
    else onCancel();
  };
  return (
    <input
      autoFocus
      value={value}
      maxLength={200}
      aria-label={label}
      onChange={(e) => setValue(e.target.value)}
      onFocus={(e) => e.currentTarget.select()}
      onKeyDown={(e) => {
        if (e.key === "Enter") {
          e.preventDefault();
          finish(true);
        } else if (e.key === "Escape") {
          e.preventDefault();
          finish(false);
        }
      }}
      onBlur={() => finish(true)}
      className={className ?? "w-full bg-transparent text-[13px] text-ink focus:outline-none"}
    />
  );
}

/** Карточка внизу списка: бот привязан — «Чаты из Telegram тоже здесь», нет — «Подключить Telegram» (окно привязки кодом). */
function TelegramCard() {
  const t = useT();
  const [linked, setLinked] = useState<boolean | null>(null);
  useEffect(() => {
    let alive = true;
    myTelegramApi
      .links()
      .then((l) => alive && setLinked(l.length > 0))
      .catch(() => alive && setLinked(false));
    return () => {
      alive = false;
    };
  }, []);

  const body = (title: string, text: string) => (
    <>
      <span className="grid size-9 shrink-0 place-items-center rounded-[10px] bg-telegram/15 text-telegram">
        <Send className="size-4" aria-hidden />
      </span>
      <span className="min-w-0 text-left">
        <span className="block truncate text-[13px] font-semibold text-ink">{title}</span>
        <span className="block truncate text-[11px] text-ink-3">{text}</span>
      </span>
    </>
  );

  if (linked === null) return null;
  return linked ? (
    <div className="flex items-center gap-3 rounded-xl border border-line bg-surface p-3">{body(t("Telegram-бот подключён"), t("Чаты из Telegram тоже здесь"))}</div>
  ) : (
    <LinkTelegramButton
      trigger={(open) => (
        <button type="button" onClick={open} className="flex w-full items-center gap-3 rounded-xl border border-line bg-surface p-3 hover:border-telegram/50">
          {body(t("Подключить Telegram"), t("Задавайте вопросы и в Telegram"))}
        </button>
      )}
    />
  );
}
