"use client";

import { Fragment, useEffect, useState } from "react";
import { ChevronDown, ChevronRight, Loader2, RefreshCw, RotateCcw } from "lucide-react";
import { SyncStatusBadge } from "@/components/hr/HrBits";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { dayLabel, momentLabel, triggerLabel, type HrSyncRun, type PageResult } from "@/lib/hr";

const button = "inline-flex h-9 items-center gap-2 rounded-lg border border-line bg-surface px-4 text-sm font-medium text-ink hover:bg-muted disabled:opacity-50";

const seconds = (run: HrSyncRun) => {
  const end = run.finishedAt ? Date.parse(run.finishedAt) : Date.now();
  const s = Math.max(0, Math.round((end - Date.parse(run.startedAt)) / 1000));
  return s >= 60 ? `${Math.floor(s / 60)} мин ${s % 60} с` : `${s} с`;
};

/**
 * Журнал синхронизации Verifix: начало, конец, длительность, загружено (новых и изменённых), пропущено (без изменений), ошибки, шаги;
 * «Повторить» — для неудачных и частичных. Пока идёт запуск — обновляется сам.
 */
export function SyncJournal({ initial, configured }: { initial: PageResult<HrSyncRun>; configured: boolean }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [page, setPage] = useState(initial.page);
  const [open, setOpen] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const running = data.items.some((r) => r.status === "Running");

  const load = async (p = page) => {
    setData(await bff<PageResult<HrSyncRun>>(`integrations/verifix/runs?page=${p}&pageSize=30`));
    setPage(p);
  };

  useEffect(() => {
    if (!running) return;
    const timer = setInterval(() => void load().catch(() => undefined), 3000);
    return () => clearInterval(timer);
  }, [running]);

  const act = async (key: string, path: string) => {
    setBusy(key);
    setError(null);
    try {
      await bff(path, { method: "POST" });
      await load(1);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };

  const pages = Math.max(1, Math.ceil(data.total / data.pageSize));

  return (
    <div>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <button type="button" className={button} disabled={!configured || running || busy !== null} onClick={() => void act("sync", "integrations/verifix/sync")}>
          {busy === "sync" || running ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />} {t(running ? "Синхронизация идёт…" : "Синхронизировать сейчас")}
        </button>
        <button type="button" className={button} onClick={() => void load().catch((e) => setError((e as Error).message))}>
          {t("Обновить журнал")}
        </button>
        {error && <span className="text-sm text-bad">{error}</span>}
      </div>

      {data.items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-line bg-surface/60 px-6 py-10 text-center text-sm text-ink-3">{t("Синхронизаций ещё не было.")}</p>
      ) : (
        <div className="overflow-x-auto rounded-xl border border-line bg-surface">
          <table className="w-full min-w-[880px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="w-8 px-2 py-2.5" />
                <th className="px-3 py-2.5 font-semibold">{t("Начало")}</th>
                <th className="px-3 py-2.5 font-semibold">{t("Конец")}</th>
                <th className="px-3 py-2.5 font-semibold">{t("Запуск")}</th>
                <th className="px-3 py-2.5 font-semibold">{t("Итог")}</th>
                <th className="px-3 py-2.5 text-right font-semibold">{t("Загружено")}</th>
                <th className="px-3 py-2.5 text-right font-semibold">{t("Пропущено")}</th>
                <th className="px-3 py-2.5 text-right font-semibold">{t("Ошибок")}</th>
                <th className="px-3 py-2.5 font-semibold">{t("Период")}</th>
                <th className="px-3 py-2.5" />
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {data.items.map((r) => (
                <Fragment key={r.id}>
                  <tr className="cursor-pointer hover:bg-muted/40" onClick={() => setOpen(open === r.id ? null : r.id)}>
                    <td className="px-2 py-2 text-ink-3">{open === r.id ? <ChevronDown className="size-4" /> : <ChevronRight className="size-4" />}</td>
                    <td className="px-3 py-2 tabular-nums text-ink">{momentLabel(r.startedAt)}</td>
                    <td className="px-3 py-2 tabular-nums text-ink-2">
                      {r.finishedAt ? momentLabel(r.finishedAt) : "—"} <span className="text-xs text-ink-3">({seconds(r)})</span>
                    </td>
                    <td className="px-3 py-2 text-ink-2">
                      {t(triggerLabel[r.trigger])}
                      {r.startedBy && <div className="text-xs text-ink-3">{r.startedBy}</div>}
                    </td>
                    <td className="px-3 py-2">
                      <SyncStatusBadge status={r.status} />
                    </td>
                    <td className="px-3 py-2 text-right tabular-nums" title={t("новых {c}, изменённых {u}", { c: r.created, u: r.updated })}>
                      {r.created + r.updated}
                    </td>
                    <td className="px-3 py-2 text-right tabular-nums text-ink-3">{r.skipped}</td>
                    <td className={`px-3 py-2 text-right tabular-nums ${r.errors > 0 ? "font-semibold text-bad" : "text-ink-3"}`}>{r.errors}</td>
                    <td className="px-3 py-2 tabular-nums text-ink-2">{r.periodFrom ? `${dayLabel(r.periodFrom)}–${dayLabel(r.periodTo)}` : "—"}</td>
                    <td className="px-3 py-2 text-right">
                      {(r.status === "Failed" || r.status === "Partial") && (
                        <button
                          type="button"
                          className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-line px-3 text-xs font-medium text-ink hover:bg-muted disabled:opacity-50"
                          disabled={running || busy !== null}
                          onClick={(e) => {
                            e.stopPropagation();
                            void act(r.id, `integrations/verifix/runs/${r.id}/retry`);
                          }}
                        >
                          {busy === r.id ? <Loader2 className="size-3.5 animate-spin" /> : <RotateCcw className="size-3.5" />} {t("Повторить")}
                        </button>
                      )}
                    </td>
                  </tr>
                  {open === r.id && (
                    <tr className="bg-muted/30">
                      <td />
                      <td colSpan={9} className="px-3 py-3">
                        {r.message && <p className={`mb-2 text-sm ${r.status === "Succeeded" ? "text-ink-2" : "text-bad"}`}>{t(r.message)}</p>}
                        {r.retryOfId && <p className="mb-2 text-xs text-ink-3">{t("Повтор неудачной синхронизации от {date}", { date: momentLabel(data.items.find((x) => x.id === r.retryOfId)?.startedAt) })}</p>}
                        {r.steps.length > 0 && (
                          <table className="w-full text-xs">
                            <thead>
                              <tr className="text-left text-ink-3">
                                <th className="py-1 pr-3 font-semibold">{t("Шаг")}</th>
                                <th className="py-1 pr-3 font-semibold">{t("Итог")}</th>
                                <th className="py-1 pr-3 text-right font-semibold">{t("Строк")}</th>
                                <th className="py-1 pr-3 text-right font-semibold">{t("Новых")}</th>
                                <th className="py-1 pr-3 text-right font-semibold">{t("Изменено")}</th>
                                <th className="py-1 pr-3 text-right font-semibold">{t("Без изменений")}</th>
                                <th className="py-1 pr-3 text-right font-semibold">{t("Страниц")}</th>
                                <th className="py-1 font-semibold">{t("Сообщение")}</th>
                              </tr>
                            </thead>
                            <tbody>
                              {r.steps.map((s) => (
                                <tr key={s.step}>
                                  <td className="py-1 pr-3 text-ink">{t(s.title)}</td>
                                  <td className={`py-1 pr-3 ${s.status === "ok" ? "text-ok" : s.status === "failed" ? "text-bad" : "text-ink-3"}`}>
                                    {t(s.status === "ok" ? "готово" : s.status === "failed" ? "ошибка" : "пропущено")}
                                  </td>
                                  <td className="py-1 pr-3 text-right tabular-nums">{s.rows}</td>
                                  <td className="py-1 pr-3 text-right tabular-nums">{s.created}</td>
                                  <td className="py-1 pr-3 text-right tabular-nums">{s.updated}</td>
                                  <td className="py-1 pr-3 text-right tabular-nums text-ink-3">{s.skipped}</td>
                                  <td className="py-1 pr-3 text-right tabular-nums text-ink-3">{s.pages}</td>
                                  <td className="py-1 text-ink-2">{s.message ? t(s.message) : ""}</td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        )}
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {pages > 1 && (
        <div className="mt-3 flex items-center justify-between text-sm text-ink-3">
          <span>{t("Стр. {page} из {pages} · всего {total}", { page, pages, total: data.total })}</span>
          <span className="flex gap-2">
            {page > 1 && (
              <button type="button" className={button} onClick={() => void load(page - 1)}>
                {t("Назад")}
              </button>
            )}
            {page < pages && (
              <button type="button" className={button} onClick={() => void load(page + 1)}>
                {t("Дальше")}
              </button>
            )}
          </span>
        </div>
      )}
    </div>
  );
}
