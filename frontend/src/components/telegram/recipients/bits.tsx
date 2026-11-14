"use client";

import { useEffect, useRef, useState } from "react";
import { useT } from "@/i18n/client";
import { lastSendOf, type MatrixCell } from "@/lib/telegram-recipients";
import type { ReportJob, ReportRecipient, ReportRole } from "@/lib/telegramReports";

/** Цвета ролей — как на картинках отчётов: РМ — синий, СВ — зелёный, ТП — оранжевый. */
export const ROLE_TONE: Record<ReportRole, { pill: string; bar: string; ring: string }> = {
  rm: { pill: "bg-accent-soft text-accent-strong", bar: "bg-accent-strong", ring: "border-accent" },
  sv: { pill: "bg-ok-soft text-ok", bar: "bg-ok", ring: "border-ok" },
  tp: { pill: "bg-warn-soft text-warn", bar: "bg-warn", ring: "border-warn" },
};

export const ROLE_LABEL: Record<ReportRole, string> = { rm: "РМ", sv: "СВ", tp: "ТП" };

export function RolePill({ role }: { role: ReportRole }) {
  const t = useT();
  return <span className={`inline-flex h-[18px] shrink-0 items-center rounded-[5px] px-1.5 text-[10px] font-bold ${ROLE_TONE[role].pill}`}>{t(ROLE_LABEL[role])}</span>;
}

export const initials = (name: string) =>
  name
    .split(/\s+/)
    .filter((w) => /\p{L}/u.test(w[0] ?? ""))
    .slice(0, 2)
    .map((w) => w[0].toUpperCase())
    .join("") || "?";

export function Avatar({ name, role, size = 34 }: { name: string; role: ReportRole; size?: number }) {
  return (
    <span aria-hidden style={{ width: size, height: size }} className={`grid shrink-0 place-items-center rounded-full text-[12px] font-bold ${ROLE_TONE[role].pill}`}>
      {initials(name)}
    </span>
  );
}

/** Цветная полоска роли перед счётчиком в сегментах. */
export const RoleBar = ({ role }: { role: ReportRole }) => <span aria-hidden className={`inline-block h-3 w-[3px] rounded-full ${ROLE_TONE[role].bar}`} />;

/** Квадраты отчётов 1–9: включён — синий, нет — серый; подсказка — название и запуски. */
export function ReportSquares({ cells, size = "md" }: { cells: MatrixCell[]; size?: "md" | "lg" }) {
  const t = useT();
  const on = cells.filter((c) => c.on);
  const label = on.length ? `${t("Отчёты")}: ${on.map((c) => `${c.n} ${t(c.title)}`).join(", ")}` : t("Отчёты не выбраны");
  return (
    <span role="img" aria-label={label} className="flex gap-[3px]">
      {cells.map((c) => (
        <span
          key={c.n}
          title={c.slots.length ? `${c.n}. ${t(c.title)}${c.on ? ` — ${c.slots.filter((s) => s.on).map((s) => s.time).join(", ")}` : ` — ${t("выключен")}`}` : `${c.n}. ${t(c.title)} — ${t("не для этой роли")}`}
          className={`grid shrink-0 place-items-center rounded-[5px] text-[10px] font-bold tabular-nums ${size === "lg" ? "h-5 w-[18px]" : "h-5 w-[17px]"} ${
            c.on ? "border border-accent/35 bg-accent-soft text-accent-strong" : c.slots.length ? "bg-muted text-ink-4" : "bg-muted/50 text-ink-4/50"
          }`}
        >
          {c.n}
        </span>
      ))}
    </span>
  );
}

/** Переключатель: role="switch"; tone ok — активность получателя (зелёный), accent — отчёт в панели. */
export function Toggle({
  checked,
  onChange,
  label,
  disabled,
  tone = "ok",
  size = "md",
}: {
  checked: boolean;
  onChange: (v: boolean) => void;
  label: string;
  disabled?: boolean;
  tone?: "ok" | "accent";
  size?: "sm" | "md" | "lg";
}) {
  const box = size === "sm" ? "h-4 w-7" : size === "lg" ? "h-[22px] w-10" : "h-5 w-9";
  const knob = size === "sm" ? "size-3" : size === "lg" ? "size-[18px]" : "size-4";
  const shift = size === "sm" ? "translate-x-[14px]" : size === "lg" ? "translate-x-5" : "translate-x-[18px]";
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={(e) => {
        e.stopPropagation();
        onChange(!checked);
      }}
      className={`relative inline-flex shrink-0 items-center rounded-full transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:opacity-40 ${box} ${
        checked ? (tone === "ok" ? "bg-ok" : "bg-accent") : "bg-line-strong"
      }`}
    >
      <span className={`inline-block rounded-full bg-white shadow transition-transform ${knob} ${checked ? shift : "translate-x-0.5"}`} />
    </button>
  );
}

/** Закрытие по клику мимо и Esc. */
export function useDismiss(open: boolean, onClose: () => void) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    const down = (e: MouseEvent | TouchEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) onClose();
    };
    const key = (e: KeyboardEvent) => {
      if (e.key === "Escape") {
        e.stopPropagation();
        onClose();
      }
    };
    document.addEventListener("mousedown", down);
    document.addEventListener("touchstart", down);
    document.addEventListener("keydown", key, true);
    return () => {
      document.removeEventListener("mousedown", down);
      document.removeEventListener("touchstart", down);
      document.removeEventListener("keydown", key, true);
    };
  }, [open, onClose]);
  return ref;
}

/** Выпадающая панель под кнопкой. */
export function Dropdown({
  trigger,
  children,
  align = "left",
  width = 280,
}: {
  trigger: (props: { open: boolean; toggle: () => void }) => React.ReactNode;
  children: (close: () => void) => React.ReactNode;
  align?: "left" | "right";
  width?: number;
}) {
  const [open, setOpen] = useState(false);
  const ref = useDismiss(open, () => setOpen(false));
  return (
    <div ref={ref} className="relative">
      {trigger({ open, toggle: () => setOpen((v) => !v) })}
      {open && (
        <div
          style={{ width }}
          className={`absolute top-full z-40 mt-1.5 max-h-[min(440px,70vh)] overflow-y-auto rounded-xl border border-line bg-surface-solid p-1.5 shadow-[0_12px_40px_rgb(0_0_0/0.35)] ${align === "right" ? "right-0" : "left-0"}`}
        >
          {children(() => setOpen(false))}
        </div>
      )}
    </div>
  );
}

const ddmm = (iso: string) => new Date(iso).toLocaleDateString("ru-RU", { day: "2-digit", month: "2-digit", timeZone: "Asia/Tashkent" });
const hhmm = (iso: string) => new Date(iso).toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit", timeZone: "Asia/Tashkent" });
const dayOf = (d: Date) => d.toLocaleDateString("sv-SE", { timeZone: "Asia/Tashkent" });

/** «Сегодня 18:00», «Вчера 13:00», «12.11 18:00». */
export function whenText(iso: string, t: (s: string) => string, now = new Date()) {
  const day = dayOf(new Date(iso));
  const label = day === dayOf(now) ? t("Сегодня") : day === dayOf(new Date(now.getTime() - 86_400_000)) ? t("Вчера") : ddmm(iso);
  return `${label} ${hhmm(iso)}`;
}

export { ddmm };

const DOT: Record<string, string> = { ok: "bg-ok", warn: "bg-warn", bad: "bg-bad", muted: "bg-ink-4", accent: "bg-accent-strong" };

/** Последняя отправка: точка, когда и что; пропуск — причина; выключен — с какого дня и почему. */
export function LastSendView({ r, jobs, compact }: { r: ReportRecipient; jobs: ReportJob[]; compact?: boolean }) {
  const t = useT();
  const s = lastSendOf(r, jobs);
  const what = s.job ? `${t(s.job.reportTitle).replace(/^\d+\.\s*/, "")} · ${t(s.job.slotTitle)}` : null;
  const reports = r.reports.length;
  const view = {
    sent: { tone: "ok", title: s.at ? whenText(s.at, t) : t("Отправлен"), detail: what ? `${what} · ${t("доставлено")}` : t("последняя доставка") },
    pending: { tone: "accent", title: t("В очереди"), detail: what ?? "" },
    skipped: { tone: "warn", title: t("Пропущено"), detail: [s.at ? whenText(s.at, t) : null, s.error ? t(s.error) : what].filter(Boolean).join(" · ") },
    failed: { tone: "bad", title: t("Ошибка"), detail: [s.at ? whenText(s.at, t) : null, s.error ? t(s.error) : what].filter(Boolean).join(" · ") },
    unknown: { tone: "bad", title: t("Неизвестно, дошёл ли"), detail: [s.at ? whenText(s.at, t) : null, what].filter(Boolean).join(" · ") },
    nochat: { tone: "warn", title: t("Не уходит"), detail: `${t("нет чата")} · ${t("{n} отч.", { n: reports })}` },
    off: { tone: "muted", title: t("Отключён"), detail: [s.at ? t("с {date}", { date: ddmm(s.at) }) : null, s.error, r.disabledUntil ? t("до {date}", { date: ddmm(r.disabledUntil) }) : null].filter(Boolean).join(" · ") },
    none: { tone: "muted", title: t("Не было"), detail: t("ещё не отправлялось") },
  }[s.kind];
  const tone = { ok: "text-ink", accent: "text-accent-strong", warn: "text-warn", bad: "text-bad", muted: "text-ink-3" }[view.tone];
  if (compact) {
    return (
      <span className={`flex min-w-0 items-center gap-1.5 text-[12px] font-semibold ${tone}`} title={view.detail}>
        <span aria-hidden className={`size-1.5 shrink-0 rounded-full ${DOT[view.tone]}`} />
        <span className="truncate">
          {s.kind === "nochat" || s.kind === "off" || s.kind === "none" ? view.title.toLowerCase() : s.kind === "sent" && s.at ? `${t("отправлено")} ${hhmm(s.at)}` : `${view.title.toLowerCase()}${s.at ? ` ${hhmm(s.at)}` : ""}`}
        </span>
      </span>
    );
  }

  return (
    <span className="block min-w-0">
      <span className={`flex items-center gap-1.5 text-[12px] font-semibold ${tone}`}>
        <span aria-hidden className={`size-1.5 shrink-0 rounded-full ${DOT[view.tone]}`} />
        <span className="truncate">{view.title}</span>
      </span>
      {view.detail && (
        <span className="block truncate pl-3 text-[10px] text-ink-4" title={view.detail}>
          {view.detail}
        </span>
      )}
    </span>
  );
}

/** Уведомление внизу с «Отменить» (5 с). */
export type ToastData = { id: number; text: string; tone?: "ok" | "bad"; undo?: () => void };

export function Toast({ toast, onClose }: { toast: ToastData | null; onClose: () => void }) {
  const t = useT();
  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(onClose, toast.undo ? 5000 : 4000);
    return () => clearTimeout(timer);
  }, [toast, onClose]);
  if (!toast) return null;
  return (
    <div role="status" aria-live="polite" className="pointer-events-none fixed inset-x-0 bottom-[calc(84px+env(safe-area-inset-bottom))] z-[60] flex justify-center px-4 lg:bottom-6">
      <div
        className={`pointer-events-auto flex max-w-[560px] items-center gap-3 rounded-xl border px-4 py-3 text-[13px] shadow-[0_12px_40px_rgb(0_0_0/0.4)] ${
          toast.tone === "bad" ? "border-bad/40 bg-surface-solid text-bad" : "border-line bg-surface-solid text-ink"
        }`}
      >
        <span className="min-w-0 flex-1">{toast.text}</span>
        {toast.undo && (
          <button
            type="button"
            className="shrink-0 font-semibold text-accent-strong hover:underline"
            onClick={() => {
              toast.undo!();
              onClose();
            }}
          >
            {t("Отменить")}
          </button>
        )}
      </div>
    </div>
  );
}
