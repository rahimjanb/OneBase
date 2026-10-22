"use client";

import { useCallback, useEffect, useState } from "react";
import { Loader2, RefreshCw, Search } from "lucide-react";
import { button, field } from "@/components/ai/form";
import { Note } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { chatTypeLabel, telegramApi, type TelegramAccount, type TelegramChat } from "@/lib/telegram";

/** Чаты аккаунта из Telegram: получатели рассылок; в группах — включение агента. */
export function TelegramChats({ account, onCount }: { account: TelegramAccount; onCount: (n: number) => void }) {
  const t = useT();
  const [rows, setRows] = useState<TelegramChat[] | null>(null);
  const [search, setSearch] = useState("");
  const [type, setType] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setRows(await telegramApi.chats(account.id, search, type));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [account.id, search, type]);

  useEffect(() => {
    const t = setTimeout(load, 250);
    return () => clearTimeout(t);
  }, [load]);

  const sync = async () => {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      const r = await telegramApi.syncChats(account.id);
      onCount(r.chats);
      setMessage(t("Загружено чатов: {n}.", { n: r.chats }));
      await load();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const toggleAgent = async (chat: TelegramChat) => {
    try {
      const updated = await telegramApi.chatAgent(account.id, chat.id, !chat.agentEnabled);
      setRows((r) => r?.map((c) => (c.id === updated.id ? updated : c)) ?? null);
    } catch (e) {
      setError((e as Error).message);
    }
  };

  return (
    <div>
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative min-w-[240px] flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" />
          <input className={`${field} pl-9`} value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("Название или @username")} />
        </div>
        <select className={field.replace("w-full", "w-auto")} value={type} onChange={(e) => setType(e.target.value)}>
          <option value="">{t("Все")}</option>
          <option value="Private">{t("Личные")}</option>
          <option value="Group">{t("Группы")}</option>
          <option value="Channel">{t("Каналы")}</option>
        </select>
        <button className={button} onClick={sync} disabled={busy || account.status !== "Connected"}>
          {busy ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
          {t("Обновить из Telegram")}
        </button>
      </div>
      {message && <p className="mt-3 rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">{message}</p>}
      {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}

      <div className="mt-4 overflow-x-auto">
        {rows === null ? (
          <Loader2 className="size-5 animate-spin text-ink-3" />
        ) : rows.length === 0 ? (
          <p className="text-sm text-ink-3">{t("Чатов нет — нажмите «Обновить из Telegram».")}</p>
        ) : (
          <table className="w-full min-w-[640px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">{t("Чат")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Тип")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Писать")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Агент в группе")}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((c) => (
                <tr key={c.id} className={`border-b border-line last:border-b-0 ${c.canSend ? "" : "opacity-60"}`}>
                  <td className="py-2 pr-3">
                    <span className="block font-medium text-ink">{c.title}</span>
                    {c.username && <span className="text-xs text-ink-3">@{c.username}</span>}
                  </td>
                  <td className="py-2 pr-3 text-ink-2">{t(chatTypeLabel[c.type])}</td>
                  <td className="py-2 pr-3">
                    {c.canSend ? (
                      <span className="text-ok">{t("можно")}</span>
                    ) : (
                      <span className="text-ink-3">
                        {t("нельзя")}
                        {c.reason && <span className="block text-xs">{t(c.reason)}</span>}
                      </span>
                    )}
                  </td>
                  <td className="py-2 pr-3">
                    {c.type === "Group" ? (
                      <label className="flex items-center gap-2 text-ink">
                        <input type="checkbox" checked={c.agentEnabled} onChange={() => toggleAgent(c)} className="size-4 accent-[var(--color-accent)]" />
                        {c.agentEnabled ? t("отвечает на обращения") : t("выключен")}
                      </label>
                    ) : (
                      <span className="text-xs text-ink-3">{c.type === "Private" ? t("по списку пользователей") : "—"}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      <Note>
        {t(
          "Здесь — чаты, которые есть у аккаунта в Telegram. «Писать нельзя» — канал без прав администратора, группа, из которой аккаунт вышел, или чат пропал из диалогов. Рассылка идёт только в чаты, куда писать можно.",
        )}
      </Note>
    </div>
  );
}
