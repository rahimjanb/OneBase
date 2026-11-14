"use client";

import { useEffect, useState } from "react";
import { AlertTriangle, RefreshCw, ShieldCheck, ShieldOff } from "lucide-react";
import { button } from "@/components/ai/form";
import { Card, ErrorText, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { when } from "@/lib/recruitment";
import { screeningApi, type HhGuard, type HhOperation } from "@/lib/screening";
import { toneClass } from "./parts";

const COST: Record<HhOperation["cost"], { label: string; tone: keyof typeof toneClass }> = {
  free: { label: "бесплатно", tone: "ok" },
  limit: { label: "расходует лимит HH", tone: "warn" },
  paid: { label: "платно", tone: "bad" },
  unknown: { label: "не подтверждено", tone: "neutral" },
};

/**
 * HH.uz → Quota Guard: каждый запрос к HH проверяется по каталогу до отправки — платные, расходующие лимиты и неподтверждённые операции не
 * выполняются. Счётчики за 30 дней, предохранитель (HH ответил «нужна оплата» или «лимит исчерпан» — синхронизация остановлена, «Проверить
 * снова» снимает), каталог операций с причинами и источниками, журнал последних запросов.
 */
export function HhGuardCard() {
  const t = useT();
  const [g, setG] = useState<HhGuard | null>(null);
  const [journal, setJournal] = useState(false);
  const { busy, error, run } = useAction();
  useEffect(() => {
    run("load", async () => setG(await screeningApi.guard()));
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  return (
    <div id="guard" className="scroll-mt-24">
      <Card title="Quota Guard — расход ресурсов HH">
        {!g ? (
          error ? <ErrorText error={error} /> : <p className="text-sm text-ink-3">{t("Загрузка…")}</p>
        ) : (
          <div className="space-y-4">
            <p className="text-[13px] leading-relaxed text-ink-2">
              {t("Каждый запрос к HH проверяется до отправки: выполняются только операции, бесплатность которых подтверждена документацией HH. Платные, расходующие лимиты просмотров и неподтверждённые — блокируются. Бесплатные запросы — это не доступ к базе резюме: резюме откликнувшихся HR открывает на hh.uz и загружает файлом.")}
            </p>
            <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
              <Stat icon={<ShieldCheck className="size-4 text-ok" />} label="Бесплатных запросов за 30 дн." value={g.freeCalls30} />
              <Stat icon={<ShieldOff className="size-4 text-bad" />} label="Заблокировано платных операций" value={g.blockedCalls30} />
              <Stat icon={<AlertTriangle className="size-4 text-warn" />} label="Ошибки HH за 30 дн." value={g.failedCalls30} />
              <Stat label="Запросов за сутки" value={g.callsToday} hint={t("предел OneBase — {n}", { n: g.dailyCap })} />
            </div>
            {g.guardTrippedAt ? (
              <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-bad/40 bg-bad-soft px-3 py-2.5">
                <p className="text-[13px] text-bad">
                  <b>{t("Предохранитель сработал")} {when(g.guardTrippedAt)}:</b> {g.guardReason} {t("Автоматическая синхронизация остановлена.")}
                </p>
                <button type="button" className={button} disabled={!!busy} onClick={() => run("reset", async () => setG(await screeningApi.resetGuard()))}>
                  <RefreshCw className="size-4" />
                  {t("Проверить снова")}
                </button>
              </div>
            ) : (
              <p className="text-[12px] text-ok">{t("Предохранитель не срабатывал: HH не сообщал о платных услугах или исчерпанных лимитах.")}</p>
            )}

            <div className="overflow-x-auto rounded-xl border border-line">
              <table className="w-full min-w-[720px] border-collapse text-left text-[12px]">
                <thead>
                  <tr className="bg-muted/50 text-ink-3">
                    <th className="px-3 py-2 font-semibold">{t("Операция HH")}</th>
                    <th className="px-3 py-2 font-semibold">{t("Стоимость")}</th>
                    <th className="px-3 py-2 font-semibold">{t("Разрешено")}</th>
                    <th className="px-3 py-2 font-semibold">{t("Почему")}</th>
                  </tr>
                </thead>
                <tbody>
                  {g.catalog.map((o) => (
                    <tr key={o.code} className="border-t border-line align-top">
                      <td className="px-3 py-2">
                        <span className="block font-medium text-ink">{o.method} {o.path}</span>
                        <span className="text-[11px] text-ink-4">{o.code}</span>
                      </td>
                      <td className="px-3 py-2">
                        <span className={`whitespace-nowrap rounded-[6px] px-1.5 py-0.5 text-[11px] font-semibold ${toneClass[COST[o.cost].tone]}`}>{t(COST[o.cost].label)}</span>
                      </td>
                      <td className="whitespace-nowrap px-3 py-2 text-ink-2">
                        {o.auto && o.manual ? t("авто и вручную") : o.manual ? t("только кнопкой HR") : o.auto ? t("только авто") : <span className="font-semibold text-bad">{t("заблокировано")}</span>}
                      </td>
                      <td className="px-3 py-2 leading-relaxed text-ink-2">
                        {o.why}
                        <span className="block text-[11px] text-ink-4">{t("Источник")}: {o.source}</span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div>
              <button type="button" className="text-[13px] font-medium text-accent-strong hover:underline" aria-expanded={journal} onClick={() => setJournal(!journal)}>
                {journal ? t("Скрыть журнал запросов") : t("Журнал запросов к HH ({n})", { n: g.journal.length })}
              </button>
              {journal && (
                <div className="mt-2 overflow-x-auto rounded-xl border border-line">
                  <table className="w-full min-w-[720px] border-collapse text-left text-[12px]">
                    <thead>
                      <tr className="bg-muted/50 text-ink-3">
                        <th className="px-3 py-2 font-semibold">{t("Когда")}</th>
                        <th className="px-3 py-2 font-semibold">{t("Операция")}</th>
                        <th className="px-3 py-2 font-semibold">{t("Кто")}</th>
                        <th className="px-3 py-2 font-semibold">{t("Итог")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {g.journal.length === 0 && (
                        <tr>
                          <td colSpan={4} className="px-3 py-3 text-ink-3">
                            {t("Запросов ещё не было.")}
                          </td>
                        </tr>
                      )}
                      {g.journal.map((c, i) => (
                        <tr key={i} className="border-t border-line align-top">
                          <td className="whitespace-nowrap px-3 py-2 text-ink-2">{when(c.at)}</td>
                          <td className="px-3 py-2">
                            <span className="font-medium text-ink">{c.method} {c.path}</span>
                            <span className="block text-[11px] text-ink-4">{c.operation}</span>
                          </td>
                          <td className="px-3 py-2 text-ink-2">{t(c.trigger === "manual" ? "HR" : "синхронизация")}</td>
                          <td className="px-3 py-2">
                            {!c.allowed ? (
                              <span className="text-bad">{t("заблокировано")}: {c.reason}</span>
                            ) : c.status && c.status >= 400 ? (
                              <span className="text-warn">
                                {t("ошибка HH")} {c.status}
                                {c.errorType && ` (${c.errorType})`}
                              </span>
                            ) : (
                              <span className="text-ok">
                                {t("выполнен")} · {c.durationMs} {t("мс")}
                              </span>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
            <ErrorText error={error} />
          </div>
        )}
      </Card>
    </div>
  );
}

function Stat({ icon, label, value, hint }: { icon?: React.ReactNode; label: string; value: number; hint?: string }) {
  const t = useT();
  return (
    <div className="rounded-xl border border-line px-3 py-2.5">
      <span className="flex items-center gap-1.5 text-[11px] font-medium text-ink-3">
        {icon}
        {t(label)}
      </span>
      <span className="mt-1 block text-[22px] font-bold tabular-nums text-ink">{value}</span>
      {hint && <span className="block text-[11px] text-ink-4">{hint}</span>}
    </div>
  );
}
