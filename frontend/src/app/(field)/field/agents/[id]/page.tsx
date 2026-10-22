import { formatDate } from "@/i18n/core";
import Link from "next/link";
import { DateSwitch } from "@/components/field/DateSwitch";
import { NewTaskButton } from "@/components/field/TaskBoard";
import { Chip, FieldPage, Panel, Stat, buttonClass } from "@/components/field/ui";
import { AgentTodayView } from "@/components/field/views";
import { getLocale, getT } from "@/i18n/server";
import { fieldGet, fieldMe, qs, sp, type FieldSearchParams } from "@/lib/field/api";
import { geoLabel, visitResultLabel } from "@/lib/field/labels";
import { money, num, pct } from "@/lib/sales/format";
import type { FieldAgentCard, FieldMe } from "@/lib/field/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Агент") };
}

/** Карточка агента: день, KPI месяца, маршрут, последние визиты; супервайзер ставит задачи и строит маршрут. */
export default async function AgentPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<FieldSearchParams> }) {
  const { id } = await params;
  const query = await searchParams;
  const t = await getT();
  const locale = await getLocale();
  const me = await fieldMe();
  const card = await fieldGet<FieldAgentCard>(`agents/${id}${qs({ date: sp(query, "date") })}`, `/field/agents/${id}`);
  const k = card.kpi;
  const date = card.today.date;

  return (
    <FieldPage
      title={card.agent.fullName}
      subtitle={[card.teamName, card.supervisorName ? t("супервайзер {name}", { name: card.supervisorName }) : null, card.agent.phone].filter(Boolean).join(" · ")}
      back={{ href: "/field/agents", label: t("Агенты") }}
      actions={
        <>
          <DateSwitch value={date} today={me.today} />
          {card.canPlan && <NewTaskButton agents={[{ id: card.agent.id, name: card.agent.fullName, teamName: card.teamName }]} defaultAgentId={card.agent.id} />}
          <Link href={card.today.route ? `/field/routes/${card.today.route.id}` : `/field/routes?agent=${card.agent.id}&date=${date}`} className={buttonClass.outline}>
            {t("Маршрут")}
          </Link>
        </>
      }
    >
      <AgentTodayView data={card.today} me={me} own={false} />

      <Panel title={t("KPI месяца")} hint={t("на {month}", { month: formatDate(new Date(`${date}T00:00:00Z`), locale, { month: "long", timeZone: "UTC" }) })}>
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <Stat label={t("Продажи")} value={money(k.sum, t)} unit={t("сум")} hint={t("{kg} кг · план {plan} кг", { kg: num(k.kg), plan: k.planKg ? num(k.planKg) : "—" })} share={k.shareKg} />
          <Stat label={t("Заказы / АКБ")} value={`${k.orders} / ${k.activeMarkets}`} hint={k.planAkb ? t("план АКБ {n}", { n: num(k.planAkb) }) : t("точек за агентом: {n}", { n: k.assignedMarkets })} />
          <Stat label={t("Визиты Linko")} value={num(k.visitsDone)} hint={k.visitsPlanned ? t("из плана выполнено {pct}", { pct: pct(k.visitPlanShare) }) : t("плана визитов нет")} share={k.visitPlanShare} />
          <Stat label={t("Покрытие точек")} value={pct(k.marketCoverage)} hint={t("задачи {done}/{total} · маршруты {pct}", { done: k.tasksDone, total: k.tasksTotal, pct: pct(k.routeCoverage) })} share={k.marketCoverage} />
        </div>
      </Panel>

      <Panel title={t("Визиты Sales Base")} hint={t("за 2 недели")}>
        {card.visits.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Визитов в Sales Base не было.")}</p>
        ) : (
          <ul className="divide-y divide-line">
            {card.visits.map((v) => (
              <li key={v.id} className="flex flex-wrap items-center gap-2 py-2.5 text-sm">
                <span className="w-24 shrink-0 text-xs text-ink-3">{new Date(v.startedAt).toLocaleString("ru-RU", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}</span>
                <Link href={`/field/customers/${v.marketId}`} className="min-w-0 flex-1 truncate text-ink hover:underline">
                  {v.marketName}
                </Link>
                {v.result && <Chip tone={visitResultLabel[v.result][1]}>{t(visitResultLabel[v.result][0])}</Chip>}
                <Chip tone={geoLabel[v.geoStatus][1]}>
                  {t(geoLabel[v.geoStatus][0])}
                  {v.distanceM != null ? ` · ${t("{n} м", { n: Math.round(v.distanceM) })}` : ""}
                </Chip>
                {v.minutes != null && <span className="text-xs text-ink-3">{t("{n} мин", { n: v.minutes })}</span>}
              </li>
            ))}
          </ul>
        )}
      </Panel>
    </FieldPage>
  );
}
