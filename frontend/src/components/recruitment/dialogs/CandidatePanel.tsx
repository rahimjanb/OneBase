"use client";

import Link from "next/link";
import { useState } from "react";
import { ArrowRightLeft, Briefcase, CalendarCheck, IdCard, Loader2, Phone, Send, UserX } from "lucide-react";
import { useT } from "@/i18n/client";
import type { DialogCandidate } from "@/lib/hr-dialogs";
import { STAGE_STEPS, scoreTone, stageIndex } from "@/lib/hr-dialogs-view";
import { recruitmentApi, sourceLabel, stageLabel, STAGES, type Stage } from "@/lib/recruitment";
import { Avatar } from "./parts";

const stepLabel: Record<string, string> = { New: "Отклик", Screening: "Анкета", Interview: "Собес.", Offer: "Оффер" };
const toneText = { ok: "text-ok", warn: "text-warn", bad: "text-bad" } as const;
const toneBar = { ok: "bg-ok", warn: "bg-warn", bad: "bg-bad" } as const;

/** Собеседование: «чт, 13 ноя, 11:00» (по поясу собеседования). */
export function interviewText(iso: string, zone: string | null, long = false): string {
  return new Date(iso).toLocaleString("ru-RU", {
    timeZone: zone ?? "Asia/Tashkent",
    weekday: long ? "short" : undefined,
    day: "numeric",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  }).replace(".", "");
}

function Section({ title, right, children }: { title: string; right?: React.ReactNode; children: React.ReactNode }) {
  const t = useT();
  return (
    <section className="border-b border-line px-[18px] py-3.5">
      <div className="mb-2.5 flex items-baseline justify-between gap-2">
        <h3 className="text-[11px] font-semibold uppercase tracking-[0.4px] text-ink-4">{t(title)}</h3>
        {right}
      </div>
      {children}
    </section>
  );
}

/**
 * Карточка кандидата в диалоге: профиль и контакты (username — открыть Telegram, телефон — скопировать), вакансия и этапы, собеседование, оценка
 * AI по критериям, анкета, которую собрал AI, действия — открыть карточку, сменить этап, отказать (с подтверждением).
 */
export function CandidatePanel({ c, canManage, onChanged }: { c: DialogCandidate; canManage: boolean; onChanged: () => void }) {
  const t = useT();
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [stageMenu, setStageMenu] = useState(false);
  const [copied, setCopied] = useState(false);
  const current = stageIndex(c.stage);
  const closed = c.stage === "Rejected" || c.stage === "Withdrawn" || c.stage === "Hired";

  const run = async (what: string, action: () => Promise<unknown>) => {
    setBusy(what);
    setError(null);
    try {
      await action();
      onChanged();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(null);
    }
  };
  const changeStage = (stage: Stage) => {
    setStageMenu(false);
    if (!c.applicationId) return;
    void run("stage", () => recruitmentApi.stage(c.applicationId!, stage, null, null));
  };
  const reject = () => {
    if (!c.applicationId) return;
    const reason = prompt(t("Отказать кандидату «{name}»? Причина — для истории (кандидат её не увидит):", { name: c.name }), "");
    if (reason === null) return;
    void run("reject", () => recruitmentApi.stage(c.applicationId!, "Rejected", null, reason.trim() || t("Не подходит")));
  };

  return (
    <div className="flex min-h-full flex-col">
      <div className="flex flex-col items-center border-b border-line p-[18px] text-center">
        <Avatar id={c.id} name={c.name} size={64} />
        <p className="mt-2.5 text-base font-bold text-ink">{c.name}</p>
        {c.city && <p className="mt-0.5 text-xs text-ink-3">{c.city}</p>}
        <div className="mt-2.5 flex flex-wrap justify-center gap-1.5">
          {c.username && (
            <a href={`https://t.me/${c.username}`} target="_blank" rel="noreferrer" className="flex items-center gap-1 rounded-lg bg-muted px-2 py-1 text-[11px] font-medium text-ink-2 hover:text-ink">
              <Send className="size-3" aria-hidden />@{c.username}
            </a>
          )}
          {c.phone && (
            <button
              type="button"
              onClick={() => {
                void navigator.clipboard?.writeText(c.phone!);
                setCopied(true);
                setTimeout(() => setCopied(false), 1500);
              }}
              title={t("Скопировать номер")}
              className="flex items-center gap-1 rounded-lg bg-muted px-2 py-1 text-[11px] font-medium text-ink-2 tabular-nums hover:text-ink"
            >
              <Phone className="size-3" aria-hidden />
              {copied ? t("Скопировано") : c.phone}
            </button>
          )}
        </div>
      </div>

      {c.vacancyTitle && (
        <Section title="Вакансия и этап">
          <div className="flex items-center gap-2.5">
            <span className="grid size-[34px] shrink-0 place-items-center rounded-[10px] bg-muted text-ink-2">
              <Briefcase className="size-4" aria-hidden />
            </span>
            <div className="min-w-0">
              <p className="truncate text-[13px] font-semibold text-ink">{c.vacancyTitle}</p>
              <p className="truncate text-[11px] text-ink-3">{[c.vacancyPlace, c.source ? t(sourceLabel[c.source] ?? c.source) : null].filter(Boolean).join(" · ")}</p>
            </div>
          </div>
          {current >= 0 ? (
            <div className="mt-3 grid grid-cols-4 gap-1">
              {STAGE_STEPS.map((s, i) => (
                <div key={s}>
                  <span aria-hidden className={`block h-1 rounded-full ${i < current ? "bg-ok" : i === current ? "bg-accent" : "bg-line-strong"}`} />
                  <span className={`mt-1 block text-[10px] ${i === current ? "font-bold text-accent-strong" : "text-ink-4"}`}>{t(stepLabel[s])}</span>
                </div>
              ))}
            </div>
          ) : (
            <p className="mt-3 text-xs font-semibold text-bad">{t(stageLabel[c.stage as Stage] ?? c.stage ?? "")}</p>
          )}
          {c.interviewAt && (
            <p className="mt-3 flex items-center gap-2 rounded-[10px] bg-ok-soft px-3 py-2 text-xs font-semibold text-ok">
              <CalendarCheck className="size-4 shrink-0" aria-hidden />
              {t("Собеседование: {at}", { at: interviewText(c.interviewAt, c.interviewTimeZone, true) })}
            </p>
          )}
        </Section>
      )}

      {c.score && (
        <Section
          title="Оценка AI"
          right={
            <span className="tabular-nums">
              <b className={`text-lg font-bold ${toneText[scoreTone(c.score.total)]}`}>{c.score.total}</b>
              <span className="text-xs text-ink-4">/100</span>
            </span>
          }
        >
          <ul className="space-y-2.5">
            {c.score.criteria.map((k) => (
              <li key={k.label}>
                <div className="flex justify-between gap-2 text-xs">
                  <span className="truncate text-ink-2">{k.label}</span>
                  <span className={`text-[11px] font-semibold tabular-nums ${toneText[scoreTone(k.value)]}`}>{k.value}%</span>
                </div>
                <span aria-hidden className="mt-1 block h-1 rounded-full bg-line-strong">
                  <span className={`block h-1 rounded-full ${toneBar[scoreTone(k.value)]}`} style={{ width: `${k.value}%` }} />
                </span>
              </li>
            ))}
          </ul>
        </Section>
      )}

      {c.form.length > 0 && (
        <Section title="Анкета · собрал AI">
          <dl className="space-y-1.5 text-xs">
            {c.form.map((f) => (
              <div key={f.label} className="flex justify-between gap-3">
                <dt className="text-ink-3">{f.label}</dt>
                <dd className="text-right font-medium text-ink">{f.value}</dd>
              </div>
            ))}
          </dl>
        </Section>
      )}

      <div className="mt-auto space-y-2 p-[18px]">
        <Link href={`/hr/recruitment/candidates/${c.id}`} className="flex h-10 items-center justify-center gap-2 rounded-xl bg-accent text-[13px] font-semibold text-white">
          <IdCard className="size-4" aria-hidden />
          {t("Открыть карточку кандидата")}
        </Link>
        {canManage && c.applicationId && !closed && (
          <div className="relative grid grid-cols-2 gap-2">
            <button
              type="button"
              onClick={() => setStageMenu(!stageMenu)}
              aria-expanded={stageMenu}
              className="flex h-10 items-center justify-center gap-1.5 rounded-xl border border-line bg-muted text-xs font-semibold text-ink"
            >
              {busy === "stage" ? <Loader2 className="size-4 animate-spin" /> : <ArrowRightLeft className="size-4" aria-hidden />}
              {t("Сменить этап")}
            </button>
            <button type="button" onClick={reject} className="flex h-10 items-center justify-center gap-1.5 rounded-xl bg-bad-soft text-xs font-semibold text-bad">
              {busy === "reject" ? <Loader2 className="size-4 animate-spin" /> : <UserX className="size-4" aria-hidden />}
              {t("Отказать")}
            </button>
            {stageMenu && (
              <div role="menu" className="absolute bottom-12 left-0 z-20 w-56 rounded-xl border border-line bg-surface-solid p-1 shadow-lg">
                {STAGES.filter((s) => s !== c.stage && s !== "Rejected" && s !== "Withdrawn").map((s) => (
                  <button key={s} role="menuitem" type="button" onClick={() => changeStage(s)} className="block w-full rounded-lg px-3 py-2 text-left text-sm text-ink hover:bg-muted">
                    {t(stageLabel[s])}
                  </button>
                ))}
              </div>
            )}
          </div>
        )}
        {error && <p className="text-xs text-bad">{t(error)}</p>}
      </div>
    </div>
  );
}
