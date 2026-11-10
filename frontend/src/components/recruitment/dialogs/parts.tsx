"use client";

import Link from "next/link";
import { Hand, Settings, Sparkles, UserRound } from "lucide-react";
import { useT } from "@/i18n/client";
import type { DialogSummary } from "@/lib/hr-dialogs";
import { avatarTone, initials, type DialogMode, type Tone } from "@/lib/hr-dialogs-view";
import { when } from "@/lib/recruitment";

/** Общие детали «Диалогов Telegram»: аватар, чип режима, плашка номера HR, скелетоны. Цвета — токены палитры макета. */

const toneClass: Record<Tone, string> = {
  accent: "bg-accent-soft text-accent-strong",
  violet: "bg-violet-soft text-violet",
  ok: "bg-ok-soft text-ok",
  warn: "bg-warn-soft text-warn",
  bad: "bg-bad-soft text-bad",
};

export function Avatar({ id, name, size = 42, className = "" }: { id: string; name: string; size?: number; className?: string }) {
  return (
    <span
      aria-hidden
      className={`grid shrink-0 place-items-center rounded-full font-bold ${toneClass[avatarTone(id)]} ${className}`}
      style={{ width: size, height: size, fontSize: Math.round(size * 0.34) }}
    >
      {initials(name)}
    </span>
  );
}

const modeView: Record<DialogMode, { label: string; className: string; icon: typeof Sparkles }> = {
  ai: { label: "AI ведёт", className: "bg-violet-soft text-violet", icon: Sparkles },
  hr: { label: "HR ведёт", className: "bg-accent-soft text-accent-strong", icon: UserRound },
  need_hr: { label: "Нужен HR", className: "bg-warn-soft text-warn", icon: Hand },
};

/** Кто ведёт диалог — текстом и цветом (режим не передаётся одним цветом). */
export function ModeChip({ mode, large = false }: { mode: DialogMode; large?: boolean }) {
  const t = useT();
  const view = modeView[mode];
  const Icon = view.icon;
  return (
    <span className={`inline-flex shrink-0 items-center gap-1 rounded-md font-bold ${view.className} ${large ? "px-2 py-0.5 text-[11px]" : "px-1.5 py-0.5 text-[10px]"}`}>
      <Icon className={large ? "size-3" : "size-[11px]"} aria-hidden />
      {t(view.label)}
    </span>
  );
}

/**
 * Плашка «Номер HR подключён +998…» (или «не подключён»). Подключён, но клиент не запущен или Telegram остановил отправку — жёлтая, с причиной:
 * сообщения кандидатам в это время ждут в очереди.
 */
export function NumberPill({ summary, wide = false }: { summary: DialogSummary | null; wide?: boolean }) {
  const t = useT();
  const number = summary?.number;
  const connected = !!number?.connected;
  const issue = !connected
    ? null
    : number?.pausedUntil
      ? t("отправка остановлена до {date}", { date: when(number.pausedUntil) })
      : !number?.online
        ? t("клиент не запущен")
        : null;
  const fine = connected && !issue;
  return (
    <span
      className={`flex items-center gap-2 rounded-[10px] border px-3 py-2 text-xs ${
        fine ? "border-line bg-surface" : "border-warn/25 bg-warn-soft"
      } ${wide ? "w-full justify-between rounded-xl" : ""}`}
    >
      <span className="flex items-center gap-2">
        <span aria-hidden className={`size-2 rounded-full ${fine ? "bg-ok" : "bg-warn"}`} />
        <span className={`font-semibold ${fine ? "text-ink" : "text-warn"}`}>
          {t(connected ? "Номер HR подключён" : "Номер HR не подключён")}
          {issue && <span className="font-medium"> · {issue}</span>}
        </span>
      </span>
      {connected && (number?.phoneHint || number?.username) && (
        <span className="font-medium text-ink-3 tabular-nums">{number.phoneHint ?? `@${number.username}`}</span>
      )}
    </span>
  );
}

export function SettingsLink({ compact = false }: { compact?: boolean }) {
  const t = useT();
  return compact ? (
    <Link href="/settings/telegram" aria-label={t("Настройки Telegram")} className="grid size-[40px] place-items-center rounded-full bg-surface text-ink-2 hover:text-ink">
      <Settings className="size-5" />
    </Link>
  ) : (
    <Link href="/settings/telegram" className="flex items-center gap-2 rounded-[10px] border border-line bg-surface px-3 py-2 text-xs font-semibold text-ink hover:bg-muted">
      <Settings className="size-4" />
      {t("Настройки Telegram")}
    </Link>
  );
}

export function Pulse({ className }: { className: string }) {
  return <span aria-hidden className={`block animate-pulse rounded-md bg-muted ${className}`} />;
}

export function ListSkeleton({ rows = 8 }: { rows?: number }) {
  return (
    <div className="space-y-1 p-2" aria-busy="true">
      {Array.from({ length: rows }, (_, i) => (
        <div key={i} className="flex gap-3 rounded-xl p-2.5">
          <Pulse className="size-[42px] rounded-full" />
          <div className="flex-1 space-y-2 py-1">
            <Pulse className="h-3 w-2/3" />
            <Pulse className="h-3 w-full" />
            <Pulse className="h-3 w-1/3" />
          </div>
        </div>
      ))}
    </div>
  );
}

export function FeedSkeleton() {
  return (
    <div className="flex flex-1 flex-col justify-end gap-3 p-5" aria-busy="true">
      {["w-1/2", "ml-auto w-2/3", "w-1/3", "ml-auto w-1/2", "w-2/5"].map((w, i) => (
        <Pulse key={i} className={`h-12 rounded-2xl ${w}`} />
      ))}
    </div>
  );
}
