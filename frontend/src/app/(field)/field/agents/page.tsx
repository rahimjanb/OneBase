import { redirect } from "next/navigation";
import { DateSwitch, ParamSelect } from "@/components/field/DateSwitch";
import { FieldPage } from "@/components/field/ui";
import { AgentTable } from "@/components/field/views";
import { fieldGet, fieldMe, qs, sp, type FieldSearchParams } from "@/lib/field/api";
import type { FieldDashboard, FieldMe, FieldStructure } from "@/lib/field/types";

export const metadata = { title: "Агенты" };

/** Агенты зоны: статус дня, продажи, план, визиты, задачи. */
export default async function AgentsPage({ searchParams }: { searchParams: Promise<FieldSearchParams> }) {
  const params = await searchParams;
  const me = await fieldMe();
  if (me.role === "Agent") redirect("/field");
  const team = sp(params, "team");
  const [data, structure] = await Promise.all([
    fieldGet<FieldDashboard>(`dashboard${qs({ date: sp(params, "date"), teamId: team })}`, "/field/agents"),
    fieldGet<FieldStructure>("structure", "/field/agents"),
  ]);

  return (
    <FieldPage
      title="Агенты"
      subtitle={`${data.activeAgents} из ${data.agents} работали в этот день`}
      actions={
        <>
          {structure.teams.length > 1 && <ParamSelect param="team" value={team ?? ""} label="Команда" options={[{ value: "", label: "Все команды" }, ...structure.teams.map((t) => ({ value: t.id, label: t.name }))]} />}
          <DateSwitch value={sp(params, "date") ?? me.today} today={me.today} />
        </>
      }
    >
      <AgentTable data={data} />
    </FieldPage>
  );
}
