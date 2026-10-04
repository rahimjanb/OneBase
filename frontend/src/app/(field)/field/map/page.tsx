import { DateSwitch, ParamSelect } from "@/components/field/DateSwitch";
import { FieldMap } from "@/components/field/FieldMap";
import { FieldPage } from "@/components/field/ui";
import { fieldGet, fieldMe, qs, sp, type FieldSearchParams } from "@/lib/field/api";
import type { FieldMapView } from "@/lib/field/types";

export const metadata = { title: "Карта" };

type AgentOption = { id: string; name: string; teamName: string | null; role: string };

/** Карта: точки зоны по состоянию, маршруты дня, позиции агентов (по последнему визиту), «я здесь» у агента. */
export default async function MapPage({ searchParams }: { searchParams: Promise<FieldSearchParams> }) {
  const params = await searchParams;
  const me = await fieldMe();
  const date = sp(params, "date") ?? me.today;
  const agent = me.role === "Agent" ? undefined : sp(params, "agent");
  const query = qs({ date, agentId: agent }).replace(/^\?/, "");
  const [view, agents] = await Promise.all([
    fieldGet<FieldMapView>(`map?${query}`, "/field/map"),
    me.role === "Agent" ? Promise.resolve([]) : fieldGet<AgentOption[]>("agents", "/field/map"),
  ]);

  return (
    <FieldPage
      title="Карта"
      subtitle={`${view.points.length} точек${view.truncated ? ` из ${view.total}` : ""} · маршрутов: ${view.routes.length}`}
      actions={
        <>
          {agents.length > 0 && <ParamSelect param="agent" value={agent ?? ""} label="Агент" options={[{ value: "", label: "Все агенты" }, ...agents.filter((a) => a.role === "Agent").map((a) => ({ value: a.id, label: a.name }))]} />}
          <DateSwitch value={date} today={me.today} />
        </>
      }
    >
      <FieldMap initial={view} query={query} followMe={me.role === "Agent"} />
    </FieldPage>
  );
}
