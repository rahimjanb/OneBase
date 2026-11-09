"use client";

import Link from "next/link";
import { useState } from "react";
import { AlertTriangle, CalendarClock, ChevronRight, FlaskConical, PackageCheck, Truck } from "lucide-react";
import { ArrivalSheet } from "@/components/procurement/ArrivalSheet";
import { useT } from "@/i18n/client";
import {
  arrivalStatusLabel,
  arrivalStatusTone,
  byDue,
  dueGroupLabel,
  type ArrivalPage,
  type ArrivalRow,
  type ArrivalStatus,
  sourceHref,
  type ExpectedDelivery,
} from "@/lib/arrivals";
import { dateRu, money, quantity, stampRu } from "@/lib/procurement";

const card = "rounded-2xl border border-line bg-surface";
const primary = "inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong";

export function ArrivalStatusPill({ status }: { status: ArrivalStatus }) {
  const t = useT();
  return <span className={`inline-flex shrink-0 whitespace-nowrap rounded-md px-2 py-0.5 text-xs font-semibold leading-5 ${arrivalStatusTone[status]}`}>{t(arrivalStatusLabel[status])}</span>;
}

/** Когда ждать поставку: «через 3 дн.», «сегодня», «просрочено на 2 дн.». */
export function DueText({ delivery }: { delivery: Pick<ExpectedDelivery, "daysLeft" | "deliveryDate"> }) {
  const t = useT();
  const d = delivery.daysLeft;
  if (d === null) return <span className="text-ink-3">{t("срок не указан")}</span>;
  const date = dateRu(delivery.deliveryDate);
  if (d < 0) return <span className="font-semibold text-bad">{t("{date} · просрочено на {n} дн.", { date, n: -d })}</span>;
  if (d === 0) return <span className="font-semibold text-accent-strong">{t("сегодня, {date}", { date })}</span>;
  if (d === 1) return <span className="text-ink">{t("завтра, {date}", { date })}</span>;
  return <span className="text-ink-2">{t("{date} · через {n} дн.", { date, n: d })}</span>;
}

/** Ожидаемая поставка: поставщик, тендер, срок, что осталось привезти (и что уже на приёмке), склад; кладовщику — «Машина приехала». */
export function ExpectedCard({
  delivery,
  onArrive,
  compact = false,
  seesTenders = true,
}: {
  delivery: ExpectedDelivery;
  onArrive?: (d: ExpectedDelivery) => void;
  compact?: boolean;
  /** Карточку тендера открывают снабжение, директор и кладовщик; лаборатории — номер без ссылки. */
  seesTenders?: boolean;
}) {
  const t = useT();
  const items = delivery.items.filter((i) => i.left > 0 || i.onArrival > 0);
  return (
    <li className={`${card} p-3.5 sm:p-4`}>
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm font-semibold text-ink">{delivery.supplierName}</p>
          <p className="mt-0.5 text-xs text-ink-3">
            {seesTenders ? (
              <Link href={sourceHref(delivery.kind, delivery.sourceId)} className="tabular-nums hover:text-accent-strong hover:underline">
                {delivery.number}
              </Link>
            ) : (
              <span className="tabular-nums">{delivery.number}</span>
            )}
            {` · ${delivery.title} · ${delivery.warehouseName}`}
          </p>
        </div>
        <span className="flex items-center gap-1.5 text-xs">
          {delivery.overdue && <AlertTriangle className="size-3.5 text-bad" aria-hidden />}
          <DueText delivery={delivery} />
        </span>
      </div>
      <ul className="mt-2.5 space-y-1 text-sm">
        {items.slice(0, compact ? 3 : items.length).map((i) => (
          <li key={i.lineId} className="flex flex-wrap items-baseline justify-between gap-x-3">
            <span className="min-w-0 text-ink">
              {i.name}
              {i.control !== "None" && <FlaskConical className="ml-1.5 inline size-3.5 text-warn" aria-label={t("проверит лаборатория")} />}
            </span>
            <span className="tabular-nums text-ink-2">
              {i.left > 0 && `${quantity(i.left)} ${t(i.unitCode)}`}
              {i.onArrival > 0 && <span className="text-warn">{`${i.left > 0 ? " · " : ""}${t("на приёмке {n}", { n: `${quantity(i.onArrival)} ${t(i.unitCode)}` })}`}</span>}
            </span>
          </li>
        ))}
        {compact && items.length > 3 && <li className="text-xs text-ink-3">{t("и ещё {n} поз.", { n: items.length - 3 })}</li>}
      </ul>
      <div className="mt-3 flex flex-wrap items-center justify-between gap-2 border-t border-line pt-3">
        <span className="text-xs text-ink-3">
          {t("осталось на {sum} сум", { sum: money(delivery.leftAmount) })}
          {delivery.openArrivals > 0 && (
            <Link href={`/procurement/arrivals?tab=open&source=${delivery.sourceId}`} className="ml-2 font-medium text-warn hover:underline">
              {t("машин на приёмке: {n}", { n: delivery.openArrivals })}
            </Link>
          )}
        </span>
        {delivery.canArrive && onArrive && (
          <button type="button" onClick={() => onArrive(delivery)} className={primary}>
            <Truck className="size-4" aria-hidden /> {t("Машина приехала")}
          </button>
        )}
      </div>
    </li>
  );
}

/** Где поставка сейчас: что ждёт и от кого — лаборатория (сколько позиций ещё проверяет), кладовщик (взвесить и принять), итог. */
export function ArrivalStage({ row }: { row: ArrivalRow }) {
  const t = useT();
  if (row.status === "Quality")
    return (
      <span className="inline-flex items-center gap-1 text-xs text-warn">
        <FlaskConical className="size-3.5" aria-hidden /> {t("лаборатория проверяет: {n} из {total}", { n: row.pending, total: row.lines })}
      </span>
    );
  if (row.status === "Ready")
    return (
      <span className="inline-flex items-center gap-1 text-xs text-accent-strong">
        <PackageCheck className="size-3.5" aria-hidden /> {t("взвесить и принять")}
        {row.rejected > 0 && <span className="text-bad">{` · ${t("вернуть: {n} поз.", { n: row.rejected })}`}</span>}
      </span>
    );
  if (row.status === "Accepted")
    return (
      <span className="text-xs text-ink-2">
        {[row.receiptNumber, `${money(row.acceptedTotal)} ${t("сум")}`, row.acceptedByName].filter(Boolean).join(" · ")}
      </span>
    );
  return <span className="text-xs text-ink-3">{t(arrivalStatusLabel[row.status])}</span>;
}

/** Поставка в списке: машина, поставщик, что привезли, когда приехала, где сейчас (качество, приёмка, итог). */
export function ArrivalItem({ row }: { row: ArrivalRow }) {
  const t = useT();
  return (
    <li>
      <Link href={`/procurement/arrivals/${row.id}`} className={`${card} flex items-center gap-3 p-3.5 hover:bg-muted/40 sm:p-4`}>
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-muted text-ink-2">
          <Truck className="size-5" aria-hidden />
        </span>
        <span className="min-w-0 flex-1">
          <span className="flex flex-wrap items-center gap-2">
            <span className="text-sm font-semibold text-ink">{row.supplierName}</span>
            <ArrivalStatusPill status={row.status} />
          </span>
          <span className="mt-0.5 block truncate text-sm text-ink-2">{row.summary}</span>
          <span className="mt-0.5 block text-xs text-ink-3">
            {[row.number, row.vehicleNo, row.reference ? t("накладная {n}", { n: row.reference }) : null, stampRu(row.arrivedAt)].filter(Boolean).join(" · ")}
          </span>
          <span className="mt-1 block">
            <ArrivalStage row={row} />
          </span>
        </span>
        <ChevronRight className="size-4 shrink-0 text-ink-4" aria-hidden />
      </Link>
    </li>
  );
}

/**
 * «Снабжение → Поставки»: ожидаются (тендеры по сроку — просроченные сверху, кладовщику — «Машина приехала»), на приёмке (машины: проверка
 * качества и приёмка), принято и закрытые поставки. Что можно сделать, решает сервер.
 */
export function ArrivalsView({
  tab,
  expected,
  open,
  done,
  source,
  seesTenders = true,
}: {
  tab: "expected" | "open" | "done";
  expected: ExpectedDelivery[];
  open: ArrivalPage;
  done: ArrivalPage | null;
  /** Поставки одного тендера или покупки. */
  source?: string;
  seesTenders?: boolean;
}) {
  const t = useT();
  const [arriving, setArriving] = useState<ExpectedDelivery | null>(null);
  const tabs = [
    { key: "expected", label: "Ожидаются", count: expected.length },
    { key: "open", label: "На приёмке", count: open.total },
    { key: "done", label: "Принято и закрыто", count: null },
  ] as const;
  const href = (key: string) => `/procurement/arrivals?tab=${key}${source ? `&source=${source}` : ""}`;

  return (
    <div className="mx-auto max-w-5xl space-y-4">
      <nav className="flex gap-1 overflow-x-auto rounded-xl bg-muted p-1" aria-label={t("Поставки")}>
        {tabs.map((x) => (
          <Link
            key={x.key}
            href={href(x.key)}
            aria-current={tab === x.key ? "page" : undefined}
            className={`inline-flex h-9 shrink-0 items-center gap-2 rounded-lg px-3.5 text-sm font-medium ${tab === x.key ? "bg-surface text-ink shadow-sm" : "text-ink-2 hover:text-ink"}`}
          >
            {t(x.label)}
            {x.count !== null && x.count > 0 && <span className="tabular-nums text-ink-3">{x.count}</span>}
          </Link>
        ))}
      </nav>

      {tab === "expected" &&
        (expected.length === 0 ? (
          <Empty icon={CalendarClock} text={t("Поставок не ждём: всё привезено, тендеров и покупок с доставкой нет.")} />
        ) : (
          byDue(expected).map((g) => (
            <section key={g.group} aria-label={t(dueGroupLabel[g.group])}>
              <h2 className={`mb-2 text-sm font-semibold ${g.group === "overdue" ? "text-bad" : "text-ink"}`}>
                {t(dueGroupLabel[g.group])} <span className="font-normal text-ink-3">· {g.rows.length}</span>
              </h2>
              <ul className="space-y-2">
                {g.rows.map((d) => (
                  <ExpectedCard key={d.sourceId} delivery={d} onArrive={setArriving} seesTenders={seesTenders} />
                ))}
              </ul>
            </section>
          ))
        ))}

      {tab === "open" &&
        (open.items.length === 0 ? (
          <Empty icon={Truck} text={t("Машин на приёмке нет. Приехала машина — откройте «Ожидаются» и нажмите «Машина приехала».")} />
        ) : (
          <ul className="space-y-2">
            {open.items.map((r) => (
              <ArrivalItem key={r.id} row={r} />
            ))}
          </ul>
        ))}

      {tab === "done" &&
        done &&
        (done.items.length === 0 ? (
          <Empty icon={PackageCheck} text={t("Принятых поставок пока нет.")} />
        ) : (
          <>
            <ul className="space-y-2">
              {done.items.map((r) => (
                <ArrivalItem key={r.id} row={r} />
              ))}
            </ul>
            {done.total > done.pageSize && (
              <div className="flex items-center justify-between text-sm text-ink-3">
                <span>{t("Показано {a} из {b}", { a: Math.min(done.page * done.pageSize, done.total), b: done.total })}</span>
                <span className="flex gap-2">
                  {done.page > 1 && (
                    <Link href={`${href("done")}&page=${done.page - 1}`} className="rounded-lg border border-line px-3 py-1.5 text-ink hover:bg-muted">
                      {t("Назад")}
                    </Link>
                  )}
                  {done.page * done.pageSize < done.total && (
                    <Link href={`${href("done")}&page=${done.page + 1}`} className="rounded-lg border border-line px-3 py-1.5 text-ink hover:bg-muted">
                      {t("Дальше")}
                    </Link>
                  )}
                </span>
              </div>
            )}
          </>
        ))}

      {arriving && <ArrivalSheet delivery={arriving} onClose={() => setArriving(null)} />}
    </div>
  );
}

function Empty({ icon: Icon, text }: { icon: typeof Truck; text: string }) {
  return (
    <div className={`${card} flex flex-col items-center gap-2 px-6 py-10 text-center`}>
      <Icon className="size-6 text-ink-3" aria-hidden />
      <p className="max-w-md text-sm text-ink-2">{text}</p>
    </div>
  );
}
