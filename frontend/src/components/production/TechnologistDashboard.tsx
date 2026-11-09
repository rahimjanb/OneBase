"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { AlertTriangle, BadgeCheck, ClipboardList, FileBarChart, FlaskConical, PackageX, TrendingUp } from "lucide-react";
import { useT } from "@/i18n/client";
import { money, periodOf, quantity } from "@/lib/procurement";
import { dayRu, percentLabel, productionOrderStatusLabel, type DashboardBar, type ProductionDashboard } from "@/lib/production";
import { todayInTashkent } from "@/lib/tasks";
import { OrderStatusPill, card } from "./ProdKit";

function Stat({ label, value, note, sub, tone = "" }: { label: string; value: string; note?: string; sub?: { text: string; title?: string }; tone?: string }) {
  const t = useT();
  return (
    <div className="rounded-2xl border border-line bg-surface px-4 py-3">
      <span className="block text-xs text-ink-3">{t(label)}</span>
      <span className={`mt-1 block text-2xl font-semibold tabular-nums ${tone || "text-ink"}`}>{value}</span>
      {note && <span className="mt-0.5 block text-xs text-ink-3">{note}</span>}
      {sub && <span className="block text-xs tabular-nums text-ink-3" title={sub.title}>{sub.text}</span>}
    </div>
  );
}

/** Стоимость брака по цене партий: сумма; без цены партии — сколько материалов не посчитано (их список — в подсказке). */
function defectCostLine(data: ProductionDashboard, t: ReturnType<typeof useT>): { text: string; title?: string } | undefined {
  const missing = data.defectCostUnpriced;
  const title = missing.length > 0 ? `${t("Без цены партии")}: ${missing.join(", ")}` : undefined;
  if (data.defectCost !== null) {
    return { text: missing.length > 0 ? t("{s} сум · без цены: {n}", { s: money(data.defectCost), n: missing.length }) : t("{s} сум", { s: money(data.defectCost) }), title };
  }
  return missing.length > 0 ? { text: t("стоимость: нет цены партии"), title } : undefined;
}

/** Ранжированный список с тонкой полосой: значение и, если есть, дополнительное (брак). */
function Ranked({ title, rows, unit, empty, extraLabel }: { title: string; rows: DashboardBar[]; unit?: string; empty: string; extraLabel?: string }) {
  const t = useT();
  const max = Math.max(...rows.map((r) => r.value), 0);
  return (
    <section className={card}>
      <h2 className="mb-3 text-sm font-semibold text-ink">{t(title)}</h2>
      {rows.length === 0 ? (
        <p className="text-sm text-ink-3">{t(empty)}</p>
      ) : (
        <ul className="space-y-2.5">
          {rows.map((r) => (
            <li key={r.name} className="text-sm">
              <div className="flex items-baseline justify-between gap-3">
                <span className="min-w-0 truncate text-ink">{r.name}</span>
                <span className="shrink-0 tabular-nums text-ink-2">
                  {quantity(Math.round(r.value * 1000) / 1000)} {r.unit ?? unit ?? t("кг")}
                  {extraLabel && r.extra !== null && r.extra > 0 && <span className="text-xs text-bad"> · {t(extraLabel, { kg: quantity(r.extra) })}</span>}
                </span>
              </div>
              <div className="mt-1 h-1.5 rounded-full bg-muted">
                <div className="h-1.5 rounded-full bg-accent" style={{ width: `${max > 0 ? Math.max(2, (r.value / max) * 100) : 0}%` }} />
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/**
 * Панель «Технолога»: выпуск сегодня и за период, план и выполнение, брак, норма и факт расхода, выход после выпечки; главный график —
 * выпуск и брак по дням; по сменам и продуктам, брак по этапам и видам, отклонения расхода, брак упаковки; задания в работе (с нехваткой
 * сырья) и что требует внимания. Только данные утверждённых смен и закрытых заданий; продукты в штуках в килограммы не складываются.
 */
export function TechnologistDashboard({ data }: { data: ProductionDashboard }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const today = todayInTashkent();
  const kg = (n: number) => `${quantity(Math.round(n * 10) / 10)} ${t("кг")}`;
  const max = Math.max(...data.days.map((d) => d.goodKg + d.defectKg), 0);
  const apply = (from: string, to: string) => {
    const next = new URLSearchParams(params.toString());
    next.set("from", from);
    next.set("to", to);
    router.push(`${pathname}?${next}`, { scroll: false });
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-2">
        {(["month", "prev", "quarter", "year"] as const).map((p) => {
          const r = periodOf(p, today);
          const on = r.from === data.from && r.to === data.to;
          return (
            <button key={p} type="button" onClick={() => apply(r.from, r.to)} aria-pressed={on}
              className={`h-9 rounded-lg border px-3 text-sm font-medium ${on ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink hover:bg-muted"}`}>
              {t({ month: "Этот месяц", prev: "Прошлый месяц", quarter: "3 месяца", year: "Год" }[p])}
            </button>
          );
        })}
        <nav className="ml-auto flex flex-wrap gap-2" aria-label={t("Технолог")}>
          <Link href="/production/recipes" className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm text-ink hover:bg-muted">
            <FlaskConical className="size-4 text-ink-3" aria-hidden /> {t("Техкарты")}
          </Link>
          <Link href="/production/shifts" className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm text-ink hover:bg-muted">
            <ClipboardList className="size-4 text-ink-3" aria-hidden /> {t("Смены")}
          </Link>
          <Link href="/production/reports" className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3 text-sm text-ink hover:bg-muted">
            <FileBarChart className="size-4 text-ink-3" aria-hidden /> {t("Отчёты")}
          </Link>
        </nav>
      </div>

      <div className="grid grid-cols-2 gap-2 md:grid-cols-4 xl:grid-cols-6">
        <Stat label="Выпуск сегодня" value={kg(data.todayKg)} />
        <Stat label="Выпуск за период" value={kg(data.goodKg)} note={data.planKg > 0 ? t("план {kg}", { kg: kg(data.planKg) }) : t("плана на период нет")} />
        <Stat label="Выполнение плана" value={data.completionPercent !== null ? `${quantity(data.completionPercent)} %` : "—"} tone={data.completionPercent !== null && data.completionPercent < 90 ? "text-warn" : ""} />
        <Stat label="Брак" value={kg(data.defectKg)} note={data.defectPercent !== null ? t("{p} % от выпуска", { p: quantity(data.defectPercent) }) : undefined}
          sub={defectCostLine(data, t)} tone={data.defectKg > 0 ? "text-bad" : ""} />
        <Stat label="Расход к норме" value={percentLabel(data.normDeviationPercent)} note={data.normKg > 0 ? t("норма {a} · факт {b}", { a: kg(data.normKg), b: kg(data.factKg) }) : t("закрытых заданий нет")}
          tone={data.normDeviationPercent !== null && data.normDeviationPercent > 5 ? "text-bad" : ""} />
        <Stat label="Выход после выпечки" value={data.bakeYieldPercent !== null ? `${quantity(data.bakeYieldPercent)} %` : "—"} />
      </div>
      <p className="-mt-1 text-xs text-ink-3">
        {t("Заданий в работе: {a} · открытых смен: {b}, ждут утверждения: {c} · техкарт на утверждении: {d}", { a: data.activeOrders, b: data.openShifts, c: data.closedShifts, d: data.pendingRecipes })}
        {data.pieceOutputs > 0 && ` · ${t("смен по продуктам в штуках: {n} — в килограммы не складываются", { n: data.pieceOutputs })}`}
      </p>

      <section className={card} aria-labelledby="td-days">
        <h2 id="td-days" className="mb-1 flex items-center gap-2 text-base font-semibold text-ink">
          <TrendingUp className="size-4 text-ink-3" aria-hidden /> {t("Выпуск и брак по дням")}
        </h2>
        <p className="mb-3 text-xs text-ink-3">{t("Годный выход утверждённых смен, кг; красным — брак.")}</p>
        {max === 0 ? (
          <p className="py-6 text-center text-sm text-ink-3">{t("Утверждённых смен за период нет.")}</p>
        ) : (
          <div className="flex h-44 items-end gap-[3px]" role="img" aria-label={t("Выпуск и брак по дням")}>
            {data.days.map((d) => (
              <div key={d.date} className="group relative flex h-full min-w-0 flex-1 flex-col justify-end" title={`${dayRu(d.date)}: ${kg(d.goodKg)}${d.defectKg > 0 ? `, ${t("брак")} ${kg(d.defectKg)}` : ""}`}>
                <div className="rounded-t-[3px] bg-bad/70" style={{ height: `${(d.defectKg / max) * 100}%` }} />
                <div className={`${d.defectKg > 0 ? "" : "rounded-t-[3px]"} bg-accent group-hover:bg-accent-strong`} style={{ height: `${(d.goodKg / max) * 100}%` }} />
              </div>
            ))}
          </div>
        )}
        {data.days.length > 0 && max > 0 && (
          <div className="mt-1 flex justify-between text-[11px] tabular-nums text-ink-3">
            <span>{dayRu(data.days[0].date)}</span>
            <span>{dayRu(data.days[data.days.length - 1].date)}</span>
          </div>
        )}
      </section>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        <Ranked title="По продуктам" rows={data.byProduct} empty="Выпуска за период нет." extraLabel="брак {kg} кг" />
        <Ranked title="Брак по этапам" rows={data.defectsByStage} empty="Брака за период нет." />
        <Ranked title="Брак по видам" rows={data.defectsByType} empty="Брака за период нет." />
        <Ranked title="По сменам" rows={data.byShift} empty="Выпуска за период нет." />
        <Ranked title="Брак упаковки" rows={data.packagingDefects} empty="Брака упаковки за период нет." />
        <section className={card}>
          <h2 className="mb-3 text-sm font-semibold text-ink">{t("Отклонения расхода")}</h2>
          {data.normFact.length === 0 ? (
            <p className="text-sm text-ink-3">{t("Закрытых заданий за период нет.")}</p>
          ) : (
            <ul className="divide-y divide-line">
              {data.normFact.map((n) => (
                <li key={n.name} className="flex items-center justify-between gap-3 py-2 text-sm">
                  <span className="min-w-0">
                    <span className="block truncate text-ink">{n.name}</span>
                    <span className="text-xs tabular-nums text-ink-3">
                      {t("норма {a} · факт {b}", { a: `${quantity(n.norm)} ${n.unit}`, b: `${quantity(n.fact)} ${n.unit}` })}
                    </span>
                  </span>
                  <span className={`shrink-0 tabular-nums ${(n.percent ?? 0) > 5 ? "font-semibold text-bad" : "text-ink-2"}`}>{percentLabel(n.percent)}</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_380px]">
        <section className={card}>
          <h2 className="mb-3 text-base font-semibold text-ink">{t("Задания в работе")}</h2>
          {data.orders.length === 0 ? (
            <p className="text-sm text-ink-3">{t("Выпущенных заданий нет.")}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[560px] text-sm">
                <thead>
                  <tr className="border-b border-line text-left text-[11px] text-ink-3">
                    <th className="py-2 pr-3 font-semibold">{t("Задание")}</th>
                    <th className="py-2 pr-3 font-semibold">{t("Продукт")}</th>
                    <th className="py-2 pr-3 text-right font-semibold">{t("Выпущено из плана")}</th>
                    <th className="py-2 text-right font-semibold">{t("Статус")}</th>
                  </tr>
                </thead>
                <tbody>
                  {data.orders.map((o) => (
                    <tr key={o.id} className="border-b border-line last:border-0">
                      <td className="py-2 pr-3">
                        <Link href={`/production/orders/${o.id}`} className="font-medium tabular-nums text-accent-strong hover:underline">
                          {o.number}
                        </Link>
                        <span className="block text-xs text-ink-3">{dayRu(o.plannedDate)}</span>
                      </td>
                      <td className="py-2 pr-3 text-ink">
                        {o.product}
                        {o.shortages > 0 && <span className="block text-xs text-bad">{t("не хватает сырья: позиций {n}", { n: o.shortages })}</span>}
                      </td>
                      <td className="whitespace-nowrap py-2 pr-3 text-right tabular-nums">
                        {quantity(o.good)} / {quantity(o.planned)} {o.unit}
                      </td>
                      <td className="py-2 text-right" title={t(productionOrderStatusLabel[o.status])}>
                        <OrderStatusPill status={o.status} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
        <section className={card}>
          <h2 className="mb-3 text-base font-semibold text-ink">{t("Требует внимания")}</h2>
          {data.issues.length === 0 ? (
            <p className="flex items-center gap-2 text-sm text-ink-3">
              <BadgeCheck className="size-4 text-ok" aria-hidden /> {t("Нехватки, перерасхода, брака выше норматива и ожидающих утверждения нет.")}
            </p>
          ) : (
            <ul className="divide-y divide-line">
              {data.issues.map((i) => (
                <li key={`${i.kind}-${i.link}-${i.title}`}>
                  <Link href={i.link} className="flex items-start gap-2.5 py-2.5 text-sm hover:bg-muted/40">
                    {i.kind === "approval" ? (
                      <BadgeCheck className="mt-0.5 size-4 shrink-0 text-violet-light" aria-hidden />
                    ) : i.kind === "shortage" ? (
                      <PackageX className="mt-0.5 size-4 shrink-0 text-warn" aria-hidden />
                    ) : (
                      <AlertTriangle className="mt-0.5 size-4 shrink-0 text-bad" aria-hidden />
                    )}
                    <span className="min-w-0">
                      <span className="block text-ink">{t(i.title)}</span>
                      <span className="block text-xs text-ink-3">{t(i.detail)}</span>
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  );
}
