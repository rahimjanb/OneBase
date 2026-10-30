"use client";

import Link from "next/link";
import { AlertTriangle, CheckCircle2, ChevronRight, Lightbulb, MessageCircle, Sparkles } from "lucide-react";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import type { FocusItem, Severity } from "@/lib/dashboard-mobile";
import { useHomeFilters } from "./Filters";
import m from "./mobile.module.css";
import { card, focusRing, press } from "./ui";

const tone: Record<Severity, { label: string; text: string; soft: string; dot: string }> = {
  critical: { label: "Критично", text: "text-(--ts-danger)", soft: "bg-(--ts-danger-soft)", dot: "bg-(--ts-danger)" },
  warning: { label: "Важно", text: "text-(--ts-warning)", soft: "bg-(--ts-warning-soft)", dot: "bg-(--ts-warning)" },
  info: { label: "Возможность", text: "text-(--ts-accent)", soft: "bg-(--ts-accent-soft)", dot: "bg-(--ts-accent)" },
};

const SHOW = 3;

/**
 * «Фокус дня» — находки AI-консультанта: до трёх, по важности (критично → важно → возможность), по выбранным в фильтре отделам.
 * Слева — ключевая цифра находки, тап открывает связанный раздел. items = null — находки недоступны (нет права или сервис не ответил),
 * блока нет; находок нет — «Сегодня всё спокойно».
 */
export function FocusCard({ items, departments, activeTasks }: { items: FocusItem[] | null; departments: number; activeTasks: number }) {
  const t = useT();
  const [filters] = useHomeFilters();
  if (!items) return null;
  const list = filters.departments.length ? items.filter((i) => filters.departments.includes(i.category)) : items;
  const shown = list.slice(0, SHOW);
  const more = list.length - shown.length;
  const question = shown.length
    ? t("Разбери находки «Фокуса дня» и подскажи, что сделать сегодня: {list}", { list: shown.map((i) => `«${i.text}»`).join("; ") })
    : t("Что сегодня стоит проверить в компании?");

  return (
    <section aria-labelledby="focus-title" className={`${card} ${m.focus} pb-[6px] pt-[18px]`}>
      <div className="flex items-center gap-[12px] px-[18px] pb-[14px]">
        <span className="grid size-[40px] shrink-0 place-items-center rounded-[12px] bg-(--ts-violet-soft) text-(--ts-violet)">
          <Sparkles aria-hidden className="size-[20px]" strokeWidth={2} />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-[8px]">
            <h2 id="focus-title" className="text-[16px] font-bold leading-tight text-(--ts-text)">
              {t("Фокус дня")}
            </h2>
            {list.length > 0 && <span className="rounded-[6px] bg-(--ts-violet-soft) px-[6px] py-[2px] text-[11px] font-semibold text-(--ts-violet)">AI · {list.length}</span>}
          </div>
          <p className="mt-[3px] truncate text-[12px] text-(--ts-muted)">
            {departments} {t(plural(departments, ["отдел", "отдела", "отделов"]))} · {activeTasks}{" "}
            {t(plural(activeTasks, ["активная задача", "активные задачи", "активных задач"]))}
          </p>
        </div>
        <Link
          href={`/consultant?ask=${encodeURIComponent(question)}`}
          aria-label={t("Спросить AI")}
          className={`-mr-[4px] grid size-[44px] shrink-0 place-items-center rounded-full ${press} ${focusRing}`}
        >
          <span className="grid size-[36px] place-items-center rounded-full border border-(--ts-border) bg-(--ts-surface) text-(--ts-text-2)">
            <MessageCircle aria-hidden className="size-[17px]" strokeWidth={2} />
          </span>
        </Link>
      </div>

      {shown.length === 0 ? (
        <p className="flex items-center gap-[10px] border-t border-(--mh-row) px-[18px] py-[16px] text-[14px] font-medium text-(--ts-text)">
          <CheckCircle2 aria-hidden className="size-[20px] shrink-0 text-(--ts-success)" strokeWidth={2} />
          {t("Сегодня всё спокойно")}
        </p>
      ) : (
        <ul>
          {shown.map((i) => {
            const s = tone[i.severity];
            const Icon = i.severity === "info" ? Lightbulb : AlertTriangle;
            return (
              <li key={i.id} className="border-t border-(--mh-row)">
                <Link href={i.href} className={`flex items-center gap-[12px] px-[18px] py-[13px] ${press} ${focusRing}`}>
                  <span className={`flex size-[46px] shrink-0 flex-col items-center justify-center rounded-[12px] ${s.soft} ${s.text}`}>
                    {i.metric ? (
                      <>
                        <span className="max-w-[44px] truncate text-[15px] font-bold leading-none">{i.metric.value}</span>
                        {i.metric.caption && <span className="mt-[3px] max-w-[44px] truncate text-[9px] font-medium leading-none opacity-90">{i.metric.caption}</span>}
                      </>
                    ) : (
                      <Icon aria-hidden className="size-[20px]" strokeWidth={2} />
                    )}
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="flex items-center gap-[5px] text-[11px]">
                      <span aria-hidden className={`size-[6px] shrink-0 rounded-full ${s.dot}`} />
                      <span className={`font-semibold ${s.text}`}>{t(s.label)}</span>
                      <span className="truncate text-(--ts-muted)">· {t(i.department)}</span>
                    </span>
                    <span className="mt-[3px] block text-[14px] font-medium leading-[1.35] text-(--ts-text)">{i.text}</span>
                  </span>
                  <ChevronRight aria-hidden className="size-[16px] shrink-0 text-(--ts-subtle)" strokeWidth={2} />
                </Link>
              </li>
            );
          })}
        </ul>
      )}

      {more > 0 && (
        <Link
          href="/ai"
          className={`flex min-h-[44px] items-center justify-center gap-[4px] border-t border-(--mh-row) text-[13px] font-semibold text-(--ts-link) ${press} ${focusRing}`}
        >
          {t("Ещё {n}", { n: more })}
          <ChevronRight aria-hidden className="size-[15px]" strokeWidth={2.2} />
        </Link>
      )}
    </section>
  );
}
