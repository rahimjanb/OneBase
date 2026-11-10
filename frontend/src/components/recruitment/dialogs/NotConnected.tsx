"use client";

import Link from "next/link";
import { useState } from "react";
import { ArrowRight, Loader2, Send, Settings, TriangleAlert, UserRoundPlus } from "lucide-react";
import { useT } from "@/i18n/client";
import { dialogsApi } from "@/lib/hr-dialogs";

const steps: [string, string][] = [
  ["Откройте «Настройки → Telegram»", "Доступно администратору компании"],
  ["Подключите номер и войдите по коду", "Код придёт в Telegram на этот номер"],
  ["Выберите «Подразделения: HR»", "Диалоги появятся в этом разделе"],
];

/** Баннер «Номер HR не подключён» над колонками (компьютер). */
export function NotConnectedBanner({ canConfigure }: { canConfigure: boolean }) {
  const t = useT();
  return (
    <div className="mx-6 mt-4 flex flex-wrap items-center gap-3 rounded-2xl border border-warn/25 bg-warn-soft px-4 py-3">
      <TriangleAlert className="size-5 shrink-0 text-warn" aria-hidden />
      <div className="min-w-0 flex-1">
        <p className="text-[13px] font-bold text-warn">{t("Номер HR не подключён")}</p>
        <p className="text-xs text-ink-2">
          {t("Администратор подключает его в «Настройки → Telegram» и выбирает «Подразделения: HR». До этого кандидаты не смогут написать.")}
        </p>
      </div>
      {canConfigure && (
        <Link href="/settings/telegram" className="flex items-center gap-1.5 rounded-lg bg-warn px-3 py-2 text-xs font-bold text-[#1a1206]">
          {t("Настройки → Telegram")}
          <ArrowRight className="size-4" aria-hidden />
        </Link>
      )}
    </div>
  );
}

/**
 * «Подключите номер HR к Telegram»: зачем, три шага и действие — администратору Telegram «Перейти в настройки», остальным «Попросить
 * администратора» (ему придёт уведомление).
 */
export function NotConnected({ canConfigure, mobile = false }: { canConfigure: boolean; mobile?: boolean }) {
  const t = useT();
  const [state, setState] = useState<"idle" | "busy" | "done" | string>("idle");
  const ask = async () => {
    setState("busy");
    try {
      const { notified } = await dialogsApi.askAdmin();
      setState(notified > 0 ? "done" : t("Администраторов Telegram в OneBase нет — обратитесь к руководителю."));
    } catch (e) {
      setState((e as Error).message);
    }
  };
  return (
    <div className={`mx-auto flex w-full max-w-[520px] flex-col items-center text-center ${mobile ? "py-4" : "py-10"}`}>
      <span className={`grid place-items-center rounded-2xl border border-line bg-muted text-accent-strong ${mobile ? "size-14" : "size-[72px]"}`}>
        <Send className={mobile ? "size-6" : "size-8"} aria-hidden />
      </span>
      <h2 className={`mt-4 font-bold text-ink ${mobile ? "text-lg" : "text-[22px]"}`}>{t("Подключите номер HR к Telegram")}</h2>
      <p className={`mt-2 text-ink-3 ${mobile ? "text-[13px]" : "text-sm"}`}>
        {t("Кандидаты будут писать на один номер, AI ответит на типовые вопросы и соберёт анкету, а сложные диалоги передаст вам.")}
      </p>
      <ol className="mt-5 w-full space-y-2 text-left">
        {steps.map(([title, hint], i) => (
          <li key={title} className="flex items-center gap-3 rounded-xl border border-line bg-muted px-3.5 py-2.5">
            <span className="grid size-[26px] shrink-0 place-items-center rounded-full bg-accent-soft text-xs font-bold text-accent-strong">{i + 1}</span>
            <span className="min-w-0">
              <span className="block text-[13px] font-semibold text-ink">{t(title)}</span>
              <span className="block text-xs text-ink-3">{t(hint)}</span>
            </span>
          </li>
        ))}
      </ol>
      <div className="mt-5 flex flex-wrap justify-center gap-2.5">
        {canConfigure ? (
          <Link href="/settings/telegram" className="flex h-10 items-center gap-2 rounded-xl bg-accent px-4 text-sm font-semibold text-white">
            <Settings className="size-4" aria-hidden />
            {t("Перейти в настройки")}
          </Link>
        ) : (
          <button type="button" onClick={ask} disabled={state === "busy" || state === "done"} className="flex h-10 items-center gap-2 rounded-xl border border-line bg-surface px-4 text-sm font-semibold text-ink disabled:opacity-60">
            {state === "busy" ? <Loader2 className="size-4 animate-spin" /> : <UserRoundPlus className="size-4" aria-hidden />}
            {t("Попросить администратора")}
          </button>
        )}
      </div>
      {state === "done" && <p className="mt-3 text-xs text-ok">{t("Администратору отправлено уведомление.")}</p>}
      {state !== "idle" && state !== "busy" && state !== "done" && <p className="mt-3 text-xs text-warn">{t(state)}</p>}
    </div>
  );
}
