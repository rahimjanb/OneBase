"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { ChevronDown, Database, FlaskConical, Loader2, MessageSquareText, PlayCircle, Sigma, Timer } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { dateTime } from "@/lib/analytik";
import {
  cellText,
  duration,
  evidenceLevel,
  investigationStatus,
  matrixCell,
  operationStatus,
  researchKinds,
  researchSections,
  type InvestigationRow,
  type ResearchDetail,
  type ResearchMatrix,
  type ResearchOperation,
  type ResearchTable,
} from "@/lib/intelligence";
import { Card } from "../ui";

function Pill({ tone, children }: { tone: string; children: React.ReactNode }) {
  return <span className={`inline-flex shrink-0 items-center rounded-full px-2 py-0.5 text-[11px] font-medium ${tone}`}>{children}</span>;
}

function Level({ level }: { level: string }) {
  const t = useT();
  const l = evidenceLevel[level] ?? evidenceLevel.UNVERIFIED;
  return <Pill tone={l.tone}>{t(l.label)}</Pill>;
}

const hypothesisTone: Record<string, { label: string; tone: string }> = {
  confirmed: { label: "подтверждена", tone: "bg-ok/15 text-ok" },
  partially_confirmed: { label: "частично", tone: "bg-warn/15 text-warn" },
  rejected: { label: "не подтверждена", tone: "bg-muted text-ink-3" },
  inconclusive: { label: "не ясно", tone: "bg-muted text-ink-3" },
};

const day = (iso?: string | null) => (iso ? iso.split("-").reverse().join(".") : "—");

/**
 * Исследование-шаблон (Research Overview): статус, период и сравнение, область доступа, операции и время; выводы по разделам ТЗ с уровнем
 * доказательности; гипотезы; матрицы корреляций; таблицы; операции (Research Details) — метод, параметры, статус, причина сбоя; ограничения и источники.
 */
export function ResearchView({ row, research }: { row: InvestigationRow; research: ResearchDetail }) {
  const t = useT();
  const st = investigationStatus[row.status] ?? investigationStatus.planned;
  const kind = researchKinds.find((k) => k.kind === research.kind);
  const tm = research.timings;
  const failed = research.operations.filter((o) => !["SUCCESS", "PARTIAL_RESULT"].includes(o.status));
  return (
    <div className="grid gap-4">
      <Card>
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="text-xs text-ink-3">
              {dateTime(row.startedAt)}
              {row.author ? ` · ${row.author}` : ""} · {t("исследование без модели")}
            </p>
            <h2 className="mt-1 text-base font-semibold text-ink">{t(kind?.title ?? research.kind)}</h2>
            <p className="mt-1 text-sm text-ink-2">{research.plan.objective}</p>
            <p className="mt-1 text-xs text-ink-3">
              {t("Период")}: {day(research.plan.from)} — {day(research.plan.to)}
              {research.plan.compareFrom ? ` · ${t("сравнение")}: ${day(research.plan.compareFrom)} — ${day(research.plan.compareTo)}` : ""} · {t("область")}: {research.plan.accessScope}
            </p>
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
        <dl className="mt-4 grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
          {[
            { icon: Database, label: "Операций", value: `${research.operations.length}${failed.length ? ` (${t("без результата")} ${failed.length})` : ""}` },
            { icon: Sigma, label: "Срезов / расчётов", value: tm ? `${tm.queries} / ${tm.computations}` : "—" },
            { icon: FlaskConical, label: "Из кэша / повторов", value: tm ? `${tm.cacheHits} / ${tm.retries}` : "—" },
            { icon: Timer, label: "Время (срезы · расчёты)", value: tm ? `${duration(tm.totalMs)} (${duration(tm.sqlMs)} · ${duration(tm.statisticsMs)})` : duration(row.durationMs) },
          ].map((m) => (
            <div key={m.label} className="min-w-0 rounded-xl bg-muted/60 px-3 py-2">
              <dt className="flex items-center gap-1 text-[11px] text-ink-4">
                <m.icon className="size-3.5" />
                {t(m.label)}
              </dt>
              <dd className="mt-0.5 truncate font-medium tabular-nums text-ink">{m.value}</dd>
            </div>
          ))}
        </dl>
      </Card>

      {researchSections.filter((s) => s.section !== "limitations").map((s) => {
        const items = research.findings.filter((f) => f.section === s.section);
        const hypotheses = s.section === "causes" ? research.hypotheses : [];
        if (!items.length && !hypotheses.length) return null;
        return (
          <Card key={s.section} title={s.title}>
            <ul className="grid gap-2">
              {items.map((f, i) => (
                <li key={i} className="flex flex-col items-start gap-1 text-sm sm:flex-row sm:gap-2">
                  <Level level={f.level} />
                  <span className="min-w-0 text-ink-2">{f.text}</span>
                </li>
              ))}
            </ul>
            {hypotheses.length > 0 && (
              <div className="mt-3 overflow-x-auto">
                <table className="w-full min-w-[520px] text-sm">
                  <thead className="text-left text-[11px] text-ink-4">
                    <tr>
                      <th className="py-1 font-medium">{t("Гипотеза")}</th>
                      <th className="py-1 font-medium">{t("Проверка")}</th>
                      <th className="py-1 font-medium">{t("Основание")}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line">
                    {hypotheses.map((h) => {
                      const ht = hypothesisTone[h.status] ?? hypothesisTone.inconclusive;
                      return (
                        <tr key={h.id}>
                          <td className="py-1.5 pr-2 text-ink-2">{h.statement}</td>
                          <td className="py-1.5 pr-2"><Pill tone={ht.tone}>{t(ht.label)}</Pill></td>
                          <td className="py-1.5 text-ink-3">{h.basis}</td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        );
      })}

      {research.matrices.map((m) => <MatrixCard key={m.id} matrix={m} />)}
      {research.tables.filter((tb) => tb.rows.length > 0).map((tb) => <TableCard key={tb.id} table={tb} />)}

      <Card title="Операции исследования" subtitle="План: срезы и расчёты, которые выполнил сервер — параллельно, с кэшем одинаковых срезов">
        <ul className="grid gap-2">
          {research.operations.map((o) => <Operation key={o.id} op={o} />)}
        </ul>
      </Card>

      <Card title="Ограничения и источники">
        <ul className="grid gap-1.5 text-sm text-ink-2">
          {research.findings.filter((f) => f.section === "limitations").map((f, i) => <li key={`f${i}`}>— {f.text}</li>)}
          {research.limitations.map((l, i) => <li key={`l${i}`}>— {l}</li>)}
        </ul>
        {research.sources.length > 0 && (
          <ul className="mt-3 grid gap-1 text-xs text-ink-3">
            {research.sources.map((s, i) => (
              <li key={i}>
                {s.title}
                {s.dataThrough ? ` · ${t("данные по")} ${day(s.dataThrough)}` : ""}
                {s.syncedAt ? ` · ${t("загружено")} ${dateTime(s.syncedAt)}` : ""}
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}

function MatrixCard({ matrix }: { matrix: ResearchMatrix }) {
  const t = useT();
  const [hover, setHover] = useState<{ i: number; j: number } | null>(null);
  const short = (s: string) => (s.length > 18 ? `${s.slice(0, 17)}…` : s);
  const h = hover ? { a: matrix.titles[hover.i], b: matrix.titles[hover.j], r: matrix.r[hover.i]?.[hover.j], q: matrix.q[hover.i]?.[hover.j] } : null;
  return (
    <Card title={`${t("Матрица корреляций")}: ${t(matrix.title)}`} subtitle={`${t("наблюдений")}: ${matrix.n} · ${t("насыщенный цвет — связь подтверждена (q < 0,05)")}`}>
      <p className="mb-2 min-h-5 text-xs text-ink-3">
        {h && h.r != null ? `${h.a} × ${h.b}: r = ${h.r.toFixed(2).replace(".", ",")}${h.q != null ? `, q = ${h.q < 0.001 ? "< 0,001" : h.q.toFixed(3).replace(".", ",")}` : ""}` : t("Наведите на ячейку")}
      </p>
      <div className="overflow-x-auto">
        <table className="border-separate border-spacing-0.5 text-[11px]">
          <thead>
            <tr>
              <th />
              {matrix.titles.map((title, j) => (
                <th key={j} className="relative h-24 w-10 min-w-10 max-w-10 align-bottom font-normal text-ink-3">
                  {/* Подпись вне потока: повёрнутый текст не раздвигает колонку — все колонки одной ширины. */}
                  <span className="absolute bottom-1 left-4 origin-bottom-left -rotate-60 whitespace-nowrap" title={title}>{short(title)}</span>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {matrix.titles.map((title, i) => (
              <tr key={i}>
                <th className="max-w-40 truncate pr-2 text-left font-normal text-ink-3" title={title}>{short(title)}</th>
                {matrix.titles.map((_, j) => {
                  const r = matrix.r[i]?.[j];
                  const cell = i === j ? { background: "var(--color-muted)", strong: false } : matrixCell(r, matrix.q[i]?.[j]);
                  return (
                    <td
                      key={j}
                      onMouseEnter={() => setHover({ i, j })}
                      onFocus={() => setHover({ i, j })}
                      tabIndex={0}
                      className={`h-8 w-10 min-w-10 max-w-10 rounded text-center tabular-nums ${cell.strong ? "font-semibold text-ink" : "text-ink-3"}`}
                      style={{ background: cell.background }}
                    >
                      {i === j || r == null ? "" : r.toFixed(2).replace("0.", ".").replace("-.", "−.")}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

function TableCard({ table }: { table: ResearchTable }) {
  const t = useT();
  const [all, setAll] = useState(false);
  const rows = all ? table.rows : table.rows.slice(0, 15);
  return (
    <Card title={table.title}>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[560px] text-sm">
          <thead className="text-left text-[11px] text-ink-4">
            <tr>
              {table.columns.map((c, i) => (
                <th key={c.code} className={`py-1 pr-2 font-medium ${i > 0 ? "text-right" : ""}`}>{t(c.title)}</th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {rows.map((r, ri) => (
              <tr key={ri}>
                {r.map((v, ci) => (
                  <td key={ci} className={`py-1.5 pr-2 ${ci > 0 && typeof v === "number" ? "text-right tabular-nums" : "text-ink-2"}`}>
                    {cellText(v, table.columns[ci]?.unit)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {table.rows.length > 15 && (
        <button type="button" onClick={() => setAll(!all)} className="mt-2 text-xs font-medium text-accent-strong hover:underline">
          {all ? t("Свернуть") : `${t("Показать все")} (${table.rows.length})`}
        </button>
      )}
    </Card>
  );
}

function Operation({ op }: { op: ResearchOperation }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const st = operationStatus[op.status] ?? operationStatus.PENDING;
  const what = op.compute
    ? `${t("метод")} ${op.compute.method}${op.compute.data?.metrics?.length ? ` · ${op.compute.data.metrics.join(", ")}` : ""}${op.compute.data?.dimension ? ` · ${t("по")} ${op.compute.data.dimension}` : ""}${op.compute.data?.grain ? ` · ${op.compute.data.grain}` : ""}`
    : op.query
      ? `${t("срез")} ${(op.query.metrics ?? []).join(", ")}${op.query.dimensions?.length ? ` × ${op.query.dimensions.join(", ")}` : ""}${op.query.grain ? ` · ${op.query.grain}` : ""}`
      : op.source;
  return (
    <li className="rounded-xl border border-line px-3 py-2">
      <button type="button" onClick={() => setOpen(!open)} className="flex w-full flex-col gap-1 text-left sm:flex-row sm:items-center sm:gap-3">
        <span className="flex min-w-0 flex-1 items-center gap-2">
          <Pill tone={st.tone}>{t(st.label)}</Pill>
          <span className="truncate text-sm text-ink">{op.title}</span>
        </span>
        <span className="flex items-center gap-2 text-xs text-ink-3">
          {op.fromCache && <Pill tone="bg-accent-soft text-accent-strong">{t("из кэша")}</Pill>}
          {op.attempts > 1 && <Pill tone="bg-warn/15 text-warn">{t("повтор")}</Pill>}
          {op.optional && <span>{t("необязательная")}</span>}
          <span className="tabular-nums">{duration(op.elapsedMs)}</span>
          <ChevronDown className={`size-4 transition ${open ? "rotate-180" : ""}`} />
        </span>
      </button>
      {(open || op.errorReason) && (
        <div className="mt-2 grid gap-1 text-xs text-ink-3">
          {op.errorReason && <p className="text-bad">{op.errorReason}</p>}
          {open && <p>{what}</p>}
          {open && op.compute?.params && Object.keys(op.compute.params).length > 0 && <p>{t("Параметры")}: {JSON.stringify(op.compute.params)}</p>}
          {open && op.summary && <p className="text-ink-2">{op.summary}</p>}
          {open && op.note && <p>{op.note}</p>}
          {open && op.dependsOn.length > 0 && <p>{t("После")}: {op.dependsOn.join(", ")}</p>}
        </div>
      )}
    </li>
  );
}

/** Запуск исследования без модели (бесплатно): шаблон и период — сервер строит план, считает и сохраняет; открывается страница исследования. */
export function ResearchLauncher() {
  const t = useT();
  const router = useRouter();
  const [kind, setKind] = useState(researchKinds[0].kind);
  const [days, setDays] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const selected = researchKinds.find((k) => k.kind === kind)!;
  const run = async () => {
    setBusy(true);
    setError(null);
    try {
      const n = Number(days);
      const r = await bff<{ result: { id: string } }>("analytik/research", {
        method: "POST",
        body: JSON.stringify({ kind, question: t(selected.title), days: Number.isFinite(n) && n >= 7 ? Math.round(n) : null }),
      });
      router.push(`/analytik/investigations/${r.result.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setBusy(false);
    }
  };
  return (
    <Card title="Исследование без модели" subtitle="Сервер сам строит план, параллельно считает срезы и статистику и сохраняет выводы с уровнем доказательности — без платных вызовов модели">
      <div className="grid gap-2 sm:grid-cols-[minmax(0,1fr)_auto_auto] sm:items-end">
        <label className="grid gap-1 text-xs text-ink-3">
          {t("Шаблон")}
          <select value={kind} onChange={(e) => setKind(e.target.value)} className="h-9 rounded-lg border border-line bg-surface px-2 text-sm text-ink">
            {researchKinds.map((k) => (
              <option key={k.kind} value={k.kind}>{t(k.title)}</option>
            ))}
          </select>
        </label>
        <label className="grid gap-1 text-xs text-ink-3">
          {t("Дней (необязательно)")}
          <input value={days} onChange={(e) => setDays(e.target.value)} inputMode="numeric" placeholder="90" className="h-9 w-full rounded-lg border border-line bg-surface px-2 text-sm sm:w-28" />
        </label>
        <button type="button" disabled={busy} onClick={run} className="inline-flex h-9 items-center justify-center gap-1.5 rounded-lg bg-accent px-3 text-sm font-medium text-white hover:bg-accent-strong disabled:opacity-60">
          {busy ? <Loader2 className="size-4 animate-spin" /> : <PlayCircle className="size-4" />}
          {busy ? t("Исследую…") : t("Запустить")}
        </button>
      </div>
      <p className="mt-2 text-xs text-ink-3">{t(selected.hint)}</p>
      {error && <p className="mt-2 text-sm text-bad">{error}</p>}
    </Card>
  );
}
