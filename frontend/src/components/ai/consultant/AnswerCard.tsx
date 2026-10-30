"use client";

import Link from "next/link";
import { useState } from "react";
import { ArrowRight, Check, ChevronRight, Plus, Target } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { formatNumber, progressView, signed, type AnswerBlock, type FactRow, type KpiItem, type RecommendationItem, type StructuredAnswer, type Tone } from "@/lib/consultant-view";
import { Markdown } from "../Markdown";

/** Сколько рекомендаций видно сразу; остальные — по «ещё N». */
const shownRecommendations = 3;

const toneText: Record<Tone, string> = { good: "text-ok", bad: "text-bad", warn: "text-warn", neutral: "text-ink-3" };
const toneBadge: Record<Tone, string> = { good: "bg-ok-soft text-ok", bad: "bg-bad-soft text-bad", warn: "bg-warn-soft text-warn", neutral: "bg-surface-2 text-ink-2" };

/** Созданная задача по рекомендации: id задачи (ссылка «✓ Задача создана»). */
export type CreatedTasks = Record<number, string>;

/**
 * Карточка отчёта консультанта: «Главное» (вывод и полоса плана), плитки показателей, факты (таблица; на телефоне — список и «Таблица ›»),
 * ключевые отклонения, «Что сделать в первую очередь» с «+ Задача». Блок рисуется, только если он есть в ответе; Markdown-блоки — как есть.
 * Цвета изменений пришли с сервера (tone, severity): здесь только раскраска. Изменения — со знаком, а не только цветом.
 */
export function AnswerCard({
  answer,
  created,
  onCreateTask,
}: {
  answer: StructuredAnswer;
  created: CreatedTasks;
  /** null — нет права ставить задачи: кнопок «+ Задача» нет. */
  onCreateTask: ((item: RecommendationItem, index: number) => void) | null;
}) {
  return (
    <div className="space-y-[22px] rounded-[18px] border border-line bg-surface p-4 sm:p-6 max-lg:space-y-5 max-lg:border-0 max-lg:bg-transparent max-lg:p-0">
      {answer.blocks.map((b, i) => (
        <div key={i} className="ai-block-in" style={{ animationDelay: `${Math.min(i, 6) * 70}ms` }}>
          <Block block={b} created={created} onCreateTask={onCreateTask} />
        </div>
      ))}
    </div>
  );
}

function Block({ block, created, onCreateTask }: { block: AnswerBlock; created: CreatedTasks; onCreateTask: ((item: RecommendationItem, index: number) => void) | null }) {
  switch (block.type) {
    case "summary":
      return <Summary text={block.text} progress={block.progress} />;
    case "kpis":
      return <Kpis items={block.items} />;
    case "facts":
      return <Facts rows={block.rows} />;
    case "deviations":
      return <Deviations items={block.items} />;
    case "recommendations":
      return <Recommendations items={block.items} total={block.total} created={created} onCreateTask={onCreateTask} />;
    case "markdown":
      return (
        <div className="max-lg:rounded-2xl max-lg:border max-lg:border-line max-lg:bg-surface max-lg:p-4">
          <Markdown text={block.text} />
        </div>
      );
  }
}

function SectionTitle({ children, action }: { children: React.ReactNode; action?: React.ReactNode }) {
  return (
    <div className="mb-2.5 flex items-center justify-between gap-3">
      <h3 className="text-[15px] font-bold text-ink lg:text-base">{children}</h3>
      {action}
    </div>
  );
}

function Summary({ text, progress }: { text: string; progress: Extract<AnswerBlock, { type: "summary" }>["progress"] }) {
  const t = useT();
  const number = (n: number) => formatNumber(n);
  const pct = (n: number) => `${formatNumber(n, 1)}%`;
  const view = progress ? progressView(progress) : null;
  const bar = view && (
    <div className="relative h-2.5 overflow-hidden rounded-full bg-[var(--ai-track)] max-lg:h-2" role="img"
      aria-label={t("Факт {fact} от плана{forecast}", { fact: pct(view.fact), forecast: view.forecast !== null ? `, ${t("прогноз")} ${pct(view.forecast)}` : "" })}>
      {view.forecastWidth !== null && <div className="absolute inset-y-0 left-0 rounded-full bg-accent/25" style={{ width: `${view.forecastWidth}%` }} />}
      <div className="absolute inset-y-0 left-0 rounded-full bg-gradient-to-r from-[#3d7bff] to-[#6e9bff]" style={{ width: `${view.factWidth}%` }} />
    </div>
  );

  return (
    <section className="rounded-[14px] border border-[var(--ai-summary-line)] p-[18px] max-lg:rounded-[18px] max-lg:p-4" style={{ background: "var(--ai-summary-bg)" }}>
      <div className="flex items-center justify-between gap-2">
        <h3 className="flex items-center gap-2 text-[11px] font-bold uppercase tracking-[1px] text-link">
          <Target className="size-4" aria-hidden />
          {t("Главное")}
        </h3>
      </div>

      {/* Телефон: крупно процент плана и прогноз, полоса, факт «из» плана, затем вывод. */}
      {progress && view && (
        <div className="mt-3 lg:hidden">
          <div className="flex items-baseline gap-2.5">
            <span className="text-[34px] font-bold leading-none tabular-nums text-ink">{pct(view.fact)}</span>
            {view.forecast !== null && <span className="text-[13px] font-semibold text-warn">{t("прогноз {pct}", { pct: pct(view.forecast) })}</span>}
          </div>
          <div className="mt-3">{bar}</div>
          <div className="mt-2 flex justify-between gap-3 text-[12px] tabular-nums text-ink-2">
            <span>{number(progress.fact)} {progress.unit}</span>
            <span className="text-ink-3">{t("из {plan}", { plan: `${number(progress.plan)} ${progress.unit}` })}</span>
          </div>
        </div>
      )}

      <p className="mt-2.5 text-[15px] font-medium leading-[1.55] text-ink max-lg:mt-3 max-lg:text-sm">
        <InlineText text={text} />
      </p>

      {progress && view && (
        <div className="mt-4 hidden lg:block">
          <div className="mb-2 flex justify-between gap-3 text-[13px] tabular-nums">
            <span className="font-semibold text-ink">
              {t(progress.label)} {number(progress.fact)} {progress.unit} · {pct(view.fact)}
            </span>
            <span className="text-ink-3">{t("План {plan}", { plan: `${number(progress.plan)} ${progress.unit}` })}</span>
          </div>
          {bar}
          <div className="mt-2 flex flex-wrap gap-x-5 gap-y-1 text-[12px] text-ink-3">
            <span className="inline-flex items-center gap-1.5">
              <span className="h-1.5 w-2.5 rounded-full bg-accent" aria-hidden />
              {t("факт {pct}", { pct: pct(view.fact) })}
            </span>
            {view.forecast !== null && progress.forecast !== null && (
              <span className="inline-flex items-center gap-1.5">
                <span className="h-1.5 w-2.5 rounded-full bg-accent/30" aria-hidden />
                {progress.forecastLabel ? `${t(progress.forecastLabel)} — ` : `${t("прогноз")} — `}
                {pct(view.forecast)} ({number(progress.forecast)} {progress.unit})
              </span>
            )}
          </div>
        </div>
      )}
    </section>
  );
}

/** Строка ответа с полужирным и кодом (без блоков Markdown). */
function InlineText({ text }: { text: string }) {
  return (
    <>
      {text.split(/(\*\*[^*]+\*\*)/g).map((part, i) =>
        part.startsWith("**") && part.endsWith("**") ? (
          <strong key={i} className="font-semibold">
            {part.slice(2, -2)}
          </strong>
        ) : (
          part
        ),
      )}
    </>
  );
}

function Kpis({ items }: { items: KpiItem[] }) {
  return (
    <ul className={`grid grid-cols-2 gap-2.5 lg:gap-3 ${items.length >= 4 ? "lg:grid-cols-4" : items.length === 3 ? "lg:grid-cols-3" : "lg:grid-cols-2"}`}>
      {items.map((k, i) => (
        <li key={i} className="min-w-0 rounded-xl border border-line bg-ai-bg px-3.5 py-3 max-lg:rounded-[14px] max-lg:bg-surface">
          <div className="truncate text-[11px] text-ink-3">{k.label}</div>
          <div className="mt-1 truncate text-lg font-bold tabular-nums text-ink">{k.value}</div>
          {k.delta && <div className={`mt-1 line-clamp-2 text-[11px] font-semibold tabular-nums ${toneText[k.tone]}`}>{signed(k.delta)}</div>}
        </li>
      ))}
    </ul>
  );
}

function DeltaBadge({ value, tone }: { value: string; tone: Tone }) {
  return <span className={`inline-flex shrink-0 items-center rounded-md px-[7px] py-0.5 text-[12px] font-bold tabular-nums ${toneBadge[tone]}`}>{signed(value)}</span>;
}

function FactsTable({ rows }: { rows: FactRow[] }) {
  const t = useT();
  return (
    <div className="overflow-hidden rounded-xl border border-line">
      <table className="w-full border-collapse text-[13px]">
        <caption className="sr-only">{t("Факты")}</caption>
        <thead className="sr-only">
          <tr>
            <th scope="col">{t("Показатель")}</th>
            <th scope="col">{t("Значение и изменение")}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r, i) => (
            <tr key={i} className={`${i > 0 ? "border-t border-line" : ""} ${i % 2 === 0 ? "bg-[var(--ai-zebra)]" : ""}`}>
              <th scope="row" className="w-[260px] px-4 py-2.5 text-left align-top font-normal text-ink-3 max-lg:w-[42%] max-lg:px-3">
                {r.label}
              </th>
              <td className="px-4 py-2.5 align-top max-lg:px-3">
                <div className="flex flex-wrap items-center gap-x-2 gap-y-1.5">
                  {r.value && <span className="font-semibold tabular-nums text-ink">{r.value}</span>}
                  {r.deltas.map((d, j) => (
                    <span key={j} className="inline-flex items-center gap-1.5">
                      <DeltaBadge value={d.value} tone={d.tone} />
                      {d.caption && <span className="text-[12px] text-subtle">{d.caption}</span>}
                    </span>
                  ))}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Facts({ rows }: { rows: FactRow[] }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  // На телефоне в списке — строки с изменением (значения — в плитках); полная таблица — по «Таблица ›».
  const compact = rows.filter((r) => r.deltas.length > 0);
  const list = compact.length > 0 ? compact : rows;
  return (
    <section>
      <SectionTitle
        action={
          <button type="button" onClick={() => setOpen(true)} className="inline-flex items-center gap-0.5 text-[13px] font-medium text-link lg:hidden">
            {t("Таблица")}
            <ChevronRight className="size-4" aria-hidden />
          </button>
        }
      >
        {t("Факты")}
      </SectionTitle>
      <div className="hidden lg:block">
        <FactsTable rows={rows} />
      </div>
      <ul className="divide-y divide-line overflow-hidden rounded-2xl border border-line bg-surface lg:hidden">
        {list.map((r, i) => (
          <li key={i} className="flex items-center justify-between gap-3 px-4 py-3">
            <div className="min-w-0">
              <div className="text-[13px] font-medium text-ink">{r.label}</div>
              <div className="mt-0.5 truncate text-[11px] text-subtle">{r.deltas[0]?.caption ?? r.value}</div>
            </div>
            {r.deltas[0] ? <DeltaBadge value={r.deltas[0].value} tone={r.deltas[0].tone} /> : r.value && <span className="shrink-0 text-[13px] font-semibold tabular-nums text-ink">{r.value}</span>}
          </li>
        ))}
      </ul>
      <Sheet open={open} onClose={() => setOpen(false)} title={t("Факты")}>
        <FactsTable rows={rows} />
      </Sheet>
    </section>
  );
}

function Deviations({ items }: { items: Extract<AnswerBlock, { type: "deviations" }>["items"] }) {
  const t = useT();
  return (
    <section>
      <SectionTitle>{t("Ключевые отклонения")}</SectionTitle>
      <ul className="space-y-2">
        {items.map((d, i) => (
          <li key={i} className={`rounded-xl border-l-[3px] bg-ai-bg px-4 py-3 max-lg:bg-surface ${d.severity === "high" ? "border-bad" : "border-warn"}`}>
            <span className="sr-only">{d.severity === "high" ? t("Серьёзное отклонение") : t("Отклонение")}: </span>
            <div className="text-[13px] font-semibold text-ink">{d.title}</div>
            {d.text && <div className="mt-0.5 text-[13px] text-ink-3">{d.text}</div>}
          </li>
        ))}
      </ul>
    </section>
  );
}

function Recommendations({
  items,
  total,
  created,
  onCreateTask,
}: {
  items: RecommendationItem[];
  total: number;
  created: CreatedTasks;
  onCreateTask: ((item: RecommendationItem, index: number) => void) | null;
}) {
  const t = useT();
  const [all, setAll] = useState(false);
  const more = Math.max(0, Math.max(total, items.length) - shownRecommendations);
  const shown = all ? items : items.slice(0, shownRecommendations);
  return (
    <section>
      <SectionTitle
        action={
          more > 0 && !all ? (
            <button type="button" onClick={() => setAll(true)} className="inline-flex items-center gap-1 text-[13px] font-semibold text-link">
              {t("ещё {n}", { n: more })}
              <ArrowRight className="size-3.5 max-lg:hidden" aria-hidden />
              <ChevronRight className="size-4 lg:hidden" aria-hidden />
            </button>
          ) : null
        }
      >
        <span className="max-lg:hidden">{t("Что сделать в первую очередь")}</span>
        <span className="lg:hidden">{t("Что сделать")}</span>
      </SectionTitle>
      <ol className="space-y-3.5 max-lg:space-y-0 max-lg:divide-y max-lg:divide-line max-lg:overflow-hidden max-lg:rounded-2xl max-lg:border max-lg:border-line max-lg:bg-surface">
        {shown.map((r, i) => {
          const taskId = created[i];
          const owner = r.owner ?? null;
          return (
            <li key={i} className="flex items-start gap-3 max-lg:items-center max-lg:px-4 max-lg:py-3">
              <span className="grid size-6 shrink-0 place-items-center rounded-full bg-accent-soft text-[12px] font-bold text-link" aria-hidden>
                {i + 1}
              </span>
              <div className="min-w-0 flex-1">
                <div className="text-sm font-semibold text-ink max-lg:text-[13px]">{r.title}</div>
                {r.text && <div className="mt-0.5 text-[13px] text-ink-3 max-lg:hidden">{r.text}</div>}
                {owner && <div className="mt-0.5 truncate text-[11px] text-ink-3 lg:hidden">{owner}</div>}
              </div>
              {owner && <span className="hidden shrink-0 rounded-md bg-surface-2 px-2 py-1 text-[11px] text-ink-2 lg:inline">{owner}</span>}
              {taskId ? (
                <Link href={`/tasks/task/${taskId}`} className="inline-flex shrink-0 items-center gap-1 rounded-lg px-2 py-1 text-[12px] font-semibold text-ok hover:underline">
                  <Check className="size-3.5" aria-hidden />
                  <span className="max-lg:sr-only">{t("Задача создана")}</span>
                </Link>
              ) : onCreateTask === null ? null : (
                <>
                  <button
                    type="button"
                    onClick={() => onCreateTask(r, i)}
                    className="hidden shrink-0 items-center gap-1 rounded-lg border border-line-strong px-2.5 py-1 text-[12px] font-medium text-ink-2 hover:border-accent hover:text-ink lg:inline-flex"
                  >
                    <Plus className="size-3.5" aria-hidden />
                    {t("Задача")}
                  </button>
                  <button
                    type="button"
                    onClick={() => onCreateTask(r, i)}
                    aria-label={t("Создать задачу: {title}", { title: r.title })}
                    className="grid size-9 shrink-0 place-items-center rounded-full border border-line bg-surface-2 text-ink-2 lg:hidden"
                  >
                    <Plus className="size-4" aria-hidden />
                  </button>
                </>
              )}
            </li>
          );
        })}
      </ol>
    </section>
  );
}
