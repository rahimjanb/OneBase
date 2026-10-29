"use client";

import { formatDate } from "@/i18n/core";
import { useLocale, useT } from "@/i18n/client";
import { duration, weekday, type HrDay } from "@/lib/hr";
import { dayBadges, dayTones } from "@/lib/hr-employee";
import { toneDot, type Tone } from "@/lib/hr-overview";
import { card, focus, Tip } from "../overview/bits";

const LEGEND: { tone: Tone; label: string }[] = [
  { tone: "success", label: "Вовремя" },
  { tone: "warning", label: "Опоздание" },
  { tone: "danger", label: "Ранний уход" },
  { tone: "accent", label: "Выходной с приходом" },
  { tone: "violet", label: "По документу" },
  { tone: "subtle", label: "Не ясно" },
];

/** Выходные — воскресенье и суббота; подписываются акцентом. */
const isWeekend = (date: string) => [0, 6].includes(new Date(`${date}T12:00:00Z`).getUTCDay());

/**
 * «Период по дням»: клетка на каждый день — день недели, число и индикатор из 1–2 цветов итога. До двух недель на широком экране — одной строкой,
 * дольше (и всегда на телефоне) — сеткой по 7. Наведение — итог дня, нажатие — переход к строке таблицы. Сегодня — с рамкой.
 */
export function DayStrip({ days, today, onPick }: { days: HrDay[]; today: string; onPick: (date: string) => void }) {
  const t = useT();
  const locale = useLocale();
  const list = [...days].sort((a, b) => a.date.localeCompare(b.date));
  const used = new Set(list.flatMap(dayTones));
  const legend = LEGEND.filter((l) => used.has(l.tone));
  const wide = list.length <= 14;

  const describe = (d: HrDay) => {
    const date = formatDate(new Date(`${d.date.slice(0, 10)}T12:00:00Z`), locale, { day: "numeric", month: "long", timeZone: "UTC" });
    const parts = dayBadges(d).map((b) =>
      b.label === "Опоздание" && d.lateMinutes != null
        ? t("опоздание {n}", { n: duration(d.lateMinutes, t) })
        : b.label === "Ранний уход" && d.earlyLeaveMinutes != null
          ? t("ранний уход {n}", { n: duration(d.earlyLeaveMinutes, t) })
          : t(b.label).toLocaleLowerCase("ru"),
    );
    return `${date}, ${parts.join(", ")}`;
  };

  const legendList = (
    <ul className="flex flex-wrap items-center gap-x-3.5 gap-y-1.5">
      {legend.map((l) => (
        <li key={l.tone} className="flex items-center gap-1.5 text-[11px] text-(--ts-muted)">
          <span aria-hidden className={`size-2 rounded-[2px] ${toneDot[l.tone]}`} />
          {t(l.label)}
        </li>
      ))}
    </ul>
  );

  return (
    <section aria-labelledby="hr-days-strip" className={`${card} px-4 py-4 md:px-[18px]`}>
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
        <h2 id="hr-days-strip" className="text-sm font-semibold text-(--ts-text)">
          {t("Период по дням")}
        </h2>
        <div className="max-md:hidden">{legendList}</div>
      </div>
      <ol
        className={`mt-3.5 grid grid-cols-7 gap-1.5 ${wide ? "md:grid-cols-(--strip-cols)" : ""}`}
        style={{ "--strip-cols": `repeat(${Math.max(list.length, 1)}, minmax(0, 1fr))` } as React.CSSProperties}
      >
        {list.map((d) => {
          const date = d.date.slice(0, 10);
          const tones = dayTones(d);
          const label = describe(d);
          const current = date === today;
          return (
            <li key={date} className="group/tip relative min-w-0">
              <button
                type="button"
                onClick={() => onPick(date)}
                aria-label={label}
                className={`flex w-full cursor-pointer flex-col items-center gap-1 rounded-[10px] border py-2 transition-colors duration-150 hover:border-(--ts-border-strong) ${focus} ${
                  current ? "border-(--ts-border-strong) bg-(--ts-surface-2)" : "border-transparent bg-(--ts-bg)"
                }`}
              >
                <span className={`text-[10px] font-medium ${isWeekend(date) ? "text-(--ts-accent)" : "text-(--ts-subtle)"}`}>{t(weekday(date))}</span>
                <span className="text-[15px] font-semibold leading-none text-(--ts-text)">{Number(date.slice(8, 10))}</span>
                <span aria-hidden className="mt-1 flex h-[5px] w-7 max-w-[80%] gap-0.5">
                  {tones.length === 0 ? (
                    <span className="h-full flex-1 rounded-full bg-(--ts-border)" />
                  ) : (
                    tones.map((tone, i) => <span key={i} className={`h-full flex-1 rounded-full ${toneDot[tone]}`} />)
                  )}
                </span>
              </button>
              <Tip>{label}</Tip>
            </li>
          );
        })}
      </ol>
      {legend.length > 0 && <div className="mt-3 md:hidden">{legendList}</div>}
    </section>
  );
}
