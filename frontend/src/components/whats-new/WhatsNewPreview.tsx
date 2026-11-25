"use client";

import {
  CalendarDays,
  ChartLine,
  CircleCheck,
  CircleDot,
  Flag,
  Gift,
  Image as ImageIcon,
  Layers,
  LayoutGrid,
  Lock,
  MapPin,
  Maximize2,
  Moon,
  Quote,
  Repeat,
  Route,
  ScanSearch,
  Send,
  Shapes,
  ShieldCheck,
  Store,
  Target,
  Users,
  Wallet,
  type LucideIcon,
} from "lucide-react";
import { useT } from "@/i18n/client";
import type { WhatsNewColor, WhatsNewIcon } from "@/lib/whats-new";

export const STEP_ICON: Record<WhatsNewIcon, LucideIcon> = {
  battle: Flag,
  salesData: Layers,
  agentDay: Route,
  assortment: LayoutGrid,
  forecast: ChartLine,
  screening: ScanSearch,
  recipients: Send,
};

/** Цвет шага: текст и мягкий фон метки, переменная для свечения превью. */
export const COLOR: Record<WhatsNewColor, { text: string; soft: string; dot: string; border: string; glow: string }> = {
  violet: { text: "text-violet", soft: "bg-violet/15", dot: "bg-violet", border: "border-violet/60", glow: "var(--color-violet)" },
  accent: { text: "text-accent", soft: "bg-accent/15", dot: "bg-accent", border: "border-accent/60", glow: "var(--color-accent)" },
  ok: { text: "text-ok", soft: "bg-ok/15", dot: "bg-ok", border: "border-ok/60", glow: "var(--color-ok)" },
  warn: { text: "text-warn", soft: "bg-warn/15", dot: "bg-warn", border: "border-warn/60", glow: "var(--color-warn)" },
  cyan: { text: "text-accent-light", soft: "bg-accent-light/15", dot: "bg-accent-light", border: "border-accent-light/60", glow: "var(--color-accent-light)" },
};

/** Плашка без текста — место строки или числа: превью ничего не показывает из данных. */
function Line({ w, className = "" }: { w: string; className?: string }) {
  return <span aria-hidden className={`block h-2 rounded-full bg-line-strong ${className}`} style={{ width: w }} />;
}

function Chip({ icon: Icon, children, tone = "text-ink-2" }: { icon?: LucideIcon; children: React.ReactNode; tone?: string }) {
  return (
    <span className="inline-flex shrink-0 items-center gap-1.5 rounded-lg border border-line bg-muted px-2.5 py-1.5 text-[12px] font-medium text-ink-2 max-md:px-2 max-md:text-[11px]">
      {Icon && <Icon aria-hidden className={`size-[14px] ${tone}`} />}
      {children}
    </span>
  );
}

function Card({ icon: Icon, color, title, badge, children }: { icon: LucideIcon; color: WhatsNewColor; title: string; badge?: string; children: React.ReactNode }) {
  const c = COLOR[color];
  return (
    <div className="w-full max-w-[460px] rounded-[14px] border border-line-strong bg-surface-solid p-4 text-left shadow-[0_16px_40px_rgb(0_0_0/0.28)] max-md:max-w-[340px] max-md:p-3.5">
      <div className="flex items-center gap-2">
        <Icon aria-hidden className={`size-[17px] shrink-0 ${c.text}`} />
        <span className="min-w-0 flex-1 truncate text-[14px] font-semibold text-ink">{title}</span>
        {badge && (
          <span className={`inline-flex shrink-0 items-center gap-1.5 rounded-full px-2 py-0.5 text-[11px] font-semibold ${c.soft} ${c.text}`}>
            <span aria-hidden className={`size-1.5 rounded-full ${c.dot}`} />
            {badge}
          </span>
        )}
      </div>
      <div className="mt-3 flex flex-col gap-2.5">{children}</div>
    </div>
  );
}

/** Схема раздела над текстом шага: подписи разделов и формы, без имён и чисел (в 1Base нет выдуманных данных). */
export function WhatsNewPreview({ icon, color }: { icon: WhatsNewIcon; color: WhatsNewColor }) {
  const t = useT();
  const c = COLOR[color];
  switch (icon) {
    case "battle":
      return (
        <Card icon={Flag} color={color} title={t("Маршрут дня")} badge="RU · UZ">
          {[
            { goal: t("Могул"), icon: Moon, w: "52%" },
            { goal: "Twizos", icon: Gift, w: "40%" },
            { goal: t("Стойка"), icon: Store, w: "60%" },
          ].map((row) => (
            <div key={row.goal} className="flex items-center gap-3 rounded-[10px] bg-muted px-3 py-1.5">
              <span aria-hidden className="grid size-6 shrink-0 place-items-center rounded-full bg-line-strong/70">
                <MapPin className="size-3 text-ink-3" />
              </span>
              <Line w={row.w} className="flex-1" />
              <span className={`inline-flex shrink-0 items-center gap-1.5 rounded-full px-2 py-0.5 text-[11px] font-semibold ${c.soft} ${c.text}`}>
                <row.icon aria-hidden className="size-3" />
                {row.goal}
              </span>
            </div>
          ))}
          <div className="flex flex-wrap items-center gap-1.5">
            <Chip icon={Wallet} tone={c.text}>{t("Кешбек Могул")}</Chip>
            <Chip icon={Wallet} tone={c.text}>{t("Кешбек Twizos")}</Chip>
          </div>
        </Card>
      );
    case "salesData":
      return (
        <Card icon={Layers} color={color} title={t("Из чего складывается объём")} badge="Linko">
          {[
            { name: "Придзел", w: "72%" },
            { name: "Помадка", w: "54%" },
            { name: "Cripzel", w: "30%", own: true },
          ].map((row) => (
            <div key={row.name} className="flex items-center gap-3">
              <span className="flex w-[112px] shrink-0 items-center gap-1.5 truncate text-[12px] text-ink-2 max-md:w-[96px] max-md:text-[11px]">
                {row.name}
                {row.own && <span className={`rounded-full px-1.5 text-[10px] font-semibold ${c.soft} ${c.text}`}>{t("отдельно")}</span>}
              </span>
              <span className="relative h-1.5 flex-1 rounded-full bg-line-strong">
                <span aria-hidden className={`absolute inset-y-0 left-0 rounded-full ${c.dot}`} style={{ width: row.w }} />
              </span>
            </div>
          ))}
          <div className="flex flex-wrap items-center gap-1.5">
            <Chip icon={Target} tone={c.text}>{t("План АКБ")}</Chip>
            <Chip icon={Lock} tone={c.text}>{t("Бронь — подтверждённые")}</Chip>
          </div>
        </Card>
      );
    case "agentDay":
      return (
        <Card icon={Route} color={color} title={t("День ТП")}>
          <ol className="flex flex-col">
            {[
              { status: t("визит"), tone: "bg-ok/15 text-ok", w: "50%" },
              { status: t("визит"), tone: "bg-ok/15 text-ok", w: "38%" },
              { status: t("не посетил"), tone: "bg-bad/15 text-bad", w: "56%" },
              { status: t("заказ без визита"), tone: "bg-warn/15 text-warn", w: "44%" },
            ].map((row, i, rows) => (
              <li key={i} className="relative flex items-center gap-3 py-1.5">
                {i < rows.length - 1 && <span aria-hidden className="absolute left-[11px] top-[calc(50%+12px)] h-[calc(100%-12px)] w-px bg-line-strong" />}
                <span aria-hidden className={`grid size-6 shrink-0 place-items-center rounded-full border text-[11px] font-semibold ${i === 0 ? `${c.border} ${c.soft} ${c.text}` : "border-line-strong text-ink-3"}`}>
                  {i + 1}
                </span>
                <Line w={row.w} className="flex-1" />
                <span className={`shrink-0 rounded-full px-2 py-0.5 text-[11px] font-semibold ${row.tone}`}>{row.status}</span>
              </li>
            ))}
          </ol>
          <span className="flex items-center gap-1.5 text-[12px] text-ink-3 max-md:text-[11px]">
            <Repeat aria-hidden className="size-3.5" />
            {t("Какой визит после прошлого заказа")}
          </span>
        </Card>
      );
    case "assortment":
      return (
        <Card icon={LayoutGrid} color={color} title={t("Категория")}>
          <div className="flex w-fit rounded-lg border border-line bg-muted p-0.5 text-[11px] font-medium">
            <span className="rounded-md px-2.5 py-1 text-ink-3">{t("Таблица")}</span>
            <span className={`rounded-md px-2.5 py-1 ${c.soft} ${c.text}`}>{t("Карточки")}</span>
          </div>
          <div className="grid grid-cols-3 gap-2">
            {["62%", "48%", "56%"].map((w, i) => (
              <span key={i} className="flex flex-col gap-1.5 rounded-[10px] border border-line p-1.5">
                <span aria-hidden className="grid h-14 place-items-center rounded-md bg-line-strong/50 max-md:h-12">
                  <ImageIcon className="size-5 text-ink-3" />
                </span>
                <Line w={w} />
                <Line w="34%" className="opacity-60" />
              </span>
            ))}
          </div>
          <span className="flex items-center gap-1.5 text-[12px] text-ink-3 max-md:text-[11px]">
            <Maximize2 aria-hidden className="size-3.5" />
            {t("Фото на весь экран по нажатию")}
          </span>
        </Card>
      );
    case "forecast":
      return (
        <Card icon={ChartLine} color={color} title={t("Прогноз")} badge="P10 · P50 · P90">
          <svg viewBox="0 0 300 96" className="h-[104px] w-full max-md:h-[84px]" role="presentation" aria-hidden>
            {[24, 48, 72].map((y) => (
              <line key={y} x1="0" x2="300" y1={y} y2={y} stroke="var(--color-line)" strokeWidth="1" />
            ))}
            <path d="M170 52 L200 40 L230 44 L260 30 L300 34 L300 70 L260 66 L230 74 L200 66 L170 58 Z" fill={c.glow} opacity="0.2" />
            <path d="M0 70 L30 62 L60 66 L90 54 L120 58 L150 48 L170 55" fill="none" stroke="var(--color-ink-3)" strokeWidth="2" strokeLinejoin="round" strokeLinecap="round" />
            <path d="M170 55 L200 53 L230 59 L260 48 L300 52" fill="none" stroke={c.glow} strokeWidth="2" strokeDasharray="5 4" strokeLinejoin="round" strokeLinecap="round" />
            <line x1="170" x2="170" y1="8" y2="92" stroke="var(--color-line-strong)" strokeWidth="1.5" />
          </svg>
          <div className="flex flex-wrap items-center gap-1.5">
            <Chip icon={CalendarDays} tone={c.text}>{t("Сегодня")}</Chip>
            <Chip icon={Target} tone={c.text}>{t("Точность прошлых месяцев")}</Chip>
          </div>
        </Card>
      );
    case "screening":
      return (
        <Card icon={ScanSearch} color={color} title={t("AI-скрининг")} badge={t("Подходит")}>
          {[
            { name: t("Опыт"), w: "78%", met: true },
            { name: t("Навыки"), w: "56%", met: false },
          ].map((row) => (
            <div key={row.name} className="flex items-center gap-3">
              <span className="w-[88px] shrink-0 truncate text-[12px] text-ink-2 max-md:w-[72px] max-md:text-[11px]">{row.name}</span>
              <span className="relative h-1.5 flex-1 rounded-full bg-line-strong">
                <span aria-hidden className={`absolute inset-y-0 left-0 rounded-full ${c.dot}`} style={{ width: row.w }} />
              </span>
              {row.met ? <CircleCheck aria-hidden className="size-4 shrink-0 text-ok" /> : <CircleDot aria-hidden className="size-4 shrink-0 text-warn" />}
            </div>
          ))}
          <div className={`flex gap-2.5 rounded-[10px] border-l-2 bg-muted px-3 py-2.5 ${c.border}`}>
            <Quote aria-hidden className={`size-3.5 shrink-0 ${c.text}`} />
            <span className="flex min-w-0 flex-1 flex-col gap-1.5">
              <Line w="88%" />
              <Line w="62%" className="opacity-60" />
            </span>
          </div>
          <span className="flex items-center gap-1.5 text-[12px] text-ink-3 max-md:text-[11px]">
            <ShieldCheck aria-hidden className="size-3.5" />
            {t("Подтверждено фактами")}
          </span>
        </Card>
      );
    case "recipients":
      return (
        <Card icon={Send} color={color} title={t("Получатели")}>
          {[
            { on: [1, 1, 0, 1], off: false },
            { on: [1, 0, 1, 1], off: false },
            { on: [0, 0, 0, 0], off: true },
          ].map((row, i) => (
            <div key={i} className={`flex items-center gap-3 rounded-[10px] bg-muted px-3 py-2 ${row.off ? "opacity-70" : ""}`}>
              <span aria-hidden className="grid size-7 shrink-0 place-items-center rounded-full bg-line-strong/70">
                <Users className="size-3.5 text-ink-3" />
              </span>
              <Line w="34%" className="flex-1" />
              {row.off ? (
                <span className="shrink-0 rounded-full bg-warn/15 px-2 py-0.5 text-[11px] font-semibold text-warn">{t("Нет чата")}</span>
              ) : (
                <span aria-hidden className="flex shrink-0 gap-1">
                  {row.on.map((on, j) => (
                    <span key={j} className={`size-3.5 rounded-[4px] ${on ? c.dot : "border border-line-strong"}`} />
                  ))}
                </span>
              )}
            </div>
          ))}
          <span className="flex items-center gap-1.5 text-[12px] text-ink-3 max-md:text-[11px]">
            <Shapes aria-hidden className="size-3.5" />
            {t("Итоги дня по категориям")}
          </span>
        </Card>
      );
  }
}
