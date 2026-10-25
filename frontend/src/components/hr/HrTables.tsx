import Link from "next/link";
import { DayBadges } from "@/components/hr/HrBits";
import { getT } from "@/i18n/server";
import { clock, dayLabel, duration, markTypeLabel, trackTypeLabel, weekday, type HrDay, type HrEvent } from "@/lib/hr";

const th = "px-3 py-2.5 font-semibold";

/** Дни сотрудника: график, приход и уход, отклонения, отработано, отметки и пояснение расчёта. */
export async function HrDaysTable({ days }: { days: HrDay[] }) {
  const t = await getT();
  return (
    <div className="overflow-x-auto rounded-xl border border-line bg-surface">
      <table className="w-full min-w-[860px] text-sm">
        <thead>
          <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
            <th className="px-4 py-2.5 font-semibold">{t("Дата")}</th>
            <th className={th}>{t("Итог")}</th>
            <th className={th}>{t("График")}</th>
            <th className={th}>{t("Приход")}</th>
            <th className={th}>{t("Уход")}</th>
            <th className={`${th} text-right`}>{t("Опоздание")}</th>
            <th className={`${th} text-right`}>{t("Ранний уход")}</th>
            <th className={`${th} text-right`}>{t("Отработано")}</th>
            <th className={`${th} text-right`}>{t("Отметок")}</th>
            <th className="px-4 py-2.5 font-semibold">{t("Пояснение")}</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {days
            .filter((d) => d.status !== "NotEmployed")
            .map((d) => (
              <tr key={d.date}>
                <td className="px-4 py-2 tabular-nums text-ink">
                  {dayLabel(d.date)} <span className="text-ink-3">{t(weekday(d.date))}</span>
                </td>
                <td className="px-3 py-2">
                  <DayBadges day={d} />
                </td>
                <td className="whitespace-nowrap px-3 py-2 tabular-nums text-ink-2">{d.planBegin ? `${clock(d.planBegin)}–${clock(d.planEnd)}` : d.dayKind && d.dayKind !== "W" ? t("нерабочий") : "—"}</td>
                <td className="px-3 py-2 tabular-nums">{clock(d.firstIn)}</td>
                <td className="px-3 py-2 tabular-nums">{clock(d.lastOut)}</td>
                <td className={`px-3 py-2 text-right tabular-nums ${d.late ? "text-warn" : "text-ink-3"}`}>{d.late ? duration(d.lateMinutes, t) : "—"}</td>
                <td className={`px-3 py-2 text-right tabular-nums ${d.earlyLeave ? "text-warn" : "text-ink-3"}`}>{d.earlyLeave ? duration(d.earlyLeaveMinutes, t) : "—"}</td>
                <td className="px-3 py-2 text-right tabular-nums text-ink-2">{duration(d.workedMinutes, t)}</td>
                <td className="px-3 py-2 text-right tabular-nums text-ink-3">{d.events}</td>
                <td className="px-4 py-2 text-xs text-ink-3">{d.reason ? t(d.reason) : ""}</td>
              </tr>
            ))}
        </tbody>
      </table>
    </div>
  );
}

/** Журнал отметок: время как в Verifix (время организации), тип и способ отметки, локация, ID отметки. */
export async function HrEventsTable({ events, showEmployee = true }: { events: HrEvent[]; showEmployee?: boolean }) {
  const t = await getT();
  return (
    <div className="overflow-x-auto rounded-xl border border-line bg-surface">
      <table className="w-full min-w-[760px] text-sm">
        <thead>
          <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
            <th className="px-4 py-2.5 font-semibold">{t("Время (организации)")}</th>
            {showEmployee && <th className={th}>{t("Сотрудник")}</th>}
            <th className={th}>{t("Тип отметки")}</th>
            <th className={th}>{t("Способ")}</th>
            <th className={th}>{t("Локация")}</th>
            <th className={th}>{t("Подразделение")}</th>
            <th className="px-4 py-2.5 text-right font-semibold">{t("ID отметки")}</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {events.map((e) => (
            <tr key={e.id} className={e.missingSince ? "opacity-60" : undefined}>
              <td className="px-4 py-2 tabular-nums text-ink" title={t("Как пришло из Verifix: {raw}", { raw: e.rawTime })}>
                {dayLabel(e.localTime)} {e.localTime.slice(11, 19)}
              </td>
              {showEmployee && (
                <td className="px-3 py-2">
                  {e.employeeId ? (
                    <Link href={`/hr/employees/${e.employeeId}`} className="text-ink hover:text-accent-strong">
                      {e.employee}
                    </Link>
                  ) : (
                    <span className="text-ink-3">{t("сотрудник не загружен")}</span>
                  )}
                </td>
              )}
              <td className="px-3 py-2 text-ink-2">
                {trackTypeLabel[e.trackType] ? t(trackTypeLabel[e.trackType]) : t("тип «{type}» (не описан в документации)", { type: e.trackType || "—" })}
                {e.missingSince && <span className="ml-1.5 text-xs text-warn">{t("нет в Verifix")}</span>}
              </td>
              <td className="px-3 py-2 text-ink-3">{e.markType ? t(markTypeLabel[e.markType] ?? e.markType) : "—"}</td>
              <td className="px-3 py-2 text-ink-2">{e.location ?? "—"}</td>
              <td className="px-3 py-2 text-ink-2">{e.division ?? "—"}</td>
              <td className="px-4 py-2 text-right tabular-nums text-ink-3">{e.trackId}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
