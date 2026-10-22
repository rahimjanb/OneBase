"use client";

import { useCallback, useId, useState } from "react";
import { Download } from "lucide-react";
import { useT } from "@/i18n/client";
import type { T } from "@/i18n/core";
import { Note, Section } from "./bits";
import { CopyButton } from "./DataTable";
import { monthLabel, monthShort, num, tsvValue } from "@/lib/sales/format";
import type { AkbByMonth } from "@/lib/sales/types";

/** Цвета категорий — различимы и в светлой, и в тёмной теме. Итог рисуется цветом текста. */
const PALETTE = ["#3b82f6", "#f97316", "#10b981", "#eab308", "#ec4899", "#8b5cf6", "#ef4444", "#06b6d4", "#84cc16", "#a16207", "#64748b", "#14b8a6"];
const HEIGHT = 320;
const TOTAL = "total";

/** average — «Сред/мес» с сервера (null — считать не по чему). */
type Series = { id: string; name: string; values: (number | null)[]; average: number | null; color: string; total: boolean };

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

function tableRows(data: AkbByMonth, series: Series[], t: T): (string | number | null)[][] {
  return [
    [t("Месяц"), ...series.map((s) => s.name)],
    ...data.months.map((m, i) => [monthLabel(data.year, m, t), ...series.map((s) => s.values[i])]),
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

type Format = (value: number | null | undefined) => string;

/** Таблица под графиком: категория × месяц, «Сред/мес» (с сервера); внизу — итог («Всего точек»). Клик по строке — подсветить линию. */
function AkbTable({
  data,
  series,
  active,
  onToggle,
  head,
  totalLabel,
  format,
}: {
  data: AkbByMonth;
  series: Series[];
  active: string | null;
  onToggle: (id: string) => void;
  head: string;
  totalLabel: string;
  format: Format;
}) {
  const t = useT();
  const last = data.months.length - 1;
  const th = "px-3 py-2 text-right text-[11px] font-semibold uppercase tracking-wide text-ink-3";
  const cell = (v: number | null) => (v == null ? <span className="text-ink-3">—</span> : v === 0 ? <span className="text-ink-3">0</span> : format(v));
  const total = series.find((s) => s.total);
  const rows = series.filter((s) => !s.total);

  return (
    <div className="-mx-4 mt-4 overflow-x-auto px-4 sm:-mx-5 sm:px-5">
      <table className="w-full min-w-max border-collapse text-sm">
        <thead>
          <tr className="border-b border-line">
            <th className={`${th} text-left`}>{head}</th>
            {data.months.map((m, i) => (
              <th key={m} className={th}>
                {monthShort(m, t)}
                {i === last && data.lastPartial ? t(" (идёт)") : ""}
              </th>
            ))}
            <th className={th}>{t("Сред/мес")}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((s) => (
            <tr
              key={s.id}
              onClick={() => onToggle(s.id)}
              className={`cursor-pointer border-b border-line hover:bg-muted ${active === s.id ? "bg-accent-soft" : ""}`}
            >
              <td className="px-3 py-2 font-medium text-ink">
                <span className="inline-flex items-center gap-2">
                  <span className="size-2.5 rounded-sm" style={{ background: s.color }} />
                  {s.name}
                </span>
              </td>
              {s.values.map((v, i) => (
                <td key={i} className="px-3 py-2 text-right tabular-nums text-ink">
                  {cell(v)}
                </td>
              ))}
              <td className="px-3 py-2 text-right font-semibold tabular-nums text-ink">{format(s.average)}</td>
            </tr>
          ))}
        </tbody>
        {total && (
          <tfoot>
            <tr className="bg-muted font-semibold text-ink">
              <td className="px-3 py-2">{totalLabel}</td>
              {total.values.map((v, i) => (
                <td key={i} className="px-3 py-2 text-right tabular-nums">
                  {cell(v)}
                </td>
              ))}
              <td className="px-3 py-2 text-right tabular-nums">{format(total.average)}</td>
            </tr>
          </tfoot>
        )}
      </table>
    </div>
  );
}

/**
 * АКБ по месяцам: итог и линии категорий. Клик по линии или легенде — подсветить, ещё раз — снять.
 * Подписи, формат чисел и кнопки — для другой меры в той же форме (первичка: клиенты / кг / сум); по умолчанию — АКБ точек вторички.
 * «Сред/мес» считает сервер; averageNote — по каким месяцам (у вторички — по всем месяцам с данными, у первички — только по закрытым).
 */
export function AkbChart({
  data,
  title: titleText = "АКБ по месяцам",
  hint: hintText = "уникальные точки, купившие за месяц",
  head: headText = "Категория — АКБ, точек",
  totalLabel: totalText = "Всего точек",
  format = num,
  actions,
  note,
  averageNote: averageText = "«Сред/мес» — среднее за месяц по месяцам с данными (идущий месяц тоже), считает сервер.",
}: {
  data: AkbByMonth;
  title?: string;
  hint?: string;
  head?: string;
  totalLabel?: string;
  format?: Format;
  actions?: React.ReactNode;
  note?: React.ReactNode;
  averageNote?: string;
}) {
  // Подписи приходят русскими (константы вызывающих) — переводятся здесь; уже переведённые не меняются.
  const t = useT();
  const title = t(titleText);
  const hint = t(hintText);
  const head = t(headText);
  const totalLabel = t(totalText);
  const averageNote = t(averageText);
  const [width, setWidth] = useState(0);
  const [active, setActive] = useState<string | null>(null);
  const [hover, setHover] = useState<number | null>(null);
  // Столбцы — итог или выбранная категория по месяцам (по умолчанию); линии — итог и все категории сразу.
  const [mode, setMode] = useState<"bars" | "lines">("bars");
  const gradient = useId();

  // Ширину меряем, когда поле графика появляется в DOM: в свёрнутой секции его нет,
  // и замер «один раз при загрузке страницы» оставил бы график пустым после раскрытия.
  const box = useCallback((el: HTMLDivElement | null) => {
    if (!el) return;
    const observer = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width));
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const series: Series[] = [
    { id: TOTAL, name: t("Итого"), values: data.total, average: data.average ?? null, color: "var(--color-ink)", total: true },
    ...data.categories.map((c, i) => ({ id: c.id, name: c.name, values: c.values, average: c.average ?? null, color: PALETTE[i % PALETTE.length], total: false })),
  ];
  const rows = tableRows(data, series, t);
  const toggle = (id: string) => setActive((a) => (a === id ? null : id));

  const narrow = width < 560;
  const pad = { top: 28, right: narrow ? 40 : 64, bottom: 30, left: narrow ? 44 : 60 };
  const plotW = Math.max(1, width - pad.left - pad.right);
  const plotH = HEIGHT - pad.top - pad.bottom;
  const n = data.months.length;
  const bars = mode === "bars";
  // В столбцах — одна мера: итог или выбранная категория; шкала — по ней.
  const shown = series.find((s) => s.id === (active ?? TOTAL)) ?? series[0];
  const { top, step } = niceScale(Math.max(0, ...(bars ? [shown] : series).flatMap((s) => s.values.map((v) => v ?? 0))));
  const band = plotW / Math.max(1, n);
  const barWidth = Math.min(44, band * 0.56);
  const x = bars ? (i: number) => pad.left + band * (i + 0.5) : (i: number) => pad.left + (n <= 1 ? plotW / 2 : (i * plotW) / (n - 1));
  const y = (v: number) => pad.top + plotH * (1 - v / top);
  const base = pad.top + plotH;
  const ticks = Array.from({ length: Math.round(top / step) + 1 }, (_, i) => i * step);
  const lastIndex = n - 1;

  // Подписи первой и последней точки — у итога и у подсвеченной категории (в линиях).
  const labelled = bars ? [] : series.filter((s) => s.total || s.id === active);
  const hovered = hover == null ? [] : [...series].filter((s) => s.values[hover] != null).sort((a, b) => (b.total ? 1 : 0) - (a.total ? 1 : 0) || (b.values[hover] ?? 0) - (a.values[hover] ?? 0));

  return (
    <Section
      title={title}
      hint={
        bars
          ? t("{hint} · {shown}; клик по категории — её столбцы", { hint, shown: shown.total ? t("итог") : shown.name })
          : t("{hint} · клик по линии — подсветить, ещё раз — снять", { hint })
      }
      actions={
        <>
          {actions}
          <div className="flex overflow-hidden rounded-lg border border-line text-xs" role="group" aria-label={t("Вид графика")}>
            {(
              [
                ["bars", t("Столбцы")],
                ["lines", t("Линии")],
              ] as const
            ).map(([key, label]) => (
              <button
                key={key}
                type="button"
                aria-pressed={mode === key}
                onClick={() => setMode(key)}
                className={`px-2.5 py-1.5 ${mode === key ? "bg-accent text-white" : "bg-surface text-ink hover:bg-muted"}`}
              >
                {label}
              </button>
            ))}
          </div>
          <CopyButton onCopy={() => rows.map((r) => r.map(tsvValue).join("\t")).join("\n")} />
          <button
            type="button"
            onClick={() => downloadCsv(`${title} ${data.year}`, rows)}
            title={t("Скачать таблицу для Excel (CSV)")}
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
            aria-label={title}
            onMouseMove={(e) => {
              const left = e.currentTarget.getBoundingClientRect().left;
              const offset = e.clientX - left - pad.left;
              const i = bars ? Math.floor(offset / band) : n <= 1 ? 0 : Math.round((offset / plotW) * (n - 1));
              setHover(Math.min(lastIndex, Math.max(0, i)));
            }}
          >
            <defs>
              <linearGradient id={gradient} x1="0" y1="0" x2="0" y2="1">
                <stop offset="0" style={{ stopColor: shown.total ? "var(--color-accent-strong)" : shown.color, stopOpacity: 0.95 }} />
                <stop offset="1" style={{ stopColor: shown.total ? "var(--color-accent)" : shown.color, stopOpacity: shown.total ? 1 : 0.6 }} />
              </linearGradient>
            </defs>
            {ticks.map((tick) => (
              <g key={tick}>
                <line x1={pad.left} x2={width - pad.right} y1={y(tick)} y2={y(tick)} stroke="var(--color-line)" />
                <text x={pad.left - 8} y={y(tick)} dy="0.32em" textAnchor="end" fontSize={11} fill="var(--color-ink-3)" className="tabular-nums">
                  {format(tick)}
                </text>
              </g>
            ))}
            {data.months.map((m, i) => (
              <text key={m} x={x(i)} y={HEIGHT - 8} textAnchor="middle" fontSize={11} fill={i === hover ? "var(--color-ink)" : "var(--color-ink-3)"}>
                {monthShort(m, t)}
              </text>
            ))}
            {hover != null && !bars && <line x1={x(hover)} x2={x(hover)} y1={pad.top} y2={pad.top + plotH} stroke="var(--color-ink-3)" strokeDasharray="3 3" />}

            {/* Столбцы: выбранная мера по месяцам; идущий месяц — бледнее, с пунктиром; над столбцом — значение. */}
            {bars &&
              shown.values.map((v, i) => {
                if (v == null) return null;
                const partial = i === lastIndex && data.lastPartial;
                const yTop = y(v);
                const h = Math.max(0, base - yTop);
                const r = Math.min(6, h / 2, barWidth / 2);
                const left = x(i) - barWidth / 2;
                return (
                  <g key={i} opacity={hover != null && hover !== i ? 0.55 : 1} className="transition-opacity">
                    {h > 0 && (
                      <path
                        d={`M${left},${base} V${yTop + r} Q${left},${yTop} ${left + r},${yTop} H${left + barWidth - r} Q${left + barWidth},${yTop} ${left + barWidth},${yTop + r} V${base} Z`}
                        fill={`url(#${gradient})`}
                        fillOpacity={partial ? 0.45 : 1}
                        stroke={partial ? (shown.total ? "var(--color-accent-strong)" : shown.color) : "none"}
                        strokeDasharray={partial ? "4 3" : undefined}
                      />
                    )}
                    {band >= 34 && (
                      <text
                        x={x(i)}
                        y={yTop - 7}
                        textAnchor="middle"
                        fontSize={11}
                        fontWeight={hover === i ? 700 : 600}
                        fill={hover === i ? "var(--color-ink)" : "var(--color-ink-2)"}
                        className="tabular-nums"
                      >
                        {format(v)}
                      </text>
                    )}
                  </g>
                );
              })}

            {/* Линии: категории под итогом; подсвеченная — поверх остальных. */}
            {(bars ? [] : [...series.filter((s) => !s.total && s.id !== active), ...series.filter((s) => !s.total && s.id === active), ...series.filter((s) => s.total)]).map((s) => {
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
                  {format(s.values[i])}
                </text>
              ));
            })}
          </svg>
        )}

        {hover != null && hovered.length > 0 && (
          <div
            className="pointer-events-none absolute top-2 z-10 min-w-[180px] rounded-lg border border-line bg-surface-solid px-3 py-2 text-xs shadow-lg"
            style={x(hover) > width / 2 ? { left: x(hover) - 12, transform: "translateX(-100%)" } : { left: x(hover) + 12 }}
          >
            <div className="mb-1 font-semibold text-ink">
              {monthLabel(data.year, data.months[hover], t)}
              {hover === lastIndex && data.lastPartial && <span className="font-normal text-ink-3"> · {t("месяц идёт")}</span>}
            </div>
            {hovered.map((s) => (
              <div key={s.id} className={`flex items-center justify-between gap-4 ${s.total || s.id === active ? "font-semibold text-ink" : "text-ink-2"}`}>
                <span className="flex items-center gap-1.5">
                  <span className="size-2 rounded-full" style={{ background: s.color }} />
                  {s.name}
                </span>
                <span className="tabular-nums">{format(s.values[hover])}</span>
              </div>
            ))}
          </div>
        )}
      </div>

      <div className="mt-3 flex flex-wrap gap-1.5">
        {series.map((s) => {
          // В столбцах выбрана одна мера (итог по умолчанию), в линиях — подсвеченная.
          const pressed = bars ? shown.id === s.id : active === s.id;
          return (
            <button
              key={s.id}
              type="button"
              onClick={() => toggle(s.id)}
              aria-pressed={pressed}
              className={`inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs transition-colors ${
                pressed ? "border-accent bg-accent-soft font-semibold text-ink" : "border-line bg-surface text-ink-2 hover:bg-muted"
              } ${!bars && active != null && active !== s.id ? "opacity-50" : ""}`}
            >
              <span className={`rounded-full ${s.total ? "h-0.5 w-3.5" : "size-2"}`} style={{ background: s.total && bars ? "var(--color-accent)" : s.color }} />
              {s.name}
            </button>
          );
        })}
      </div>

      <AkbTable data={data} series={series} active={active} onToggle={toggle} head={head} totalLabel={totalLabel} format={format} />

      <Note>
        {note ?? (
          <>
            {t("АКБ — уникальные ТТ, у которых чистая покупка (продажи минус возвраты) за месяц больше нуля. Итог — не сумма категорий: одна ТТ покупает несколько категорий.")}{" "}
          </>
        )}
        {data.lastPartial && (bars ? t("Бледный столбец с пунктиром — месяц ещё идёт. ") : t("Пустая точка — месяц ещё идёт. "))}
        {averageNote} {t("Наведите на график — значения за месяц.")}
      </Note>
    </Section>
  );
}
