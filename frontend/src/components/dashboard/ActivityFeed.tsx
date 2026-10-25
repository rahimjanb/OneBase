import Link from "next/link";
import { CircleCheck, FileText, MessageCircle, TriangleAlert, UserPlus, type LucideIcon } from "lucide-react";
import type { ActivityEvent } from "@/lib/dashboard.mock";
import type { T } from "@/i18n/core";
import { CardHeader, DashCard, toneSoft, toneText } from "./blocks";

const icons: Record<ActivityEvent["icon"], LucideIcon> = { check: CircleCheck, file: FileText, alert: TriangleAlert, "user-plus": UserPlus, comment: MessageCircle };

/** «Лента активности»: последние уведомления пользователя (задачи поставлены, изменены, выполнены); «Live» — лента обновляется вместе с данными. */
export function ActivityFeed({ events, available, t }: { events: ActivityEvent[]; available: boolean; t: T }) {
  return (
    <DashCard className="flex flex-col gap-4 p-5 xl:w-[320px]">
      <CardHeader
        title={t("Лента активности")}
        subtitle={t("Последние события")}
        action={
          available ? (
            <span className="inline-flex items-center gap-1.5 rounded-[20px] bg-ok/10 px-2.5 py-1 text-[11.5px] font-semibold text-ok">
              <span className="size-1.5 rounded-full bg-ok" /> Live
            </span>
          ) : undefined
        }
      />
      {events.length === 0 ? (
        <div className="flex flex-1 items-center justify-center px-4 py-10 text-center text-[13px] text-ink-4">
          {available ? t("Событий пока нет") : t("Уведомления недоступны")}
        </div>
      ) : (
        <ul className="flex flex-col gap-4">
          {events.map((e) => {
            const Icon = icons[e.icon];
            const body = (
              <>
                <span className="block text-[12.5px] font-semibold text-ink">{e.who}</span>
                {e.what && <span className="block text-[12.5px] leading-[1.4] text-ink-3">{e.what}</span>}
              </>
            );
            return (
              <li key={e.id} className="flex items-start gap-3">
                <span className={`grid size-[30px] shrink-0 place-items-center rounded-lg ${toneSoft[e.tone]} ${toneText[e.tone]}`}>
                  <Icon className="size-[15px]" strokeWidth={1.75} />
                </span>
                {e.href ? (
                  <Link href={e.href} className="min-w-0 flex-1 rounded-md hover:underline">
                    {body}
                  </Link>
                ) : (
                  <span className="min-w-0 flex-1">{body}</span>
                )}
                <span className="shrink-0 text-[11px] text-ink-4">{e.when}</span>
              </li>
            );
          })}
        </ul>
      )}
    </DashCard>
  );
}
