"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState, useTransition } from "react";
import { AlertTriangle, CalendarClock, ChevronRight, ClipboardList, FileBarChart, Newspaper, PackageCheck, ShieldAlert, Sparkles, Timer, TrendingDown, TrendingUp } from "lucide-react";
import { useT } from "@/i18n/client";
import { compactMoney, dateRu, periodOf, quantity, stampRu, type Dashboard, type Kpis } from "@/lib/procurement";
import { todayInTashkent } from "@/lib/tasks";
import { AmountBars, MonthColumns, useUnits } from "./Charts";
import { OrderStatusPill, RatingBadge } from "./ProcKit";

const card = "rounded-2xl border border-line bg-surface p-4 sm:p-5";
const field = "h-9 rounded-lg border border-line bg-surface px-2.5 text-sm text-ink focus:border-accent focus:outline-none";

type Tone = "bad" | "warn" | "ok" | undefined;

/** Плитка показателя: подпись, значение, пояснение; тон — только для состояния (просрочки), всегда с иконкой и словом. */
function Tile({ label, value, hint, href, tone, icon: Icon }: { label: string; value: string; hint?: string; href?: string; tone?: Tone; icon?: typeof AlertTriangle }) {
  const t = useT();
  const body = (
    <>
      <div className={`flex items-center gap-1.5 text-xl font-semibold text-ink ${tone === "bad" ? "text-bad" : tone === "warn" ? "text-warn" : ""}`}>
        {Icon && tone && <Icon className="size-4 shrink-0" aria-hidden />}
        {value}
      </div>
      <div className="mt-0.5 text-xs text-ink-3">{t(label)}</div>
      {hint && <div className="mt-0.5 text-[11px] text-ink-3">{hint}</div>}
    </>
  );
  const cls = "block rounded-xl border border-line bg-surface p-4";
  return href ? (
    <Link href={href} className={`${cls} transition-colors hover:border-accent/40`}>
      {body}
    </Link>
  ) : (
    <div className={cls}>{body}</div>
  );
}

function Section({ title, children, action }: { title: string; children: React.ReactNode; action?: React.ReactNode }) {
  const t = useT();
  return (
    <section className="space-y-3">
      <div className="flex items-baseline justify-between gap-3">
        <h2 className="text-lg font-semibold text-ink">{t(title)}</h2>
        {action}
      </div>
      {children}
    </section>
  );
}

/**
 * Вопросы Supply AI по роли: директору и снабжению — цены, переплаты, поставщики, объединение заказов; кладовщику — минимальные остатки;
 * всем — просроченные поставки. Ссылка только открывает консультанта с готовым вопросом — отправляет его сам пользователь.
 */
function AskAi({ data }: { data: Dashboard }) {
  const t = useT();
  const questions = [
    data.kpis ? "Какие закупочные цены выросли больше чем на 10% за 3 месяца и почему?" : null,
    data.kpis ? "Где мы переплачиваем поставщикам?" : null,
    data.director || data.head ? "Какие поставщики работают хуже всего?" : null,
    data.kpis ? "Какие заявки можно объединить в один заказ?" : null,
    "Какие поставки просрочены?",
    data.warehouse ? "Что на складе ниже минимума и уже ли это в закупке?" : null,
  ].filter((q): q is string => q !== null);
  // На телефоне — одна строка с прокруткой (показатели не уезжают вниз), шире — с переносом.
  return (
    <div className="space-y-2 sm:flex sm:flex-wrap sm:items-center sm:gap-2 sm:space-y-0">
      <span className="flex items-center gap-1.5 text-sm font-medium text-ink-2">
        <Sparkles className="size-4 text-accent" aria-hidden /> {t("Спросить Supply AI")}
      </span>
      <div className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1 sm:contents">
        {questions.map((q) => (
          <Link
            key={q}
            href={`/consultant?ask=${encodeURIComponent(t(q))}&dept=supply`}
            className="shrink-0 whitespace-nowrap rounded-full border border-line bg-surface px-3 py-1.5 text-sm text-ink hover:border-accent/40 hover:bg-muted sm:whitespace-normal"
          >
            {t(q)}
          </Link>
        ))}
      </div>
    </div>
  );
}

const noData = (n: number | null, render: (v: number) => string, t: ReturnType<typeof useT>) => (n === null ? t("нет данных") : render(n));

function KpiRow({ k }: { k: Kpis }) {
  const t = useT();
  const units = useUnits();
  const days = (v: number) => `${v.toString().replace(".", ",")} ${t("дн.")}`;
  return (
    <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
      <Tile label="Среднее время закупки" value={noData(k.avgPurchaseDays, days, t)} hint={t("от утверждения до поставки · заказов: {n}", { n: k.purchasesMeasured })} />
      <Tile
        label="Поставки в срок"
        value={noData(k.onTimePercent, (v) => `${v.toString().replace(".", ",")} %`, t)}
        hint={t("поставлено заказов со сроком: {n}", { n: k.deliveriesMeasured })}
        tone={k.onTimePercent !== null && k.onTimePercent < 80 ? "warn" : undefined}
        icon={AlertTriangle}
      />
      <Tile
        label={k.savings >= 0 ? "Экономия" : "Перерасход"}
        value={`${compactMoney(Math.abs(k.savings), units)} ${t("сум")}`}
        hint={k.savingsLines > 0 ? `${k.savingsPercent !== null ? `${Math.abs(k.savingsPercent).toString().replace(".", ",")} % · ` : ""}${t("против цен в заявках, позиций: {n}", { n: k.savingsLines })}` : t("в заявках не было цен")}
        tone={k.savings < 0 ? "warn" : undefined}
        icon={TrendingDown}
      />
      <Tile label="Выполнено заявок" value={String(k.fulfilledRequests)} hint={t("перешли в «Выполнена» за период")} />
      <Tile label="Претензий" value={String(k.claims)} hint={t("брак или недостача в приёмках")} tone={k.claims > 0 ? "warn" : undefined} icon={ShieldAlert} />
      <Tile
        label="Качество поставщиков"
        value={noData(k.supplierQuality, (v) => `${v.toString().replace(".", ",")} / 100`, t)}
        hint={t("средний рейтинг, поставщиков: {n}", { n: k.suppliersRated })}
      />
    </div>
  );
}

/**
 * Обзор «Снабжения»: фильтр периода (одной строкой над всем), KPI, блоки по роли — директор (закупки, согласования, просрочки, расходы
 * по месяцам, отделам и категориям, лучшие и худшие поставщики), руководитель снабжения (нагрузка и сроки сотрудников), сотрудник
 * снабжения (мои заказы, задачи, ближайшие сроки), кладовщик (поступления, приёмки, остатки, выдачи), последние сводки.
 */
/** ask — пользователю открыт AI-консультант: показать быстрые вопросы Supply AI. */
export function DashboardView({ data, ask = false }: { data: Dashboard; ask?: boolean }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [pending, start] = useTransition();
  const units = useUnits();
  const today = todayInTashkent();
  const [from, setFrom] = useState(data.from);
  const [to, setTo] = useState(data.to);
  useEffect(() => {
    setFrom(data.from);
    setTo(data.to);
  }, [data.from, data.to]);
  const go = (range: { from: string; to: string }) => {
    const next = new URLSearchParams(params.toString());
    next.set("from", range.from);
    next.set("to", range.to);
    start(() => router.push(`${pathname}?${next}`, { scroll: false }));
  };
  const presets = [
    { key: "month", label: "Этот месяц" },
    { key: "prev", label: "Прошлый месяц" },
    { key: "quarter", label: "3 месяца" },
    { key: "year", label: "Год" },
  ] as const;
  const active = presets.find((p) => {
    const r = periodOf(p.key, today);
    return r.from === data.from && r.to === data.to;
  })?.key;
  const d = data.director;
  const h = data.head;
  const b = data.buyer;
  const w = data.warehouse;
  const days = (v: number | null) => (v === null ? t("нет данных") : `${v.toString().replace(".", ",")} ${t("дн.")}`);

  return (
    <div className={`space-y-8 transition-opacity ${pending ? "opacity-60" : ""}`}>
      <div className="flex flex-wrap items-center gap-2">
        {presets.map((p) => (
          <button
            key={p.key}
            type="button"
            onClick={() => go(periodOf(p.key, today))}
            aria-pressed={active === p.key}
            className={`h-9 rounded-lg border px-3 text-sm font-medium ${active === p.key ? "border-accent bg-accent text-white" : "border-line bg-surface text-ink hover:bg-muted"}`}
          >
            {t(p.label)}
          </button>
        ))}
        <span className="ml-1 inline-flex items-center gap-1.5 text-sm text-ink-3">
          <input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} aria-label={t("С")} className={field} />—
          <input type="date" value={to} min={from} max={today} onChange={(e) => setTo(e.target.value)} aria-label={t("По")} className={field} />
          {(from !== data.from || to !== data.to) && from && to && (
            <button type="button" onClick={() => go({ from, to })} className="h-9 rounded-lg bg-accent px-3 text-sm font-semibold text-white hover:bg-accent-strong">
              {t("Показать")}
            </button>
          )}
        </span>
      </div>

      {ask && <AskAi data={data} />}

      {data.kpis && (
        <Section title="Показатели" action={<span className="text-xs text-ink-3">{t("{a} — {b}", { a: dateRu(data.from), b: dateRu(data.to) })}</span>}>
          <KpiRow k={data.kpis} />
        </Section>
      )}

      {d && (
        <Section title="Закупки компании">
          <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-5">
            <Tile label="Закуплено за период" value={`${compactMoney(d.orderedTotal, units)} ${t("сум")}`} hint={t("заказов: {n}", { n: d.orderedCount })} href="/procurement/orders" />
            <Tile label="Поступило на склад" value={`${compactMoney(d.receivedTotal, units)} ${t("сум")}`} hint={t("по ценам заказов")} href="/procurement/receipts" />
            <Tile
              label="Ждут согласования"
              value={String(d.awaitingRequests + d.awaitingOrders)}
              hint={t("заявок {a}, заказов {b} · {sum} сум", { a: d.awaitingRequests, b: d.awaitingOrders, sum: compactMoney(d.awaitingRequestsTotal + d.awaitingOrdersTotal, units) })}
              href="/requests/approvals"
            />
            <Tile label="Активные заказы" value={String(d.activeOrders)} hint={`${compactMoney(d.activeTotal, units)} ${t("сум")}`} href="/procurement/orders?status=open" />
            <Tile label="Просроченные поставки" value={String(d.overdueOrders)} href="/procurement/orders?status=overdue" tone={d.overdueOrders > 0 ? "bad" : undefined} icon={AlertTriangle} />
          </div>
          <div className="grid gap-4 xl:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)]">
            <div className={card}>
              <h3 className="text-base font-semibold text-ink">{t("Динамика закупок по месяцам")}</h3>
              <p className="mb-3 text-xs text-ink-3">{t("Утверждённые заказы, сум, за 12 месяцев")}</p>
              <MonthColumns data={d.byMonth} countLabel="Заказов" />
            </div>
            <div className={card}>
              <h3 className="mb-3 text-base font-semibold text-ink">{t("Поставщики")}</h3>
              {d.best.length + d.worst.length === 0 ? (
                <p className="text-sm text-ink-3">{t("Рейтинг появится с первыми заказами.")}</p>
              ) : (
                <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-1">
                  {[
                    { title: "Лучшие", icon: TrendingUp, list: d.best },
                    { title: "Худшие", icon: TrendingDown, list: d.worst },
                  ]
                    .filter((g) => g.list.length > 0)
                    .map((g) => (
                      <div key={g.title}>
                        <p className="mb-1 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-ink-3">
                          <g.icon className="size-3.5" aria-hidden /> {t(g.title)}
                        </p>
                        <ul className="divide-y divide-line">
                          {g.list.map((s) => (
                            <li key={s.id}>
                              <Link href={`/procurement/suppliers/${s.id}`} className="flex items-center justify-between gap-3 py-2 text-sm hover:text-accent-strong">
                                <span className="min-w-0 truncate text-ink">{s.name}</span>
                                <span className="flex shrink-0 items-center gap-2 text-xs text-ink-3">
                                  {t("заказов: {n}", { n: s.orders })} <RatingBadge rating={s.rating} />
                                </span>
                              </Link>
                            </li>
                          ))}
                        </ul>
                      </div>
                    ))}
                </div>
              )}
            </div>
          </div>
          <div className="grid gap-4 lg:grid-cols-2">
            <div className={card}>
              <h3 className="text-base font-semibold text-ink">{t("Расходы по отделам")}</h3>
              <p className="mb-3 text-xs text-ink-3">{t("По позициям заказов за период, с НДС")}</p>
              <AmountBars data={d.byDepartment} empty="За период закупок не было." />
            </div>
            <div className={card}>
              <h3 className="text-base font-semibold text-ink">{t("Расходы по категориям")}</h3>
              <p className="mb-3 text-xs text-ink-3">{t("По позициям заказов за период, с НДС")}</p>
              <AmountBars data={d.byCategory} empty="За период закупок не было." />
            </div>
          </div>
          {d.overdue.length > 0 && (
            <div className={`${card} border-bad/30`}>
              <h3 className="mb-2 flex items-center gap-2 text-base font-semibold text-bad">
                <AlertTriangle className="size-4" aria-hidden /> {t("Просроченные поставки")}
              </h3>
              <ul className="divide-y divide-line">
                {d.overdue.map((o) => (
                  <li key={o.id}>
                    <Link href={`/procurement/orders/${o.id}`} className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2 text-sm hover:text-accent-strong">
                      <span className="w-28 font-medium tabular-nums text-ink">{o.number}</span>
                      <span className="min-w-0 flex-1 truncate text-ink-2">{o.supplierName}</span>
                      <span className="text-xs text-ink-3">{o.responsibleName}</span>
                      <span className="text-xs font-semibold text-bad">{t("ожидалась {date}", { date: dateRu(o.expectedDate) })}</span>
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </Section>
      )}

      {h && (
        <Section title="Работа снабжения">
          <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
            <Tile label="Заявки ждут заказа" value={String(h.waitingOrder)} href="/procurement/orders/new" />
            <Tile label="Заказов в работе" value={String(h.ordersInWork)} href="/procurement/orders?status=open" />
            <Tile label="Просрочено поставок" value={String(h.overdueOrders)} tone={h.overdueOrders > 0 ? "bad" : undefined} icon={AlertTriangle} href="/procurement/orders?status=overdue" />
            <Tile label="Просрочено задач" value={String(h.overdueTasks)} tone={h.overdueTasks > 0 ? "bad" : undefined} icon={AlertTriangle} />
            <Tile label="До заказа" value={days(h.avgToOrderDays)} hint={t("от утверждения заявки, в среднем")} />
            <Tile label="Поставка" value={days(h.avgDeliveryDays)} hint={t("от заказа до поставки, в среднем")} />
          </div>
          <div className={card}>
            <h3 className="mb-3 text-base font-semibold text-ink">{t("Сотрудники снабжения")}</h3>
            {h.staff.length === 0 ? (
              <p className="text-sm text-ink-3">{t("Сотрудников снабжения нет — назначьте роль «Снабжение».")}</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full min-w-[720px] text-sm">
                  <thead>
                    <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                      <th className="py-2 pr-3 font-semibold">{t("Сотрудник")}</th>
                      <th className="py-2 pr-3 font-semibold">{t("Нагрузка")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("Задачи")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("Заказы")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("Поставлено")}</th>
                      <th className="py-2 pr-3 text-right font-semibold">{t("До заказа")}</th>
                      <th className="py-2 text-right font-semibold">{t("Поставка")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {h.staff.map((s) => {
                      const maxLoad = Math.max(...h.staff.map((x) => x.load), 1);
                      return (
                        <tr key={s.userId} className="border-b border-line last:border-0">
                          <td className="py-2 pr-3 text-ink">{s.name}</td>
                          <td className="w-40 py-2 pr-3">
                            <span className="flex items-center gap-2">
                              <span className="h-2 flex-1 rounded-full bg-muted">
                                <span className="block h-2 rounded-full bg-accent" style={{ width: `${(s.load / maxLoad) * 100}%` }} />
                              </span>
                              <span className="w-6 text-right text-xs tabular-nums text-ink-2">{s.load}</span>
                            </span>
                          </td>
                          <td className="py-2 pr-3 text-right tabular-nums">
                            {s.openTasks}
                            {s.overdueTasks > 0 && <span className="ml-1 text-xs font-semibold text-bad">{t("просрочено {n}", { n: s.overdueTasks })}</span>}
                          </td>
                          <td className="py-2 pr-3 text-right tabular-nums">
                            {s.activeOrders}
                            {s.overdueOrders > 0 && <span className="ml-1 text-xs font-semibold text-bad">{t("просрочено {n}", { n: s.overdueOrders })}</span>}
                          </td>
                          <td className="py-2 pr-3 text-right tabular-nums text-ink-2">{s.delivered}</td>
                          <td className="py-2 pr-3 text-right tabular-nums text-ink-2">{days(s.avgToOrderDays)}</td>
                          <td className="py-2 text-right tabular-nums text-ink-2">{days(s.avgDeliveryDays)}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
            <p className="mt-2 text-[11px] text-ink-3">{t("Нагрузка — открытые задачи и заказы в работе. Сроки — за выбранный период.")}</p>
          </div>
        </Section>
      )}

      {b && (
        <Section title="Мои закупки">
          <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
            <Tile label="Мои заявки в работе" value={String(b.myRequestsOpen)} href="/requests/my" icon={ClipboardList} />
            <Tile label="Мои заказы" value={String(b.myOrders)} hint={`${compactMoney(b.myOrdersTotal, units)} ${t("сум")}`} href="/procurement/orders" />
            <Tile label="Мои задачи" value={String(b.myTasks)} href="/tasks" />
            <Tile label="Задачи просрочены" value={String(b.myTasksOverdue)} tone={b.myTasksOverdue > 0 ? "bad" : undefined} icon={AlertTriangle} href="/tasks" />
          </div>
          <div className="grid gap-4 lg:grid-cols-2">
            <div className={card}>
              <h3 className="mb-2 flex items-center gap-2 text-base font-semibold text-ink">
                <Timer className="size-4 text-ink-3" aria-hidden /> {t("Срочные поставки")}
              </h3>
              {b.urgent.length === 0 ? (
                <p className="text-sm text-ink-3">{t("Просроченных и ближайших поставок нет.")}</p>
              ) : (
                <ul className="divide-y divide-line">
                  {b.urgent.map((o) => (
                    <li key={o.id}>
                      <Link href={`/procurement/orders/${o.id}`} className="flex flex-wrap items-center gap-x-3 gap-y-1 py-2 text-sm hover:text-accent-strong">
                        <span className="font-medium tabular-nums text-ink">{o.number}</span>
                        <span className="min-w-0 flex-1 truncate text-ink-2">{o.supplierName}</span>
                        <OrderStatusPill status={o.status} />
                        <span className={`text-xs ${o.overdue ? "font-semibold text-bad" : "text-ink-3"}`}>{dateRu(o.expectedDate)}</span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <div className={card}>
              <h3 className="mb-2 flex items-center gap-2 text-base font-semibold text-ink">
                <CalendarClock className="size-4 text-ink-3" aria-hidden /> {t("Ближайшие сроки")}
              </h3>
              {b.deadlines.length === 0 ? (
                <p className="text-sm text-ink-3">{t("Сроков нет.")}</p>
              ) : (
                <ul className="divide-y divide-line">
                  {b.deadlines.map((x) => (
                    <li key={`${x.kind}-${x.id}`}>
                      <Link href={x.link} className="flex items-center gap-3 py-2 text-sm hover:text-accent-strong">
                        <span className={`w-20 shrink-0 tabular-nums ${x.overdue ? "font-semibold text-bad" : "text-ink-2"}`}>{dateRu(x.due)}</span>
                        <span className="min-w-0 flex-1 truncate text-ink">{x.title}</span>
                        <span className="text-xs text-ink-3">{x.kind === "task" ? t("задача") : t("поставка")}</span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        </Section>
      )}

      {w && (
        <Section title="Склад">
          <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
            <Tile label="Ждут приёмки" value={String(w.expected)} hint={t("сегодня и завтра: {n}", { n: w.expectedSoon })} href="/procurement/receipts" />
            <Tile label="Черновики приёмок" value={String(w.drafts)} href="/procurement/receipts?status=draft" />
            <Tile label="Приёмок за неделю" value={String(w.receiptsWeek)} href="/procurement/receipts?status=posted" />
            <Tile label="Позиций на складе" value={String(w.stock.items)} href="/procurement/stock" />
            <Tile label="Ниже минимума" value={String(w.stock.low)} tone={w.stock.low > 0 ? "warn" : undefined} icon={AlertTriangle} href="/procurement/stock?filter=low" />
            <Tile label="К выдаче" value={String(w.stock.toIssueRequests)} href="/procurement/issue" icon={PackageCheck} />
          </div>
          <div className="grid gap-4 lg:grid-cols-3">
            <div className={card}>
              <h3 className="mb-2 text-base font-semibold text-ink">{t("Ожидаемые поступления")}</h3>
              {w.expectedList.length === 0 ? (
                <p className="text-sm text-ink-3">{t("Поставок не ждём.")}</p>
              ) : (
                <ul className="divide-y divide-line">
                  {w.expectedList.map((o) => (
                    <li key={o.orderId}>
                      <Link href={`/procurement/orders/${o.orderId}`} className="block py-2 text-sm hover:text-accent-strong">
                        <span className="flex justify-between gap-2">
                          <span className="font-medium tabular-nums text-ink">{o.number}</span>
                          <span className={`text-xs ${o.overdue ? "font-semibold text-bad" : "text-ink-3"}`}>{dateRu(o.expectedDate)}</span>
                        </span>
                        <span className="block truncate text-xs text-ink-3">{o.supplierName}</span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <div className={card}>
              <h3 className="mb-2 text-base font-semibold text-ink">{t("Ниже минимального остатка")}</h3>
              {w.low.length === 0 ? (
                <p className="text-sm text-ink-3">{t("Все остатки не ниже минимума.")}</p>
              ) : (
                <ul className="divide-y divide-line">
                  {w.low.map((i) => (
                    <li key={i.id} className="flex items-baseline justify-between gap-2 py-2 text-sm">
                      <span className="min-w-0 truncate text-ink">{i.name}</span>
                      <span className="shrink-0 text-xs tabular-nums text-warn">
                        {quantity(i.available)} / {quantity(i.minQty)} {i.unitCode}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <div className={card}>
              <h3 className="mb-2 text-base font-semibold text-ink">{t("К выдаче")}</h3>
              {w.toIssue.length === 0 ? (
                <p className="text-sm text-ink-3">{t("Выдавать нечего.")}</p>
              ) : (
                <ul className="divide-y divide-line">
                  {w.toIssue.map((r) => (
                    <li key={r.requestId}>
                      <Link href={`/requests/${r.requestId}`} className="block py-2 text-sm hover:text-accent-strong">
                        <span className="font-medium tabular-nums text-ink">{r.number}</span> <span className="text-ink-2">{r.title}</span>
                        <span className="block text-xs text-ink-3">
                          {r.departmentName} · {t("позиций: {n}", { n: r.lines })}
                        </span>
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </div>
        </Section>
      )}

      {data.digests && (
        <Section
          title="Ежедневные сводки"
          action={
            <Link href="/procurement/digests" className="inline-flex items-center gap-1 text-sm text-accent-strong hover:underline">
              {t("Все сводки")} <ChevronRight className="size-4" aria-hidden />
            </Link>
          }
        >
          {data.digests.length === 0 ? (
            <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-6 text-center text-sm text-ink-3">
              {t("Первая сводка придёт утром после 09:00 и сохранится здесь.")}
            </p>
          ) : (
            <ul className="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
              {data.digests.map((x) => (
                <li key={x.id}>
                  <Link href={`/procurement/digests/${x.id}`} className="block h-full rounded-xl border border-line bg-surface p-4 hover:border-accent/40">
                    <span className="flex items-center gap-2 text-sm font-semibold text-ink">
                      <Newspaper className="size-4 text-ink-3" aria-hidden /> {t("Сводка за {date}", { date: dateRu(x.day) })}
                    </span>
                    <span className="mt-1 block text-xs text-ink-3">{t(x.summary)}</span>
                    <span className="mt-1 block text-[11px] text-ink-3">{stampRu(x.createdAt)}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Section>
      )}

      {(data.kpis || d) && (
        <p className="flex items-center gap-1.5 text-xs text-ink-3">
          <FileBarChart className="size-3.5" aria-hidden />
          {t("Подробные отчёты с выгрузкой в Excel и PDF —")}{" "}
          <Link href="/procurement/reports" className="text-accent-strong hover:underline">
            {t("в «Отчётах»")}
          </Link>
          . {t("Суммы — в сумах.")}
        </p>
      )}
    </div>
  );
}
