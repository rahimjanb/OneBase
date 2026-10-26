/**
 * Демо-подробности дня для карточки дня (?demo=1 в разработке): у демо-сотрудников нет карточек в API. Отдельно от демо-табеля,
 * чтобы сам набор не попадал в клиентскую сборку.
 */
import type { DayDetail } from "@/lib/hr-timesheet";

/** ID демо-сотрудников из hr-timesheet.mock.ts. */
export const isMockEmployee = (id: string) => id.startsWith("00000000-0000-4000-8000-");

/** Подробности дня для демо (в рабочей версии — из карточки сотрудника): 9 октября у первого — как в макете. */
export function mockDayDetail(code: string, date: string): DayDetail {
  const day = Number(date.slice(8, 10));
  const at = (h: number, m: number) => `${date}T${String(h).padStart(2, "0")}:${String(m).padStart(2, "0")}:00`;
  const plan = { planBegin: at(9, 0), planEnd: at(18, 0) };
  const none: DayDetail = { firstIn: null, lastOut: null, planBegin: null, planEnd: null, workedMinutes: null, lateMinutes: null, earlyLeaveMinutes: null, leaveName: null, reason: null, events: 0 };
  switch (code) {
    case "ОП": {
      const late = day === 9 ? 24 : 6 + ((day * 7) % 40);
      return { ...none, ...plan, firstIn: at(9, late), lastOut: at(18, 5), workedMinutes: day === 9 ? 522 : 530 - late, lateMinutes: late, events: 3 };
    }
    case "РУ":
      return { ...none, ...plan, firstIn: at(8, 52), lastOut: at(16, 40 - (day % 20)), workedMinutes: 420 - (day % 20), earlyLeaveMinutes: 80 + (day % 20), events: 3 };
    case "Я":
      return { ...none, ...plan, firstIn: at(8, 50 + (day % 9)), lastOut: at(18, day % 15), workedMinutes: 485 + (day % 15), events: 3 };
    case "РВ":
      return { ...none, firstIn: at(10, 5), lastOut: at(15, 20), workedMinutes: 315, events: 2, reason: "работа в нерабочий день" };
    case "?":
      return { ...none, ...plan, events: 1, reason: "есть отметки без подтверждённого прихода" };
    case "ОТС":
      return { ...none, ...plan, reason: "нет прихода в рабочий день" };
    default:
      return { ...none };
  }
}
