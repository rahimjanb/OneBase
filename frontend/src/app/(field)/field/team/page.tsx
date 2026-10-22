import { redirect } from "next/navigation";
import { TeamManager } from "@/components/field/TeamManager";
import { FieldPage } from "@/components/field/ui";
import { getT } from "@/i18n/server";
import { fieldGet, fieldMe } from "@/lib/field/api";
import type { FieldStructure } from "@/lib/field/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Команды и доступы") };
}

export default async function TeamPage() {
  const t = await getT();
  const me = await fieldMe();
  if (me.role === "Agent") redirect("/field");
  const data = await fieldGet<FieldStructure>("structure", "/field/team");
  return (
    <FieldPage
      title={t("Команды и доступы")}
      subtitle={data.canManage ? t("Команды, супервайзеры, агенты и вход в Sales Base. Агенты и супервайзеры берутся из Linko.") : t("Ваша команда")}
    >
      <TeamManager data={data} />
    </FieldPage>
  );
}
