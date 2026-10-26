"use client";

import { useT } from "@/i18n/client";
import type { HrSyncBrief } from "@/lib/hr";

export type SyncTone = "success" | "warning" | "danger" | "accent";

const tones: Record<SyncTone, string> = {
  success: "bg-(--ts-success-soft) text-(--ts-success)",
  warning: "bg-(--ts-warning-soft) text-(--ts-warning)",
  danger: "bg-(--ts-danger-soft) text-(--ts-danger)",
  accent: "bg-(--ts-accent-soft) text-(--ts-accent)",
};

const dots: Record<SyncTone, string> = {
  success: "bg-(--ts-success)",
  warning: "bg-(--ts-warning)",
  danger: "bg-(--ts-danger)",
  accent: "bg-(--ts-accent)",
};

/** «26.10 в 21:12» по Ташкенту. */
const at = (iso: string) => {
  const parts = new Intl.DateTimeFormat("ru-RU", { timeZone: "Asia/Tashkent", day: "2-digit", month: "2-digit", hour: "2-digit", minute: "2-digit" }).formatToParts(new Date(iso));
  const get = (type: string) => parts.find((p) => p.type === type)?.value ?? "";
  return { date: `${get("day")}.${get("month")}`, time: `${get("hour")}:${get("minute")}` };
};

/**
 * Состояние данных Verifix: успешно — зелёный; синхронизация старше часа или не настроена — жёлтый; последняя закончилась ошибкой —
 * красный с текстом ошибки; идёт — синий.
 */
export function syncState(sync: HrSyncBrief, now: number): { tone: SyncTone; text: string; vars?: Record<string, string> } {
  if (!sync.configured) return { tone: "warning", text: "Verifix не подключён" };
  if (!sync.enabled) return { tone: "warning", text: "Verifix · интеграция выключена" };
  if (sync.running) return { tone: "accent", text: "Verifix · идёт синхронизация" };
  if (sync.lastStatus === "Failed") return { tone: "danger", text: "Verifix · ошибка: {message}", vars: { message: sync.lastMessage ?? "синхронизация не прошла" } };
  if (!sync.lastSuccessAt) return { tone: "warning", text: "Verifix · синхронизации ещё не было" };
  const { date, time } = at(sync.lastSuccessAt);
  const stale = now - Date.parse(sync.lastSuccessAt) > 60 * 60 * 1000;
  return { tone: stale || sync.lastStatus === "Partial" ? "warning" : "success", text: "Verifix · синхронизировано {date} в {time}", vars: { date, time } };
}

export function SyncStatus({ sync, now }: { sync: HrSyncBrief; now: number }) {
  const t = useT();
  const s = syncState(sync, now);
  const text = t(s.text, s.vars);
  return (
    <span title={text} className={`inline-flex h-[26px] max-w-full min-w-0 items-center gap-1.5 rounded-[13px] px-2.5 text-xs font-medium ${tones[s.tone]}`}>
      <span aria-hidden className={`size-1.5 shrink-0 rounded-full ${dots[s.tone]}`} />
      <span className="truncate">{text}</span>
    </span>
  );
}
