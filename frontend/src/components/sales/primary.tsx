"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { AkbChart } from "./AkbChart";
import { ExecutionBar, KpiTile, Note, Section, execClass } from "./bits";
import { CopyButton, DataTable, NameCell, type Column } from "./DataTable";
import { date, kg, money, monthGenitive, monthName, monthShort, num, ordersLabel, pct, plural, rowsLabel, tsvValue } from "@/lib/sales/format";
import type { PrimaryAmounts, PrimaryGroup, PrimaryRow, PrimaryView } from "@/lib/sales/types";

// «Первичка → Республика» как в «Полевом контроле»: плитки месяца, столбики по месяцам, план и факт по категориям, дилерам и прямым
// клиентам завода, календарь отгрузок, «С начала года», клиенты по месяцам и товары. Все суммы, доли, выполнение и итоги — с сервера:
// компоненты только форматируют. Переключатель единиц меняет графики и таблицы сразу; план и факт по категориям, дилерам и прямым
// клиентам — всегда в кг (DOC-filters §3).

type Unit = keyof PrimaryAmounts;

type Option<T extends string> = { key: T; label: string; title?: string };

const units: Option<Unit>[] = [
  { key: "kg", label: "кг" },
  { key: "boxes", label: "коробки" },
  { key: "sumFactory", label: "сум (цена дилера)", title: "Сумма (цена дилера): сумма перемещения или заказа — по этой цене дилер берёт товар у завода" },
  { key: "sumDealer", label: "сум (продажа дилера)", title: "Сумма (цена продажи дилера): количество × цена прайса «Дилердан чикиш нарх»" },
];

const exportUnits: Option<Unit>[] = [
  { key: "kg", label: "кг" },
  { key: "sumFactory", label: "сум", title: "Сумма заказа в сумах" },
];

/** Параметры календаря отгрузок в адресе: период «с … по …», день и контрагент разбора. */
const calendarKeys = ["from", "to", "day", "dealer"] as const;
type CalendarParams = Partial<Record<(typeof calendarKeys)[number], string | number | null>>;

const fmt = (value: number | null | undefined, unit: Unit) => (unit === "kg" ? kg(value) : unit === "boxes" ? num(value) : money(value));
const tons = (value: number | null | undefined) => (value == null ? "—" : num(value / 1000, 1));
const barTone = (v: number | null) => (v == null ? "accent" : v < 0.5 ? "bad" : v < 0.9 ? "warn" : "accent");

function Switch<T extends string>({ value, onChange, options, label }: { value: T; onChange: (v: T) => void; options: Option<T>[]; label: string }) {
  return (
    <div className="inline-flex overflow-hidden rounded-full border border-line bg-surface text-xs shadow-sm" role="group" aria-label={label}>
      {options.map((o) => (
        <button
          key={o.key}
          type="button"
          onClick={() => onChange(o.key)}
          aria-pressed={value === o.key}
          title={o.title}
          className={`px-3 py-1.5 font-medium ${value === o.key ? "bg-accent text-white" : "text-ink-2 hover:bg-muted"}`}
        >
          {o.label}
        </button>
      ))}
    </div>
  );
}

function MonthTiles({ data }: { data: PrimaryView }) {
  const plan = data.planMonthKg;
  const fact = data.monthTotal.kg;
  const exec = data.monthExecution;
  const noPlan = data.hasPlan ? "плана первички на этот месяц нет" : "план первички не загружен";
  return (
    <div className="grid grid-cols-2 gap-3 lg:grid-cols-3 min-[87.5rem]:grid-cols-5">
      <KpiTile label="План месяца" value={plan != null ? kg(plan) : "—"} unit={plan != null ? "кг" : undefined}>
        {plan != null ? `отгрузка за ${monthName(data.month)}` : noPlan}
      </KpiTile>
      <KpiTile label="Отгружено" value={kg(fact)} unit="кг">
        {num(data.monthTotal.boxes)} коробок · {money(data.monthTotal.sumFactory)}
      </KpiTile>
      <KpiTile label="Выполнение" value={pct(exec, 1)} tone={exec == null ? "muted" : barTone(exec)}>
        {plan != null ? (
          <>
            {kg(fact)} из {kg(plan)} кг
            <div className="mt-1.5">
              <ExecutionBar value={exec} tone={barTone(exec)} />
            </div>
          </>
        ) : (
          noPlan
        )}
      </KpiTile>
      <KpiTile label="Осталось" value={plan != null ? kg(data.monthRemainingKg) : "—"} unit={plan != null ? "кг" : undefined}>
        {plan == null
          ? noPlan
          : data.monthOverPlanKg
            ? `план выполнен, сверх него ${kg(data.monthOverPlanKg)} кг`
            : data.monthRemainingKg === 0
              ? "план выполнен"
              : "до плана месяца"}
      </KpiTile>
      <KpiTile label="Прогноз" value={kg(data.forecastKg ?? fact)} unit="кг">
        {data.forecastKg != null
          ? `${data.forecastExecution != null ? `${pct(data.forecastExecution)} плана · ` : ""}темп по ${data.workedDays}-е число`
          : data.dataThrough
            ? "месяц закрыт — это факт"
            : "отгрузок в месяце нет"}
      </KpiTile>
    </div>
  );
}

/** Столбики по месяцам: план — светлая мишень во всю высоту плана, факт — заливка внутри. Клик — открыть месяц. */
function MonthsChart({ data, unit }: { data: PrimaryView; unit: Unit }) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const last = Math.max(data.month, ...data.monthsWithData, 1);
  const months = Array.from({ length: last }, (_, i) => i + 1);
  const planOn = unit === "kg" && data.planMonths.some((p) => p != null);
  const values = months.map((m) => data.months[m - 1][unit] ?? 0);
  const plans = months.map((m) => (planOn ? data.planMonths[m - 1] : null));
  const max = Math.max(1, ...values, ...plans.map((p) => p ?? 0));

  const open = (m: number) => {
    const next = new URLSearchParams(params);
    next.set("year", String(data.year));
    next.set("month", String(m));
    // Другой месяц — календарь отгрузок с начала: период и разбор дня сбрасываются.
    for (const key of calendarKeys) next.delete(key);
    router.push(`${pathname}?${next}`);
  };

  return (
    <Section title="По месяцам" hint={`${data.year} · клик — открыть месяц${data.runningMonth === last ? " · последний месяц ещё идёт" : ""}`}>
      <div className="flex h-[260px] items-end gap-2 sm:gap-4">
        {months.map((m, i) => {
          const v = values[i];
          const plan = plans[i];
          const exec = planOn ? data.monthExecutions[m - 1] : null;
          const running = data.runningMonth === m;
          const selected = m === data.month;
          return (
            <button key={m} type="button" onClick={() => open(m)} className="group flex h-full min-w-0 flex-1 flex-col items-center justify-end" title={`${monthName(m)}: ${fmt(v, unit)}${plan ? ` из ${kg(plan)} кг плана` : ""}`}>
              <span className="mb-1 whitespace-nowrap text-[11px] font-semibold tabular-nums text-ink-2">{v ? fmt(v, unit) : ""}</span>
              <div className="relative w-full max-w-[56px]" style={{ height: `${(Math.max(v, plan ?? 0) / max) * 190}px` }}>
                {plan != null && <div className="absolute inset-x-0 bottom-0 rounded-t bg-accent/15" style={{ height: `${(plan / Math.max(v, plan)) * 100}%` }} />}
                <div
                  className={`absolute inset-x-1 bottom-0 rounded-t ${running ? "border border-accent" : "bg-accent group-hover:bg-accent-strong"}`}
                  style={{
                    height: `${(v / Math.max(v, plan ?? 0, 1)) * 100}%`,
                    backgroundImage: running ? "repeating-linear-gradient(-45deg, var(--color-accent) 0 4px, transparent 4px 8px)" : undefined,
                  }}
                />
              </div>
              <span className={`mt-2 whitespace-nowrap text-xs ${selected ? "font-semibold text-accent-strong underline underline-offset-4" : "text-ink-2"}`}>
                {monthShort(m)}
                {running ? " (идёт)" : ""}
              </span>
              <span className={`text-[11px] tabular-nums ${execClass(exec)}`}>{exec != null ? pct(exec) : " "}</span>
            </button>
          );
        })}
      </div>
      <div className="mt-3 flex flex-wrap gap-4 text-xs text-ink-2">
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm bg-accent" />
          Факт
        </span>
        {planOn && (
          <span className="inline-flex items-center gap-1.5">
            <span className="size-2.5 rounded-sm bg-accent/15" />
            План
          </span>
        )}
        <span className="inline-flex items-center gap-1.5">
          <span className="size-2.5 rounded-sm border border-accent" style={{ backgroundImage: "repeating-linear-gradient(-45deg, var(--color-accent) 0 2px, transparent 2px 4px)" }} />
          месяц идёт
        </span>
      </div>
    </Section>
  );
}

function TotalsFooter({ cells }: { cells: { value: React.ReactNode; right?: boolean }[] }) {
  return (
    <tfoot>
      <tr className="bg-muted font-semibold text-ink">
        {cells.map((c, i) => (
          <td key={i} className={`px-3 py-2.5 ${c.right ? "text-right tabular-nums" : ""}`}>
            {c.value}
          </td>
        ))}
      </tr>
    </tfoot>
  );
}

/**
 * План и факт за выбранный месяц: категории, дилеры или прямые клиенты — всегда в килограммах, переключатель единиц сюда не действует
 * (DOC-filters §3): факт кг, план, выполнение, «осталось»; без плана — факт и доля в весе месяца. Строка с планом остаётся и без факта.
 * total — итог таблицы с сервера.
 */
function PlanFactTable({ title, hint, nameLabel, rows, total, data }: { title: string; hint?: string; nameLabel: string; rows: PrimaryRow[]; total: PrimaryRow; data: PrimaryView }) {
  const unit: Unit = "kg";
  const planOn = total.planMonthKg != null;
  const shown = rows.filter((r) => r.month[unit] !== 0 || (planOn && r.planMonthKg != null));

  const columns: Column<PrimaryRow>[] = [
    { key: "name", label: nameLabel, value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={r.sub} /> },
    ...(planOn ? [{ key: "plan", label: "План, кг", align: "right" as const, value: (r: PrimaryRow) => r.planMonthKg, render: (r: PrimaryRow) => kg(r.planMonthKg) }] : []),
    { key: "fact", label: "Факт, кг", align: "right", value: (r) => r.month[unit], render: (r) => <span className="font-semibold">{fmt(r.month[unit], unit)}</span> },
    ...(planOn
      ? [
          {
            key: "exec",
            label: "Выполнение",
            align: "right" as const,
            value: (r: PrimaryRow) => r.monthExecution,
            render: (r: PrimaryRow) => (
              <span className="inline-flex items-center justify-end gap-3">
                <span className={`w-12 tabular-nums ${execClass(r.monthExecution)}`}>{pct(r.monthExecution)}</span>
                <span className="w-28">
                  <ExecutionBar value={r.monthExecution} tone={barTone(r.monthExecution)} />
                </span>
              </span>
            ),
          },
          {
            key: "left",
            label: "Осталось",
            align: "right" as const,
            value: (r: PrimaryRow) => r.monthRemainingKg,
            render: (r: PrimaryRow) =>
              r.monthRemainingKg == null ? "—" : r.monthRemainingKg === 0 ? <span className="text-ok">выполнен</span> : <span className="text-bad">{kg(r.monthRemainingKg)}</span>,
          },
        ]
      : [{ key: "share", label: "Доля", align: "right" as const, value: (r: PrimaryRow) => r.monthShare[unit], render: (r: PrimaryRow) => pct(r.monthShare[unit], 1) }]),
  ];

  return (
    <DataTable
      title={title}
      hint={hint ?? `за ${monthName(data.month)}, в килограммах`}
      columns={columns}
      rows={shown}
      rowKey={(r) => r.id}
      empty="В этом месяце отгрузок нет"
      footer={
        shown.length > 0 && (
          <TotalsFooter
            cells={[
              { value: "Итого" },
              ...(planOn ? [{ value: kg(total.planMonthKg), right: true }] : []),
              { value: fmt(total.month[unit], unit), right: true },
              ...(planOn
                ? [
                    { value: <span className={execClass(total.monthExecution)}>{pct(total.monthExecution)}</span>, right: true },
                    { value: kg(total.monthRemainingKg), right: true },
                  ]
                : [{ value: pct(total.monthShare[unit], 1), right: true }]),
            ]}
          />
        )
      }
    />
  );
}

/**
 * Календарь отгрузок: контрагент × день за период «с … по …» (дни с отгрузкой), итоги строк и дней, «Дней» — с сервера. Клик по клетке —
 * что отгрузили в этот день; по дню — всем; по контрагенту — за период. Период и разбор — параметры адреса (from, to, day, dealer).
 */
function ShipmentCalendar({
  data,
  unit,
  nameLabel = "Контрагент",
  allLabel = "все контрагенты",
  dealerSum = true,
  boxes = true,
}: {
  data: PrimaryView;
  unit: Unit;
  nameLabel?: string;
  allLabel?: string;
  /** Колонка «По цене продажи дилера» в разборе (у экспорта её нет). */
  dealerSum?: boolean;
  /** Колонка «Коробок» в разборе (у экспорта коробок нет). */
  boxes?: boolean;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const cal = data.calendar;
  if (!cal || cal.monthDays.length === 0) return null;

  const go = (changes: CalendarParams) => {
    const next = new URLSearchParams(params);
    for (const [key, value] of Object.entries(changes)) {
      if (value == null || value === "") next.delete(key);
      else next.set(key, String(value));
    }
    router.push(`${pathname}?${next}`, { scroll: false });
  };
  const pick = (dealer: string | null, day: number | null) => (cal.dealer === dealer && cal.day === day ? go({ dealer: null, day: null }) : go({ dealer, day }));

  const mon = monthShort(data.month).toLowerCase();
  const isRange = cal.days.length !== cal.monthDays.length;
  // В списках — дни с отгрузкой; день из адреса без отгрузок тоже остаётся видимым.
  const withDay = (days: number[], day: number | null) => (day == null || days.includes(day) ? days : [...days, day].sort((a, b) => a - b));
  const partyName = (id: string | null) => (id ? (cal.rows.find((r) => r.id === id)?.name ?? data.dealers.find((d) => d.id === id)?.name ?? id) : allLabel);
  const select = "h-8 rounded-lg border border-line bg-surface px-2 text-xs text-ink";
  const bound = (label: string, key: "from" | "to", value: number | null) => (
    <label className="flex items-center gap-1.5 text-xs text-ink-3">
      {label}
      <select
        value={value ?? ""}
        onChange={(e) => go(key === "from" ? { from: e.target.value } : { to: e.target.value })}
        aria-label={key === "from" ? "Период с какого дня" : "Период по какой день"}
        className={select}
      >
        {withDay(cal.monthDays, value).map((d) => (
          <option key={d} value={d}>
            {d} {mon}
          </option>
        ))}
      </select>
    </label>
  );
  const copy = () =>
    [
      [nameLabel, ...cal.days.map(String), "Итого", "Дней"].join("\t"),
      ...cal.rows.map((r) => [r.name, ...r.cells.map((c) => tsvValue(c?.[unit] ?? "")), tsvValue(r.total[unit]), String(r.days)].join("\t")),
      ["Итого", ...cal.dayTotals.map((t) => tsvValue(t[unit])), tsvValue(cal.total[unit]), String(cal.days.length)].join("\t"),
    ].join("\n");
  const th = "px-2 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const pickCls = (on: boolean) => (on ? "bg-accent-soft font-semibold text-ink" : "");
  const period = cal.day ? `${cal.day} ${monthGenitive(data.month)}` : isRange ? `${cal.from}–${cal.to} ${monthGenitive(data.month)}` : `весь ${monthName(data.month)}`;

  return (
    <Section
      title="Календарь отгрузок"
      hint="клик по клетке — что отгрузили в этот день"
      actions={
        <>
          {bound("с", "from", cal.from)}
          {bound("по", "to", cal.to)}
          {isRange && (
            <button type="button" onClick={() => go({ from: null, to: null })} className="h-8 rounded-lg border border-line bg-surface px-2.5 text-xs font-medium text-ink hover:bg-muted">
              все дни
            </button>
          )}
          <select value={cal.day ?? ""} onChange={(e) => go({ day: e.target.value || null, dealer: null })} aria-label="День разбора" className={select}>
            <option value="">все дни</option>
            {withDay(cal.days, cal.day).map((d) => (
              <option key={d} value={d}>
                {d} {mon}
              </option>
            ))}
          </select>
          <CopyButton onCopy={copy} />
        </>
      }
    >
      <div className="-mx-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
        <table className="w-full min-w-max border-collapse text-xs">
          <thead>
            <tr className="border-b border-line">
              <th className="sticky left-0 z-10 bg-surface px-2 py-2 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3">{nameLabel}</th>
              {cal.days.map((d) => (
                <th key={d} className={th}>
                  <button type="button" onClick={() => pick(null, d)} className={`hover:text-accent-strong ${cal.day === d && cal.dealer == null ? "text-accent-strong" : ""}`}>
                    {d}
                  </button>
                </th>
              ))}
              <th className={th}>
                Итого
                {isRange && (
                  <div className="font-normal normal-case tracking-normal">
                    {cal.from}–{cal.to} {mon}
                  </div>
                )}
              </th>
              <th className={th}>Дней</th>
            </tr>
          </thead>
          <tbody>
            {cal.rows.map((r) => (
              <tr key={r.id} className="border-b border-line">
                <td className={`sticky left-0 z-10 bg-surface px-2 py-1.5 ${pickCls(cal.dealer === r.id && cal.day == null)}`}>
                  <button type="button" onClick={() => pick(r.id, null)} className="text-left font-medium text-ink hover:text-accent-strong" title={r.sub ?? undefined}>
                    {r.name}
                  </button>
                </td>
                {r.cells.map((c, i) => {
                  const d = cal.days[i];
                  return (
                    <td key={d} className={`px-1 py-1 text-right tabular-nums ${pickCls(cal.day === d && (cal.dealer == null || cal.dealer === r.id))}`}>
                      {c ? (
                        <button type="button" onClick={() => pick(r.id, d)} className="w-full rounded px-1 py-0.5 text-right text-ink hover:bg-muted">
                          {fmt(c[unit], unit)}
                        </button>
                      ) : (
                        <span className="px-1 text-ink-3">·</span>
                      )}
                    </td>
                  );
                })}
                <td className="px-2 py-1.5 text-right font-semibold tabular-nums text-ink">{fmt(r.total[unit], unit)}</td>
                <td className="px-2 py-1.5 text-right tabular-nums text-ink-2">{num(r.days)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="bg-muted/60 font-semibold">
              <td className="sticky left-0 z-10 bg-muted px-2 py-1.5 text-ink">Итого</td>
              {cal.dayTotals.map((t, i) => (
                <td key={cal.days[i]} className="px-2 py-1.5 text-right tabular-nums text-ink">
                  {fmt(t[unit], unit)}
                </td>
              ))}
              <td className="px-2 py-1.5 text-right tabular-nums text-ink">{fmt(cal.total[unit], unit)}</td>
              <td className="px-2 py-1.5 text-right tabular-nums text-ink">{num(cal.days.length)}</td>
            </tr>
          </tfoot>
        </table>
        {cal.days.length === 0 && <p className="py-3 text-center text-sm text-ink-3">В эти дни отгрузок не было</p>}
      </div>

      {cal.detail && (
        <div className="mt-4 rounded-lg border border-line bg-muted/40 p-3">
          <div className="mb-2 flex flex-wrap items-center justify-between gap-2 text-sm">
            <span className="font-semibold text-ink">
              {partyName(cal.dealer)} · {period}
              <span className="ml-2 font-normal text-ink-3">{rowsLabel(cal.detail.length)}</span>
            </span>
            <button type="button" onClick={() => go({ day: null, dealer: null })} className="rounded-lg border border-line bg-surface px-2.5 py-1 text-xs font-medium text-ink hover:bg-muted">
              Сбросить
            </button>
          </div>
          {cal.detail.length === 0 ? (
            <p className="text-sm text-ink-3">Отгрузок нет.</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-line text-[11px] uppercase tracking-wide text-ink-3">
                    {cal.dealer == null && <th className="py-1.5 pr-3 text-left font-semibold">{nameLabel}</th>}
                    <th className="py-1.5 text-left font-semibold">Товар</th>
                    <th className="py-1.5 pl-3 text-left font-semibold">Категория</th>
                    {boxes && <th className="py-1.5 text-right font-semibold">Коробок</th>}
                    <th className="py-1.5 text-right font-semibold">Кг</th>
                    <th className="py-1.5 text-right font-semibold">{dealerSum ? "Сумма (цена дилера)" : "Сумма"}</th>
                    {dealerSum && <th className="py-1.5 text-right font-semibold">Сумма (цена продажи дилера)</th>}
                  </tr>
                </thead>
                <tbody>
                  {cal.detail.map((d) => (
                    <tr key={`${d.dealerId}|${d.productId}|${d.isReturn}`} className="border-b border-line last:border-0">
                      {cal.dealer == null && <td className="py-1.5 pr-3 font-medium text-ink">{d.dealerName}</td>}
                      <td className="max-w-[420px] py-1.5 pr-3 text-ink">
                        {d.name}
                        {(d.code || d.isReturn) && (
                          <div className="text-[11.5px] text-ink-3">
                            {d.code && `Артикул ${d.code}`}
                            {d.isReturn && <span className="text-bad">{d.code ? " · " : ""}возврат</span>}
                          </div>
                        )}
                      </td>
                      <td className="py-1.5 pl-3 text-ink-2">{d.category}</td>
                      {boxes && <td className="py-1.5 text-right tabular-nums text-ink-2">{d.amounts.boxes ? num(d.amounts.boxes) : "—"}</td>}
                      <td className="py-1.5 text-right tabular-nums text-ink">{kg(d.amounts.kg)}</td>
                      <td className="py-1.5 text-right tabular-nums text-ink-2">{money(d.amounts.sumFactory)}</td>
                      {dealerSum && <td className="py-1.5 text-right tabular-nums text-ink-2">{money(d.amounts.sumDealer)}</td>}
                    </tr>
                  ))}
                </tbody>
                {cal.detailTotal && (
                  <tfoot>
                    <tr className="font-semibold text-ink">
                      <td className="pt-2" colSpan={cal.dealer == null ? 3 : 2}>
                        Итого
                      </td>
                      {boxes && <td className="pt-2 text-right tabular-nums">{num(cal.detailTotal.boxes)}</td>}
                      <td className="pt-2 text-right tabular-nums">{kg(cal.detailTotal.kg)}</td>
                      <td className="pt-2 text-right tabular-nums">{money(cal.detailTotal.sumFactory)}</td>
                      {dealerSum && <td className="pt-2 text-right tabular-nums">{money(cal.detailTotal.sumDealer)}</td>}
                    </tr>
                  </tfoot>
                )}
              </table>
            </div>
          )}
        </div>
      )}
    </Section>
  );
}

function YtdTiles({ data }: { data: PrimaryView }) {
  const y = data.ytd;
  const exec = data.ytdExecution;
  return (
    <div className="grid grid-cols-2 gap-3 lg:grid-cols-3 min-[87.5rem]:grid-cols-5">
      <KpiTile label="Отгружено" value={tons(y.kg)} unit="т">
        {num(data.ytdArticles)} артикулов · нетто
      </KpiTile>
      <KpiTile label="Сумма (цена дилера)" value={money(y.sumFactory)}>
        {data.ytdPricePerKg != null ? `${money(data.ytdPricePerKg)} за кг` : "—"}
      </KpiTile>
      <KpiTile label="По цене продажи дилера" value={money(y.sumDealer)}>
        {data.ytdMarkup != null ? `наценка дилера ${money(data.ytdMarkupSum)} · ${pct(data.ytdMarkup, 1)}` : "—"}
      </KpiTile>
      <KpiTile label="Возвраты" value={tons(data.ytdReturnsKg)} unit="т">
        {rowsLabel(data.ytdReturnLines)} · {pct(data.ytdReturnsShare, 1)} от отгруженного
      </KpiTile>
      <KpiTile label="Выполнение" value={pct(exec, 1)} tone={exec == null ? "muted" : barTone(exec)}>
        {data.planYtdKg != null ? `план ${tons(data.planYtdKg)} т` : data.hasPlan ? "плана на эти месяцы нет" : "план первички не загружен"}
      </KpiTile>
    </div>
  );
}

/** С начала года по месяцам (по выбранный): доля — от итога с начала года; план и выполнение — только в кг. total — итог таблицы с сервера. */
function YearMatrix({ title, hint, nameLabel, rows, total, data, unit }: { title: string; hint: string; nameLabel: string; rows: PrimaryRow[]; total: PrimaryRow; data: PrimaryView; unit: Unit }) {
  const months = Array.from({ length: data.month }, (_, i) => i + 1);
  const planOn = unit === "kg" && total.planYtdKg != null;
  const shown = rows.filter((r) => r.ytd[unit] !== 0 || (planOn && r.planYtdKg != null));

  const columns: Column<PrimaryRow>[] = [
    { key: "name", label: nameLabel, value: (r) => r.name, render: (r) => <NameCell name={r.name} sub={r.sub} /> },
    ...months.map((m) => ({
      key: `m${m}`,
      label: `${monthShort(m)}${data.runningMonth === m ? " (идёт)" : ""}`,
      align: "right" as const,
      value: (r: PrimaryRow) => r.months[m - 1][unit],
      render: (r: PrimaryRow) => <span className={r.months[m - 1][unit] ? "text-ink" : "text-ink-3"}>{fmt(r.months[m - 1][unit], unit)}</span>,
    })),
    { key: "total", label: "Итого", align: "right", value: (r) => r.ytd[unit], render: (r) => <span className="font-semibold">{fmt(r.ytd[unit], unit)}</span> },
    { key: "share", label: "Доля", align: "right", value: (r) => r.ytdShare[unit], render: (r) => pct(r.ytdShare[unit], 1) },
    ...(planOn
      ? [
          { key: "plan", label: "План", align: "right" as const, value: (r: PrimaryRow) => r.planYtdKg, render: (r: PrimaryRow) => kg(r.planYtdKg) },
          {
            key: "exec",
            label: "Выполнение",
            align: "right" as const,
            value: (r: PrimaryRow) => r.ytdExecution,
            render: (r: PrimaryRow) => <span className={execClass(r.ytdExecution)}>{pct(r.ytdExecution)}</span>,
          },
        ]
      : []),
  ];

  return (
    <DataTable
      title={title}
      hint={hint}
      columns={columns}
      rows={shown}
      rowKey={(r) => r.id}
      footer={
        shown.length > 0 && (
          <TotalsFooter
            cells={[
              { value: "Итого" },
              ...months.map((m) => ({ value: fmt(total.months[m - 1][unit], unit), right: true })),
              { value: fmt(total.ytd[unit], unit), right: true },
              { value: pct(total.ytdShare[unit], 1), right: true },
              ...(planOn
                ? [
                    { value: kg(total.planYtdKg), right: true },
                    { value: <span className={execClass(total.ytdExecution)}>{pct(total.ytdExecution)}</span>, right: true },
                  ]
                : []),
            ]}
          />
        )
      }
    />
  );
}

function ItemsTable({ data, dealer = true, boxes = true }: { data: PrimaryView; dealer?: boolean; boxes?: boolean }) {
  type Row = PrimaryView["items"][number];
  const columns: Column<Row>[] = [
    {
      key: "name",
      label: "Наименование",
      value: (r) => r.name,
      render: (r) => (
        <span className="block max-w-[360px] whitespace-normal">
          <NameCell name={r.name} sub={r.code ? `Артикул ${r.code}` : null} />
        </span>
      ),
    },
    { key: "cat", label: "Категория", value: (r) => r.category },
    ...(boxes
      ? [
          {
            key: "boxes",
            label: "Коробок",
            align: "right" as const,
            value: (r: Row) => r.ytd.boxes,
            render: (r: Row) => (r.ytd.boxes ? <span title={r.boxesKnown ? undefined : "часть строк без известной фасовки"}>{num(r.ytd.boxes)}{r.boxesKnown ? "" : " ≈"}</span> : "—"),
          },
        ]
      : []),
    { key: "kg", label: "Вес, кг", align: "right", value: (r) => r.ytd.kg, render: (r) => <span className="font-semibold">{kg(r.ytd.kg)}</span> },
    { key: "sf", label: dealer ? "Сумма (цена дилера)" : "Сумма", align: "right", value: (r) => r.ytd.sumFactory, render: (r) => money(r.ytd.sumFactory) },
    ...(dealer ? [{ key: "sd", label: "По цене продажи дилера", align: "right" as const, value: (r: Row) => r.ytd.sumDealer, render: (r: Row) => money(r.ytd.sumDealer) }] : []),
    { key: "perKg", label: dealer ? "Цена дилера за кг" : "Цена за кг", align: "right", value: (r) => r.pricePerKg, render: (r) => money(r.pricePerKg) },
    ...(dealer ? [{ key: "margin", label: "Наценка дилера", align: "right" as const, value: (r: Row) => r.markup, render: (r: Row) => pct(r.markup, 1) }] : []),
  ];
  return (
    <DataTable
      title="Товары"
      hint={`${num(data.items.length)} артикулов с начала года (по ${monthName(data.month)}), по убыванию веса`}
      columns={columns}
      rows={data.items}
      rowKey={(r) => String(r.productId)}
      limit={50}
    />
  );
}

type ClientsMode = "akb" | "kg" | "sum";

const clientModes: Option<ClientsMode>[] = [
  { key: "akb", label: "АКБ" },
  { key: "kg", label: "кг" },
  { key: "sum", label: "сум" },
];

const clientView = {
  akb: { title: "Клиенты по месяцам", hint: "клиенты, получившие отгрузку за месяц", head: "Категория — клиентов", total: "Всего клиентов", format: num },
  kg: { title: "Отгрузка по месяцам, кг", hint: "вес нетто за месяц", head: "Категория — кг", total: "Итого, кг", format: kg },
  sum: { title: "Отгрузка по месяцам, сум", hint: "сумма нетто по цене дилера", head: "Категория — сум", total: "Итого, сум", format: money },
} satisfies Record<ClientsMode, { title: string; hint: string; head: string; total: string; format: (v: number | null | undefined) => string }>;

/** Клиенты по месяцам (DOC §9.3): АКБ / кг / сум — та же карточка, что «АКБ по месяцам»; числа считает сервер. */
function ClientsChart({ clients }: { clients: NonNullable<PrimaryView["clients"]> }) {
  const [mode, setMode] = useState<ClientsMode>("akb");
  const view = clientView[mode];
  return (
    <AkbChart
      data={clients[mode]}
      title={view.title}
      hint={view.hint}
      head={view.head}
      totalLabel={view.total}
      format={view.format}
      actions={<Switch value={mode} onChange={setMode} options={clientModes} label="Мера" />}
      note={
        <>
          Клиент — контрагент первички (дилер или прямой клиент завода); за месяц он считается один раз, если нетто его отгрузок (отгрузка минус
          возвраты) больше нуля; в строке категории — если нетто по категории больше нуля. Итог — не сумма категорий. Кг — нетто, сум — по цене
          дилера.{" "}
        </>
      }
      averageNote="«Сред/мес» — среднее за закрытые месяцы с отгрузками, считает сервер: идущий месяц не в счёт, неполный месяц занижал бы его."
    />
  );
}

/** Строки контрагентов группы и её итог (дилеры или прямые клиенты завода). */
function groupOf(data: PrimaryView, group: PrimaryGroup) {
  const total = data.dealerGroups.find((g) => g.group === group);
  return total ? { rows: data.dealers.filter((r) => r.group === group), total } : null;
}

/** Страница «Первичка → Республика». */
export function PrimaryRepublic({ data }: { data: PrimaryView }) {
  const [unit, setUnit] = useState<Unit>("kg");
  const dealers = groupOf(data, "dealer");
  const direct = groupOf(data, "direct");
  const directHint = "базары, сети, фирменный магазин — заказы «Завода» и «К К Мерч»";

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center justify-end gap-3">
        <Switch value={unit} onChange={setUnit} options={units} label="Единицы" />
      </div>
      <MonthTiles data={data} />
      <MonthsChart data={data} unit={unit} />
      <PlanFactTable title="План и факт по категориям" nameLabel="Категория" rows={data.categories} total={data.categoryTotal} data={data} />
      {dealers && <PlanFactTable title="План и факт по дилерам" nameLabel="Дилер" rows={dealers.rows} total={dealers.total} data={data} />}
      {direct && (
        <PlanFactTable
          title="Прямые клиенты завода"
          hint={`за ${monthName(data.month)}, в килограммах · ${directHint}`}
          nameLabel="Клиент"
          rows={direct.rows}
          total={direct.total}
          data={data}
        />
      )}
      <ShipmentCalendar key={`${data.year}-${data.month}`} data={data} unit={unit} />

      <h2 className="mt-8 flex flex-wrap items-baseline gap-x-3 text-lg font-semibold text-ink">
        С начала года
        <span className="text-xs font-normal uppercase tracking-[0.08em] text-ink-3">
          {data.ytdMonths} мес. · по {date(data.dataThrough)}
        </span>
      </h2>
      <div className="mt-3">
        <YtdTiles data={data} />
      </div>
      {data.clients && <ClientsChart clients={data.clients} />}
      <YearMatrix title="По категориям" hint="доля — от итога с начала года" nameLabel="Категория" rows={data.categories} total={data.categoryTotal} data={data} unit={unit} />
      {dealers && <YearMatrix title="По дилерам" hint="доля — от всей отгрузки с начала года" nameLabel="Дилер" rows={dealers.rows} total={dealers.total} data={data} unit={unit} />}
      {direct && <YearMatrix title="Прямые клиенты завода" hint={`доля — от всей отгрузки · ${directHint}`} nameLabel="Клиент" rows={direct.rows} total={direct.total} data={data} unit={unit} />}
      <ItemsTable data={data} />

      {data.notes?.map((n) => (
        <Note key={n}>{n}</Note>
      ))}
      {data.otherStocks && data.otherStocks.length > 0 && (
        <Note>
          Склады вне справочника регионов — не дилеры, в первичку и в число клиентов не входят:{" "}
          {data.otherStocks.map((s, i) => (
            <span key={s.name}>
              {i > 0 && ", "}«{s.name}» — {num(s.transfers)} {plural(s.transfers, ["перемещение", "перемещения", "перемещений"])}, {kg(s.kg)} кг
            </span>
          ))}{" "}
          за {data.year} год.
        </Note>
      )}
      <Note>
        Первичка — отгрузка завода контрагентам, это не продажи в торговые точки. Источники в Linko: перемещения со склада «{data.factoryStock ?? "Завод"}» на склады
        дилеров («отдано» или «принято», дата — доставка) и заказы прямых клиентов завода — базаров, сетей, фирменного магазина (филиалы «Завод» и «К К Мерч»,
        точки не экспортного типа, «доставлен» и «отдан», дата — создание заказа; без «Дегустатсия + Акция»). Склад «{data.exportStock ?? "Экспорт"}» — экспорт,
        сюда не входит. Возвраты — перемещения со складов дилеров на завод и возвраты по заказам — вычтены: все цифры нетто. Склады сводятся к регионам
        («Коканд бозор» — Коканд), дилер — склад с регионом из справочника, подпись строки — дилер региона из «Настроек продаж». Сумма (цена дилера) — сумма
        перемещения или заказа, по этой цене дилер берёт товар у завода; сумма (цена продажи дилера) — количество × цена прайса «{data.dealerPriceList ?? "Дилердан чикиш нарх"}».
        Цены завода в Linko нет, поэтому «суммы завода», как в таблице первички, здесь нет. План и факт по категориям, дилерам и прямым клиентам — всегда в кг.
        Коробки — только у товаров, где вес коробки из названия делится на вес единицы ({kg(data.boxesUnknownKg)} кг с начала года без коробок).
        План — загруженный в OneBase план вида «Первичка»{data.hasPlan ? "" : " (сейчас не загружен — первичка без плана)"}; строки позже отчётного дня (вчера,
        не позже синхронизации) в отчёт не входят; прогноз — по дате данных{data.asOf ? ` (${date(data.asOf)})` : ""}, а не по сегодняшнему дню.
      </Note>
    </>
  );
}

/** Страница «Первичка → Экспорт»: заказы филиала «Завод» экспортным точкам — по месяцам, странам и дням. Коробок у экспорта нет. */
export function PrimaryExport({ data }: { data: PrimaryView }) {
  const [unit, setUnit] = useState<Unit>("kg");
  const fact = data.monthTotal;

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center justify-end gap-3">
        <Switch value={unit} onChange={setUnit} options={exportUnits} label="Единицы" />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <KpiTile label="Отгружено" value={kg(fact.kg)} unit="кг">
          {money(fact.sumFactory)} · {ordersLabel(data.monthTransfers)} · {num(data.monthCounterparties)} стран
        </KpiTile>
        <KpiTile label="Прогноз" value={kg(data.forecastKg ?? fact.kg)} unit="кг">
          {data.forecastKg != null ? `темп по ${data.workedDays}-е число` : data.dataThrough ? "месяц закрыт — это факт" : "отгрузок в месяце нет"}
        </KpiTile>
      </div>
      <MonthsChart data={data} unit={unit} />
      <ShipmentCalendar key={`${data.year}-${data.month}`} data={data} unit={unit} nameLabel="Страна" allLabel="все страны" dealerSum={false} boxes={false} />

      <h2 className="mt-8 flex flex-wrap items-baseline gap-x-3 text-lg font-semibold text-ink">
        С начала года
        <span className="text-xs font-normal uppercase tracking-[0.08em] text-ink-3">
          {data.ytdMonths} мес. · по {date(data.dataThrough)}
        </span>
      </h2>
      <div className="mt-3 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile label="Отгружено" value={tons(data.ytd.kg)} unit="т">
          {num(data.ytdArticles)} артикулов
        </KpiTile>
        <KpiTile label="Сумма" value={money(data.ytd.sumFactory)}>
          {data.ytdPricePerKg != null ? `${money(data.ytdPricePerKg)} за кг` : "—"}
        </KpiTile>
        <KpiTile label="Стран" value={num(data.export.counterparties)}>
          {ordersLabel(data.export.transfers)} за {data.year} год
        </KpiTile>
        <KpiTile label="Возвраты" value={tons(data.ytdReturnsKg)} unit="т">
          {data.ytdReturnLines ? rowsLabel(data.ytdReturnLines) : "возвратов не было"}
        </KpiTile>
      </div>
      <YearMatrix title="По странам" hint="доля — от итога с начала года" nameLabel="Страна" rows={data.dealers} total={data.dealerTotal} data={data} unit={unit} />
      <YearMatrix title="По категориям" hint="доля — от итога с начала года" nameLabel="Категория" rows={data.categories} total={data.categoryTotal} data={data} unit={unit} />
      <ItemsTable data={data} dealer={false} boxes={false} />

      {data.notes?.map((n) => (
        <Note key={n}>{n}</Note>
      ))}
      <Note>
        Экспорт — заказы Linko филиала «Завод» торговым точкам с типом EXPORT (статус «доставлен», дата — приёмка), минус возвраты по строкам. Страна
        определяется по названию или адресу точки в Linko (например «Daler Tojikiston», «Adamium Armenia»); Россия — по точкам: «Россия (Дагестан)» и
        «Россия (Уфа)» («ООО ВЛАДКОН»), как в «Полевом контроле»; точка без страны в названии показывается своим названием. Сумма — сумма заказа в сумах.
        Коробок у экспорта нет: в строках заказа Linko только штуки и кг. Перемещения на склад «Экспорт» сюда не входят: это внутреннее движение склада, а не продажа.
      </Note>
    </>
  );
}
