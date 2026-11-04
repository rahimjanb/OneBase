"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { Banknote, CalendarDays, ChevronDown, Download, Scale } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { choiceQuery, choose, quarterName, quarterOf, rangeLabel, readChoice, type PrimaryChoice, type PrimaryPeriodType } from "@/lib/primary-period";
import { monthName, monthShort } from "@/lib/sales/format";

const types: { type: PrimaryPeriodType; label: string }[] = [
  { type: "month", label: "Месяц" },
  { type: "quarter", label: "Квартал" },
  { type: "ytd", label: "С начала года" },
];

/** Выбор из адреса и значения по умолчанию (год и месяц последних данных) — для подсветки и подписей. */
function useChoice(through: string | null) {
  const params = useSearchParams();
  const pathname = usePathname();
  const c = readChoice(Object.fromEntries(params));
  const dataYear = through ? Number(through.slice(0, 4)) : new Date().getFullYear();
  const dataMonth = through ? Number(through.slice(5, 7)) : new Date().getMonth() + 1;
  const year = c.year ?? dataYear;
  const lastMonth = year === dataYear ? dataMonth : year < dataYear ? 12 : 1;
  const month = c.month ?? lastMonth;
  const quarter = c.quarter ?? quarterOf(lastMonth);
  const href = (next: PrimaryChoice) => `${pathname}?${choiceQuery(next)}`;
  return { c, year, month, quarter, href };
}

function useNames() {
  const t = useT();
  return { t, names: { month: (m: number) => monthName(m, t), short: (m: number) => monthShort(m, t), t } };
}

const segment = (on: boolean) =>
  `inline-flex h-8 items-center gap-1.5 whitespace-nowrap rounded-[9px] px-3 text-[13px] transition-colors ${
    on ? "bg-accent-soft font-semibold text-accent-strong ring-1 ring-accent/40" : "text-ink-2 hover:bg-muted hover:text-ink"
  }`;

/** Годы, месяцы или кварталы — внутри уточнения периода (выпадающее меню на компьютере, шторка на телефоне). */
function PeriodChoices({ years, through, onPick }: { years: number[]; through: string | null; onPick?: () => void }) {
  const { t } = useNames();
  const { c, year, month, quarter, href } = useChoice(through);
  const cell = (on: boolean) =>
    `grid h-10 place-items-center rounded-lg border text-[13px] transition-colors ${on ? "border-accent/50 bg-accent-soft font-semibold text-accent-strong" : "border-line text-ink-2 hover:bg-muted hover:text-ink"}`;
  const list = [...new Set([...years, year])].sort((a, b) => b - a);
  return (
    <div className="space-y-3">
      <div>
        <div className="mb-1.5 text-[11px] font-semibold uppercase tracking-wide text-ink-4">{t("Год")}</div>
        <div className="flex flex-wrap gap-1.5">
          {list.map((y) => (
            <Link key={y} href={href(choose(c, { year: y, month: undefined, quarter: undefined }))} onClick={onPick} className={`${cell(y === year)} px-3 tabular-nums`}>
              {y}
            </Link>
          ))}
        </div>
      </div>
      {c.period === "month" && (
        <div>
          <div className="mb-1.5 text-[11px] font-semibold uppercase tracking-wide text-ink-4">{t("Месяц")}</div>
          <div className="grid grid-cols-4 gap-1.5">
            {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
              <Link key={m} href={href(choose(c, { year, month: m }))} onClick={onPick} className={cell(m === month)}>
                {monthShort(m, t)}
              </Link>
            ))}
          </div>
        </div>
      )}
      {c.period === "quarter" && (
        <div>
          <div className="mb-1.5 text-[11px] font-semibold uppercase tracking-wide text-ink-4">{t("Квартал")}</div>
          <div className="grid grid-cols-4 gap-1.5">
            {[1, 2, 3, 4].map((q) => (
              <Link key={q} href={href(choose(c, { year, quarter: q }))} onClick={onPick} className={cell(q === quarter)}>
                {quarterName(q)}
              </Link>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

/** Сегменты типа периода: «Месяц · Квартал · С начала года» (месяц и квартал по умолчанию — последние данные). */
function PeriodTypes({ through, onPick }: { through: string | null; onPick?: () => void }) {
  const { t } = useNames();
  const { c, year, href } = useChoice(through);
  return (
    <div role="radiogroup" aria-label={t("Период")} className="inline-flex shrink-0 items-center gap-0.5 rounded-xl border border-line bg-surface p-1">
      {types.map((x) => (
        <Link
          key={x.type}
          href={href(choose(c, { period: x.type, year, month: undefined, quarter: undefined }))}
          role="radio"
          aria-checked={c.period === x.type}
          onClick={onPick}
          className={segment(c.period === x.type)}
        >
          {t(x.label)}
        </Link>
      ))}
    </div>
  );
}

/** Переключатель единиц: что крупно и по чему доли — тонны или сумма. */
function Units({ through, compact }: { through: string | null; compact?: boolean }) {
  const { t } = useNames();
  const { c, href } = useChoice(through);
  const items: { unit: PrimaryChoice["unit"]; label: string; short: string; icon: typeof Scale }[] = [
    { unit: "t", label: "Тонны", short: "т", icon: Scale },
    { unit: "sum", label: "Сумма", short: "сум", icon: Banknote },
  ];
  return (
    <div role="radiogroup" aria-label={t("Единицы")} className="inline-flex shrink-0 items-center gap-0.5 rounded-xl border border-line bg-surface p-1">
      {items.map((x) => (
        <Link key={x.unit} href={href(choose(c, { unit: x.unit }))} role="radio" aria-checked={c.unit === x.unit} scroll={false} className={`${segment(c.unit === x.unit)} ${compact ? "min-w-11 justify-center" : ""}`}>
          {!compact && <x.icon aria-hidden className="size-4" strokeWidth={1.9} />}
          {compact ? t(x.short) : t(x.label)}
        </Link>
      ))}
    </div>
  );
}

/**
 * Полоса раздела на обзоре «Первички» (компьютер): слева тип периода и уточнение («2026 · янв – 5 ноя ▾» — год, месяц или квартал),
 * справа единицы и «Excel» — выгрузка за выбранный период.
 */
export function PrimaryStrip({ years, through }: { years: number[]; through: string | null }) {
  const { t, names } = useNames();
  const { c, year, month, quarter } = useChoice(through);
  const [open, setOpen] = useState(false);
  const box = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const down = (e: PointerEvent) => !box.current?.contains(e.target as Node) && setOpen(false);
    const key = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("pointerdown", down);
    document.addEventListener("keydown", key);
    return () => {
      document.removeEventListener("pointerdown", down);
      document.removeEventListener("keydown", key);
    };
  }, [open]);
  const label = rangeLabel({ period: c.period, year, month, quarter }, through, names);
  return (
    <div className="flex items-center gap-2 max-lg:hidden">
      <PeriodTypes through={through} />
      <div ref={box} className="relative">
        <button
          type="button"
          onClick={() => setOpen((v) => !v)}
          aria-haspopup="dialog"
          aria-expanded={open}
          className="inline-flex h-10 items-center gap-2 rounded-xl border border-line bg-surface px-3 text-[13px] font-semibold text-ink hover:bg-muted"
        >
          <CalendarDays aria-hidden className="size-4 text-ink-3" strokeWidth={1.9} />
          <span className="tabular-nums">{label}</span>
          <ChevronDown aria-hidden className="size-4 text-ink-3" />
        </button>
        {open && (
          <div role="dialog" aria-label={t("Период")} className="absolute left-0 top-full z-40 mt-2 w-[300px] rounded-2xl border border-line-strong bg-surface-solid p-3 shadow-[0_20px_50px_rgb(0_0_0/0.35)]">
            <PeriodChoices years={years} through={through} onPick={() => setOpen(false)} />
          </div>
        )}
      </div>
    </div>
  );
}

/** Правая часть полосы на компьютере: единицы и «Excel». */
export function PrimaryStripActions({ through }: { through: string | null }) {
  const { t, names } = useNames();
  const { c, year, month, quarter } = useChoice(through);
  const name = `${t("Первичка")} ${rangeLabel({ period: c.period, year, month, quarter }, null, names)}`;
  return (
    <div className="flex items-center gap-2 max-lg:hidden">
      <Units through={through} />
      <a
        href={`/bff/api/sales/primary/overview/xlsx/${encodeURIComponent(name)}.xlsx?${choiceQuery(c, false)}`}
        download={`${name}.xlsx`}
        className="inline-flex h-10 items-center gap-2 rounded-xl border border-line bg-surface px-3 text-[13px] font-semibold text-ink hover:bg-muted"
      >
        <Download aria-hidden className="size-4" strokeWidth={1.9} />
        Excel
      </a>
    </div>
  );
}

/** Телефон: чип периода во всю ширину (выбор — в шторке) и «т | сум». */
export function PrimaryMobileFilters({ years, through }: { years: number[]; through: string | null }) {
  const { t, names } = useNames();
  const { c, year, month, quarter } = useChoice(through);
  const [open, setOpen] = useState(false);
  const label =
    c.period === "ytd"
      ? t("{year}, с начала года", { year })
      : c.period === "quarter"
        ? t("{q} квартал {year}", { q: quarterName(quarter), year })
        : `${monthName(month, t)[0]?.toUpperCase()}${monthName(month, t).slice(1)} ${year}`;
  return (
    <div className="flex items-center gap-2 lg:hidden">
      <button
        type="button"
        onClick={() => setOpen(true)}
        aria-haspopup="dialog"
        className="flex h-11 min-w-0 flex-1 items-center gap-2 rounded-2xl border border-accent/30 bg-accent-soft px-3.5 text-left text-[14px] font-medium text-ink"
      >
        <CalendarDays aria-hidden className="size-4 shrink-0 text-accent-strong" strokeWidth={1.9} />
        <span className="min-w-0 flex-1 truncate">{label}</span>
        <ChevronDown aria-hidden className="size-4 shrink-0 text-accent-strong" />
      </button>
      <Units through={through} compact />
      <Sheet open={open} onClose={() => setOpen(false)} title={t("Период")} subtitle={rangeLabel({ period: c.period, year, month, quarter }, through, names)}>
        <div className="space-y-4 pb-2">
          <PeriodTypes through={through} />
          <PeriodChoices years={years} through={through} onPick={() => setOpen(false)} />
        </div>
      </Sheet>
    </div>
  );
}
