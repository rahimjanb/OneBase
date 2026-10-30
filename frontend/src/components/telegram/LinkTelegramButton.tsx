"use client";

import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import { Copy, Loader2, Send, X } from "lucide-react";
import { button, primary } from "@/components/ai/form";
import { useT } from "@/i18n/client";
import { myTelegramApi, type LinkCode, type TelegramLink } from "@/lib/telegram";

/**
 * «Привязать Telegram» в меню пользователя: одноразовый код (15 минут). Пользователь отправляет его аккаунту OneBase в Telegram — так
 * агент узнаёт, что этот Telegram принадлежит ему, и (если администратор разрешил полный доступ) отвечает с его правами.
 */
export function LinkTelegramButton({ trigger }: { trigger?: (open: () => void) => React.ReactNode } = {}) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [links, setLinks] = useState<TelegramLink[] | null>(null);
  const [code, setCode] = useState<LinkCode | null>(null);
  const [busy, setBusy] = useState(false);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    const close = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    window.addEventListener("keydown", close);
    return () => window.removeEventListener("keydown", close);
  }, [open]);

  const show = async () => {
    setOpen(true);
    setCode(null);
    setError(null);
    try {
      setLinks(await myTelegramApi.links());
    } catch (e) {
      setError((e as Error).message);
    }
  };

  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      setCode(await myTelegramApi.code());
      setCopied(false);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const unlink = async (id: string) => {
    if (!confirm(t("Отвязать Telegram? Агент перестанет отвечать вам с вашими правами до новой привязки."))) return;
    try {
      await myTelegramApi.unlink(id);
      setLinks(await myTelegramApi.links());
    } catch (e) {
      setError((e as Error).message);
    }
  };

  return (
    <>
      {trigger ? (
        trigger(() => void show())
      ) : (
        <button type="button" onClick={show} className="flex w-full items-center gap-2 rounded-md px-2.5 py-2 text-left text-sm text-ink hover:bg-muted">
          <Send className="size-4 text-ink-3" />
          {t("Привязать Telegram")}
        </button>
      )}
      {/* Окно — в body: меню профиля создаёт свой контекст позиционирования, внутри него окно обрезалось бы. */}
      {open &&
        createPortal(
        <div className="fixed inset-0 z-50 grid place-items-center bg-black/40 p-4" onClick={() => setOpen(false)}>
          <div className="w-full max-w-md rounded-xl border border-line bg-surface-solid p-5 text-ink shadow-xl" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-center justify-between">
              <h2 className="text-base font-semibold">{t("Привязать Telegram")}</h2>
              <button className="text-ink-3 hover:text-ink" onClick={() => setOpen(false)} aria-label={t("Закрыть")}>
                <X className="size-5" />
              </button>
            </div>

            {links && links.length > 0 && (
              <div className="mt-3 space-y-2">
                {links.map((l) => (
                  <div key={l.permissionId} className="flex items-center gap-2 rounded-lg bg-muted px-3 py-2 text-sm">
                    <span className="min-w-0 flex-1">
                      <span className="block truncate">
                        {l.username ? `@${l.username}` : `ID ${l.telegramUserId}`} → {l.account}
                      </span>
                      <span className="text-xs text-ink-3">{l.mode === "Full" ? t("полный доступ") : t("только отправленные данные")}</span>
                    </span>
                    <button className="text-xs text-bad hover:underline" onClick={() => unlink(l.permissionId)}>
                      {t("Отвязать")}
                    </button>
                  </div>
                ))}
              </div>
            )}

            {code ? (
              <div className="mt-4">
                <p className="text-sm text-ink-2">
                  {t("Отправьте этот код сообщением")} {code.sendTo.length > 0 ? <b className="text-ink">{code.sendTo.join(", ")}</b> : t("аккаунту OneBase")}{" "}
                  {t("в Telegram — со своего аккаунта Telegram:")}
                </p>
                <div className="mt-3 flex items-center gap-2">
                  <code className="flex-1 whitespace-nowrap rounded-lg border border-line bg-page px-3 py-2 text-center text-lg font-semibold tracking-widest">{code.code}</code>
                  <button
                    className={button}
                    onClick={async () => {
                      await navigator.clipboard.writeText(code.code);
                      setCopied(true);
                    }}
                  >
                    <Copy className="size-4" />
                    {copied ? t("Скопирован") : t("Копировать")}
                  </button>
                </div>
                <p className="mt-2 text-xs text-ink-3">
                  {t("Действует до {time}, один раз. Не передавайте код другим: кто его отправит, тот и будет привязан к вашему профилю.", {
                    time: new Date(code.expiresAt).toLocaleTimeString("ru-RU", { timeStyle: "short" }),
                  })}
                </p>
              </div>
            ) : (
              <p className="mt-3 text-sm text-ink-2">
                {t(
                  "Получите код и отправьте его аккаунту OneBase в Telegram. После этого AI-агент будет знать, что это вы, — и, если администратор включил вам полный доступ, отвечать по данным OneBase с вашими правами.",
                )}
              </p>
            )}

            {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
            <div className="mt-4 flex gap-2">
              <button className={primary} onClick={create} disabled={busy}>
                {busy && <Loader2 className="size-4 animate-spin" />}
                {code ? t("Новый код") : t("Получить код")}
              </button>
              <button className={button} onClick={() => setOpen(false)}>
                {t("Закрыть")}
              </button>
            </div>
          </div>
        </div>,
          document.body,
        )}
    </>
  );
}
