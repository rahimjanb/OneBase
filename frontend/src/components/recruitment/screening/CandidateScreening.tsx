"use client";

import { useCallback, useEffect, useState } from "react";
import { Globe, History, Loader2, Sparkles } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Card, ErrorText, useAction } from "@/components/recruitment/ui";
import { useT } from "@/i18n/client";
import { day, when } from "@/lib/recruitment";
import { screeningApi, type CandidateScreening as View } from "@/lib/screening";
import { SETTABLE, statusLabel, type ScreeningStatus } from "@/lib/screening-view";
import { AnalysisDetails, ScoreRing, StatusPill } from "./parts";

/**
 * Карточка кандидата → «AI-анализ»: по каждому отклику — статус скрининга, последний анализ целиком (оценки по критериям с доказательствами,
 * сильные и слабые стороны, недостающие требования, рекомендация, вопросы), история рейтинга, ссылка на профиль HH. Анализировать и менять
 * статус — HR; анализ — платный вызов AI, только по кнопке.
 */
export function CandidateScreening({ candidateId, canManage, hasResume }: { candidateId: string; canManage: boolean; hasResume: boolean }) {
  const t = useT();
  const [items, setItems] = useState<View[] | null>(null);
  const [reject, setReject] = useState<{ id: string; reason: string } | null>(null);
  const [open, setOpen] = useState<Record<string, string>>({});
  const { busy, error, run } = useAction();
  const load = useCallback(() => screeningApi.candidate(candidateId).then(setItems, () => setItems([])), [candidateId]);
  useEffect(() => {
    void load();
  }, [load]);
  const analyzing = items?.some((i) => i.analyzing) ?? false;
  useEffect(() => {
    if (!analyzing) return;
    const id = window.setInterval(() => void load(), 8000);
    return () => window.clearInterval(id);
  }, [analyzing, load]);

  const status = (id: string, s: ScreeningStatus, reason: string | null = null) =>
    run(`status-${id}`, async () => {
      await screeningApi.status(id, s, null, reason);
      setReject(null);
      await load();
    });

  return (
    <Card title="AI-анализ">
      {!items ? (
        <p className="text-sm text-ink-3">{t("Загрузка…")}</p>
      ) : items.length === 0 ? (
        <p className="text-sm text-ink-3">{t("Кандидат не рассматривается ни на одну вакансию — добавьте вакансию выше, чтобы AI оценил его под неё.")}</p>
      ) : (
        <div className="space-y-5">
          {items.map((it) => {
            const shownId = open[it.applicationId] ?? it.history[0]?.id;
            const shown = it.history.find((h) => h.id === shownId) ?? it.history[0];
            return (
              <section key={it.applicationId} className="space-y-3 border-b border-line pb-5 last:border-0 last:pb-0">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex min-w-0 flex-wrap items-center gap-2">
                    <span className="truncate text-[14px] font-semibold text-ink">
                      {it.vacancyCode} · {it.vacancyTitle}
                    </span>
                    <StatusPill status={it.status} />
                    {it.hhUrl && (
                      <a href={it.hhUrl} target="_blank" rel="noreferrer noopener" className="inline-flex items-center gap-1 text-[12px] font-medium text-accent-strong hover:underline">
                        <Globe aria-hidden className="size-3.5" />
                        {t("Профиль на hh.uz")}
                      </a>
                    )}
                  </div>
                  {canManage && (
                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        className={button}
                        disabled={!!busy || it.analyzing || !hasResume}
                        title={hasResume ? t("Платный вызов AI") : t("Сначала загрузите резюме")}
                        onClick={() =>
                          run(`analyze-${it.applicationId}`, async () => {
                            await screeningApi.analyze(it.applicationId);
                            setOpen((o) => ({ ...o, [it.applicationId]: "" }));
                            await load();
                          })
                        }
                      >
                        {busy === `analyze-${it.applicationId}` || it.analyzing ? <Loader2 className="size-4 animate-spin" /> : <Sparkles className="size-4" />}
                        {it.analyzing ? t("Анализируется…") : it.history.length > 0 ? t("Проанализировать заново") : t("Проанализировать")}
                      </button>
                      <select
                        className={`${field} w-auto!`}
                        aria-label={t("Статус кандидата")}
                        value=""
                        disabled={!!busy}
                        onChange={(e) => {
                          const s = e.target.value as ScreeningStatus;
                          if (!s) return;
                          if (s === "rejected") setReject({ id: it.applicationId, reason: "" });
                          else void status(it.applicationId, s);
                        }}
                      >
                        <option value="">{t("Сменить статус…")}</option>
                        {SETTABLE.filter((s) => s !== it.status).map((s) => (
                          <option key={s} value={s}>
                            {t(statusLabel[s])}
                          </option>
                        ))}
                      </select>
                    </div>
                  )}
                </div>
                {reject?.id === it.applicationId && (
                  <div className="flex flex-wrap gap-2">
                    <input className={`${field} w-auto! min-w-[240px] flex-1`} autoFocus placeholder={t("Причина отказа (обязательно)")} value={reject.reason}
                      onChange={(e) => setReject({ ...reject, reason: e.target.value })} />
                    <button type="button" className={primary} disabled={!reject.reason.trim() || !!busy} onClick={() => status(it.applicationId, "rejected", reject.reason.trim())}>
                      {t("Отказать")}
                    </button>
                    <button type="button" className={button} onClick={() => setReject(null)}>
                      {t("Отмена")}
                    </button>
                  </div>
                )}

                {shown ? (
                  <AnalysisDetails a={shown} />
                ) : (
                  <p className="rounded-lg bg-muted/50 px-3 py-2.5 text-[13px] text-ink-3">
                    {it.analyzing
                      ? t("Резюме обрабатывается — анализ появится здесь через минуту-две.")
                      : hasResume
                        ? t("Анализа ещё нет. Нажмите «Проанализировать» — AI оценит кандидата под вакансию по шести критериям.")
                        : t("Анализа нет: у кандидата нет резюме. Загрузите файл в «Документах» ниже (с HH — откройте профиль на hh.uz и скачайте резюме).")}
                  </p>
                )}

                {it.history.length > 1 && (
                  <div>
                    <p className="mb-1.5 flex items-center gap-1.5 text-[12px] font-semibold text-ink">
                      <History aria-hidden className="size-3.5" />
                      {t("История рейтинга")}
                    </p>
                    <div className="flex flex-wrap gap-2">
                      {it.history.map((h) => (
                        <button
                          key={h.id}
                          type="button"
                          onClick={() => setOpen((o) => ({ ...o, [it.applicationId]: h.id }))}
                          aria-pressed={h.id === shown?.id}
                          className={`flex items-center gap-2 rounded-[10px] border px-2 py-1 text-left outline-none focus-visible:ring-2 focus-visible:ring-accent ${h.id === shown?.id ? "border-accent bg-accent-soft" : "border-line bg-surface hover:border-accent/60"}`}
                          title={when(h.createdAt)}
                        >
                          <ScoreRing score={h.overall} size={40} />
                          <span className="text-[11px] leading-tight text-ink-2">
                            {day(h.createdAt)}
                            {h.delta != null && h.delta !== 0 && (
                              <span className={`block font-semibold ${h.delta > 0 ? "text-ok" : "text-bad"}`}>
                                {h.delta > 0 ? "+" : "−"}
                                {Math.abs(h.delta)}
                              </span>
                            )}
                          </span>
                        </button>
                      ))}
                    </div>
                  </div>
                )}
              </section>
            );
          })}
        </div>
      )}
      <ErrorText error={error} />
    </Card>
  );
}
