import { formatDate } from "@/i18n/core";
import { RouteBoard } from "@/components/field/RouteBoard";
import { FieldPage } from "@/components/field/ui";
import { getLocale, getT } from "@/i18n/server";
import { fieldGet, fieldMe } from "@/lib/field/api";
import type { FieldRoute } from "@/lib/field/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Маршрут") };
}

export default async function RoutePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const t = await getT();
  const locale = await getLocale();
  const me = await fieldMe();
  const route = await fieldGet<FieldRoute>(`routes/${id}`, `/field/routes/${id}`);
  const isOwn = me.memberId === route.agentId;
  const day = formatDate(new Date(`${route.date}T00:00:00Z`), locale, { weekday: "long", day: "numeric", month: "long", timeZone: "UTC" });
  return (
    <FieldPage title={isOwn ? t("Мой маршрут") : t("Маршрут: {name}", { name: route.agentName })} subtitle={day} back={{ href: "/field/routes", label: t("Маршруты") }}>
      <RouteBoard route={route} agentId={route.agentId} agentName={route.agentName} date={route.date} isOwn={isOwn} canPlan={me.canPlan && !isOwn} active={isOwn ? me.activeVisit : null} geoRadiusM={me.geoRadiusM} />
    </FieldPage>
  );
}
