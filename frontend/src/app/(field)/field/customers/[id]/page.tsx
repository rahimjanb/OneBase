import { MapPin, Phone, User } from "lucide-react";
import { ChangeAgentButton, EditCustomerButton } from "@/components/field/CustomerActions";
import { RecommendationList } from "@/components/field/Recommendations";
import { NewTaskButton, TaskList } from "@/components/field/TaskBoard";
import { Chip, FieldPage, Panel, Stat, buttonClass } from "@/components/field/ui";
import { VisitPanel } from "@/components/field/VisitPanel";
import { getT } from "@/i18n/server";
import { fieldGet, fieldMe } from "@/lib/field/api";
import { yandexNavigateTo } from "@/lib/field/yandex";
import { customerStatusLabel, priorityLabel, visitResultLabel } from "@/lib/field/labels";
import { date, kg, money, monthShort, pct } from "@/lib/sales/format";
import type { FieldCustomerCard } from "@/lib/field/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Точка") };
}

const statusRu: Record<string, string> = { delivered: "доставлен", given: "отдан", requested: "заявка", cancelled: "отменён", not_delivered: "не доставлен" };

/** Карточка точки: кто ведёт, продажи 7/30/90 и по месяцам, план, визиты (Sales Base + Linko), заказы, задачи, AI; начать визит. */
export default async function CustomerPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const t = await getT();
  const me = await fieldMe();
  const card = await fieldGet<FieldCustomerCard>(`customers/${id}`, `/field/customers/${id}`);
  const c = card.customer;
  const isMine = me.memberId !== null && c.agentId === me.memberId;
  const maxMonth = Math.max(...card.months.map((m) => m.sum), 1);
  const yandex = yandexNavigateTo(c);

  return (
    <FieldPage
      title={c.name}
      subtitle={
        <span className="flex flex-wrap items-center gap-1.5">
          {c.branch && <span>{c.branch}</span>}
          {c.type && <span>· {c.type}</span>}
          <Chip tone={customerStatusLabel[c.status][1]}>{t(customerStatusLabel[c.status][0])}</Chip>
          {c.priority !== "Medium" && <Chip tone={priorityLabel[c.priority][1]}>{t("{level} приоритет", { level: t(priorityLabel[c.priority][0]) })}</Chip>}
        </span>
      }
      back={{ href: "/field/customers", label: t("Точки") }}
      actions={
        <>
          {card.canEdit && <EditCustomerButton marketId={c.marketId} canPlan={card.canChangeAgent} initial={{ priority: c.priority, status: c.status, monthlyTarget: c.monthlyTarget, contactPerson: card.contactPerson, phone: card.phone, note: card.note }} />}
          {card.canChangeAgent && <ChangeAgentButton marketId={c.marketId} marketName={c.name} currentAgentId={c.agentId} options={card.agentOptions} />}
          {(card.canChangeAgent || isMine) && <NewTaskButton agents={card.agentOptions} marketId={c.marketId} marketName={c.name} defaultAgentId={c.agentId} />}
        </>
      }
    >
      {(isMine || me.activeVisit?.marketId === c.marketId) && (
        <VisitPanel marketId={c.marketId} marketName={c.name} active={me.activeVisit} canStart={isMine} geoRadiusM={me.geoRadiusM} />
      )}

      <div className="grid gap-4 lg:grid-cols-3">
        <Panel title={t("Точка")} className="lg:col-span-1">
          <dl className="space-y-2.5 text-sm">
            {c.address && (
              <div className="flex gap-2">
                <MapPin className="mt-0.5 size-4 shrink-0 text-ink-3" />
                <span className="text-ink">{c.address}</span>
              </div>
            )}
            <div className="flex gap-2">
              <User className="mt-0.5 size-4 shrink-0 text-ink-3" />
              <span>
                <span className="text-ink">{c.agentName ?? <span className="text-warn">{t("Агент не назначен")}</span>}</span>
                {card.supervisorName && <span className="block text-xs text-ink-3">{t("супервайзер {name}", { name: card.supervisorName })}</span>}
                {card.linkoResponsibleName && card.linkoResponsibleName !== c.agentName && <span className="block text-xs text-ink-3">{t("в Linko: {name}", { name: card.linkoResponsibleName })}</span>}
              </span>
            </div>
            {(card.contactPerson || card.phone) && (
              <div className="flex gap-2">
                <Phone className="mt-0.5 size-4 shrink-0 text-ink-3" />
                <span>
                  {card.contactPerson && <span className="block text-ink">{card.contactPerson}</span>}
                  {card.phone && (
                    <a href={`tel:${card.phone}`} className="text-accent-strong underline">
                      {card.phone}
                    </a>
                  )}
                </span>
              </div>
            )}
            {card.note && <p className="rounded-lg bg-muted px-3 py-2 text-ink-2">{card.note}</p>}
          </dl>
          {yandex && (
            <a href={yandex} target="_blank" rel="noreferrer" className={`${buttonClass.outline} mt-3 w-full`}>
              <MapPin className="size-4" /> {t("Проложить маршрут")}
            </a>
          )}
        </Panel>

        <div className="grid grid-cols-2 gap-3 lg:col-span-2 lg:grid-cols-3">
          <Stat label={t("Продажи 7 дней")} value={money(c.sales7, t)} unit={t("сум")} />
          <Stat label={t("Продажи 30 дней")} value={money(c.sales30, t)} unit={t("сум")} hint={c.trendPct != null ? <span className={c.trendPct < 0 ? "text-bad" : "text-ok"}>{t("{pct}% к прошлым 30", { pct: `${c.trendPct > 0 ? "+" : ""}${Math.round(c.trendPct)}` })}</span> : t("{n} заказов", { n: card.orders30 })} />
          <Stat label={t("Продажи 90 дней")} value={money(c.sales90, t)} unit={t("сум")} />
          <Stat label={t("План месяца")} value={c.monthlyTarget ? money(c.monthlyTarget, t) : "—"} hint={c.monthlyTarget ? t("выполнено {pct} ({sum})", { pct: pct(card.monthShare), sum: money(card.monthToDate, t) }) : t("факт месяца {sum}", { sum: money(card.monthToDate, t) })} share={card.monthShare} />
          <Stat label={t("Последний визит")} value={c.lastVisitDate ? date(c.lastVisitDate) : "—"} hint={t("визитов за 30 дней: {n}", { n: card.visits30 })} />
          <Stat label={t("Последняя продажа")} value={c.lastOrderDate ? date(c.lastOrderDate) : "—"} />
        </div>
      </div>

      <Panel title={t("Продажи по месяцам")} hint={t("доставленные, сум")}>
        <div className="flex h-36 items-end gap-2">
          {card.months.map((m) => (
            <div key={`${m.year}-${m.month}`} className="flex h-full min-w-0 flex-1 flex-col items-center justify-end gap-1">
              <span className="text-[11px] tabular-nums text-ink-3">{m.sum ? money(m.sum, t) : ""}</span>
              <div className="w-full max-w-[56px] rounded-t-md bg-accent" style={{ height: `${Math.max(m.sum > 0 ? 3 : 0, (m.sum / maxMonth) * 100)}%` }} title={t("{kg} кг · {orders} заказов", { kg: kg(m.kg), orders: m.orders })} />
              <span className="text-xs text-ink-2">{t(monthShort(m.month, t))}</span>
            </div>
          ))}
        </div>
      </Panel>

      <div className="grid gap-4 lg:grid-cols-2">
        <Panel title={t("Активные задачи")}>
          <TaskList tasks={card.tasks.filter((task) => ["New", "Accepted", "InProgress", "Postponed", "Completed"].includes(task.status))} emptyText="Задач по точке нет" />
        </Panel>
        <Panel title={t("AI-рекомендации")}>
          {card.recommendations.length > 0 ? <RecommendationList items={card.recommendations} agents={card.agentOptions} compact /> : <p className="text-sm text-ink-3">{t("Рекомендаций по точке нет.")}</p>}
        </Panel>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Panel title={t("История визитов")} hint={t("Sales Base и Linko")}>
          {card.visits.length === 0 ? (
            <p className="text-sm text-ink-3">{t("Визитов не было.")}</p>
          ) : (
            <ul className="divide-y divide-line">
              {card.visits.map((v, i) => {
                const result = v.source === "Sales Base" && v.result ? visitResultLabel[v.result as keyof typeof visitResultLabel] : null;
                return (
                  <li key={`${v.at}-${i}`} className="flex flex-wrap items-center gap-2 py-2 text-sm">
                    <span className="w-28 shrink-0 text-xs text-ink-3">{new Date(v.at).toLocaleString("ru-RU", { day: "2-digit", month: "2-digit", year: "2-digit", hour: "2-digit", minute: "2-digit" })}</span>
                    <span className="min-w-0 flex-1 truncate text-ink">{v.agent ?? "—"}</span>
                    <Chip tone={v.source === "Linko" ? "muted" : "accent"}>{v.source}</Chip>
                    {result ? (
                      <Chip tone={result[1]}>{t(result[0])}</Chip>
                    ) : (
                      <span className="text-xs text-ink-2">
                        {v.source === "Sales Base" ? (v.status === "Cancelled" ? t("отменён") : v.status === "InProgress" ? t("идёт") : v.status) : t(v.status)}
                        {v.source === "Linko" && v.result ? ` · ${t(v.result)}` : ""}
                      </span>
                    )}
                    {v.comment && <span className="w-full text-xs text-ink-3 sm:pl-28">«{v.comment}»</span>}
                  </li>
                );
              })}
            </ul>
          )}
        </Panel>
        <Panel title={t("История продаж")} hint={t("заказы Linko")}>
          {card.orders.length === 0 ? (
            <p className="text-sm text-ink-3">{t("Заказов за полгода нет.")}</p>
          ) : (
            <ul className="divide-y divide-line">
              {card.orders.map((o) => (
                <li key={o.id} className="flex items-center gap-2 py-2 text-sm">
                  <span className="w-20 shrink-0 text-xs text-ink-3">{date(o.date)}</span>
                  <span className="min-w-0 flex-1 truncate text-ink-2">{o.agent ?? "—"}</span>
                  <span className={`text-xs ${o.status === "delivered" ? "text-ok" : o.status === "cancelled" || o.status === "not_delivered" ? "text-bad" : "text-ink-3"}`}>{statusRu[o.status] ? t(statusRu[o.status]) : o.status}</span>
                  <span className="w-24 text-right font-medium tabular-nums text-ink">{money(o.sum, t)}</span>
                </li>
              ))}
            </ul>
          )}
        </Panel>
      </div>
    </FieldPage>
  );
}
