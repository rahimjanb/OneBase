"use client";

import { useEffect, useMemo, useState } from "react";
import { Check, FilePlus, Loader2, MessageCirclePlus, PowerOff, Search, Timer, UserRound, UsersRound, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import { REPORTS, defaultCodes, timesOf, type MatrixCell } from "@/lib/telegram-recipients";
import type { ReportChat, ReportJob, ReportRecipientInput } from "@/lib/telegramReports";
import { Dropdown } from "./bits";
import { Checkbox } from "./RecipientsTable";

export type BulkKind = "report" | "times" | "chats" | "disable";

const ACTIONS: { key: BulkKind; label: string; icon: typeof FilePlus }[] = [
  { key: "report", label: "Включить отчёт", icon: FilePlus },
  { key: "times", label: "Время отправки", icon: Timer },
  { key: "chats", label: "Привязать чат", icon: MessageCirclePlus },
  { key: "disable", label: "Отключить", icon: PowerOff },
];

const btn = "flex h-[30px] shrink-0 items-center gap-1.5 whitespace-nowrap rounded-lg border border-line bg-surface px-2.5 text-[12px] font-semibold text-ink hover:bg-muted";

/** Что делают массовые действия — вызывает вкладка (одно сохранение на всех, «Отменить» в уведомлении). */
export type BulkHandlers = {
  report: (codes: string[], title: string) => void;
  times: (times: Record<string, boolean>) => void;
  chats: (chatIds: string[]) => void;
  disable: () => void;
};

/** Полоса над таблицей при выборе: «Выбрано N», действия, «Снять выбор». */
export function BulkBar({
  count,
  total,
  inputs,
  jobs,
  chats,
  busy,
  onAll,
  onClear,
  handlers,
}: {
  count: number;
  total: number;
  inputs: ReportRecipientInput[];
  jobs: ReportJob[];
  chats: ReportChat[];
  busy: boolean;
  onAll: (on: boolean) => void;
  onClear: () => void;
  handlers: BulkHandlers;
}) {
  const t = useT();
  return (
    <div className="flex flex-wrap items-center gap-2 rounded-t-[15px] border-b border-accent/25 bg-accent-soft px-4 py-2.5">
      <Checkbox checked={count === total} indeterminate={count < total} onChange={onAll} label={t("Выбрать всех")} />
      <span className="mr-1 text-[13px] font-bold text-accent-strong">{t("Выбрано {n}", { n: count })}</span>
      <BulkMenus inputs={inputs} jobs={jobs} chats={chats} busy={busy} handlers={handlers} />
      <button type="button" onClick={onClear} className="ml-auto text-[12px] font-semibold text-ink-3 hover:text-ink">
        {t("Снять выбор")}
      </button>
    </div>
  );
}

/** Кнопки действий с выпадающими панелями (на телефоне — в нижней полосе, панели — шторкой). */
export function BulkMenus({
  inputs,
  jobs,
  chats,
  busy,
  handlers,
  compact,
}: {
  inputs: ReportRecipientInput[];
  jobs: ReportJob[];
  chats: ReportChat[];
  busy: boolean;
  handlers: BulkHandlers;
  compact?: boolean;
}) {
  const t = useT();
  const [sheet, setSheet] = useState<BulkKind | null>(null);
  const label = (a: (typeof ACTIONS)[number]) => (
    <>
      {busy && a.key !== "disable" ? <Loader2 aria-hidden className="size-3.5 animate-spin" /> : <a.icon aria-hidden className="size-3.5 shrink-0" />}
      <span className={compact ? "text-[10px]" : ""}>{t(a.label)}</span>
    </>
  );
  const body = (kind: BulkKind, close: () => void) =>
    kind === "report" ? (
      <ReportPicker inputs={inputs} jobs={jobs} onApply={(codes, title) => (handlers.report(codes, title), close())} />
    ) : kind === "times" ? (
      <TimesPicker inputs={inputs} jobs={jobs} onApply={(times) => (handlers.times(times), close())} />
    ) : (
      <ChatPicker chats={chats} onApply={(ids) => (handlers.chats(ids), close())} />
    );

  if (compact) {
    return (
      <>
        {ACTIONS.map((a) => (
          <button
            key={a.key}
            type="button"
            disabled={busy}
            onClick={() => (a.key === "disable" ? handlers.disable() : setSheet(a.key))}
            className="flex min-w-0 flex-1 flex-col items-center gap-1 rounded-lg px-1 py-1.5 text-ink disabled:opacity-40"
          >
            {label(a)}
          </button>
        ))}
        <Sheet open={!!sheet} onClose={() => setSheet(null)} title={sheet ? t(ACTIONS.find((a) => a.key === sheet)!.label) : ""} subtitle={t("Выбрано {n}", { n: inputs.length })}>
          {sheet && body(sheet, () => setSheet(null))}
        </Sheet>
      </>
    );
  }

  return (
    <>
      {ACTIONS.map((a) =>
        a.key === "disable" ? (
          <button key={a.key} type="button" disabled={busy} onClick={handlers.disable} className={btn}>
            {label(a)}
          </button>
        ) : (
          <Dropdown
            key={a.key}
            width={a.key === "chats" ? 320 : 300}
            trigger={({ open, toggle }) => (
              <button type="button" aria-expanded={open} disabled={busy} onClick={toggle} className={btn}>
                {label(a)}
              </button>
            )}
          >
            {(close) => <div className="p-1">{body(a.key, close)}</div>}
          </Dropdown>
        ),
      )}
    </>
  );
}

/** «Включить отчёт»: отчёт 1–9 и его запуски (по умолчанию — как у роли); каждому — только положенное его роли. */
function ReportPicker({ inputs, jobs, onApply }: { inputs: ReportRecipientInput[]; jobs: ReportJob[]; onApply: (codes: string[], title: string) => void }) {
  const t = useT();
  const roles = [...new Set(inputs.map((i) => i.role))];
  const cells: MatrixCell[] = REPORTS.map((x) => {
    const own = jobs.filter((j) => j.report === x.type && roles.some((r) => j.roles.includes(r)));
    return { ...x, on: false, slots: own.map((j) => ({ code: j.code, time: j.time, title: j.slotTitle, on: false })) };
  }).filter((c) => c.slots.length > 0);
  const [n, setN] = useState<number | null>(null);
  const [codes, setCodes] = useState<string[]>([]);
  const cell = cells.find((c) => c.n === n) ?? null;
  const pick = (c: MatrixCell) => {
    setN(c.n);
    const defaults = c.slots.map((s) => s.code).filter((code) => roles.some((r) => defaultCodes(jobs, r).includes(code)));
    setCodes(defaults.length ? defaults : c.slots.map((s) => s.code));
  };
  const covered = (code: string) => inputs.filter((i) => jobs.find((j) => j.code === code)?.roles.includes(i.role)).length;
  return (
    <div>
      <p className="px-1.5 pb-2 text-[11px] text-ink-3">{t("Отчёт и время — каждому добавится то, что положено его роли")}</p>
      <div className="space-y-0.5">
        {cells.map((c) => (
          <button
            key={c.n}
            type="button"
            onClick={() => pick(c)}
            className={`flex w-full items-center gap-2 rounded-lg px-2 py-1.5 text-left text-[13px] ${n === c.n ? "bg-accent-soft text-accent-strong" : "text-ink hover:bg-muted"}`}
          >
            <span className={`grid size-5 shrink-0 place-items-center rounded-[5px] text-[10px] font-bold ${n === c.n ? "bg-accent text-white" : "bg-muted text-ink-3"}`}>{c.n}</span>
            {t(c.title)}
          </button>
        ))}
      </div>
      {cell && (
        <div className="mt-2 border-t border-line px-1.5 pt-2">
          <div className="flex flex-wrap gap-1">
            {cell.slots.map((s) => {
              const on = codes.includes(s.code);
              const reach = covered(s.code);
              return (
                <button
                  key={s.code}
                  type="button"
                  aria-pressed={on}
                  title={`${t(s.title)}${reach < inputs.length ? ` · ${t("положено {n} из {total}", { n: reach, total: inputs.length })}` : ""}`}
                  onClick={() => setCodes((l) => (l.includes(s.code) ? l.filter((x) => x !== s.code) : [...l, s.code]))}
                  className={`flex h-6 items-center gap-1 rounded-md border px-1.5 text-[11px] font-semibold tabular-nums ${on ? "border-accent/40 bg-accent-soft text-accent-strong" : "border-line-strong text-ink-4"}`}
                >
                  {on && <Check aria-hidden className="size-3" />}
                  {s.time}
                </button>
              );
            })}
          </div>
          <button
            type="button"
            disabled={codes.length === 0}
            onClick={() => onApply(codes, `${t(cell.title)}`)}
            className="mt-2.5 h-9 w-full rounded-lg bg-accent text-[13px] font-semibold text-white disabled:opacity-40"
          >
            {t("Включить «{report}»", { report: t(cell.title) })}
          </button>
        </div>
      )}
    </div>
  );
}

/** «Время отправки»: в какое время выбранные получают свои отчёты. Само расписание не меняется. */
function TimesPicker({ inputs, jobs, onApply }: { inputs: ReportRecipientInput[]; jobs: ReportJob[]; onApply: (times: Record<string, boolean>) => void }) {
  const t = useT();
  const rows = useMemo(() => timesOf(inputs, jobs), [inputs, jobs]);
  const [changes, setChanges] = useState<Record<string, boolean>>({});
  const state = (time: string, base: "on" | "off" | "some") => (time in changes ? (changes[time] ? "on" : "off") : base);
  return (
    <div>
      <p className="px-1.5 pb-2 text-[11px] text-ink-3">
        {t("Время запусков общее — меняется на вкладке «Расписание». Здесь — в какое время эти получатели получают свои отчёты.")}
      </p>
      {rows.map((row) => {
        const s = state(row.time, row.state);
        return (
          <label key={row.time} className="flex cursor-pointer items-center gap-2.5 rounded-lg px-2 py-1.5 text-[13px] hover:bg-muted">
            <Checkbox checked={s === "on"} indeterminate={s === "some"} onChange={(v) => setChanges((c) => ({ ...c, [row.time]: v }))} label={row.time} />
            <span className="w-12 font-semibold tabular-nums text-ink">{row.time}</span>
            <span className="text-[11px] text-ink-4">
              {s === "some" ? t("у части") : s === "on" ? t("у всех") : t("ни у кого")}
            </span>
          </label>
        );
      })}
      <button
        type="button"
        disabled={Object.keys(changes).length === 0}
        onClick={() => onApply(changes)}
        className="mt-2 h-9 w-full rounded-lg bg-accent text-[13px] font-semibold text-white disabled:opacity-40"
      >
        {t("Применить")}
      </button>
    </div>
  );
}

/** «Привязать чат»: добавить выбранные чаты всем выбранным получателям. */
function ChatPicker({ chats, onApply }: { chats: ReportChat[]; onApply: (ids: string[]) => void }) {
  const t = useT();
  const [q, setQ] = useState("");
  const [ids, setIds] = useState<string[]>([]);
  const list = chats
    .filter((c) => c.canSend)
    .filter((c) => {
      const s = q.trim().toLowerCase().replace(/^@/, "");
      return !s || c.title.toLowerCase().includes(s) || (c.username ?? "").toLowerCase().includes(s) || (c.linkedUser ?? "").toLowerCase().includes(s);
    })
    .slice(0, 40);
  return (
    <div>
      <label className="relative block">
        <span className="sr-only">{t("Поиск чата")}</span>
        <Search aria-hidden className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-ink-4" />
        <input
          autoFocus
          type="search"
          value={q}
          onChange={(e) => setQ(e.target.value)}
          placeholder={t("Название, @username или имя")}
          className="h-9 w-full rounded-lg border border-line bg-page pl-8 pr-2 text-[13px] text-ink outline-none focus:border-accent"
        />
      </label>
      <div className="mt-1.5 max-h-[260px] overflow-y-auto">
        {list.length === 0 ? (
          <p className="px-2 py-3 text-[12px] text-ink-3">{t("Чатов не найдено.")}</p>
        ) : (
          list.map((c) => {
            const Icon = c.type === "Private" ? UserRound : UsersRound;
            const on = ids.includes(c.id);
            return (
              <label key={c.id} className={`flex cursor-pointer items-center gap-2.5 rounded-lg px-2 py-1.5 ${on ? "bg-accent-soft" : "hover:bg-muted"}`}>
                <input type="checkbox" checked={on} onChange={() => setIds((l) => (l.includes(c.id) ? l.filter((x) => x !== c.id) : [...l, c.id]))} className="size-4 accent-[var(--color-accent)]" />
                <Icon aria-hidden className="size-4 shrink-0 text-ink-3" />
                <span className="min-w-0">
                  <span className="block truncate text-[12px] font-semibold text-ink">{c.title}</span>
                  <span className="block truncate text-[10px] text-ink-4">
                    {c.type === "Private" ? t("личный") : t("группа")}
                    {c.username ? ` · @${c.username}` : ""}
                    {c.linkedUser ? ` · ${t("привязан: {name}", { name: c.linkedUser })}` : ""}
                  </span>
                </span>
              </label>
            );
          })
        )}
      </div>
      <button type="button" disabled={ids.length === 0} onClick={() => onApply(ids)} className="mt-2 h-9 w-full rounded-lg bg-accent text-[13px] font-semibold text-white disabled:opacity-40">
        {ids.length ? t("Привязать {n} {word}", { n: ids.length, word: t(plural(ids.length, ["чат", "чата", "чатов"])) }) : t("Выберите чаты")}
      </button>
    </div>
  );
}

const REASONS = ["Отпуск", "Больничный", "Уволен", "Временно не нужен"];

/** Отключение одного или нескольких: причина и срок (напоминание — сам получатель не включится). */
export function DisableDialog({
  open,
  names,
  onClose,
  onConfirm,
}: {
  open: boolean;
  names: string[];
  onClose: () => void;
  onConfirm: (reason: string | null, until: string | null) => void;
}) {
  const t = useT();
  const [reason, setReason] = useState("");
  const [until, setUntil] = useState("");
  useEffect(() => {
    if (open) {
      setReason("");
      setUntil("");
    }
  }, [open]);
  const today = new Date().toLocaleDateString("sv-SE", { timeZone: "Asia/Tashkent" });
  const title = names.length === 1 ? t("Отключить «{name}»", { name: names[0] }) : t("Отключить {n} {word}", { n: names.length, word: t(plural(names.length, ["получателя", "получателей", "получателей"])) });
  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={title}
      subtitle={t("Отчёты перестанут уходить, настройки сохранятся")}
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className="h-10 rounded-[10px] border border-line px-4 text-[13px] font-semibold text-ink">
            {t("Отмена")}
          </button>
          <button type="button" onClick={() => onConfirm(reason.trim() || null, until || null)} className="flex h-10 items-center gap-1.5 rounded-[10px] bg-bad px-4 text-[13px] font-semibold text-white">
            <PowerOff aria-hidden className="size-4" />
            {t("Отключить")}
          </button>
        </div>
      }
    >
      <span className="mb-1.5 block text-[11px] font-bold uppercase tracking-wide text-ink-4">{t("Причина")}</span>
      <div className="mb-2 flex flex-wrap gap-1.5">
        {REASONS.map((r) => (
          <button
            key={r}
            type="button"
            aria-pressed={reason === t(r).toLowerCase()}
            onClick={() => setReason(t(r).toLowerCase())}
            className={`h-8 rounded-lg border px-3 text-[13px] font-semibold ${reason === t(r).toLowerCase() ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink-2"}`}
          >
            {t(r)}
          </button>
        ))}
      </div>
      <input
        value={reason}
        maxLength={200}
        onChange={(e) => setReason(e.target.value)}
        placeholder={t("Своя причина (необязательно)")}
        className="h-10 w-full rounded-[10px] border border-line bg-page px-3 text-[13px] text-ink outline-none focus:border-accent"
      />
      <label className="mt-4 block">
        <span className="mb-1.5 block text-[11px] font-bold uppercase tracking-wide text-ink-4">{t("До какого дня (необязательно)")}</span>
        <span className="flex items-center gap-2">
          <input type="date" min={today} value={until} onChange={(e) => setUntil(e.target.value)} className="h-10 rounded-[10px] border border-line bg-page px-3 text-[13px] text-ink outline-none focus:border-accent" />
          {until && (
            <button type="button" onClick={() => setUntil("")} aria-label={t("Без срока")} className="grid size-8 place-items-center rounded-lg text-ink-3 hover:bg-muted">
              <X className="size-4" />
            </button>
          )}
        </span>
        <span className="mt-1.5 block text-[11px] text-ink-3">{t("После срока получатель появится в «Требуют внимания» — включите его сами.")}</span>
      </label>
    </Sheet>
  );
}
