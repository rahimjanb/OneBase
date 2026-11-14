"use client";

import { useEffect, useRef, useState } from "react";
import { ChevronDown, ChevronRight, Copy, Ellipsis, Eye, MapPin, MessageCircleOff, MessageCirclePlus, Pencil, PowerOff, Send, Trash2, TriangleAlert, UserRoundX } from "lucide-react";
import { useT } from "@/i18n/client";
import { REPORTS, hasChat, matrix, nameOf, scopeProblem, type Group } from "@/lib/telegram-recipients";
import type { ReportJob, ReportRecipient } from "@/lib/telegramReports";
import { Avatar, Dropdown, LastSendView, ReportSquares, RolePill, Toggle } from "./bits";

/** Сколько строк группы видно сразу; остальные — «Ещё N ТП в Навои». */
const FIRST = 6;
const COLLAPSED_KEY = "onebase:tg-reports:collapsed";

export type RowAction = "edit" | "test" | "send" | "chats" | "duplicate" | "delete";

function readCollapsed(): string[] {
  try {
    const raw = localStorage.getItem(COLLAPSED_KEY);
    return raw ? (JSON.parse(raw) as string[]) : [];
  } catch {
    return [];
  }
}

/** Ширина карточки таблицы: на узкой колонка «Чаты» — только счётчик. */
function useNarrow<T extends HTMLElement>(limit: number) {
  const ref = useRef<T>(null);
  const [narrow, setNarrow] = useState(false);
  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    const observer = new ResizeObserver(([entry]) => setNarrow(entry.contentRect.width < limit));
    observer.observe(el);
    return () => observer.disconnect();
  }, [limit]);
  return [ref, narrow] as const;
}

export function Checkbox({ checked, indeterminate, onChange, label }: { checked: boolean; indeterminate?: boolean; onChange: (v: boolean) => void; label: string }) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (ref.current) ref.current.indeterminate = !!indeterminate;
  }, [indeterminate]);
  return (
    <input
      ref={ref}
      type="checkbox"
      checked={checked}
      aria-label={label}
      onClick={(e) => e.stopPropagation()}
      onChange={(e) => onChange(e.target.checked)}
      className="size-[18px] shrink-0 cursor-pointer rounded-[5px] accent-[var(--color-accent)]"
    />
  );
}

function RowMenu({ r, onAction }: { r: ReportRecipient; onAction: (r: ReportRecipient, action: RowAction) => void }) {
  const t = useT();
  const items: { key: RowAction; label: string; icon: typeof Pencil; danger?: boolean; off?: boolean }[] = [
    { key: "edit", label: "Редактировать", icon: Pencil },
    { key: "test", label: "Тест-отправка", icon: Eye, off: !hasChat(r) },
    { key: "send", label: "Отправить сейчас", icon: Send, off: !hasChat(r) || !r.enabled },
    { key: "chats", label: "Привязать чат", icon: MessageCirclePlus },
    { key: "duplicate", label: "Дублировать настройки", icon: Copy },
    { key: "delete", label: "Удалить", icon: Trash2, danger: true },
  ];
  return (
    <Dropdown
      align="right"
      width={230}
      trigger={({ open, toggle }) => (
        <button
          type="button"
          aria-label={t("Действия: {name}", { name: nameOf(r) })}
          aria-expanded={open}
          onClick={(e) => {
            e.stopPropagation();
            toggle();
          }}
          className="grid size-7 place-items-center rounded-md text-ink-3 hover:bg-muted hover:text-ink"
        >
          <Ellipsis className="size-4" />
        </button>
      )}
    >
      {(close) => (
        <div role="menu" onClick={(e) => e.stopPropagation()}>
          {items.map((item) => (
            <button
              key={item.key}
              type="button"
              role="menuitem"
              disabled={item.off}
              onClick={() => {
                close();
                onAction(r, item.key);
              }}
              className={`flex w-full items-center gap-2.5 rounded-lg px-2.5 py-2 text-left text-[13px] disabled:opacity-40 ${item.danger ? "text-bad hover:bg-bad-soft" : "text-ink hover:bg-muted"}`}
            >
              <item.icon aria-hidden className="size-4 shrink-0" />
              {t(item.label)}
            </button>
          ))}
        </div>
      )}
    </Dropdown>
  );
}

/** Подпись под именем: «Навои · СВ Шахзод К.», «Навои · команда 7 ТП», «Навои, Бухара, Самарканд» (РМ — регионы из профиля или направления). */
export function subtitleOf(r: ReportRecipient, regionNames: string[], t: (s: string, v?: Record<string, string | number>) => string) {
  // РМ-пользователь без регионов: причину («больше не РМ», «нет ТП») показывает строка ниже — подпись её не повторяет.
  if (r.role === "rm" && r.userId) return regionNames.length ? regionNames.join(", ") : scopeProblem(r) ? "—" : t("Нет регионов с ТП в профиле");
  if (r.role === "rm") return !r.directionId || regionNames.length === 0 ? t("Вся страна") : regionNames.join(", ");
  if (r.role === "sv") return `${r.region ?? r.scope} · ${t("команда {n} ТП", { n: r.agents })}`;
  return `${r.region ?? "—"} · ${r.supervisor ? `${t("СВ")} ${shortName(r.supervisor)}` : t("без СВ")}`;
}

/** «Шахзод Кувондиков» → «Шахзод К.». */
export const shortName = (name: string) => {
  const [first, second] = name.trim().split(/\s+/);
  return second ? `${first} ${second[0]}.` : first;
};

export function ChatCell({ r, onBind, narrow }: { r: ReportRecipient; onBind: () => void; narrow?: boolean }) {
  const t = useT();
  if (!hasChat(r)) {
    return (
      <button
        type="button"
        onClick={(e) => {
          e.stopPropagation();
          onBind();
        }}
        title={r.chats.length ? t("В чаты получателя писать нельзя — привяжите другой") : t("Привязать чат")}
        className="inline-flex h-6 items-center gap-1 whitespace-nowrap rounded-md bg-warn-soft px-2 text-[11px] font-bold text-warn hover:brightness-110"
      >
        <MessageCircleOff aria-hidden className="size-3.5" />
        {t("нет чата")}
      </button>
    );
  }

  const first = r.chats.find((c) => c.canSend) ?? r.chats[0];
  const kind = first.type === "Private" ? t("личный") : t("группа");
  return (
    <span className="flex min-w-0 items-center gap-2" title={r.chats.map((c) => c.title).join(", ")}>
      <span className="grid size-[22px] shrink-0 place-items-center rounded-md bg-muted text-[11px] font-bold tabular-nums text-ink-2">{r.chats.length}</span>
      {!narrow && (
        <span className="line-clamp-2 min-w-0 text-[12px] leading-tight text-ink-2">
          {first.title}
          {r.chats.length === 1 && first.type === "Private" && <span className="text-ink-4"> · {kind}</span>}
        </span>
      )}
    </span>
  );
}

/**
 * Таблица получателей: группы-регионы (сворачиваются, состояние — в браузере), в группе первые 6 и «Ещё N»; строка — выбор, получатель,
 * чаты или «нет чата», отчёты 1–9, последняя отправка, переключатель, меню. Клик по строке — панель редактирования.
 */
export function RecipientsTable({
  groups,
  jobs,
  regionNamesOf,
  selected,
  onSelect,
  onOpen,
  onToggle,
  onAction,
  busy,
  editingId,
  bulkBar,
  flat,
  onFlat,
}: {
  groups: Group[];
  jobs: ReportJob[];
  regionNamesOf: (r: ReportRecipient) => string[];
  selected: Set<string>;
  onSelect: (ids: string[], on: boolean) => void;
  onOpen: (r: ReportRecipient) => void;
  onToggle: (r: ReportRecipient, on: boolean) => void;
  onAction: (r: ReportRecipient, action: RowAction) => void;
  busy: Set<string>;
  editingId: string | null;
  bulkBar: React.ReactNode;
  flat: boolean;
  onFlat: (flat: boolean) => void;
}) {
  const t = useT();
  const [ref, narrow] = useNarrow<HTMLDivElement>(820);
  const [collapsed, setCollapsed] = useState<string[]>([]);
  const [expanded, setExpanded] = useState<string[]>([]);
  useEffect(() => setCollapsed(readCollapsed()), []);
  const toggleGroup = (key: string) =>
    setCollapsed((list) => {
      const next = list.includes(key) ? list.filter((x) => x !== key) : [...list, key];
      try {
        localStorage.setItem(COLLAPSED_KEY, JSON.stringify(next));
      } catch {
        // без хранилища браузера — просто не запоминаем
      }
      return next;
    });
  const ids = groups.flatMap((g) => g.rows.map((r) => r.id));
  const all = ids.length > 0 && ids.every((id) => selected.has(id));
  const some = !all && ids.some((id) => selected.has(id));

  return (
    <div ref={ref} className="rounded-[16px] border border-line bg-surface">
      {bulkBar}
      <table className="w-full table-fixed border-collapse text-left">
        <colgroup>
          <col className="w-[46px]" />
          <col />
          <col className={narrow ? "w-[92px]" : "w-[160px]"} />
          <col className="w-[190px]" />
          <col className={narrow ? "w-[118px]" : "w-[150px]"} />
          <col className="w-[52px]" />
          <col className="w-[40px]" />
        </colgroup>
        <thead>
          <tr className="border-b border-line bg-muted/60 text-[10px] font-bold uppercase tracking-wide text-ink-4">
            <th scope="col" className="py-2.5 pl-4">
              <Checkbox checked={all} indeterminate={some} onChange={(v) => onSelect(ids, v)} label={t("Выбрать всех")} />
            </th>
            <th scope="col" className="px-2 py-2.5">
              {t("Получатель")}
            </th>
            <th scope="col" className="px-2 py-2.5">
              {t("Чаты")}
            </th>
            <th scope="col" className="px-2 py-2.5">
              {t("Отчёты 1–9")}
            </th>
            <th scope="col" className="px-2 py-2.5 leading-tight">
              {t("Последняя отправка")}
            </th>
            <th scope="col" className="px-2 py-2.5">
              <span className="sr-only">{t("Активен")}</span>
            </th>
            <th scope="col" className="py-2.5 pr-3">
              <span className="sr-only">{t("Действия")}</span>
            </th>
          </tr>
        </thead>
        {groups.map((g) => {
          const open = flat || !collapsed.includes(g.key);
          const more = expanded.includes(g.key) ? 0 : Math.max(0, g.rows.length - FIRST);
          const rows = more > 0 ? g.rows.slice(0, FIRST) : g.rows;
          const rest = g.rows.slice(FIRST);
          const restLabel =
            rest.every((r) => r.role === "tp") && g.regionId
              ? t("Ещё {n} ТП в {region}", { n: more, region: g.name })
              : t("Ещё {n} — {region}", { n: more, region: t(g.name) });
          return (
            <tbody key={g.key}>
              {!flat && (
                <tr className="border-b border-line bg-ink/[0.02]">
                  <th scope="rowgroup" colSpan={7} className="px-4 py-2 text-left font-normal">
                    <button type="button" aria-expanded={open} onClick={() => toggleGroup(g.key)} className="flex w-full flex-wrap items-center gap-x-2 gap-y-1 text-left">
                      {open ? <ChevronDown aria-hidden className="size-4 text-ink-3" /> : <ChevronRight aria-hidden className="size-4 text-ink-3" />}
                      <MapPin aria-hidden className="size-3.5 text-ink-3" />
                      <span className="text-[12px] font-bold text-ink">{t(g.name)}</span>
                      <span className="text-[11px] text-ink-4">
                        {[g.counts.rm && `${g.counts.rm} ${t("РМ")}`, g.counts.sv && `${g.counts.sv} ${t("СВ")}`, g.counts.tp && `${g.counts.tp} ${t("ТП")}`].filter(Boolean).join(" · ")}
                      </span>
                      {g.noChat > 0 && <Warn icon={TriangleAlert} text={t("{n} без чата", { n: g.noChat })} />}
                      {g.disabled > 0 && <Warn icon={PowerOff} text={t(g.disabled === 1 ? "{n} отключён" : "{n} отключены", { n: g.disabled })} />}
                      {g.noSupervisor > 0 && <Warn icon={UserRoundX} text={t("{n} без СВ", { n: g.noSupervisor })} />}
                    </button>
                  </th>
                </tr>
              )}
              {open &&
                rows.map((r) => {
                  const isSelected = selected.has(r.id);
                  return (
                    <tr
                      key={r.id}
                      onClick={() => onOpen(r)}
                      className={`cursor-pointer border-b border-line transition-colors ${isSelected ? "bg-accent/[0.06]" : editingId === r.id ? "bg-accent/[0.04]" : "hover:bg-muted/50"} ${
                        r.enabled ? "" : "[&>td:not(:nth-child(6)):not(:nth-child(7))]:opacity-55"
                      }`}
                    >
                      <td className="py-2.5 pl-4 align-middle">
                        <Checkbox checked={isSelected} onChange={(v) => onSelect([r.id], v)} label={t("Выбрать {name}", { name: nameOf(r) })} />
                      </td>
                      <td className="px-2 py-2.5">
                        <span className="flex min-w-0 items-center gap-2.5">
                          <Avatar name={nameOf(r)} role={r.role} />
                          <span className="min-w-0">
                            <span className="flex min-w-0 items-center gap-1.5">
                              <span className="truncate text-[13px] font-semibold text-ink">{nameOf(r)}</span>
                              <RolePill role={r.role} />
                            </span>
                            <span className="block truncate text-[11px] text-ink-4">{subtitleOf(r, regionNamesOf(r), t)}</span>
                            {scopeProblem(r) && (
                              <span className="block truncate text-[11px] font-semibold text-warn" title={t(scopeProblem(r) as string)}>
                                {t(scopeProblem(r) as string)}
                              </span>
                            )}
                          </span>
                        </span>
                      </td>
                      <td className="px-2 py-2.5">
                        <ChatCell r={r} narrow={narrow} onBind={() => onAction(r, "chats")} />
                      </td>
                      <td className="px-2 py-2.5">
                        <ReportSquares cells={matrix(r, jobs)} />
                      </td>
                      <td className="px-2 py-2.5">
                        <LastSendView r={r} jobs={jobs} />
                      </td>
                      <td className="px-2 py-2.5">
                        <Toggle checked={r.enabled} disabled={busy.has(r.id)} onChange={(v) => onToggle(r, v)} label={r.enabled ? t("Выключить {name}", { name: nameOf(r) }) : t("Включить {name}", { name: nameOf(r) })} />
                      </td>
                      <td className="py-2.5 pr-3" onClick={(e) => e.stopPropagation()}>
                        <RowMenu r={r} onAction={onAction} />
                      </td>
                    </tr>
                  );
                })}
              {open && more > 0 && (
                <tr className="border-b border-line">
                  <td colSpan={7} className="px-4 py-2">
                    <button type="button" onClick={() => setExpanded((list) => [...list, g.key])} className="ml-8 flex items-center gap-1 text-[12px] font-semibold text-accent-strong hover:underline">
                      <ChevronDown aria-hidden className="size-3.5" />
                      {restLabel}
                    </button>
                  </td>
                </tr>
              )}
            </tbody>
          );
        })}
      </table>
      <div className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-[11px] text-ink-3">
        <label className="flex cursor-pointer items-center gap-2 text-[12px] text-ink-2">
          <Toggle checked={!flat} onChange={(v) => onFlat(!v)} label={t("Группировка по регионам")} tone="accent" size="sm" />
          {t("Группировка по регионам")}
        </label>
        <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
          {t("Отчёты")}:
          {REPORTS.map((x) => (
            <span key={x.n} className="flex items-center gap-1">
              <span className="grid h-4 w-[15px] place-items-center rounded-[4px] bg-muted text-[9px] font-bold text-ink-3">{x.n}</span>
              {t(x.short)}
            </span>
          ))}
        </span>
      </div>
    </div>
  );
}

function Warn({ icon: Icon, text }: { icon: typeof TriangleAlert; text: string }) {
  return (
    <span className="inline-flex h-[18px] items-center gap-1 rounded-[5px] bg-warn-soft px-1.5 text-[10px] font-bold text-warn">
      <Icon aria-hidden className="size-3" />
      {text}
    </span>
  );
}
