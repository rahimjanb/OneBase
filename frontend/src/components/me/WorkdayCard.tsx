import { AlarmClock, Clock, Coffee, LogIn, LogOut, UserX, type LucideIcon } from "lucide-react";
import type { T } from "@/i18n/core";
import { plural } from "@/lib/format";
import { clock, hhmm, hm, hmLong, ringShare, weekTone, weekdayCell, type MyToday, type MyWeekDay, type MyWorkday } from "@/lib/my-day";
import m from "./me.module.css";
import { BlockHeader, Empty, card, toneDot, toneText } from "./parts";

type Metric = { icon: LucideIcon; label: string; value: string; note: string | null; noteTone: "success" | "warning" | "muted" };

/** Показатели дня: приход, уход, перерыв (только если он есть в графике дня или по умолчанию компании), опоздание. */
function metricsOf(today: MyToday, t: T): Metric[] {
  const late = today.lateMinutes ?? 0;
  const arrived = today.arrival !== null;
  const list: Metric[] = [
    {
      icon: LogIn,
      label: t("Приход"),
      value: arrived ? hhmm(today.arrival) : "—",
      note: arrived ? (late > 0 ? t("опоздание {n} мин", { n: late }) : t("вовремя")) : today.shiftStart ? t("смена с {time}", { time: hhmm(today.shiftStart) }) : null,
      noteTone: arrived ? (late > 0 ? "warning" : "success") : "muted",
    },
    {
      icon: LogOut,
      label: t("Уход"),
      value: today.departure ? hhmm(today.departure) : "—",
      note: today.departure
        ? today.earlyLeaveMinutes
          ? t("раньше на {n} мин", { n: today.earlyLeaveMinutes })
          : null
        : today.status === "out"
          ? t("вышли в {time}", { time: hhmm(today.lastMarkAt) })
          : today.shiftEnd
            ? t("ожидается {time}", { time: hhmm(today.shiftEnd) })
            : null,
      noteTone: today.earlyLeaveMinutes ? "warning" : "muted",
    },
  ];
  if (today.break) {
    list.push({
      icon: Coffee,
      label: t("Перерыв"),
      value: hhmm(today.break.start),
      note: t("по графику, {d}", { d: hmLong(today.break.minutes, t) }),
      noteTone: "muted",
    });
  }
  list.push({
    icon: AlarmClock,
    label: t("Опоздание"),
    value: t("{n} мин", { n: late }),
    note: arrived ? t("за сегодня") : null,
    noteTone: late > 0 ? "warning" : "success",
  });
  return list;
}

/** Кольцо рабочего дня: отработано из плана (SVG, без библиотек). */
function Ring({ today, t }: { today: MyToday; t: T }) {
  const worked = today.workedMinutes ?? 0;
  const planned = today.plannedMinutes ?? 0;
  const share = ringShare(worked, planned);
  const length = 2 * Math.PI * 52;
  const off = today.status === "day_off" || today.status === "on_leave";
  const label = off
    ? t(today.status === "day_off" ? "Выходной" : "По документу")
    : planned > 0
      ? t("Отработано {worked} из {planned}", { worked: hm(worked, t), planned: hmLong(planned, t) })
      : t("Отработано {worked}", { worked: hm(worked, t) });
  return (
    <div role="img" aria-label={label} className="relative grid size-[96px] shrink-0 place-items-center @xl:size-[120px]">
      <svg viewBox="0 0 120 120" className="absolute inset-0 size-full -rotate-90" aria-hidden>
        <circle cx="60" cy="60" r="52" fill="none" strokeWidth="10" className={m.ringTrack} />
        {share > 0 && (
          <circle
            cx="60"
            cy="60"
            r="52"
            fill="none"
            strokeWidth="10"
            strokeLinecap="round"
            stroke="currentColor"
            className="text-ok"
            strokeDasharray={`${share * length} ${length}`}
          />
        )}
      </svg>
      <div className="relative text-center">
        {off ? (
          <p className="text-[13px] font-semibold text-ink-2">{label}</p>
        ) : (
          <>
            <p className="text-[18px] font-bold leading-none text-ink @xl:hidden">{clock(worked)}</p>
            <p className="hidden text-[17px] font-bold leading-none text-ink @xl:block">{hm(worked, t)}</p>
            {planned > 0 && <p className="mt-1 text-[11.5px] text-ink-3 @xl:text-[12px]">{t("из {planned}", { planned: hmLong(planned, t) })}</p>}
          </>
        )}
      </div>
    </div>
  );
}

function WeekCell({ day, today, locale, t }: { day: MyWeekDay; today: string; locale: string; t: T }) {
  const { weekday, day: number } = weekdayCell(day.date, locale);
  const isToday = day.date === today;
  const view = weekTone(day.status);
  const late = day.status === "late";
  const off = day.status === "day_off" || day.status === "future";
  const worked = day.workedMinutes;
  const label = `${weekday} ${number}: ${t(view.label)}${worked ? `, ${hm(worked, t)}` : ""}`;
  return (
    <li
      aria-label={label}
      aria-current={isToday ? "date" : undefined}
      className={`min-w-0 rounded-xl border px-2 py-2 @min-[640px]:px-1.5 @min-[640px]:py-2.5 @min-[760px]:px-2.5 ${isToday ? "border-accent/60 bg-accent-soft" : "border-line bg-page/40"} ${off && !isToday ? "opacity-60" : ""}`}
    >
      {/* Компьютер: «Пн 26», точка, «08:47 – 18:05», часы. */}
      <div className="hidden items-center justify-between @min-[640px]:flex">
        <span className={`text-[12.5px] font-semibold ${off ? "text-ink-3" : "text-ink"}`}>
          {weekday} {number}
        </span>
        <span aria-hidden className={`size-[7px] rounded-full ${isToday ? "bg-accent" : toneDot[view.tone]}`} />
      </div>
      <p className="mt-1.5 hidden truncate text-[10.5px] tracking-[-0.4px] text-ink-3 @min-[640px]:block @min-[760px]:text-[11.5px] @min-[760px]:tracking-normal">
        {day.arrival ? `${hhmm(day.arrival)}–${day.departure ? hhmm(day.departure) : "…"}` : "—"}
      </p>
      <p className={`mt-1 hidden truncate text-[13px] font-bold @min-[640px]:block ${late ? "text-warn" : off ? "text-ink-3" : "text-ink"}`}>
        {worked ? hm(worked, t) : day.status === "day_off" ? t("выходной") : day.status === "absent" ? t("неявка") : "—"}
      </p>
      {/* Телефон: «Пн», цветная полоска, «9:18». */}
      <div className="flex flex-col items-center gap-1.5 @min-[640px]:hidden">
        <span className={`text-[11px] font-semibold ${isToday ? "text-link" : "text-ink-3"}`}>{weekday}</span>
        <span aria-hidden className={`h-[3px] w-5 rounded-full ${isToday ? "bg-accent" : off ? "bg-line-strong" : toneDot[view.tone]}`} />
        <span className={`text-[12px] font-bold ${late ? "text-warn" : off ? "text-ink-3" : "text-ink"}`}>{worked ? clock(worked) : "—"}</span>
      </div>
    </li>
  );
}

/**
 * «Рабочий день» из Verifix: кольцо (отработано из плана), приход, уход, перерыв по графику (13:00–14:00, если в графике его нет),
 * опоздание; ниже «Эта неделя» — итог и 7 дней. Нет привязки к Verifix — понятная карточка вместо цифр.
 */
export async function WorkdayCard({ data, today, locale, t }: { data: Promise<MyWorkday | null>; today: string; locale: string; t: T }) {
  const workday = await data;
  if (!workday) return null;
  if (!workday.available) {
    const link = workday.unavailableReason === "no_employee_link";
    return (
      <section aria-label={t("Рабочий день")} className={card}>
        <BlockHeader icon={Clock} title={t("Рабочий день")} />
        <div className="mt-4 flex items-start gap-3 rounded-xl bg-surface-2 p-4">
          <UserX aria-hidden className="mt-0.5 size-5 shrink-0 text-ink-3" />
          <div>
            <p className="text-[14px] font-semibold text-ink">{t(link ? "Учёт рабочего времени недоступен" : "Учёт рабочего времени не подключён")}</p>
            <p className="mt-1 text-[13px] text-ink-3">
              {t(link ? "Ваш профиль не связан с сотрудником в Verifix. Обратитесь в HR." : "Данные Verifix в OneBase пока не загружаются.")}
            </p>
          </div>
        </div>
      </section>
    );
  }

  const day = workday.today;
  const shift = day?.shiftStart && day.shiftEnd ? t("смена {from}–{to}", { from: hhmm(day.shiftStart), to: hhmm(day.shiftEnd) }) : null;
  const synced = workday.syncedAt
    ? new Intl.DateTimeFormat("ru-RU", { hour: "2-digit", minute: "2-digit", timeZone: "Asia/Tashkent" }).format(new Date(workday.syncedAt))
    : null;
  const totals = workday.totals;
  const lateText = totals && totals.lateCount > 0 ? `${totals.lateCount} ${t(plural(totals.lateCount, ["опоздание", "опоздания", "опозданий"]))}` : null;

  return (
    // Подробный вид (показатели в ряд, ячейки недели с временем) — когда самой карточке хватает ширины (контейнерные запросы), а не по экрану:
    // на 1280 px с меню левая колонка уже, чем на телефоне в альбомной ориентации.
    <section aria-label={t("Рабочий день")} className={`${card} ${m.workday} @container`}>
      <BlockHeader
        icon={Clock}
        title={t("Рабочий день")}
        aside={
          <span title={synced ? t("данные Verifix обновлены в {time}", { time: synced }) : undefined}>
            {[shift, t("данные Verifix")].filter(Boolean).join(" · ")}
          </span>
        }
      />
      {day ? (
        <div className="mt-4 flex items-center gap-4 @xl:mt-5 @xl:gap-6">
          <Ring today={day} t={t} />
          {day.status === "day_off" || day.status === "on_leave" || day.status === "absent" ? (
            <div className="min-w-0">
              <p className="text-[15px] font-semibold text-ink">
                {day.status === "day_off" ? t("Сегодня выходной") : day.status === "absent" ? t("Прихода сегодня нет") : (day.leaveName ?? t("Отсутствие по документу"))}
              </p>
              <p className="mt-1 text-[13px] text-ink-3">{t("Неделя — ниже.")}</p>
            </div>
          ) : (
            <dl className="grid min-w-0 flex-1 grid-cols-2 gap-x-4 gap-y-3 @xl:flex @xl:divide-x @xl:divide-line">
              {metricsOf(day, t).map((x) => (
                <div key={x.label} className="min-w-0 @xl:flex-1 @xl:px-5 @xl:first:pl-0">
                  <dt className="flex items-center gap-1.5 text-[12px] text-ink-3 @xl:text-[13px]">
                    <x.icon aria-hidden className="hidden size-4 @xl:block" strokeWidth={1.8} />
                    {x.label}
                  </dt>
                  <dd className="mt-0.5 text-[18px] font-bold leading-tight text-ink @xl:mt-1 @xl:text-[26px]">{x.value}</dd>
                  {x.note && <dd className={`mt-0.5 truncate text-[11.5px] font-medium @xl:mt-1 @xl:text-[12.5px] ${toneText[x.noteTone]}`}>{x.note}</dd>}
                </div>
              ))}
            </dl>
          )}
        </div>
      ) : (
        <div className="mt-4">
          <Empty>{t("За сегодня данных Verifix ещё нет.")}</Empty>
        </div>
      )}
      {day?.status === "not_started" && (
        <p className="mt-3 text-[13px] text-ink-3">
          {day.shiftStart ? t("Рабочий день не начат · смена с {time}", { time: hhmm(day.shiftStart) }) : t("Рабочий день не начат")}
        </p>
      )}

      {workday.week.length > 0 && (
        <div className="mt-4 border-t border-line pt-4 @xl:mt-5 @xl:pt-5">
          <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
            <p className="text-[13.5px] font-semibold text-ink @xl:text-[14px]">
              <span className="@xl:hidden">{t("Неделя")}</span>
              <span className="hidden @xl:inline">{t("Эта неделя")}</span>
              {totals && <span className="font-medium text-ink-2 @xl:hidden"> · {hmLong(totals.workedMinutes, t)}</span>}
            </p>
            {totals && (
              <p className="text-[12.5px] text-ink-2 @xl:text-[13px]">
                <span className="hidden @xl:inline">{hmLong(totals.workedMinutes, t)}</span>
                {lateText && (
                  <span className="font-medium text-warn">
                    <span className="hidden @xl:inline"> · </span>
                    {lateText}
                    <span className="hidden @xl:inline"> · {hmLong(totals.lateMinutes, t)}</span>
                  </span>
                )}
              </p>
            )}
          </div>
          <ol className="@container mt-3 grid grid-cols-7 gap-1.5">
            {workday.week.map((d) => (
              <WeekCell key={d.date} day={d} today={today} locale={locale} t={t} />
            ))}
          </ol>
        </div>
      )}
    </section>
  );
}
