"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { AlertTriangle, ChevronLeft, ChevronRight, ExternalLink } from "lucide-react";
import { Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import {
  agingLabel,
  date,
  docTypeLabel,
  full,
  money,
  monthShort,
  type AmountRow,
  type EntryView,
  type FinanceAccount,
  type FinanceAnomalies,
  type FinanceCash,
  type FinanceDocument,
  type FinanceEntries,
  type FinanceOperations,
  type FinanceOverview,
  type FinancePnl,
  type FinanceSettlements,
  type FinanceTrialBalance,
} from "@/lib/finance";
import { Delta, Kpi, MetaBar, MonthBars, num, ShareBars, TableBox, td, th } from "./FinanceBits";

/** Ссылка внутри раздела с сохранением организации и периода. */
function useLink() {
  const sp = useSearchParams();
  return (path: string, extra: Record<string, string | undefined> = {}) => {
    const next = new URLSearchParams();
    for (const k of ["organization", "from", "to"]) {
      const v = sp.get(k);
      if (v) next.set(k, v);
    }

    for (const [k, v] of Object.entries(extra)) if (v) next.set(k, v);
    const q = next.toString();
    return q ? `${path}?${q}` : path;
  };
}

const value = (rows: { code: string; value: number }[], code: string) => rows.find((r) => r.code === code)?.value ?? 0;

// ---------- Обзор ----------

export function OverviewView({ data }: { data: FinanceOverview }) {
  const t = useT();
  const link = useLink();
  const prev = (code: string) => data.pnl.find((p) => p.code === code)?.previous ?? null;
  const months = data.months.map((m) => m.month);
  return (
    <div className="space-y-4">
      <MetaBar meta={data.meta} settingsLink />
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Kpi label="Чистая выручка" value={value(data.pnl, "net_revenue")} previous={prev("net_revenue")} href={link("/finance/pnl")} />
        <Kpi label="Валовая прибыль" value={value(data.pnl, "gross_profit")} previous={prev("gross_profit")} href={link("/finance/pnl")} />
        <Kpi label="Чистая прибыль" value={value(data.pnl, "net_profit")} previous={prev("net_profit")} href={link("/finance/pnl")} />
        <Kpi label="Деньги на счетах и в кассе" value={data.cashTotal} hint={data.balancesMonth ? `на конец ${monthShort(data.balancesMonth)}` : undefined} href={link("/finance/cash")} />
        <Kpi label="Дебиторская задолженность" value={data.receivables} good="down" href={link("/finance/settlements", { kind: "receivables" })} />
        <Kpi label="Кредиторская задолженность" value={data.payables} good="down" href={link("/finance/settlements", { kind: "payables" })} />
        <Kpi label="Запасы" value={data.inventory.reduce((s, r) => s + r.amount, 0)} href={link("/finance/operations")} />
        <Kpi label="Себестоимость продаж" value={value(data.pnl, "cost_of_sales")} previous={prev("cost_of_sales")} good="down" href={link("/finance/operations")} />
      </div>

      <Section title="Выручка, расходы и прибыль за 12 месяцев" hint="Расходы — себестоимость, расходы периода и финансовые расходы">
        <MonthBars
          months={months}
          series={[
            { label: "Чистая выручка", values: data.months.map((m) => m.revenue), className: "fill-accent/70" },
            { label: "Расходы", values: data.months.map((m) => m.expenses), className: "fill-ink-3/40" },
          ]}
          line={{ label: "Чистая прибыль", values: data.months.map((m) => m.netProfit) }}
        />
      </Section>

      <div className="grid gap-4 lg:grid-cols-2">
        <Section title="Деньги" hint={data.balancesMonth ? `остатки на конец ${monthShort(data.balancesMonth)}` : "остатков 1С пока нет"} className="!mt-0">
          <ShareBars rows={data.cash.map((c) => ({ key: c.key, title: `${c.key} · ${c.title}`, amount: c.amount, note: c.currencyAmount != null ? `${full(c.currencyAmount, 2)} ${c.currency}` : undefined }))} href={(k) => link(`/finance/accounts/${k}`)} />
        </Section>
        <Section title="Где выросли расходы" hint="к прошлому периоду той же длины" className="!mt-0">
          {data.expenseGrowth.length === 0 ? (
            <p className="text-sm text-ink-3">{t("Расходы не выросли.")}</p>
          ) : (
            <ul className="space-y-2 text-sm">
              {data.expenseGrowth.map((g) => (
                <li key={g.key} className="flex items-baseline justify-between gap-3">
                  <Link href={link(`/finance/accounts/${g.key}`)} className="min-w-0 truncate text-ink hover:text-accent-strong hover:underline">
                    {g.key} · {g.title}
                  </Link>
                  <span className="flex shrink-0 items-center gap-2 tabular-nums">
                    {money(g.amount)} <Delta now={g.amount} before={g.previous} good="down" />
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Section>
      </div>

      <Section title="Отчёт о финансовых результатах" hint="коротко; полностью — «Доходы и расходы»">
        <PnlTable rows={data.pnl.map((p) => ({ code: p.code, title: p.title, kind: p.kind, total: p.value, previous: p.previous }))} />
      </Section>
    </div>
  );
}

function PnlTable({ rows, months }: { rows: { code: string; title: string; kind: string; total: number; previous: number | null; months?: number[] }[]; months?: string[] }) {
  const t = useT();
  return (
    <TableBox>
      <table className="w-full min-w-[520px] text-sm">
        <thead>
          <tr>
            <th className={th}>{t("Показатель")}</th>
            {months?.map((m) => (
              <th key={m} className={`${th} text-right`}>
                {monthShort(m)}
              </th>
            ))}
            <th className={`${th} text-right`}>{t("За период")}</th>
            <th className={`${th} text-right`}>{t("Прошлый период")}</th>
            <th className={`${th} text-right`}>±</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.code} className={r.kind === "total" ? "bg-muted/50 font-semibold" : ""}>
              <td className={`${td} ${r.kind === "total" ? "text-ink" : "pl-4 text-ink-2"}`}>{t(r.title)}</td>
              {r.months?.map((v, i) => (
                <td key={i} className={`${td} ${num}`} title={full(v)}>
                  {money(v)}
                </td>
              ))}
              <td className={`${td} ${num} ${r.total < 0 ? "text-bad" : ""}`} title={full(r.total)}>
                {money(r.total)}
              </td>
              <td className={`${td} ${num} text-ink-3`}>{money(r.previous)}</td>
              <td className={`${td} ${num}`}>
                <Delta now={r.total} before={r.previous} good={r.kind === "expense" ? "down" : "up"} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </TableBox>
  );
}

// ---------- Доходы и расходы ----------

export function PnlView({ data }: { data: FinancePnl }) {
  const t = useT();
  const link = useLink();
  return (
    <div className="space-y-4">
      <MetaBar meta={data.meta} settingsLink />
      <Section
        title="Отчёт о финансовых результатах"
        hint="Выручка, себестоимость и расходы — по проводкам 1С (счета 90–98); закрытие месяца на 9910 и переносы между счетами доходов и расходов не учитываются"
      >
        <PnlTable rows={data.lines} months={data.months.length > 1 && data.months.length <= 12 ? data.months : undefined} />
      </Section>
      <div className="grid gap-4 lg:grid-cols-2">
        <Section title="Расходы по счетам" hint="рост к прошлому периоду — сверху" className="!mt-0">
          <TableBox>
            <table className="w-full text-sm">
              <thead>
                <tr>
                  <th className={th}>{t("Счёт")}</th>
                  <th className={`${th} text-right`}>{t("За период")}</th>
                  <th className={`${th} text-right`}>{t("Было")}</th>
                  <th className={`${th} text-right`}>±</th>
                </tr>
              </thead>
              <tbody>
                {data.expensesByAccount.map((r) => (
                  <tr key={r.key}>
                    <td className={td}>
                      <Link href={link(`/finance/accounts/${r.key}`)} className="hover:text-accent-strong hover:underline">
                        {r.key} · {r.title}
                      </Link>
                    </td>
                    <td className={`${td} ${num}`}>{money(r.amount)}</td>
                    <td className={`${td} ${num} text-ink-3`}>{money(r.previous)}</td>
                    <td className={`${td} ${num}`}>
                      <Delta now={r.amount} before={r.previous} good="down" />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableBox>
        </Section>
        <Section title="Расходы периода по подразделениям" hint="счета 94xx" className="!mt-0">
          <ShareBars rows={data.expensesByDepartment} />
        </Section>
      </div>
    </div>
  );
}

// ---------- Деньги ----------

export function CashView({ data }: { data: FinanceCash }) {
  const t = useT();
  const link = useLink();
  const inflow = data.flows.filter((f) => f.inflow);
  const outflow = data.flows.filter((f) => !f.inflow);
  const net = data.inflow - data.outflow;
  return (
    <div className="space-y-4">
      <MetaBar meta={data.meta} settingsLink />
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Kpi label="Поступило" value={data.inflow} />
        <Kpi label="Выплачено" value={data.outflow} good="down" />
        <Kpi label="Чистый поток" value={net} />
        <Kpi label="Остаток денег" value={data.balances.reduce((s, b) => s + b.amount, 0)} hint={data.balancesMonth ? `на конец ${monthShort(data.balancesMonth)}` : undefined} />
      </div>
      {data.months.length > 1 && (
        <Section title="Поступления и выплаты по месяцам">
          <MonthBars
            months={data.months}
            series={[
              { label: "Поступления", values: data.months.map((_, i) => inflow.reduce((s, f) => s + f.months[i], 0)), className: "fill-ok/60" },
              { label: "Выплаты", values: data.months.map((_, i) => outflow.reduce((s, f) => s + f.months[i], 0)), className: "fill-bad/50" },
            ]}
          />
        </Section>
      )}
      <div className="grid gap-4 lg:grid-cols-2">
        <Section title="Откуда пришли деньги" hint="по корреспондирующим счетам; переводы между своими счетами и курсовые разницы не учитываются" className="!mt-0">
          <ShareBars rows={inflow.map((f) => ({ key: f.code, title: f.title, amount: f.total }))} />
        </Section>
        <Section title="Куда ушли деньги" className="!mt-0">
          <ShareBars rows={outflow.map((f) => ({ key: f.code, title: f.title, amount: f.total }))} />
        </Section>
      </div>
      <Section title="Остатки денег по счетам" hint="валютные — в валюте и в сумах по курсу 1С">
        <TableBox>
          <table className="w-full text-sm">
            <thead>
              <tr>
                <th className={th}>{t("Счёт")}</th>
                <th className={`${th} text-right`}>{t("В валюте")}</th>
                <th className={`${th} text-right`}>{t("В сумах")}</th>
              </tr>
            </thead>
            <tbody>
              {data.balances.map((b) => (
                <tr key={`${b.key}-${b.currency}`}>
                  <td className={td}>
                    <Link href={link(`/finance/accounts/${b.key}`)} className="hover:text-accent-strong hover:underline">
                      {b.key} · {b.title}
                    </Link>
                  </td>
                  <td className={`${td} ${num} text-ink-2`}>{b.currencyAmount != null ? `${full(b.currencyAmount, 2)} ${b.currency}` : "—"}</td>
                  <td className={`${td} ${num}`}>{full(b.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableBox>
      </Section>
    </div>
  );
}

// ---------- Расчёты ----------

export function SettlementsView({ data }: { data: FinanceSettlements }) {
  const t = useT();
  const link = useLink();
  const pathname = usePathname();
  const receivable = data.kind === "receivables";
  const tabs = [
    { kind: "receivables", label: "Дебиторская задолженность" },
    { kind: "payables", label: "Кредиторская задолженность" },
  ];
  return (
    <div className="space-y-4">
      <nav aria-label={t("Вид задолженности")} className="flex gap-1 overflow-x-auto border-b border-line">
        {tabs.map((tab) => (
          <Link
            key={tab.kind}
            href={link(pathname, { kind: tab.kind })}
            aria-current={tab.kind === data.kind ? "page" : undefined}
            className={`-mb-px shrink-0 border-b-2 px-3 py-2.5 text-sm font-medium ${tab.kind === data.kind ? "border-accent text-accent-strong" : "border-transparent text-ink-2 hover:text-ink"}`}
          >
            {t(tab.label)}
          </Link>
        ))}
      </nav>
      <MetaBar meta={data.meta} settingsLink />
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Kpi label={receivable ? "Нам должны" : "Мы должны"} value={data.total} good="down" hint={data.balancesMonth ? `на конец ${monthShort(data.balancesMonth)}` : undefined} />
        <Kpi label="Просрочено" value={data.overdue} good="down" />
        <Kpi label={receivable ? "Авансы получены" : "Авансы выданы"} value={data.advances} />
        <Kpi label="Контрагентов с долгом" value={data.rows.length} />
      </div>
      <Section title="Структура по срокам" hint={data.method}>
        <div className="flex h-3 overflow-hidden rounded-full bg-muted">
          {Object.entries(data.buckets).map(([k, v]) =>
            v > 0 ? (
              <div
                key={k}
                title={`${t(agingLabel[k] ?? k)}: ${full(v)}`}
                className={k === "not_due" ? "bg-ok/60" : k === "no_term" ? "bg-ink-3/30" : k === "1_30" ? "bg-warn/50" : "bg-bad/60"}
                style={{ width: `${(v / Math.max(1, data.total)) * 100}%` }}
              />
            ) : null,
          )}
        </div>
        <ul className="mt-3 grid grid-cols-2 gap-2 text-sm sm:grid-cols-3">
          {Object.entries(data.buckets).map(([k, v]) => (
            <li key={k} className="flex justify-between gap-2">
              <span className="text-ink-2">{t(agingLabel[k] ?? k)}</span>
              <span className="tabular-nums">{money(v)}</span>
            </li>
          ))}
        </ul>
      </Section>
      <Section title={receivable ? "Покупатели с задолженностью" : "Поставщики, которым мы должны"}>
        <TableBox>
          <table className="w-full min-w-[640px] text-sm">
            <thead>
              <tr>
                <th className={th}>{t("Контрагент")}</th>
                <th className={`${th} text-right`}>{t("Задолженность")}</th>
                <th className={`${th} text-right`}>{t("Просрочено")}</th>
                <th className={`${th} text-right`}>{t("Дней просрочки")}</th>
                <th className={th}>{t("Сроки")}</th>
              </tr>
            </thead>
            <tbody>
              {data.rows.map((r) => (
                <tr key={r.counterpartyId}>
                  <td className={td}>
                    <Link href={link("/finance/entries", { counterparty: r.counterpartyId, account: receivable ? "40" : "60" })} className="font-medium text-ink hover:text-accent-strong hover:underline">
                      {r.name}
                    </Link>
                    {r.inn && <div className="text-xs text-ink-3">{t("ИНН")} {r.inn}</div>}
                  </td>
                  <td className={`${td} ${num}`}>{full(r.balance)}</td>
                  <td className={`${td} ${num} ${r.overdue > 0 ? "text-bad" : "text-ink-3"}`}>{r.overdue > 0 ? full(r.overdue) : "—"}</td>
                  <td className={`${td} ${num}`}>{r.maxOverdueDays ?? "—"}</td>
                  <td className={`${td} text-xs text-ink-3`}>{r.termsKnown ? t("из документов и договоров") : t("часть без срока оплаты")}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </TableBox>
      </Section>
    </div>
  );
}

// ---------- Счета ----------

export function TrialBalanceView({ data }: { data: FinanceTrialBalance }) {
  const t = useT();
  const link = useLink();
  const cell = (v: number) => (v === 0 ? <span className="text-ink-3">—</span> : full(v));
  return (
    <div className="space-y-4">
      <MetaBar meta={data.meta} settingsLink />
      <Section title="Оборотно-сальдовая ведомость" hint={data.method}>
        <TableBox>
          <table className="w-full min-w-[760px] text-sm">
            <thead>
              <tr>
                <th className={th} rowSpan={2}>
                  {t("Счёт")}
                </th>
                <th className={`${th} text-center`} colSpan={2}>
                  {t("Сальдо на начало")}
                </th>
                <th className={`${th} text-center`} colSpan={2}>
                  {t("Обороты")}
                </th>
                <th className={`${th} text-center`} colSpan={2}>
                  {t("Сальдо на конец")}
                </th>
              </tr>
              <tr>
                {["od", "oc", "td", "tc", "cd", "cc"].map((k, i) => (
                  <th key={k} className={`${th} text-right`}>
                    {t(i % 2 === 0 ? "Дт" : "Кт")}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {data.rows.map((r) => (
                <tr key={r.account}>
                  <td className={td}>
                    <Link href={link(`/finance/accounts/${r.account}`)} className="block hover:text-accent-strong hover:underline">
                      <span className="font-medium tabular-nums">{r.account}</span> <span className="text-ink-2">{r.name}</span>
                    </Link>
                  </td>
                  <td className={`${td} ${num}`}>{cell(r.openingDebit)}</td>
                  <td className={`${td} ${num}`}>{cell(r.openingCredit)}</td>
                  <td className={`${td} ${num}`}>{cell(r.turnoverDebit)}</td>
                  <td className={`${td} ${num}`}>{cell(r.turnoverCredit)}</td>
                  <td className={`${td} ${num}`}>{cell(r.closingDebit)}</td>
                  <td className={`${td} ${num}`}>{cell(r.closingCredit)}</td>
                </tr>
              ))}
              <tr className="bg-muted/50 font-semibold">
                <td className={td}>{t("Итого")}</td>
                {[data.total.openingDebit, data.total.openingCredit, data.total.turnoverDebit, data.total.turnoverCredit, data.total.closingDebit, data.total.closingCredit].map((v, i) => (
                  <td key={i} className={`${td} ${num}`}>
                    {full(v)}
                  </td>
                ))}
              </tr>
            </tbody>
          </table>
        </TableBox>
      </Section>
    </div>
  );
}

export function AccountView({ data }: { data: FinanceAccount }) {
  const t = useT();
  const link = useLink();
  const typeLabel = { active: "активный", passive: "пассивный", active_passive: "активно-пассивный" }[data.type] ?? data.type;
  return (
    <div className="space-y-4">
      <MetaBar meta={data.meta} settingsLink />
      <div className="flex flex-wrap items-center gap-2 text-sm text-ink-2">
        <span className="rounded-full bg-muted px-2.5 py-1">{t(typeLabel)}</span>
        {data.subconto.map((s) => (
          <span key={s} className="rounded-full border border-line px-2.5 py-1 text-xs">
            {s}
          </span>
        ))}
        <Link href={link("/finance/entries", { account: data.account })} className="ml-auto inline-flex items-center gap-1 font-medium text-accent-strong hover:underline">
          {t("Все проводки по счёту")} <ExternalLink aria-hidden className="size-3.5" />
        </Link>
      </div>
      {data.totals && (
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <Kpi label="Сальдо на начало" value={data.totals.openingDebit - data.totals.openingCredit} />
          <Kpi label="Оборот по дебету" value={data.totals.turnoverDebit} />
          <Kpi label="Оборот по кредиту" value={data.totals.turnoverCredit} />
          <Kpi label="Сальдо на конец" value={data.totals.closingDebit - data.totals.closingCredit} />
        </div>
      )}
      {data.monthly.length > 1 && (
        <Section title="Обороты по месяцам">
          <MonthBars
            months={data.monthly.map((m) => m.month)}
            series={[
              { label: "Дебет", values: data.monthly.map((m) => m.debit), className: "fill-accent/70" },
              { label: "Кредит", values: data.monthly.map((m) => m.credit), className: "fill-ink-3/40" },
            ]}
          />
        </Section>
      )}
      <Section title="Сальдо по аналитике" hint="на конец последнего месяца с остатками 1С; первое субконто счёта">
        {data.breakdown.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Остатков по аналитике нет.")}</p>
        ) : (
          <TableBox>
            <table className="w-full text-sm">
              <thead>
                <tr>
                  <th className={th}>{t("Аналитика")}</th>
                  <th className={`${th} text-right`}>{t("Количество")}</th>
                  <th className={`${th} text-right`}>{t("В валюте")}</th>
                  <th className={`${th} text-right`}>{t("Сальдо")}</th>
                </tr>
              </thead>
              <tbody>
                {data.breakdown.map((b, i) => (
                  <tr key={`${b.id}-${b.currency}-${i}`}>
                    <td className={td}>
                      {b.kind === "counterparty" && b.id ? (
                        <Link href={link("/finance/entries", { counterparty: b.id, account: data.account })} className="hover:text-accent-strong hover:underline">
                          {b.name}
                        </Link>
                      ) : (
                        b.name
                      )}
                    </td>
                    <td className={`${td} ${num}`}>{b.quantity != null ? full(b.quantity, 3) : "—"}</td>
                    <td className={`${td} ${num}`}>{b.currencyAmount != null ? `${full(b.currencyAmount, 2)} ${b.currency}` : "—"}</td>
                    <td className={`${td} ${num} ${b.closing < 0 ? "text-bad" : ""}`}>{full(b.closing)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableBox>
        )}
      </Section>
    </div>
  );
}

// ---------- Проводки и документ ----------

function EntryRows({ items, showDocument = true }: { items: EntryView[]; showDocument?: boolean }) {
  const t = useT();
  return (
    <TableBox>
      <table className="w-full min-w-[760px] text-sm">
        <thead>
          <tr>
            <th className={th}>{t("Дата")}</th>
            <th className={th}>{t("Дебет")}</th>
            <th className={th}>{t("Кредит")}</th>
            <th className={`${th} text-right`}>{t("Сумма")}</th>
            <th className={th}>{t("Содержание и аналитика")}</th>
            {showDocument && <th className={th}>{t("Документ")}</th>}
          </tr>
        </thead>
        <tbody>
          {items.map((e) => (
            <tr key={e.id}>
              <td className={`${td} whitespace-nowrap`}>{date(e.date)}</td>
              <td className={td} title={e.debitName ?? undefined}>
                <span className="font-medium tabular-nums">{e.debitAccount}</span>
              </td>
              <td className={td} title={e.creditName ?? undefined}>
                <span className="font-medium tabular-nums">{e.creditAccount}</span>
              </td>
              <td className={`${td} ${num}`}>
                {full(e.amount)}
                {e.currencyAmount != null && e.currencyAmount !== 0 && <div className="text-xs text-ink-3">{`${full(e.currencyAmount, 2)} ${e.currency}`}</div>}
              </td>
              <td className={`${td} text-ink-2`}>
                {e.content && <div>{e.content}</div>}
                <div className="text-xs text-ink-3">{e.subconto.filter((s) => s.name).map((s) => s.name).join(" · ")}</div>
              </td>
              {showDocument && (
                <td className={td}>
                  {e.documentId ? (
                    <Link href={`/finance/documents/${e.documentId}`} className="whitespace-nowrap text-accent-strong hover:underline">
                      {t(docTypeLabel[e.registrarType] ?? e.registrarSourceType ?? e.registrarType)} № {e.registrarNumber}
                    </Link>
                  ) : (
                    <span className="text-ink-3">{e.registrarSourceType ?? e.registrarType} № {e.registrarNumber}</span>
                  )}
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </TableBox>
  );
}

export function EntriesView({ data, filters }: { data: FinanceEntries; filters: { account?: string; counterparty?: string; registrar?: string } }) {
  const t = useT();
  const link = useLink();
  const pathname = usePathname();
  const pages = Math.max(1, Math.ceil(data.total / data.pageSize));
  const go = (page: number) => link(pathname, { ...filters, page: String(page) });
  return (
    <div className="space-y-4">
      <MetaBar meta={data.meta} settingsLink />
      <div className="flex flex-wrap items-center gap-2 text-sm text-ink-2">
        {filters.account && <span className="rounded-full bg-muted px-2.5 py-1">{t("Счёт {code}", { code: filters.account })}</span>}
        {filters.counterparty && <span className="rounded-full bg-muted px-2.5 py-1">{data.items.find((i) => i.counterparty)?.counterparty ?? t("Контрагент")}</span>}
        {filters.registrar && <span className="rounded-full bg-muted px-2.5 py-1">{t("Один документ")}</span>}
        <span className="ml-auto tabular-nums">
          {t("{count} проводок на {amount}", { count: full(data.total), amount: full(data.amount) })}
        </span>
      </div>
      <Section title="Проводки 1С">
        {data.items.length === 0 ? <p className="text-sm text-ink-3">{t("Проводок нет.")}</p> : <EntryRows items={data.items} />}
        {pages > 1 && (
          <div className="mt-3 flex items-center justify-end gap-2 text-sm">
            {data.page > 1 && (
              <Link href={go(data.page - 1)} className="inline-flex items-center gap-1 rounded-lg border border-line px-3 py-1.5 hover:bg-muted">
                <ChevronLeft aria-hidden className="size-4" /> {t("Назад")}
              </Link>
            )}
            <span className="text-ink-3">{t("{page} из {pages}", { page: data.page, pages })}</span>
            {data.page < pages && (
              <Link href={go(data.page + 1)} className="inline-flex items-center gap-1 rounded-lg border border-line px-3 py-1.5 hover:bg-muted">
                {t("Дальше")} <ChevronRight aria-hidden className="size-4" />
              </Link>
            )}
          </div>
        )}
      </Section>
    </div>
  );
}

export function DocumentView({ doc }: { doc: FinanceDocument }) {
  const t = useT();
  const field = (label: string, v: React.ReactNode) =>
    v == null || v === "" ? null : (
      <div>
        <dt className="text-xs text-ink-3">{t(label)}</dt>
        <dd className="text-sm text-ink">{v}</dd>
      </div>
    );
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap gap-2 text-xs">
        <span className="rounded-full bg-accent-soft px-2.5 py-1 font-semibold text-accent-strong">{t("Факт · 1С")}</span>
        {!doc.posted && <span className="rounded-full bg-warn-soft px-2.5 py-1 font-semibold text-warn">{t("Не проведён")}</span>}
        {doc.deleted && <span className="rounded-full bg-bad-soft px-2.5 py-1 font-semibold text-bad">{t("Удалён в 1С")}</span>}
        {doc.changes > 0 && <span className="rounded-full bg-muted px-2.5 py-1 text-ink-2">{t("Изменений в 1С: {n}", { n: doc.changes })}</span>}
      </div>
      <Section title="Документ">
        <dl className="grid grid-cols-2 gap-x-6 gap-y-3 sm:grid-cols-3 lg:grid-cols-4">
          {field("Вид", `${t(docTypeLabel[doc.type] ?? doc.type)} (${doc.sourceType})`)}
          {field("Дата", date(doc.date))}
          {field("Организация", doc.organization)}
          {field("Контрагент", doc.counterparty)}
          {field("Договор", doc.contract)}
          {field("Склад", doc.warehouse)}
          {field("Сумма документа", `${full(doc.amount, 2)} ${doc.currency}`)}
          {field("НДС", doc.vatAmount != null ? full(doc.vatAmount, 2) : null)}
          {field("В валюте учёта", `${full(doc.amountAccounting, 2)} UZS`)}
          {field("Курс", doc.rate != null ? `${full(doc.rate, 4)}${doc.rateMultiplier && doc.rateMultiplier > 1 ? ` за ${doc.rateMultiplier}` : ""} · ${date(doc.rateDate)} · ${doc.rateSource ?? ""}` : null)}
          {field("Срок оплаты", doc.dueDate ? date(doc.dueDate) : null)}
          {field("Назначение", doc.purpose)}
          {field("Комментарий", doc.comment)}
          {field("Версия 1С", doc.version)}
        </dl>
      </Section>
      {doc.lines.length > 0 && (
        <Section title="Строки">
          <TableBox>
            <table className="w-full min-w-[600px] text-sm">
              <thead>
                <tr>
                  <th className={th}>№</th>
                  <th className={th}>{t("Номенклатура")}</th>
                  <th className={`${th} text-right`}>{t("Количество")}</th>
                  <th className={`${th} text-right`}>{t("Цена")}</th>
                  <th className={`${th} text-right`}>{t("Сумма")}</th>
                  <th className={`${th} text-right`}>{t("НДС")}</th>
                  <th className={`${th} text-right`}>{t("Себестоимость")}</th>
                </tr>
              </thead>
              <tbody>
                {doc.lines.map((l) => (
                  <tr key={l.line}>
                    <td className={td}>{l.line}</td>
                    <td className={td}>{l.item}</td>
                    <td className={`${td} ${num}`}>
                      {full(l.quantity, 3)} {l.unit}
                    </td>
                    <td className={`${td} ${num}`}>{l.price != null ? full(l.price, 2) : "—"}</td>
                    <td className={`${td} ${num}`}>{full(l.amount, 2)}</td>
                    <td className={`${td} ${num}`}>{l.vatAmount != null ? full(l.vatAmount, 2) : "—"}</td>
                    <td className={`${td} ${num}`}>{l.costAmount != null ? full(l.costAmount, 2) : "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableBox>
        </Section>
      )}
      <Section title="Проводки" hint={doc.posted ? undefined : "документ не проведён — проводок нет"}>
        {doc.entries.length === 0 ? <p className="text-sm text-ink-3">{t("Проводок нет.")}</p> : <EntryRows items={doc.entries} showDocument={false} />}
      </Section>
      <p className="text-xs text-ink-3">
        {t("Загружен из 1С {imported}, изменён {updated}. Изменить документ можно только в 1С — OneBase получит новую версию при следующей синхронизации.", {
          imported: date(doc.importedAt),
          updated: date(doc.updatedAt),
        })}
      </p>
    </div>
  );
}

// ---------- Продажи, закупки, склад, производство, валюты ----------

export function OperationsView({ data }: { data: FinanceOperations }) {
  const t = useT();
  const link = useLink();
  const sum = (a: number[]) => a.reduce((s, v) => s + v, 0);
  const rows = (list: AmountRow[]) => list.map((r) => ({ key: r.key, title: r.title, amount: r.amount, note: r.quantity != null ? `${full(r.quantity)} ед.` : undefined }));
  return (
    <div className="space-y-4">
      <MetaBar meta={data.meta} settingsLink />
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Kpi label="Продажи (документы)" value={sum(data.sales)} hint="реализация минус возвраты" />
        <Kpi label="Закупки (документы)" value={sum(data.purchases)} />
        <Kpi label="Себестоимость продаж" value={sum(data.costOfSales)} good="down" />
        <Kpi label="Затраты производства" value={sum(data.productionCosts)} good="down" hint="Дт 20xx" />
      </div>
      {data.months.length > 1 && (
        <Section title="Продажи, закупки и себестоимость по месяцам">
          <MonthBars
            months={data.months}
            series={[
              { label: "Продажи", values: data.sales, className: "fill-accent/70" },
              { label: "Закупки", values: data.purchases, className: "fill-warn/50" },
              { label: "Себестоимость", values: data.costOfSales, className: "fill-ink-3/40" },
            ]}
          />
        </Section>
      )}
      <div className="grid gap-4 lg:grid-cols-2">
        <Section title="Крупнейшие покупатели" className="!mt-0">
          <ShareBars rows={rows(data.topCustomers)} href={(k) => link("/finance/entries", { counterparty: k, account: "40" })} />
        </Section>
        <Section title="Крупнейшие поставщики" className="!mt-0">
          <ShareBars rows={rows(data.topSuppliers)} href={(k) => link("/finance/entries", { counterparty: k, account: "60" })} />
        </Section>
        <Section title="Продажи по номенклатуре" hint="сумма строк в сумах по курсу документа" className="!mt-0">
          <ShareBars rows={rows(data.topItems)} />
        </Section>
        <Section title="Себестоимость единицы по месяцам" hint="по строкам реализации" className="!mt-0">
          {data.unitCosts.length === 0 ? (
            <p className="text-sm text-ink-3">{t("Нет данных за период.")}</p>
          ) : (
            <TableBox>
              <table className="w-full text-sm">
                <thead>
                  <tr>
                    <th className={th}>{t("Товар")}</th>
                    {data.months.slice(-6).map((m) => (
                      <th key={m} className={`${th} text-right`}>
                        {monthShort(m)}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {data.unitCosts.map((u) => (
                    <tr key={u.itemId}>
                      <td className={td}>
                        {u.item}
                        {u.unit && <span className="text-xs text-ink-3"> · {t("за")} {u.unit}</span>}
                      </td>
                      {u.months.slice(-6).map((v, i) => (
                        <td key={i} className={`${td} ${num}`}>
                          {v != null ? full(v) : "—"}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </TableBox>
          )}
        </Section>
        <Section title="Запасы по складам" hint={data.balancesMonth ? `на конец ${monthShort(data.balancesMonth)}` : undefined} className="!mt-0">
          <ShareBars rows={data.inventoryByWarehouse.map((r) => ({ key: r.key, title: r.title, amount: r.amount }))} />
          {data.inventory.length > 0 && (
            <p className="mt-3 text-xs text-ink-3">{data.inventory.map((i) => `${t(i.title)}: ${money(i.amount)}`).join(" · ")}</p>
          )}
        </Section>
        <Section title="Выпуск продукции" hint="документы производства за период" className="!mt-0">
          <ShareBars rows={rows(data.production)} />
        </Section>
        <Section title="Продажи по валютам" hint="в валюте документа и в сумах по курсу 1С" className="!mt-0">
          <ul className="space-y-2 text-sm">
            {data.salesByCurrency.map((c) => (
              <li key={c.key} className="flex justify-between gap-3">
                <span className="font-medium">{c.key}</span>
                <span className="tabular-nums text-ink-2">
                  {c.key !== "UZS" && c.currencyAmount != null && <span className="mr-3">{`${full(c.currencyAmount, 2)} ${c.key}`}</span>}
                  {money(c.amount)}
                </span>
              </li>
            ))}
          </ul>
        </Section>
        <Section title="Курсы валют 1С" hint="за период" className="!mt-0">
          <ul className="space-y-2 text-sm">
            {data.rates.map((r) => (
              <li key={r.currency} className="flex justify-between gap-3">
                <span className="font-medium">{r.currency}</span>
                <span className="tabular-nums text-ink-2">
                  {full(r.first, 2)} → {full(r.last, 2)} {r.changePercent != null && <span className={r.changePercent >= 0 ? "text-ink" : "text-ink"}>({r.changePercent > 0 ? "+" : ""}{full(r.changePercent, 2)} %)</span>}
                  <span className="ml-2 text-xs text-ink-3">{r.source}</span>
                </span>
              </li>
            ))}
          </ul>
        </Section>
      </div>
    </div>
  );
}

// ---------- Отклонения ----------

export function AnomaliesView({ data }: { data: FinanceAnomalies }) {
  const t = useT();
  return (
    <div className="space-y-4">
      <MetaBar meta={data.meta} settingsLink />
      <Section title="Что стоит проверить" hint="сигналы, а не выводы: каждый — со ссылкой на исходные проводки или документ">
        {data.items.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Отклонений за период не найдено.")}</p>
        ) : (
          <ul className="divide-y divide-line">
            {data.items.map((a, i) => (
              <li key={i} className="flex items-start gap-3 py-3">
                <AlertTriangle aria-hidden className={`mt-0.5 size-4 shrink-0 ${a.severity >= 3 ? "text-bad" : a.severity === 2 ? "text-warn" : "text-ink-3"}`} />
                <div className="min-w-0 flex-1">
                  <div className="text-sm font-medium text-ink">{a.title}</div>
                  <div className="text-sm text-ink-2">{a.detail}</div>
                </div>
                <div className="shrink-0 text-right text-sm">
                  {a.amount != null && <div className="tabular-nums">{money(a.amount)}</div>}
                  {a.link && (
                    <Link href={a.link} className="text-xs text-accent-strong hover:underline">
                      {t("Открыть")}
                    </Link>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </Section>
      <Section title="Какие проверки выполнены">
        <ul className="list-disc space-y-1 pl-5 text-sm text-ink-2">
          {data.checks.map((c) => (
            <li key={c}>{t(c)}</li>
          ))}
        </ul>
      </Section>
    </div>
  );
}
