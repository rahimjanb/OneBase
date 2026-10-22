import { NotificationList } from "@/components/field/Misc";
import { FieldPage } from "@/components/field/ui";
import { getT } from "@/i18n/server";
import { fieldGet } from "@/lib/field/api";
import type { FieldNotification } from "@/lib/field/types";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Уведомления") };
}

export default async function NotificationsPage() {
  const t = await getT();
  const data = await fieldGet<{ items: FieldNotification[]; unread: number }>("notifications?take=100", "/field/notifications");
  return (
    <FieldPage title={t("Уведомления")} subtitle={data.unread ? t("непрочитанных: {n}", { n: data.unread }) : t("всё прочитано")}>
      <NotificationList items={data.items} />
    </FieldPage>
  );
}
