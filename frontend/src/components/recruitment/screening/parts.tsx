"use client";

import Link from "next/link";
import { useState } from "react";
import { AlertTriangle, BadgeCheck, CircleHelp, Globe, Quote, ShieldCheck, ShieldOff } from "lucide-react";
import { useT } from "@/i18n/client";
import { when } from "@/lib/recruitment";
import type { HhGuardBrief, ScreeningAnalysis, ScreeningCriterion } from "@/lib/screening";
import { categoryLabel, percent, pointsText, questionTypeLabel, scoreTone, statusLabel, statusTone, type ScreeningStatus, type Tone } from "@/lib/screening-view";

/** Общие части AI-скрининга: тон, оценка кольцом, статус, плитка показателя, полоса HH, анализ кандидата целиком. Цвета — токены палитры подбора. */

export const toneClass: Record<Tone, string> = {
  ok: "bg-ok-soft text-ok",
  accent: "bg-accent-soft text-accent-strong",
  violet: "bg-violet-soft text-violet",
  warn: "bg-warn-soft text-warn",
  bad: "bg-bad-soft text-bad",
  neutral: "bg-muted text-ink-2",
};

const toneStroke: Record<Tone, string> = {
  ok: "var(--color-ok)",
  accent: "var(--color-accent)",
  violet: "var(--color-violet)",
  warn: "var(--color-warn)",
  bad: "var(--color-bad)",
  neutral: "var(--color-ink-4)",
};

/** Оценка 0–100 кольцом; нет оценки — серое кольцо и «—». */
export function ScoreRing({ score, size = 52 }: { score: number | null; size?: 40 | 52 | 72 }) {
  const t = useT();
  const tone = scoreTone(score);
  const r = 42;
  const c = 2 * Math.PI * r;
  const text = size === 72 ? "text-[22px]" : size === 52 ? "text-[15px]" : "text-[12px]";
  return (
    <span className="relative inline-grid shrink-0 place-items-center" style={{ width: size, height: size }} role="img" aria-label={score == null ? t("Нет оценки") : t("Оценка {n} из 100", { n: score })}>
      <svg viewBox="0 0 100 100" className="absolute inset-0 -rotate-90" aria-hidden>
        <circle cx="50" cy="50" r={r} fill="none" stroke="var(--color-muted)" strokeWidth="9" />
        {score != null && <circle cx="50" cy="50" r={r} fill="none" stroke={toneStroke[tone]} strokeWidth="9" strokeLinecap="round" strokeDasharray={`${(c * score) / 100} ${c}`} />}
      </svg>
      <span className={`font-bold tabular-nums text-ink ${text}`}>{score ?? "—"}</span>
    </span>
  );
}

export function StatusPill({ status }: { status: ScreeningStatus }) {
  const t = useT();
  return (
    <span className={`inline-flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-[7px] px-2 py-[3px] text-[11px] font-semibold ${toneClass[statusTone[status]]}`}>
      <span aria-hidden className={`size-1.5 rounded-full bg-current ${status === "analyzing" ? "animate-pulse" : ""}`} />
      {t(statusLabel[status])}
    </span>
  );
}

export function CategoryPill({ score }: { score: number }) {
  const t = useT();
  const tone = scoreTone(score);
  const cat = score >= 90 ? "excellent" : score >= 80 ? "very_good" : score >= 70 ? "good" : score >= 50 ? "partial" : "low";
  return <span className={`inline-flex whitespace-nowrap rounded-[7px] px-2 py-[3px] text-[11px] font-semibold ${toneClass[tone]}`}>{t(categoryLabel[cat])}</span>;
}

export function Kpi({ label, value, hint, tone = "neutral", active, onClick }: { label: string; value: number; hint?: string; tone?: Tone; active?: boolean; onClick?: () => void }) {
  const t = useT();
  const body = (
    <>
      <span className="block truncate text-[12px] font-medium text-ink-3">{t(label)}</span>
      <span className="mt-1 flex items-baseline gap-2">
        <span className="text-[26px] font-bold leading-none tabular-nums text-ink max-md:text-[22px]">{value}</span>
        <span aria-hidden className={`size-2 rounded-full ${toneClass[tone].split(" ")[0]}`} />
      </span>
      {hint && <span className="mt-1 block truncate text-[11px] text-ink-4">{t(hint)}</span>}
    </>
  );
  const cls = `rounded-[14px] border bg-surface px-4 py-3 text-left max-md:px-3 ${active ? "border-accent ring-2 ring-accent/20" : "border-line"}`;
  return onClick ? (
    <button type="button" onClick={onClick} aria-pressed={active} className={`${cls} outline-none hover:border-accent/60 focus-visible:ring-2 focus-visible:ring-accent`}>
      {body}
    </button>
  ) : (
    <div className={cls}>{body}</div>
  );
}

/**
 * Полоса HH над списком: подключение, последняя синхронизация, бесплатные запросы и заблокированные Quota Guard операции за 30 дней,
 * предохранитель. Бесплатные запросы — это не доступ к базе резюме: резюме из откликов HR загружает файлом.
 */
export function HhStrip({ hh, canManage }: { hh: HhGuardBrief; canManage: boolean }) {
  const t = useT();
  const tripped = !!hh.guardTrippedAt;
  return (
    <div className={`flex flex-wrap items-center gap-x-4 gap-y-2 rounded-[14px] border px-4 py-2.5 text-[12px] ${tripped ? "border-bad/40 bg-bad-soft" : "border-line bg-surface"}`}>
      <span className="flex items-center gap-2 font-semibold text-ink">
        <Globe aria-hidden className="size-4 text-warn" />
        HH.uz
        <span className={`rounded-[6px] px-1.5 py-0.5 text-[11px] ${hh.connected ? (hh.enabled ? toneClass.ok : toneClass.warn) : toneClass.neutral}`}>
          {t(hh.connected ? (hh.enabled ? "подключён" : "синхронизация остановлена") : "не подключён")}
        </span>
      </span>
      {hh.lastSyncAt && (
        <span className={hh.lastSyncOk === false ? "text-bad" : "text-ink-3"}>
          {t("Синхронизация")}: {when(hh.lastSyncAt)}
        </span>
      )}
      <span className="flex items-center gap-1 text-ink-3" title={t("Запросы, которые не расходуют платные услуги и лимиты HH (список откликов, вакансии, вход). Это не доступ к базе резюме.")}>
        <ShieldCheck aria-hidden className="size-3.5 text-ok" />
        {t("бесплатных запросов за 30 дн.: {n}", { n: hh.freeCalls })}
      </span>
      <span className="flex items-center gap-1 text-ink-3" title={hh.lastBlockReason ?? undefined}>
        <ShieldOff aria-hidden className="size-3.5 text-bad" />
        {t("заблокировано платных операций: {n}", { n: hh.blockedCalls })}
      </span>
      {tripped && (
        <span className="flex items-center gap-1 font-medium text-bad">
          <AlertTriangle aria-hidden className="size-3.5" />
          {t("Предохранитель")}: {hh.guardReason}
        </span>
      )}
      {canManage && (
        <Link href="/hr/recruitment/hh#guard" className="ml-auto font-medium text-accent-strong hover:underline">
          {t("Quota Guard →")}
        </Link>
      )}
    </div>
  );
}

const statusName: Record<ScreeningCriterion["status"], string> = { met: "соответствует", partial: "частично", not_met: "не соответствует", not_stated: "не указано" };

/** Критерий: баллы полосой, статус, объяснение, цитаты (подтверждённые — с галочкой, нет в резюме — «не подтверждено»). */
function CriterionRow({ c }: { c: ScreeningCriterion }) {
  const t = useT();
  const pct = c.max > 0 ? Math.round((c.points / c.max) * 100) : 0;
  const tone = c.status === "not_stated" ? "neutral" : pct >= 75 ? "ok" : pct >= 50 ? "accent" : pct >= 30 ? "warn" : "bad";
  return (
    <li className="py-2.5">
      <div className="flex items-center justify-between gap-3">
        <span className="min-w-0 text-[13px] font-semibold text-ink">{t(c.name)}</span>
        <span className="shrink-0 text-[12px] tabular-nums text-ink-2">{pointsText(c.points, c.max)}</span>
      </div>
      <span aria-hidden className="mt-1.5 block h-1.5 overflow-hidden rounded-full bg-muted">
        <span className="block h-full rounded-full" style={{ width: `${pct}%`, background: toneStroke[tone] }} />
      </span>
      <p className="mt-1.5 text-[12px] leading-relaxed text-ink-2">
        <span className={`mr-1.5 rounded-[5px] px-1.5 py-px text-[10px] font-semibold ${toneClass[tone]}`}>{t(statusName[c.status] ?? c.status)}</span>
        {c.explanation}
      </p>
      {c.quotes.length > 0 && (
        <ul className="mt-1 space-y-0.5">
          {c.quotes.map((q, i) => (
            <li key={i} className={`flex gap-1.5 text-[11px] ${q.verified ? "text-ink-3" : "text-warn"}`}>
              {q.verified ? <BadgeCheck aria-hidden className="mt-px size-3 shrink-0 text-ok" /> : <Quote aria-hidden className="mt-px size-3 shrink-0" />}
              <span>
                «{q.text}»{!q.verified && ` — ${t("в резюме не найдено, не засчитано")}`}
              </span>
            </li>
          ))}
        </ul>
      )}
      {c.capped && <p className="mt-1 text-[11px] text-warn">{t("Балл ограничен половиной: нет подтверждения в резюме.")}</p>}
    </li>
  );
}

function List({ title, items, tone }: { title: string; items: string[]; tone: Tone }) {
  const t = useT();
  if (items.length === 0) return null;
  return (
    <div>
      <p className="mb-1 text-[12px] font-semibold text-ink">{t(title)}</p>
      <ul className="space-y-1">
        {items.map((x, i) => (
          <li key={i} className="flex gap-2 text-[12px] leading-relaxed text-ink-2">
            <span aria-hidden className={`mt-1.5 size-1.5 shrink-0 rounded-full ${toneClass[tone].split(" ")[0]}`} />
            {x}
          </li>
        ))}
      </ul>
    </div>
  );
}

/**
 * Анализ кандидата целиком: итог, категория, рекомендация, уверенность модели отдельно от оценки, покрытие фактами, критерии с
 * доказательствами, почему подходит, сильные и слабые стороны, недостающие требования, вопросы для собеседования.
 */
export function AnalysisDetails({ a }: { a: ScreeningAnalysis }) {
  const t = useT();
  const [all, setAll] = useState(false);
  const questions = all ? a.questions : a.questions.slice(0, 5);
  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-4">
        <ScoreRing score={a.overall} size={72} />
        <div className="min-w-0 flex-1 space-y-1.5">
          <div className="flex flex-wrap items-center gap-2">
            <CategoryPill score={a.overall} />
            {a.delta != null && a.delta !== 0 && (
              <span className={`text-[12px] font-semibold ${a.delta > 0 ? "text-ok" : "text-bad"}`}>
                {a.delta > 0 ? "+" : "−"}
                {Math.abs(a.delta)} {t("к прошлому анализу")}
              </span>
            )}
            {a.lowData && <span className={`rounded-[7px] px-2 py-[3px] text-[11px] font-semibold ${toneClass.warn}`}>{t("Мало данных в резюме")}</span>}
            {a.version < 2 && <span className={`rounded-[7px] px-2 py-[3px] text-[11px] font-semibold ${toneClass.neutral}`}>{t("Прежняя оценка — проанализируйте заново")}</span>}
          </div>
          <p className="text-[13px] leading-relaxed text-ink">{a.summary}</p>
          <p className="flex flex-wrap gap-x-3 text-[11px] text-ink-3">
            <span>{t("Анализ")}: {when(a.createdAt)}</span>
            {a.coverage != null && (
              <span title={t("Доля веса критериев, подтверждённых цитатами из резюме.")}>
                {t("Подтверждено фактами")}: {percent(a.coverage)}
              </span>
            )}
            {a.confidence != null && (
              <span className="inline-flex items-center gap-1" title={t("Насколько модель уверена в оценке по полноте данных. На оценку не влияет.")}>
                {t("Уверенность AI")}: {percent(a.confidence)}
                <CircleHelp aria-hidden className="size-3" />
              </span>
            )}
          </p>
        </div>
      </div>

      {a.recommendationText && (
        <div className="rounded-xl bg-accent-soft px-3 py-2.5 text-[13px] text-ink">
          <span className="font-semibold text-accent-strong">{t("Рекомендация AI")}: </span>
          {a.recommendationText}
        </div>
      )}
      {a.whyFits && (
        <p className="text-[13px] leading-relaxed text-ink-2">
          <span className="font-semibold text-ink">{t("Почему подходит")}: </span>
          {a.whyFits}
        </p>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <ul className="divide-y divide-line">
          {a.criteria.map((c) => (
            <CriterionRow key={c.code} c={c} />
          ))}
        </ul>
        <div className="space-y-4">
          <List title="Сильные стороны" items={a.strengths} tone="ok" />
          <List title="Слабые стороны" items={a.weaknesses} tone="warn" />
          <List title="Недостающие требования" items={a.missing} tone="bad" />
          {a.questions.length > 0 && (
            <div>
              <p className="mb-1 text-[12px] font-semibold text-ink">{t("Вопросы для собеседования")}</p>
              <ol className="space-y-1.5">
                {questions.map((q, i) => (
                  <li key={i} className="text-[12px] leading-relaxed text-ink-2">
                    <span className="font-medium text-ink">
                      {i + 1}. {q.question}
                    </span>
                    <span className="ml-1.5 rounded-[5px] bg-muted px-1.5 py-px text-[10px] text-ink-3">{t(questionTypeLabel[q.type] ?? q.type)}</span>
                    {q.reason && <span className="block text-[11px] text-ink-4">{q.reason}</span>}
                  </li>
                ))}
              </ol>
              {a.questions.length > 5 && (
                <button type="button" className="mt-1 text-[12px] font-medium text-accent-strong hover:underline" onClick={() => setAll(!all)}>
                  {all ? t("Свернуть") : t("Все вопросы ({n})", { n: a.questions.length })}
                </button>
              )}
            </div>
          )}
        </div>
      </div>
      <p className="text-[11px] text-ink-4">
        {t("Совет для HR, не решение. AI не учитывает пол, возраст, национальность, религию, фото и семейное положение — такие сведения скрываются до анализа.")}
      </p>
    </div>
  );
}
