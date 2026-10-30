import { NotificationsScreen } from "@/components/notifications/NotificationsScreen";
import { getT } from "@/i18n/server";
import { apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Уведомления · OneBase") };
}

/** «Уведомления»: на телефоне — экран по колокольчику, на компьютере — «Все уведомления» из панели. Вкладки разделов — по правам. */
export default async function NotificationsPage() {
  const me = await apiSession<Me>("/api/auth/me");
  const permissions = me ? (me.hrAccess ? [...me.permissions, "hr.department"] : me.permissions) : [];
  return <NotificationsScreen permissions={permissions} />;
}
