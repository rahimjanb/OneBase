"use client";

import Link from "next/link";
import { usePathname, useSearchParams } from "next/navigation";
import { useState } from "react";
import { Check, ChevronRight, Siren } from "lucide-react";
import { Alert, KpiTile, Note } from "./bits";
import { CopyButton, DataTable, type Column } from "./DataTable";
import { MonthSelect } from "./ScopeSelect";
import { Section } from "./Section";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { plural } from "@/lib/format";
import { dateTime, money, monthLabel, num, pct, tsvValue } from "@/lib/sales/format";
import { promoKindLabel, type PromoAgentRow, type PromoCalendarRow, type PromoRegionRow, type PromoView } from "@/lib/sales/promo";

// «Акции»: блоки как во вкладке «Акции» «Полевого контроля», расчёт — на сервере (PromoCompute). Фильтры — в адресе: месяц (year, month),
// РМ (rm) и отмеченные акции (promos) — клик по акции в «По акциям» или «Эффект акций» отмечает её, вся страница считается по отмеченным.

const dm = (iso: string | null) => (iso ? `${iso.slice(8, 10)}.${iso.slice(5, 7)}` : "—");
const signed = (v: number | null | undefined, digits = 0) => (v == null || !Number.isFinite(v) ? "—" : `${v > 0 ? "+" : ""}${pct(v, digits)}`);

function useHref() {
  const pathname = usePathname();
  const params = useSearchParams();
  return (change: Record<string, string | null>) => {
    const next = new URLSearchParams(params);
    for (const [k, v] of Object.entries(change)) {
      if (v == null || v === "") next.delete(k);
      else next.set(k, v);
    }
    return `${pathname}${next.size ? `?${next}` : ""}`;
  };
}

/** Параметры вывода с сервера: «m:» — сумма, «pN:» — доля с N знаками, «sN:» — со знаком, «nN:» — число. */
function arg(value: string, t: T) {
  const m = /^(m|p\d|s\d|n\d?):(.*)$/.exec(value);
  if (!m) return value;
  const v = Number(m[2]);
  if (m[1] === "m") return money(v, t);
  if (m[1].startsWith("p")) return pct(v, Number(m[1].slice(1)));
  if (m[1].startsWith("s")) return signed(v, Number(m[1].slice(1)));
  return num(v, m[1].length > 1 ? Number(m[1].slice(1)) : 0);
}

export function PromoReport({ data }: { data: PromoView }) {
  const t = useT();
  const href = useHref();
  const selected = new Set(data.selected);
  const toggle = (id: number) => {
    const next = new Set(selected);
    if (next.has(id)) next.delete(id);
    else next.add(id);
    return href({ promos: [...next].join(",") || null });
  };
  const monthOptions = data.months.map((m) => ({
    value: `${m.year}-${m.month}`,
    label: `${monthLabel(m.year, m.month, t).replace(/^\S+/, (w) => t(w))}${m.partial ? ` ${t("(по {day}-е)", { day: m.days })}` : ""}`,
  }));
  const tiles = data.tiles;
  const one = data.selected.length === 1 ? data.byPromo.find((p) => p.id === data.selected[0]) : null;

  return (
    <>
      <div className="flex flex-wrap items-center gap-3">
        <MonthSelect label={t("Месяц")} value={`${data.month.year}-${data.month.month}`} options={monthOptions} />
        {data.rmOptions.length > 1 && (
          <div className="no-scrollbar flex max-w-full overflow-x-auto rounded-lg border border-line text-sm">
            {[null, ...data.rmOptions].map((rm) => (
              <Link
                key={rm ?? "all"}
                href={href({ rm })}
                className={`shrink-0 whitespace-nowrap px-3 py-1.5 max-lg:py-2.5 ${data.rm === rm ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}
              >
                {rm ? t(rm) : t("Все РМ")}
              </Link>
            ))}
          </div>
        )}
        {data.selected.length > 0 && (
          <span className="flex flex-wrap items-center gap-2 text-sm">
            <span className="rounded-full bg-accent-soft px-3 py-1 font-medium text-accent-strong">
              {one ? t("Акция: {name}", { name: one.id === -1 ? t(one.name) : one.name }) : t("Отмечено акций: {n}", { n: data.selected.length })}
            </span>
            <Link href={href({ promos: null })} className="text-accent hover:underline">
              {t("Сбросить")}
            </Link>
          </span>
        )}
        {data.asOf && <span className="ml-auto text-xs text-ink-3">{t("Заказы Linko загружены {time}", { time: dateTime(data.asOf) })}</span>}
      </div>

      <div className="mt-4 grid grid-cols-2 gap-3 lg:grid-cols-3 min-[87.5rem]:grid-cols-5">
        <KpiTile label={t("Скидки и бонусы")} value={money(tiles.total, t)} unit={t("сум")}>
          {tiles.toSales == null
            ? t("продаж за месяц нет")
            : tiles.salesDay < tiles.days
              ? t("{pct} к продажам за 1–{day}-е (дальше вторички пока нет)", { pct: pct(tiles.toSales, 2), day: tiles.salesDay })
              : t("{pct} к продажам", { pct: pct(tiles.toSales, 2) })}
        </KpiTile>
        <KpiTile label={t("По акциям")} value={money(tiles.byPromos, t)} unit={t("сум")}>
          {num(tiles.promos)} {t(plural(tiles.promos, ["акция", "акции", "акций"]))}
        </KpiTile>
        <KpiTile label={t("Без акции")} value={money(tiles.manual, t)} unit={t("сум")} tone={tiles.manual > 0 ? "warn" : "ink"}>
          {t("{pct} всех скидок — поставили вручную", { pct: pct(tiles.total ? tiles.manual / tiles.total : 0) })}
        </KpiTile>
        <KpiTile label={t("Вне срока акции")} value={money(tiles.out, t)} unit={t("сум")} tone={tiles.out > 0 ? "bad" : "ok"}>
          {t("приёмка раньше начала или позже конца акции больше чем на 2 дня")}
        </KpiTile>
        <KpiTile label={t("В пути")} value={money(tiles.pending, t)} unit={t("сум")} tone="muted">
          {t("заказы со скидкой отданы, но ещё не доставлены")}
        </KpiTile>
      </div>

      {data.attention.length > 0 && (
        <div className="mt-4 rounded-xl border border-bad/30 bg-bad-soft px-4 py-3 text-sm">
          <div className="flex items-center gap-2 font-semibold text-bad">
            <Siren className="size-4" aria-hidden />
            {t("Внимание: ручные скидки выше 1% от продаж — {n}", { n: `${num(data.attention.length)} ${t(plural(data.attention.length, ["агент", "агента", "агентов"]))}` })}
          </div>
          <ul className="mt-2 space-y-1 text-ink-2">
            {data.attention.slice(0, 8).map((a) => (
              <li key={`${a.agent}|${a.region}`}>
                <b className="text-ink">{a.agent}</b> ({t(a.region)}) — {money(a.sum, t)}, {t("{pct} от продаж", { pct: pct(a.pct, 2) })}
                {a.monthsWith >= 3 && ` · ${t("{k} из {m} мес. со скидкой", { k: a.monthsWith, m: a.monthsTotal })}`}
              </li>
            ))}
          </ul>
          {data.attention.length > 8 && <p className="mt-1 text-xs text-ink-3">{t("и ещё {n} — в таблице «Ручные скидки — кто ставит»", { n: data.attention.length - 8 })}</p>}
        </div>
      )}

      {data.conclusions.length > 0 && (
        <Alert tone="info">
          <div className="font-semibold text-ink">{t("Выводы")}</div>
          <ul className="mt-1 list-disc space-y-1 pl-5">
            {data.conclusions.map((c, i) => (
              <li key={i}>{t(c.text, Object.fromEntries(Object.entries(c.args).map(([k, v]) => [k, arg(v, t)])))}</li>
            ))}
          </ul>
        </Alert>
      )}

      <ByPromo data={data} toggle={toggle} />
      {data.effects.length > 0 && <Effects data={data} toggle={toggle} />}
      <Regions data={data} />
      {data.manual.length > 0 && (data.selected.length === 0 || selected.has(-1)) && <Manual data={data} />}
      <Calendar data={data} />
      <Tops data={data} />
      <ByMonth data={data} />

      <Note>
        {t(
          "Источник — заказы Linko: доставленные, по дате приёмки магазином; сумма — скидка строки заказа (у бонуса — стоимость бонусного товара) за вычетом возвратов. Акцию строки Linko в API не отдаёт — она определяется по справочнику акций Linko: акция, действующая в день заказа, где этот товар со скидкой того же процента (или бонусный), с учётом региона в названии. Одновременные акции с теми же товарами и процентом, различающиеся только списками точек («Могул 8% \"A\" / \"S\"»), показаны одной строкой. «Без акции» — скидка, которую поставили вручную. «Вне срока» — приёмка раньше начала или позже конца акции больше чем на 2 дня. Продажи — вторичка региона и агента за месяц.",
        )}
      </Note>
    </>
  );
}

function ByPromo({ data, toggle }: { data: PromoView; toggle: (id: number) => string }) {
  const t = useT();
  const th = "whitespace-nowrap px-3 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const td = "px-3 py-2.5 text-right tabular-nums";
  const copy = () =>
    [
      [t("Акция"), t("Тип"), t("Срок"), t("Регионов"), t("Магазинов"), t("Кол-во"), t("Сумма"), t("Доля"), t("Вне срока")].join("\t"),
      ...data.byPromo.map((p) =>
        [p.id === -1 ? t(p.name) : p.name, t(promoKindLabel[p.kind]), p.begin ? `${dm(p.begin)}–${dm(p.till)}` : "", p.regions, p.shops, p.qty, p.sum, p.share, p.out]
          .map((v) => tsvValue(v))
          .join("\t"),
      ),
    ].join("\n");
  const sel = data.selectedTotals;
  return (
    <Section title="По акциям" hint="нажмите на акцию — отметить, можно несколько; вся страница считается по отмеченным" actions={<CopyButton onCopy={copy} />}>
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className={`${th} sticky left-0 z-[1] bg-surface-solid pl-4 text-left sm:pl-5`}>{t("Акция")}</th>
              <th className={`${th} text-left`}>{t("Тип")}</th>
              <th className={`${th} text-left`}>{t("Срок")}</th>
              <th className={th}>{t("Регионов")}</th>
              <th className={th}>{t("Магазинов")}</th>
              <th className={th}>{t("Кол-во")}</th>
              <th className={th}>{t("Сумма")}</th>
              <th className={th}>{t("Доля")}</th>
              <th className={`${th} pr-4 sm:pr-5`}>{t("Вне срока")}</th>
            </tr>
          </thead>
          <tbody>
            {data.byPromo.map((p) => (
              <tr key={p.id} className={`group/row border-b border-line ${p.selected ? "bg-accent-soft" : "hover:bg-muted"}`}>
                <td className={`sticky left-0 z-[1] px-3 py-2.5 pl-4 sm:pl-5 ${p.selected ? "bg-accent-soft" : "bg-surface-solid group-hover/row:bg-muted-solid"}`}>
                  <Link href={toggle(p.id)} scroll={false} className="flex max-w-[420px] items-start gap-2">
                    <span className={`mt-0.5 flex size-4 shrink-0 items-center justify-center rounded border ${p.selected ? "border-accent bg-accent text-white" : "border-line"}`}>
                      {p.selected && <Check className="size-3" />}
                    </span>
                    <span className={`whitespace-normal font-medium ${p.id === -1 ? "text-warn" : "text-ink"}`}>{p.id === -1 ? t(p.name) : p.name}</span>
                  </Link>
                </td>
                <td className="px-3 py-2.5 text-ink-2">{t(promoKindLabel[p.kind])}</td>
                <td className="whitespace-nowrap px-3 py-2.5 tabular-nums text-ink-2">{p.begin ? `${dm(p.begin)}–${dm(p.till)}` : "—"}</td>
                <td className={td}>{num(p.regions)}</td>
                <td className={td}>{num(p.shops)}</td>
                <td className={td}>{num(p.qty)}</td>
                <td className={`${td} font-semibold`}>{num(p.sum)}</td>
                <td className={td}>{pct(p.share, 1)}</td>
                <td className={`${td} pr-4 sm:pr-5 ${p.out ? "font-semibold text-bad" : "text-ink-3"}`}>{p.out ? num(p.out) : "—"}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            {sel && (
              <tr className="font-semibold text-accent-strong">
                <td className="sticky left-0 z-[1] bg-surface-solid px-3 py-2.5 pl-4 sm:pl-5">{t("Итого по отмеченным ({n})", { n: data.selected.length })}</td>
                <td colSpan={2} />
                <td className={td}>{num(sel.regions)}</td>
                <td className={td}>{num(sel.shops)}</td>
                <td className={td}>{num(sel.qty)}</td>
                <td className={td}>{num(sel.sum)}</td>
                <td className={td}>{pct(sel.share, 1)}</td>
                <td className={`${td} pr-4 sm:pr-5`}>{sel.out ? num(sel.out) : "—"}</td>
              </tr>
            )}
            <tr className="font-semibold text-ink">
              <td className="sticky left-0 z-[1] bg-surface-solid px-3 py-2.5 pl-4 sm:pl-5">{t("Итого")}</td>
              <td colSpan={5} />
              <td className={td}>{num(data.promoTotal)}</td>
              <td colSpan={2} />
            </tr>
          </tfoot>
        </table>
      </div>
    </Section>
  );
}

function Effects({ data, toggle }: { data: PromoView; toggle: (id: number) => string }) {
  const t = useT();
  const th = "whitespace-nowrap px-3 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const td = "px-3 py-2.5 text-right tabular-nums";
  const good = (v: number | null) => (v == null ? "" : v > 0 ? "text-ok" : "text-bad");
  return (
    <Section
      title="Эффект акций"
      hint={t("кг в день товаров акции в её регионах против среднего за 3 месяца до старта, с поправкой на тренд остальных товаров; продажи по {day}-е; нажмите — отметить", { day: data.effectDay })}
    >
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className={`${th} sticky left-0 z-[1] bg-surface-solid pl-4 text-left sm:pl-5`}>{t("Акция")}</th>
              <th className={th}>{t("Регионов")}</th>
              <th className={th}>{t("SKU")}</th>
              <th className={th}>{t("База, кг/день")}</th>
              <th className={th}>{t("В акции, кг/день")}</th>
              <th className={th}>{t("Рост")}</th>
              <th className={th}>{t("Рост остальных")}</th>
              <th className={th}>{t("Доп. кг")}</th>
              <th className={th}>{t("Скидка")}</th>
              <th className={`${th} pr-4 sm:pr-5`}>{t("Выручка на 1 сум скидки")}</th>
            </tr>
          </thead>
          <tbody>
            {data.effects.map((e) => {
              const on = data.selected.includes(e.id);
              return (
                <tr key={e.id} className={`group/row border-b border-line ${on ? "bg-accent-soft" : "hover:bg-muted"}`}>
                  <td className={`sticky left-0 z-[1] px-3 py-2.5 pl-4 sm:pl-5 ${on ? "bg-accent-soft" : "bg-surface-solid group-hover/row:bg-muted-solid"}`}>
                    <Link href={toggle(e.id)} scroll={false} className="block max-w-[380px] whitespace-normal font-medium text-ink hover:text-accent">
                      {e.name}
                    </Link>
                  </td>
                  {e.status === "ok" ? (
                    <>
                      <td className={td}>{num(e.regions)}</td>
                      <td className={td}>{num(e.skus)}</td>
                      <td className={td}>{num(e.baseKgDay, 0)}</td>
                      <td className={td}>{num(e.curKgDay, 0)}</td>
                      <td className={`${td} ${good(e.growth)}`}>{signed(e.growth)}</td>
                      <td className={`${td} text-ink-2`}>{signed(e.controlGrowth)}</td>
                      <td className={`${td} font-semibold ${good(e.extraKg)}`}>{num(e.extraKg, 0)}</td>
                    </>
                  ) : (
                    <td colSpan={7} className="px-3 py-2.5 text-xs text-ink-3">
                      {e.status === "long" ? t("давняя скидка — периода «до акции» в данных нет, эффект не оценить") : t("нет месяцев до старта акции для сравнения")}
                    </td>
                  )}
                  <td className={td}>{num(e.discount)}</td>
                  <td className={`${td} pr-4 sm:pr-5 ${e.status === "ok" ? good(e.returnPerSum) : ""}`}>{e.status === "ok" ? num(e.returnPerSum, 1) : ""}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </Section>
  );
}

/** «По регионам»: регион → агенты → акции агента → магазины акции. */
function Regions({ data }: { data: PromoView }) {
  const t = useT();
  const [open, setOpen] = useState<Set<string>>(new Set());
  const flip = (key: string) => setOpen((s) => (s.has(key) ? new Set([...s].filter((x) => x !== key)) : new Set(s).add(key)));
  const th = "whitespace-nowrap px-3 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const td = "px-3 py-2.5 text-right tabular-nums";
  const totals = data.regionTotals;
  const row = (r: PromoRegionRow | PromoAgentRow, key: string, depth: number, growth?: number | null) => (
    <>
      <td className={td}>{r.sales == null ? "—" : num(r.sales)}</td>
      {growth !== undefined && <td className={`${td} ${growth == null ? "" : growth > 0 ? "text-ok" : "text-bad"}`}>{signed(growth)}</td>}
      {growth === undefined && depth > 0 && <td className={td} />}
      <td className={`${td} font-semibold`}>{r.promoSale ? num(r.promoSale) : "—"}</td>
      <td className={td}>{r.share == null ? "—" : pct(r.share)}</td>
      <td className={td}>{r.noPromo == null ? "—" : num(r.noPromo)}</td>
      <td className={td}>{r.mogul ? num(r.mogul) : "—"}</td>
      <td className={td}>{r.akbPromo ? num(r.akbPromo) : "—"}</td>
      <td className={td}>{r.cost ? num(r.cost) : "—"}</td>
      <td className={`${td} pr-4 sm:pr-5`} data-key={key}>
        {r.costPct == null ? "—" : pct(r.costPct, 2)}
      </td>
    </>
  );
  return (
    <Section title="По регионам" hint={t("по {day}-е; нажмите на регион — агенты, на агента — его акции, на акцию — магазины", { day: data.salesDay })}>
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className={`${th} sticky left-0 z-[1] bg-surface-solid pl-4 text-left sm:pl-5`}>{t("Регион")}</th>
              <th className={th}>{t("Продажи")}</th>
              <th className={th}>{t("Рост продаж")}</th>
              <th className={th}>{t("Продажи по акции")}</th>
              <th className={th}>{t("Доля акций")}</th>
              <th className={th}>{t("Без акции")}</th>
              <th className={th}>{t("АКБ Могуль")}</th>
              <th className={th}>{t("АКБ по акции")}</th>
              <th className={th}>{t("Затраты")}</th>
              <th className={`${th} pr-4 sm:pr-5`}>{t("% затрат")}</th>
            </tr>
          </thead>
          <tbody>
            {data.regions.map((r) => {
              const rk = r.name;
              return (
                <RegionBlock key={rk}>
                  <tr className="group/row cursor-pointer border-b border-line hover:bg-muted" onClick={() => flip(rk)}>
                    <td className="sticky left-0 z-[1] bg-surface-solid px-3 py-2.5 pl-4 group-hover/row:bg-muted-solid sm:pl-5">
                      <span className="flex items-center gap-1.5 font-medium text-ink">
                        <ChevronRight className={`size-3.5 text-ink-3 transition-transform ${open.has(rk) ? "rotate-90" : ""}`} />
                        {t(r.name)}
                      </span>
                      {r.rm && <span className="block pl-5 text-xs text-ink-3">{t(r.rm)}</span>}
                    </td>
                    {row(r, rk, 0, r.growth)}
                  </tr>
                  {open.has(rk) &&
                    r.agents.map((a) => {
                      const ak = `${rk}|${a.id}`;
                      return (
                        <RegionBlock key={ak}>
                          <tr
                            className={`border-b border-line bg-muted/40 ${a.promos.length ? "cursor-pointer hover:bg-muted" : ""}`}
                            onClick={() => a.promos.length && flip(ak)}
                          >
                            <td className="sticky left-0 z-[1] bg-muted-solid px-3 py-2 pl-10 sm:pl-11">
                              <span className="flex items-center gap-1.5 text-ink">
                                {a.promos.length > 0 && <ChevronRight className={`size-3 text-ink-3 transition-transform ${open.has(ak) ? "rotate-90" : ""}`} />}
                                {a.name}
                              </span>
                            </td>
                            {row(a, ak, 1)}
                          </tr>
                          {open.has(ak) && (
                            <tr className="border-b border-line">
                              <td colSpan={10} className="bg-surface px-4 py-3 sm:px-5">
                                <AgentPromos agent={a} />
                              </td>
                            </tr>
                          )}
                        </RegionBlock>
                      );
                    })}
                </RegionBlock>
              );
            })}
          </tbody>
          <tfoot>
            <tr className="font-semibold text-ink">
              <td className="sticky left-0 z-[1] bg-surface-solid px-3 py-2.5 pl-4 sm:pl-5">{t("Итого")}</td>
              <td className={td}>{num(totals.sales)}</td>
              <td className={td} />
              <td className={td}>{num(totals.promoSale)}</td>
              <td className={td}>{totals.share == null ? "—" : pct(totals.share)}</td>
              <td className={td} />
              <td className={td}>{num(totals.mogul)}</td>
              <td className={td}>{num(totals.akbPromo)}</td>
              <td className={td}>{num(totals.cost)}</td>
              <td className={`${td} pr-4 sm:pr-5`}>{totals.costPct == null ? "—" : pct(totals.costPct, 2)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
      <Note>{t("Продажи по акции, АКБ по акции и затраты — без ручных скидок и по тот же день, что продажи. Доля и % затрат — к продажам региона.")}</Note>
    </Section>
  );
}

function RegionBlock({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}

function AgentPromos({ agent }: { agent: PromoAgentRow }) {
  const t = useT();
  const [open, setOpen] = useState<number | null>(null);
  const th = "whitespace-nowrap px-2 py-1.5 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const td = "px-2 py-1.5 text-right tabular-nums";
  return (
    <div className="space-y-2">
      <div className="text-xs font-semibold text-ink-2">{t("{agent} — акции", { agent: agent.name })}</div>
      <table className="w-full min-w-max border-collapse text-xs">
        <thead>
          <tr className="border-b border-line">
            <th className={`${th} text-left`}>{t("Акция")}</th>
            <th className={th}>{t("Участников")}</th>
            <th className={th}>{t("Ср. закупка")}</th>
            <th className={th}>{t("Макс.")}</th>
            <th className={th}>{t("Закупились")}</th>
            <th className={th}>{t("Затраты")}</th>
            <th className={th}>{t("% скидки")}</th>
          </tr>
        </thead>
        <tbody>
          {agent.promos.map((p) => (
            <RegionBlock key={p.id}>
              <tr className="cursor-pointer border-b border-line hover:bg-muted" onClick={() => setOpen(open === p.id ? null : p.id)}>
                <td className="px-2 py-1.5">
                  <span className="flex max-w-[360px] items-start gap-1 whitespace-normal text-ink">
                    <ChevronRight className={`mt-0.5 size-3 shrink-0 text-ink-3 transition-transform ${open === p.id ? "rotate-90" : ""}`} />
                    {p.id === -1 ? t(p.name) : p.name}
                  </span>
                </td>
                <td className={td}>{num(p.participants)}</td>
                <td className={td}>{num(p.avg)}</td>
                <td className={td}>{num(p.max)}</td>
                <td className={`${td} font-semibold`}>{num(p.promoSale)}</td>
                <td className={td}>{num(p.cost)}</td>
                <td className={td}>{p.discountPct == null ? "—" : pct(p.discountPct, 1)}</td>
              </tr>
              {open === p.id && (
                <tr>
                  <td colSpan={7} className="px-2 py-2">
                    <div className="mb-1 text-[11px] font-semibold text-ink-2">
                      {t("{promo} — {n}", { promo: p.id === -1 ? t(p.name) : p.name, n: `${num(p.shops.length)} ${t(plural(p.shops.length, ["магазин", "магазина", "магазинов"]))}` })}
                    </div>
                    <table className="w-full border-collapse">
                      <thead>
                        <tr className="border-b border-line">
                          <th className={`${th} text-left`}>{t("Магазин")}</th>
                          <th className={th}>{t("№")}</th>
                          <th className={th}>{t("Закупка за месяц")}</th>
                          <th className={th}>{t("Продажи по акции")}</th>
                          <th className={th}>{t("Доля акций")}</th>
                          <th className={th}>{t("Затраты")}</th>
                          <th className={th}>{t("% скидки")}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {p.shops.map((s) => (
                          <tr key={s.id ?? s.name} className="border-b border-line last:border-0">
                            <td className="max-w-[320px] whitespace-normal px-2 py-1.5 text-ink">{s.name}</td>
                            <td className={`${td} text-ink-3`}>{s.id ?? "—"}</td>
                            <td className={td}>{s.purchase == null ? "—" : num(s.purchase)}</td>
                            <td className={`${td} font-semibold`}>{num(s.promoSale)}</td>
                            <td className={td}>{s.share == null ? "—" : pct(s.share)}</td>
                            <td className={td}>{num(s.cost)}</td>
                            <td className={td}>{s.discountPct == null ? "—" : pct(s.discountPct, 1)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </td>
                </tr>
              )}
            </RegionBlock>
          ))}
        </tbody>
      </table>
    </div>
  );
}

const shortMonth = (key: string, t: T) => {
  const [y, m] = key.split("-").map(Number);
  return monthLabel(y, m, t).replace(/^\S+/, (w) => t(w)).split(" ")[0].slice(0, 3);
};

function Manual({ data }: { data: PromoView }) {
  const t = useT();
  type Row = PromoView["manual"][number];
  const months = data.manualMonths;
  const columns: Column<Row>[] = [
    { key: "agent", label: t("Агент"), value: (r) => r.agent, render: (r) => <span className="block min-w-[200px] font-medium text-ink">{r.agent}</span> },
    { key: "region", label: t("Регион"), value: (r) => r.region, render: (r) => t(r.region) },
    { key: "sum", label: t("Без акции"), align: "right", value: (r) => r.sum, render: (r) => <b>{num(r.sum)}</b> },
    { key: "sales", label: t("Продажи агента"), align: "right", value: (r) => r.sales, render: (r) => (r.sales == null ? "—" : num(r.sales)) },
    {
      key: "pct",
      label: t("% к продажам"),
      align: "right",
      value: (r) => r.pct,
      render: (r) => <span className={r.pct != null && r.pct >= 0.01 ? "font-semibold text-bad" : ""}>{r.pct == null ? "—" : pct(r.pct, 2)}</span>,
    },
    { key: "shops", label: t("Магазинов"), align: "right", value: (r) => r.shops },
    ...months.map(
      (m, i): Column<Row> => ({
        key: `m${m}`,
        label: shortMonth(m, t),
        align: "right",
        value: (r) => r.history[i] ?? 0,
        render: (r) => <span className={i === months.length - 1 ? "rounded bg-accent-soft px-1" : ""}>{r.history[i] ? money(r.history[i], t) : "—"}</span>,
      }),
    ),
    {
      key: "months",
      label: t("Месяцев со скидкой"),
      align: "right",
      value: (r) => r.monthsWith,
      render: (r) => <span className={r.monthsWith === months.length && months.length >= 3 ? "font-semibold text-bad" : ""}>{`${r.monthsWith} / ${months.length}`}</span>,
    },
  ];
  return (
    <DataTable
      title={t("Ручные скидки — кто ставит")}
      hint={t("скидки без акции по агентам; % — к продажам агента за месяц; справа — последние месяцы")}
      columns={columns}
      rows={data.manual}
      rowKey={(r) => `${r.agent}|${r.region}`}
      limit={20}
    />
  );
}

type Metric = "sale" | "discount" | "akb" | "qty";

function Calendar({ data }: { data: PromoView }) {
  const t = useT();
  const [group, setGroup] = useState<"region" | "promo">("region");
  const [metric, setMetric] = useState<Metric>("discount");
  const [open, setOpen] = useState<Set<string>>(new Set());
  const cal = data.calendar;
  const days = Array.from({ length: cal.days }, (_, i) => i + 1);
  const value = (r: PromoCalendarRow, i: number) => (metric === "sale" ? r.sale[i] : metric === "discount" ? r.discount[i] : metric === "qty" ? r.qty[i] : r.akb[i]);
  const totalOf = (r: PromoCalendarRow) =>
    metric === "akb" ? r.akbMonth : (metric === "sale" ? r.sale : metric === "discount" ? r.discount : r.qty).reduce((a, b) => a + b, 0);
  const fmt = (v: number) => (metric === "sale" || metric === "discount" ? money(v, t) : num(v));
  const rows = [...(group === "region" ? cal.byRegion : cal.byPromo)].sort((a, b) => totalOf(b) - totalOf(a));
  const seg = (on: boolean) => `px-3 py-1.5 max-lg:py-2.5 ${on ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`;
  const line = (r: PromoCalendarRow, depth: number) => (
    <tr
      key={r.key}
      className={`border-b border-line ${depth ? "bg-muted/40" : r.children.length ? "cursor-pointer hover:bg-muted" : ""}`}
      onClick={() => r.children.length && setOpen((s) => (s.has(r.key) ? new Set([...s].filter((x) => x !== r.key)) : new Set(s).add(r.key)))}
    >
      <td className={`sticky left-0 z-[1] px-3 py-1.5 ${depth ? "bg-muted-solid pl-8" : "bg-surface-solid pl-4 sm:pl-5"}`}>
        <span className="flex min-w-[200px] max-w-[300px] items-start gap-1 whitespace-normal">
          {r.children.length > 0 && <ChevronRight className={`mt-0.5 size-3 shrink-0 text-ink-3 transition-transform ${open.has(r.key) ? "rotate-90" : ""}`} />}
          <span>
            <span className="block text-ink">{r.key === "-1" ? t(r.name) : t(r.name)}</span>
            {r.sub && <span className="block text-[11px] text-ink-3">{group === "promo" && !depth ? t(promoKindLabel[r.sub as keyof typeof promoKindLabel] ?? r.sub) : t(r.sub)}</span>}
          </span>
        </span>
      </td>
      {days.map((d, i) => {
        const v = value(r, i);
        return (
          <td key={d} className={`px-1.5 py-1.5 text-right text-[11px] tabular-nums ${cal.sundays.includes(d) ? "bg-muted/60" : ""} ${v ? "text-ink" : "text-ink-3"}`}>
            {v ? fmt(v) : "·"}
          </td>
        );
      })}
      <td className="px-3 py-1.5 pr-4 text-right text-xs font-semibold tabular-nums sm:pr-5">{fmt(totalOf(r))}</td>
    </tr>
  );
  return (
    <Section
      title="Календарь акций по дням"
      hint={t("{n} дн. · столбец — день приёмки", { n: cal.days })}
      actions={
        <span className="flex flex-wrap gap-2">
          <span className="flex overflow-hidden rounded-lg border border-line text-xs">
            <button type="button" className={seg(group === "region")} onClick={() => setGroup("region")}>
              {t("регионы → ТП")}
            </button>
            <button type="button" className={seg(group === "promo")} onClick={() => setGroup("promo")}>
              {t("акции")}
            </button>
          </span>
          <span className="flex overflow-hidden rounded-lg border border-line text-xs">
            {(["discount", "sale", "akb", "qty"] as const).map((m) => (
              <button key={m} type="button" className={seg(metric === m)} onClick={() => setMetric(m)}>
                {t({ discount: "скидка", sale: "продажа", akb: "АКБ", qty: "шт" }[m])}
              </button>
            ))}
          </span>
        </span>
      }
    >
      <div className="-mx-4 max-h-[560px] overflow-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead className="sticky top-0 z-[2] bg-surface-solid">
            <tr className="border-b border-line">
              <th className="sticky left-0 z-[3] bg-surface-solid px-3 py-2 pl-4 text-left text-[11px] font-semibold uppercase tracking-wide text-ink-3 sm:pl-5">
                {group === "region" ? t("Регион") : t("Акция")}
              </th>
              {days.map((d) => (
                <th key={d} className={`px-1.5 py-2 text-right text-[11px] font-semibold text-ink-3 ${cal.sundays.includes(d) ? "bg-muted/60" : ""}`}>
                  {d}
                </th>
              ))}
              <th className="px-3 py-2 pr-4 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3 sm:pr-5">{t("Итого")}</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => [line(r, 0), ...(open.has(r.key) ? [...r.children].sort((a, b) => totalOf(b) - totalOf(a)).map((c) => line(c, 1)) : [])])}
          </tbody>
          <tfoot>{line({ ...cal.total, name: "Итого", children: [] }, 0)}</tfoot>
        </table>
      </div>
      {metric === "akb" && (
        <Note>{t("АКБ за день — точки с отгрузкой именно в этот день; по месяцу точка считается один раз, поэтому «Итого» меньше суммы дней.")}</Note>
      )}
    </Section>
  );
}

function Tops({ data }: { data: PromoView }) {
  const t = useT();
  type Agent = PromoView["agents"][number];
  type Shop = PromoView["shops"][number];
  type Wait = PromoView["waiting"][number];
  type Product = PromoView["products"][number];
  const agentCols: Column<Agent>[] = [
    { key: "agent", label: t("Агент"), value: (r) => r.agent, render: (r) => <span className="block min-w-[200px] font-medium text-ink">{r.agent}</span> },
    { key: "region", label: t("Регион"), value: (r) => r.region, render: (r) => t(r.region) },
    { key: "sum", label: t("Сумма"), align: "right", value: (r) => r.sum, render: (r) => <b>{num(r.sum)}</b> },
    { key: "manual", label: t("Без акции"), align: "right", value: (r) => r.manual, render: (r) => (r.manual ? num(r.manual) : "—") },
    { key: "shops", label: t("Магазинов"), align: "right", value: (r) => r.shops },
    { key: "share", label: t("Доля"), align: "right", value: (r) => r.share, render: (r) => pct(r.share, 1) },
  ];
  const shopCols: Column<Shop>[] = [
    { key: "name", label: t("Магазин"), value: (r) => r.name, render: (r) => <span className="block min-w-[200px] max-w-[320px] whitespace-normal font-medium text-ink">{r.name}</span> },
    { key: "id", label: t("№"), align: "right", value: (r) => r.id },
    { key: "region", label: t("Регион"), value: (r) => r.region, render: (r) => t(r.region) },
    { key: "agent", label: t("Агент"), value: (r) => r.agent },
    { key: "sum", label: t("Сумма"), align: "right", value: (r) => r.sum, render: (r) => <b>{num(r.sum)}</b> },
    {
      key: "promos",
      label: t("Акция"),
      value: (r) => r.promos.join("; "),
      render: (r) => <span className="block max-w-[280px] whitespace-normal text-xs text-ink-2">{r.promos.map((p) => (p.startsWith("Без акции") ? t(p) : p)).join("\n")}</span>,
    },
  ];
  const months = data.waitingMonths;
  const waitCols: Column<Wait>[] = [
    { key: "name", label: t("Магазин"), value: (r) => r.name, render: (r) => <span className="block min-w-[200px] max-w-[300px] whitespace-normal font-medium text-ink">{r.name}</span> },
    { key: "id", label: t("№"), align: "right", value: (r) => r.id },
    { key: "region", label: t("Регион"), value: (r) => r.region, render: (r) => t(r.region) },
    { key: "agent", label: t("Агент"), value: (r) => r.agent },
    {
      key: "kind",
      label: t("Признак"),
      value: (r) => (r.kind === "only" ? t("всегда со скидкой") : t("под акцию")),
      render: (r) => (r.kind === "only" ? <b className="text-bad">{t("всегда со скидкой")}</b> : t("под акцию")),
    },
    { key: "months", label: t("Мес. со скидкой / с закупкой"), align: "right", value: (r) => r.monthsDiscount, render: (r) => `${r.monthsDiscount} / ${r.monthsBuy}` },
    { key: "avgD", label: t("Закупка/мес со скидкой"), align: "right", value: (r) => r.avgDiscountMonth, render: (r) => num(r.avgDiscountMonth) },
    { key: "avgN", label: t("Без скидки"), align: "right", value: (r) => r.avgNoDiscount, render: (r) => (r.avgNoDiscount ? num(r.avgNoDiscount) : "—") },
    { key: "dp", label: t("Скидка к закупкам"), align: "right", value: (r) => r.discountToPurchase, render: (r) => (r.discountToPurchase == null ? "—" : pct(r.discountToPurchase, 1)) },
    ...months.map(
      (m, i): Column<Wait> => ({
        key: `w${m}`,
        label: shortMonth(m, t),
        align: "right",
        value: (r) => r.purchases[i],
        render: (r) => (
          <span className={r.discountMonths[i] ? "rounded bg-accent-soft px-1 font-semibold text-ink" : ""}>{r.purchases[i] == null ? "—" : money(r.purchases[i]!, t)}</span>
        ),
      }),
    ),
  ];
  const productCols: Column<Product>[] = [
    { key: "name", label: t("Товар"), value: (r) => r.name, render: (r) => <span className="block min-w-[240px] max-w-[420px] whitespace-normal text-ink">{r.name}</span> },
    { key: "qty", label: t("Кол-во"), align: "right", value: (r) => r.qty, render: (r) => num(r.qty) },
    { key: "sum", label: t("Сумма"), align: "right", value: (r) => r.sum, render: (r) => <b>{num(r.sum)}</b> },
    { key: "share", label: t("Доля"), align: "right", value: (r) => r.share, render: (r) => pct(r.share, 1) },
  ];
  const top = (n: number) => (n > 100 ? t("первые 100 из {n} по сумме", { n: num(n) }) : undefined);
  return (
    <>
      <DataTable title={t("Агенты")} hint={top(data.agentCount)} columns={agentCols} rows={data.agents} rowKey={(r) => `${r.agent}|${r.region}`} limit={20} />
      <DataTable title={t("Магазины")} hint={top(data.shopCount)} columns={shopCols} rows={data.shops} rowKey={(r) => String(r.id ?? r.name)} limit={20} />
      {data.waiting.length > 0 && (
        <DataTable
          title={t("Магазины и скидки — кто ждёт акцию")}
          hint={t(
            "за {m} мес.; «всегда со скидкой» — в ≥80% месяцев с закупкой была скидка ({only}); «под акцию» — ≥2 мес. без скидки, а со скидкой берёт в 2+ раза больше ({wait}); выделены месяцы со скидкой",
            { m: months.length, only: num(data.waitingOnly), wait: num(data.waitingWait) },
          )}
          columns={waitCols}
          rows={data.waiting}
          rowKey={(r) => String(r.id ?? r.name)}
          limit={20}
        />
      )}
      <DataTable title={t("Товары")} hint={top(data.productCount)} columns={productCols} rows={data.products} rowKey={(r) => r.name} limit={20} />
    </>
  );
}

function ByMonth({ data }: { data: PromoView }) {
  const t = useT();
  const max = Math.max(0, ...data.byMonth.map((m) => m.sum));
  const th = "whitespace-nowrap px-3 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const td = "px-3 py-2.5 text-right tabular-nums";
  return (
    <Section title="По месяцам">
      <div className="-mx-4 overflow-x-auto sm:-mx-5">
        <table className="w-full min-w-max border-collapse text-sm">
          <thead>
            <tr className="border-b border-line">
              <th className={`${th} pl-4 text-left sm:pl-5`}>{t("Месяц")}</th>
              <th className={th}>{t("Продажи")}</th>
              <th className={th}>{t("Скидки")}</th>
              <th className={th}>{t("% к продажам")}</th>
              <th className={th}>{t("Без акции")}</th>
              <th className={`${th} w-1/3 pr-4 sm:pr-5`} />
            </tr>
          </thead>
          <tbody>
            {data.byMonth.map((m) => (
              <tr key={m.key} className={`border-b border-line ${m.current ? "bg-accent-soft/60" : ""}`}>
                <td className="px-3 py-2.5 pl-4 font-medium text-ink sm:pl-5">{monthLabel(m.year, m.month, t).replace(/^\S+/, (w) => t(w)).split(" ")[0]}</td>
                <td className={td}>{m.sales == null ? "—" : money(m.sales, t)}</td>
                <td className={`${td} font-semibold`}>{money(m.sum, t)}</td>
                <td className={td}>{m.toSales == null ? "—" : pct(m.toSales, 2)}</td>
                <td className={td}>{m.manual ? money(m.manual, t) : "—"}</td>
                <td className="px-3 py-2.5 pr-4 sm:pr-5">
                  <div className="h-2 rounded-sm bg-accent/75" style={{ width: `${max > 0 ? Math.max(1, Math.round((m.sum / max) * 100)) : 0}%` }} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Section>
  );
}

