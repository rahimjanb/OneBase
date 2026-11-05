"use client";

import Link from "next/link";
import { useState } from "react";
import { Database } from "lucide-react";
import { KpiTile, Note } from "./bits";
import { CopyButton } from "./DataTable";
import { Section } from "./Section";
import { useT } from "@/i18n/client";
import { date, dayMonth, kg, monthShort, num, pct, tsvValue } from "@/lib/sales/format";
import type { DmpOrders, DmpView } from "@/lib/sales/dmp";

// «ДМП»: стеллажи в магазинах — как вкладка «ДМП» «Полевого контроля». Путь стеллажа: завод → склад дилера (перемещение Linko) → магазин
// (заказ со строкой стеллажа). Всё считает сервер (DmpCompute); страница только отбирает магазины дилера.

const th = "whitespace-nowrap px-2.5 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
const thFirst = "whitespace-nowrap py-2 pl-4 pr-3 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3 sm:pl-5";
const thText = "whitespace-nowrap px-2.5 py-2 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3";
const td = "px-2.5 py-2 text-right tabular-nums";
const tdFirst = "py-2 pl-4 pr-3 sm:pl-5";
const sub = "block text-[11px] font-normal text-ink-3";
const pcs = (v: number) => (v ? num(v, Number.isInteger(v) ? 0 : 2) : "—");
const tsv = (rows: (string | number | null)[][]) => rows.map((r) => r.map(tsvValue).join("\t")).join("\n");

export function DmpReport({ data }: { data: DmpView }) {
  const t = useT();
  const [region, setRegion] = useState<string | null>(null);
  const focus = data.focusCategory;

  return (
    <>
      <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-ink-3">
        <Database className="size-3.5" aria-hidden />
        <span>
          {t("перемещения, остатки и заказы Linko по {date}", {
            date: date(data.asOf),
          })}
        </span>
        <span>{data.products.join(", ")}</span>
      </p>

      <div className="mt-3 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile label={t("Получено дилерами")} value={pcs(data.received)} unit={t("шт")}>
          {t("с завода, по приёмке дилера")}
        </KpiTile>
        <KpiTile label={t("На складах дилеров")} value={pcs(data.left)} unit={t("шт")}>
          {t("остаток Linko · на складе завода {n} шт", {
            n: pcs(data.factoryLeft),
          })}
        </KpiTile>
        <KpiTile label={t("Установлено в магазины")} value={pcs(data.installed)} unit={t("шт")} tone={data.installed > 0 ? "ink" : "bad"}>
          {t("{share} от полученного · {n} магазинов", {
            share: pct(data.received ? data.installed / data.received : null),
            n: num(data.shops),
          })}
        </KpiTile>
        <KpiTile label={t("Выдано, не подтверждено")} value={pcs(data.issued)} unit={t("шт")} tone={data.issued > 0 ? "warn" : "ink"}>
          {t("заказ «отдан», доставка не закрыта")}
        </KpiTile>
      </div>

      <Regions data={data} selected={region} onSelect={(r) => setRegion(region === r ? null : r)} />
      <Shops data={data} region={region} focus={focus} onAll={() => setRegion(null)} />
    </>
  );
}

function Regions({ data, selected, onSelect }: { data: DmpView; selected: string | null; onSelect: (region: string) => void }) {
  const t = useT();
  const share = (inst: number, recv: number) => (recv ? pct(inst / recv) : "—");

  return (
    <Section title={t("По дилерам")} hint={t("нажмите на дилера — его магазины")} className="mt-4">
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className={`${thFirst} sticky left-0 z-[1] bg-surface-solid`}>{t("Дилер (склад)")}</th>
              <th className={th}>{t("Получено")}</th>
              <th className={th}>{t("Дата прихода")}</th>
              <th className={th}>{t("На складе")}</th>
              <th className={th}>{t("Установлено")}</th>
              <th className={th}>{t("% установлено")}</th>
              <th className={th}>{t("Выдано, не подтв.")}</th>
              <th className={`${th} pr-4 sm:pr-5`}>{t("Магазинов")}</th>
            </tr>
          </thead>
          <tbody>
            {data.regions.map((r) => {
              const on = selected === r.region;
              return (
                <tr
                  key={r.region}
                  onClick={() => onSelect(r.region)}
                  aria-selected={on}
                  className={`group/row cursor-pointer border-b border-line ${on ? "bg-accent-soft" : "hover:bg-muted"}`}
                >
                  <td
                    className={`${tdFirst} sticky left-0 z-[1] font-medium text-ink ${on ? "bg-accent-soft" : "bg-surface-solid group-hover/row:bg-muted-solid"}`}
                  >
                    {r.region}
                  </td>
                  <td className={td}>{pcs(r.received)}</td>
                  <td className={`${td} text-ink-3`}>{r.receivedDates.length ? r.receivedDates.map(dayMonth).join(", ") : "—"}</td>
                  <td className={td}>{pcs(r.left)}</td>
                  <td className={`${td} font-semibold text-ink`}>{pcs(r.installed)}</td>
                  <td className={`${td} ${r.received && !r.installed ? "text-bad" : ""}`}>{share(r.installed, r.received)}</td>
                  <td className={`${td} ${r.issued ? "text-warn" : ""}`}>{pcs(r.issued)}</td>
                  <td className={`${td} pr-4 sm:pr-5`}>{r.shops ? num(r.shops) : "—"}</td>
                </tr>
              );
            })}
          </tbody>
          <tfoot>
            <tr className="font-semibold text-ink">
              <td className={`${tdFirst} sticky left-0 z-[1] bg-surface-solid`}>{t("Итого")}</td>
              <td className={td}>{pcs(data.received)}</td>
              <td className={td} />
              <td className={td}>{pcs(data.left)}</td>
              <td className={td}>{pcs(data.installed)}</td>
              <td className={td}>{share(data.installed, data.received)}</td>
              <td className={td}>{pcs(data.issued)}</td>
              <td className={`${td} pr-4 sm:pr-5`}>{num(data.shops)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </Section>
  );
}

function Shops({ data, region, focus, onAll }: { data: DmpView; region: string | null; focus: string | null; onAll: () => void }) {
  const t = useT();
  const shops = data.shopRows.filter((s) => !region || s.region === region);
  const focusName = focus ? t(focus) : null;
  const window = (o: DmpOrders) =>
    o.orders || o.kg ? (
      <>
        {num(o.orders)} / {kg(o.kg)}
        {focusName && (
          <>
            {" "}
            / <b className="font-semibold text-ink">{kg(o.focusKg)}</b>
          </>
        )}
      </>
    ) : (
      <span className="text-ink-3">—</span>
    );
  const parts = focusName ? t("заказов / кг / {focus}", { focus: focusName }) : t("заказов / кг");
  const copy = () =>
    tsv([
      [
        t("Дилер"),
        t("ТП"),
        t("Магазин"),
        t("Установлен"),
        t("Шт"),
        t("Выдано"),
        t("Наполнение, кг"),
        ...(focusName ? [t("Наполнение {focus}, кг", { focus: focusName })] : []),
        t("Дней в окне"),
        t("До: заказов"),
        t("До: кг"),
        t("После: заказов"),
        t("После: кг"),
        t("Посл. заказ"),
        ...data.months.map((m) => t("{month}, кг", { month: monthShort(m.month, t) })),
      ],
      ...shops.map((s) => [
        s.region,
        s.agent ?? "",
        s.market,
        date(s.date),
        s.qty,
        s.issued,
        s.fill.kg,
        ...(focusName ? [s.fill.focusKg] : []),
        s.days,
        s.before.orders,
        s.before.kg,
        s.after.orders,
        s.after.kg,
        s.lastOrder ? date(s.lastOrder) : "",
        ...s.months.map((m) => m.kg),
      ]),
    ]);

  return (
    <Section
      title={region ? t("Магазины со стеллажом · {region}", { region }) : t("Магазины со стеллажом")}
      hint={t("{n} магазинов", { n: num(shops.length) })}
      className="mt-4"
      actions={
        <div className="flex items-center gap-2">
          {region && (
            <button type="button" onClick={onAll} className="rounded-lg border border-line px-2.5 py-1 text-xs font-medium text-ink hover:bg-muted">
              {t("Все дилеры")}
            </button>
          )}
          <CopyButton onCopy={copy} />
        </div>
      }
    >
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line align-bottom">
              <th className={thFirst}>{t("Дилер")}</th>
              <th className={thText}>{t("ТП")}</th>
              <th className={thText}>{t("Магазин")}</th>
              <th className={th}>{t("Установлен")}</th>
              <th className={th}>{t("Шт")}</th>
              <th className={`${th} border-l border-line`}>
                {t("Наполнение")}
                <span className={sub}>{focusName ? t("{focus} / всего, кг", { focus: focusName }) : t("всего, кг")}</span>
              </th>
              <th className={`${th} border-l border-line`}>
                {t("До установки")}
                <span className={sub}>{parts}</span>
              </th>
              <th className={th}>
                {t("После установки")}
                <span className={sub}>{parts}</span>
              </th>
              <th className={`${th} border-l border-line`}>{t("Посл. заказ")}</th>
              {data.months.map((m, i) => (
                <th key={`${m.year}-${m.month}`} className={`${th} border-l border-line ${i === data.months.length - 1 ? "pr-4 sm:pr-5" : ""}`}>
                  {t("{month}, кг", { month: monthShort(m.month, t) })}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {shops.map((s) => (
              <tr key={s.marketId} className="border-b border-line hover:bg-muted">
                <td className={`${tdFirst} whitespace-nowrap text-ink-2`}>{s.region}</td>
                <td className="whitespace-nowrap px-2.5 py-2 text-ink-2">
                  {s.agentId ? (
                    <Link href={`/sales/agents/${s.agentId}`} className="hover:text-accent hover:underline">
                      {s.agent ?? "—"}
                    </Link>
                  ) : (
                    "—"
                  )}
                </td>
                <td className="min-w-[180px] px-2.5 py-2 font-medium text-ink">
                  <Link href={`/sales/stores/${s.marketId}`} className="hover:text-accent hover:underline">
                    {s.market}
                  </Link>
                  {!s.qty && s.issued > 0 && (
                    <span className="ml-1.5 rounded-full bg-warn-soft px-2 py-0.5 text-[11px] font-medium text-warn">{t("выдан")}</span>
                  )}
                </td>
                <td className={td}>{dayMonth(s.date)}</td>
                <td className={td}>{pcs(s.qty || s.issued)}</td>
                <td className={`${td} border-l border-line`}>
                  {s.fill.kg ? focusName ? `${kg(s.fill.focusKg)} / ${kg(s.fill.kg)}` : kg(s.fill.kg) : <span className="text-ink-3">—</span>}
                </td>
                <td className={`${td} border-l border-line`}>
                  {window(s.before)}
                  <span className={sub}>{t("{n} дн.", { n: num(s.days) })}</span>
                </td>
                <td className={td}>
                  {window(s.after)}
                  <span className={sub}>{t("{n} дн.", { n: num(s.days) })}</span>
                </td>
                <td className={`${td} border-l border-line`}>
                  {s.lastOrder ? dayMonth(s.lastOrder) : "—"}
                  {s.idleDays != null && (
                    <span className={`${sub} ${s.idleDays > 14 ? "!text-bad" : ""}`}>
                      {s.idleDays === 0 ? t("сегодня") : t("{n} дн. назад", { n: num(s.idleDays) })}
                    </span>
                  )}
                </td>
                {s.months.map((m, i) => (
                  <td key={`${m.year}-${m.month}`} className={`${td} border-l border-line ${i === s.months.length - 1 ? "pr-4 sm:pr-5" : ""}`}>
                    {m.kg ? kg(m.kg) : <span className="text-ink-3">—</span>}
                    {focusName && m.focusKg ? (
                      <span className={sub}>
                        {t("{focus} {kg}", {
                          focus: focusName,
                          kg: kg(m.focusKg),
                        })}
                      </span>
                    ) : null}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <Note>
        {t(
          "Путь стеллажа: завод → склад дилера (перемещение Linko, по приёмке дилера) → магазин (заказ со строкой стеллажа: «доставлен» — установлен, «отдан» — выдан, доставка не подтверждена; возврат стеллажа вычитается). «Наполнение» — заказ, которым привезли стеллаж. «До» и «после» — заказы магазина за одинаковое число дней до и после установки (дней от установки до даты данных; заказ наполнения не входит ни в одно окно), возвраты вычтены, по дате реализации, как во вторичке. По месяцам — вес магазина за вычетом возвратов. Реестра «Склад → Работа с инвентарём» (серийные номера) внешний API Linko не отдаёт — в OneBase его нет.",
        )}
      </Note>
    </Section>
  );
}
