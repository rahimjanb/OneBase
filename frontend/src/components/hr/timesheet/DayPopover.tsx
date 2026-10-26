"use client";

import Link from "next/link";
import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { formatDate } from "@/i18n/core";
import { useLocale, useT } from "@/i18n/client";
import { clock, type HrEmployeeCard } from "@/lib/hr";
import { dayKind, dayKindLabel, decimal, toDetail, type DayDetail, type DayKind } from "@/lib/hr-timesheet";
import { isMockEmployee, mockDayDetail } from "@/lib/hr-timesheet.mock-day";

const badge: Record<DayKind, string> = {
  present: "bg-(--ts-pop-success-bg) text-(--ts-pop-success)",
  late: "bg-(--ts-pop-warning-bg) text-(--ts-pop-warning)",
  early: "bg-(--ts-pop-violet-bg) text-(--ts-pop-violet)",
  absent: "bg-(--ts-pop-danger-bg) text-(--ts-pop-danger)",
  dayoffWork: "bg-(--ts-pop-accent-bg) text-(--ts-pop-accent)",
  unknown: "bg-(--ts-pop-neutral-bg) text-(--ts-pop-muted)",
  document: "bg-(--ts-pop-neutral-bg) text-(--ts-pop-text)",
  dayoff: "bg-(--ts-pop-neutral-bg) text-(--ts-pop-muted)",
  holiday: "bg-(--ts-pop-neutral-bg) text-(--ts-pop-muted)",
  none: "",
};

/** Подробности дня — из итога дня в карточке сотрудника (отметки Verifix и табель); один раз на сотрудника и день. */
const cache = new Map<string, Promise<DayDetail>>();
function loadDetail(employeeId: string, date: string, code: string): Promise<DayDetail> {
  if (isMockEmployee(employeeId)) return Promise.resolve(mockDayDetail(code, date));
  const key = `${employeeId}|${date}`;
  let p = cache.get(key);
  if (!p) {
    p = fetch(`/bff/api/hr/employees/${employeeId}?from=${date}&to=${date}`, { cache: "no-store" })
      .then(async (r) => {
        if (!r.ok) throw new Error(String(r.status));
        const card = (await r.json()) as HrEmployeeCard;
        const day = card.days.find((d) => d.date === date);
        if (!day) throw new Error("no day");
        return toDetail(day);
      })
      .catch((e) => {
        cache.delete(key);
        throw e;
      });
    cache.set(key, p);
  }
  return p;
}

export type DayAnchor = { rowId: string; employee: string; date: string; code: string; rect: DOMRect; pinned: boolean; chip: HTMLElement; keyboard: boolean };

const WIDTH = 260;

/**
 * Карточка дня под ячейкой: дата и статус, приход (с опозданием), уход, график, отработано; ссылки на отметки сотрудника и на Verifix
 * (документы — отпуск, больничный — оформляются там). Открывается наведением или нажатием на чип; Escape закрывает.
 */
export function DayPopover({
  anchor,
  from,
  to,
  onClose,
  onPointerEnter,
  onPointerLeave,
}: {
  anchor: DayAnchor;
  from: string;
  to: string;
  onClose: () => void;
  onPointerEnter: () => void;
  onPointerLeave: () => void;
}) {
  const t = useT();
  const locale = useLocale();
  const box = useRef<HTMLDivElement>(null);
  const [detail, setDetail] = useState<{ key: string; value: DayDetail | null; failed: boolean } | null>(null);
  const [pos, setPos] = useState<{ left: number; top: number } | null>(null);
  const key = `${anchor.rowId}|${anchor.date}`;
  const kind = dayKind(anchor.code);

  useEffect(() => {
    let live = true;
    loadDetail(anchor.rowId, anchor.date, anchor.code)
      .then((value) => live && setDetail({ key, value, failed: false }))
      .catch(() => live && setDetail({ key, value: null, failed: true }));
    return () => {
      live = false;
    };
  }, [anchor.rowId, anchor.date, anchor.code, key]);

  // Под ячейкой; не помещается снизу — над ней; по горизонтали — в пределах экрана.
  useLayoutEffect(() => {
    const h = box.current?.offsetHeight ?? 200;
    const r = anchor.rect;
    const left = Math.max(8, Math.min(r.left + r.width / 2 - WIDTH / 2, window.innerWidth - WIDTH - 8));
    const below = r.bottom + 8;
    const top = below + h > window.innerHeight - 8 && r.top - 8 - h > 8 ? r.top - 8 - h : below;
    setPos({ left, top });
  }, [anchor.rect, detail]);

  // Открыта с клавиатуры — фокус в карточку (дальше Tab — к ссылкам); Escape закрывает и возвращает фокус на чип.
  useEffect(() => {
    if (anchor.keyboard) box.current?.focus();
  }, [anchor.keyboard, key]);

  useEffect(() => {
    const chip = anchor.chip;
    const onKey = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      onClose();
      chip.focus();
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose, anchor.chip]);

  const date = new Date(`${anchor.date}T12:00:00Z`);
  const head = formatDate(date, locale, { weekday: "short", day: "numeric", month: "long" });
  const title = head.charAt(0).toLocaleUpperCase(locale) + head.slice(1);
  const d = detail?.key === key ? detail.value : null;
  const loading = detail?.key !== key;
  const failed = detail?.key === key && detail.failed;
  const plan = d?.planBegin && d.planEnd ? `${clock(d.planBegin)} – ${clock(d.planEnd)}` : kind === "dayoff" || kind === "holiday" || kind === "dayoffWork" ? t("выходной") : "—";

  const Row = ({ label, value, extra }: { label: string; value: string; extra?: React.ReactNode }) => (
    <div className="flex items-center justify-between gap-3 text-[12.5px]">
      <span className="text-(--ts-pop-muted)">{label}</span>
      <span className="flex items-center gap-1.5 font-semibold text-(--ts-pop-text) tabular-nums">
        {value}
        {extra}
      </span>
    </div>
  );

  return (
    <div
      ref={box}
      role="dialog"
      tabIndex={-1}
      aria-label={t("{date} — {status}", { date: title, status: t(dayKindLabel[kind]) })}
      onPointerEnter={onPointerEnter}
      onPointerLeave={onPointerLeave}
      style={{ left: pos?.left ?? -9999, top: pos?.top ?? -9999, width: WIDTH }}
      className="fixed z-50 flex flex-col gap-2.5 rounded-[10px] outline-none focus-visible:ring-2 focus-visible:ring-(--ts-pop-link) border border-(--ts-pop-border) bg-(--ts-pop-bg) p-3.5 shadow-(--ts-pop-shadow)"
    >
      <div className="flex items-center justify-between gap-2">
        <span className="text-[13px] font-semibold text-(--ts-pop-text)">{title}</span>
        <span className={`rounded-[5px] px-1.5 py-0.5 text-[11px] font-semibold ${badge[kind]}`}>{t(dayKindLabel[kind])}</span>
      </div>
      {loading ? (
        <div className="flex flex-col gap-2" aria-hidden>
          {[0, 1, 2, 3].map((i) => (
            <span key={i} className="h-3.5 rounded bg-(--ts-pop-divider)" />
          ))}
        </div>
      ) : failed ? (
        <p className="text-[12.5px] text-(--ts-pop-muted)">{t("Не удалось загрузить подробности дня.")}</p>
      ) : (
        <div className="flex flex-col gap-2">
          <Row
            label={t("Приход")}
            value={clock(d?.firstIn)}
            extra={d?.lateMinutes ? <span className="text-(--ts-pop-warning)">{t("+{n} мин", { n: d.lateMinutes })}</span> : undefined}
          />
          <Row
            label={t("Уход")}
            value={clock(d?.lastOut)}
            extra={d?.earlyLeaveMinutes ? <span className="text-(--ts-pop-violet)">{t("−{n} мин", { n: d.earlyLeaveMinutes })}</span> : undefined}
          />
          <Row label={t("График")} value={plan} />
          <Row label={t("Отработано")} value={d?.workedMinutes != null ? t("{h} ч", { h: decimal(d.workedMinutes / 60, 1) }) : "—"} />
          {d?.leaveName && <Row label={t("Документ")} value={d.leaveName} />}
          {d?.reason && !d.firstIn && <p className="text-xs text-(--ts-pop-muted)">{t(d.reason)}</p>}
        </div>
      )}
      <div className="h-px bg-(--ts-pop-divider)" />
      <div className="flex items-center gap-4 text-xs font-semibold">
        {isMockEmployee(anchor.rowId) ? (
          <span className="text-(--ts-pop-link) opacity-70">{t("Отметки Verifix")}</span>
        ) : (
          <Link
            href={`/hr/employees/${anchor.rowId}?from=${from}&to=${to}`}
            className="rounded text-(--ts-pop-link) hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-pop-link)"
          >
            {t("Отметки Verifix")}
          </Link>
        )}
        <a
          href="https://app.verifix.com"
          target="_blank"
          rel="noopener noreferrer"
          title={t("Документы (отпуск, больничный, командировка) оформляются в Verifix")}
          className="rounded text-(--ts-pop-link) hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-(--ts-pop-link)"
        >
          {t("Добавить документ")}
        </a>
      </div>
    </div>
  );
}
