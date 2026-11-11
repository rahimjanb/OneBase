"use client";

import Link from "next/link";
import { useState } from "react";
import {
  ArrowRight,
  ChartColumn,
  ChevronRight,
  CircleCheck,
  CircleX,
  ClipboardList,
  FileChartColumn,
  FlaskConical,
  Hammer,
  Hourglass,
  PackageX,
  Scale,
  Timer,
  TrendingUp,
  type LucideIcon,
} from "lucide-react";
import { useT } from "@/i18n/client";
import type { ProductionDashboard } from "@/lib/production";
import { axisTicks, kg, share, shortDay, TECH_PERIODS, type Caption, type Counter, type QuickLink, type TechPeriod, type Tone } from "@/lib/technologist-view";

/*
 * Общие детали экрана «Технолог» (макеты kPu1u — компьютер, RkFy8 — телефон). Цвета — токены палитры макета (data-palette). Мелкий текст — muted
 * (ink-3): subtle макета (ink-4) на фоне карточки даёт 2,9:1 в светлой теме и 3,3:1 в тёмной — ниже AA; subtle — у иконок и тире «нет данных».
 */

export const toneText: Record<NonNullable<Tone>, string> = { ok: "text-ok", warn: "text-warn", bad: "text-bad" };

/** Пульсирующая заготовка на время загрузки. */
export function Skel({ className }: { className: string }) {
  return <span aria-hidden className={`block animate-pulse rounded-md bg-muted ${className}`} />;
}

const quickIcon: Record<QuickLink["key"], { icon: LucideIcon; box: string; hover: string }> = {
  recipes: { icon: FlaskConical, box: "bg-accent-soft text-accent-strong", hover: "hover:border-accent-strong" },
  shifts: { icon: ClipboardList, box: "bg-violet-soft text-violet", hover: "hover:border-violet" },
  reports: { icon: FileChartColumn, box: "bg-ok-soft text-ok", hover: "hover:border-ok" },
};

function CaptionText({ caption, loading, unknown, mobile }: { caption: Caption; loading: boolean; unknown: boolean; mobile: boolean }) {
  const t = useT();
  if (loading && caption.vars) return <Skel className="mt-1 h-3 w-20" />;
  if (unknown && caption.vars) return null; // данные не пришли — не показываем нули
  return (
    <span
      title={t(caption.key, caption.vars)}
      className={`block truncate ${mobile ? "text-[12px]" : "text-[11px]"} ${caption.warn ? "font-semibold text-warn" : "font-medium text-ink-3"}`}
    >
      {t(caption.key, caption.vars)}
    </span>
  );
}

/**
 * Кнопка быстрого перехода «Техкарты / Смены / Отчёты»: цветная иконка в квадрате, название и подпись-счётчик, стрелка. Это ссылка — открывается
 * в новой вкладке по Cmd/Ctrl+клику; для скринридера — полный текст («Техкарты, на утверждении: 0»).
 */
export function QuickLinkCard({ link, loading, unknown = false, mobile = false }: { link: QuickLink; loading: boolean; unknown?: boolean; mobile?: boolean }) {
  const t = useT();
  const view = quickIcon[link.key];
  const Icon = view.icon;
  const caption = mobile ? link.mobileCaption : link.caption;
  const label = (loading || unknown) && caption.vars ? t(link.title) : `${t(link.title)}, ${t(caption.key, caption.vars)}`;
  return (
    <Link
      href={link.href}
      aria-label={label}
      className={`group flex min-w-0 items-center border border-line-strong bg-surface outline-none transition-colors hover:bg-muted ${view.hover} focus-visible:ring-2 focus-visible:ring-accent active:translate-y-px ${
        mobile ? "min-h-[68px] gap-3.5 rounded-2xl py-2.5 pl-2.5 pr-3.5 active:bg-muted" : "gap-3 rounded-[14px] py-2 pl-2 pr-4 shadow-[var(--sec-float-shadow)]"
      }`}
    >
      <span aria-hidden className={`grid shrink-0 place-items-center ${view.box} ${mobile ? "size-[48px] rounded-[13px]" : "size-10 rounded-[11px]"}`}>
        <Icon className={mobile ? "size-[22px]" : "size-5"} />
      </span>
      <span className="min-w-0 flex-1">
        <span className={`block font-semibold text-ink ${mobile ? "text-[16px]" : "text-[15px]"}`}>{t(link.title)}</span>
        <CaptionText caption={caption} loading={loading} unknown={unknown} mobile={mobile} />
      </span>
      <ChevronRight aria-hidden className={`shrink-0 text-ink-4 ${mobile ? "size-[18px]" : "size-4"}`} />
    </Link>
  );
}

/** Переключатель периода (radiogroup «Период»): стрелки ← → двигают выбор. На телефоне — во всю ширину, короткие подписи. */
export function PeriodSwitch({ value, onChange, mobile = false }: { value: TechPeriod | null; onChange: (p: TechPeriod) => void; mobile?: boolean }) {
  const t = useT();
  const onKey = (e: React.KeyboardEvent) => {
    if (e.key !== "ArrowRight" && e.key !== "ArrowLeft") return;
    e.preventDefault();
    const i = Math.max(0, TECH_PERIODS.findIndex((p) => p.key === value));
    const next = TECH_PERIODS[(i + (e.key === "ArrowRight" ? 1 : TECH_PERIODS.length - 1)) % TECH_PERIODS.length];
    onChange(next.key);
    (e.currentTarget.querySelector(`[data-period="${next.key}"]`) as HTMLElement | null)?.focus();
  };
  return (
    <div
      role="radiogroup"
      aria-label={t("Период")}
      onKeyDown={onKey}
      className={`flex gap-1 rounded-xl border border-line bg-surface p-1 ${mobile ? "w-full" : "w-fit"}`}
    >
      {TECH_PERIODS.map((p) => {
        const on = p.key === value;
        return (
          <button
            key={p.key}
            type="button"
            role="radio"
            aria-checked={on}
            data-period={p.key}
            tabIndex={on || (!value && p.key === "this_month") ? 0 : -1}
            onClick={() => onChange(p.key)}
            className={`rounded-[9px] outline-none focus-visible:ring-2 focus-visible:ring-accent ${
              mobile ? "h-[34px] flex-1 px-1 text-[12px]" : "px-3.5 py-[9px] text-[13px]"
            } ${on ? "bg-accent font-semibold text-white" : "font-medium text-ink-2 hover:text-ink"}`}
          >
            {t(mobile ? p.short : p.label)}
          </button>
        );
      })}
    </div>
  );
}

/** Карточка KPI: подпись с иконкой, значение с единицей (нет данных — тире subtle), пояснение; строка пояснения есть всегда — карточки одной высоты. */
export function Kpi({
  icon: Icon,
  label,
  value,
  unit,
  note,
  extra,
  tone = null,
  loading,
  mobile = false,
}: {
  icon: LucideIcon;
  label: string;
  value: string | null;
  unit?: string;
  note?: string | null;
  extra?: { text: string; title?: string } | null;
  tone?: Tone;
  loading: boolean;
  mobile?: boolean;
}) {
  const t = useT();
  return (
    <div className={`flex min-w-0 flex-col rounded-2xl border border-line bg-surface ${mobile ? "gap-1.5 px-3.5 py-3" : "gap-2 px-4 py-3.5"}`}>
      <span className={`flex min-w-0 items-center gap-1.5 font-medium text-ink-3 ${mobile ? "text-[11px]" : "text-[12px]"}`}>
        <Icon aria-hidden className={`shrink-0 text-ink-4 ${mobile ? "size-[13px]" : "size-3.5"}`} />
        <span className="truncate" title={t(label)}>
          {t(label)}
        </span>
      </span>
      {loading ? (
        <Skel className={mobile ? "h-6 w-16" : "h-7 w-20"} />
      ) : value === null ? (
        <span className={`font-bold leading-none text-ink-4 ${mobile ? "text-[20px]" : "text-[24px]"}`} aria-label={t("нет данных")}>
          —
        </span>
      ) : (
        <span className="flex items-baseline gap-1">
          <b className={`font-bold leading-none tabular-nums ${tone ? toneText[tone] : "text-ink"} ${mobile ? "text-[20px]" : "text-[24px]"}`}>{value}</b>
          {unit && <span className={`font-medium text-ink-3 ${mobile ? "text-[12px]" : "text-[13px]"}`}>{t(unit)}</span>}
        </span>
      )}
      <span className="min-h-4 truncate text-[11px] font-medium leading-4 text-ink-3" title={!loading && note ? note : undefined}>
        {!loading && note ? note : ""}
      </span>
      {!loading && extra && (
        <span className="-mt-1.5 truncate text-[11px] font-medium tabular-nums text-ink-3" title={extra.title}>
          {extra.text}
        </span>
      )}
    </div>
  );
}

const counterIcon: Record<Counter["key"], LucideIcon> = { orders: Hammer, open: Timer, approval: Hourglass, recipes: FlaskConical };

/** Счётчики чипами (компьютер, планшет): при N > 0 — ссылка в отфильтрованный список. */
export function CounterChips({ items, loading }: { items: Counter[]; loading: boolean }) {
  const t = useT();
  return (
    <ul className="flex flex-wrap gap-2" aria-label={t("Счётчики")}>
      {items.map((c) => {
        const Icon = counterIcon[c.key];
        const body = (
          <>
            <Icon aria-hidden className="size-3 shrink-0 text-ink-4" />
            <span>
              {t(c.label)}: {loading ? <Skel className="inline-block h-3 w-4 align-middle" /> : <span className="tabular-nums">{c.value}</span>}
            </span>
          </>
        );
        const cls = "flex items-center gap-1.5 rounded-lg border border-line bg-surface px-2.5 py-[5px] text-[12px] font-medium text-ink-2";
        return (
          <li key={c.key}>
            {c.href && !loading ? (
              <Link href={c.href} className={`${cls} hover:border-line-strong hover:text-ink`}>
                {body}
              </Link>
            ) : (
              <span className={cls}>{body}</span>
            )}
          </li>
        );
      })}
    </ul>
  );
}

/** Счётчики одной карточкой-списком (телефон). */
export function CounterList({ items, loading }: { items: Counter[]; loading: boolean }) {
  const t = useT();
  return (
    <ul className="rounded-2xl border border-line bg-surface px-3.5 py-1" aria-label={t("Счётчики")}>
      {items.map((c) => {
        const Icon = counterIcon[c.key];
        const body = (
          <>
            <Icon aria-hidden className="size-[15px] shrink-0 text-ink-4" />
            <span className="flex-1 text-[13px] text-ink-2">{t(c.label)}</span>
            {loading ? <Skel className="h-3.5 w-5" /> : <span className="text-[13px] font-semibold tabular-nums text-ink">{c.value}</span>}
          </>
        );
        return (
          <li key={c.key}>
            {c.href && !loading ? (
              <Link href={c.href} className="flex items-center gap-2.5 py-[9px]">
                {body}
              </Link>
            ) : (
              <span className="flex items-center gap-2.5 py-[9px]">{body}</span>
            )}
          </li>
        );
      })}
    </ul>
  );
}

/** Пустое состояние раздела: иконка в квадрате и текст. */
export function EmptyRow({ icon: Icon, text, size = "md" }: { icon: LucideIcon; text: string; size?: "md" | "lg" }) {
  const t = useT();
  return size === "lg" ? (
    <div className="flex flex-col items-center justify-center gap-3 text-center">
      <span className="grid size-10 place-items-center rounded-xl bg-muted">
        <Icon aria-hidden className="size-[18px] text-ink-4" />
      </span>
      <p className="text-[13px] font-medium text-ink-3">{t(text)}</p>
    </div>
  ) : (
    <p className="flex items-center gap-3 text-[13px] text-ink-3">
      <span className="grid size-8 shrink-0 place-items-center rounded-[10px] bg-muted">
        <Icon aria-hidden className="size-[15px] text-ink-4" />
      </span>
      {t(text)}
    </p>
  );
}

/**
 * «Выпуск и брак по дням»: столбцы по дням с накоплением (годный выход снизу, брак сверху, между ними — 2 px фона), ось 0 / середина / верх,
 * легенда; при наведении (или фокусе) — подсказка с датой и значениями. Для скринридера — таблица значений. Дней много (год) — на узком экране
 * график прокручивается по горизонтали.
 */
export function DailyChart({ days, loading, mobile = false }: { days: ProductionDashboard["days"]; loading: boolean; mobile?: boolean }) {
  const t = useT();
  const [hover, setHover] = useState<number | null>(null);
  const max = Math.max(0, ...days.map((d) => d.goodKg + d.defectKg));
  const ticks = axisTicks(max);
  const top = ticks[ticks.length - 1] || 1;
  const height = mobile ? 160 : 200;
  const wide = mobile && days.length > 31;
  const active = hover !== null ? days[hover] : null;

  return (
    <section className={`rounded-2xl border border-line bg-surface ${mobile ? "px-4 py-3.5" : "px-5 py-[18px]"}`} aria-labelledby={mobile ? "tech-days-m" : "tech-days"}>
      <div className="flex flex-wrap items-start justify-between gap-x-4 gap-y-2">
        <div>
          <h2 id={mobile ? "tech-days-m" : "tech-days"} className={`flex items-center gap-2 font-bold text-ink ${mobile ? "text-[14px]" : "text-[15px]"}`}>
            <TrendingUp aria-hidden className="size-4 text-accent-strong" />
            {t("Выпуск и брак по дням")}
          </h2>
          {!mobile && <p className="mt-1 text-[12px] text-ink-3">{t("Годный выход утверждённых смен")}</p>}
        </div>
        <ul className="flex items-center gap-3 text-[12px] text-ink-3" aria-label={t("Легенда")}>
          <li className="flex items-center gap-1.5">
            <span aria-hidden className="size-2.5 rounded-[3px] bg-accent" />
            {t("Годный выход, кг")}
          </li>
          <li className="flex items-center gap-1.5">
            <span aria-hidden className="size-2.5 rounded-[3px] bg-bad" />
            {t("Брак, кг")}
          </li>
        </ul>
      </div>

      {loading ? (
        <Skel className={`mt-4 w-full rounded-xl ${mobile ? "h-[110px]" : "h-[150px]"}`} />
      ) : max === 0 ? (
        <div className={`grid place-items-center ${mobile ? "h-[110px]" : "h-[150px]"}`}>
          <EmptyRow icon={ChartColumn} text="Утверждённых смен за период нет" size="lg" />
        </div>
      ) : (
        <div className="mt-4 flex gap-2">
          <div className="relative w-12 shrink-0 text-right text-[11px] tabular-nums text-ink-3" style={{ height }} aria-hidden>
            {ticks.map((v) => (
              <span key={v} className="absolute right-0 -translate-y-1/2" style={{ top: `${(1 - v / top) * 100}%` }}>
                {kg(v)}
              </span>
            ))}
          </div>
          <div className={`min-w-0 flex-1 ${wide ? "overflow-x-auto" : ""}`}>
            <div className="relative" style={{ height, minWidth: wide ? days.length * 8 : undefined }}>
              {ticks.map((v) => (
                <span key={v} aria-hidden className="absolute inset-x-0 border-t border-line" style={{ top: `${(1 - v / top) * 100}%` }} />
              ))}
              <div className="absolute inset-0 flex items-end gap-[2px]" role="img" aria-label={t("Выпуск и брак по дням: годный выход {a} кг, брак {b} кг", {
                a: kg(days.reduce((s, d) => s + d.goodKg, 0)),
                b: kg(days.reduce((s, d) => s + d.defectKg, 0)),
              })}>
                {days.map((d, i) => {
                  const total = d.goodKg + d.defectKg;
                  return (
                    <div
                      key={d.date}
                      className="relative flex h-full min-w-0 flex-1 flex-col justify-end"
                      onMouseEnter={() => setHover(i)}
                      onMouseLeave={() => setHover(null)}
                    >
                      {total > 0 && (
                        <div
                          className={`mx-auto flex w-full max-w-7 flex-col gap-[2px] transition-opacity ${hover !== null && hover !== i ? "opacity-45" : ""}`}
                          style={{ height: `${(total / top) * 100}%` }}
                        >
                          {d.defectKg > 0 && <div className="min-h-[2px] rounded-t-[4px] bg-bad" style={{ flexGrow: d.defectKg }} />}
                          {d.goodKg > 0 && <div className={`min-h-[2px] bg-accent ${d.defectKg > 0 ? "" : "rounded-t-[4px]"}`} style={{ flexGrow: d.goodKg }} />}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
              {active && hover !== null && (
                <div
                  role="tooltip"
                  className="pointer-events-none absolute z-10 w-max -translate-x-1/2 rounded-lg border border-line-strong bg-surface-solid px-2.5 py-1.5 text-[12px] shadow-lg"
                  style={{ left: `${((hover + 0.5) / days.length) * 100}%`, bottom: `${Math.min(92, ((active.goodKg + active.defectKg) / top) * 100 + 4)}%` }}
                >
                  <p className="font-semibold text-ink">{shortDay(active.date)}</p>
                  <p className="flex items-center gap-1.5 text-ink-2">
                    <span aria-hidden className="size-2 rounded-[2px] bg-accent" />
                    {t("Годный выход")}: <b className="tabular-nums">{kg(active.goodKg)}</b> {t("кг")}
                  </p>
                  <p className="flex items-center gap-1.5 text-ink-2">
                    <span aria-hidden className="size-2 rounded-[2px] bg-bad" />
                    {t("Брак")}: <b className="tabular-nums">{kg(active.defectKg)}</b> {t("кг")}
                  </p>
                </div>
              )}
            </div>
            <div className="mt-1.5 flex justify-between text-[11px] tabular-nums text-ink-3" style={{ minWidth: wide ? days.length * 8 : undefined }} aria-hidden>
              <span>{shortDay(days[0].date)}</span>
              {days.length > 2 && <span>{shortDay(days[Math.floor(days.length / 2)].date)}</span>}
              <span>{shortDay(days[days.length - 1].date)}</span>
            </div>
          </div>
          <table className="sr-only">
            <caption>{t("Выпуск и брак по дням")}</caption>
            <thead>
              <tr>
                <th>{t("Дата")}</th>
                <th>{t("Годный выход, кг")}</th>
                <th>{t("Брак, кг")}</th>
              </tr>
            </thead>
            <tbody>
              {days.filter((d) => d.goodKg + d.defectKg > 0).map((d) => (
                <tr key={d.date}>
                  <td>{shortDay(d.date)}</td>
                  <td>{kg(d.goodKg)}</td>
                  <td>{kg(d.defectKg)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

/** Строки раздела: название, значение, мини-полоса доли от наибольшего; у продукта — брак рядом. */
export function BarRows({ rows, limit, extraLabel }: { rows: ProductionDashboard["byProduct"]; limit?: number; extraLabel?: string }) {
  const t = useT();
  const max = Math.max(0, ...rows.map((r) => r.value));
  return (
    <ul className="space-y-2.5">
      {(limit ? rows.slice(0, limit) : rows).map((r) => (
        <li key={r.name} className="text-[13px]">
          <div className="flex items-baseline justify-between gap-3">
            <span className="min-w-0 truncate text-ink">{r.name}</span>
            <span className="shrink-0 tabular-nums text-ink-2">
              {kg(r.value)} <span className="text-ink-3">{t(r.unit ?? "кг")}</span>
              {extraLabel && r.extra !== null && r.extra > 0 && <span className="text-[11px] text-bad"> · {t(extraLabel, { kg: kg(r.extra) })}</span>}
            </span>
          </div>
          <div className="mt-1 h-1 rounded-full bg-muted">
            <div className="h-1 rounded-full bg-accent" style={{ width: `${share(r.value, max)}%` }} />
          </div>
        </li>
      ))}
    </ul>
  );
}

/** Отклонения расхода: позиция, «норма · факт», процент (больше 5 % — красный). */
export function NormRows({ rows, limit }: { rows: ProductionDashboard["normFact"]; limit?: number }) {
  const t = useT();
  return (
    <ul className="divide-y divide-line">
      {(limit ? rows.slice(0, limit) : rows).map((n) => (
        <li key={n.name} className="flex items-center justify-between gap-3 py-2 text-[13px] first:pt-0 last:pb-0">
          <span className="min-w-0">
            <span className="block truncate text-ink">{n.name}</span>
            <span className="text-[11px] tabular-nums text-ink-3">
              {t("норма {a} · факт {b}", { a: `${kg(n.norm)} ${t(n.unit)}`, b: `${kg(n.fact)} ${t(n.unit)}` })}
            </span>
          </span>
          <span className={`shrink-0 tabular-nums ${(n.percent ?? 0) > 5 ? "font-semibold text-bad" : "text-ink-2"}`}>
            {n.percent === null ? "—" : `${n.percent > 0 ? "+" : n.percent < 0 ? "−" : ""}${Math.abs(n.percent).toLocaleString("ru-RU", { maximumFractionDigits: 1 })}%`}
          </span>
        </li>
      ))}
    </ul>
  );
}

/** Ссылка «Все →» в шапке карточки. */
export function AllLink({ href, label = "Все" }: { href: string; label?: string }) {
  const t = useT();
  return (
    <Link href={href} className="flex shrink-0 items-center gap-1 text-[12px] font-semibold text-accent-strong hover:underline">
      {t(label)}
      <ArrowRight aria-hidden className="size-[13px]" />
    </Link>
  );
}

const issueIcon: Record<string, LucideIcon> = { approval: Hourglass, shortage: PackageX, overconsumption: Scale, defect: CircleX };

/** «Требует внимания»: всё хорошо — зелёная плашка; иначе пункты с иконкой и цветом по типу (жёлтые — ждут утверждения и нехватка, красные — перерасход и брак). */
export function AttentionList({ issues, mobile = false }: { issues: ProductionDashboard["issues"]; mobile?: boolean }) {
  const t = useT();
  if (issues.length === 0) {
    return (
      <p className="flex items-start gap-2.5 rounded-[10px] bg-ok-soft px-3 py-2.5 text-[12px] leading-[18px] text-ink-2">
        <CircleCheck aria-hidden className="mt-px size-4 shrink-0 text-ok" />
        {t("Нехватки, перерасхода, брака выше норматива и ожидающих утверждения нет")}
      </p>
    );
  }
  return (
    <ul className="space-y-2">
      {issues.map((i) => {
        const Icon = issueIcon[i.kind] ?? CircleX;
        const warn = i.kind === "approval" || i.kind === "shortage";
        return (
          <li key={`${i.kind}-${i.link}-${i.title}`}>
            <Link
              href={i.link}
              className={`flex items-start gap-2.5 rounded-[10px] px-3 py-2.5 hover:brightness-110 ${warn ? "bg-warn-soft" : "bg-bad-soft"} ${mobile ? "" : ""}`}
            >
              <Icon aria-hidden className={`mt-px size-4 shrink-0 ${warn ? "text-warn" : "text-bad"}`} />
              <span className="min-w-0 flex-1">
                <span className="block text-[13px] font-medium text-ink">{t(i.title)}</span>
                <span className="block text-[12px] text-ink-3">{t(i.detail)}</span>
              </span>
              <ChevronRight aria-hidden className="mt-0.5 size-4 shrink-0 text-ink-4" />
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
