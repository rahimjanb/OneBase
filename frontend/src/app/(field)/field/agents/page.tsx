import { redirect } from "next/navigation";
import { DateSwitch, ParamSelect } from "@/components/field/DateSwitch";
import { FieldPage } from "@/components/field/ui";
import { getT } from "@/i18n/server";
import { AgentTable } from "@/components/field/views";
import { fieldGet, fieldMe, qs, sp, type FieldSearchParams } from "@/lib/field/api";
import type { FieldDashboard, FieldMe, FieldStructure } from "@/lib/field/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Агенты") };
}

/** Агенты зоны: статус дня, продажи, план, визиты, задачи. */
export default async function AgentsPage({ searchParams }: { searchParams: Promise<FieldSearchParams> }) {
  const params = await searchParams;
  const t = await getT();
  const me = await fieldMe();
  if (me.role === "Agent") redirect("/field");
  const team = sp(params, "team");
  const [data, structure] = await Promise.all([
    fieldGet<FieldDashboard>(`dashboard${qs({ date: sp(params, "date"), teamId: team })}`, "/field/agents"),
    fieldGet<FieldStructure>("structure", "/field/agents"),
  ]);

  return (
    <FieldPage
      title={t("Агенты")}
      subtitle={t("{active} из {total} работали в этот день", { active: data.activeAgents, total: data.agents })}
      actions={
        <>
          {structure.teams.length > 1 && <ParamSelect param="team" value={team ?? ""} label={t("Команда")} options={[{ value: "", label: t("Все команды") }, ...structure.teams.map((tm) => ({ value: tm.id, label: tm.name }))]} />}
          <DateSwitch value={sp(params, "date") ?? me.today} today={me.today} />
        </>
      }
    >
      <AgentTable data={data} />
    </FieldPage>
  );
}
