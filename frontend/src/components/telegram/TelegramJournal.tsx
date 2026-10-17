"use client";

import { Fragment, useCallback, useEffect, useState } from "react";
import { Loader2, RefreshCw } from "lucide-react";
import { button, field } from "@/components/ai/form";
import { executionStatus, modeLabel, telegramApi, type TelegramAccount, type TelegramExecution } from "@/lib/telegram";

const modeText: Record<TelegramExecution["mode"], string> = { full: modeLabel.Full, sent_only: modeLabel.SentOnly, link: "Привязка" };

/** Журнал агента: кто спросил, в каком режиме, что ответил, модель, токены, время, ошибки. */
export function TelegramJournal({ account }: { account: TelegramAccount }) {
  const [rows, setRows] = useState<TelegramExecution[] | null>(null);
  const [status, setStatus] = useState("");
  const [open, setOpen] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setRows(await telegramApi.executions(account.id, status));
    } catch (e) {
      setError((e as Error).message);
    }
  }, [account.id, status]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <div>
      <div className="flex flex-wrap items-center gap-2">
        <select className={field.replace("w-full", "w-auto")} value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="">Все</option>
          {Object.entries(executionStatus).map(([key, v]) => (
            <option key={key} value={key}>
              {v.label}
            </option>
          ))}
        </select>
        <button className={button} onClick={load}>
          <RefreshCw className="size-4" />
          Обновить
        </button>
      </div>
      {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
      <div className="mt-4 overflow-x-auto">
        {rows === null ? (
          <Loader2 className="size-5 animate-spin text-ink-3" />
        ) : rows.length === 0 ? (
          <p className="text-sm text-ink-3">Записей нет.</p>
        ) : (
          <table className="w-full min-w-[860px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">Когда</th>
                <th className="py-2 pr-3 font-semibold">Кто / чат</th>
                <th className="py-2 pr-3 font-semibold">Режим</th>
                <th className="py-2 pr-3 font-semibold">Итог</th>
                <th className="py-2 pr-3 font-semibold">Вопрос</th>
                <th className="py-2 pr-3 text-right font-semibold">Токены</th>
                <th className="py-2 text-right font-semibold">Время</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <Fragment key={r.id}>
                  <tr className="cursor-pointer border-b border-line hover:bg-muted/50" onClick={() => setOpen(open === r.id ? null : r.id)}>
                    <td className="whitespace-nowrap py-2 pr-3 text-ink-2">{new Date(r.createdAt).toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" })}</td>
                    <td className="py-2 pr-3">
                      <span className="block text-ink">{r.who ?? (r.telegramUserId ? `ID ${r.telegramUserId}` : "—")}</span>
                      <span className="text-xs text-ink-3">{r.chatTitle ?? r.peerId}</span>
                    </td>
                    <td className="py-2 pr-3 text-ink-2">{modeText[r.mode]}</td>
                    <td className={`py-2 pr-3 ${executionStatus[r.status]?.className ?? ""}`}>{executionStatus[r.status]?.label ?? r.status}</td>
                    <td className="max-w-[320px] truncate py-2 pr-3 text-ink-2">{r.question ?? "—"}</td>
                    <td className="py-2 pr-3 text-right tabular-nums text-ink-2">{r.inputTokens + r.outputTokens || "—"}</td>
                    <td className="py-2 text-right tabular-nums text-ink-2">{r.durationMs ? `${(r.durationMs / 1000).toFixed(1)} с` : "—"}</td>
                  </tr>
                  {open === r.id && (
                    <tr key={`${r.id}-details`} className="border-b border-line bg-muted/30">
                      <td colSpan={7} className="px-3 py-3 text-sm">
                        {r.question && (
                          <p className="whitespace-pre-wrap text-ink">
                            <b>Вопрос:</b> {r.question}
                          </p>
                        )}
                        {r.answer && (
                          <p className="mt-2 whitespace-pre-wrap text-ink-2">
                            <b className="text-ink">Ответ:</b> {r.answer}
                          </p>
                        )}
                        {r.error && <p className="mt-2 text-bad">{r.error}</p>}
                        <p className="mt-2 text-xs text-ink-3">
                          {r.model && `Модель ${r.model} · `}токены {r.inputTokens} / {r.outputTokens}
                          {r.reportId && " · отправлен отчёт"}
                        </p>
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
