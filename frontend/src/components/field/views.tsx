import Link from "next/link";
import { AlertTriangle, Bot, ChevronRight, Footprints, ListTodo, MapPin, Route as RouteIcon } from "lucide-react";
import { getT } from "@/i18n/server";
import { date as fmtDate, kg, money, num, pct } from "@/lib/sales/format";
import { dayStatusLabel, plural, tParts } from "@/lib/field/labels";
import type { FieldDashboard, FieldMe, FieldToday } from "@/lib/field/types";
import { DayBars, PlanFactBar } from "./charts";
import { Chip, Empty, Panel, Stat, buttonClass } from "./ui";
import { RecommendationList } from "./Recommendations";
import { TaskList } from "./TaskBoard";
import { YandexRouteButton } from "./YandexRouteButton";

const shareTone = (share: number | null | undefined, expected = 1) => (share == null ? "muted" : share >= expected ? "ok" : share >= expected * 0.7 ? "accent" : "bad");

/** День агента: «Сегодня», план, визиты, задачи, следующая точка, AI-рекомендации, продажи по дням. */
export async function AgentTodayView({ data, me, own }: { data: FieldToday; me: FieldMe; own: boolean }) {
  const t = await getT();
  const [year, month] = data.date.split("-").map(Number);
  const next = data.route?.points.find((p) => p.id === data.route?.nextPointId) ?? null;
  const routeShare = data.route && data.route.planned > 0 ? data.route.visited / data.route.planned : null;
  const stale = data.dataAsOf && data.dataAsOf < data.date;

  return (
    <div className="space-y-4">
      {stale && (
        <p className="rounded-lg bg-warn-soft px-3 py-2 text-xs text-ink-2">
          {t("Заказы и визиты Linko загружены по {asOf} — данные за {date} появятся после синхронизации.", { asOf: fmtDate(data.dataAsOf), date: fmtDate(data.date) })}
        </p>
      )}

      {/* Главное: продажи, план, выполнение — как на макете */}
      <div className="rounded-2xl bg-sidebar p-5 text-white">
        <div className="grid grid-cols-3 gap-3">
          <div>
            <div className="text-xs text-sidebar-muted">{t("Заказы сегодня")}</div>
            <div className="mt-1 text-2xl font-semibold tabular-nums">{money(data.today.sum, t)}</div>
            <div className="text-xs text-sidebar-muted">{t("{kg} кг · {orders} шт.", { kg: num(data.today.kg), orders: data.today.orders })}</div>
          </div>
          <div>
            <div className="text-xs text-sidebar-muted">{t("План на день")}</div>
            <div className="mt-1 text-2xl font-semibold tabular-nums">{data.dailyPlanSum ? money(data.dailyPlanSum, t) : data.dailyPlanKg ? `${num(data.dailyPlanKg)}` : "—"}</div>
            <div className="text-xs text-sidebar-muted">{t(data.dailyPlanSum ? "сум" : data.dailyPlanKg ? "кг" : "плана нет в Linko")}</div>
          </div>
          <div>
            <div className="text-xs text-sidebar-muted">{t("Выполнение")}</div>
            <div className="mt-1 text-2xl font-semibold tabular-nums">{pct(data.dayShareSum ?? data.dayShareKg)}</div>
            <div className="text-xs text-sidebar-muted">{t("месяц {pct}", { pct: pct(data.month.shareKg) })}</div>
          </div>
        </div>
        {(data.dayShareSum ?? data.dayShareKg) != null && (
          <div className="mt-4 h-2 overflow-hidden rounded-full bg-white/15">
            <div className="h-full rounded-full bg-[#5eead4]" style={{ width: `${Math.min(100, (data.dayShareSum ?? data.dayShareKg ?? 0) * 100)}%` }} />
          </div>
        )}
      </div>

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat label={t("Визиты")} value={data.visitsToday} hint={t("план Linko: {n}", { n: data.linkoVisitsPlanned })} href={own ? "/field/visits" : undefined} />
        <Stat label={t("Задачи")} value={data.tasksOpen} unit={t("открыто")} hint={data.tasksOverdue > 0 ? <span className="text-bad">{t("просрочено: {n}", { n: data.tasksOverdue })}</span> : t("сегодня выполнено: {n}", { n: data.tasksDoneToday })} tone={data.tasksOverdue > 0 ? "bad" : "ink"} href={own ? "/field/tasks" : undefined} />
        <Stat label={t("Осталось точек")} value={data.route?.remaining ?? "—"} hint={data.route ? t("из {n} в маршруте", { n: data.route.planned }) : t("маршрута нет")} share={routeShare} href={data.route ? `/field/routes/${data.route.id}` : own ? "/field/routes" : undefined} />
        <Stat label={t("Месяц, кг")} value={num(data.month.factKg)} hint={data.month.planKg ? t("план {kg} кг", { kg: num(data.month.planKg) }) : t("плана нет")} share={data.month.shareKg} tone={shareTone(data.month.shareKg, data.month.expected)} />
      </div>

      {/* Следующий визит */}
      {next ? (
        <Panel title={t("Следующий визит")} hint={next.plannedTime ? `≈ ${next.plannedTime.slice(0, 5)}` : undefined}>
          <Link href={`/field/customers/${next.marketId}`} className="block text-base font-semibold text-ink hover:underline">
            {next.name}
          </Link>
          {next.address && (
            <p className="mt-1 flex items-start gap-1.5 text-sm text-ink-2">
              <MapPin className="mt-0.5 size-4 shrink-0" /> {next.address}
            </p>
          )}
          <div className="mt-3 flex flex-wrap gap-2">
            <Link href={`/field/routes/${data.route!.id}`} className={`${buttonClass.primary} flex-1 sm:flex-none`}>
              <RouteIcon className="size-4" /> {t("Открыть маршрут")}
            </Link>
            <Link href={`/field/customers/${next.marketId}`} className={buttonClass.outline}>
              {t("Карточка точки")}
            </Link>
            <YandexRouteButton route={data.route!} fromHere={own} />
          </div>
        </Panel>
      ) : (
        own &&
        !data.route && (
          <Empty
            action={
              <Link href="/field/routes" className={buttonClass.primary}>
                <RouteIcon className="size-4" /> {t("Маршрут на сегодня")}
              </Link>
            }
          >
            {t("Маршрута на сегодня ещё нет.")}
          </Empty>
        )
      )}

      {data.route && data.route.points.length > 0 && (
        <Panel title={t("Сегодня")} hint={t("{visited} из {planned} посещено", { visited: data.route.visited, planned: data.route.planned })} action={<Link href={`/field/routes/${data.route.id}`} className="text-sm text-accent-strong hover:underline">{t("Весь маршрут")}</Link>}>
          <ul className="space-y-1.5">
            {data.route.points.slice(0, 6).map((p) => (
              <li key={p.id} className="flex items-center gap-2 text-sm">
                <span className={`size-2.5 shrink-0 rounded-full ${p.status === "Visited" ? "bg-ok" : p.status === "Skipped" ? "bg-bad" : p.status === "InProgress" ? "bg-accent" : "bg-line"}`} />
                <Link href={`/field/customers/${p.marketId}`} prefetch={false} className={`min-w-0 flex-1 truncate hover:underline ${p.status === "Visited" ? "text-ink-3 line-through" : "text-ink"}`}>
                  {p.name}
                </Link>
                {p.plannedTime && p.status === "Planned" && <span className="text-xs text-ink-3">{p.plannedTime.slice(0, 5)}</span>}
              </li>
            ))}
          </ul>
        </Panel>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <Panel title={t("Задачи")} hint={data.tasksOpen ? `${data.tasksOpen} ${t(plural(data.tasksOpen, "открыта", "открыты", "открыто"))}` : undefined} action={<Link href="/field/tasks" className="text-sm text-accent-strong hover:underline">{t("Все")}</Link>}>
          <TaskList tasks={data.tasks.slice(0, 5)} emptyText="Открытых задач нет" />
        </Panel>
        <Panel title={t("AI-рекомендации")} hint={t("по вашим точкам")}>
          {data.recommendations.length > 0 ? (
            <RecommendationList items={data.recommendations} agents={[]} compact />
          ) : (
            <p className="flex items-center gap-2 text-sm text-ink-3">
              <Bot className="size-4" /> {t("Новых рекомендаций нет — задачи от AI появятся в списке задач после подтверждения супервайзером.")}
            </p>
          )}
        </Panel>
      </div>

      <Panel title={t("Продажи по дням")} hint={t("доставленные · {who}", { who: me.role === "Agent" ? t("ваши") : data.agentName })}>
        <DayBars days={data.days} year={year} month={month} metric="kg" dailyPlan={data.dailyPlanKg} />
        <div className="mt-4 space-y-3">
          <PlanFactBar label={t("План / факт месяца, кг")} fact={data.month.factKg} plan={data.month.planKg} unit="кг" expected={data.month.expected} />
          {data.month.planSum ? <PlanFactBar label={t("План / факт месяца, сум")} fact={data.month.factSum} plan={data.month.planSum} unit="сум" expected={data.month.expected} /> : null}
        </div>
      </Panel>
    </div>
  );
}

/** Дашборд супервайзера (команда) и РМ (организация). */
export async function DashboardView({ data, me, query }: { data: FieldDashboard; me: FieldMe; query: string }) {
  const t = await getT();
  // Заголовок «Требует внимания» с сервера — «Агент {имя}»: собираем из данных, а не общим шаблоном «Агент {name}» в словаре
  // (он перехватывал бы любые тексты сервера, начинающиеся с «Агент …»).
  const agentNames = new Map(data.agentRows.map((r) => [r.agentId, r.name]));
  const [year, month] = data.date.split("-").map(Number);
  const stale = data.dataAsOf && data.dataAsOf < data.date;
  return (
    <div className="space-y-4">
      {stale && (
        <p className="rounded-lg bg-warn-soft px-3 py-2 text-xs text-ink-2">
          {t("Заказы и визиты Linko загружены по {asOf} — выберите этот день, чтобы увидеть полный день команды.", { asOf: fmtDate(data.dataAsOf) })}
        </p>
      )}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4 xl:grid-cols-8">
        <Stat label={t("Продажи сегодня")} value={money(data.today.sum, t)} hint={t("{kg} кг · {orders} заказов", { kg: num(data.today.kg), orders: data.today.orders })} />
        <Stat label={t("План на день")} value={data.dailyPlanKg ? num(data.dailyPlanKg) : "—"} unit={data.dailyPlanKg ? t("кг") : undefined} hint={t("из планов Linko")} />
        <Stat label={t("Выполнение дня")} value={pct(data.dayShareKg)} share={data.dayShareKg} tone={shareTone(data.dayShareKg)} />
        <Stat label={t("Месяц")} value={pct(data.month.shareKg)} hint={t("{fact} из {plan} кг", { fact: num(data.month.factKg), plan: data.month.planKg ? num(data.month.planKg) : "—" })} share={data.month.shareKg} tone={shareTone(data.month.shareKg, data.month.expected)} />
        <Stat label={t("Агенты")} value={`${data.activeAgents}/${data.agents}`} hint={me.role === "Rm" ? t("супервайзеров: {n}", { n: data.supervisors }) : t("работали в этот день")} href="/field/agents" />
        <Stat label={t("Визиты")} value={num(data.visits)} hint={data.plannedPoints ? t("маршруты: {visited}/{planned}", { visited: data.visitedPoints, planned: data.plannedPoints }) : t("маршрутов нет")} share={data.coverage} />
        <Stat label={t("Задачи")} value={data.tasksOpen} unit={t("открыто")} hint={data.tasksOverdue ? <span className="text-bad">{t("просрочено {n}", { n: data.tasksOverdue })}</span> : t("сделано сегодня {n}", { n: data.tasksDoneToday })} tone={data.tasksOverdue ? "bad" : "ink"} href="/field/tasks" />
        <Stat label={t("Маршруты")} value={data.activeRoutes} unit={t("активны")} hint={data.pendingRecommendations ? t("AI ждёт решения: {n}", { n: data.pendingRecommendations }) : t("рекомендаций нет")} href={data.pendingRecommendations ? "/field/ai" : "/field/routes"} />
      </div>

      <div className="grid gap-4 xl:grid-cols-3">
        <Panel title={t("Требует внимания")} className="xl:col-span-1" hint={data.attention.length ? `${data.attention.length} ${t(plural(data.attention.length, "агент", "агента", "агентов"))}` : undefined}>
          {data.attention.length === 0 ? (
            <p className="text-sm text-ink-3">{t("Проблем не видно.")}</p>
          ) : (
            <ul className="space-y-2">
              {data.attention.map((a) => (
                <li key={a.agentId}>
                  <Link href={a.link} className={`block rounded-lg border px-3 py-2.5 hover:bg-muted ${a.severity === "bad" ? "border-bad/40" : "border-warn/40"}`}>
                    <div className="flex items-center gap-2 text-sm font-medium text-ink">
                      <AlertTriangle className={`size-4 shrink-0 ${a.severity === "bad" ? "text-bad" : "text-warn"}`} />
                      <span className="min-w-0 flex-1 truncate">{agentNames.has(a.agentId) ? `${t("Агент")} ${agentNames.get(a.agentId)}` : t(a.title)}</span>
                      <ChevronRight className="size-4 text-ink-3" />
                    </div>
                    <ul className="mt-1 space-y-0.5 pl-6 text-xs text-ink-2">
                      {a.details.map((d) => (
                        <li key={d}>{t(d)}</li>
                      ))}
                    </ul>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Panel>

        <Panel title={t("Продажи команды по дням")} className="xl:col-span-2" hint={t("доставленные, сум")}>
          <DayBars days={data.days} year={year} month={month} metric="sum" />
          <div className="mt-4">
            <PlanFactBar label={t("План / факт месяца, кг")} fact={data.month.factKg} plan={data.month.planKg} unit="кг" expected={data.month.expected} />
          </div>
        </Panel>
      </div>

      {me.role === "Rm" && data.teams.length > 1 && (
        <Panel title={t("Команды")}>
          <div className="-mx-4 overflow-x-auto sm:-mx-5">
            <table className="w-full min-w-[640px] text-sm">
              <thead className="text-left text-xs uppercase tracking-wide text-ink-3">
                <tr className="border-b border-line">
                  <th className="px-4 py-2 font-medium sm:px-5">{t("Команда")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Агентов")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Продажи сегодня")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Месяц, кг")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Выполнение")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Визиты")}</th>
                  <th className="px-4 py-2 text-right font-medium sm:px-5">{t("Проблемы")}</th>
                </tr>
              </thead>
              <tbody>
                {data.teams.map((team) => (
                  <tr key={team.teamId} className="border-b border-line last:border-0 hover:bg-muted">
                    <td className="px-4 py-2.5 sm:px-5">
                      <Link href={`/field?${query}${query ? "&" : ""}team=${team.teamId}`} className="font-medium text-ink hover:underline">
                        {team.name}
                      </Link>
                      <div className="text-xs text-ink-3">{team.supervisor ?? t("без супервайзера")}</div>
                    </td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{team.agents}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{money(team.sumToday, t)}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{kg(team.monthKg)}</td>
                    <td className={`px-2 py-2.5 text-right tabular-nums ${team.shareKg != null && team.shareKg < data.month.expected * 0.8 ? "text-bad" : ""}`}>{pct(team.shareKg)}</td>
                    <td className="px-2 py-2.5 text-right tabular-nums">{num(team.visits)}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums sm:px-5">{team.problems ? <span className="text-bad">{team.problems}</span> : "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      )}

      <AgentTable data={data} />
    </div>
  );
}

/** Таблица агентов: продажи, план, %, визиты, задачи, статус; на телефоне — карточки. */
export async function AgentTable({ data }: { data: FieldDashboard }) {
  const t = await getT();
  return (
    <Panel title={t("Агенты")} hint={t("{n} · статусы на {date}", { n: data.agentRows.length, date: fmtDate(data.date) })}>
      {data.agentRows.length === 0 ? (
        <p className="text-sm text-ink-3">{t("Агентов в зоне нет — РМ добавляет их в «Команды и доступы».")}</p>
      ) : (
        <>
          <ul className="-mx-4 divide-y divide-line sm:-mx-5 lg:hidden">
            {data.agentRows.map((r) => {
              const [label, tone] = dayStatusLabel[r.status];
              return (
                <li key={r.agentId}>
                  <Link href={`/field/agents/${r.agentId}`} prefetch={false} className="flex items-center gap-3 px-4 py-3 active:bg-muted sm:px-5">
                    <div className="min-w-0 flex-1">
                      <div className="truncate text-sm font-medium text-ink">{r.name}</div>
                      <div className="mt-0.5 flex flex-wrap items-center gap-1.5 text-xs text-ink-3">
                        <Chip tone={tone}>{t(label)}</Chip>
                        <span>
                          <Footprints className="inline size-3" /> {r.visits}
                        </span>
                        <span>
                          <ListTodo className="inline size-3" /> {r.tasksOpen}
                          {r.tasksOverdue ? <span className="text-bad"> ({r.tasksOverdue})</span> : null}
                        </span>
                      </div>
                    </div>
                    <div className="text-right">
                      <div className="text-sm font-semibold tabular-nums text-ink">{money(r.sumToday, t)}</div>
                      <div className={`text-xs tabular-nums ${r.monthShareKg != null && r.behindPp != null && r.behindPp > 15 ? "text-bad" : "text-ink-3"}`}>{t("мес. {pct}", { pct: pct(r.monthShareKg) })}</div>
                    </div>
                    <ChevronRight className="size-4 text-ink-3" />
                  </Link>
                </li>
              );
            })}
          </ul>
          <div className="-mx-5 hidden overflow-x-auto lg:block">
            <table className="w-full text-sm">
              <thead className="text-left text-xs uppercase tracking-wide text-ink-3">
                <tr className="border-b border-line">
                  <th className="px-5 py-2 font-medium">{t("Агент")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Продажи")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("План, кг")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("День")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Месяц")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Визиты")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Маршрут")}</th>
                  <th className="px-2 py-2 text-right font-medium">{t("Задачи")}</th>
                  <th className="px-5 py-2 font-medium">{t("Статус")}</th>
                </tr>
              </thead>
              <tbody>
                {data.agentRows.map((r) => {
                  const [label, tone] = dayStatusLabel[r.status];
                  const note = r.statusNote ? tParts(t, r.statusNote, " · ") : null;
                  return (
                    <tr key={r.agentId} className="border-b border-line last:border-0 hover:bg-muted">
                      <td className="px-5 py-2.5">
                        <Link href={`/field/agents/${r.agentId}`} prefetch={false} className="font-medium text-ink hover:underline">
                          {r.name}
                        </Link>
                        <div className="text-xs text-ink-3">{r.teamName}</div>
                      </td>
                      <td className="px-2 py-2.5 text-right tabular-nums">{money(r.sumToday, t)}</td>
                      <td className="px-2 py-2.5 text-right tabular-nums text-ink-2">{r.dailyPlanKg ? num(r.dailyPlanKg) : "—"}</td>
                      <td className="px-2 py-2.5 text-right tabular-nums">{pct(r.dayShareKg)}</td>
                      <td className={`px-2 py-2.5 text-right tabular-nums ${r.behindPp != null && r.behindPp > 15 ? "text-bad" : ""}`}>{pct(r.monthShareKg)}</td>
                      <td className="px-2 py-2.5 text-right tabular-nums">{r.visits}</td>
                      <td className="px-2 py-2.5 text-right tabular-nums text-ink-2">{r.routePlanned ? `${r.routeVisited}/${r.routePlanned}` : "—"}</td>
                      <td className="px-2 py-2.5 text-right tabular-nums">
                        {r.tasksOpen}
                        {r.tasksOverdue ? <span className="text-bad"> ({r.tasksOverdue})</span> : null}
                      </td>
                      <td className="px-5 py-2.5">
                        <Chip tone={tone}>{t(label)}</Chip>
                        {note && <div className="mt-0.5 max-w-[260px] truncate text-xs text-ink-3" title={note}>{note}</div>}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </>
      )}
    </Panel>
  );
}
