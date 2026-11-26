"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import { ArrowDownRight, ArrowUpRight, Database, FlaskConical, Info, X } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  change,
  date,
  dimensionKinds,
  full,
  moment,
  money,
  monthShort,
  ONEC_SETTINGS,
  periodPresets,
  type FinanceDimension,
  type FinanceMeta,
  type FinanceOrganization,
} from "@/lib/finance";

/** Сменить параметры адреса страницы (null — убрать); номер страницы сбрасывается. Данные пересчитывает сервер. */
function useGo() {
  const router = useRouter();
  const pathname = usePathname();
  const sp = useSearchParams();
  return (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(sp.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }

    next.delete("page");
    router.push(`${pathname}?${next.toString()}`);
  };
}

/** Применённые отборы — плашками с крестиком (убрать отбор). */
function ActiveDimensions({ dimensions }: { dimensions: FinanceDimension[] }) {
  const t = useT();
  const go = useGo();
  if (dimensions.length === 0) return null;
  return (
    <div className="flex flex-wrap gap-1.5">
      {dimensions.map((d) => (
        <span key={d.kind} className="inline-flex max-w-full items-center gap-1 rounded-full border border-accent/30 bg-accent-soft px-2.5 py-1 text-xs text-accent-strong">
          <span className="truncate">
            {t(dimensionKinds.find((k) => k.key === d.kind)?.label ?? d.kind)}: {d.name}
          </span>
          <button type="button" aria-label={t("Убрать отбор")} className="shrink-0 rounded-full hover:bg-accent/10" onClick={() => go({ [d.kind]: null })}>
            <X aria-hidden className="size-3.5" />
          </button>
        </span>
      ))}
    </div>
  );
}

/** Добавить отбор: вид (контрагент, номенклатура, склад, подразделение) и поиск по названию, коду или ИНН среди данных 1С. */
function DimensionPicker() {
  const t = useT();
  const go = useGo();
  const [kind, setKind] = useState(dimensionKinds[0].key);
  const [q, setQ] = useState("");
  const [open, setOpen] = useState(false);
  const [options, setOptions] = useState<FinanceDimension[]>([]);
  useEffect(() => {
    if (!open) return;
    const id = setTimeout(() => {
      bff<FinanceDimension[]>(`finance/dimensions?kind=${kind}&q=${encodeURIComponent(q)}&limit=12`).then(setOptions).catch(() => setOptions([]));
    }, 250);
    return () => clearTimeout(id);
  }, [kind, q, open]);
  return (
    <div className="relative flex items-center gap-1">
      <select aria-label={t("Вид отбора")} value={kind} onChange={(e) => setKind(e.target.value)} className="h-8 rounded-full border border-line bg-surface px-2 text-xs font-medium text-ink">
        {dimensionKinds.map((k) => (
          <option key={k.key} value={k.key}>
            {t(k.label)}
          </option>
        ))}
      </select>
      <input
        aria-label={t("Отбор")}
        value={q}
        placeholder={t("название, код или ИНН")}
        onFocus={() => setOpen(true)}
        onBlur={() => setTimeout(() => setOpen(false), 150)}
        onChange={(e) => setQ(e.target.value)}
        className="h-8 w-44 rounded-full border border-line bg-surface px-3 text-xs"
      />
      {open && options.length > 0 && (
        <ul className="absolute left-0 top-9 z-20 max-h-64 w-72 max-w-[80vw] overflow-y-auto rounded-lg border border-line bg-surface p-1 shadow-lg">
          {options.map((o) => (
            <li key={o.id}>
              <button
                type="button"
                className="w-full truncate rounded-md px-2 py-1.5 text-left text-xs text-ink hover:bg-muted"
                onMouseDown={(e) => {
                  e.preventDefault();
                  setQ("");
                  setOpen(false);
                  go({ [kind]: o.id });
                }}
              >
                {o.name}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/**
 * Откуда цифры: «Факт · 1С:Бухгалтерия», период, организация, когда синхронизировано и по какую дату есть данные. Демо-данные тестового
 * источника — заметной пометкой. Управленческие данные OneBase (планы, бюджеты, прогнозы) здесь не смешиваются.
 */
export function MetaBar({ meta, settingsLink = false }: { meta: FinanceMeta; settingsLink?: boolean }) {
  const t = useT();
  return (
    <div className="space-y-2">
      {meta.demo && (
        <p className="flex items-start gap-2 rounded-lg border border-warn/30 bg-warn-soft px-3 py-2 text-sm text-warn">
          <FlaskConical aria-hidden className="mt-0.5 size-4 shrink-0" />
          {t("Демо-данные тестового источника 1С — не настоящая бухгалтерия. Настоящая 1С подключается после установки в ней расширения OneBaseExchange (HTTP-сервис только для чтения).")}
        </p>
      )}
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-ink-3">
        <span className="inline-flex items-center gap-1.5 rounded-full bg-accent-soft px-2.5 py-1 font-semibold text-accent-strong">
          <Database aria-hidden className="size-3.5" />
          {t("Факт · {source}", { source: meta.demo ? "1С (демо)" : "1С:Бухгалтерия" })}
        </span>
        <span>
          {t("Период")}: {date(meta.from)} — {date(meta.to)}
        </span>
        <span>{meta.organizationName ?? t("Все организации")}</span>
        <span>
          {t("Синхронизировано")}: {meta.syncedAt ? moment(meta.syncedAt) : t("ещё не было")}
        </span>
        {meta.dataThrough && (
          <span>
            {t("Проводки по")} {date(meta.dataThrough)}
          </span>
        )}
      </div>
      <ActiveDimensions dimensions={meta.dimensions ?? []} />
      {meta.note && (
        <p className="flex items-start gap-2 rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink-2">
          <Info aria-hidden className="mt-0.5 size-4 shrink-0 text-ink-3" />
          <span>
            {t(meta.note)}{" "}
            {settingsLink && (
              <Link href={ONEC_SETTINGS} className="font-medium text-accent-strong hover:underline">
                {t("Открыть настройки 1С")}
              </Link>
            )}
          </span>
        </p>
      )}
    </div>
  );
}

/** Изменение к прошлому периоду: рост дохода — зелёный, рост расхода — красный (good = направление «к лучшему»). */
export function Delta({ now, before, good = "up" }: { now: number; before: number | null | undefined; good?: "up" | "down" }) {
  const pct = change(now, before);
  if (pct == null || !Number.isFinite(pct)) return null;
  const up = pct >= 0;
  const better = good === "up" ? up : !up;
  const Icon = up ? ArrowUpRight : ArrowDownRight;
  return (
    <span className={`inline-flex items-center gap-0.5 text-xs font-semibold tabular-nums ${Math.abs(pct) < 0.5 ? "text-ink-3" : better ? "text-ok" : "text-bad"}`}>
      <Icon aria-hidden className="size-3.5" />
      {Math.abs(pct) >= 1000 ? ">999" : full(Math.abs(pct), Math.abs(pct) < 10 ? 1 : 0)} %
    </span>
  );
}

export function Kpi({ label, value, previous, good = "up", hint, href }: { label: string; value: number; previous?: number | null; good?: "up" | "down"; hint?: string; href?: string }) {
  const t = useT();
  const body = (
    <>
      <div className="text-xs font-medium text-ink-3">{t(label)}</div>
      <div className={`mt-1 text-xl font-semibold tabular-nums ${value < 0 ? "text-bad" : "text-ink"}`} title={full(value)}>
        {money(value)}
      </div>
      <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-ink-3">
        {previous !== undefined && <Delta now={value} before={previous} good={good} />}
        {previous != null && <span>{t("было {value}", { value: money(previous) })}</span>}
        {hint && <span>{t(hint)}</span>}
      </div>
    </>
  );
  const box = "block rounded-xl border border-line bg-surface p-4";
  return href ? (
    <Link href={href} className={`${box} hover:border-line-strong`}>
      {body}
    </Link>
  ) : (
    <div className={box}>{body}</div>
  );
}

/** Столбцы по месяцам: несколько рядов рядом (например, выручка и расходы) и линия-итог (чистая прибыль). */
export function MonthBars({
  months,
  series,
  line,
}: {
  months: string[];
  series: { label: string; values: number[]; className: string }[];
  line?: { label: string; values: number[] };
}) {
  const t = useT();
  const all = [...series.flatMap((s) => s.values), ...(line?.values ?? [])];
  const max = Math.max(1, ...all.map((v) => Math.abs(v)));
  const hasNegative = all.some((v) => v < 0);
  const h = 160;
  const zero = hasNegative ? h / 2 : h;
  const scale = (v: number) => (Math.abs(v) / max) * (hasNegative ? h / 2 : h);
  const slot = 100 / Math.max(1, months.length);
  const bar = (slot * 0.8) / Math.max(1, series.length);
  const points = line?.values.map((v, i) => `${i * slot + slot / 2},${v >= 0 ? zero - scale(v) : zero + scale(v)}`).join(" ");
  return (
    <figure>
      <svg viewBox={`0 0 100 ${h}`} preserveAspectRatio="none" className="h-44 w-full overflow-visible" role="img" aria-label={series.map((s) => t(s.label)).join(", ")}>
        <line x1="0" x2="100" y1={zero} y2={zero} className="stroke-line" strokeWidth="0.4" vectorEffect="non-scaling-stroke" />
        {months.map((m, i) =>
          series.map((s, si) => {
            const v = s.values[i] ?? 0;
            const height = scale(v);
            return (
              <rect key={`${m}-${si}`} x={i * slot + slot * 0.1 + si * bar} y={v >= 0 ? zero - height : zero} width={bar * 0.92} height={Math.max(0.5, height)} rx="0.6" className={s.className}>
                <title>{`${monthShort(m)} · ${t(s.label)}: ${full(v)}`}</title>
              </rect>
            );
          }),
        )}
        {points && <polyline points={points} fill="none" className="stroke-accent" strokeWidth="2" vectorEffect="non-scaling-stroke" />}
      </svg>
      <div className="mt-1 grid text-center text-[10px] text-ink-3" style={{ gridTemplateColumns: `repeat(${months.length}, minmax(0, 1fr))` }}>
        {months.map((m, i) => (
          <span key={m} className={months.length > 8 && i % 2 === 1 ? "max-sm:invisible" : ""}>
            {monthShort(m)}
          </span>
        ))}
      </div>
      <figcaption className="mt-2 flex flex-wrap gap-3 text-xs text-ink-2">
        {series.map((s) => (
          <span key={s.label} className="inline-flex items-center gap-1.5">
            <svg aria-hidden viewBox="0 0 10 10" className="size-2.5">
              <rect width="10" height="10" rx="2" className={s.className} />
            </svg>
            {t(s.label)}
          </span>
        ))}
        {line && (
          <span className="inline-flex items-center gap-1.5">
            <span aria-hidden className="h-0.5 w-3 rounded bg-accent" />
            {t(line.label)}
          </span>
        )}
      </figcaption>
    </figure>
  );
}

/** Горизонтальные полосы долей: «кто больше всех» (контрагенты, статьи расходов, склады). */
export function ShareBars({ rows, href }: { rows: { key: string; title: string; amount: number; note?: string }[]; href?: (key: string) => string | null }) {
  const t = useT();
  const max = Math.max(1, ...rows.map((r) => Math.abs(r.amount)));
  if (rows.length === 0) return <p className="text-sm text-ink-3">{t("Нет данных за период.")}</p>;
  return (
    <ul className="space-y-2">
      {rows.map((r) => {
        const link = href?.(r.key);
        const title = link ? (
          <Link href={link} className="hover:text-accent-strong hover:underline">
            {r.title}
          </Link>
        ) : (
          r.title
        );
        return (
          <li key={r.key} className="text-sm">
            <div className="flex items-baseline justify-between gap-3">
              <span className="min-w-0 truncate text-ink">{title}</span>
              <span className="shrink-0 tabular-nums text-ink" title={full(r.amount)}>
                {money(r.amount)}
                {r.note && <span className="ml-2 text-xs text-ink-3">{r.note}</span>}
              </span>
            </div>
            <div className="mt-1 h-1.5 rounded-full bg-muted">
              <div className={`h-1.5 rounded-full ${r.amount < 0 ? "bg-bad" : "bg-accent"}`} style={{ width: `${Math.max(2, (Math.abs(r.amount) / max) * 100)}%` }} />
            </div>
          </li>
        );
      })}
    </ul>
  );
}

/** Отбор раздела: организация 1С и период (быстрые периоды и свои даты). Меняет адрес страницы — данные считает сервер. */
export function FinanceFilters({ organizations, from, to, organization, single = false }: { organizations: FinanceOrganization[]; from: string; to: string; organization?: string; single?: boolean }) {
  const t = useT();
  const [custom, setCustom] = useState({ from, to });
  const presets = periodPresets();
  const go = useGo();
  const chip = (active: boolean) =>
    `h-8 rounded-full border px-3 text-xs font-medium whitespace-nowrap ${active ? "border-accent bg-accent-soft text-accent-strong" : "border-line bg-surface text-ink-2 hover:bg-muted"}`;
  return (
    <div className="flex flex-wrap items-center gap-2">
      <select
        aria-label={t("Организация")}
        value={organization ?? ""}
        onChange={(e) => go({ organization: e.target.value || null })}
        className="h-8 max-w-full rounded-full border border-line bg-surface px-3 text-xs font-medium text-ink"
      >
        <option value="">{t("Все организации")}</option>
        {organizations.map((o) => (
          <option key={o.id} value={o.id}>
            {o.name}
          </option>
        ))}
      </select>
      {single ? (
        <label className="flex items-center gap-1.5 text-xs text-ink-2">
          {t("на")}
          <input type="date" value={custom.to} onChange={(e) => setCustom({ ...custom, to: e.target.value })} onBlur={() => custom.to !== to && go({ to: custom.to, from: null })} className="h-8 rounded-full border border-line bg-surface px-3 text-xs" />
        </label>
      ) : (
        <>
          <div className="flex gap-1 overflow-x-auto">
            {presets.map((p) => (
              <button key={p.key} type="button" className={chip(p.from === from && p.to === to)} onClick={() => go({ from: p.from, to: p.to })}>
                {t(p.label)}
              </button>
            ))}
          </div>
          <div className="flex items-center gap-1 text-xs text-ink-2">
            <input aria-label={t("С")} type="date" value={custom.from} onChange={(e) => setCustom({ ...custom, from: e.target.value })} className="h-8 rounded-full border border-line bg-surface px-2 text-xs" />
            <span>—</span>
            <input aria-label={t("По")} type="date" value={custom.to} onChange={(e) => setCustom({ ...custom, to: e.target.value })} className="h-8 rounded-full border border-line bg-surface px-2 text-xs" />
            <button type="button" className={chip(false)} disabled={!custom.from || !custom.to || custom.from > custom.to} onClick={() => go({ from: custom.from, to: custom.to })}>
              {t("Показать")}
            </button>
          </div>
        </>
      )}
      <DimensionPicker />
    </div>
  );
}

/** Таблица с горизонтальной прокруткой на телефоне. */
export function TableBox({ children }: { children: React.ReactNode }) {
  return <div className="-mx-4 overflow-x-auto px-4 sm:mx-0 sm:px-0">{children}</div>;
}

export const th = "whitespace-nowrap border-b border-line px-2 py-2 text-left text-xs font-semibold text-ink-3";
export const td = "border-b border-line/60 px-2 py-2 align-top";
export const num = "whitespace-nowrap text-right tabular-nums";
