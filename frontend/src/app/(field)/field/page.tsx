import { DateSwitch, ParamSelect } from "@/components/field/DateSwitch";
import { FieldPage } from "@/components/field/ui";
import { PushPrompt } from "@/components/shell/AppBridge";
import { getT } from "@/i18n/server";
import { AgentTodayView, DashboardView } from "@/components/field/views";
import { fieldGet, fieldMe, qs, sp, type FieldSearchParams } from "@/lib/field/api";
import type { FieldDashboard, FieldMe, FieldStructure, FieldToday } from "@/lib/field/types";

export const metadata = { title: { absolute: "Sales Base" } };

/** Главная Sales Base: агенту — его день, супервайзеру — команда, РМ — организация (с фильтрами команды и филиала). */
export default async function FieldHome({ searchParams }: { searchParams: Promise<FieldSearchParams> }) {
  const params = await searchParams;
  const t = await getT();
  const me = await fieldMe();
  const date = sp(params, "date") ?? me.today;

  if (me.role === "Agent") {
    const today = await fieldGet<FieldToday>(`today${qs({ date: sp(params, "date") })}`, "/field");
    return (
      <FieldPage title={t("Сегодня")} subtitle={[me.name, me.teamName, me.supervisorName ? t("супервайзер {name}", { name: me.supervisorName }) : null].filter(Boolean).join(" · ")} actions={<DateSwitch value={date} today={me.today} />}>
        <div className="space-y-4">
          <PushPrompt appName="Sales Base" text={t("Включите уведомления — новые задачи и изменения маршрута будут приходить сразу на телефон.")} />
          <AgentTodayView data={today} me={me} own />
        </div>
      </FieldPage>
    );
  }

  const team = sp(params, "team");
  const branch = sp(params, "branch");
  const [dashboard, structure] = await Promise.all([
    fieldGet<FieldDashboard>(`dashboard${qs({ date: sp(params, "date"), teamId: team, branchId: branch })}`, "/field"),
    me.role === "Rm" ? fieldGet<FieldStructure>("structure", "/field") : Promise.resolve(null),
  ]);
  const query = new URLSearchParams(Object.entries({ date: sp(params, "date") ?? "", branch: branch ?? "" }).filter(([, v]) => v)).toString();

  return (
    <FieldPage
      title={me.role === "Rm" ? t("Дашборд") : t("Моя команда")}
      subtitle={me.role === "Rm" ? t("Продажи, план и работа команд") : me.teamName ?? t("Агенты вашей команды")}
      actions={
        <>
          {structure && (
            <>
              <ParamSelect param="team" value={team ?? ""} label={t("Команда")} options={[{ value: "", label: t("Все команды") }, ...structure.teams.map((tm) => ({ value: tm.id, label: tm.name }))]} />
              <ParamSelect param="branch" value={branch ?? ""} label={t("Филиал")} options={[{ value: "", label: t("Все филиалы") }, ...structure.branches.map((b) => ({ value: String(b.id), label: b.name }))]} />
            </>
          )}
          <DateSwitch value={date} today={me.today} />
        </>
      }
    >
      <div className="space-y-4">
        <PushPrompt appName="Sales Base" text={t("Включите уведомления — выполненные задачи и рекомендации AI по команде будут приходить сразу.")} />
        <DashboardView data={dashboard} me={me} query={query} />
      </div>
    </FieldPage>
  );
}
