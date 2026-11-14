"use client";

import Link from "next/link";
import { useState } from "react";
import { ArrowRight, CircleAlert, FlaskConical, GitBranch, ListChecks, MessageSquareText, ShieldCheck, Timer } from "lucide-react";
import { useT } from "@/i18n/client";
import { dateTime, num } from "@/lib/analytik";
import {
  duration,
  evidenceClass,
  hypothesisStatus,
  investigationStatus,
  researchOf,
  roleTitle,
  stepStatus,
  traceKind,
  usd,
  waves as planWaves,
  type HypothesisView,
  type InvestigationDetail,
  type InvestigationRow,
  type StepView,
} from "@/lib/intelligence";
import { Card, Empty } from "../ui";
import { ResearchView } from "./ResearchView";

function Pill({ tone, children }: { tone: string; children: React.ReactNode }) {
  return <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium ${tone}`}>{children}</span>;
}

/** Строка списка исследований: вопрос, статус, шаги и гипотезы, проверки, стоимость и время. */
export function InvestigationList({ rows, showAuthor }: { rows: InvestigationRow[]; showAuthor: boolean }) {
  const t = useT();
  if (!rows.length) return <Empty text="Исследований пока нет" hint="Задайте AI-аналитику сложный вопрос — «почему…», «исследуй…», «проверь гипотезу…» — или запустите исследование без модели" />;
  return (
    <div className="overflow-hidden rounded-2xl border border-line bg-surface">
      <ul className="divide-y divide-line">
        {rows.map((r) => {
          const st = investigationStatus[r.status] ?? investigationStatus.planned;
          return (
            <li key={r.id}>
              <Link href={`/analytik/investigations/${r.id}`} className="flex flex-col gap-1.5 px-4 py-3 hover:bg-muted/50 sm:flex-row sm:items-center sm:gap-4">
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-ink">{r.question}</p>
                  <p className="mt-0.5 text-xs text-ink-3">
                    {dateTime(r.startedAt)}
                    {showAuthor && r.author ? ` · ${r.author}` : ""}
                  </p>
                </div>
                <div className="flex flex-wrap items-center gap-2 text-xs text-ink-3">
                  <Pill tone={st.tone}>{t(st.label)}</Pill>
                  <span>{t("шагов")} {r.stepCount}</span>
                  <span>
                    {t("гипотез")} {r.hypotheses}
                    {r.hypotheses > 0 ? ` (✓${r.confirmed} ✗${r.rejected})` : ""}
                  </span>
                  {r.reviewRejections > 0 && <Pill tone="bg-warn/15 text-warn">{t("проверка вернула")} {r.reviewRejections}</Pill>}
                  <span>{duration(r.durationMs)}</span>
                  <span>{usd(r.costUsd)}</span>
                  <ArrowRight className="hidden size-4 text-ink-4 sm:block" />
                </div>
              </Link>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

/** Исследование: исследование-шаблон (без модели) — своим видом (ResearchView), исследование модели — планом, шагами и проверками. */
export function InvestigationView({ data }: { data: InvestigationDetail }) {
  const research = researchOf(data);
  return research ? <ResearchView row={data.row} research={research} /> : <ModelInvestigation data={data} />;
}

/** Исследование модели целиком: цель, роли, план волнами, шаги с промежуточными результатами, гипотезы, противоречия, проверки, трасса, метрики. */
function ModelInvestigation({ data }: { data: InvestigationDetail }) {
  const t = useT();
  const { row } = data;
  const st = investigationStatus[row.status] ?? investigationStatus.planned;
  const steps = data.steps ?? [];
  const waves = data.plan?.waves?.length ? data.plan.waves : planWaves(data.plan?.steps ?? steps);
  const hypotheses = data.hypotheses ?? [];
  const metrics: { label: string; value: string }[] = [
    { label: "Глубина", value: String(row.depth) },
    { label: "Шагов", value: String(row.stepCount) },
    { label: "Вызовов модели", value: String(row.llmCalls) },
    { label: "Инструментов и расчётов", value: String(row.toolCalls) },
    { label: "Токены (вход / выход)", value: `${num(data.inputTokens)} / ${num(data.outputTokens)}` },
    { label: "Стоимость", value: usd(row.costUsd) },
    { label: "Время", value: duration(row.durationMs) },
    { label: "Возвратов проверки", value: String(row.reviewRejections) },
  ];

  return (
    <div className="grid gap-4">
      <Card>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="text-xs text-ink-3">{dateTime(row.startedAt)}{row.author ? ` · ${row.author}` : ""}</p>
            <h2 className="mt-1 text-base font-semibold text-ink">{row.question}</h2>
            {row.goal && row.goal !== row.question && <p className="mt-1 text-sm text-ink-2">{t("Цель")}: {row.goal}</p>}
          </div>
          <div className="flex shrink-0 items-center gap-2">
            <Pill tone={st.tone}>{t(st.label)}</Pill>
            {row.conversationId && (
              <Link href={`/analytik/chat/${row.conversationId}`} className="inline-flex items-center gap-1 text-xs font-medium text-accent-strong hover:underline">
                <MessageSquareText className="size-3.5" />
                {t("Открыть чат")}
              </Link>
            )}
          </div>
        </div>
        {data.roles && data.roles.length > 0 && (
          <div className="mt-3 flex flex-wrap gap-1.5">
            {data.roles.map((r) => (
              <Pill key={r} tone="bg-muted text-ink-2">{t(roleTitle[r] ?? r)}</Pill>
            ))}
          </div>
        )}
        <dl className="mt-4 grid grid-cols-2 gap-x-4 gap-y-2 sm:grid-cols-4">
          {metrics.map((m) => (
            <div key={m.label} className="min-w-0">
              <dt className="text-[11px] text-ink-4">{t(m.label)}</dt>
              <dd className="text-sm font-medium tabular-nums text-ink">{m.value}</dd>
            </div>
          ))}
        </dl>
      </Card>

      <Card title="Гипотезы" subtitle="Статус и вид вывода ставит расчёт (код), а не модель">
        {hypotheses.length === 0 ? <Empty text="Гипотез не было" hint="Вопрос не о причинах — проверять было нечего" /> : <Hypotheses items={hypotheses} />}
      </Card>

      <Card title="План и шаги" subtitle={data.plan?.fallback ? "План составлен без модели (планировщик был недоступен)" : "Шаги одной волны выполнялись параллельно"}>
        {steps.length === 0 ? (
          <Empty text="Шаги не выполнялись" />
        ) : (
          <ol className="grid gap-3">
            {waves.map((wave, i) => (
              <li key={i} className="grid gap-2">
                <p className="flex items-center gap-1.5 text-[11px] font-medium uppercase tracking-wide text-ink-4">
                  <GitBranch className="size-3.5" />
                  {t("Волна")} {i + 1}
                </p>
                <div className="grid gap-2 md:grid-cols-2">
                  {wave.map((id) => {
                    const s = steps.find((x) => x.id === id);
                    return s ? <Step key={id} step={s} /> : null;
                  })}
                </div>
              </li>
            ))}
          </ol>
        )}
      </Card>

      {(data.contradictions?.length ?? 0) > 0 && (
        <Card title="Противоречия">
          <ul className="grid gap-2">
            {data.contradictions!.map((c, i) => (
              <li key={i} className="flex gap-2 rounded-xl bg-warn/10 px-3 py-2 text-sm text-ink-2">
                <CircleAlert className="mt-0.5 size-4 shrink-0 text-warn" />
                {c.text}
              </li>
            ))}
          </ul>
        </Card>
      )}

      <Card title="Проверка выводов" subtitle="Ревизор: числа, причины против статусов гипотез, ограничения, период, пересчёт финансовых выводов">
        <Reviews data={data} />
      </Card>

      {(data.limitations?.length ?? 0) > 0 && (
        <Card title="Ограничения данных">
          <ul className="grid list-disc gap-1 pl-5 text-sm text-ink-2">
            {data.limitations!.map((l, i) => (
              <li key={i}>{l}</li>
            ))}
          </ul>
        </Card>
      )}

      <Card title="Ход исследования" subtitle="Трасса: что и когда сделано">
        <Trace data={data} />
      </Card>
    </div>
  );
}

function Hypotheses({ items }: { items: HypothesisView[] }) {
  const t = useT();
  return (
    <ul className="grid gap-2">
      {items.map((h) => {
        const st = hypothesisStatus[h.status] ?? hypothesisStatus.inconclusive;
        return (
          <li key={h.id} className="rounded-xl border border-line px-3 py-2.5">
            <div className="flex flex-wrap items-start justify-between gap-2">
              <p className="min-w-0 flex-1 text-sm font-medium text-ink">
                <FlaskConical className="mr-1.5 inline size-4 text-ink-4" />
                {h.statement}
              </p>
              <div className="flex flex-wrap items-center gap-1.5">
                <Pill tone={st.tone}>{t(st.label)}</Pill>
                <span className="text-xs tabular-nums text-ink-3">{Math.round(h.confidence * 100)} %</span>
              </div>
            </div>
            <p className="mt-1 text-xs text-ink-3">
              {t(evidenceClass[h.evidenceClass ?? ""] ?? "—")}
              {h.test ? ` · ${t("проверка")}: ${h.test.method}` : ""}
              {h.origin !== "planner" ? ` · ${h.origin === "skeptic" ? t("предложил скептик") : t("добавлена при перепланировании")}` : ""}
            </p>
            {h.supporting.length > 0 && <p className="mt-1.5 text-xs text-ok">+ {h.supporting.join(" ")}</p>}
            {h.refuting.length > 0 && <p className="mt-1 text-xs text-bad">− {h.refuting.join(" ")}</p>}
            {h.needed.length > 0 && <p className="mt-1 text-xs text-ink-3">{t("Не хватает")}: {h.needed.join("; ")}</p>}
          </li>
        );
      })}
    </ul>
  );
}

function Step({ step }: { step: StepView }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const ok = step.status === "completed";
  return (
    <div className="min-w-0 rounded-xl border border-line px-3 py-2.5">
      <div className="flex items-start justify-between gap-2">
        <p className="min-w-0 text-sm font-medium text-ink">
          <span className="mr-1.5 text-xs text-ink-4">{step.id}</span>
          {step.question}
        </p>
        <Pill tone={ok ? "bg-ok/15 text-ok" : step.status === "partial" ? "bg-warn/15 text-warn" : "bg-bad/15 text-bad"}>{t(stepStatus[step.status] ?? step.status)}</Pill>
      </div>
      <p className="mt-1 text-xs text-ink-3">
        {step.analyst}
        {step.dependsOn.length > 0 ? ` · ${t("после")} ${step.dependsOn.join(", ")}` : ""} · {duration(step.ms)} · {t("инструментов")} {step.toolCalls}
      </p>
      <p className="mt-1.5 text-sm text-ink-2">{step.summary}</p>
      {(step.findings.length > 0 || step.metrics.length > 0) && (
        <button type="button" onClick={() => setOpen(!open)} className="mt-1.5 text-xs font-medium text-accent-strong hover:underline">
          {open ? t("Скрыть подробности") : t("Подробности шага")}
        </button>
      )}
      {open && (
        <div className="mt-2 grid gap-2 text-xs text-ink-2">
          {step.findings.length > 0 && (
            <ul className="grid list-disc gap-0.5 pl-4">
              {step.findings.map((f, i) => (
                <li key={i}>{f}</li>
              ))}
            </ul>
          )}
          {step.metrics.length > 0 && (
            <dl className="grid grid-cols-2 gap-x-3 gap-y-1">
              {step.metrics.map((m, i) => (
                <div key={i} className="min-w-0">
                  <dt className="truncate text-ink-4">{m.name}</dt>
                  <dd className="tabular-nums">{m.value} {m.unit ?? ""}</dd>
                </div>
              ))}
            </dl>
          )}
          {step.unverified.length > 0 && <p className="text-warn">{t("Не подтверждено данными")}: {step.unverified.join("; ")}</p>}
          {step.sources.length > 0 && <p className="text-ink-4">{step.sources.map((s) => `${s.title}${s.period ? ` (${s.period})` : ""}`).join("; ")}</p>}
        </div>
      )}
    </div>
  );
}

function Reviews({ data }: { data: InvestigationDetail }) {
  const t = useT();
  const cycles = data.reviews?.cycles ?? [];
  const objections = data.reviews?.objections ?? [];
  if (!cycles.length && !objections.length) return <Empty text="Проверок не было" />;
  return (
    <div className="grid gap-3">
      {cycles.map((c) => (
        <div key={c.round} className="rounded-xl border border-line px-3 py-2.5">
          <p className="flex flex-wrap items-center gap-2 text-sm font-medium text-ink">
            <ShieldCheck className={`size-4 ${c.verdict === "approve" ? "text-ok" : "text-warn"}`} />
            {t("Проверка")} {c.round}: {c.verdict === "approve" ? t("выводы приняты") : t("возвращено на доработку")}
            <span className="text-xs font-normal text-ink-4">{c.by === "code+model" ? t("код и независимая модель") : t("правила кода")}</span>
          </p>
          {c.issues.length > 0 && (
            <ul className="mt-1.5 grid gap-1 text-xs text-ink-2">
              {c.issues.map((i, k) => (
                <li key={k} className={i.severity === "error" ? "text-bad" : "text-ink-3"}>
                  {i.text}
                </li>
              ))}
            </ul>
          )}
          {c.action && <p className="mt-1 text-xs text-ink-3">{t("Действие")}: {c.action}</p>}
        </div>
      ))}
      {objections.length > 0 && (
        <div>
          <p className="mb-1 text-xs font-medium text-ink-3">{t("Возражения скептика")}</p>
          <ul className="grid list-disc gap-0.5 pl-5 text-sm text-ink-2">
            {objections.map((o, i) => (
              <li key={i}>{o}</li>
            ))}
          </ul>
        </div>
      )}
      {data.reviews?.grounding && (
        <p className="flex items-center gap-1.5 text-xs text-ink-3">
          <ListChecks className="size-3.5" />
          {t("Чисел ответа сверено с данными")}: {data.reviews.grounding.checked}
          {data.reviews.grounding.unsupported.length > 0 ? ` · ${t("не подтверждено")}: ${data.reviews.grounding.unsupported.join("; ")}` : ""}
        </p>
      )}
    </div>
  );
}

function Trace({ data }: { data: InvestigationDetail }) {
  const t = useT();
  const events = data.trace ?? [];
  if (!events.length) return <Empty text="Трасса пуста" />;
  return (
    <ol className="relative grid gap-2 border-l border-line pl-4">
      {events.map((e, i) => (
        <li key={i} className="relative text-sm">
          <span className="absolute -left-[21px] top-1.5 size-2 rounded-full bg-accent" />
          <p className="text-xs text-ink-4">
            <Timer className="mr-1 inline size-3" />
            {duration(e.atMs)} · {t(traceKind[e.kind] ?? e.kind)}
          </p>
          <p className="text-ink-2">{e.text}</p>
        </li>
      ))}
    </ol>
  );
}
