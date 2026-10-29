"use client";

import { useRouter } from "next/navigation";
import Link from "next/link";
import { useMemo, useState } from "react";
import { useT } from "@/i18n/client";
import type { HrGroupStat } from "@/lib/hr";
import { attendanceTone, num, pct, share, toneDot, toneText } from "@/lib/hr-overview";
import { card, focus } from "./bits";

type Sort = "desc" | "asc" | "name";
const NEXT: Record<Sort, Sort> = { desc: "asc", asc: "name", name: "desc" };
const SORT_LABEL: Record<Sort, string> = { desc: "явка ↓", asc: "явка ↑", name: "А–Я" };
/** Подразделений сразу; остальные — по «Показать все». */
const FIRST = 8;

/** Явка подразделения — на месте от «по графику»; без графика на день — null (в конце списка). */
const rateOf = (d: HrGroupStat) => share(d.present, d.scheduled);

/**
 * «По подразделениям»: по графику, на месте, опоздали, нет, явка. Карточка от 640 — таблица, уже — список с полосой и строкой
 * «33 из 38 на месте · 4 опозд. · 2 нет». Сортировка — явка по убыванию, по возрастанию или по названию. Строка открывает «Посещаемость»
 * этого подразделения за день.
 */
export function DivisionsCard({ divisions, attendanceHref }: { divisions: HrGroupStat[]; attendanceHref: (divisionId: number) => string }) {
  const t = useT();
  const router = useRouter();
  const [sort, setSort] = useState<Sort>("desc");
  const [all, setAll] = useState(false);

  const rows = useMemo(() => {
    const list = [...divisions];
    if (sort === "name") return list.sort((a, b) => a.name.localeCompare(b.name, "ru"));
    const dir = sort === "desc" ? -1 : 1;
    return list.sort((a, b) => {
      const ra = rateOf(a);
      const rb = rateOf(b);
      if (ra == null || rb == null) return ra == null ? (rb == null ? a.name.localeCompare(b.name, "ru") : 1) : -1;
      return ra === rb ? b.scheduled - a.scheduled : dir * (ra - rb);
    });
  }, [divisions, sort]);
  const shown = all ? rows : rows.slice(0, FIRST);

  const open = (d: HrGroupStat) => d.id != null && router.push(attendanceHref(d.id));

  const bar = (d: HrGroupStat, height: string) => {
    const rate = rateOf(d);
    return (
      <span className={`block ${height} flex-1 overflow-hidden rounded-full bg-(--ts-surface-2)`}>
        {rate != null && <span className={`block h-full rounded-full ${toneDot[attendanceTone(rate)]}`} style={{ width: `${Math.min(100, rate)}%` }} />}
      </span>
    );
  };
  const rateText = (d: HrGroupStat) => {
    const rate = rateOf(d);
    return rate == null ? "—" : pct(rate, 0);
  };

  return (
    <section aria-labelledby="hr-div" className={`${card} @container h-full overflow-hidden`}>
      <div className="flex items-center justify-between gap-3 px-5 pb-3 pt-5 lg:px-6 lg:pb-4">
        <h2 id="hr-div" className="text-base font-semibold text-(--ts-text)">
          {t("По подразделениям")}
        </h2>
        {divisions.length > 1 && (
          <button
            type="button"
            onClick={() => setSort(NEXT[sort])}
            className={`shrink-0 cursor-pointer rounded text-[13px] font-medium text-(--ts-muted) transition-colors duration-150 hover:text-(--ts-text-2) ${focus}`}
          >
            <span className="hidden @min-[640px]:inline">{t("Сортировка:")} </span>
            {t(SORT_LABEL[sort])}
          </button>
        )}
      </div>

      {rows.length === 0 ? (
        <p className="border-t border-(--ts-border) px-6 py-8 text-center text-sm text-(--ts-muted)">{t("Сотрудники появятся после первой синхронизации")}</p>
      ) : (
        <>
          {/* Широкая карточка (от 640): таблица. */}
          <table className="hidden w-full table-fixed text-sm @min-[640px]:table">
            <colgroup>
              <col />
              <col className="w-[96px]" />
              <col className="w-[84px]" />
              <col className="w-[88px]" />
              <col className="w-[52px]" />
              <col className="w-[180px] 2xl:w-[220px]" />
            </colgroup>
            <thead>
              <tr className="whitespace-nowrap bg-(--ts-surface-2) text-left text-[11px] font-semibold uppercase tracking-[0.6px] text-(--ts-subtle)">
                <th className="py-2.5 pl-6 pr-3 font-semibold">{t("Подразделение")}</th>
                <th className="px-2.5 py-2.5 text-right font-semibold">{t("По графику")}</th>
                <th className="px-2.5 py-2.5 text-right font-semibold">{t("На месте")}</th>
                <th className="px-2.5 py-2.5 text-right font-semibold">{t("Опоздали")}</th>
                <th className="px-2.5 py-2.5 text-right font-semibold">{t("Нет")}</th>
                <th className="py-2.5 pl-3 pr-6 font-semibold">{t("Явка")}</th>
              </tr>
            </thead>
            <tbody>
              {shown.map((d) => {
                const link = d.id != null;
                return (
                  <tr
                    key={d.id ?? "none"}
                    onClick={() => open(d)}
                    className={`border-t border-(--ts-border) tabular-nums ${link ? "cursor-pointer transition-colors duration-150 hover:bg-(--ts-row-hover)" : ""}`}
                  >
                    <td className="py-4 pl-6 pr-3 font-medium leading-snug text-(--ts-text)">
                      {link ? (
                        <Link href={attendanceHref(d.id!)} onClick={(e) => e.stopPropagation()} className={`rounded ${focus}`}>
                          {d.name}
                        </Link>
                      ) : (
                        t(d.name)
                      )}
                    </td>
                    <td className="px-2.5 py-4 text-right text-(--ts-muted)">{num(d.scheduled)}</td>
                    <td className="px-2.5 py-4 text-right font-semibold text-(--ts-text)">{num(d.present)}</td>
                    <td className={`px-2.5 py-4 text-right font-medium ${d.late > 0 ? "text-(--ts-warning)" : "text-(--ts-subtle)"}`}>{num(d.late)}</td>
                    <td className={`px-2.5 py-4 text-right font-medium ${d.absent > 0 ? "text-(--ts-danger)" : "text-(--ts-subtle)"}`}>{num(d.absent)}</td>
                    <td className="py-4 pl-3 pr-6">
                      <span className="flex items-center gap-3">
                        {bar(d, "h-1.5")}
                        <span className="w-10 text-right text-[13px] font-semibold text-(--ts-text)">{rateText(d)}</span>
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>

          {/* Узкая карточка (телефон, планшет, ноутбук с боковым меню до ~1350): список. */}
          <ul className="@min-[640px]:hidden">
            {shown.map((d) => {
              const body = (
                <>
                  <span className="flex items-baseline justify-between gap-3">
                    <span className="truncate text-sm font-medium text-(--ts-text)">{d.id != null ? d.name : t(d.name)}</span>
                    <span className="text-sm font-bold tabular-nums text-(--ts-text)">{rateText(d)}</span>
                  </span>
                  <span className="mt-2 flex">{bar(d, "h-[5px]")}</span>
                  <span className="mt-2 flex flex-wrap gap-x-2.5 text-xs tabular-nums">
                    <span className="text-(--ts-muted)">{t("{present} из {scheduled} на месте", { present: num(d.present), scheduled: num(d.scheduled) })}</span>
                    {d.late > 0 && <span className={toneText.warning}>{t("{n} опозд.", { n: num(d.late) })}</span>}
                    {d.absent > 0 && <span className={toneText.danger}>{t("{n} нет", { n: num(d.absent) })}</span>}
                  </span>
                </>
              );
              return (
                <li key={d.id ?? "none"} className="border-t border-(--ts-border)">
                  {d.id != null ? (
                    <Link href={attendanceHref(d.id)} className="block px-5 py-3.5 hover:bg-(--ts-row-hover) focus-visible:bg-(--ts-row-hover) focus-visible:outline-none">
                      {body}
                    </Link>
                  ) : (
                    <div className="px-5 py-3.5">{body}</div>
                  )}
                </li>
              );
            })}
          </ul>
          {rows.length > FIRST && (
            <button
              type="button"
              onClick={() => setAll((v) => !v)}
              aria-expanded={all}
              className={`block w-full cursor-pointer border-t border-(--ts-border) px-6 py-3 text-center text-[13px] font-medium text-(--ts-link) hover:bg-(--ts-row-hover) ${focus}`}
            >
              {all ? t("Свернуть") : t("Показать все {n}", { n: rows.length })}
            </button>
          )}
        </>
      )}
    </section>
  );
}
