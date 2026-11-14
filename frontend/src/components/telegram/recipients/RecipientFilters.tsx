"use client";

import { useEffect, useState } from "react";
import { Activity, Check, ChevronDown, FileChartColumn, MapPin, MessageCircleOff, PowerOff, Search, SlidersHorizontal, TriangleAlert, UserPlus, UserRoundX, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import {
  ISSUES,
  LAST_FILTERS,
  NO_FILTERS,
  REPORTS,
  SORTS,
  activeCount,
  facets as countFacets,
  isFiltered,
  reportByN,
  type Ctx,
  type Facets,
  type Issue,
  type LastFilter,
  type RecipientFilters,
  type SortKey,
} from "@/lib/telegram-recipients";
import type { ReportJob, ReportRecipient, ReportRegion, ReportRole } from "@/lib/telegramReports";
import { Dropdown, RoleBar } from "./bits";

type Patch = (patch: Partial<RecipientFilters>) => void;

export const ISSUE_VIEW: Record<Issue, { label: string; short: string; icon: typeof MessageCircleOff; tone: "warn" | "muted" | "bad" }> = {
  no_chat: { label: "Без чата", short: "Без чата", icon: MessageCircleOff, tone: "warn" },
  disabled: { label: "Отключены", short: "Отключены", icon: PowerOff, tone: "muted" },
  delivery_error: { label: "Ошибки доставки", short: "Ошибки", icon: TriangleAlert, tone: "bad" },
  no_supervisor: { label: "Без супервайзера", short: "Без СВ", icon: UserRoundX, tone: "warn" },
};

const LAST_LABEL: Record<LastFilter, string> = { today: "Сегодня", "7d": "За 7 дней", never: "Не было" };
const SORT_LABEL: Record<SortKey, string> = { problems: "Проблемные сверху", region: "По региону", name: "По имени" };

const ICON_TONE = { warn: "text-warn", muted: "text-ink-3", bad: "text-bad" } as const;
const ON_TONE = { warn: "border-warn bg-warn-soft text-warn", muted: "border-ink-3 bg-muted text-ink", bad: "border-bad bg-bad-soft text-bad" } as const;

/** Поиск с задержкой 300 мс: печать не перерисовывает таблицу на каждую букву. */
function SearchBox({ value, onChange, placeholder, tall }: { value: string; onChange: (v: string) => void; placeholder: string; tall?: boolean }) {
  const t = useT();
  const [text, setText] = useState(value);
  useEffect(() => setText(value), [value]);
  useEffect(() => {
    if (text === value) return;
    const timer = setTimeout(() => onChange(text), 300);
    return () => clearTimeout(timer);
  }, [text, value, onChange]);
  return (
    <label className={`relative block min-w-0 flex-1 ${tall ? "" : "min-w-[200px]"}`}>
      <span className="sr-only">{t("Поиск получателей")}</span>
      <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-4" />
      <input
        type="search"
        value={text}
        onChange={(e) => setText(e.target.value)}
        placeholder={t(placeholder)}
        className={`w-full rounded-[10px] border border-line bg-page pl-9 pr-3 text-[13px] text-ink outline-none placeholder:text-ink-4 focus:border-accent ${tall ? "h-[42px] text-[14px]" : "h-[38px]"}`}
      />
    </label>
  );
}

/** Роль — сегменты со счётчиками: «Все 76 / ▍РМ 6 / ▍СВ 18 / ▍ТП 52». */
export function RoleSegments({ value, facets, onChange, full }: { value: ReportRole | null; facets: Facets; onChange: (role: ReportRole | null) => void; full?: boolean }) {
  const t = useT();
  const items: { key: ReportRole | null; label: string; n: number }[] = [
    { key: null, label: "Все", n: facets.role.all },
    { key: "rm", label: "РМ", n: facets.role.rm },
    { key: "sv", label: "СВ", n: facets.role.sv },
    { key: "tp", label: "ТП", n: facets.role.tp },
  ];
  return (
    <div role="radiogroup" aria-label={t("Роль")} className={`flex shrink-0 gap-0.5 rounded-[10px] border border-line bg-page p-[3px] ${full ? "w-full" : ""}`}>
      {items.map((item) => {
        const on = value === item.key;
        return (
          <button
            key={item.key ?? "all"}
            type="button"
            role="radio"
            aria-checked={on}
            onClick={() => onChange(item.key)}
            className={`flex items-center justify-center gap-1.5 whitespace-nowrap rounded-[7px] px-2.5 text-[13px] font-semibold ${full ? "h-8 flex-1" : "h-[30px]"} ${
              on ? "bg-accent text-white" : "text-ink-2 hover:bg-muted"
            }`}
          >
            {item.key && !on && <RoleBar role={item.key} />}
            {t(item.label)}
            <span className={`text-[11px] font-semibold tabular-nums ${on ? "text-white/80" : "text-ink-4"}`}>{item.n}</span>
          </button>
        );
      })}
    </div>
  );
}

function MenuButton({ icon: Icon, label, selected, open, onClick }: { icon: typeof MapPin; label: string; selected: boolean; open: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      aria-expanded={open}
      onClick={onClick}
      className={`flex h-[38px] shrink-0 items-center gap-2 whitespace-nowrap rounded-[10px] border px-3 text-[13px] font-semibold ${
        selected ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink hover:bg-muted"
      }`}
    >
      <Icon aria-hidden className="size-4 shrink-0" />
      <span className="max-w-[180px] truncate">{label}</span>
      <ChevronDown aria-hidden className="size-3.5 shrink-0 opacity-70" />
    </button>
  );
}

function MenuItem({ on, children, onClick, count }: { on: boolean; children: React.ReactNode; onClick: () => void; count?: number }) {
  return (
    <button
      type="button"
      role="menuitemcheckbox"
      aria-checked={on}
      onClick={onClick}
      className={`flex w-full items-center gap-2 rounded-lg px-2.5 py-2 text-left text-[13px] ${on ? "bg-accent-soft text-accent-strong" : "text-ink hover:bg-muted"}`}
    >
      <span className={`grid size-4 shrink-0 place-items-center rounded border ${on ? "border-accent bg-accent text-white" : "border-line-strong"}`}>{on && <Check className="size-3" />}</span>
      <span className="min-w-0 flex-1 truncate">{children}</span>
      {count !== undefined && <span className="text-[11px] tabular-nums text-ink-4">{count}</span>}
    </button>
  );
}

/** Строка фильтров на компьютере: поиск, роль, регион (несколько), отчёт и запуск, статус, «Добавить»; ниже — быстрые фильтры проблем. */
export function FilterBar({
  f,
  set,
  facets,
  regions,
  jobs,
  total,
  regionsShown,
  onAdd,
  canAdd,
}: {
  f: RecipientFilters;
  set: Patch;
  facets: Facets;
  regions: ReportRegion[];
  jobs: ReportJob[];
  total: number;
  regionsShown: number;
  onAdd: () => void;
  canAdd: boolean;
}) {
  const t = useT();
  const regionLabel =
    f.regions.length === 0 ? t("Регион: все") : f.regions.length === 1 ? (regions.find((r) => r.id === f.regions[0])?.name ?? "—") : t("Регионов: {n}", { n: f.regions.length });
  const report = f.report ? reportByN(f.report) : null;
  const slot = f.slot ? jobs.find((j) => j.code === f.slot) : null;
  const reportLabel = report ? `${report.n}. ${t(report.title)}${slot ? ` · ${slot.time}` : ""}` : t("Отчёт: любой");
  const statusLabel = f.status === "active" ? t("Активные") : f.status === "disabled" ? t("Отключённые") : t("Статус: все");
  const toggleRegion = (id: string) => set({ regions: f.regions.includes(id) ? f.regions.filter((x) => x !== id) : [...f.regions, id] });
  const toggleIssue = (i: Issue) => set({ issues: f.issues.includes(i) ? f.issues.filter((x) => x !== i) : [...f.issues, i] });
  const filtered = isFiltered(f);

  return (
    <div className="rounded-[16px] border border-line bg-surface px-4 py-3.5">
      <div className="flex flex-wrap items-center gap-2.5">
        <SearchBox value={f.q} onChange={(q) => set({ q })} placeholder="Имя, регион, чат или @username" />
        <RoleSegments value={f.role} facets={facets} onChange={(role) => set({ role })} />
        <Dropdown
          width={260}
          trigger={({ open, toggle }) => <MenuButton icon={MapPin} label={regionLabel} selected={f.regions.length > 0} open={open} onClick={toggle} />}
        >
          {() => (
            <div role="menu" aria-label={t("Регион")}>
              <MenuItem on={f.regions.length === 0} onClick={() => set({ regions: [] })}>
                {t("Все регионы")}
              </MenuItem>
              {regions.map((r) => (
                <MenuItem key={r.id} on={f.regions.includes(r.id)} onClick={() => toggleRegion(r.id)} count={facets.region[r.id] ?? 0}>
                  {r.name}
                </MenuItem>
              ))}
            </div>
          )}
        </Dropdown>
        <Dropdown
          width={300}
          trigger={({ open, toggle }) => <MenuButton icon={FileChartColumn} label={reportLabel} selected={!!f.report} open={open} onClick={toggle} />}
        >
          {(close) => (
            <div role="menu" aria-label={t("Отчёт")}>
              <MenuItem on={!f.report} onClick={() => (set({ report: null, slot: null }), close())}>
                {t("Любой отчёт")}
              </MenuItem>
              {REPORTS.map((r) => {
                const on = f.report === r.n;
                const slots = jobs.filter((j) => j.report === r.type);
                return (
                  <div key={r.n}>
                    <MenuItem on={on && !f.slot} onClick={() => set({ report: r.n, slot: null })} count={facets.report[r.n] ?? 0}>
                      {r.n}. {t(r.title)}
                    </MenuItem>
                    {on && slots.length > 1 && (
                      <div className="mb-1 ml-8 flex flex-wrap gap-1">
                        {slots.map((j) => (
                          <button
                            key={j.code}
                            type="button"
                            aria-pressed={f.slot === j.code}
                            onClick={() => set({ slot: f.slot === j.code ? null : j.code })}
                            title={t(j.slotTitle)}
                            className={`h-6 rounded-md border px-1.5 text-[11px] font-semibold ${f.slot === j.code ? "border-accent bg-accent-soft text-accent-strong" : "border-line-strong text-ink-3"}`}
                          >
                            {j.time}
                          </button>
                        ))}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </Dropdown>
        <Dropdown
          width={220}
          trigger={({ open, toggle }) => <MenuButton icon={Activity} label={statusLabel} selected={!!f.status} open={open} onClick={toggle} />}
        >
          {(close) => (
            <div role="menu" aria-label={t("Статус")}>
              {(
                [
                  [null, "Все", facets.status.active + facets.status.disabled],
                  ["active", "Активные", facets.status.active],
                  ["disabled", "Отключённые", facets.status.disabled],
                ] as const
              ).map(([key, label, n]) => (
                <MenuItem key={key ?? "all"} on={f.status === key} onClick={() => (set({ status: key }), close())} count={n}>
                  {t(label)}
                </MenuItem>
              ))}
            </div>
          )}
        </Dropdown>
        <button
          type="button"
          onClick={onAdd}
          disabled={!canAdd}
          className="ml-auto flex h-[38px] shrink-0 items-center gap-2 rounded-[10px] bg-accent px-4 text-[13px] font-semibold text-white hover:brightness-110 disabled:opacity-40"
        >
          <UserPlus aria-hidden className="size-4" />
          <span className="2xl:hidden">{t("Добавить")}</span>
          <span className="hidden 2xl:inline">{t("Добавить получателя")}</span>
        </button>
      </div>
      <div className="mt-2.5 flex flex-wrap items-center gap-2">
        <span className="text-[12px] text-ink-3">{t("Быстрые")}:</span>
        {ISSUES.map((i) => {
          const view = ISSUE_VIEW[i];
          const on = f.issues.includes(i);
          return (
            <button
              key={i}
              type="button"
              aria-pressed={on}
              onClick={() => toggleIssue(i)}
              className={`flex h-[28px] items-center gap-1.5 rounded-lg border px-2.5 text-[12px] font-semibold ${on ? ON_TONE[view.tone] : "border-line bg-muted text-ink-2 hover:text-ink"}`}
            >
              <view.icon aria-hidden className={`size-3.5 ${on ? "" : ICON_TONE[view.tone]}`} />
              {t(view.label)}
              <span className={`tabular-nums ${on ? "" : "text-ink-4"}`}>{facets.issue[i]}</span>
            </button>
          );
        })}
        <span className="ml-auto flex items-center gap-3 text-[12px]">
          <span className="font-semibold text-ink-2">
            {filtered ? t("Найдено {n} из {total}", { n: facets.total, total }) : `${total} ${t(plural(total, ["получатель", "получателя", "получателей"]))} · ${regionsShown} ${t(plural(regionsShown, ["регион", "региона", "регионов"]))}`}
          </span>
          {filtered && (
            <button type="button" onClick={() => set({ ...NO_FILTERS, sort: f.sort, flat: f.flat })} className="flex items-center gap-1 text-ink-3 hover:text-ink">
              <X aria-hidden className="size-3.5" />
              {t("Сбросить фильтры")}
            </button>
          )}
        </span>
      </div>
    </div>
  );
}

/** Телефон и планшет: поиск, «Фильтры N», роль, плашки активных фильтров и «Найдено N». */
export function MobileFilters({
  f,
  set,
  facets,
  regions,
  jobs,
  recipients,
  ctx,
}: {
  f: RecipientFilters;
  set: Patch;
  facets: Facets;
  regions: ReportRegion[];
  jobs: ReportJob[];
  recipients: ReportRecipient[];
  ctx: Ctx;
}) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const n = activeCount(f);
  const chips: { key: string; label: string; clear: () => void }[] = [
    ...f.regions.map((id) => ({ key: `r:${id}`, label: regions.find((r) => r.id === id)?.name ?? "—", clear: () => set({ regions: f.regions.filter((x) => x !== id) }) })),
    ...f.issues.map((i) => ({ key: `i:${i}`, label: t(ISSUE_VIEW[i].short), clear: () => set({ issues: f.issues.filter((x) => x !== i) }) })),
    ...(f.report ? [{ key: "rep", label: `${f.report}. ${t(reportByN(f.report)!.title)}${f.slot ? ` · ${jobs.find((j) => j.code === f.slot)?.time ?? ""}` : ""}`, clear: () => set({ report: null, slot: null }) }] : []),
    ...(f.status ? [{ key: "st", label: f.status === "active" ? t("Активные") : t("Отключённые"), clear: () => set({ status: null }) }] : []),
    ...(f.last ? [{ key: "last", label: t(LAST_LABEL[f.last]), clear: () => set({ last: null }) }] : []),
  ];
  return (
    <div className="space-y-3">
      <div className="flex gap-2.5">
        <SearchBox value={f.q} onChange={(q) => set({ q })} placeholder="Имя, регион или чат" tall />
        <button
          type="button"
          onClick={() => setOpen(true)}
          aria-haspopup="dialog"
          className={`flex h-[42px] shrink-0 items-center gap-2 rounded-[12px] border px-3 text-[14px] font-semibold ${n > 0 ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink"}`}
        >
          <SlidersHorizontal aria-hidden className="size-4" />
          {t("Фильтры")}
          {n > 0 && <span className="grid size-[18px] place-items-center rounded-full bg-accent text-[11px] font-bold text-white">{n}</span>}
        </button>
      </div>
      <RoleSegments value={f.role} facets={facets} onChange={(role) => set({ role })} full />
      {(chips.length > 0 || f.q.trim()) && (
        <div className="flex items-center gap-2">
          <div className="no-scrollbar flex min-w-0 flex-1 gap-2 overflow-x-auto">
            {chips.map((c) => (
              <span key={c.key} className="flex h-8 shrink-0 items-center gap-1.5 rounded-[10px] border border-line bg-muted pl-3 pr-1.5 text-[13px] font-semibold text-ink">
                {c.label}
                <button type="button" onClick={c.clear} aria-label={t("Убрать фильтр «{name}»", { name: c.label })} className="grid size-6 place-items-center rounded-md text-ink-3 hover:text-ink">
                  <X className="size-3.5" />
                </button>
              </span>
            ))}
          </div>
          <span className="shrink-0 text-[12px] font-semibold text-ink-3">{t("Найдено {n}", { n: facets.total })}</span>
        </div>
      )}
      <FilterSheet open={open} onClose={() => setOpen(false)} f={f} apply={(next) => (set(next), setOpen(false))} regions={regions} recipients={recipients} ctx={ctx} />
    </div>
  );
}

function SheetButton({ on, onClick, children, className = "" }: { on: boolean; onClick: () => void; children: React.ReactNode; className?: string }) {
  return (
    <button
      type="button"
      aria-pressed={on}
      onClick={onClick}
      className={`flex h-9 min-w-0 items-center justify-center gap-1.5 rounded-[10px] border px-2 text-[13px] font-semibold ${on ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink"} ${className}`}
    >
      {on && <Check aria-hidden className="size-3.5 shrink-0" />}
      {children}
    </button>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  const t = useT();
  return (
    <section className="mt-5 first:mt-1">
      <h3 className="mb-2 text-[11px] font-bold uppercase tracking-wide text-ink-4">{t(title)}</h3>
      {children}
    </section>
  );
}

/** Шторка фильтров: черновик; «Показать N получателей» — с живым пересчётом. */
function FilterSheet({
  open,
  onClose,
  f,
  apply,
  regions,
  recipients,
  ctx,
}: {
  open: boolean;
  onClose: () => void;
  f: RecipientFilters;
  apply: (f: RecipientFilters) => void;
  regions: ReportRegion[];
  recipients: ReportRecipient[];
  ctx: Ctx;
}) {
  const t = useT();
  const [draft, setDraft] = useState(f);
  useEffect(() => {
    if (open) setDraft(f);
  }, [open, f]);
  const facets = countFacets(recipients, draft, ctx);
  const n = activeCount(draft);
  const patch = (p: Partial<RecipientFilters>) => setDraft((d) => ({ ...d, ...p }));
  const toggle = <T,>(list: T[], x: T) => (list.includes(x) ? list.filter((y) => y !== x) : [...list, x]);
  const found = facets.total;
  return (
    <Sheet
      open={open}
      onClose={onClose}
      title={t("Фильтры")}
      subtitle={n > 0 ? t("{n} активных", { n }) : undefined}
      footer={
        <div className="flex gap-2.5">
          <button type="button" onClick={() => setDraft({ ...NO_FILTERS, q: draft.q, role: draft.role })} className="h-12 w-[120px] shrink-0 rounded-[12px] border border-line text-[15px] font-semibold text-ink">
            {t("Сбросить")}
          </button>
          <button type="button" onClick={() => apply(draft)} className="h-12 min-w-0 flex-1 rounded-[12px] bg-accent text-[15px] font-semibold text-white">
            {found === 0 ? t("Никого не найдено") : t("Показать {n} {word}", { n: found, word: t(plural(found, ["получателя", "получателя", "получателей"])) })}
          </button>
        </div>
      }
    >
      <Section title="Регион">
        <div className="grid grid-cols-3 gap-2">
          {regions.map((r) => (
            <SheetButton key={r.id} on={draft.regions.includes(r.id)} onClick={() => patch({ regions: toggle(draft.regions, r.id) })}>
              <span className="truncate">{r.name}</span>
              <span className="shrink-0 text-[12px] font-medium text-ink-4">{facets.region[r.id] ?? 0}</span>
            </SheetButton>
          ))}
          <SheetButton on={draft.regions.length === 0} onClick={() => patch({ regions: [] })}>
            {t("Все")}
          </SheetButton>
        </div>
      </Section>
      <Section title="Состояние">
        <div className="grid grid-cols-2 gap-2">
          {ISSUES.map((i) => {
            const view = ISSUE_VIEW[i];
            const on = draft.issues.includes(i);
            return (
              <SheetButton key={i} on={on} onClick={() => patch({ issues: toggle(draft.issues, i) })}>
                {!on && <view.icon aria-hidden className={`size-4 shrink-0 ${ICON_TONE[view.tone]}`} />}
                <span className="truncate">{t(view.short)}</span>
                <span className="shrink-0 text-[12px] font-medium text-ink-4">{facets.issue[i]}</span>
              </SheetButton>
            );
          })}
        </div>
      </Section>
      <Section title="Получает отчёт">
        <div className="grid grid-cols-3 gap-2">
          {REPORTS.map((r) => {
            const on = draft.report === r.n;
            return (
              <button
                key={r.n}
                type="button"
                aria-pressed={on}
                onClick={() => patch({ report: on ? null : r.n, slot: null })}
                className={`flex h-10 min-w-0 items-center gap-2 rounded-[10px] border px-2 text-[13px] font-semibold ${on ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink"}`}
              >
                <span className={`grid size-6 shrink-0 place-items-center rounded-md text-[11px] font-bold ${on ? "bg-accent text-white" : "bg-muted text-ink-3"}`}>{r.n}</span>
                <span className="truncate">{t(r.short.charAt(0).toUpperCase() + r.short.slice(1))}</span>
              </button>
            );
          })}
        </div>
      </Section>
      <Section title="Последняя отправка">
        <div className="grid grid-cols-3 gap-2">
          {LAST_FILTERS.map((l) => (
            <SheetButton key={l} on={draft.last === l} onClick={() => patch({ last: draft.last === l ? null : l })}>
              {t(LAST_LABEL[l])}
            </SheetButton>
          ))}
        </div>
      </Section>
      <Section title="Сортировка">
        <div role="radiogroup" aria-label={t("Сортировка")} className="flex gap-0.5 rounded-[12px] border border-line bg-page p-[3px]">
          {SORTS.map((s) => (
            <button
              key={s}
              type="button"
              role="radio"
              aria-checked={draft.sort === s}
              onClick={() => patch({ sort: s })}
              className={`h-9 min-w-0 flex-1 truncate rounded-[9px] px-1.5 text-[13px] font-semibold ${draft.sort === s ? "border border-line-strong bg-surface text-ink" : "text-ink-3"}`}
            >
              {t(SORT_LABEL[s])}
            </button>
          ))}
        </div>
      </Section>
    </Sheet>
  );
}
