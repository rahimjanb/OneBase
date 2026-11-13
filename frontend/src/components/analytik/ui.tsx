"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useMemo, useState } from "react";
import { AlertTriangle, ArrowUpRight, CheckCircle2, ChevronDown, CircleAlert, Clock, Info, Lightbulb, Loader2, RefreshCw, Settings2, Sparkles } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  change,
  changeTone,
  dateTime,
  kindLabel,
  kpiValue,
  moduleLabel,
  num,
  originLabel,
  periodKinds,
  periodQuery,
  polyline,
  readinessLabel,
  severityLabel,
  type DataGap,
  type FreshnessRow,
  type Insight,
  type Kpi,
  type Period,
  type PeriodKind,
  type Severity,
} from "@/lib/analytik";

/** Карточка блока: заголовок, подзаголовок, действие справа (ссылка «Открыть»). */
export function Card({
  title,
  subtitle,
  href,
  action,
  children,
  className = "",
}: {
  title?: string;
  subtitle?: React.ReactNode;
  href?: string;
  action?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
}) {
  const t = useT();
  return (
    <section className={`min-w-0 rounded-2xl border border-line bg-surface p-4 sm:p-5 ${className}`}>
      {(title || action || href) && (
        <header className="mb-3 flex items-start justify-between gap-3">
          <div className="min-w-0">
            {title && <h2 className="text-[15px] font-semibold text-ink">{t(title)}</h2>}
            {subtitle && <p className="mt-0.5 text-xs text-ink-3">{typeof subtitle === "string" ? t(subtitle) : subtitle}</p>}
          </div>
          {action ??
            (href && (
              <Link href={href} className="inline-flex shrink-0 items-center gap-1 text-xs font-medium text-accent-strong hover:underline">
                {t("Подробнее")}
                <ArrowUpRight className="size-3.5" />
              </Link>
            ))}
        </header>
      )}
      {children}
    </section>
  );
}

const kindTone: Record<string, string> = {
  fact: "bg-muted text-ink-3",
  calculated: "bg-accent-soft text-accent-strong",
  forecast: "bg-violet/15 text-violet",
};

/** Плитка показателя: значение, изменение к прошлому периоду, вид (факт / расчёт / прогноз), источник — во всплывающей подсказке и ссылке. */
export function KpiCard({ kpi, compact = false }: { kpi: Kpi; compact?: boolean }) {
  const t = useT();
  const delta = change(kpi.changePercent);
  const tone = changeTone(kpi.code, kpi.changePercent);
  const body = (
    <div
      className={`flex h-full min-w-0 flex-col justify-between gap-2 rounded-xl border border-line bg-surface-solid/40 ${compact ? "p-3" : "p-3.5"} transition-colors ${kpi.href ? "hover:border-line-strong" : ""}`}
      title={`${t("Источник")}: ${kpi.source}${kpi.previous != null ? ` · ${t("прошлый период")}: ${kpiValue(kpi.previous, kpi.unit)}` : ""}`}
    >
      <div className="flex items-start justify-between gap-2">
        <span className="min-w-0 break-words text-[12px] leading-tight text-ink-3">{t(kpi.title)}</span>
        <span className={`shrink-0 rounded-md px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wide ${kindTone[kpi.kind] ?? kindTone.fact}`}>
          {t(kindLabel[kpi.kind] ?? kpi.kind)}
        </span>
      </div>
      <div className={`${compact ? "text-lg" : "text-[22px]"} font-semibold leading-none tracking-tight ${kpi.value == null ? "text-ink-4" : "text-ink"}`}>
        {kpi.value == null ? t("Нет данных") : kpiValue(kpi.value, kpi.unit)}
      </div>
      <div className="flex min-h-4 flex-wrap items-center gap-x-2 text-[11px]">
        {delta && <span className={tone === "ok" ? "text-ok" : tone === "bad" ? "text-bad" : "text-ink-3"}>{delta}</span>}
        {kpi.note && <span className="text-ink-4">{t(kpi.note)}</span>}
      </div>
    </div>
  );
  return kpi.href ? (
    <Link href={kpi.href} className="block h-full">
      {body}
    </Link>
  ) : (
    body
  );
}

export function KpiGrid({ kpis, columns = "sm:grid-cols-2 xl:grid-cols-4", compact }: { kpis: Kpi[]; columns?: string; compact?: boolean }) {
  return (
    <div className={`grid grid-cols-2 gap-2.5 ${columns}`}>
      {kpis.map((k) => (
        <KpiCard key={k.code} kpi={k} compact={compact} />
      ))}
    </div>
  );
}

/** Заметки о полноте данных и источники — внизу блока. */
export function Notes({ notes, sources, at }: { notes?: string[]; sources?: string[]; at?: string | null }) {
  const t = useT();
  if (!notes?.length && !sources?.length) return null;
  return (
    <div className="mt-3 space-y-1.5 text-[11.5px] leading-relaxed text-ink-3">
      {notes?.map((n) => (
        <p key={n} className="flex gap-1.5">
          <Info className="mt-0.5 size-3.5 shrink-0 text-ink-4" />
          <span>{t(n)}</span>
        </p>
      ))}
      {sources && sources.length > 0 && (
        <p className="text-ink-4">
          {t("Источники")}: {sources.join(" · ")}
          {at && ` · ${t("на")} ${dateTime(at)}`}
        </p>
      )}
    </div>
  );
}

const severityStyle: Record<string, { cls: string; icon: typeof AlertTriangle }> = {
  critical: { cls: "bg-bad-soft text-bad", icon: CircleAlert },
  warning: { cls: "bg-warn-soft text-warn", icon: AlertTriangle },
  high: { cls: "bg-warn-soft text-warn", icon: AlertTriangle },
  medium: { cls: "bg-accent-soft text-accent-strong", icon: Info },
  info: { cls: "bg-muted text-ink-3", icon: Info },
  low: { cls: "bg-muted text-ink-3", icon: Info },
  none: { cls: "bg-ok-soft text-ok", icon: CheckCircle2 },
};

export function SeverityBadge({ value, label }: { value: string; label?: string }) {
  const t = useT();
  const s = severityStyle[value] ?? severityStyle.info;
  const Icon = s.icon;
  return (
    <span className={`inline-flex shrink-0 items-center gap-1 rounded-md px-1.5 py-0.5 text-[11px] font-medium ${s.cls}`}>
      <Icon className="size-3" />
      {t(label ?? severityLabel[value as Severity] ?? value)}
    </span>
  );
}

/** Находка: суть, причина — только подтверждённая (иначе гипотезы), последствия, рекомендации, ссылка на данные. */
export function InsightCard({ insight, open: initial = false }: { insight: Insight; open?: boolean }) {
  const t = useT();
  const [open, setOpen] = useState(initial);
  return (
    <article className="rounded-xl border border-line bg-surface-solid/30 p-3.5">
      <button type="button" onClick={() => setOpen(!open)} className="flex w-full items-start gap-2.5 text-left" aria-expanded={open}>
        <SeverityBadge value={insight.severity} />
        <div className="min-w-0 flex-1">
          <h3 className="text-[13.5px] font-medium leading-snug text-ink">{insight.title}</h3>
          <p className="mt-0.5 text-[11px] text-ink-4">
            {t(moduleLabel[insight.module] ?? insight.module)} · {t(insight.kind === "quality" ? "качество данных" : insight.kind === "anomaly" ? "отклонение" : insight.kind === "opportunity" ? "возможность" : "риск")}
            {insight.source === "proactive" ? ` · ${t("проактивный анализ")}` : ""} · {dateTime(insight.detectedAt)}
          </p>
        </div>
        <ChevronDown className={`mt-0.5 size-4 shrink-0 text-ink-4 transition-transform ${open ? "rotate-180" : ""}`} />
      </button>
      {open && (
        <div className="mt-3 space-y-2.5 border-t border-line pt-3 text-[12.5px] leading-relaxed text-ink-2">
          <p>{insight.description}</p>
          {insight.cause && (
            <p>
              <span className="font-medium text-ink">{t(insight.causeConfirmed ? "Причина (подтверждена данными)" : "Причина")}: </span>
              {insight.cause}
            </p>
          )}
          {insight.hypotheses.length > 0 && (
            <div>
              <span className="font-medium text-ink">{t("Гипотезы (не подтверждены данными)")}:</span>
              <ul className="mt-1 list-disc space-y-0.5 pl-5 text-ink-3">
                {insight.hypotheses.map((h) => (
                  <li key={h}>{h}</li>
                ))}
              </ul>
            </div>
          )}
          {insight.impact != null && (
            <p>
              <span className="font-medium text-ink">{t("Масштаб")}: </span>
              {num(insight.impact, 2)} {insight.impactUnit}
            </p>
          )}
          {insight.consequences && (
            <p>
              <span className="font-medium text-ink">{t("Последствия")}: </span>
              {insight.consequences}
            </p>
          )}
          {insight.recommendations.length > 0 && (
            <div>
              <span className="font-medium text-ink">{t("Рекомендации")}:</span>
              <ul className="mt-1 space-y-1">
                {insight.recommendations.map((r) => (
                  <li key={r} className="flex gap-1.5">
                    <Lightbulb className="mt-0.5 size-3.5 shrink-0 text-accent-strong" />
                    <span>{r}</span>
                  </li>
                ))}
              </ul>
            </div>
          )}
          <div className="flex flex-wrap gap-3 pt-0.5 text-xs">
            {insight.href && (
              <Link href={insight.href} className="inline-flex items-center gap-1 font-medium text-accent-strong hover:underline">
                {t("Открыть данные")}
                <ArrowUpRight className="size-3.5" />
              </Link>
            )}
            <Link href={`/analytik/chat?q=${encodeURIComponent(`Разбери находку: ${insight.title}`)}`} className="inline-flex items-center gap-1 font-medium text-accent-strong hover:underline">
              <Sparkles className="size-3.5" />
              {t("Спросить аналитика")}
            </Link>
          </div>
        </div>
      )}
    </article>
  );
}

/** Пустое состояние: «нет данных» — с причиной, а не ноль. */
export function Empty({ text, hint }: { text: string; hint?: string }) {
  const t = useT();
  return (
    <div className="rounded-xl border border-dashed border-line px-4 py-8 text-center">
      <p className="text-sm text-ink-2">{t(text)}</p>
      {hint && <p className="mt-1 text-xs text-ink-4">{t(hint)}</p>}
    </div>
  );
}

/**
 * Линейный график (SVG): одна или несколько линий, полоса неопределённости прогноза (band), подписи первой и последней даты, наведение —
 * значения точки. Отдельные серии могут быть короче: прогноз продолжает историю.
 */
export function LineChart({
  labels,
  series,
  band,
  format = (v) => num(v),
  height = 180,
}: {
  labels: string[];
  series: { name: string; values: (number | null)[]; color: string; dashed?: boolean }[];
  band?: { lower: (number | null)[]; upper: (number | null)[] };
  format?: (v: number) => string;
  height?: number;
}) {
  const t = useT();
  const [hover, setHover] = useState<number | null>(null);
  const width = 640;
  const all = [...series.flatMap((s) => s.values), ...(band?.lower ?? []), ...(band?.upper ?? [])].filter((v): v is number => v != null);
  const min = Math.min(0, ...all);
  const max = Math.max(1, ...all);
  const n = labels.length;
  const x = (i: number) => (n <= 1 ? width / 2 : 8 + (i * (width - 16)) / (n - 1));
  const y = (v: number) => height - 18 - ((v - min) / (max - min || 1)) * (height - 30);
  const path = (values: (number | null)[]) => {
    const points = values.map((v, i) => (v == null ? null : `${x(i).toFixed(1)},${y(v).toFixed(1)}`)).filter(Boolean);
    return points.join(" ");
  };
  const bandPath = useMemo(() => {
    if (!band) return null;
    const idx = band.upper.map((v, i) => (v != null && band.lower[i] != null ? i : -1)).filter((i) => i >= 0);
    if (idx.length < 2) return null;
    const top = idx.map((i) => `${x(i).toFixed(1)},${y(band.upper[i]!).toFixed(1)}`);
    const bottom = [...idx].reverse().map((i) => `${x(i).toFixed(1)},${y(band.lower[i]!).toFixed(1)}`);
    return [...top, ...bottom].join(" ");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [band, n, min, max]);
  if (n === 0) return <Empty text="Нет данных для графика" />;
  return (
    <div className="relative">
      <svg
        viewBox={`0 0 ${width} ${height}`}
        className="h-auto w-full touch-none"
        onMouseLeave={() => setHover(null)}
        onMouseMove={(e) => {
          const box = e.currentTarget.getBoundingClientRect();
          const rel = ((e.clientX - box.left) / box.width) * width;
          setHover(Math.max(0, Math.min(n - 1, Math.round(((rel - 8) / (width - 16)) * (n - 1)))));
        }}
        role="img"
        aria-label={series.map((s) => t(s.name)).join(", ")}
      >
        <line x1="0" x2={width} y1={y(min)} y2={y(min)} stroke="var(--color-line)" />
        {bandPath && <polygon points={bandPath} fill="var(--color-violet)" opacity="0.12" />}
        {series.map((s) => (
          <polyline key={s.name} points={path(s.values)} fill="none" stroke={s.color} strokeWidth="2" strokeDasharray={s.dashed ? "5 4" : undefined} strokeLinejoin="round" />
        ))}
        {hover != null && <line x1={x(hover)} x2={x(hover)} y1="6" y2={height - 18} stroke="var(--color-line-strong)" strokeDasharray="3 3" />}
        <text x="8" y={height - 4} className="fill-ink-4" style={{ fontSize: 11 }}>
          {labels[0]}
        </text>
        <text x={width - 8} y={height - 4} textAnchor="end" className="fill-ink-4" style={{ fontSize: 11 }}>
          {labels[n - 1]}
        </text>
        <text x={width - 8} y="12" textAnchor="end" className="fill-ink-4" style={{ fontSize: 11 }}>
          {format(max)}
        </text>
      </svg>
      {hover != null && (
        <div className="pointer-events-none absolute left-2 top-2 rounded-lg border border-line bg-surface-solid px-2.5 py-1.5 text-[11px] shadow-sm">
          <div className="font-medium text-ink">{labels[hover]}</div>
          {series.map((s) =>
            s.values[hover] != null ? (
              <div key={s.name} className="flex items-center gap-1.5 text-ink-2">
                <span className="size-2 rounded-full" style={{ background: s.color }} />
                {t(s.name)}: {format(s.values[hover]!)}
              </div>
            ) : null,
          )}
          {band && band.lower[hover] != null && (
            <div className="text-ink-4">
              {t("диапазон")}: {format(band.lower[hover]!)} — {format(band.upper[hover]!)}
            </div>
          )}
        </div>
      )}
      <div className="mt-1 flex flex-wrap gap-3 text-[11px] text-ink-3">
        {series.map((s) => (
          <span key={s.name} className="inline-flex items-center gap-1.5">
            <span className="h-0.5 w-4" style={{ background: s.color }} />
            {t(s.name)}
          </span>
        ))}
        {band && (
          <span className="inline-flex items-center gap-1.5">
            <span className="h-2 w-4 rounded-sm bg-violet/20" />
            {t("диапазон прогноза")}
          </span>
        )}
      </div>
    </div>
  );
}

/** Горизонтальные полосы: подпись, значение, полоса относительно максимума, подстрочник. */
export function BarList({ rows }: { rows: { key: string; label: string; value: number; text: string; sub?: React.ReactNode; tone?: "accent" | "ok" | "warn" | "bad" }[] }) {
  const max = Math.max(...rows.map((r) => Math.abs(r.value)), 1);
  const tones = { accent: "bg-accent", ok: "bg-ok", warn: "bg-warn", bad: "bg-bad" };
  return (
    <ul className="space-y-2.5">
      {rows.map((r) => (
        <li key={r.key} className="text-[12.5px]">
          <div className="flex items-baseline justify-between gap-3">
            <span className="min-w-0 truncate text-ink-2">{r.label}</span>
            <span className="shrink-0 font-medium tabular-nums text-ink">{r.text}</span>
          </div>
          <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-muted">
            <div className={`h-full rounded-full ${tones[r.tone ?? "accent"]}`} style={{ width: `${(Math.abs(r.value) / max) * 100}%` }} />
          </div>
          {r.sub && <div className="mt-0.5 text-[11px] text-ink-4">{r.sub}</div>}
        </li>
      ))}
    </ul>
  );
}

/** Ряд значений в линию для мини-графика (без осей). */
export function Spark({ values, color = "var(--color-accent)" }: { values: number[]; color?: string }) {
  if (values.length < 2) return null;
  const min = Math.min(...values);
  const max = Math.max(...values);
  return (
    <svg viewBox="0 0 120 32" className="h-8 w-28" aria-hidden>
      <polyline points={polyline(values, 120, 32, min, max, 2)} fill="none" stroke={color} strokeWidth="1.8" />
    </svg>
  );
}

/** Выбор периода: сегодня, вчера, неделя, месяц, квартал, год, свой (с датами). Меняет адрес страницы — данные пересчитывает сервер. */
export function PeriodPicker({ period, extra }: { period: Period; extra?: Record<string, string | undefined> }) {
  const t = useT();
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const [from, setFrom] = useState(period.from);
  const [to, setTo] = useState(period.to);
  const [custom, setCustom] = useState(period.kind === "custom");
  const go = (kind: PeriodKind, f?: string, tt?: string) => {
    const rest: Record<string, string | undefined> = { ...extra };
    params.forEach((v, k) => {
      if (!["period", "from", "to"].includes(k)) rest[k] = v;
    });
    router.push(`${pathname}${periodQuery(kind, f, tt, rest)}`);
  };
  return (
    <div className="flex flex-wrap items-center gap-2">
      <div role="radiogroup" aria-label={t("Период")} className="inline-flex flex-wrap rounded-xl border border-line bg-surface p-0.5">
        {periodKinds.map((p) => {
          const active = p.kind === "custom" ? custom : !custom && period.kind === p.kind;
          return (
            <button
              key={p.kind}
              type="button"
              role="radio"
              aria-checked={active}
              onClick={() => (p.kind === "custom" ? setCustom(true) : (setCustom(false), go(p.kind)))}
              className={`rounded-[10px] px-2.5 py-1.5 text-xs font-medium transition-colors ${active ? "bg-accent text-white shadow-sm" : "text-ink-3 hover:text-ink"}`}
            >
              {t(p.label)}
            </button>
          );
        })}
      </div>
      {custom && (
        <form
          className="flex items-center gap-1.5"
          onSubmit={(e) => {
            e.preventDefault();
            go("custom", from, to);
          }}
        >
          <input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} className="h-8 rounded-lg border border-line bg-surface px-2 text-xs text-ink" aria-label={t("С")} />
          <span className="text-ink-4">—</span>
          <input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} className="h-8 rounded-lg border border-line bg-surface px-2 text-xs text-ink" aria-label={t("По")} />
          <button type="submit" className="h-8 rounded-lg bg-accent px-3 text-xs font-medium text-white">
            {t("Показать")}
          </button>
        </form>
      )}
      <span className="text-[11px] text-ink-4">
        {period.label} · {t("сравнение")}: {period.previousLabel}
      </span>
    </div>
  );
}

/**
 * Данные выбранного периода загружены не полностью: что не загружено и почему, что система уже сделала (запустила обновление, уведомила
 * администраторов), ссылка на настройки подключения (тем, кто может исправить) и кнопка — показать месяц, за который данные есть.
 */
export function DataGaps({ gaps }: { gaps: DataGap[] }) {
  const t = useT();
  const pathname = usePathname();
  const params = useSearchParams();
  if (gaps.length === 0) return null;
  const rest: Record<string, string | undefined> = {};
  params.forEach((v, k) => {
    if (!["period", "from", "to"].includes(k)) rest[k] = v;
  });
  return (
    <div className="space-y-2">
      {gaps.map((g) => (
        <div key={g.text} role="status" className="flex flex-col gap-3 rounded-2xl border border-warn/30 bg-warn-soft px-4 py-3 sm:flex-row sm:items-center">
          <div className="flex min-w-0 flex-1 gap-2.5">
            <AlertTriangle className="mt-0.5 size-4 shrink-0 text-warn" />
            <div className="min-w-0 space-y-0.5 text-[12.5px] leading-relaxed">
              <p className="break-words text-ink">{t(g.text)}</p>
              {g.action && <p className="break-words text-ink-3">{t(g.action)}</p>}
            </div>
          </div>
          {(g.latest || g.href) && (
            <div className="flex shrink-0 flex-wrap gap-2 pl-6 sm:pl-0">
              {g.latest && (
                <Link
                  href={`${pathname}${periodQuery("custom", g.latest.from, g.latest.to, rest)}`}
                  className="inline-flex h-8 items-center rounded-lg bg-accent px-3 text-xs font-medium text-white"
                >
                  {t("Показать {period}", { period: g.latest.label })}
                </Link>
              )}
              {g.href && (
                <Link href={g.href} className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-xs font-medium text-ink-2 hover:border-line-strong">
                  <Settings2 className="size-3.5" />
                  {t("Подключение")}
                </Link>
              )}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

const readinessTone: Record<string, string> = {
  ok: "text-ok",
  updating: "text-accent-strong",
  stale: "text-warn",
  missing: "text-warn",
  error: "text-bad",
  not_connected: "text-bad",
};

/**
 * Свежесть данных по источникам: наборы (продажи, оплаты, остатки, планы, посещаемость), когда обновлены, почему отстают и что система уже
 * сделала — один раз на источник; «Обновить» — тем, у кого есть право на подключение (проверяет сервер).
 */
export function FreshnessList({ rows }: { rows: FreshnessRow[] }) {
  const t = useT();
  const router = useRouter();
  const [busy, setBusy] = useState<string | null>(null);
  const [message, setMessage] = useState<Record<string, { text: string; ok: boolean }>>({});
  const groups = useMemo(() => {
    const map = new Map<string, FreshnessRow[]>();
    for (const r of rows) {
      const key = r.origin ?? "onebase";
      map.set(key, [...(map.get(key) ?? []), r]);
    }
    return [...map.entries()];
  }, [rows]);

  const refresh = async (origin: string) => {
    setBusy(origin);
    try {
      await bff(`analytik/readiness/${origin}/refresh`, { method: "POST" });
      setMessage((m) => ({ ...m, [origin]: { text: t("Обновление запущено — данные появятся через минуту-две"), ok: true } }));
      setTimeout(() => router.refresh(), 20_000);
    } catch (e) {
      setMessage((m) => ({ ...m, [origin]: { text: (e as Error).message, ok: false } }));
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="space-y-3">
      {groups.map(([origin, items]) => {
        const notes = [...new Set(items.filter((r) => r.status !== "ok" && r.note).map((r) => r.note as string))];
        const actions = [...new Set(items.filter((r) => r.action).map((r) => r.action as string))];
        // «Обновить» — когда данные отстают; свежие обновляются сами по расписанию.
        const canRefresh = items.some((r) => r.canRefresh && r.status !== "ok");
        const href = items.find((r) => r.href && r.status !== "ok")?.href;
        const msg = message[origin];
        return (
          <div key={origin} className="space-y-1.5">
            <ul className="space-y-1.5 text-[12.5px]">
              {items.map((f) => {
                const label = readinessLabel(f.status);
                const value = f.code === "plans" ? (f.status === "ok" ? t("заведены") : null) : f.at ? dateTime(f.at) : t("не было");
                return (
                  <li key={f.code ?? f.source} className="flex items-start justify-between gap-3">
                    <span className="inline-flex min-w-0 items-start gap-1.5 text-ink-2">
                      {f.status === "updating" ? (
                        <Loader2 className="mt-0.5 size-3.5 shrink-0 animate-spin text-accent-strong" />
                      ) : (
                        <Clock className={`mt-0.5 size-3.5 shrink-0 ${readinessTone[f.status] ?? "text-warn"}`} />
                      )}
                      <span className="min-w-0 break-words">{t(f.source)}</span>
                    </span>
                    <span className={`shrink-0 text-right ${f.status === "ok" ? "text-ink-3" : readinessTone[f.status] ?? "text-warn"}`}>
                      {[value, label ? t(label) : null].filter(Boolean).join(" · ")}
                    </span>
                  </li>
                );
              })}
            </ul>
            {(notes.length > 0 || actions.length > 0) && (
              <div className="space-y-1 rounded-lg bg-muted px-2.5 py-2 text-[11.5px] leading-relaxed text-ink-3">
                {notes.map((n) => (
                  <p key={n} className="break-words">{t(n)}</p>
                ))}
                {actions.map((a) => (
                  <p key={a} className="break-words text-ink-4">{t(a)}</p>
                ))}
              </div>
            )}
            {(canRefresh || href || msg) && (
              <div className="flex flex-wrap items-center gap-2">
                {canRefresh && (
                  <button
                    type="button"
                    disabled={busy !== null}
                    onClick={() => refresh(origin)}
                    className="inline-flex h-7 items-center gap-1.5 rounded-lg border border-line bg-surface px-2.5 text-[11.5px] font-medium text-ink-2 hover:border-line-strong disabled:opacity-50"
                  >
                    {busy === origin ? <Loader2 className="size-3.5 animate-spin" /> : <RefreshCw className="size-3.5" />}
                    {t("Обновить {source}", { source: originLabel(origin) })}
                  </button>
                )}
                {href && (
                  <Link href={href} className="inline-flex h-7 items-center gap-1.5 rounded-lg px-1.5 text-[11.5px] font-medium text-accent-strong hover:underline">
                    <Settings2 className="size-3.5" />
                    {t("Подключение")}
                  </Link>
                )}
                {msg && <span className={`text-[11.5px] ${msg.ok ? "text-ok" : "text-bad"}`}>{msg.text}</span>}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
