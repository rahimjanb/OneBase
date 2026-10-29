import Link from "next/link";
import { getT } from "@/i18n/server";
import { dayLabel, markTypeLabel, trackTypeLabel, type HrEvent } from "@/lib/hr";

const th = "px-3 py-2.5 font-semibold";

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
