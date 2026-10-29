"use client";

import { useT } from "@/i18n/client";
import type { HrOverview } from "@/lib/hr";
import { counted, hourRange, num, pad2, peakHour } from "@/lib/hr-overview";
import { card, Tip } from "./bits";

/**
 * «Проходы по часам»: прошедшие часы — accent вполовину, пиковый и текущий — accent целиком (текущий подписан ярче), будущие — плейсхолдер.
 * За прошлый день все часы прошедшие. Нет проходов — плейсхолдеры и «Проходов пока нет».
 */
export function HoursChart({ o }: { o: HrOverview }) {
  const t = useT();
  const byHour = o.passesByHour.length === 24 ? o.passesByHour : Array(24).fill(0);
  const hours = hourRange(byHour);
  const peak = peakHour(byHour);
  const current = o.isToday ? Number(o.now.slice(11, 13)) : null;
  const max = Math.max(1, ...hours.map((h) => byHour[h]));
  // Больше 13 часов (ночные проходы) — столбцы теснее, подписан каждый второй час.
  const dense = hours.length > 13;
  const gap = dense ? "gap-1" : "gap-1.5 lg:gap-2";
  const passes = (n: number) => counted(n, ["проход", "прохода", "проходов"], t);
  const label =
    peak == null
      ? t("Проходов пока нет")
      : hours
          .filter((h) => byHour[h] > 0)
          .map((h) => `${pad2(h)}:00–${pad2(h + 1)}:00 — ${passes(byHour[h])}`)
          .join(", ");

  return (
    <section aria-labelledby="hr-hours" className={`${card} p-5`}>
      <div className="flex items-baseline justify-between gap-3">
        <h2 id="hr-hours" className="text-base font-semibold text-(--ts-text)">
          {t("Проходы по часам")}
        </h2>
        <span className="text-[13px] text-(--ts-muted)">
          {peak == null ? (
            t("Проходов пока нет")
          ) : (
            <>
              <span className="lg:hidden">{t("пик {range}", { range: `${pad2(peak)}–${pad2(peak + 1)}` })}</span>
              <span className="max-lg:hidden">{t("пик {range}", { range: `${pad2(peak)}:00–${pad2(peak + 1)}:00` })}</span>
            </>
          )}
        </span>
      </div>

      <div role="img" aria-label={label} className={`mt-4 flex h-24 items-end lg:h-[84px] ${gap}`}>
        {hours.map((h) => {
          const count = byHour[h];
          const future = current != null && h > current;
          const strong = h === peak || h === current;
          const height = future || count === 0 ? 4 : Math.max(4, Math.round((count / max) * 100));
          return (
            <span key={h} className="group/tip relative flex h-full flex-1 items-end">
              <span
                aria-hidden
                className={`block w-full rounded-t-[4px] ${future || count === 0 ? "rounded-b-[4px] bg-(--ts-surface-2)" : strong ? "bg-(--ts-accent)" : "bg-(--ts-accent)/50"}`}
                style={{ height: future || count === 0 ? 4 : `${height}%` }}
              />
              {!future && (
                <Tip align={h === hours[0] ? "start" : h === hours[hours.length - 1] ? "end" : "center"}>
                  {pad2(h)}:00–{pad2(h + 1)}:00 — {passes(count)}
                </Tip>
              )}
            </span>
          );
        })}
      </div>
      <div aria-hidden className={`mt-2 flex ${gap}`}>
        {hours.map((h) => (
          <span key={h} className={`min-w-0 flex-1 overflow-visible text-center text-[10px] tabular-nums ${h === current ? "font-semibold text-(--ts-text)" : "text-(--ts-subtle)"}`}>
            {!dense || h % 2 === 0 || h === current ? pad2(h) : ""}
          </span>
        ))}
      </div>
    </section>
  );
}

/** «По филиалам»: проходы за день по локациям Verifix, полоса — от наибольшего. */
export function BranchesCard({ o }: { o: HrOverview }) {
  const t = useT();
  const max = Math.max(1, ...o.locations.map((l) => l.events));
  return (
    <section aria-labelledby="hr-branches" className={`${card} flex-1 p-5`}>
      <div className="flex items-baseline justify-between gap-3">
        <h2 id="hr-branches" className="text-base font-semibold text-(--ts-text)">
          {t("По филиалам")}
        </h2>
        <span className="text-[13px] text-(--ts-muted)">{o.isToday ? t("проходы сегодня") : t("проходы за день")}</span>
      </div>
      {o.locations.length === 0 ? (
        <p className="mt-4 text-sm text-(--ts-muted)">{t("Проходов пока нет")}</p>
      ) : (
        <ul className="mt-3.5 flex flex-col gap-3.5">
          {o.locations.map((l) => (
            <li key={l.id ?? "none"}>
              <span className="flex items-baseline justify-between gap-3 text-[13px]">
                <span className="truncate font-medium text-(--ts-text-2)">{l.id != null ? l.name : t(l.name)}</span>
                <span className="font-semibold tabular-nums text-(--ts-text)">{num(l.events)}</span>
              </span>
              <span className="mt-2 block h-1 overflow-hidden rounded-full bg-(--ts-surface-2)">
                <span className="block h-full rounded-full bg-(--ts-accent)" style={{ width: `${(100 * l.events) / max}%` }} />
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
