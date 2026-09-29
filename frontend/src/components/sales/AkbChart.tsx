"use client";

import { useEffect, useRef, useState } from "react";
import { Download } from "lucide-react";
import { Note, Section } from "./bits";
import { CopyButton } from "./DataTable";
import { monthLabel, monthShort, num, tsvValue } from "@/lib/sales/format";
import type { AkbByMonth } from "@/lib/sales/types";

/** Цвета категорий — различимы и в светлой, и в тёмной теме. Итог рисуется цветом текста. */
const PALETTE = ["#3b82f6", "#f97316", "#10b981", "#eab308", "#ec4899", "#8b5cf6", "#ef4444", "#06b6d4", "#84cc16", "#a16207", "#64748b", "#14b8a6"];
const HEIGHT = 320;
const TOTAL = "total";

type Series = { id: string; name: string; values: (number | null)[]; color: string; total: boolean };

/** Шкала «15 000 с шагом 2 500»: шаг 1 / 2 / 2,5 / 5 × 10ⁿ, около шести делений. */
function niceScale(max: number): { top: number; step: number } {
  if (max <= 0) return { top: 10, step: 2 };
  const rough = max / 6;
  const pow = 10 ** Math.floor(Math.log10(rough));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * pow).find((s) => s >= rough) ?? 10 * pow;
  return { top: Math.ceil(max / step) * step, step };
}

/** Путь линии с разрывами там, где за месяц нет данных. */
function linePath(values: (number | null)[], x: (i: number) => number, y: (v: number) => number): string {
  let d = "";
  let open = false;
  values.forEach((v, i) => {
    if (v == null) {
      open = false;
      return;
    }
    d += `${open ? "L" : "M"}${x(i).toFixed(1)},${y(v).toFixed(1)} `;
    open = true;
  });
  return d.trim();
}

function tableRows(data: AkbByMonth, series: Series[]): (string | number | null)[][] {
  return [
    ["Месяц", ...series.map((s) => s.name)],
    ...data.months.map((m, i) => [monthLabel(data.year, m), ...series.map((s) => s.values[i])]),
  ];
}

/** CSV для Excel: BOM, чтобы кириллица открылась без настроек, и «;» — разделитель русского Excel. */
function downloadCsv(name: string, rows: (string | number | null)[][]) {
  const cell = (v: string | number | null) => {
    const s = tsvValue(v);
    return /[;"\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const csv = "﻿" + rows.map((r) => r.map(cell).join(";")).join("\r\n");
  const url = URL.createObjectURL(new Blob([csv], { type: "text/csv;charset=utf-8" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = `${name}.csv`;
  link.click();
  URL.revokeObjectURL(url);
}

/** АКБ по месяцам: итог и линии категорий. Клик по линии или легенде — подсветить, ещё раз — снять. */
export function AkbChart({ data }: { data: AkbByMonth }) {
  const box = useRef<HTMLDivElement>(null);
  const [width, setWidth] = useState(0);
  const [active, setActive] = useState<string | null>(null);
  const [hover, setHover] = useState<number | null>(null);

  useEffect(() => {
    const el = box.current;
    if (!el) return;
    const observer = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width));
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const series: Series[] = [
    { id: TOTAL, name: "Итого", values: data.total, color: "var(--color-ink)", total: true },
    ...data.categories.map((c, i) => ({ ...c, color: PALETTE[i % PALETTE.length], total: false })),
  ];
  const rows = tableRows(data, series);
  const toggle = (id: string) => setActive((a) => (a === id ? null : id));

  const narrow = width < 560;
  const pad = { top: 28, right: narrow ? 40 : 64, bottom: 30, left: narrow ? 44 : 60 };
  const plotW = Math.max(1, width - pad.left - pad.right);
  const plotH = HEIGHT - pad.top - pad.bottom;
  const n = data.months.length;
  const { top, step } = niceScale(Math.max(0, ...series.flatMap((s) => s.values.map((v) => v ?? 0))));
  const x = (i: number) => pad.left + (n <= 1 ? plotW / 2 : (i * plotW) / (n - 1));
  const y = (v: number) => pad.top + plotH * (1 - v / top);
  const ticks = Array.from({ length: Math.round(top / step) + 1 }, (_, i) => i * step);
  const lastIndex = n - 1;

  // Подписи первой и последней точки — у итога и у подсвеченной категории.
  const labelled = series.filter((s) => s.total || s.id === active);
  const hovered = hover == null ? [] : [...series].filter((s) => s.values[hover] != null).sort((a, b) => (b.total ? 1 : 0) - (a.total ? 1 : 0) || (b.values[hover] ?? 0) - (a.values[hover] ?? 0));

  return (
    <Section
      title="АКБ по месяцам"
      hint="уникальные точки, купившие за месяц · клик по линии — подсветить, ещё раз — снять"
      actions={
        <>
          <CopyButton onCopy={() => rows.map((r) => r.map(tsvValue).join("\t")).join("\n")} />
          <button
            type="button"
            onClick={() => downloadCsv(`АКБ по месяцам ${data.year}`, rows)}
            title="Скачать таблицу для Excel (CSV)"
            className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-xs font-medium text-ink hover:bg-muted"
          >
            <Download className="size-3.5" />
            Excel
          </button>
        </>
      }
    >
      <div ref={box} className="relative w-full" style={{ height: HEIGHT }} onMouseLeave={() => setHover(null)}>
        {width > 0 && n > 0 && (
          <svg
            width={width}
            height={HEIGHT}
            className="block select-none"
            role="img"
            aria-label="АКБ по месяцам"
            onMouseMove={(e) => {
              const left = e.currentTarget.getBoundingClientRect().left;
              const i = n <= 1 ? 0 : Math.round(((e.clientX - left - pad.left) / plotW) * (n - 1));
              setHover(Math.min(lastIndex, Math.max(0, i)));
            }}
          >
            {ticks.map((t) => (
              <g key={t}>
                <line x1={pad.left} x2={width - pad.right} y1={y(t)} y2={y(t)} stroke="var(--color-line)" />
                <text x={pad.left - 8} y={y(t)} dy="0.32em" textAnchor="end" fontSize={11} fill="var(--color-ink-3)" className="tabular-nums">
                  {num(t)}
                </text>
              </g>
            ))}
            {data.months.map((m, i) => (
              <text key={m} x={x(i)} y={HEIGHT - 8} textAnchor="middle" fontSize={11} fill={i === hover ? "var(--color-ink)" : "var(--color-ink-3)"}>
                {monthShort(m)}
              </text>
            ))}
            {hover != null && <line x1={x(hover)} x2={x(hover)} y1={pad.top} y2={pad.top + plotH} stroke="var(--color-ink-3)" strokeDasharray="3 3" />}

            {/* Категории под итогом; подсвеченная — поверх остальных. */}
            {[...series.filter((s) => !s.total && s.id !== active), ...series.filter((s) => !s.total && s.id === active), ...series.filter((s) => s.total)].map((s) => {
              const dim = active != null && active !== s.id;
              const path = linePath(s.values, x, y);
              const strong = s.total || s.id === active;
              return (
                <g key={s.id} opacity={dim ? 0.15 : 1} className="transition-opacity">
                  <path d={path} fill="none" stroke={s.color} strokeWidth={s.total ? 2.5 : s.id === active ? 3 : 2} strokeLinejoin="round" strokeLinecap="round" />
                  {strong &&
                    s.values.map((v, i) =>
                      v == null ? null : (
                        <circle
                          key={i}
                          cx={x(i)}
                          cy={y(v)}
                          r={s.total ? 5 : 4}
                          fill={i === lastIndex && data.lastPartial ? "var(--color-surface)" : s.color}
                          stroke={s.color}
                          strokeWidth={2}
                        />
                      ),
                    )}
                  {/* Широкая невидимая линия — чтобы по ней было легко попасть кликом. */}
                  <path d={path} fill="none" stroke="transparent" strokeWidth={14} className="cursor-pointer" onClick={() => toggle(s.id)}>
                    <title>{s.name}</title>
                  </path>
                </g>
              );
            })}

            {labelled.map((s) => {
              const first = s.values.findIndex((v) => v != null);
              const last = s.values.findLastIndex((v) => v != null);
              if (first < 0) return null;
              return [first, ...(last !== first ? [last] : [])].map((i) => (
                <text
                  key={`${s.id}-${i}`}
                  x={x(i)}
                  y={y(s.values[i]!) - 12}
                  textAnchor={i === first && first !== last ? "start" : "end"}
                  fontSize={s.total ? 14 : 12}
                  fontWeight={600}
                  fill={s.color}
                  opacity={active != null && active !== s.id ? 0.3 : 1}
                  className="tabular-nums"
                  style={{ paintOrder: "stroke", stroke: "var(--color-surface)", strokeWidth: 4 }}
                >
                  {num(s.values[i])}
                </text>
              ));
            })}
          </svg>
        )}

        {hover != null && hovered.length > 0 && (
          <div
            className="pointer-events-none absolute top-2 z-10 min-w-[180px] rounded-lg border border-line bg-surface px-3 py-2 text-xs shadow-lg"
            style={x(hover) > width / 2 ? { left: x(hover) - 12, transform: "translateX(-100%)" } : { left: x(hover) + 12 }}
          >
            <div className="mb-1 font-semibold text-ink">
              {monthLabel(data.year, data.months[hover])}
              {hover === lastIndex && data.lastPartial && <span className="font-normal text-ink-3"> · месяц идёт</span>}
            </div>
            {hovered.map((s) => (
              <div key={s.id} className={`flex items-center justify-between gap-4 ${s.total || s.id === active ? "font-semibold text-ink" : "text-ink-2"}`}>
                <span className="flex items-center gap-1.5">
                  <span className="size-2 rounded-full" style={{ background: s.color }} />
                  {s.name}
                </span>
                <span className="tabular-nums">{num(s.values[hover])}</span>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="mt-3 flex flex-wrap gap-1.5">
        {series.map((s) => (
          <button
            key={s.id}
            type="button"
            onClick={() => toggle(s.id)}
            aria-pressed={active === s.id}
            className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs transition-colors ${
              active === s.id ? "border-accent bg-accent-soft font-semibold text-ink" : "border-line bg-surface text-ink-2 hover:bg-muted"
            } ${active != null && active !== s.id ? "opacity-50" : ""}`}
          >
            <span className={`rounded-full ${s.total ? "h-0.5 w-3.5" : "size-2"}`} style={{ background: s.color }} />
            {s.name}
          </button>
        ))}
      </div>

      <Note>
        АКБ — уникальные ТТ, у которых чистая покупка (продажи минус возвраты) за месяц больше нуля. Итог — не сумма категорий: одна ТТ
        покупает несколько категорий. {data.lastPartial && "Пустая точка — месяц ещё идёт. "}Наведите на график — значения за месяц.
      </Note>
    </Section>
  );
}
