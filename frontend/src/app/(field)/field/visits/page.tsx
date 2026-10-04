import Link from "next/link";
import { ParamSelect } from "@/components/field/DateSwitch";
import { JointVisits } from "@/components/field/Misc";
import { Chip, Empty, FieldPage, Pager, Tabs } from "@/components/field/ui";
import { fieldGet, fieldMe, qs, sp, type FieldSearchParams } from "@/lib/field/api";
import { geoLabel, visitResultLabel } from "@/lib/field/labels";
import { money } from "@/lib/sales/format";
import type { FieldJointVisit, FieldVisitRow, Page } from "@/lib/field/types";

export const metadata = { title: "Визиты" };

type AgentOption = { id: string; name: string; teamName: string | null; role: string };

/** Визиты Sales Base (с геоотметкой и результатом) и совместные выезды. */
export default async function VisitsPage({ searchParams }: { searchParams: Promise<FieldSearchParams> }) {
  const params = await searchParams;
  const me = await fieldMe();
  const tab = sp(params, "tab") === "joint" ? "joint" : "visits";
  const tabs = [
    { key: "visits", label: "Визиты", href: "/field/visits" },
    { key: "joint", label: "Совместные выезды", href: "/field/visits?tab=joint" },
  ];

  if (tab === "joint") {
    const [items, agents] = await Promise.all([
      fieldGet<FieldJointVisit[]>("visits/joint", "/field/visits?tab=joint"),
      me.canPlan ? fieldGet<AgentOption[]>("agents", "/field/visits?tab=joint") : Promise.resolve([]),
    ]);
    const members = [...(me.memberId && me.role !== "Agent" ? [{ id: me.memberId, name: `${me.name} (я)`, role: me.role }] : []), ...agents.map((a) => ({ id: a.id, name: a.name, role: a.role }))];
    return (
      <FieldPage title="Визиты">
        <Tabs items={tabs} current={tab} />
        <JointVisits items={items} members={members} canPlan={me.canPlan} />
      </FieldPage>
    );
  }

  const page = Number(sp(params, "page") ?? 1) || 1;
  const filters = { agentId: sp(params, "agent"), result: sp(params, "result"), geo: sp(params, "geo"), from: sp(params, "from"), to: sp(params, "to") };
  const [data, agents] = await Promise.all([
    fieldGet<Page<FieldVisitRow>>(`visits${qs({ ...filters, page, pageSize: 40 })}`, "/field/visits"),
    me.role === "Agent" ? Promise.resolve([]) : fieldGet<AgentOption[]>("agents", "/field/visits"),
  ]);
  const link = (p: number) => `/field/visits${qs({ agent: filters.agentId, result: filters.result, geo: filters.geo, page: p })}`;

  return (
    <FieldPage title="Визиты" subtitle={`${data.total} визитов в Sales Base`}>
      <Tabs items={tabs} current={tab} />
      <div className="flex flex-wrap gap-2">
        {agents.length > 0 && <ParamSelect param="agent" value={filters.agentId ?? ""} label="Агент" options={[{ value: "", label: "Все агенты" }, ...agents.filter((a) => a.role === "Agent").map((a) => ({ value: a.id, label: a.name }))]} />}
        <ParamSelect param="result" value={filters.result ?? ""} label="Результат" options={[{ value: "", label: "Любой результат" }, ...Object.entries(visitResultLabel).map(([k, [l]]) => ({ value: k, label: l }))]} />
        <ParamSelect param="geo" value={filters.geo ?? ""} label="Геопозиция" options={[{ value: "", label: "Любая геопозиция" }, ...Object.entries(geoLabel).map(([k, [l]]) => ({ value: k, label: l }))]} />
      </div>
      {data.items.length === 0 ? (
        <Empty>Визитов нет. Визит начинается в карточке точки или в маршруте — кнопкой «Начать визит».</Empty>
      ) : (
        <>
          <ul className="divide-y divide-line overflow-hidden rounded-xl border border-line bg-surface">
            {data.items.map((v) => (
              <li key={v.id} className="px-4 py-3">
                <div className="flex flex-wrap items-center gap-2">
                  <Link href={`/field/customers/${v.marketId}`} className="min-w-0 flex-1 truncate text-sm font-medium text-ink hover:underline">
                    {v.marketName}
                  </Link>
                  <span className="text-xs text-ink-3">{new Date(v.startedAt).toLocaleString("ru-RU", { day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" })}</span>
                </div>
                <div className="mt-1.5 flex flex-wrap items-center gap-1.5 text-xs text-ink-3">
                  {me.role !== "Agent" && <span>{v.agentName}</span>}
                  {v.status === "InProgress" ? <Chip tone="accent">идёт</Chip> : v.status === "Cancelled" ? <Chip tone="muted">отменён</Chip> : v.result && <Chip tone={visitResultLabel[v.result][1]}>{visitResultLabel[v.result][0]}</Chip>}
                  <Chip tone={geoLabel[v.geoStatus][1]}>
                    {geoLabel[v.geoStatus][0]}
                    {v.distanceM != null ? ` · ${Math.round(v.distanceM)} м` : ""}
                  </Chip>
                  {v.minutes != null && <span>{v.minutes} мин</span>}
                  {v.amount != null && <span>{money(v.amount)} сум</span>}
                  {v.hasPhoto && (
                    <a href={`/bff/api/field/visits/${v.id}/photo`} target="_blank" rel="noreferrer" className="text-accent-strong underline">
                      фото
                    </a>
                  )}
                </div>
                {v.comment && <p className="mt-1 text-xs text-ink-2">«{v.comment}»</p>}
              </li>
            ))}
          </ul>
          <Pager page={data.page} pageSize={data.pageSize} total={data.total} href={link} />
        </>
      )}
    </FieldPage>
  );
}
