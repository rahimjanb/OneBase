import Link from "next/link";
import { AssignBoard } from "@/components/field/AssignBoard";
import { DateSwitch, ParamSelect } from "@/components/field/DateSwitch";
import { RouteBoard } from "@/components/field/RouteBoard";
import { Chip, Empty, FieldPage, Tabs, buttonClass } from "@/components/field/ui";
import { fieldGet, fieldMe, qs, sp, type FieldSearchParams } from "@/lib/field/api";
import { routeSourceLabel } from "@/lib/field/labels";
import type { FieldRoute, FieldRouteSummary } from "@/lib/field/types";

export const metadata = { title: "Маршруты" };

type AgentOption = { id: string; name: string; teamName: string | null; role: string };

/**
 * Маршруты. Агент — свой маршрут на день. Супервайзер/РМ — маршруты команды на день, построение для агента,
 * «Распределение» — перетаскивание точек между агентами.
 */
export default async function RoutesPage({ searchParams }: { searchParams: Promise<FieldSearchParams> }) {
  const params = await searchParams;
  const me = await fieldMe();
  const date = sp(params, "date") ?? me.today;
  const back = `/field/routes${qs({ date: sp(params, "date") })}`;

  if (me.role === "Agent") {
    const route = await fieldGet<FieldRoute | null>(`routes/for${qs({ date })}`, back);
    return (
      <FieldPage title="Маршрут" actions={<DateSwitch value={date} today={me.today} />}>
        <RouteBoard key={`${me.memberId}-${date}`} route={route} agentId={me.memberId!} agentName={me.name} date={date} isOwn canPlan={false} active={me.activeVisit} geoRadiusM={me.geoRadiusM} />
      </FieldPage>
    );
  }

  const tab = sp(params, "tab") === "assign" ? "assign" : "list";
  const agentParam = sp(params, "agent");
  const [routes, agents] = await Promise.all([
    fieldGet<FieldRouteSummary[]>(`routes${qs({ date })}`, back),
    fieldGet<AgentOption[]>("agents", back),
  ]);
  const agentOptions = agents.filter((a) => a.role === "Agent");
  const tabs = [
    { key: "list", label: "Маршруты", href: `/field/routes${qs({ date: sp(params, "date") })}` },
    { key: "assign", label: "Распределение", href: `/field/routes${qs({ date: sp(params, "date"), tab: "assign" })}` },
  ];

  // Маршрут конкретного агента (из карточки агента или выбора) — сразу с действиями.
  if (agentParam) {
    const route = await fieldGet<FieldRoute | null>(`routes/for${qs({ agentId: agentParam, date })}`, back);
    const agent = agentOptions.find((a) => a.id === agentParam);
    return (
      <FieldPage title={`Маршрут: ${agent?.name ?? "агент"}`} back={{ href: back, label: "Маршруты" }} actions={<DateSwitch value={date} today={me.today} />}>
        <RouteBoard key={`${agentParam}-${date}`} route={route} agentId={agentParam} agentName={agent?.name ?? ""} date={date} isOwn={false} canPlan={me.canPlan} active={null} geoRadiusM={me.geoRadiusM} />
      </FieldPage>
    );
  }

  if (tab === "assign") {
    const team = sp(params, "team");
    const columnsAgents = agentOptions.filter((a) => !team || a.teamName === team).slice(0, 30);
    const columns = await Promise.all(
      columnsAgents.map(async (a) => ({ agentId: a.id, agentName: a.name, route: await fieldGet<FieldRoute | null>(`routes/for${qs({ agentId: a.id, date })}`, back) })),
    );
    const teams = [...new Set(agentOptions.map((a) => a.teamName).filter(Boolean))] as string[];
    return (
      <FieldPage title="Распределение точек" subtitle="Перетащите точку к другому агенту — она закрепится за ним, маршруты пересчитаются" actions={<DateSwitch value={date} today={me.today} />}>
        <Tabs items={tabs} current={tab} />
        {teams.length > 1 && <ParamSelect param="team" value={team ?? ""} label="Команда" options={[{ value: "", label: teams.length > 1 ? "Выберите команду" : "Все" }, ...teams.map((t) => ({ value: t, label: t }))]} />}
        {teams.length > 1 && !team ? <Empty>Выберите команду — доска показывает агентов одной команды.</Empty> : <AssignBoard columns={columns} />}
      </FieldPage>
    );
  }

  const withRoute = new Set(routes.map((r) => r.agentId));
  return (
    <FieldPage title="Маршруты" subtitle={`${routes.length} из ${agentOptions.length} агентов с маршрутом`} actions={<DateSwitch value={date} today={me.today} />}>
      <Tabs items={tabs} current={tab} />
      {routes.length > 0 && (
        <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
          {routes.map((r) => (
            <li key={r.id}>
              <Link href={`/field/routes/${r.id}`} prefetch={false} className="flex items-center gap-3 px-4 py-3 hover:bg-muted">
                <div className="min-w-0 flex-1">
                  <div className="truncate text-sm font-medium text-ink">{r.agentName}</div>
                  <div className="truncate text-xs text-ink-3">
                    {r.teamName} · {routeSourceLabel[r.source]} · {r.distanceKm} км
                  </div>
                </div>
                {r.skipped > 0 && <Chip tone="bad">пропущено {r.skipped}</Chip>}
                <div className="w-20 text-right">
                  <div className="text-sm font-semibold tabular-nums text-ink">
                    {r.visited}/{r.planned}
                  </div>
                  <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-muted">
                    <div className="h-full rounded-full bg-ok" style={{ width: `${r.planned ? (r.visited / r.planned) * 100 : 0}%` }} />
                  </div>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
      {agentOptions.some((a) => !withRoute.has(a.id)) && (
        <section className="rounded-xl border border-line bg-surface">
          <div className="border-b border-line px-4 py-3 text-sm font-semibold text-ink">Без маршрута</div>
          <ul className="divide-y divide-line">
            {agentOptions
              .filter((a) => !withRoute.has(a.id))
              .map((a) => (
                <li key={a.id} className="flex items-center gap-3 px-4 py-2.5">
                  <span className="min-w-0 flex-1 truncate text-sm text-ink">
                    {a.name} <span className="text-xs text-ink-3">{a.teamName}</span>
                  </span>
                  <Link href={`/field/routes${qs({ agent: a.id, date: sp(params, "date") })}`} className={buttonClass.outline}>
                    Сформировать
                  </Link>
                </li>
              ))}
          </ul>
        </section>
      )}
    </FieldPage>
  );
}
