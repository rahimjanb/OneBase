import Link from "next/link";
import { notFound } from "next/navigation";
import { ChevronLeft, ChevronRight, MapPin } from "lucide-react";
import { KpiTile, Section } from "@/components/sales/bits";
import { SalesFrame } from "@/components/sales/SalesFrame";
import { getT } from "@/i18n/server";
import { apiGetOrNull } from "@/lib/server-api";
import { visibleBack, visibleCrumbs } from "@/lib/sales/access";
import { STOP_STATUS, canGoForward, dayTitle, hm, mapUrl, minutes, shiftDay, type AgentDayStop, type AgentDayView } from "@/lib/sales/agent-day";
import { date as dateText, dateTime, kg, money, num, pct } from "@/lib/sales/format";
import { mySalesScope } from "@/lib/sales/scope-server";
import type { SalesSearchParams } from "@/lib/sales/query";
import type { AgentView } from "@/lib/sales/types";

/** Сегодня по Ташкенту (UTC+5), yyyy-mm-dd. */
const tashkentToday = () => new Date(Date.now() + 5 * 3600_000).toISOString().slice(0, 10);

/**
 * «Продажи → ТП → День»: куда ходил ТП (точки по порядку отметок визитов Linko, по маршруту и вне, пропущенные и предстоящие точки маршрута),
 * что продал (заказы со строками), что не продал (визит без заказа: последний заказ точки и что она обычно берёт; в заказе — чего из обычного
 * не взяли), минуты между отметками. День — ?date=yyyy-mm-dd, по умолчанию сегодня; охват проверяет сервер (чужой ТП — 404).
 */
export default async function AgentDayPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<SalesSearchParams & { date?: string }> }) {
  const t = await getT();
  const scope = await mySalesScope();
  const [{ id }, sp] = await Promise.all([params, searchParams]);
  const today = tashkentToday();
  const day = typeof sp.date === "string" && /^\d{4}-\d{2}-\d{2}$/.test(sp.date) ? sp.date : today;
  const path = `/sales/agents/${id}/day`;
  const data = await apiGetOrNull<AgentDayView>(`/api/sales/agents/${id}/day?date=${day}`, `${path}?date=${day}`);
  if (!data) notFound();

  // Крошки — по месяцу дня: регион и направление ТП из его страницы (нет данных месяца — только ТП).
  const [year, month] = day.split("-").map(Number);
  const monthQuery = `year=${year}&month=${month}`;
  const agent = await apiGetOrNull<AgentView>(`/api/sales/agents/${id}?${monthQuery}`, path);
  const agentHref = `/sales/agents/${id}?${monthQuery}`;
  const crumbs = [{ label: t("Республика"), href: `/sales/republic?${monthQuery}` }];
  if (agent?.directionId) crumbs.push({ label: agent.directionName ?? t("Направление"), href: `/sales/directions/${agent.directionId}?${monthQuery}` });
  if (agent?.regionId) crumbs.push({ label: agent.regionName ?? t("Регион"), href: `/sales/regions/${agent.regionId}?${monthQuery}` });
  crumbs.push({ label: data.agent, href: agentHref });

  const s = data.summary;
  const window = s.firstMark && s.lastMark ? `${hm(s.firstMark)}–${hm(s.lastMark)}` : "—";
  const subtitle = [t("День ТП"), dayTitle(data.day, t), agent?.regionName, `ID ${data.agentId}`].filter(Boolean).join(" · ");
  const nav = (
    <div className="flex items-center gap-1.5">
      <Link href={`${path}?date=${shiftDay(day, -1)}`} aria-label={t("Предыдущий день")} className="grid size-9 place-items-center rounded-lg border border-line bg-surface text-ink-2 hover:border-accent/40">
        <ChevronLeft aria-hidden className="size-4" />
      </Link>
      <form action={path} method="get" className="flex items-center gap-1.5">
        <input type="date" name="date" defaultValue={day} max={today} aria-label={t("День")} className="h-9 rounded-lg border border-line bg-surface px-2 text-sm text-ink" />
        <button type="submit" className="h-9 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-ink hover:border-accent/40">
          {t("Показать")}
        </button>
      </form>
      {canGoForward(day, today) ? (
        <Link href={`${path}?date=${shiftDay(day, 1)}`} aria-label={t("Следующий день")} className="grid size-9 place-items-center rounded-lg border border-line bg-surface text-ink-2 hover:border-accent/40">
          <ChevronRight aria-hidden className="size-4" />
        </Link>
      ) : (
        <span aria-hidden className="grid size-9 place-items-center rounded-lg border border-line text-ink-4">
          <ChevronRight className="size-4" />
        </span>
      )}
    </div>
  );

  return (
    <SalesFrame
      title={data.agent}
      subtitle={subtitle}
      crumbs={visibleCrumbs([...crumbs, { label: t("День") }], scope, t("Мои регионы"))}
      back={visibleBack(agentHref, scope)}
      sp={sp}
      aside={nav}
    >
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiTile label={t("Визиты")} value={num(s.done)} tone={s.done > 0 ? "ink" : "bad"}>
          {t("по маршруту {done} из {planned} · вне маршрута {off}", { done: num(s.doneInPlan), planned: num(s.planned), off: num(s.offPlan) })}
        </KpiTile>
        <KpiTile label={t("Не посетил")} value={num(s.missed)} tone={s.missed > 0 ? "bad" : "ok"}>
          {s.ahead > 0 ? t("ещё впереди: {n}", { n: num(s.ahead) }) : t("точек маршрута без визита")}
        </KpiTile>
        <KpiTile label={t("Страйк")} value={pct(s.strike, 0)} title={t("Визиты с заказом ÷ выполненные визиты (заказ этого ТП в этой точке в этот день)")}>
          {t("визитов с заказом: {n}", { n: num(s.withOrder) })}
        </KpiTile>
        <KpiTile label={t("Продал")} value={money(s.sum, t)} unit={t("сум")}>
          {t("{kg} кг · заказов {n}", { kg: kg(s.kg), n: num(s.orders) })}
          {s.cancelled > 0 && <> · {t("отменено {n}", { n: num(s.cancelled) })}</>}
        </KpiTile>
        <KpiTile label={t("Отметки визитов")} value={window}>
          {s.spanMinutes != null ? t("от первой до последней — {span}", { span: minutes(s.spanMinutes, t) }) : t("отметок меньше двух")}
        </KpiTile>
        <KpiTile label={t("Между отметками")} value={minutes(s.medianGapMinutes, t)} title={t("Медиана минут между соседними отметками: время на точке вместе с дорогой")}>
          {s.longestGapMinutes != null ? t("дольше всего {n} — перед «{to}»", { n: minutes(s.longestGapMinutes, t), to: s.longestGapTo ?? "" }) : t("медиана")}
        </KpiTile>
        <KpiTile label={t("Заказы без визита")} value={num(s.ordersWithoutVisit)} tone={s.ordersWithoutVisit > 0 ? "warn" : "ink"}>
          {t("по телефону или без отметки визита")}
        </KpiTile>
        <KpiTile label={t("Данные Linko")} value={data.visitsSyncedAt ? dateTime(data.visitsSyncedAt) : "—"}>
          {t("визиты загружены")}
        </KpiTile>
      </div>

      <Section title={t("Маршрут дня")} hint={t("Точки по порядку отметок визитов; затем пропущенные и предстоящие точки маршрута и заказы без визита")}>
        {data.stops.length === 0 ? (
          <p className="rounded-lg bg-muted px-4 py-3 text-sm text-ink-2">{t("В этот день у ТП нет ни визитов, ни заказов в Linko.")}</p>
        ) : (
          <ol className="divide-y divide-line">
            {data.stops.map((stop) => (
              <StopRow key={`${stop.status}-${stop.marketId}`} stop={stop} t={t} />
            ))}
          </ol>
        )}
      </Section>

      <div className="mt-4 rounded-lg bg-muted px-4 py-3 text-sm text-ink-2">
        <ul className="list-disc space-y-1 pl-5">
          {data.notes.map((n) => (
            <li key={n}>{t(n)}</li>
          ))}
        </ul>
      </div>
    </SalesFrame>
  );
}

function StopRow({ stop, t }: { stop: AgentDayStop; t: (s: string, v?: Record<string, string | number>) => string }) {
  const status = STOP_STATUS[stop.status];
  const map = mapUrl(stop);
  const lines = stop.orders.flatMap((o) => o.lines);
  return (
    <li className="flex gap-3 py-3">
      <span className="mt-0.5 grid size-7 shrink-0 place-items-center rounded-full bg-muted text-xs font-bold text-ink-2">{stop.no}</span>
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-1">
          <div className="min-w-0">
            <div className="flex flex-wrap items-center gap-2">
              {hm(stop.at) && <span className="text-sm font-semibold tabular-nums text-ink">{hm(stop.at)}</span>}
              <span className="font-semibold text-ink">{stop.market}</span>
              <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${status.className}`}>{t(status.label)}</span>
              {stop.status !== "no_visit" && (
                <span className="rounded-full bg-muted px-2 py-0.5 text-[11px] font-medium text-ink-3">{stop.inPlan ? t("по маршруту") : t("вне маршрута")}</span>
              )}
            </div>
            <div className="mt-0.5 flex flex-wrap items-center gap-x-2 text-xs text-ink-3">
              {stop.marketType && <span>{stop.marketType}</span>}
              {stop.address && <span>{stop.address}</span>}
              {map && (
                <a href={map} target="_blank" rel="noreferrer" className="inline-flex items-center gap-0.5 font-medium text-accent-strong hover:underline">
                  <MapPin aria-hidden className="size-3" />
                  {t("на карте")}
                </a>
              )}
            </div>
          </div>
          <div className="text-right">
            {stop.orders.length > 0 ? (
              <>
                <div className="font-semibold tabular-nums text-ink">
                  {money(stop.sum, t)} {t("сум")}
                </div>
                <div className="text-xs text-ink-3">
                  {kg(stop.kg)} {t("кг")}
                  {stop.orders.length > 1 ? ` · ${t("заказов {n}", { n: stop.orders.length })}` : ""}
                </div>
              </>
            ) : stop.status === "done" ? (
              <span className="text-sm font-medium text-bad">{t("без заказа")}</span>
            ) : null}
          </div>
        </div>

        <div className="mt-1.5 flex flex-wrap gap-x-3 gap-y-1 text-xs text-ink-2">
          {stop.gapMinutes != null && <span>{t("с прошлой отметки: {n}", { n: minutes(stop.gapMinutes, t) })}</span>}
          {stop.visitNumber != null && <span>{t("{n}-й визит после прошлого заказа точки", { n: stop.visitNumber })}</span>}
          {stop.orders.length === 0 && stop.status !== "planned" && (
            <span>
              {stop.lastOrderDay ? t("последний заказ точки: {date}", { date: dateText(stop.lastOrderDay) }) : t("заказов за 120 дней не было")}
            </span>
          )}
        </div>

        {stop.orders.length === 0 && stop.usual.length > 0 && (
          <p className="mt-1 text-xs text-ink-3">{t("Обычно берёт: {list}", { list: stop.usual.join(", ") })}</p>
        )}
        {stop.usualNotOrdered.length > 0 && <p className="mt-1 text-xs text-warn">{t("Из обычного не взяли: {list}", { list: stop.usualNotOrdered.join(", ") })}</p>}

        {lines.length > 0 && (
          <details className="mt-1.5">
            <summary className="cursor-pointer text-xs font-semibold text-accent-strong">{t("Что продал: {n}", { n: lines.length })}</summary>
            <div className="mt-1.5 overflow-x-auto">
              <table className="w-full min-w-[420px] text-xs">
                <thead className="text-ink-3">
                  <tr>
                    <th className="py-1 text-left font-medium">{t("Товар")}</th>
                    <th className="py-1 text-right font-medium">{t("Кол-во")}</th>
                    <th className="py-1 text-right font-medium">{t("Кг")}</th>
                    <th className="py-1 text-right font-medium">{t("Сумма")}</th>
                  </tr>
                </thead>
                <tbody className="text-ink">
                  {lines.map((l, i) => (
                    <tr key={i} className="border-t border-line">
                      <td className="py-1 pr-2">{l.product}</td>
                      <td className="py-1 text-right tabular-nums">{num(l.qty)}</td>
                      <td className="py-1 text-right tabular-nums">{kg(l.kg)}</td>
                      <td className="py-1 text-right tabular-nums">{money(l.sum, t)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </details>
        )}
      </div>
    </li>
  );
}
