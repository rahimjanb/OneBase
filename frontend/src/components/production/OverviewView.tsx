"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { AlertTriangle, CalendarClock, Factory, ShieldAlert, TrendingUp, Warehouse as WarehouseIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import { compactMoney, money, periodOf, quantity, type Warehouse } from "@/lib/procurement";
import { percentLabel, type ProductionOverview } from "@/lib/production";
import { todayInTashkent } from "@/lib/tasks";
import { card, field } from "./ProdKit";

function Tile({ label, value, hint, tone = "", href }: { label: string; value: string; hint?: string; tone?: string; href?: string }) {
  const t = useT();
  const body = (
    <>
      <span className={`block text-2xl font-semibold tabular-nums ${tone || "text-ink"}`}>{value}</span>
      <span className="block text-xs text-ink-3">{t(label)}</span>
      {hint && <span className="mt-0.5 block text-[11px] text-ink-3">{hint}</span>}
    </>
  );
  return href ? (
    <Link href={href} className="rounded-2xl border border-line bg-surface px-4 py-3 hover:border-accent/40 hover:bg-muted/40">
      {body}
    </Link>
  ) : (
    <div className="rounded-2xl border border-line bg-surface px-4 py-3">{body}</div>
  );
}

/**
 * Обзор сырья и производства: запас по складам (сумма), на контроле качества, сроки (7 и 30 дней, просрочено), брак за период по
 * поставщикам, ниже минимума, задания в работе и просроченные, расход и норма закрытых заданий (худшие отклонения), дней запаса, закупки
 * сырья на согласовании, рост цен. Период и склад — в строке фильтров.
 */
export function OverviewView({ data, warehouses }: { data: ProductionOverview; warehouses: Warehouse[] }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const today = todayInTashkent();
  const units = { bn: t("млрд"), mn: t("млн"), th: t("тыс.") };
  // Заявки на сырьё на согласовании — по шагам, только ненулевые (руководитель снабжения — прежний маршрут).
  const approvalParts = [
    (data.analystApproval ?? 0) > 0 && t("у аналитика {n}", { n: data.analystApproval! }),
    (data.financeApproval ?? 0) > 0 && t("у финансиста {n}", { n: data.financeApproval! }),
    data.directorApproval > 0 && t("у директора {n}", { n: data.directorApproval }),
    data.supplyApproval > 0 && t("у руководителя снабжения {n}", { n: data.supplyApproval }),
  ].filter((x): x is string => typeof x === "string");
  const apply = (patch: Record<string, string | null>) => {
    const next = new URLSearchParams(params.toString());
    for (const [k, v] of Object.entries(patch)) {
      if (v) next.set(k, v);
      else next.delete(k);
    }
    router.push(`${pathname}${next.size ? `?${next}` : ""}`, { scroll: false });
  };
  const rejectPct = data.decided > 0 ? Math.round((1000 * data.rejected) / data.decided) / 10 : 0;
  const normDev = data.normTotal > 0 ? Math.round(((data.actualTotal - data.normTotal) / data.normTotal) * 1000) / 10 : null;
  const active = warehouses.filter((w) => w.isActive);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        {(["month", "prev", "quarter", "year"] as const).map((p) => {
          const r = periodOf(p, today);
          const on = r.from === data.from && r.to === data.to;
          return (
            <button
              key={p}
              type="button"
              onClick={() => apply({ from: r.from, to: r.to })}
              aria-pressed={on}
              className={`h-9 rounded-lg border px-3 text-sm font-medium ${on ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink hover:bg-muted"}`}
            >
              {t({ month: "Этот месяц", prev: "Прошлый месяц", quarter: "3 месяца", year: "Год" }[p])}
            </button>
          );
        })}
        {active.length > 1 && (
          <select value={data.warehouseId ?? ""} onChange={(e) => apply({ warehouse: e.target.value || null })} aria-label={t("Склад")} className={`${field} ml-auto w-56`}>
            <option value="">{t("Все склады")}</option>
            {active.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </select>
        )}
      </div>

      <div className="grid grid-cols-2 gap-2 md:grid-cols-4 xl:grid-cols-6">
        <Tile label="Запас сырья, сум" value={compactMoney(data.stockValue, units)} hint={t("по цене партий и приходов")} href="/procurement/stock" />
        <Tile
          label="На контроле качества"
          value={String(data.onControl.count)}
          hint={data.onControl.count > 0 ? t("самая давняя — {n} дн.", { n: data.oldestControlDays }) : undefined}
          tone={data.onControl.count > 0 ? "text-warn" : ""}
          href="/production/quality"
        />
        <Tile label="Истекает за 7 дней" value={String(data.expiring7.count)} hint={money(data.expiring7.value)} tone={data.expiring7.count > 0 ? "text-warn" : ""} href="/procurement/batches?state=expiring" />
        <Tile label="Просрочено" value={String(data.expired.count)} hint={money(data.expired.value)} tone={data.expired.count > 0 ? "text-bad" : ""} href="/procurement/batches?state=expired" />
        <Tile label="Ниже минимума" value={String(data.belowMin)} tone={data.belowMin > 0 ? "text-warn" : ""} href="/procurement/stock?filter=low" />
        <Tile label="Заданий в работе" value={String(data.openOrders)} hint={data.overdueOrders > 0 ? t("просрочено {n}", { n: data.overdueOrders }) : undefined} tone={data.overdueOrders > 0 ? "text-bad" : ""} href="/production/orders" />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <section className={card} aria-labelledby="ov-consumption">
          <h2 id="ov-consumption" className="mb-1 flex items-center gap-2 text-base font-semibold text-ink">
            <Factory className="size-4 text-ink-3" aria-hidden /> {t("Расход и норма")}
          </h2>
          <p className="mb-3 text-xs text-ink-3">
            {t("Закрыто заданий: {n}; расход к норме: {p}; строк с перерасходом: {o}", { n: data.closedOrders, p: percentLabel(normDev), o: data.overconsumed })}
          </p>
          {data.deviations.length === 0 ? (
            <p className="text-sm text-ink-3">{t("Закрытых заданий за период нет.")}</p>
          ) : (
            <ul className="divide-y divide-line">
              {data.deviations.map((d, i) => (
                <li key={`${d.orderId}-${i}`} className="flex items-center justify-between gap-3 py-2 text-sm">
                  <span className="min-w-0">
                    <span className="block truncate text-ink">{d.item}</span>
                    <Link href={`/production/orders/${d.orderId}`} className="text-xs tabular-nums text-accent-strong hover:underline">
                      {d.orderNumber}
                    </Link>
                  </span>
                  <span className={`shrink-0 text-right tabular-nums ${d.overconsumed ? "font-semibold text-bad" : "text-ink-2"}`}>
                    {percentLabel(d.percent)}
                    <span className="block text-[11px] font-normal text-ink-3">
                      {quantity(d.actual)} / {quantity(d.norm)} {d.unitCode}
                    </span>
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className={card} aria-labelledby="ov-quality">
          <h2 id="ov-quality" className="mb-1 flex items-center gap-2 text-base font-semibold text-ink">
            <ShieldAlert className="size-4 text-ink-3" aria-hidden /> {t("Качество за период")}
          </h2>
          <p className="mb-3 text-xs text-ink-3">{t("Решений: {n}; забраковано: {r} ({p} %)", { n: data.decided, r: data.rejected, p: rejectPct })}</p>
          {data.rejectSuppliers.length === 0 ? (
            <p className="text-sm text-ink-3">{t("Забракованных партий за период нет.")}</p>
          ) : (
            <ul className="divide-y divide-line">
              {data.rejectSuppliers.map((s) => (
                <li key={s.name} className="flex items-center justify-between gap-3 py-2 text-sm">
                  <span className="min-w-0 truncate text-ink">{t(s.name)}</span>
                  <span className="shrink-0 tabular-nums text-bad">
                    {s.rejected} / {s.decided} · {quantity(s.percent)} %
                  </span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className={card} aria-labelledby="ov-days">
          <h2 id="ov-days" className="mb-1 flex items-center gap-2 text-base font-semibold text-ink">
            <CalendarClock className="size-4 text-ink-3" aria-hidden /> {t("Дней запаса")}
          </h2>
          <p className="mb-3 text-xs text-ink-3">{t("Остаток ÷ средний расход в день за 30 дней — меньше всего.")}</p>
          {data.lowDays.length === 0 ? (
            <p className="text-sm text-ink-3">{t("Расхода за 30 дней не было.")}</p>
          ) : (
            <ul className="divide-y divide-line">
              {data.lowDays.map((d) => (
                <li key={d.itemId} className="flex items-center justify-between gap-3 py-2 text-sm">
                  <span className="min-w-0">
                    <span className="block truncate text-ink">{d.name}</span>
                    <span className="text-xs text-ink-3">
                      {t("остаток {a} · в день {b}", { a: `${quantity(d.onHand)} ${d.unitCode}`, b: quantity(d.perDay) })}
                    </span>
                  </span>
                  <span className={`shrink-0 tabular-nums ${d.days < 7 ? "font-semibold text-bad" : d.days < 30 ? "text-warn" : "text-ink-2"}`}>{t("{n} дн.", { n: quantity(d.days) })}</span>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className={card} aria-labelledby="ov-stock">
          <h2 id="ov-stock" className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
            <WarehouseIcon className="size-4 text-ink-3" aria-hidden /> {t("Запас по складам")}
          </h2>
          <ul className="divide-y divide-line">
            {data.stock.map((s) => (
              <li key={s.warehouseId} className="flex items-center justify-between gap-3 py-2 text-sm">
                <span className="min-w-0 truncate text-ink">
                  {s.name} <span className="text-xs text-ink-3">· {t("позиций: {n}", { n: s.items })}</span>
                </span>
                <span className="shrink-0 tabular-nums text-ink">
                  {money(s.value)} {t("сум")}
                </span>
              </li>
            ))}
          </ul>
          {approvalParts.length > 0 && (
            <p className="mt-3 rounded-lg bg-muted px-3 py-2 text-xs text-ink-2">
              {t("Закупка сырья на согласовании: {parts} — на {s} сум", { parts: approvalParts.join(", "), s: money(data.approvalAmount) })}
            </p>
          )}
        </section>

        {data.prices.length > 0 && (
          <section className={`${card} lg:col-span-2`} aria-labelledby="ov-prices">
            <h2 id="ov-prices" className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
              <TrendingUp className="size-4 text-ink-3" aria-hidden /> {t("Рост цен за период")}
            </h2>
            <ul className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
              {data.prices.map((p) => (
                <li key={p.name} className="rounded-xl border border-line px-3 py-2 text-sm">
                  <span className="block truncate text-ink">{p.name}</span>
                  <span className="text-xs tabular-nums text-ink-3">
                    {money(p.firstPrice)} → {money(p.lastPrice)} {t("за {unit}", { unit: p.unitCode })}
                  </span>
                  <span className="float-right inline-flex items-center gap-1 text-xs font-semibold text-bad">
                    <AlertTriangle className="size-3" aria-hidden /> {percentLabel(p.changePercent)}
                  </span>
                </li>
              ))}
            </ul>
          </section>
        )}
      </div>
    </div>
  );
}
