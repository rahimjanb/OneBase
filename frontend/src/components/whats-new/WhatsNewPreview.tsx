"use client";

import {
  Bot,
  BrainCircuit,
  Boxes,
  Building2,
  CalendarCheck,
  ChevronRight,
  CircleCheck,
  Clock3,
  Database,
  Factory,
  FileText,
  Gift,
  LineChart,
  MessagesSquare,
  ScanBarcode,
  Scale,
  Send,
  ShieldCheck,
  ShoppingBag,
  Table2,
  UserPlus,
  Warehouse,
  type LucideIcon,
} from "lucide-react";
import { useT } from "@/i18n/client";
import type { WhatsNewColor, WhatsNewIcon } from "@/lib/whats-new";

export const STEP_ICON: Record<WhatsNewIcon, LucideIcon> = {
  analytik: BrainCircuit,
  recruitment: UserPlus,
  production: Factory,
  twin: Boxes,
  procurement: Warehouse,
  agents: Bot,
  incentives: Gift,
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

const Arrow = () => <ChevronRight aria-hidden className="size-3.5 shrink-0 text-ink-4" />;

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
    case "analytik":
      return (
        <Card icon={BrainCircuit} color={color} title={t("Вопрос Аналитику")} badge={t("AI")}>
          <div className={`rounded-[10px] border px-3 py-2.5 text-[13px] leading-snug text-ink max-md:text-[12px] ${c.border}`}>
            {t("Сравни продажи по регионам за неделю и покажи, где план не выполняется")}
          </div>
          <div className="flex flex-wrap items-center gap-1.5">
            <Chip icon={Database} tone={c.text}>{t("Продажи")}</Chip>
            <Arrow />
            <Chip icon={Table2} tone={c.text}>{t("Срез")}</Chip>
            <Arrow />
            <Chip icon={LineChart} tone={c.text}>{t("Прогноз")}</Chip>
          </div>
          <span className="flex items-center gap-1.5 text-[12px] text-ink-3 max-md:text-[11px]">
            <FileText aria-hidden className="size-3.5" />
            {t("У каждой цифры — источник")}
          </span>
        </Card>
      );
    case "recruitment":
      return (
        <Card icon={MessagesSquare} color={color} title={t("Диалоги Telegram")}>
          {[
            { label: t("AI ведёт"), dot: "bg-violet", w: "62%" },
            { label: t("HR ведёт"), dot: "bg-accent", w: "48%" },
            { label: t("Нужен HR"), dot: "bg-warn", w: "55%" },
          ].map((row) => (
            <div key={row.label} className="flex items-center gap-3 rounded-[10px] bg-muted px-3 py-2">
              <span aria-hidden className="grid size-7 shrink-0 place-items-center rounded-full bg-line-strong/70">
                <UserPlus className="size-3.5 text-ink-3" />
              </span>
              <span className="flex min-w-0 flex-1 flex-col gap-1.5">
                <Line w="38%" />
                <Line w={row.w} className="opacity-60" />
              </span>
              <span className="inline-flex shrink-0 items-center gap-1.5 text-[11px] font-semibold text-ink-2">
                <span aria-hidden className={`size-1.5 rounded-full ${row.dot}`} />
                {row.label}
              </span>
            </div>
          ))}
          <span className="flex items-center gap-1.5 text-[12px] text-ink-3 max-md:text-[11px]">
            <CalendarCheck aria-hidden className="size-3.5" />
            {t("Собеседование — в календаре интервьюера")}
          </span>
        </Card>
      );
    case "production":
      return (
        <Card icon={Factory} color={color} title={t("Приезд сырья")}>
          <div className="flex flex-wrap items-center gap-1.5">
            <Chip icon={ShieldCheck} tone="text-ok">{t("Качество")}</Chip>
            <Arrow />
            <Chip icon={Scale} tone="text-ok">{t("Вес")}</Chip>
            <Arrow />
            <Chip icon={Warehouse} tone={c.text}>{t("Склад")}</Chip>
          </div>
          <div className="rounded-[10px] bg-muted px-3 py-2.5">
            <span className="text-[12px] font-medium text-ink-2">{t("Техкарта")}</span>
            <div className="mt-2 grid grid-cols-3 gap-1.5">
              {[t("Замес"), t("Выпечка"), t("Упаковка")].map((stage, i) => (
                <span key={stage} className="flex flex-col gap-1.5">
                  <span aria-hidden className={`h-1.5 rounded-full ${i === 0 ? c.dot : "bg-line-strong"}`} />
                  <span className="truncate text-[11px] text-ink-3">{stage}</span>
                </span>
              ))}
            </div>
          </div>
          <span className="flex items-center gap-1.5 text-[12px] text-ink-3 max-md:text-[11px]">
            <Clock3 aria-hidden className="size-3.5" />
            {t("Партии — со сроками годности")}
          </span>
        </Card>
      );
    case "twin":
      return (
        <Card icon={Boxes} color={color} title={t("Цифровой двойник")} badge="3D">
          <svg viewBox="30 20 250 92" className="h-[118px] w-full max-md:h-[96px]" role="presentation" aria-hidden>
            {[
              { x: 40, y: 58, w: 70, h: 34, glow: true },
              { x: 120, y: 40, w: 60, h: 52, glow: false },
              { x: 192, y: 62, w: 74, h: 28, glow: false },
            ].map((b, i) => (
              <g key={i}>
                <polygon points={`${b.x},${b.y} ${b.x + b.w / 2},${b.y - 14} ${b.x + b.w},${b.y} ${b.x + b.w / 2},${b.y + 14}`} fill={b.glow ? c.glow : "var(--color-line-strong)"} opacity={b.glow ? 0.9 : 0.8} />
                <polygon points={`${b.x},${b.y} ${b.x + b.w / 2},${b.y + 14} ${b.x + b.w / 2},${b.y + 14 + b.h} ${b.x},${b.y + b.h}`} fill={b.glow ? c.glow : "var(--color-line)"} opacity={b.glow ? 0.55 : 1} />
                <polygon points={`${b.x + b.w},${b.y} ${b.x + b.w / 2},${b.y + 14} ${b.x + b.w / 2},${b.y + 14 + b.h} ${b.x + b.w},${b.y + b.h}`} fill={b.glow ? c.glow : "var(--color-muted)"} opacity={b.glow ? 0.35 : 1} />
              </g>
            ))}
          </svg>
          <div className="flex flex-wrap items-center gap-1.5">
            <Chip icon={Building2} tone={c.text}>{t("Отделы")}</Chip>
            <Chip icon={Warehouse} tone={c.text}>{t("Склады")}</Chip>
            <Chip icon={LineChart} tone={c.text}>{t("Показатели")}</Chip>
          </div>
        </Card>
      );
    case "procurement":
      return (
        <Card icon={ShoppingBag} color={color} title={t("Заявка на закупку")}>
          <div className="flex flex-wrap items-center gap-1.5">
            <Chip icon={CircleCheck} tone="text-ok">{t("Руководитель")}</Chip>
            <Arrow />
            <Chip icon={CircleCheck} tone="text-ok">{t("Финансист")}</Chip>
            <Arrow />
            <Chip icon={ShoppingBag} tone={c.text}>{t("Куплено")}</Chip>
          </div>
          <div className="rounded-[10px] bg-muted px-3 py-2.5">
            <div className="flex items-center justify-between gap-3">
              <Line w="40%" />
              <span className="text-[11px] text-ink-3">{t("минимум")}</span>
            </div>
            <div className="relative mt-2 h-1.5 rounded-full bg-line-strong">
              <span aria-hidden className={`absolute inset-y-0 left-0 w-[34%] rounded-full ${c.dot}`} />
              <span aria-hidden className="absolute -top-1 left-[46%] h-3.5 w-0.5 rounded-full bg-ink-3" />
            </div>
          </div>
          <span className="flex items-center gap-1.5 text-[12px] text-ink-3 max-md:text-[11px]">
            <ScanBarcode aria-hidden className="size-3.5" />
            {t("Приёмка по штрихкоду камерой")}
          </span>
        </Card>
      );
    case "agents":
      return (
        <Card icon={Bot} color={color} title={t("ИИ-агенты отделов")}>
          {[t("Продажи"), t("HR"), t("Снабжение")].map((name) => (
            <div key={name} className="flex items-center gap-3 rounded-[10px] bg-muted px-3 py-2">
              <span aria-hidden className={`grid size-7 shrink-0 place-items-center rounded-full ${c.soft}`}>
                <Bot className={`size-3.5 ${c.text}`} />
              </span>
              <span className="min-w-0 flex-1 truncate text-[13px] font-medium text-ink max-md:text-[12px]">{name}</span>
              <span className="inline-flex shrink-0 items-center gap-1.5 text-[11px] text-ink-3">
                <Send aria-hidden className="size-3.5 text-accent" />
                {t("свой номер")}
              </span>
            </div>
          ))}
        </Card>
      );
    case "incentives":
      return (
        <Card icon={Gift} color={color} title={t("Карточка ТП")} badge="RU · UZ">
          <div className="flex flex-wrap items-center gap-1.5">
            {["08:30", "13:00", "16:00", "18:00"].map((time) => (
              <Chip key={time} icon={Clock3} tone={c.text}>
                {time}
              </Chip>
            ))}
          </div>
          {[
            { label: t("Цели маршрута"), w: "58%" },
            { label: t("Спящие точки"), w: "34%" },
            { label: t("Кэшбек"), w: "46%" },
          ].map((row) => (
            <div key={row.label} className="flex items-center gap-3">
              <span className="w-[112px] shrink-0 truncate text-[12px] text-ink-2 max-md:w-[96px] max-md:text-[11px]">{row.label}</span>
              <span className="relative h-1.5 flex-1 rounded-full bg-line-strong">
                <span aria-hidden className={`absolute inset-y-0 left-0 rounded-full ${c.dot}`} style={{ width: row.w }} />
              </span>
            </div>
          ))}
          <span className="flex items-center gap-1.5 text-[12px] text-ink-3 max-md:text-[11px]">
            <Send aria-hidden className="size-3.5" />
            {t("Каждому ТП — в Telegram")}
          </span>
        </Card>
      );
  }
}
