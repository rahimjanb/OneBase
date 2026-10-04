import { RouteBoard } from "@/components/field/RouteBoard";
import { FieldPage } from "@/components/field/ui";
import { fieldGet, fieldMe } from "@/lib/field/api";
import type { FieldRoute } from "@/lib/field/types";

export const metadata = { title: "Маршрут" };

export default async function RoutePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const me = await fieldMe();
  const route = await fieldGet<FieldRoute>(`routes/${id}`, `/field/routes/${id}`);
  const isOwn = me.memberId === route.agentId;
  const day = new Date(`${route.date}T00:00:00Z`).toLocaleDateString("ru-RU", { weekday: "long", day: "numeric", month: "long", timeZone: "UTC" });
  return (
    <FieldPage title={isOwn ? "Мой маршрут" : `Маршрут: ${route.agentName}`} subtitle={day} back={{ href: "/field/routes", label: "Маршруты" }}>
      <RouteBoard route={route} agentId={route.agentId} agentName={route.agentName} date={route.date} isOwn={isOwn} canPlan={me.canPlan && !isOwn} active={isOwn ? me.activeVisit : null} geoRadiusM={me.geoRadiusM} />
    </FieldPage>
  );
}
