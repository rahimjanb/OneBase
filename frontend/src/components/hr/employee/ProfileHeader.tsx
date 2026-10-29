"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { ArrowLeft, Briefcase, Building2, CalendarClock, ChevronLeft, Copy, Download, EllipsisVertical, ExternalLink, Hash, type LucideIcon } from "lucide-react";
import { useT } from "@/i18n/client";
import type { HrEmployeeCard } from "@/lib/hr";
import { initialsOf, shortName } from "@/lib/hr-employee";
import { focus } from "../overview/bits";

/** Пилюля статуса: работает, уволен, в отпуске сегодня; «нет в Verifix» — отдельно. */
function StatusPill({ card }: { card: HrEmployeeCard }) {
  const t = useT();
  const e = card.employee;
  const pill =
    e.status === "D"
      ? { label: "Уволен", tone: "bg-(--ts-danger-soft) text-(--ts-danger)", dot: "bg-(--ts-danger)" }
      : e.status === "U"
        ? { label: "Не принят", tone: "bg-(--ts-surface-2) text-(--ts-muted)", dot: "bg-(--ts-subtle)" }
        : e.today === "OnLeave"
          ? { label: "В отпуске", tone: "bg-(--ts-violet-soft) text-(--ts-violet)", dot: "bg-(--ts-violet)" }
          : { label: "Работает", tone: "bg-(--ts-success-soft) text-(--ts-success)", dot: "bg-(--ts-success)" };
  return (
    <>
      <span className={`inline-flex h-6 shrink-0 items-center gap-1.5 rounded-full px-2.5 text-xs font-semibold ${pill.tone}`}>
        <span aria-hidden className={`size-1.5 rounded-full ${pill.dot}`} />
        {t(pill.label)}
      </span>
      {e.missingSince && (
        <span className="inline-flex h-6 shrink-0 items-center rounded-full bg-(--ts-warning-soft) px-2.5 text-xs font-semibold text-(--ts-warning)">{t("нет в Verifix")}</span>
      )}
    </>
  );
}

/**
 * Шапка карточки: «← Сотрудники» (назад к списку с его фильтрами), аватар с инициалами (фото Verifix не загружается), ФИО в нормальном регистре,
 * статус, должность · подразделение · график · ID Verifix; «Открыть в Verifix» и «Табель за период». На телефоне — своя строка «‹ Сотрудники»
 * с Verifix и меню «⋮» (табель, копировать ID), а табель — ещё и кнопкой внизу страницы.
 */
export function ProfileHeader({ card, name, backHref, reportHref }: { card: HrEmployeeCard; name: string; backHref: string; reportHref: string | null }) {
  const t = useT();
  const e = card.employee;
  const meta: [LucideIcon, string | null][] = [
    [Briefcase, e.job],
    [Building2, e.division],
    [CalendarClock, e.schedule],
    [Hash, `Verifix ${e.verifixId}`],
  ];
  const verifix = card.verifixUrl;

  return (
    <header className="flex flex-col gap-4 md:gap-5">
      {/* Телефон: строка «назад» и действия. */}
      <div className="-mx-4 flex h-14 items-center justify-between border-b border-(--ts-border) px-2 md:hidden">
        <Link href={backHref} className={`inline-flex h-10 items-center gap-1 rounded-[10px] px-2 text-[15px] font-medium text-(--ts-text-2) ${focus}`}>
          <ChevronLeft aria-hidden className="size-5" />
          {t("Сотрудники")}
        </Link>
        <span className="flex items-center gap-1">
          {verifix && (
            <a href={verifix} target="_blank" rel="noreferrer" aria-label={t("Открыть в Verifix")} className={`grid size-10 place-items-center rounded-[10px] text-(--ts-muted) ${focus}`}>
              <ExternalLink className="size-5" />
            </a>
          )}
          <MobileMenu verifixId={e.verifixId} reportHref={reportHref} />
        </span>
      </div>

      <nav aria-label={t("Навигация")} className="flex items-center gap-1.5 text-[13px] font-medium max-md:hidden">
        <Link href={backHref} className={`inline-flex items-center gap-1.5 rounded text-(--ts-muted) hover:text-(--ts-text-2) ${focus}`}>
          <ArrowLeft aria-hidden className="size-3.5" />
          {t("Сотрудники")}
        </Link>
        <span aria-hidden className="text-(--ts-subtle)">
          /
        </span>
        <span className="truncate text-(--ts-text-2)">{shortName(name)}</span>
      </nav>

      <div className="flex items-center justify-between gap-x-6 gap-y-4 max-md:flex-wrap">
        <div className="flex min-w-0 flex-1 items-center gap-4">
          <span
            aria-hidden
            className="grid size-14 shrink-0 place-items-center rounded-full border border-(--ts-accent)/25 bg-(--ts-accent-soft) text-lg font-bold text-(--ts-accent) md:size-[60px] md:text-xl"
          >
            {initialsOf(name)}
          </span>
          <div className="flex min-w-0 flex-col gap-1.5">
            <div className="flex flex-wrap items-center gap-x-3 gap-y-1.5">
              <h1 className="text-[19px] font-bold leading-tight text-(--ts-text) md:text-[26px] md:tracking-[-0.4px]">{name}</h1>
              <span className="flex items-center gap-1.5 max-md:hidden">
                <StatusPill card={card} />
              </span>
            </div>
            <div className="flex flex-wrap items-center gap-2 md:hidden">
              <StatusPill card={card} />
              <span className="text-[13px] text-(--ts-muted)">{[e.job, e.division].filter(Boolean).join(" · ")}</span>
            </div>
            <ul className="flex flex-wrap items-center gap-x-4 gap-y-1 max-md:hidden">
              {meta
                .filter(([, text]) => text)
                .map(([Icon, text]) => (
                  <li key={text} className="flex min-w-0 items-center gap-1.5 text-[13px] font-medium text-(--ts-muted)">
                    <Icon aria-hidden className="size-3.5 shrink-0 text-(--ts-subtle)" />
                    <span className="truncate">{text}</span>
                  </li>
                ))}
            </ul>
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-2.5 max-md:hidden">
          {verifix && (
            <a
              href={verifix}
              target="_blank"
              rel="noreferrer"
              title={t("Сотрудник в Verifix: ID {id}", { id: e.verifixId })}
              className={`inline-flex h-10 items-center gap-2 rounded-[10px] border border-(--ts-border-strong) bg-(--ts-surface) px-3.5 text-sm font-medium text-(--ts-text) transition-colors duration-150 hover:bg-(--ts-surface-2) ${focus}`}
            >
              <ExternalLink aria-hidden className="size-4 text-(--ts-muted)" />
              {t("Открыть в Verifix")}
            </a>
          )}
          <ReportButton href={reportHref} className="h-10 px-4" />
        </div>
      </div>
    </header>
  );
}

/** «Табель за период» — Excel за выбранный период по этому сотруднику; в демо — неактивна. */
export function ReportButton({ href, className, label = "Табель за период" }: { href: string | null; className: string; label?: string }) {
  const t = useT();
  const base = `inline-flex items-center justify-center gap-2 rounded-[10px] bg-(--ts-accent) text-sm font-semibold text-(--ts-on-accent) transition-opacity duration-150 hover:opacity-90 ${focus} ${className}`;
  return href ? (
    <a href={href} download className={base}>
      <Download aria-hidden className="size-4" />
      {t(label)}
    </a>
  ) : (
    <span aria-disabled title={t("Выгрузка недоступна в демо-режиме")} className={`${base} opacity-60`}>
      <Download aria-hidden className="size-4" />
      {t(label)}
    </span>
  );
}

function MobileMenu({ verifixId, reportHref }: { verifixId: number; reportHref: string | null }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  const [copied, setCopied] = useState(false);
  const box = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onDown = (e: PointerEvent) => !box.current?.contains(e.target as Node) && setOpen(false);
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && setOpen(false);
    document.addEventListener("pointerdown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(String(verifixId));
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      // буфер обмена недоступен (http) — ID виден в «Данных Verifix»
    }
  };

  const item = `flex h-11 w-full items-center gap-3 rounded-[10px] px-3 text-left text-sm font-medium text-(--ts-text) hover:bg-(--ts-surface-2) ${focus}`;
  return (
    <div ref={box} className="relative">
      <button type="button" aria-label={t("Действия")} aria-expanded={open} onClick={() => setOpen((v) => !v)} className={`grid size-10 place-items-center rounded-[10px] text-(--ts-muted) ${focus}`}>
        <EllipsisVertical className="size-5" />
      </button>
      {open && (
        <div role="menu" className="absolute right-0 top-[calc(100%+4px)] z-40 w-60 rounded-[14px] border border-(--ts-border) bg-(--ts-surface) p-1.5 shadow-[0_12px_32px_#0e13221f]">
          {reportHref ? (
            <a role="menuitem" href={reportHref} download className={item}>
              <Download aria-hidden className="size-4 text-(--ts-muted)" />
              {t("Табель за период")}
            </a>
          ) : (
            <span role="menuitem" aria-disabled className={`${item} opacity-60`}>
              <Download aria-hidden className="size-4 text-(--ts-muted)" />
              {t("Табель за период")}
            </span>
          )}
          <button role="menuitem" type="button" onClick={copy} className={item}>
            <Copy aria-hidden className="size-4 text-(--ts-muted)" />
            {copied ? t("Скопировано") : t("Копировать ID Verifix")}
          </button>
        </div>
      )}
    </div>
  );
}
