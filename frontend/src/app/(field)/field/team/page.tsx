import { redirect } from "next/navigation";
import { TeamManager } from "@/components/field/TeamManager";
import { FieldPage } from "@/components/field/ui";
import { fieldGet, fieldMe } from "@/lib/field/api";
import type { FieldStructure } from "@/lib/field/types";

export const metadata = { title: "Команды и доступы" };

export default async function TeamPage() {
  const me = await fieldMe();
  if (me.role === "Agent") redirect("/field");
  const data = await fieldGet<FieldStructure>("structure", "/field/team");
  return (
    <FieldPage
      title="Команды и доступы"
      subtitle={data.canManage ? "Команды, супервайзеры, агенты и вход в Sales Base. Агенты и супервайзеры берутся из Linko." : "Ваша команда"}
    >
      <TeamManager data={data} />
    </FieldPage>
  );
}
