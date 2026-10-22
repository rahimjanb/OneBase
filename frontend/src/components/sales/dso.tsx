"use client";

import Link from "next/link";
import { Fragment, useMemo, useState } from "react";
import { ChevronRight, Database } from "lucide-react";
import { Alert, KpiTile, Note } from "./bits";
import { CopyButton } from "./DataTable";
import { Section } from "./Section";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { date, dayMonth, money, monthShort, num, pct, tsvValue } from "@/lib/sales/format";
import type { DsoDealer, DsoOpen, DsoPayMonth, DsoView, DsoWeek } from "@/lib/sales/settlements";

// «DSO»: долги дилеров по неоплаченным отгрузкам (FIFO), просрочка по норме, прогноз оплат по неделям, оплаты кассой и банком.
// Всё считает сервер (DsoCompute); страница только выбирает период и раскрывает строки. Дни — от даты данных, а не от сегодня.

const th = "whitespace-nowrap px-2.5 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
const thFirst = "whitespace-nowrap py-2 pl-4 pr-3 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3 sm:pl-5";
const td = "px-2.5 py-2 text-right tabular-nums";
const tdFirst = "py-2 pl-4 pr-3 sm:pl-5";
const full = (v: number) => (Math.round(v) ? num(v) : "—");
const tsv = (rows: (string | number | null)[][]) => rows.map((r) => r.map(tsvValue).join("\t")).join("\n");

export function DsoReport({ data }: { data: DsoView }) {
  const t = useT();
  const from = dayMonth(data.monthFrom);

  if (!data.hasPayments) {
    return (
      <Alert tone="info" className="mt-2">
        {t("Оплат дилеров в OneBase ещё нет — в Linko их нет. Привяжите таблицу оплат: «Настройки → Источники данных → DSO»: «Оплаты дилеров» (Kassa Bank), «Условия оплаты дилеров» (Ostatka), «Расходы по дилерам» (Прочие расходы).")}{" "}
        <Link href="/settings/data-sources" className="font-medium text-accent hover:underline">
          {t("Открыть источники данных")}
        </Link>
      </Alert>
    );
  }

  return (
    <>
      <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-3">
        <Database className="size-3.5" aria-hidden />
        <span>{t("отгрузки Linko и оплаты по {date}", { date: date(data.asOf) })}</span>
        {data.paymentsTo && data.paymentsTo !== data.asOf && <span>{t("последняя оплата в реестре — {date}", { date: date(data.paymentsTo) })}</span>}
        <span>{t("дни считаются от даты данных")}</span>
      </p>

      <div className="mt-3 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile label={t("Дебиторка (ДЗ)")} value={money(data.debt, t)} unit={t("сум")}>
          {t("{n} неоплаченных отгрузок · {m} дилеров", { n: num(data.openCount), m: num(data.debtDealers) })}
        </KpiTile>
        <KpiTile label={t("Просрочено (ПДЗ)")} value={money(data.overdue, t)} unit={t("сум")} tone={data.overdue > 0 ? "bad" : "ink"}>
          {t("{share} долга дилеров со сроком · {n} дилеров", { share: pct(data.overdueShare), n: num(data.overdueDealers) })}
        </KpiTile>
        <KpiTile label={t("К оплате за 7 дней")} value={money(data.dueWeek, t)} unit={t("сум")}>
          {t("по норме; по темпу оплат ждём {sum}", { sum: money(data.expectedWeek, t) })}
        </KpiTile>
        <KpiTile label={t("Оплачено с {date}", { date: from })} value={money(data.paidMonth, t)} unit={t("сум")}>
          {t("отгружено {sum}", { sum: money(data.shippedMonth, t) })}
        </KpiTile>
      </div>

      <Dealers data={data} />
      <Forecast weeks={data.weeks} title="Прогноз поступлений по неделям" />
      {data.channels.length > 0 && <Channels data={data} />}
      <PayByMonth data={data} />
      <PayByDealer data={data} />
      <OpenShipments data={data} />

      <Note>
        {t(
          "Отгрузки и возвраты — первичка из Linko (перемещения на склады дилеров и заказы прямых клиентов, сумма Linko), оплаты — реестр Kassa Bank (касса и банк), норма оплаты — лист Ostatka (не задана — {n} дн., «*»). Оплаты, возвраты и «Прочие расходы» гасят самые старые отгрузки (FIFO); долг на дату акта бухгалтерии раскладывается на последние отгрузки до неё. Просрочка — отгрузка + норма уже прошла. «По темпу оплат» — отгрузка + фактический DSO дилера: средний срок, за который он закрывал отгрузки последние {d} дн. (взвешен по сумме). Дилеры без отгрузок дольше 60 дней — «не работают с нами»: долг к взысканию, в прогноз не входит.",
          { n: data.defaultNorm, d: data.factDays },
        )}
      </Note>
    </>
  );
}

const statusTitle: Record<string, [string, string]> = {
  noterm: ["Без срока оплаты", "договор без нормы — просрочки нет, ожидаемая дата — по их темпу оплат"],
  gone: ["Не работают с нами", "отгрузок больше 60 дней нет — долг к взысканию, в прогноз не входит"],
};

function Dealers({ data }: { data: DsoView }) {
  const t = useT();
  const [open, setOpen] = useState<Set<string>>(new Set());
  const toggle = (key: string) =>
    setOpen((s) => {
      const next = new Set(s);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  const asOf = data.asOf;
  const copy = () =>
    tsv([
      [t("Дилер"), t("Норма, дн."), t("Факт DSO, дн."), t("Долг"), t("Просрочено"), t("Старая отгрузка"), t("Ближайший срок"), t("Ожидаем оплату"), t("Оплачено с {date}", { date: dayMonth(data.monthFrom) }), t("Последняя оплата")],
      ...data.dealers.map((d) => {
        const oldest = d.open[0];
        const next = nextDue(d);
        return [d.name, d.norm, d.fact, d.debt, d.overdue, oldest ? date(oldest.date) : "", next ? date(next.due) : "", firstExpected(d) ? date(firstExpected(d)) : "", d.paidMonth, d.lastPay ? date(d.lastPay) : ""];
      }),
    ]);

  return (
    <Section title={t("Дилеры")} hint={t("нажмите на дилера — неоплаченные отгрузки и когда ждать оплату; * — норма не задана, взята {n} дн.", { n: data.defaultNorm })} actions={<CopyButton onCopy={copy} />}>
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className={`${thFirst} sticky left-0 z-[1] bg-surface-solid`}>{t("Дилер")}</th>
              <th className={th}>{t("Норма, дн.")}</th>
              <th className={th}>{t("Факт DSO, дн.")}</th>
              <th className={th}>{t("Долг")}</th>
              <th className={th}>{t("Просрочено")}</th>
              <th className={th}>{t("Старая отгрузка")}</th>
              <th className={th}>{t("Ближайший срок")}</th>
              <th className={th}>{t("Ожидаем оплату")}</th>
              <th className={th}>{t("Оплачено с {date}", { date: dayMonth(data.monthFrom) })}</th>
              <th className={`${th} pr-4 sm:pr-5`}>{t("Последняя оплата")}</th>
            </tr>
          </thead>
          <tbody>
            {data.dealers.map((d, i) => {
              const header = d.status !== "active" && data.dealers[i - 1]?.status !== d.status ? statusTitle[d.status] : null;
              const oldest = d.open[0];
              const next = nextDue(d);
              const expected = firstExpected(d);
              const termless = d.status !== "active";
              const isOpen = open.has(d.key);
              return (
                <Fragment key={d.key}>
                  {header && (
                    <tr className="border-b border-line bg-muted/50">
                      <td colSpan={10} className="px-4 py-2 sm:px-5">
                        <span className="font-semibold text-ink">{t(header[0])}</span>
                        <span className="ml-2 text-xs text-ink-3">{t(header[1])}</span>
                      </td>
                    </tr>
                  )}
                  <tr onClick={() => toggle(d.key)} className="group/row cursor-pointer border-b border-line hover:bg-muted">
                    <td className={`${tdFirst} sticky left-0 z-[1] bg-surface-solid group-hover/row:bg-muted-solid`}>
                      <span className="flex min-w-[170px] items-center gap-1.5 font-medium text-ink">
                        <ChevronRight className={`size-3.5 shrink-0 text-ink-3 transition-transform ${isOpen ? "rotate-90" : ""}`} />
                        {d.name}
                      </span>
                    </td>
                    <td className={`${td} ${termless ? "text-ink-3" : ""}`}>
                      {d.status === "gone" ? t("не работает") : d.status === "noterm" ? t("без срока") : `${num(d.norm)}${d.defaultNorm ? "*" : ""}`}
                    </td>
                    <td className={`${td} ${!termless && d.fact != null && d.norm != null && d.fact > d.norm ? "font-semibold text-bad" : ""}`}>{d.fact == null ? "—" : num(d.fact, 1)}</td>
                    <td className={`${td} font-semibold ${d.status === "gone" ? "text-bad" : "text-ink"}`}>{money(d.debt, t)}</td>
                    <td className={`${td} ${d.overdue > 0 ? "font-semibold text-bad" : "text-ink-3"}`}>{d.overdue > 0 ? money(d.overdue, t) : "—"}</td>
                    <td className={`${td} ${oldest && (oldest.late > 0 || d.status === "gone") ? "text-bad" : ""}`}>
                      {oldest ? dayMonth(oldest.date) : "—"}
                      {oldest && <span className="block text-[11px] text-ink-3">{t("{n} дн. назад", { n: oldest.age })}</span>}
                    </td>
                    <td className={td}>
                      {next ? dayMonth(next.due) : "—"}
                      {next && <span className="block text-[11px] text-ink-3">{money(next.remaining, t)}</span>}
                    </td>
                    <td className={`${td} ${expected && expected < asOf ? "text-bad" : ""}`}>{d.status !== "gone" && expected ? dayMonth(expected) : "—"}</td>
                    <td className={td}>
                      {d.paidMonth ? money(d.paidMonth, t) : "—"}
                      {d.shippedMonth > 0 && <span className="block text-[11px] text-ink-3">{t("отгр. {sum}", { sum: money(d.shippedMonth, t) })}</span>}
                    </td>
                    <td className={`${td} pr-4 sm:pr-5 ${d.daysSincePay == null || d.daysSincePay > 30 ? "text-bad" : ""}`}>{d.lastPay ? dayMonth(d.lastPay) : "—"}</td>
                  </tr>
                  {isOpen && (
                    <tr className="border-b border-line">
                      <td colSpan={10} className="bg-muted/40 px-4 py-3 sm:px-5">
                        <DealerDetail dealer={d} asOf={asOf} t={t} />
                      </td>
                    </tr>
                  )}
                </Fragment>
              );
            })}
          </tbody>
          <tfoot>
            <tr className="font-semibold text-ink">
              <td className={`${tdFirst} sticky left-0 z-[1] bg-surface-solid`}>{t("Итого")}</td>
              <td className={td} />
              <td className={td} />
              <td className={td}>{money(data.debt, t)}</td>
              <td className={`${td} text-bad`}>{money(data.overdue, t)}</td>
              <td className={td} />
              <td className={td} />
              <td className={td} />
              <td className={td}>{money(data.paidMonth, t)}</td>
              <td className={`${td} pr-4 sm:pr-5`} />
            </tr>
          </tfoot>
        </table>
      </div>
    </Section>
  );
}

/** Ближайший ещё не просроченный срок по норме. */
function nextDue(d: DsoDealer): (DsoOpen & { due: string }) | null {
  if (d.status !== "active") return null;
  const items = d.open.filter((i): i is DsoOpen & { due: string } => i.due != null && i.late <= 0);
  return items.sort((a, b) => a.due.localeCompare(b.due))[0] ?? null;
}

const firstExpected = (d: DsoDealer) => d.open.map((i) => i.expected).sort()[0] ?? null;

function DealerDetail({ dealer, asOf, t }: { dealer: DsoDealer; asOf: string; t: T }) {
  return (
    <div className="grid gap-4 xl:grid-cols-[3fr_2fr]">
      <div>
        <div className="mb-1 text-xs font-semibold text-ink-2">{t("Неоплаченные отгрузки")}</div>
        <OpenTable items={dealer.open} gone={dealer.status === "gone"} asOf={asOf} t={t} />
      </div>
      {dealer.weeks && (
        <div>
          <div className="mb-1 text-xs font-semibold text-ink-2">{t("Когда ждать оплату")}</div>
          <WeeksTable weeks={dealer.weeks.filter((w) => w.byNorm >= 1 || w.expected >= 1)} t={t} compact />
        </div>
      )}
    </div>
  );
}

function OpenTable({ items, gone, asOf, t, dealer }: { items: (DsoOpen & { dealer?: string })[]; gone: boolean; asOf: string; t: T; dealer?: boolean }) {
  const cell = "px-2 py-1.5 text-right tabular-nums";
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-max border-collapse text-xs">
        <thead>
          <tr className="border-b border-line text-[10px] uppercase tracking-wide text-ink-3">
            {dealer && <th className="px-2 py-1.5 text-left font-semibold">{t("Дилер")}</th>}
            <th className="px-2 py-1.5 text-left font-semibold">{t("Отгрузка")}</th>
            <th className={`${cell} font-semibold`}>{t("Сумма")}</th>
            <th className={`${cell} font-semibold`}>{t("Не оплачено")}</th>
            <th className={`${cell} font-semibold`}>{t("Дней")}</th>
            <th className={`${cell} font-semibold`}>{t("Срок по норме")}</th>
            <th className={`${cell} font-semibold`}>{t("Просрочка, дн.")}</th>
            <th className={`${cell} font-semibold`}>{t("Ожидаем оплату")}</th>
          </tr>
        </thead>
        <tbody>
          {items.map((i, n) => (
            <tr key={n} className={`border-b border-line/60 ${i.late > 0 ? "bg-bad/5" : ""}`}>
              {dealer && <td className="px-2 py-1.5 text-ink">{i.dealer}</td>}
              <td className="px-2 py-1.5 tabular-nums text-ink-2">{date(i.date)}</td>
              <td className={cell}>{full(i.amount)}</td>
              <td className={`${cell} font-semibold text-ink`}>{full(i.remaining)}</td>
              <td className={cell}>{i.age}</td>
              <td className={cell}>{i.due ? dayMonth(i.due) : "—"}</td>
              <td className={`${cell} ${i.late > 0 ? "font-semibold text-bad" : "text-ink-3"}`}>{i.late > 0 ? i.late : "—"}</td>
              <td className={`${cell} ${!gone && i.expected < asOf ? "text-bad" : ""}`}>
                {gone ? "—" : i.expected < asOf ? t("{date} · задерживается", { date: dayMonth(i.expected) }) : dayMonth(i.expected)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

const weekLabel = (w: DsoWeek, t: T) =>
  w.key === "late" ? t("Срок уже прошёл") : w.key === "later" ? t("Позже") : `${dayMonth(w.from)}–${dayMonth(w.to)}`;

function WeeksTable({ weeks, t, compact = false }: { weeks: DsoWeek[]; t: T; compact?: boolean }) {
  const max = Math.max(1, ...weeks.map((w) => Math.max(w.byNorm, w.expected)));
  const cell = compact ? "px-2 py-1.5 text-right tabular-nums" : td;
  const first = compact ? "px-2 py-1.5" : tdFirst;
  return (
    <table className={`w-full border-collapse ${compact ? "text-xs" : "text-sm"}`}>
      <thead>
        <tr className="border-b border-line">
          <th className={compact ? "px-2 py-1.5 text-left text-[10px] font-semibold uppercase tracking-wide text-ink-3" : thFirst}>{t("Неделя")}</th>
          <th className={compact ? "px-2 py-1.5 text-right text-[10px] font-semibold uppercase tracking-wide text-ink-3" : th}>{t("По норме")}</th>
          <th className={compact ? "px-2 py-1.5 text-right text-[10px] font-semibold uppercase tracking-wide text-ink-3" : th}>{t("По темпу оплат")}</th>
          {!compact && (
            <>
              <th className={th}>{t("Касса")}</th>
              <th className={th}>{t("Банк")}</th>
              <th className={`${th} w-[28%] max-md:hidden`} aria-hidden />
            </>
          )}
        </tr>
      </thead>
      <tbody>
        {weeks.map((w) => (
          <tr key={w.key} className="border-b border-line/60">
            <td className={`${first} whitespace-nowrap ${w.key === "late" ? "font-medium text-bad" : "text-ink"}`}>{weekLabel(w, t)}</td>
            <td className={`${cell} ${w.key === "late" && w.byNorm >= 1 ? "font-semibold text-bad" : ""}`}>{w.byNorm >= 1 ? money(w.byNorm, t) : "—"}</td>
            <td className={`${cell} font-medium text-ink`}>{w.expected >= 1 ? money(w.expected, t) : "—"}</td>
            {!compact && (
              <>
                <td className={cell}>
                  {w.cash >= 1 ? money(w.cash, t) : "—"}
                  {w.expected >= 1 && <span className="ml-1.5 text-[11px] text-ink-3">{pct(w.cash / w.expected)}</span>}
                </td>
                <td className={cell}>
                  {w.bank >= 1 ? money(w.bank, t) : "—"}
                  {w.expected >= 1 && <span className="ml-1.5 text-[11px] text-ink-3">{pct(w.bank / w.expected)}</span>}
                </td>
                <td className="py-2 pr-4 max-md:hidden sm:pr-5">
                  <div className="relative h-2 rounded-full bg-muted">
                    <div className="absolute inset-y-0 left-0 rounded-full bg-line" style={{ width: `${(w.byNorm / max) * 100}%` }} />
                    <div className="absolute inset-y-0 left-0 rounded-full bg-accent/80" style={{ width: `${Math.max(w.expected >= 1 ? 1 : 0, (w.expected / max) * 100)}%`, height: "50%", top: "25%" }} />
                  </div>
                </td>
              </>
            )}
          </tr>
        ))}
      </tbody>
    </table>
  );
}

function Forecast({ weeks, title }: { weeks: DsoWeek[]; title: string }) {
  const t = useT();
  const total = (f: (w: DsoWeek) => number) => weeks.reduce((a, w) => a + f(w), 0);
  const copy = () =>
    tsv([
      [t("Неделя"), t("По норме"), t("По темпу оплат"), t("Касса"), t("Банк")],
      ...weeks.map((w) => [weekLabel(w, t), w.byNorm, w.expected, w.cash, w.bank]),
      [t("Итого"), total((w) => w.byNorm), total((w) => w.expected), total((w) => w.cash), total((w) => w.bank)],
    ]);
  return (
    <Section
      title={t(title)}
      hint={t("по норме — срок по договору; по темпу — по фактическому DSO дилера")}
      actions={<CopyButton onCopy={copy} />}
    >
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <WeeksTable weeks={weeks} t={t} />
      </div>
      <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-ink-3">
        <span>{t("Итого по норме {sum}", { sum: money(total((w) => w.byNorm), t) })}</span>
        <span>{t("по темпу {sum}: касса {cash}, банк {bank}", { sum: money(total((w) => w.expected), t), cash: money(total((w) => w.cash), t), bank: money(total((w) => w.bank), t) })}</span>
        <span className="inline-flex items-center gap-1.5">
          <span className="inline-block h-2 w-4 rounded-full bg-line" /> {t("по норме")}
          <span className="ml-2 inline-block h-1 w-4 rounded-full bg-accent/80" /> {t("по темпу")}
        </span>
      </div>
    </Section>
  );
}

function Channels({ data }: { data: DsoView }) {
  const t = useT();
  const months = data.channels[0].months;
  const [monthKey, setMonthKey] = useState(months[0].key);
  const month = months.find((m) => m.key === monthKey) ?? months[0];
  const isCurrent = month.key === months[0].key;
  const maxDay = isCurrent ? Number(data.asOf.slice(8, 10)) : month.shipped.length;
  const [day, setDay] = useState(maxDay);
  const upTo = Math.min(day, maxDay);
  const rows = data.channels.map((c) => {
    const m = c.months.find((x) => x.key === month.key)!;
    const sum = (a: number[]) => Math.round(a.slice(0, upTo).reduce((s, v) => s + v, 0));
    return { key: c.key, name: c.name, shipped: sum(m.shipped), paid: sum(m.paid) };
  });
  const total = { shipped: rows.reduce((a, r) => a + r.shipped, 0), paid: rows.reduce((a, r) => a + r.paid, 0) };
  const copy = () =>
    tsv([[t("Канал"), t("Отгружено"), t("Поступило"), t("Поступило − отгружено"), t("Поступило / отгружено")], ...[...rows, { name: t("Итого"), ...total }].map((r) => [t(r.name), r.shipped, r.paid, r.paid - r.shipped, r.shipped ? r.paid / r.shipped : null])]);
  const select = "h-8 rounded-lg border border-line bg-surface px-2 text-sm text-ink max-lg:h-11";
  return (
    <Section
      title={t("Отгрузки и поступления по каналам")}
      hint={t("{month}, 1–{day} число: отгрузки Linko и оплаты Kassa Bank", { month: `${monthShort(month.month, t)} ${month.year}`, day: upTo })}
      actions={
        <>
          <select aria-label={t("Месяц")} value={month.key} onChange={(e) => { setMonthKey(e.target.value); setDay(31); }} className={select}>
            {months.map((m) => (
              <option key={m.key} value={m.key}>
                {monthShort(m.month, t)} {m.year}
              </option>
            ))}
          </select>
          <select aria-label={t("По число")} value={upTo} onChange={(e) => setDay(Number(e.target.value))} className={select}>
            {Array.from({ length: maxDay }, (_, i) => maxDay - i).map((d) => (
              <option key={d} value={d}>
                {t("по {day}-е", { day: d })}
              </option>
            ))}
          </select>
          <CopyButton onCopy={copy} />
        </>
      }
    >
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className={thFirst}>{t("Канал")}</th>
              <th className={th}>{t("Отгружено")}</th>
              <th className={th}>{t("Поступило")}</th>
              <th className={th}>{t("Поступило − отгружено")}</th>
              <th className={`${th} pr-4 sm:pr-5`}>{t("Поступило / отгружено")}</th>
            </tr>
          </thead>
          <tbody>
            {[...rows, { key: "total", name: "Итого", ...total }].map((r) => {
              const delta = r.paid - r.shipped;
              return (
                <tr key={r.key} className={r.key === "total" ? "font-semibold text-ink" : "border-b border-line"}>
                  <td className={`${tdFirst} ${r.key === "total" ? "" : "font-medium text-ink"}`}>{t(r.name)}</td>
                  <td className={td}>{full(r.shipped)}</td>
                  <td className={td}>{full(r.paid)}</td>
                  <td className={`${td} ${delta < 0 ? "text-bad" : delta > 0 ? "text-ok" : ""}`}>{delta ? `${delta > 0 ? "+" : "−"} ${num(Math.abs(delta))}` : "—"}</td>
                  <td className={`${td} pr-4 sm:pr-5`}>{r.shipped ? pct(r.paid / r.shipped, 1) : "—"}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </Section>
  );
}

function SplitRow({ label, sub, m, bold = false }: { label: string; sub?: string | null; m: { cash: number; bank: number }; bold?: boolean }) {
  const sum = m.cash + m.bank;
  const share = sum ? m.cash / sum : 0;
  return (
    <tr className={bold ? "font-semibold text-ink" : "border-b border-line"}>
      <td className={`${tdFirst} ${bold ? "" : "text-ink"}`}>
        {label}
        {sub && <span className="ml-2 text-[11px] font-normal text-ink-3">{sub}</span>}
      </td>
      <td className={td}>
        {m.cash ? num(m.cash) : "—"}
        <span className="ml-1.5 text-[11px] font-normal text-ink-3">{sum ? pct(share) : ""}</span>
      </td>
      <td className={td}>
        {m.bank ? num(m.bank) : "—"}
        <span className="ml-1.5 text-[11px] font-normal text-ink-3">{sum ? pct(1 - share) : ""}</span>
      </td>
      <td className={`${td} font-semibold text-ink`}>{num(sum)}</td>
      <td className="w-[22%] py-2 pr-4 max-md:hidden sm:pr-5">
        <div className="flex h-2 overflow-hidden rounded-full bg-muted">
          <div className="bg-accent" style={{ width: `${Math.max(0, share) * 100}%` }} />
          <div className="bg-ink-3/40" style={{ width: `${Math.max(0, 1 - share) * 100}%` }} />
        </div>
      </td>
    </tr>
  );
}

function SplitHead({ first }: { first: string }) {
  const t = useT();
  return (
    <thead>
      <tr className="border-b border-line">
        <th className={thFirst}>{t(first)}</th>
        <th className={th}>{t("Касса")}</th>
        <th className={th}>{t("Банк")}</th>
        <th className={th}>{t("Итого")}</th>
        <th className="max-md:hidden" aria-hidden />
      </tr>
    </thead>
  );
}

const sumPay = (list: { cash: number; bank: number }[]) => ({ cash: list.reduce((a, m) => a + m.cash, 0), bank: list.reduce((a, m) => a + m.bank, 0) });

function PayByMonth({ data }: { data: DsoView }) {
  const t = useT();
  const last = data.payMonths[data.payMonths.length - 1];
  const label = (m: DsoPayMonth) => `${monthShort(m.month, t)} ${m.year}`;
  const copy = () => tsv([[t("Месяц"), t("Касса"), t("Банк"), t("Итого")], ...data.payMonths.map((m) => [label(m), m.cash, m.bank, m.cash + m.bank])]);
  return (
    <Section title={t("Оплаты по месяцам: касса и банк")} hint={t("Kassa Bank — поступления от контрагентов; синим — касса (наличные)")} actions={<CopyButton onCopy={copy} />}>
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <SplitHead first="Месяц" />
          <tbody>
            {data.payMonths.map((m, i) => (
              <SplitRow key={m.key} label={label(m)} sub={i === 0 && data.paymentsTo ? t("по {date}", { date: dayMonth(data.paymentsTo) }) : null} m={m} />
            ))}
          </tbody>
          <tfoot>{last && <SplitRow label={t("Всё с {month}", { month: label(last) })} m={sumPay(data.payMonths)} bold />}</tfoot>
        </table>
      </div>
    </Section>
  );
}

function PayByDealer({ data }: { data: DsoView }) {
  const t = useT();
  const year = Number(data.asOf.slice(0, 4));
  const [period, setPeriod] = useState("year");
  const rows = useMemo(
    () =>
      data.payDealers
        .map((d) => {
          const months = d.months.filter((m) => (period === "year" ? m.year === year : period === "all" ? true : m.key === period));
          return { name: d.name, ...sumPay(months) };
        })
        .filter((r) => Math.round(r.cash + r.bank) !== 0)
        .sort((a, b) => b.cash + b.bank - (a.cash + a.bank)),
    [data.payDealers, period, year],
  );
  const last = data.payMonths[data.payMonths.length - 1];
  const copy = () => tsv([[t("Контрагент"), t("Касса"), t("Банк"), t("Итого")], ...rows.map((r) => [r.name, r.cash, r.bank, r.cash + r.bank])]);
  return (
    <Section
      title={t("Оплаты по контрагентам: касса и банк")}
      actions={
        <>
          <select aria-label={t("Период")} value={period} onChange={(e) => setPeriod(e.target.value)} className="h-8 rounded-lg border border-line bg-surface px-2 text-sm text-ink max-lg:h-11">
            <option value="year">{t("С начала {year}", { year })}</option>
            {last && <option value="all">{t("Всё с {month}", { month: `${monthShort(last.month, t)} ${last.year}` })}</option>}
            {data.payMonths.map((m) => (
              <option key={m.key} value={m.key}>
                {monthShort(m.month, t)} {m.year}
              </option>
            ))}
          </select>
          <CopyButton onCopy={copy} />
        </>
      }
    >
      <div className="-mx-4 max-h-[560px] overflow-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <SplitHead first="Контрагент" />
          <tbody>
            {rows.map((r) => (
              <SplitRow key={r.name} label={r.name} m={r} />
            ))}
          </tbody>
          <tfoot>
            <SplitRow label={t("Итого")} m={sumPay(rows)} bold />
          </tfoot>
        </table>
      </div>
    </Section>
  );
}

function OpenShipments({ data }: { data: DsoView }) {
  const t = useT();
  const items = data.dealers
    .filter((d) => d.status === "active")
    .flatMap((d) => d.open.filter((i) => i.remaining >= 1).map((i) => ({ ...i, dealer: d.name })))
    .sort((a, b) => (a.due ?? "").localeCompare(b.due ?? "") || b.remaining - a.remaining);
  if (items.length === 0) return null;
  const copy = () =>
    tsv([
      [t("Дилер"), t("Отгрузка"), t("Сумма"), t("Не оплачено"), t("Дней"), t("Срок по норме"), t("Просрочка, дн."), t("Ожидаем оплату")],
      ...items.map((i) => [i.dealer, date(i.date), i.amount, i.remaining, i.age, i.due ? date(i.due) : "", i.late > 0 ? i.late : "", date(i.expected)]),
    ]);
  return (
    <Section title={t("Неоплаченные отгрузки")} hint={t("дилеры со сроком, по сроку оплаты; красным — срок по норме прошёл")} actions={<CopyButton onCopy={copy} />}>
      <div className="max-h-[560px] overflow-auto">
        <OpenTable items={items} gone={false} asOf={data.asOf} t={t} dealer />
      </div>
    </Section>
  );
}
