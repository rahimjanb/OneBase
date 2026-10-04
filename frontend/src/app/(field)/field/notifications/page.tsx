import { NotificationList } from "@/components/field/Misc";
import { FieldPage } from "@/components/field/ui";
import { fieldGet } from "@/lib/field/api";
import type { FieldNotification } from "@/lib/field/types";

export const metadata = { title: "Уведомления" };

export default async function NotificationsPage() {
  const data = await fieldGet<{ items: FieldNotification[]; unread: number }>("notifications?take=100", "/field/notifications");
  return (
    <FieldPage title="Уведомления" subtitle={data.unread ? `непрочитанных: ${data.unread}` : "всё прочитано"}>
      <NotificationList items={data.items} />
    </FieldPage>
  );
}
