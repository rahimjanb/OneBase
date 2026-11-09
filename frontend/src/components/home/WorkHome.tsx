"use client";

import Link from "next/link";
import { useState } from "react";
import { ArrowRight, CalendarClock, ClipboardCheck, FlaskConical, Gavel, PackageCheck, PackageOpen, ShoppingBag, TriangleAlert, Truck } from "lucide-react";
import { ArrivalSheet } from "@/components/procurement/ArrivalSheet";
import { ArrivalItem, ExpectedCard } from "@/components/procurement/ArrivalsView";
import { useT } from "@/i18n/client";
import { roleNames, type ExpectedDelivery, type HomeView } from "@/lib/arrivals";
import { money, qualityCheckStatusLabel, quantity, stampRu } from "@/lib/procurement";

const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";

/** Показатель: значение и подпись под ним; tone — цвет значения, alarm — подпись красным (просрочено). */
type Stat = { key: string; label: string; value: string; note?: string; href: string; tone?: "bad" | "accent" | "warn"; alarm?: boolean; icon: typeof Truck };

function StatCard({ s }: { s: Stat }) {
  const t = useT();
  const tone = s.tone === "bad" ? "text-bad" : s.tone === "accent" ? "text-accent-strong" : s.tone === "warn" ? "text-warn" : "text-ink";
  return (
    <Link href={s.href} className={`${card} block transition-colors hover:bg-muted/40`}>
      <span className="flex items-center justify-between gap-2 text-sm text-ink-2">
        {t(s.label)}
        <s.icon className="size-4 text-ink-3" aria-hidden />
      </span>
      <span className={`mt-1.5 block text-2xl font-semibold tabular-nums ${tone}`}>{s.value}</span>
      {s.note && <span className={`mt-0.5 block text-xs ${s.alarm ? "font-semibold text-bad" : "text-ink-3"}`}>{s.note}</span>}
    </Link>
  );
}

function Block({ title, href, more, children, id }: { title: string; href?: string; more?: string; children: React.ReactNode; id: string }) {
  const t = useT();
  return (
    <section aria-labelledby={id}>
      <div className="mb-2 flex items-baseline justify-between gap-3">
        <h2 id={id} className="text-base font-semibold text-ink">
          {title}
        </h2>
        {href && (
          <Link href={href} className="inline-flex items-center gap-1 text-sm font-medium text-accent-strong hover:underline">
            {more ?? t("Все")} <ArrowRight className="size-3.5" aria-hidden />
          </Link>
        )}
      </div>
      {children}
    </section>
  );
}

function Quiet({ text }: { text: string }) {
  return <p className={`${card} text-sm text-ink-3`}>{text}</p>;
}

/** Показатели рабочего стола по ролям: у кладовщика — машины и выдача, у лаборатории — проверки, у снабжения и аналитика — поставки. */
function stats(h: HomeView, t: ReturnType<typeof useT>): Stat[] {
  const r = h.roles;
  const ready = h.arrivals.filter((a) => a.status === "Ready").length;
  const inQuality = h.arrivals.filter((a) => a.status === "Quality").length;
  const list: Stat[] = [];
  const add = (s: Stat) => !list.some((x) => x.key === s.key) && list.push(s);
  if (h.requests.awaitingMe > 0 || r.analyst || r.head)
    add({ key: "approvals", label: "Ждут вашего согласования", value: String(h.requests.awaitingMe), href: "/requests/approvals", tone: "accent", icon: Gavel });
  if (r.warehouse) {
    add({
      key: "trucks",
      label: "Машины на приёмке",
      value: String(h.arrivals.length),
      note: ready > 0 ? t("готово к приёмке: {n}", { n: ready }) : inQuality > 0 ? t("на проверке качества: {n}", { n: inQuality }) : undefined,
      href: "/procurement/arrivals?tab=open",
      tone: ready > 0 ? "accent" : undefined,
      icon: Truck,
    });
    add({
      key: "today",
      label: "Ожидается сегодня",
      value: String(h.dueToday),
      note: h.overdue > 0 ? t("просрочено: {n}", { n: h.overdue }) : undefined,
      href: "/procurement/arrivals",
      tone: h.dueToday > 0 ? "accent" : undefined,
      alarm: h.overdue > 0,
      icon: CalendarClock,
    });
    if (h.stock) add({ key: "issue", label: "К выдаче", value: String(h.stock.toIssue), note: t("заявок"), href: "/procurement/issue", icon: PackageCheck });
    if (h.stock)
      add({ key: "low", label: "Ниже минимума", value: String(h.stock.belowMinimum), note: t("позиций"), href: "/procurement/stock?filter=low", tone: h.stock.belowMinimum > 0 ? "warn" : undefined, icon: TriangleAlert });
  }
  if (h.quality) {
    add({ key: "checks", label: "Ждут вашей проверки", value: String(h.quality.atMyStep), note: t("всего открыто: {n}", { n: h.quality.open }), href: "/production/quality", tone: h.quality.atMyStep > 0 ? "accent" : undefined, icon: FlaskConical });
    add({ key: "trucksQc", label: "Товар в машинах", value: String(h.quality.inTrucks), note: t("ждёт проверки до склада"), href: "/procurement/arrivals?tab=open", tone: h.quality.inTrucks > 0 ? "warn" : undefined, icon: Truck });
    if (h.stock) add({ key: "control", label: "На контроле на складе", value: String(h.stock.onControl), note: t("партий"), href: "/procurement/batches?state=control", icon: ClipboardCheck });
    if (h.stock) add({ key: "expiring", label: "Истекает срок", value: String(h.stock.expiring), note: h.stock.expired > 0 ? t("просрочено: {n}", { n: h.stock.expired }) : t("партий"), href: "/procurement/batches?state=expiring", tone: h.stock.expiring > 0 ? "warn" : undefined, alarm: h.stock.expired > 0, icon: CalendarClock });
  }
  if (r.supply || r.analyst || r.director) {
    add({ key: "upcoming", label: "Ожидаются поставки", value: String(h.upcomingTotal), note: h.overdue > 0 ? t("просрочено: {n}", { n: h.overdue }) : h.dueToday > 0 ? t("сегодня: {n}", { n: h.dueToday }) : undefined, href: "/procurement/arrivals", alarm: h.overdue > 0, icon: CalendarClock });
    add({ key: "open", label: "На приёмке", value: String(h.arrivals.length), note: inQuality > 0 ? t("на проверке качества: {n}", { n: inQuality }) : undefined, href: "/procurement/arrivals?tab=open", icon: Truck });
    add({ key: "accepted", label: "Принято за неделю", value: money(h.acceptedAmount), note: t("сум · поставок: {n}", { n: h.acceptedCount }), href: "/procurement/arrivals?tab=done", icon: PackageOpen });
  }
  if (r.supply && h.requests.toBuy !== null)
    add({ key: "buy", label: "К закупке", value: String(h.requests.toBuy), note: t("на {sum} сум", { sum: money(h.requests.toBuyAmount ?? 0) }), href: "/procurement/buy", icon: ShoppingBag });
  if (r.head) {
    add({ key: "dept", label: "Заявки отдела в работе", value: String(h.requests.departmentOpen), href: "/requests/department", icon: ClipboardCheck });
    add({ key: "pickup", label: "Можно получить на складе", value: String(h.requests.departmentToPickUp), note: t("заявок отдела"), href: "/requests/department", tone: h.requests.departmentToPickUp > 0 ? "accent" : undefined, icon: PackageCheck });
  }
  return list.slice(0, 4);
}

/**
 * Рабочий стол — главная кладовщика, лаборатории, снабжения и аналитика. Сверху — показатели роли; слева — машины на приёмке (где каждая:
 * у лаборатории, у кладовщика) и ближайшие поставки по сроку («Машина приехала» — кладовщику); справа — что ждёт решения пользователя
 * (согласования, проверки качества) и принятое на склад за неделю. Только настоящие данные; пустой блок говорит, что дел нет.
 */
export function WorkHome({ data, name, dateLabel, companyHref }: { data: HomeView; name: string; dateLabel: string; companyHref: string | null }) {
  const t = useT();
  const [arriving, setArriving] = useState<ExpectedDelivery | null>(null);
  const r = data.roles;
  const list = stats(data, t);
  const seesTrucks = data.seesDeliveries && (r.warehouse || r.quality || r.decidesQuality || r.supply || r.analyst || data.arrivals.length > 0);
  const approvals = data.requests.approvals;

  return (
    <div className="mx-auto flex max-w-[1440px] flex-col gap-5 px-4 pb-12 pt-4 sm:px-6 lg:px-8 lg:pt-8">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm text-ink-3">{[dateLabel, ...roleNames(r).map((x) => t(x))].join(" · ")}</p>
          <h1 className="mt-0.5 text-2xl font-semibold text-ink lg:text-[28px]">{name ? t("Рабочий стол — {name}", { name }) : t("Рабочий стол")}</h1>
        </div>
        {companyHref && (
          <Link href={companyHref} className="inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-3.5 text-sm font-medium text-ink hover:bg-muted">
            {t("Обзор компании")}
          </Link>
        )}
      </header>

      {list.length > 0 && (
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          {list.map((s) => (
            <StatCard key={s.key} s={s} />
          ))}
        </div>
      )}

      <div className="grid grid-cols-1 gap-5 lg:grid-cols-[minmax(0,1fr)_380px] lg:items-start">
        <div className="flex min-w-0 flex-col gap-5">
          {seesTrucks && (
            <Block id="home-trucks" title={t("Машины на приёмке")} href={data.arrivals.length > 0 ? "/procurement/arrivals?tab=open" : undefined}>
              {data.arrivals.length === 0 ? (
                <Quiet text={r.warehouse ? t("Машин на приёмке нет. Приехала машина — нажмите «Машина приехала» у поставки ниже.") : t("Машин на приёмке нет.")} />
              ) : (
                <ul className="space-y-2">
                  {data.arrivals.slice(0, 8).map((a) => (
                    <ArrivalItem key={a.id} row={a} />
                  ))}
                </ul>
              )}
            </Block>
          )}
          {data.seesDeliveries && (
            <Block id="home-upcoming" title={t("Ближайшие поставки")} href="/procurement/arrivals" more={data.upcomingTotal > data.upcoming.length ? t("Все {n}", { n: data.upcomingTotal }) : t("Все")}>
              {data.upcoming.length === 0 ? (
                <Quiet text={t("Поставок не ждём: всё привезено, тендеров и покупок с доставкой нет.")} />
              ) : (
                <ul className="space-y-2">
                  {data.upcoming.map((d) => (
                    <ExpectedCard key={d.sourceId} delivery={d} onArrive={setArriving} compact seesTenders={r.warehouse || r.supply || r.analyst || r.director} />
                  ))}
                </ul>
              )}
            </Block>
          )}
        </div>

        <div className="flex flex-col gap-5">
          {(approvals.length > 0 || r.head || r.analyst || r.financier || r.director) && (
            <Block id="home-approvals" title={t("Ждут вашего согласования")} href={approvals.length > 0 ? "/requests/approvals" : undefined}>
              {approvals.length === 0 ? (
                <Quiet text={t("Заявок на вашем шаге нет.")} />
              ) : (
                <ul className={`${card} divide-y divide-line p-0 sm:p-0`}>
                  {approvals.map((a) => (
                    <li key={a.id}>
                      <Link href={`/requests/${a.id}`} className="block px-4 py-3 hover:bg-muted/40">
                        <span className="flex items-baseline justify-between gap-3">
                          <span className="min-w-0 truncate text-sm font-medium text-ink">{a.title}</span>
                          <span className="shrink-0 text-sm tabular-nums text-ink">{money(a.amount)}</span>
                        </span>
                        <span className="mt-0.5 block text-xs text-ink-3">{[a.number, a.department, a.initiator, a.submittedAt ? stampRu(a.submittedAt) : null].filter(Boolean).join(" · ")}</span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </Block>
          )}

          {data.quality && (
            <Block id="home-quality" title={t("Проверки качества")} href="/production/quality">
              {data.quality.checks.length === 0 ? (
                <Quiet text={t("Открытых проверок нет.")} />
              ) : (
                <ul className={`${card} divide-y divide-line p-0 sm:p-0`}>
                  {data.quality.checks.map((c) => (
                    <li key={c.id}>
                      <Link href={`/production/quality/${c.id}`} className="block px-4 py-3 hover:bg-muted/40">
                        <span className="flex items-baseline justify-between gap-3">
                          <span className="min-w-0 truncate text-sm font-medium text-ink">{c.itemName}</span>
                          <span className={`shrink-0 text-xs font-semibold ${c.atMyStep ? "text-accent-strong" : "text-ink-3"}`}>{t(qualityCheckStatusLabel[c.status])}</span>
                        </span>
                        <span className="mt-0.5 block text-xs text-ink-3">
                          {[
                            c.number,
                            c.batchNumber ?? (c.arrivalNumber ? t("{n} · в машине", { n: c.arrivalNumber }) : null),
                            `${quantity(c.received)} ${t(c.unitCode)}`,
                            c.supplierName,
                          ]
                            .filter(Boolean)
                            .join(" · ")}
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </Block>
          )}

          {data.seesDeliveries && (r.supply || r.analyst || r.financier || r.director || r.warehouse) && (
            <Block id="home-accepted" title={t("Принято на склад за неделю")} href={data.acceptedCount > 0 ? "/procurement/arrivals?tab=done" : undefined}>
              {data.accepted.length === 0 ? (
                <Quiet text={t("За неделю на склад ничего не принято.")} />
              ) : (
                <ul className={`${card} divide-y divide-line p-0 sm:p-0`}>
                  {data.accepted.map((a) => (
                    <li key={a.id}>
                      <Link href={`/procurement/arrivals/${a.id}`} className="block px-4 py-3 hover:bg-muted/40">
                        <span className="flex items-baseline justify-between gap-3">
                          <span className="min-w-0 truncate text-sm font-medium text-ink">{a.supplierName}</span>
                          <span className="shrink-0 text-sm tabular-nums text-ink">{money(a.acceptedTotal)}</span>
                        </span>
                        <span className="mt-0.5 block truncate text-xs text-ink-3">{[a.summary, a.receiptNumber, a.acceptedAt ? stampRu(a.acceptedAt) : null].filter(Boolean).join(" · ")}</span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </Block>
          )}
        </div>
      </div>

      {arriving && <ArrivalSheet delivery={arriving} onClose={() => setArriving(null)} />}
    </div>
  );
}
