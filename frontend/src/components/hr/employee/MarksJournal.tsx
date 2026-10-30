"use client";

import { Fragment, useState } from "react";
import { useT } from "@/i18n/client";
import { plural } from "@/lib/format";
import { markTypeLabel, trackTypeLabel, weekday, type HrEvent } from "@/lib/hr";
import { card, focus } from "../overview/bits";

type Kind = "all" | "in" | "out" | "other";
const KINDS: { key: Kind; label: string }[] = [
  { key: "all", label: "Все" },
  { key: "in", label: "Приходы" },
  { key: "out", label: "Уходы" },
  { key: "other", label: "Прочие" },
];
const kindOf = (e: HrEvent): Kind => (e.trackType === "I" ? "in" : e.trackType === "O" ? "out" : "other");
const typeTone = (e: HrEvent) => (e.trackType === "I" ? "text-(--ts-success)" : e.trackType === "O" ? "text-(--ts-text-2)" : "text-(--ts-muted)");

/** Сколько отметок показывать за раз — дальше «Показать ещё». */
const STEP = 100;

/**
 * «Все отметки» за период — каждый проход и отметка терминала Verifix: время, тип (приход, уход, присутствие…), способ (лицо, карта…),
 * локация или терминал, ID отметки. Новые сверху, по дням (календарным, как в Verifix). Отметка, пропавшая из Verifix, — бледная:
 * в расчёте дня она не участвует. Фильтр по типу и подгрузка по 100.
 */
export function MarksJournal({ events }: { events: HrEvent[] }) {
  const t = useT();
  const [kind, setKind] = useState<Kind>("all");
  const [limit, setLimit] = useState(STEP);

  const sorted = [...events].sort((a, b) => b.localTime.localeCompare(a.localTime) || b.id - a.id);
  const list = kind === "all" ? sorted : sorted.filter((e) => kindOf(e) === kind);
  const shown = list.slice(0, limit);
  const count = (k: Kind) => (k === "all" ? sorted.length : sorted.filter((e) => kindOf(e) === k).length);
  const perDay = new Map<string, number>();
  for (const e of list) perDay.set(e.localTime.slice(0, 10), (perDay.get(e.localTime.slice(0, 10)) ?? 0) + 1);
  const days = [...new Set(shown.map((e) => e.localTime.slice(0, 10)))];
  const dayTitle = (date: string) => {
    const n = perDay.get(date) ?? 0;
    return `${date.slice(8, 10)}.${date.slice(5, 7)} · ${t(weekday(date))} · ${n} ${t(plural(n, ["отметка", "отметки", "отметок"]))}`;
  };
  const missing = (e: HrEvent) => (e.missingSince ? t("Нет в Verifix — в расчёте не участвует") : undefined);

  return (
    <section aria-labelledby="hr-marks" className={`${card} overflow-hidden`}>
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 px-4 py-3.5 md:px-5">
        <h2 id="hr-marks" className="text-[15px] font-semibold text-(--ts-text)">
          {t("Все отметки")} <span className="font-medium tabular-nums text-(--ts-subtle)">{sorted.length}</span>
        </h2>
        <div role="radiogroup" aria-label={t("Тип отметок")} className="flex flex-wrap items-center gap-1">
          {KINDS.map((k) => {
            const on = kind === k.key;
            return (
              <button
                key={k.key}
                type="button"
                role="radio"
                aria-checked={on}
                onClick={() => {
                  setKind(k.key);
                  setLimit(STEP);
                }}
                className={`inline-flex h-8 cursor-pointer items-center gap-1.5 rounded-[9px] border px-2.5 text-[13px] font-medium transition-colors duration-150 md:px-3 ${focus} ${
                  on ? "border-(--ts-border-strong) bg-(--ts-surface-2) text-(--ts-text)" : "border-transparent text-(--ts-muted) hover:text-(--ts-text-2)"
                }`}
              >
                {t(k.label)}
                <span className="tabular-nums text-(--ts-subtle)">{count(k.key)}</span>
              </button>
            );
          })}
        </div>
      </div>

      {shown.length === 0 ? (
        <p className="border-t border-(--ts-border) px-5 py-8 text-center text-sm text-(--ts-muted)">{t("Отметок за период нет.")}</p>
      ) : (
        <>
          {/* Планшет и компьютер: таблица с заголовками дней. */}
          <table className="w-full table-fixed text-sm max-md:hidden">
            <colgroup>
              <col className="w-[96px]" />
              <col className="w-[170px]" />
              <col className="w-[120px]" />
              <col />
              <col className="w-[110px]" />
            </colgroup>
            <thead>
              <tr className="bg-(--ts-surface-2) text-left text-[11px] font-semibold uppercase tracking-[0.5px] text-(--ts-subtle)">
                <th className="py-2.5 pl-5 pr-2 font-semibold">{t("Время")}</th>
                <th className="px-2 py-2.5 font-semibold">{t("Тип")}</th>
                <th className="px-2 py-2.5 font-semibold">{t("Способ")}</th>
                <th className="px-2 py-2.5 font-semibold">{t("Локация / терминал")}</th>
                <th className="py-2.5 pl-2 pr-5 text-right font-semibold">{t("ID отметки")}</th>
              </tr>
            </thead>
            <tbody>
              {days.map((date) => (
                <Fragment key={date}>
                  <tr className="border-t border-(--ts-border) bg-(--ts-bg)/60">
                    <th colSpan={5} scope="rowgroup" className="py-2 pl-5 pr-5 text-left text-xs font-semibold text-(--ts-muted)">
                      {dayTitle(date)}
                    </th>
                  </tr>
                  {shown
                    .filter((e) => e.localTime.startsWith(date))
                    .map((e) => (
                      <tr key={e.id} title={missing(e)} className={`border-t border-(--ts-border) tabular-nums ${e.missingSince ? "opacity-45" : ""}`}>
                        <td className="py-2 pl-5 pr-2 font-semibold text-(--ts-text)">{e.localTime.slice(11, 19)}</td>
                        <td className={`truncate px-2 py-2 font-medium ${typeTone(e)}`}>{t(trackTypeLabel[e.trackType] ?? e.trackType)}</td>
                        <td className="truncate px-2 py-2 text-(--ts-muted)">{e.markType ? t(markTypeLabel[e.markType] ?? e.markType) : "—"}</td>
                        <td className="truncate px-2 py-2 text-(--ts-text-2)">{e.location ?? <span className="text-(--ts-subtle)">—</span>}</td>
                        <td className="py-2 pl-2 pr-5 text-right text-xs text-(--ts-subtle)">{e.trackId}</td>
                      </tr>
                    ))}
                </Fragment>
              ))}
            </tbody>
          </table>

          {/* Телефон: список по дням. */}
          <div className="md:hidden">
            {days.map((date) => (
              <div key={date}>
                <p className="border-t border-(--ts-border) bg-(--ts-bg)/60 px-4 py-2 text-xs font-semibold text-(--ts-muted)">{dayTitle(date)}</p>
                <ul>
                  {shown
                    .filter((e) => e.localTime.startsWith(date))
                    .map((e) => (
                      <li key={e.id} title={missing(e)} className={`flex items-baseline gap-3 border-t border-(--ts-border) px-4 py-2.5 text-[13px] ${e.missingSince ? "opacity-45" : ""}`}>
                        <span className="w-[62px] shrink-0 font-semibold tabular-nums text-(--ts-text)">{e.localTime.slice(11, 19)}</span>
                        <span className="flex min-w-0 flex-1 flex-col">
                          <span className={`font-medium ${typeTone(e)}`}>{t(trackTypeLabel[e.trackType] ?? e.trackType)}</span>
                          <span className="truncate text-xs text-(--ts-subtle)">
                            {[e.markType ? t(markTypeLabel[e.markType] ?? e.markType) : null, e.location].filter(Boolean).join(" · ")}
                          </span>
                        </span>
                      </li>
                    ))}
                </ul>
              </div>
            ))}
          </div>

          {list.length > shown.length && (
            <button
              type="button"
              onClick={() => setLimit((n) => n + STEP)}
              className={`block w-full cursor-pointer border-t border-(--ts-border) px-5 py-3 text-center text-[13px] font-medium text-(--ts-link) hover:bg-(--ts-row-hover) ${focus}`}
            >
              {t("Показать ещё {n} из {left}", { n: Math.min(STEP, list.length - shown.length), left: list.length - shown.length })}
            </button>
          )}
        </>
      )}
    </section>
  );
}
