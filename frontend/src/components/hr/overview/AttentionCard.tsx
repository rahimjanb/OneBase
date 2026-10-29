"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { ArrowRight, CircleCheck } from "lucide-react";
import { useT } from "@/i18n/client";
import type { HrAttention } from "@/lib/hr";
import { displayName } from "@/lib/hr-employee";
import { attentionMeta, attentionTime, initials, num, toneSoft, toneText } from "@/lib/hr-overview";
import { card, focus } from "./bits";

/** Строк до замера и на телефоне. */
const MOBILE_ROWS = 4;

/**
 * «Требует внимания»: отсутствующие, опоздавшие (сильнее — выше) и «не ясно» — порядок задаёт сервер. Счётчик — все такие сотрудники,
 * «Все →» — «Опоздания и отсутствия» за этот день. На ноутбуке высоту задаёт «Явка»: строк столько, сколько помещается целиком.
 */
export function AttentionCard({ rows, total, allHref }: { rows: HrAttention[]; total: number; allHref: string }) {
  const t = useT();
  const list = useRef<HTMLUListElement>(null);
  const [fit, setFit] = useState(MOBILE_ROWS);

  useEffect(() => {
    const el = list.current;
    if (!el) return;
    const wide = window.matchMedia("(min-width: 1024px)");
    const measure = () => {
      if (!wide.matches) return setFit(MOBILE_ROWS);
      const row = el.querySelector("li")?.getBoundingClientRect().height || 66;
      setFit(Math.max(1, Math.floor((el.clientHeight + 0.5) / row)));
    };
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(el);
    wide.addEventListener("change", measure);
    return () => {
      observer.disconnect();
      wide.removeEventListener("change", measure);
    };
  }, []);

  return (
    <section aria-labelledby="hr-warn" className={`${card} flex h-full min-h-0 flex-col overflow-hidden`}>
      <div className="flex items-center justify-between gap-3 px-5 pb-3 pt-5 lg:pb-3.5">
        <div className="flex min-w-0 items-center gap-2.5">
          <h2 id="hr-warn" className="truncate text-base font-semibold text-(--ts-text)">
            {t("Требует внимания")}
          </h2>
          {total > 0 && (
            <span className="rounded-[7px] bg-(--ts-danger-soft) px-2 py-0.5 text-xs font-semibold text-(--ts-danger)" aria-label={t("всего: {n}", { n: total })}>
              {num(total)}
            </span>
          )}
        </div>
        <Link href={allHref} className={`inline-flex shrink-0 items-center gap-1 rounded text-[13px] font-medium text-(--ts-link) hover:underline ${focus}`}>
          {t("Все")}
          <ArrowRight aria-hidden className="size-3.5" />
        </Link>
      </div>

      {rows.length === 0 ? (
        <div className="flex flex-1 flex-col items-center justify-center gap-2 border-t border-(--ts-border) px-5 py-8 text-center">
          <CircleCheck aria-hidden className="size-6 text-(--ts-success)" />
          <p className="text-sm font-medium text-(--ts-text-2)">{t("Всё в порядке")}</p>
        </div>
      ) : (
        <ul ref={list} className="min-h-0 flex-1 overflow-hidden">
          {rows.slice(0, fit).map((a) => {
            const meta = attentionMeta[a.kind];
            return (
              <li key={a.employeeId} className="border-t border-(--ts-border)">
                <Link
                  href={`/hr/employees/${a.employeeId}`}
                  className={`flex items-center gap-3 px-5 py-2.5 transition-colors duration-150 hover:bg-(--ts-row-hover) focus-visible:bg-(--ts-row-hover) focus-visible:outline-none`}
                >
                  <span aria-hidden className={`grid size-[34px] shrink-0 place-items-center rounded-full text-xs font-semibold ${toneSoft[meta.tone]} ${toneText[meta.tone]}`}>
                    {initials(displayName(a.employee))}
                  </span>
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate text-sm font-medium text-(--ts-text)">{displayName(a.employee)}</span>
                    {a.division && <span className="truncate text-xs text-(--ts-subtle)">{a.division}</span>}
                  </span>
                  <span className="flex shrink-0 flex-col items-end gap-1">
                    <span className={`rounded-[6px] px-2 py-0.5 text-[11px] font-semibold ${toneSoft[meta.tone]} ${toneText[meta.tone]}`}>{t(meta.label)}</span>
                    <span className="text-xs tabular-nums text-(--ts-muted)">
                      <span className="lg:hidden">{attentionTime(a, t, true)}</span>
                      <span className="max-lg:hidden">{attentionTime(a, t)}</span>
                    </span>
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
