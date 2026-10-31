"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Bell } from "lucide-react";
import { TypeTile, useTimeText } from "@/components/notifications/parts";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import type { NotificationItem, NotificationPage } from "@/lib/notifications";
import { markRead } from "@/lib/notifications-store";
import { BlockHeader, Empty, Sk, card, focusRing } from "./parts";

/**
 * «Уведомления»: четыре последних (на телефоне — три) с плиткой типа, заголовком, текстом, временем и точкой непрочитанного; рядом с
 * заголовком — сколько непрочитанных. Нажатие отмечает прочитанным (общий счётчик колокольчика) и открывает задачу или событие.
 */
export function NotificationsBlock() {
  const t = useT();
  const router = useRouter();
  const time = useTimeText();
  const [page, setPage] = useState<NotificationPage | null>(null);
  const [error, setError] = useState(false);

  const load = useCallback(async () => {
    try {
      setPage(await bff<NotificationPage>("notifications?filter=all&take=4"));
      setError(false);
    } catch {
      setError(true);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const open = (n: NotificationItem) => {
    if (!n.read) {
      void markRead(n.id);
      setPage((p) => (p ? { ...p, items: p.items.map((x) => (x.id === n.id ? { ...x, read: true } : x)), unread: Math.max(0, p.unread - 1) } : p));
    }
    if (n.href) router.push(n.href);
  };

  const unread = page?.counts.unread ?? page?.unread ?? 0;

  return (
    <section aria-label={t("Уведомления")} aria-busy={!page && !error} className={card}>
      <BlockHeader icon={Bell} title={t("Уведомления")} href="/notifications" action={unread > 0 ? t("Все {n}", { n: unread }) : t("Все")}>
        {unread > 0 && (
          <span className="rounded-full bg-accent px-2 py-0.5 text-[11.5px] font-bold text-white" aria-label={t("{n} непрочитанных", { n: unread })}>
            {unread}
          </span>
        )}
      </BlockHeader>
      <div className="mt-3">
        {error ? (
          <p className="text-[13.5px] text-ink-3">
            {t("Не удалось загрузить данные.")}{" "}
            <button type="button" onClick={() => void load()} className={`font-semibold text-link ${focusRing}`}>
              {t("Повторить")}
            </button>
          </p>
        ) : !page ? (
          <div className="space-y-3">
            {[0, 1, 2].map((i) => (
              <div key={i} className="flex gap-3">
                <Sk className="size-[38px] shrink-0 rounded-xl" />
                <Sk className="h-10 flex-1" />
              </div>
            ))}
          </div>
        ) : page.items.length === 0 ? (
          <Empty>{t("Уведомлений пока нет.")}</Empty>
        ) : (
          <ul className="space-y-1">
            {page.items.map((n, i) => (
              <li key={n.id} className={i >= 3 ? "max-lg:hidden" : undefined}>
                <button
                  type="button"
                  onClick={() => open(n)}
                  className={`-mx-2 flex w-[calc(100%+1rem)] items-start gap-3 rounded-lg px-2 py-2 text-left transition-colors hover:bg-surface-2 ${focusRing}`}
                >
                  <TypeTile n={n} size="md" />
                  <span className="min-w-0 flex-1">
                    <span className="block truncate text-[14px] font-semibold text-ink">{t(n.title)}</span>
                    {n.body && <span className="mt-0.5 block truncate text-[12.5px] text-ink-3">{t(n.body)}</span>}
                  </span>
                  <span className="flex shrink-0 flex-col items-end gap-1.5">
                    <span className="text-[11.5px] text-ink-3">{time(n.createdAt)}</span>
                    {!n.read && <span aria-label={t("не прочитано")} className="size-2 rounded-full bg-accent" />}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}
