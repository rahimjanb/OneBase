"use client";

import Link from "next/link";
import { useRef, useState } from "react";
import { ChevronRight, Database, X } from "lucide-react";
import { Alert, KpiTile, Note } from "./bits";
import { CopyButton } from "./DataTable";
import { MonthSelect } from "./ScopeSelect";
import { Section } from "./Section";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { date, money, monthLabel, num, tsvValue } from "@/lib/sales/format";
import type { AktLine, AktRow, AktView } from "@/lib/sales/settlements";

// «Акт сверки»: строки контрагентов за месяц — остаток, отгрузка и возврат (Linko), касса и банк, расходы по статьям (реестры Google Sheets),
// остаток на конец. Расчёт на сервере (AktCompute); клик по строке — расшифровка контрагента.

const amount = (v: number) => (Math.round(v) ? num(v) : "—");
/** Целые сумы без «−0» у копеечных остатков. */
const whole = (v: number) => num(Math.round(v) || 0);

export function AktReport({ data }: { data: AktView }) {
  const t = useT();
  const options = data.months.map((m) => ({
    value: `${m.year}-${m.month}`,
    label: `${monthLabel(m.year, m.month, t).replace(/^\S+/, (w) => t(w))}${m.partial ? ` ${t("(по {day}-е)", { day: Number(data.asOf.slice(8, 10)) })}` : ""}`,
  }));
  const all = data.groups.flatMap((g) => g.rows);
  const sum = (f: (r: AktRow) => number) => all.reduce((a, r) => a + f(r), 0);

  if (!data.hasPayments && !data.hasBalances) {
    return (
      <Alert tone="info" className="mt-2">
        {t("Оплат и остатков дилеров в OneBase ещё нет — в Linko их нет. Привяжите таблицы: «Настройки → Источники данных → Акт сверки»: «Оплаты дилеров» (Kassa Bank), «Расходы по дилерам» (Прочие расходы), «Остатки дилеров на дату» (акт бухгалтерии).")}{" "}
        <Link href="/settings/data-sources" className="font-medium text-accent hover:underline">
          {t("Открыть источники данных")}
        </Link>
      </Alert>
    );
  }

  return (
    <>
      <div className="flex flex-wrap items-center gap-3">
        <MonthSelect label={t("Месяц")} value={`${data.month.year}-${data.month.month}`} options={options} />
        <span className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-3">
          <Database className="size-3.5" aria-hidden />
          {data.anchorDate && <span>{t("остаток — акт бухгалтерии на {date}", { date: date(data.anchorDate) })}</span>}
          {data.paymentsTo && <span>{t("оплаты по {date}", { date: date(data.paymentsTo) })}</span>}
          {data.expensesTo && <span>{t("расходы по {date}", { date: date(data.expensesTo) })}</span>}
          <span>{t("отгрузки Linko по {date}", { date: date(data.asOf) })}</span>
        </span>
      </div>

      <div className="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile label={t("Остаток на начало")} value={money(sum((r) => r.open), t)} unit={t("сум")}>
          {t("долг контрагентов заводу на 1-е число")}
        </KpiTile>
        <KpiTile label={t("Отгрузка")} value={money(sum((r) => r.ship), t)} unit={t("сум")}>
          {t("возврат {sum}", { sum: money(sum((r) => r.return), t) })}
        </KpiTile>
        <KpiTile label={t("Оплаты")} value={money(sum((r) => r.cash + r.bank), t)} unit={t("сум")}>
          {t("касса {cash} · банк {bank}", { cash: money(sum((r) => r.cash), t), bank: money(sum((r) => r.bank), t) })}
        </KpiTile>
        <KpiTile label={t("Остаток на конец")} value={money(sum((r) => r.close), t)} unit={t("сум")} tone="ink">
          {t("расходы завода на контрагентов {sum}", { sum: money(sum((r) => r.total), t) })}
        </KpiTile>
      </div>

      {data.checks.length > 0 && (
        <Alert tone="warn">
          <div className="font-semibold text-ink">{t("Проверки")}</div>
          <ul className="mt-1 space-y-1">
            {data.checks.map((c) => (
              <li key={c.kind}>
                {checkText(c, data, t)}
                {c.items.length > 0 && <span className="block text-xs text-ink-3">{c.items.slice(0, 12).join(", ")}{c.items.length > 12 ? " …" : ""}</span>}
              </li>
            ))}
          </ul>
        </Alert>
      )}

      {data.groups.map((g) => (
        <AktGroupTable key={g.key} data={data} group={g} />
      ))}

      <Note>
        {t(
          "Остаток на конец = остаток на начало + отгрузка − возврат − касса − банк − расходы завода на контрагента. Отгрузка и возврат — первичка из Linko (перемещения на склады дилеров и заказы прямых клиентов, сумма Linko); касса и банк — реестр оплат (Kassa Bank); расходы — реестр «Прочие расходы» по статьям: 601 — ретробонус, 602 — ЗП супервайзеров, 603/604 — прочие, 606 — логистика, 609 — скидки и акции; кешбэк 2% — за отгрузки, оплаченные (FIFO) не позже нормы, ретробонус — 601 за вычетом кешбэка. Остаток на начало месяца акта бухгалтерии — опора; остальные месяцы — по цепочке.",
        )}
      </Note>
    </>
  );
}

function AktGroupTable({ data, group }: { data: AktView; group: AktView["groups"][number] }) {
  const t = useT();
  const [open, setOpen] = useState<string | null>(null);
  const panel = useRef<HTMLDivElement>(null);
  const openRow = group.rows.find((r) => r.key === open);
  const openCard = data.cards.find((c) => c.key === open);
  // Расшифровка — под таблицей группы на всю ширину карточки (внутри широкой таблицы она уезжала бы за край прокрутки).
  const toggle = (key: string) => {
    setOpen(open === key ? null : key);
    if (open !== key) requestAnimationFrame(() => panel.current?.scrollIntoView({ behavior: "smooth", block: "nearest" }));
  };
  const th = "whitespace-nowrap px-3 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const td = "px-3 py-2 text-right tabular-nums";
  const copy = () =>
    [
      [t("Контрагент"), t("Остаток на начало"), t("Отгрузка"), t("Возврат"), t("Касса"), t("Банк"), ...data.arts.map((a) => t(a.label)), t("Итого расходов"), t("Остаток на конец")].join("\t"),
      ...[...group.rows, group.totals].map((r) =>
        [r.key === "total" ? t("Итого") : r.name, r.open, r.ship, r.return, r.cash, r.bank, ...data.arts.map((a) => r.expenses[a.key] ?? 0), r.total, r.close]
          .map((v) => tsvValue(v))
          .join("\t"),
      ),
    ].join("\n");
  const cells = (r: AktRow, bold = false) => (
    <>
      <td className={`${td} border-l border-line`}>{amount(r.open)}</td>
      <td className={td}>{amount(r.ship)}</td>
      <td className={td}>{amount(r.return)}</td>
      <td className={td}>{amount(r.cash)}</td>
      <td className={td}>{amount(r.bank)}</td>
      {data.arts.map((a) => (
        <td key={a.key} className={`${td} ${a.key === "cashback" ? "border-l border-line" : ""} text-ink-2`}>
          {amount(r.expenses[a.key] ?? 0)}
        </td>
      ))}
      <td className={`${td} ${bold ? "" : "font-medium"}`}>{amount(r.total)}</td>
      <td className={`${td} border-l-2 border-line pr-4 font-semibold sm:pr-5 ${Math.round(r.close) < 0 ? "text-ok" : "text-ink"}`}>{whole(r.close)}</td>
    </>
  );
  return (
    <Section title={t(group.name)} hint={t("нажмите на строку — расшифровка: отгрузки, оплаты и расходы")} actions={<CopyButton onCopy={copy} />}>
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className="sticky left-0 z-[1] whitespace-nowrap bg-surface-solid py-2 pl-4 pr-3 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3 sm:pl-5">{t("Контрагент")}</th>
              <th className={`${th} border-l border-line`}>{t("Остаток на начало")}</th>
              <th className={th}>{t("Отгрузка")}</th>
              <th className={th}>{t("Возврат")}</th>
              <th className={th}>{t("Касса")}</th>
              <th className={th}>{t("Банк")}</th>
              {data.arts.map((a) => (
                <th key={a.key} className={`${th} ${a.key === "cashback" ? "border-l border-line" : ""}`}>
                  {t(a.label)}
                </th>
              ))}
              <th className={th}>{t("Итого расходов")}</th>
              <th className={`${th} border-l-2 border-line pr-4 sm:pr-5`}>{t("Остаток на конец")}</th>
            </tr>
          </thead>
          <tbody>
            {group.rows.map((r) => (
              <tr
                key={r.key}
                onClick={() => toggle(r.key)}
                aria-expanded={open === r.key}
                className={`group/row cursor-pointer border-b border-line hover:bg-muted ${open === r.key ? "bg-accent/5" : ""}`}
              >
                <td className="sticky left-0 z-[1] bg-surface-solid px-3 py-2 pl-4 group-hover/row:bg-muted-solid sm:pl-5">
                  <span className="flex min-w-[200px] items-center gap-1.5 font-medium text-ink">
                    <ChevronRight className={`size-3.5 shrink-0 text-ink-3 transition-transform ${open === r.key ? "rotate-90 text-accent" : ""}`} />
                    {r.name}
                    {!r.anchored && <span title={t("остатка из акта бухгалтерии нет")} className="text-warn">*</span>}
                  </span>
                </td>
                {cells(r)}
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="font-semibold text-ink">
              <td className="sticky left-0 z-[1] bg-surface-solid px-3 py-2.5 pl-4 sm:pl-5">{t("Итого")}</td>
              {cells(group.totals, true)}
            </tr>
          </tfoot>
        </table>
      </div>
      {openRow && openCard && (
        <div ref={panel} className="mt-4 scroll-mt-24 rounded-xl border border-line bg-muted/40 p-4">
          <div className="mb-3 flex flex-wrap items-start justify-between gap-3">
            <div>
              <h3 className="text-sm font-semibold text-ink">{openRow.name}</h3>
              <p className="mt-0.5 text-xs text-ink-3">
                {t("остаток на начало {open} → на конец {close}", { open: money(openRow.open, t), close: money(openRow.close, t) })}
                {openCard.norm != null && ` · ${t("норма оплаты {n} дн.", { n: openCard.norm })}`}
              </p>
            </div>
            <button type="button" onClick={() => setOpen(null)} className="inline-flex h-8 items-center gap-1 rounded-lg border border-line bg-surface px-2.5 text-xs text-ink-2 hover:bg-muted max-lg:h-11">
              <X className="size-3.5" aria-hidden />
              {t("Закрыть")}
            </button>
          </div>
          <AktCard card={openCard} row={openRow} />
        </div>
      )}
    </Section>
  );
}

const kindLabel: Record<string, string> = {
  ship: "Отгрузка",
  return: "Возврат",
  cash: "Касса",
  bank: "Банк",
  cashback: "2% кешбэк",
  retro: "Ретробонус",
  promo: "Скидки и акции",
  logi: "Логистика",
  salary: "ЗП супервайзеров",
  other: "Прочие расходы",
};

function AktCard({ card, row }: { card: AktView["cards"][number]; row: AktRow }) {
  const t = useT();
  return (
    <div className="grid gap-4 lg:grid-cols-3">
      <Lines title={t("Отгрузки и возвраты (Linko)")} lines={card.docs} t={t} />
      <Lines title={t("Оплаты (Kassa Bank)")} lines={card.payments} t={t} />
      <div>
        <Lines title={t("Расходы (реестр)")} lines={card.expenses} t={t} code />
        {card.cashback.length > 0 && (
          <div className="mt-3">
            <Lines title={t("Кешбэк 2% по FIFO (норма {n} дн.)", { n: card.norm ?? 20 })} lines={card.cashback} t={t} />
          </div>
        )}
        {row.cashbackPending != null && (
          <p className="mt-2 text-xs text-warn">{t("Кешбэк больше проведённого по 601 на {sum} — разница в расходах акта.", { sum: num(row.cashbackPending) })}</p>
        )}
      </div>
    </div>
  );
}

function Lines({ title, lines, t, code = false }: { title: string; lines: AktLine[]; t: T; code?: boolean }) {
  const total = lines.reduce((a, l) => a + (l.kind === "return" ? -l.amount : l.amount), 0);
  return (
    <div>
      <div className="mb-1 flex items-baseline justify-between gap-2 text-xs font-semibold text-ink-2">
        <span>{title}</span>
        <span className="tabular-nums">{lines.length ? num(total) : "—"}</span>
      </div>
      {lines.length === 0 ? (
        <p className="text-xs text-ink-3">{t("нет")}</p>
      ) : (
        <ul className="max-h-[260px] space-y-1 overflow-auto pr-1 text-xs">
          {lines.map((l, i) => (
            <li key={i} className="flex gap-2">
              <span className="w-11 shrink-0 tabular-nums text-ink-3">{date(l.date).slice(0, 5)}</span>
              <span className="min-w-0 flex-1 text-ink-2">
                <span className="text-ink">{t(kindLabel[l.kind] ?? l.kind)}</span>
                {code && l.code && <span className="text-ink-3"> · {l.code}</span>}
                {l.note && <span className="block truncate text-ink-3" title={l.note}>{docLabel(l.note, t)}</span>}
              </span>
              <span className={`shrink-0 tabular-nums ${l.kind === "return" ? "text-ok" : "text-ink"}`}>{l.kind === "return" ? "−" : ""}{num(l.amount)}</span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** Документ первички: «t:2193» → «перемещение № 2193», «o:…» → «заказ № …»; остальное (комментарии реестров) — как есть. */
function docLabel(note: string, t: T): string {
  const m = /^([to]):(\d+)$/.exec(note);
  return m ? t(m[1] === "t" ? "перемещение № {n}" : "заказ № {n}", { n: m[2] }) : note;
}

/** Текст проверки по её виду — переводится; неизвестный вид — текст сервера. */
function checkText(c: AktView["checks"][number], data: AktView, t: T): string {
  const n = c.items.length;
  const month = monthLabel(data.month.year, data.month.month, t).toLowerCase();
  switch (c.kind) {
    case "expenses":
      return c.date && c.date < `${data.month.year}-${String(data.month.month).padStart(2, "0")}-01`
        ? t("Расходы за {month} в реестр ещё не внесены (последняя строка — {date}): расходы месяца — только кешбэк по FIFO.", { month, date: date(c.date) })
        : t("Реестр расходов внесён по {date}: расходы после этой даты в акт ещё не попали.", { date: date(c.date) });
    case "anchor":
      return t("Без остатка из акта бухгалтерии — остаток считается от начала реестров (может отличаться): {n}.", { n });
    case "unmatched":
      return t("Отгрузки Linko, а в таблицах контрагента с таким именем нет (оплаты и остаток не найдены): {n}.", { n });
    case "nolinko":
      return t("Оплаты есть, а отгрузок Linko под этим именем нет (контрагент таблиц не сопоставлен с дилером региона или клиентом): {n}.", { n });
    case "outside":
      return t("Отгрузки или оплаты месяца у контрагентов, которых нет в акте бухгалтерии (в акт не входят): {n}.", { n });
    case "cashback":
      return t("Кешбэк по FIFO больше, чем проведено по статье 601 (разница добавлена в расходы акта): {n}.", { n });
    default:
      return c.text;
  }
}
