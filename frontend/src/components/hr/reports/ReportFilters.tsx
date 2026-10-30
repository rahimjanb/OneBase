"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { Building2, Check, ChevronDown, Search, X } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import styles from "@/components/hr/timesheet/timesheet.module.css";
import { DateRangePicker } from "@/components/hr/timesheet/DateRangePicker";
import { useLocale, useT } from "@/i18n/client";
import { divisionOptions, type HrDivision } from "@/lib/hr";
import { MAX_REPORT_DAYS, matchesName, rangeLabel, rangeShort, type PeriodKind } from "@/lib/hr-report";
import { focus } from "../overview/bits";

const SEGMENTS: { key: PeriodKind; label: string; short: string }[] = [
  { key: "week", label: "Неделя", short: "Неделя" },
  { key: "month", label: "Месяц", short: "Месяц" },
  { key: "quarter", label: "Квартал", short: "Квартал" },
  { key: "custom", label: "Свой период", short: "Период" },
];

const select = `flex h-10 min-w-0 cursor-pointer items-center gap-2 rounded-[10px] border border-(--ts-border-strong) bg-(--ts-surface) px-3 text-left text-[13.5px] font-medium text-(--ts-text) transition-colors duration-150 hover:bg-(--ts-surface-2)`;

/**
 * Фильтры отчёта: «Неделя · Месяц · Квартал · Свой период», период (календарь, до квартала) и подразделения (мультивыбор с поиском).
 * Кнопки «Показать» нет — выбор сразу перезагружает отчёт; всё хранится в адресе. Подразделения применяются, когда список закрыт
 * («Применить», щелчок мимо, Esc): иначе отчёт пересчитывался бы на каждой галочке.
 */
export function ReportFilters({
  from,
  to,
  today,
  period,
  depts,
  divisions,
  onPeriod,
  onRange,
  onDepts,
}: {
  from: string;
  to: string;
  today: string;
  period: PeriodKind;
  depts: number[];
  divisions: HrDivision[];
  onPeriod: (kind: Exclude<PeriodKind, "custom">) => void;
  onRange: (from: string, to: string) => void;
  onDepts: (ids: number[]) => void;
}) {
  const t = useT();
  const locale = useLocale();
  const [picking, setPicking] = useState(false);
  const [mobilePicking, setMobilePicking] = useState(false);
  const options = useMemo(() => divisionOptions(divisions), [divisions]);
  const names = useMemo(() => new Map(divisions.map((d) => [d.id, d.name])), [divisions]);
  const deptLabel = (all: string) =>
    depts.length === 0 ? t(all) : depts.length === 1 ? (names.get(depts[0]) ?? t("1 подразделение")) : t("Подразделений: {n}", { n: depts.length });

  const segment = (key: PeriodKind) => (key === "custom" ? setPicking(true) : onPeriod(key));

  return (
    <div className="flex flex-col gap-2.5 md:flex-row md:flex-wrap md:items-center">
      <div role="radiogroup" aria-label={t("Период")} className="grid grid-cols-4 rounded-[12px] border border-(--ts-border) bg-(--ts-surface) p-[3px] md:inline-flex">
        {SEGMENTS.map((s) => {
          const on = period === s.key;
          return (
            <button
              key={s.key}
              type="button"
              role="radio"
              aria-checked={on}
              onClick={() => (s.key === "custom" && window.matchMedia("(width < 48rem)").matches ? setMobilePicking(true) : segment(s.key))}
              className={`h-[34px] cursor-pointer whitespace-nowrap rounded-[9px] border px-1.5 text-[13px] font-medium transition-colors duration-150 md:px-3.5 ${focus} ${
                on ? "border-(--ts-border-strong) bg-(--ts-surface-2) text-(--ts-text)" : "border-transparent text-(--ts-muted) hover:text-(--ts-text-2)"
              }`}
            >
              <span className="md:hidden">{t(s.short)}</span>
              <span className="max-md:hidden">{t(s.label)}</span>
            </button>
          );
        })}
      </div>

      {/* Ноутбук и планшет: кнопка периода с календарём и список подразделений. */}
      <div className="hidden md:block">
        <DateRangePicker
          from={from}
          to={to}
          today={today}
          onChange={onRange}
          open={picking}
          onOpenChange={setPicking}
          maxDays={MAX_REPORT_DAYS}
          label={
            <span className="flex items-center gap-2">
              {rangeLabel(from, to, locale)}
              <ChevronDown aria-hidden className="size-4 text-(--ts-subtle)" />
            </span>
          }
          className={select}
        />
      </div>
      <div className="hidden md:block">
        <DeptPopover options={options} selected={depts} label={deptLabel("Все подразделения")} onApply={onDepts} />
      </div>

      {/* Телефон: два поля 50/50, выбор — календарь и нижняя шторка. */}
      <div className="grid grid-cols-2 gap-2.5 md:hidden">
        <DateRangePicker
          from={from}
          to={to}
          today={today}
          onChange={onRange}
          open={mobilePicking}
          onOpenChange={setMobilePicking}
          maxDays={MAX_REPORT_DAYS}
          label={
            <span className="flex min-w-0 flex-1 items-center justify-between gap-1">
              <span className="truncate">{rangeShort(from, to, locale)}</span>
              <ChevronDown aria-hidden className="size-4 shrink-0 text-(--ts-subtle)" />
            </span>
          }
          className={`${select} w-full`}
        />
        <DeptSheetButton options={options} selected={depts} label={deptLabel("Все отделы")} onApply={onDepts} />
      </div>
    </div>
  );
}

type Option = { value: string; label: string };

/** Список подразделений с поиском и галочками. Дочерние — с отступом под родителем; найденные — без дерева. */
function DeptList({ options, draft, setDraft, query }: { options: Option[]; draft: number[]; setDraft: React.Dispatch<React.SetStateAction<number[]>>; query: string }) {
  const t = useT();
  const list = options.filter((o) => matchesName(o.label.replace(/^[\s└]+/, ""), query));
  const toggle = (id: number) => setDraft((d) => (d.includes(id) ? d.filter((x) => x !== id) : [...d, id]));
  if (list.length === 0) return <p className="px-3 py-6 text-center text-[13px] text-(--ts-muted)">{t("Ничего не найдено")}</p>;
  return (
    <ul role="listbox" aria-multiselectable="true" aria-label={t("Подразделения")} className="flex flex-col">
      {list.map((o) => {
        const id = Number(o.value);
        const on = draft.includes(id);
        const depth = query.trim() ? 0 : (o.label.match(/^ */)?.[0].length ?? 0) / 2;
        return (
          <li key={o.value} role="option" aria-selected={on}>
            <button
              type="button"
              onClick={() => toggle(id)}
              style={{ paddingLeft: 12 + depth * 16 }}
              className={`flex min-h-10 w-full cursor-pointer items-center gap-2.5 rounded-[8px] pr-3 text-left text-[13.5px] text-(--ts-text-2) transition-colors duration-150 hover:bg-(--ts-surface-2) max-md:min-h-12 max-md:text-[15px] ${focus}`}
            >
              <span
                aria-hidden
                className={`grid size-[18px] shrink-0 place-items-center rounded-[5px] border ${on ? "border-(--ts-accent) bg-(--ts-accent) text-(--ts-on-accent)" : "border-(--ts-border-strong)"}`}
              >
                {on && <Check className="size-3" strokeWidth={3} />}
              </span>
              <span className="min-w-0 flex-1 truncate">{o.label.replace(/^[\s└]+/, "")}</span>
            </button>
          </li>
        );
      })}
    </ul>
  );
}

function SearchField({ value, onChange, autoFocus }: { value: string; onChange: (v: string) => void; autoFocus?: boolean }) {
  const t = useT();
  return (
    <label className="relative block">
      <span className="sr-only">{t("Найти подразделение")}</span>
      <Search aria-hidden className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-(--ts-subtle)" />
      <input
        type="search"
        value={value}
        autoFocus={autoFocus}
        onChange={(e) => onChange(e.target.value)}
        placeholder={t("Найти подразделение")}
        className="h-10 w-full rounded-[10px] border border-(--ts-border-strong) bg-(--ts-surface) pl-9 pr-3 text-[13.5px] text-(--ts-text) placeholder:text-(--ts-subtle) focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-accent) max-md:h-11 max-md:text-[16px]"
      />
    </label>
  );
}

const footerButton = `h-9 cursor-pointer rounded-[9px] px-3.5 text-[13.5px] font-semibold transition-colors duration-150 ${focus}`;

/** Ноутбук: выпадающий список под кнопкой; закрыли — выбор применяется. */
function DeptPopover({ options, selected, label, onApply }: { options: Option[]; selected: number[]; label: string; onApply: (ids: number[]) => void }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState(selected);
  const [query, setQuery] = useState("");
  const box = useRef<HTMLDivElement>(null);
  const state = useRef({ draft, selected, onApply });
  state.current = { draft, selected, onApply };

  const close = (apply: boolean) => {
    setOpen(false);
    const { draft: d, selected: s, onApply: apply_ } = state.current;
    if (apply && (d.length !== s.length || d.some((x) => !s.includes(x)))) apply_(d);
  };
  const closeRef = useRef(close);
  closeRef.current = close;

  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => !box.current?.contains(e.target as Node) && closeRef.current(true);
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && closeRef.current(true);
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  return (
    <div ref={box} className="relative">
      <button
        type="button"
        aria-expanded={open}
        aria-haspopup="listbox"
        onClick={() => {
          if (open) return close(true);
          setDraft(selected);
          setQuery("");
          setOpen(true);
        }}
        className={`${select} max-w-[280px] ${focus}`}
      >
        <Building2 aria-hidden className="size-4 shrink-0 text-(--ts-subtle)" />
        <span className="truncate">{label}</span>
        <ChevronDown aria-hidden className="size-4 shrink-0 text-(--ts-subtle)" />
      </button>
      {open && (
        <div className="absolute left-0 top-[calc(100%+6px)] z-40 flex w-[320px] flex-col rounded-[12px] border border-(--ts-border) bg-(--ts-surface) shadow-[0_12px_32px_#0e13221f]">
          <div className="p-2.5 pb-1.5">
            <SearchField value={query} onChange={setQuery} autoFocus />
          </div>
          <div className="max-h-[320px] overflow-y-auto px-1.5 pb-1.5">
            <DeptList options={options} draft={draft} setDraft={setDraft} query={query} />
          </div>
          <div className="flex items-center justify-between gap-2 border-t border-(--ts-border) p-2.5">
            <button type="button" disabled={draft.length === 0} onClick={() => setDraft([])} className={`${footerButton} text-(--ts-muted) hover:bg-(--ts-surface-2) disabled:cursor-default disabled:opacity-40`}>
              {t("Все подразделения")}
            </button>
            <button type="button" onClick={() => close(true)} className={`${footerButton} bg-(--ts-accent) text-(--ts-on-accent) hover:opacity-90`}>
              {t("Применить")}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

/** Телефон: кнопка-селект и нижняя шторка с поиском; «Применить» — отчёт по выбранным. */
function DeptSheetButton({ options, selected, label, onApply }: { options: Option[]; selected: number[]; label: string; onApply: (ids: number[]) => void }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState(selected);
  const [query, setQuery] = useState("");
  const apply = () => {
    setOpen(false);
    if (draft.length !== selected.length || draft.some((x) => !selected.includes(x))) onApply(draft);
  };
  return (
    <>
      <button
        type="button"
        aria-haspopup="dialog"
        onClick={() => {
          setDraft(selected);
          setQuery("");
          setOpen(true);
        }}
        className={`${select} w-full ${focus}`}
      >
        <Building2 aria-hidden className="size-4 shrink-0 text-(--ts-subtle)" />
        <span className="min-w-0 flex-1 truncate">{label}</span>
        <ChevronDown aria-hidden className="size-4 shrink-0 text-(--ts-subtle)" />
      </button>
      <Sheet
        open={open}
        onClose={() => setOpen(false)}
        title={t("Подразделения")}
        footer={
          <div className={`${styles.root} flex gap-2.5`} style={{ background: "transparent" }}>
            <button
              type="button"
              disabled={draft.length === 0}
              onClick={() => setDraft([])}
              className={`inline-flex h-12 flex-1 cursor-pointer items-center justify-center gap-1.5 rounded-[12px] border border-(--ts-border-strong) text-[15px] font-semibold text-(--ts-text-2) disabled:opacity-40 ${focus}`}
            >
              <X aria-hidden className="size-4" />
              {t("Сбросить")}
            </button>
            <button type="button" onClick={apply} className={`h-12 flex-[2] cursor-pointer rounded-[12px] bg-(--ts-accent) text-[15px] font-semibold text-(--ts-on-accent) ${focus}`}>
              {draft.length > 0 ? t("Показать: {n}", { n: draft.length }) : t("Все отделы")}
            </button>
          </div>
        }
      >
        <div className={`${styles.root} flex flex-col gap-2`} style={{ background: "transparent" }}>
          <SearchField value={query} onChange={setQuery} />
          <DeptList options={options} draft={draft} setDraft={setDraft} query={query} />
        </div>
      </Sheet>
    </>
  );
}

