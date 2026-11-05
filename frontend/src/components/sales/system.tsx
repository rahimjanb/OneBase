"use client";

import Link from "next/link";
import { Fragment, useState } from "react";
import { ChevronRight } from "lucide-react";
import { Alert, KpiTile, Note } from "./bits";
import { Section } from "./Section";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { date, dayMonth, kg, monthShort, num, pct } from "@/lib/sales/format";
import type { SystemQualityRow, SystemRouteRow, SystemView } from "@/lib/sales/system";

// «Система»: не сколько продали, а как работает конвейер — маршрут, качество точки, деньги и запас дилера (как вкладка «Система»
// «Полевого контроля», по книгам Дорощука и Герасименко). Всё считает сервер (SystemService); страница раскрывает регионы.

const th = "whitespace-nowrap px-2.5 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
const thFirst = "whitespace-nowrap py-2 pl-4 pr-3 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3 sm:pl-5";
const td = "px-2.5 py-2 text-right tabular-nums";
const tdFirst = "py-2 pl-4 pr-3 sm:pl-5";
const sub = "block text-[11px] font-normal normal-case tracking-normal text-ink-3";
const ratio = (a: number, b: number) => (b ? a / b : null);
const mln = (v: number) => (Math.round(v / 1e6) ? num(v / 1e6) : "—");

/** Дорощук, гл. 23: заказы от плана маршрута ≥75% — отлично, ≥50% — средне, ≥40% — слабо, меньше — плохо. */
function Grade({ share, t }: { share: number | null; t: T }) {
  const [label, tone] =
    share == null
      ? ["нет маршрута", "bg-muted text-ink-3"]
      : share >= 0.75
        ? ["отлично", "bg-ok-soft text-ok"]
        : share >= 0.5
          ? ["средне", "bg-ok-soft text-ok"]
          : share >= 0.4
            ? ["слабо", "bg-warn-soft text-warn"]
            : ["плохо", "bg-bad-soft text-bad"];
  return <span className={`inline-block whitespace-nowrap rounded-full px-2 py-0.5 text-[11px] font-medium ${tone}`}>{t(label)}</span>;
}

export function SystemReport({ data }: { data: SystemView }) {
  const t = useT();
  const [open, setOpen] = useState<string | null>(null);
  const toggle = (id: string) => setOpen(open === id ? null : id);
  const r = data.routeTotal;
  const q = data.quality.total;
  const m = data.money;

  return (
    <>
      <div className={`grid grid-cols-2 gap-3 ${m ? "lg:grid-cols-5" : "lg:grid-cols-4"}`}>
        <KpiTile label={t("Заказы от плана маршрута")} value={pct(ratio(r.ordersInPlan, r.plan))} tone={tone(ratio(r.ordersInPlan, r.plan))}>
          {t("{n} заказов с плановых визитов из {plan} по плану · норма ≥75%", { n: num(r.ordersInPlan), plan: num(r.plan) })}
        </KpiTile>
        <KpiTile label={t("Посещено по плану")} value={pct(ratio(r.doneInPlan, r.plan))}>
          {t("{n} визитов по маршруту, ещё {off} вне плана", { n: num(r.doneInPlan), off: num(r.doneOffPlan) })}
        </KpiTile>
        <KpiTile label={t("Качественные точки")} value={pct(ratio(q.quality, q.akb))}>
          {t("≥{min} SKU ядра · {n} из {akb} АКБ", { min: data.quality.coreMin, n: num(q.quality), akb: num(q.akb) })}
        </KpiTile>
        <KpiTile label={t("Точки с 1 SKU")} value={num(q.oneSku)}>
          {t("{share} АКБ — кому добавить товар из ядра", { share: pct(ratio(q.oneSku, q.akb)) })}
        </KpiTile>
        {m && (
          <KpiTile label={t("Долг дилеров просрочен")} value={pct(ratio(m.overdue, m.debt))} tone={(ratio(m.overdue, m.debt) ?? 0) > 0.08 ? "bad" : "ink"}>
            {t("{overdue} из {debt} млн · резерв по шкале {reserve} млн", { overdue: mln(m.overdue), debt: mln(m.debt), reserve: mln(m.reserve) })}
          </KpiTile>
        )}
      </div>

      <Route data={data} open={open} onToggle={toggle} />
      <Quality data={data} open={open} onToggle={toggle} />
      {m ? (
        <Money data={data} />
      ) : data.moneyMissing ? (
        <Alert tone="info">
          {t("Денег и запаса дилеров нет: оплаты дилеров не привязаны. «Настройки → Источники данных → DSO»: «Оплаты дилеров», «Условия оплаты дилеров».")}{" "}
          <Link href="/settings/data-sources" className="font-medium text-accent hover:underline">
            {t("Открыть источники данных")}
          </Link>
        </Alert>
      ) : null}
    </>
  );
}

const tone = (share: number | null) => (share == null ? "ink" : share >= 0.5 ? "ok" : share >= 0.4 ? "warn" : "bad");

function Route({ data, open, onToggle }: { data: SystemView; open: string | null; onToggle: (id: string) => void }) {
  const t = useT();
  const share = (r: SystemRouteRow) => ratio(r.ordersInPlan, r.plan);
  const rows = [...data.route].sort((a, b) => (share(b) ?? -1) - (share(a) ?? -1) || b.orders - a.orders);
  const total = data.routeTotal;

  const row = (r: SystemRouteRow, child: boolean) => {
    const isOpen = !child && open === r.id;
    return (
      <tr
        key={`${child ? "a" : "r"}-${r.id}`}
        onClick={child ? undefined : () => onToggle(r.id)}
        className={`border-b border-line ${child ? "bg-muted/40" : "cursor-pointer hover:bg-muted"}`}
      >
        <td className={tdFirst}>
          {child ? (
            <span className="block pl-5 text-ink-2">{r.name}</span>
          ) : (
            <span className="flex min-w-[150px] items-center gap-1.5 font-medium text-ink">
              <ChevronRight className={`size-3.5 shrink-0 text-ink-3 transition-transform ${isOpen ? "rotate-90" : ""}`} />
              {r.name}
            </span>
          )}
        </td>
        <td className={td}>{r.plan ? num(r.plan) : "—"}</td>
        <td className={td}>{num(r.doneInPlan)}</td>
        <td className={`${td} text-ink-3`}>{pct(ratio(r.doneInPlan, r.plan))}</td>
        <td className={td}>{num(r.doneOffPlan)}</td>
        <td className={td}>{num(r.ordersInPlan)}</td>
        <td className={`${td} font-semibold text-ink`}>{pct(share(r))}</td>
        <td className="px-2.5 py-2">
          <Grade share={r.plan ? share(r) : null} t={t} />
        </td>
        <td className={`${td} pr-4 sm:pr-5`}>{num(r.orders)}</td>
      </tr>
    );
  };

  return (
    <Section title={t("Маршрут: заказы от плана")} hint={t("нажмите на регион — ТП · по {day}", { day: dayMonth(data.period.dataThrough) })} className="mt-4">
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className={thFirst}>{t("Регион / ТП")}</th>
              <th className={th}>{t("План визитов")}</th>
              <th className={th}>{t("Посещено по плану")}</th>
              <th className={th}>%</th>
              <th className={th}>{t("Вне плана")}</th>
              <th className={th}>{t("Заказы с план. визитов")}</th>
              <th className={th}>{t("Заказы от плана")}</th>
              <th className={`${th} text-left`}>{t("Оценка")}</th>
              <th className={`${th} pr-4 sm:pr-5`}>{t("Всего заказов")}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <Fragment key={r.id}>
                {row(r, false)}
                {open === r.id && r.agents.map((a) => row(a, true))}
              </Fragment>
            ))}
          </tbody>
          <tfoot>
            <tr className="font-semibold text-ink">
              <td className={tdFirst}>{t("Итого")}</td>
              <td className={td}>{num(total.plan)}</td>
              <td className={td}>{num(total.doneInPlan)}</td>
              <td className={td}>{pct(ratio(total.doneInPlan, total.plan))}</td>
              <td className={td}>{num(total.doneOffPlan)}</td>
              <td className={td}>{num(total.ordersInPlan)}</td>
              <td className={td}>{pct(ratio(total.ordersInPlan, total.plan))}</td>
              <td className="px-2.5 py-2">
                <Grade share={total.plan ? ratio(total.ordersInPlan, total.plan) : null} t={t} />
              </td>
              <td className={`${td} pr-4 sm:pr-5`}>{num(total.orders)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
      <Note>
        {t(
          "По Дорощуку («Рабочая книга супервайзера», гл. 23): визиты по плану, на которых ТП взял заказ, делённые на плановые визиты маршрута. ≥75% — отлично, ≥50% — средне, меньше 40% — плохо. Страйк считается по фактическим визитам и не видит точек, в которые ТП не зашёл. План — маршрут в Linko (визиты «в плане» по отчётный день); заказ визита — заказ этого ТП в этой точке в день визита (по дню ввода). Регион без плана — маршрут в Linko не заведён.",
        )}
      </Note>
    </Section>
  );
}

function Quality({ data, open, onToggle }: { data: SystemView; open: string | null; onToggle: (id: string) => void }) {
  const t = useT();
  const q = data.quality;
  const share = (r: SystemQualityRow) => ratio(r.quality, r.akb) ?? 0;
  const rows = [...q.regions].sort((a, b) => share(b) - share(a));
  const months = q.months.map((k) => monthShort(Number(k.split("-")[1]), t));

  const row = (r: SystemQualityRow, child: boolean) => {
    const isOpen = !child && open === r.id;
    return (
      <tr
        key={`${child ? "a" : "r"}-${r.id}`}
        onClick={child ? undefined : () => onToggle(r.id)}
        className={`border-b border-line ${child ? "bg-muted/40" : "cursor-pointer hover:bg-muted"}`}
      >
        <td className={tdFirst}>
          {child ? (
            <span className="block pl-5 text-ink-2">{r.name}</span>
          ) : (
            <span className="flex min-w-[150px] items-center gap-1.5 font-medium text-ink">
              <ChevronRight className={`size-3.5 shrink-0 text-ink-3 transition-transform ${isOpen ? "rotate-90" : ""}`} />
              {r.name}
            </span>
          )}
        </td>
        <td className={td}>{num(r.akb)}</td>
        <td className={td}>{num(r.skuAvg, 1)}</td>
        <td className={`${td} font-semibold text-ink`}>{pct(ratio(r.quality, r.akb))}</td>
        <td className={td}>{pct(ratio(r.twoCategories, r.akb))}</td>
        <td className={td}>{num(r.oneSku)}</td>
        <td className={`${td} text-ink-3`}>{pct(ratio(r.oneSku, r.akb))}</td>
        <td className={td}>{pct(r.topShare)}</td>
        <td className={`${td} pr-4 sm:pr-5`}>{t("{n} из {all}", { n: num(r.topRegular), all: num(r.topCount) })}</td>
      </tr>
    );
  };

  return (
    <Section title={t("Качество дистрибуции")} hint={t("нажмите на регион — ТП")} className="mt-4">
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className={thFirst}>{t("Регион / ТП")}</th>
              <th className={th}>{t("АКБ")}</th>
              <th className={th}>{t("SKU на точку")}</th>
              <th className={th}>{t("≥{min} SKU ядра", { min: q.coreMin })}</th>
              <th className={th}>{t("≥2 категорий")}</th>
              <th className={th}>{t("С 1 SKU")}</th>
              <th className={th}>%</th>
              <th className={th}>{t("Кг у верхних 20%")}</th>
              <th className={`${th} pr-4 sm:pr-5`}>{t("Верхние 20% брали {n} мес.", { n: q.months.length })}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <Fragment key={r.id}>
                {row(r, false)}
                {open === r.id && r.agents.map((a) => row(a, true))}
              </Fragment>
            ))}
          </tbody>
          <tfoot>
            <tr className="font-semibold text-ink">
              <td className={tdFirst}>{t("Итого")}</td>
              <td className={td}>{num(q.total.akb)}</td>
              <td className={td}>{num(q.total.skuAvg, 1)}</td>
              <td className={td}>{pct(ratio(q.total.quality, q.total.akb))}</td>
              <td className={td}>{pct(ratio(q.total.twoCategories, q.total.akb))}</td>
              <td className={td}>{num(q.total.oneSku)}</td>
              <td className={td}>{pct(ratio(q.total.oneSku, q.total.akb))}</td>
              <td className={td}>{pct(q.total.topShare)}</td>
              <td className={`${td} pr-4 sm:pr-5`}>{t("{n} из {all}", { n: num(q.total.topRegular), all: num(q.total.topCount) })}</td>
            </tr>
          </tfoot>
        </table>
      </div>
      <Note>
        {t(
          "По Дорощуку («Дистрибуция на практике»): рост даёт не только число точек, но и стабильное присутствие ключевых позиций. Ядро — {n} SKU, которые дают {share} кг своей категории за {months} по стране (Парето). Качественная точка — взяла за месяц не меньше {min} SKU ядра. Точки с 1 SKU — первые кандидаты «добавить из ядра». Верхние 20% — точки с наибольшим весом за месяц (весовая дистрибуция): какая доля кг на них и сколько из них брали все {count} месяца. Итог республики — по всем строкам: точка двух регионов считается один раз.",
          { n: q.core.length, share: pct(q.coreShare), months: months.join("–"), min: q.coreMin, count: q.months.length },
        )}
      </Note>
      <details className="mt-2 text-xs text-ink-2">
        <summary className="cursor-pointer font-medium text-ink">{t("Список ядра")}</summary>
        <div className="mt-2 gap-x-6 sm:columns-2">
          {q.core.map((c) => (
            <div key={c.productId} className="break-inside-avoid py-0.5">
              {c.name} <span className="text-ink-3">· {t(c.category)}</span>
            </div>
          ))}
        </div>
      </details>
    </Section>
  );
}

function Money({ data }: { data: SystemView }) {
  const t = useT();
  const m = data.money!;
  const totals = m.buckets.map((_, i) => m.dealers.reduce((a, d) => a + d.aging[i], 0));
  const from = dayMonth(m.stockFrom);

  return (
    <Section title={t("Деньги и запас дилеров")} hint={t("долг на {date}, млн сум", { date: dayMonth(m.asOf) })} className="mt-4">
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line align-bottom">
              <th className={thFirst}>{t("Дилер")}</th>
              <th className={`${th} text-left`}>{t("Регион")}</th>
              <th className={th}>{t("Долг")}</th>
              <th className={th}>{t("Просрочено")}</th>
              <th className={th}>%</th>
              {m.buckets.map((b, i) => (
                <th key={b.label} className={th}>
                  {t("{label} дн.", { label: b.label })}
                  <span className={sub}>{pct(m.shares[i], 1)}</span>
                </th>
              ))}
              <th className={th}>{t("Резерв")}</th>
              <th className={th}>{t("DSO факт / норма")}</th>
              <th className={th}>
                {t("Запас, дней")}
                <span className={sub}>{t("на {date} → сейчас", { date: from })}</span>
              </th>
              <th className={th}>
                {t("Первичка / вторичка")}
                <span className={sub}>{t("кг с {date}", { date: from })}</span>
              </th>
              <th className={`${th} pr-4 sm:pr-5`} />
            </tr>
          </thead>
          <tbody>
            {m.dealers.map((d) => (
              <tr key={`${d.name}-${d.region ?? ""}`} className="border-b border-line hover:bg-muted">
                <td className={`${tdFirst} font-medium text-ink`}>
                  {d.name}
                  {d.status === "gone" && <span className="ml-1.5 rounded-full bg-muted px-2 py-0.5 text-[11px] font-medium text-ink-3">{t("ушёл")}</span>}
                </td>
                <td className="whitespace-nowrap px-2.5 py-2 text-ink-2">{d.region ?? "—"}</td>
                <td className={`${td} font-semibold text-ink`}>{mln(d.debt)}</td>
                <td className={`${td} ${Math.round(d.overdue / 1e6) ? "font-semibold text-bad" : "text-ink-3"}`}>{mln(d.overdue)}</td>
                <td className={td}>{pct(ratio(d.overdue, d.debt))}</td>
                {d.aging.map((v, i) => (
                  <td key={i} className={`${td} ${i >= 2 && Math.round(v / 1e6) ? "text-bad" : ""}`}>
                    {mln(v)}
                  </td>
                ))}
                <td className={td}>{mln(d.reserve)}</td>
                <td className={td}>
                  {d.factDso != null ? num(d.factDso) : "—"} / {d.norm != null ? `${num(d.norm)}${d.defaultNorm ? "*" : ""}` : "—"}
                </td>
                <td className={td}>{d.days1 != null || d.daysNow != null ? `${num(d.days1)} → ${num(d.daysNow)}` : "—"}</td>
                <td className={td}>{d.primaryKg != null && d.secondaryKg != null ? `${kg(d.primaryKg)} / ${kg(d.secondaryKg)}` : "—"}</td>
                <td className="px-2.5 py-2 pr-4 sm:pr-5">
                  {d.overstock && (
                    <span className="whitespace-nowrap rounded-full bg-warn-soft px-2 py-0.5 text-[11px] font-medium text-warn">{t("затоваривание?")}</span>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="font-semibold text-ink">
              <td className={tdFirst}>{t("Итого")}</td>
              <td />
              <td className={td}>{mln(m.debt)}</td>
              <td className={td}>{mln(m.overdue)}</td>
              <td className={td}>{pct(ratio(m.overdue, m.debt))}</td>
              {totals.map((v, i) => (
                <td key={i} className={td}>
                  {mln(v)}
                </td>
              ))}
              <td className={td}>{mln(m.reserve)}</td>
              <td colSpan={4} />
            </tr>
          </tfoot>
        </table>
      </div>
      <Note>
        {t(
          "По Герасименко («Бизнес, финотчётность и не только», гл. 4, 5, 20). Долг — DSO на {date}, разложен по возрасту неоплаченной отгрузки (корзины старения). Резерв — сколько из долга, скорее всего, не вернётся: проценты под корзинами взяты из примера книги — своей истории невозврата у нас нет, это ориентир, а не прогноз. Просрочено — срок оплаты по норме дилера прошёл (по Дорощуку: хорошо 5–8%, плохо 40%+). Запас — остаток склада дилера в днях его продаж: на {from} и сейчас (снимок Linko {at}); Linko истории остатков не отдаёт, остаток на {from} восстановлен: сейчас + продано с {from} − отгружено заводом с {from}. «Затоваривание?» — первичка больше вторички на 15%+ и запас в днях вырос: дилера грузят быстрее, чем он продаёт, — ранний сигнал будущей просрочки. * — норма оплаты не задана, взята по умолчанию.",
          { date: date(m.asOf), from, at: m.stockAt ? date(m.stockAt.slice(0, 10)) : "—" },
        )}
      </Note>
    </Section>
  );
}
