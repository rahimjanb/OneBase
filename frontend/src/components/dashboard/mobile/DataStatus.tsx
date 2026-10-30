"use client";

import { useEffect, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import { RefreshCw, WifiOff } from "lucide-react";
import { useT } from "@/i18n/client";
import { TIME_ZONE } from "@/lib/format";

function useOnline() {
  const [online, setOnline] = useState(true);
  useEffect(() => {
    const update = () => setOnline(navigator.onLine);
    update();
    window.addEventListener("online", update);
    window.addEventListener("offline", update);
    return () => {
      window.removeEventListener("online", update);
      window.removeEventListener("offline", update);
    };
  }, []);
  return online;
}

const hhmm = (iso: string) => new Intl.DateTimeFormat("ru-RU", { hour: "2-digit", minute: "2-digit", hourCycle: "h23", timeZone: TIME_ZONE }).format(new Date(iso));

/** Нет связи: страница остаётся с последними данными, сверху — «Нет соединения · данные на 14:12». */
export function OfflineNote({ at }: { at: string }) {
  const t = useT();
  const online = useOnline();
  if (online) return null;
  return (
    <p role="status" className="flex items-center gap-[8px] rounded-[12px] bg-(--ts-warning-soft) px-[14px] py-[10px] text-[13px] font-medium text-(--ts-warning)">
      <WifiOff aria-hidden className="size-[16px] shrink-0" strokeWidth={2} />
      {t("Нет соединения · данные на {time}", { time: hhmm(at) })}
    </p>
  );
}

/**
 * Свежесть данных внизу страницы: «Данные обновлены 5 мин назад» (время — когда сервер собрал страницу; обновляет жест «потянуть вниз»).
 * API не ответил — «Не удалось обновить · Повторить».
 */
export function DataStatus({ at, ok }: { at: string; ok: boolean }) {
  const t = useT();
  const router = useRouter();
  const online = useOnline();
  const [pending, startTransition] = useTransition();
  const [now, setNow] = useState(() => Date.parse(at));

  useEffect(() => {
    setNow(Date.now());
    const timer = window.setInterval(() => setNow(Date.now()), 30_000);
    return () => window.clearInterval(timer);
  }, [at]);

  const minutes = Math.max(0, Math.floor((now - Date.parse(at)) / 60_000));
  const failed = !ok || !online;
  const retry = () => startTransition(() => router.refresh());

  return (
    <p className="flex min-h-[44px] items-center justify-center gap-[8px] text-[11px] text-(--ts-muted)" aria-live="polite">
      <span aria-hidden className={`size-[6px] rounded-full ${failed ? "bg-(--ts-danger)" : "bg-(--ts-success)"}`} />
      {failed ? (
        <>
          <span>{t("Не удалось обновить")}</span>·
          <button type="button" onClick={retry} disabled={pending || !online} className="inline-flex min-h-[44px] items-center gap-[4px] font-semibold text-(--ts-link) disabled:opacity-60">
            {pending && <RefreshCw aria-hidden className="size-[12px] animate-spin" />}
            {t("Повторить")}
          </button>
        </>
      ) : (
        <span suppressHydrationWarning>
          {pending
            ? t("Обновляем…")
            : minutes < 1
              ? t("Данные обновлены только что")
              : t("Данные обновлены {n} мин назад", { n: minutes })}
          {" · "}
          {t("потяните, чтобы обновить")}
        </span>
      )}
    </p>
  );
}
