/**
 * Демо-карточка сотрудника из макета — только для проверки вёрстки в разработке (/hr/employees/demo?demo=1). Варианты:
 * 1 — обычный период (ночные смены, опоздания, ранние уходы), empty — нет отметок, dismissed — уволен, stale — ошибка синхронизации Verifix.
 * Итоги считаются из дней так же, как HrSummary.Of на сервере.
 */
import { addDays, type HrDay, type HrEmployeeCard, type HrEvent, type HrSummary } from "@/lib/hr";

export type EmployeeDemo = "1" | "empty" | "dismissed" | "stale";

export const EMPLOYEE_DEMOS: EmployeeDemo[] = ["1", "empty", "dismissed", "stale"];

export const demoToday = "2026-10-30";

type Row = [date: string, kind: "W" | "R" | "S", inAt: string | null, outAt: string | null, marks: number];

/** Как в макете: смена в табеле 07:00–07:00 (24 ч), по субботам (S) — 07:00–19:00, воскресенья — нерабочие. */
const rows: Row[] = [
  ["2026-10-30", "W", null, "2026-10-30T00:06", 2],
  ["2026-10-29", "W", "07:27", "2026-10-29T11:25", 8],
  ["2026-10-28", "W", "07:25", "2026-10-29T00:35", 1],
  ["2026-10-27", "W", "07:31", "2026-10-27T12:02", 6],
  ["2026-10-26", "W", "07:22", "2026-10-27T00:24", 2],
  ["2026-10-25", "R", "07:22", "2026-10-26T00:17", 2],
  ["2026-10-24", "S", "07:12", "2026-10-25T00:23", 2],
  ["2026-10-23", "W", "07:20", "2026-10-24T00:13", 4],
  ["2026-10-22", "W", "07:21", "2026-10-23T00:15", 6],
  ["2026-10-21", "W", "07:16", "2026-10-22T00:13", 8],
  ["2026-10-20", "W", "07:12", "2026-10-21T00:14", 4],
  ["2026-10-19", "W", "07:10", "2026-10-19T12:40", 4],
  ["2026-10-18", "R", "07:05", "2026-10-18T09:17", 2],
  ["2026-10-17", "S", "07:08", "2026-10-17T09:20", 4],
];

const NOW = "2026-10-30T03:55";
const minutes = (from: string, to: string) => Math.round((Date.parse(`${to}:00Z`) - Date.parse(`${from}:00Z`)) / 60000);

function day([date, kind, inAt, outAt]: Row): HrDay {
  const working = kind !== "R";
  const planBegin = working ? `${date}T07:00` : null;
  const planEnd = !working ? null : kind === "S" ? `${date}T19:00` : `${addDays(date, 1)}T07:00`;
  const firstIn = inAt ? `${date}T${inAt}` : null;
  const lateMinutes = firstIn && planBegin && minutes(planBegin, firstIn) > 5 ? minutes(planBegin, firstIn) : null;
  const shiftOver = planEnd ? NOW >= planEnd : true;
  const earlyMinutes = firstIn && outAt && planEnd && shiftOver && minutes(outAt, planEnd) > 5 ? minutes(outAt, planEnd) : null;
  const base = {
    date,
    dayKind: working ? "W" : "R",
    planBegin: planBegin && `${planBegin}:00`,
    planEnd: planEnd && `${planEnd}:00`,
    planMinutes: planBegin && planEnd ? minutes(planBegin, planEnd) : null,
    firstIn: firstIn && `${firstIn}:00`,
    lastOut: outAt && `${outAt}:00`,
    events: 0,
    noExit: false,
    leaveName: null,
    leaveCode: null,
    source: "timesheet" as const,
  };
  if (!firstIn) {
    return {
      ...base,
      status: "Unknown",
      late: false,
      lateMinutes: null,
      earlyLeave: false,
      earlyLeaveMinutes: null,
      workedMinutes: null,
      workedOnDayOff: false,
      reason: "есть отметки без подтверждённого прихода — проверьте в Verifix",
    };
  }
  return {
    ...base,
    status: "Present",
    late: lateMinutes != null,
    lateMinutes,
    earlyLeave: earlyMinutes != null,
    earlyLeaveMinutes: earlyMinutes,
    workedMinutes: outAt ? minutes(firstIn, outAt) : null,
    workedOnDayOff: !working,
    reason: working ? null : "работа в нерабочий день",
  };
}

/** Отметки дня: приход, уход и отметки присутствия между ними. */
function marks([date, , inAt, outAt, count]: Row, start: number): HrEvent[] {
  const times: { at: string; type: string }[] = [];
  const at = (ms: number) => new Date(ms).toISOString().slice(0, 16);
  const first = Date.parse(`${inAt ? `${date}T${inAt}` : outAt}:00Z`);
  const last = outAt ? Date.parse(`${outAt}:00Z`) : first;
  if (inAt) times.push({ at: `${date}T${inAt}`, type: "I" });
  // Отметки присутствия — между приходом и уходом (без прихода — за несколько минут до ухода).
  const middle = count - times.length - (outAt && count > 1 ? 1 : 0);
  for (let i = 1; i <= middle; i++) times.push({ at: at(inAt ? first + ((last - first) * i) / (middle + 1) : last - i * 4 * 60000), type: "C" });
  if (outAt && count > 1) times.push({ at: outAt, type: "O" });
  return times.map((m, i) => ({
    id: start + i,
    trackId: 900000 + start + i,
    employeeId: "demo",
    employee: null,
    division: "Бамбук",
    location: m.type === "C" ? "Цех · терминал 2" : "Проходная Бамбук",
    trackType: m.type,
    markType: "F",
    rawTime: m.at,
    localTime: `${m.at}:00`,
    occurredAt: `${m.at}:00Z`,
    importedAt: `${m.at}:00Z`,
    missingSince: null,
  }));
}

function summarize(days: HrDay[]): HrSummary {
  const s: HrSummary = { planDays: 0, presentDays: 0, lateDays: 0, lateMinutes: 0, earlyDays: 0, earlyMinutes: 0, absentDays: 0, leaveDays: 0, unknownDays: 0, noExitDays: 0, workedMinutes: 0, planMinutes: 0 };
  for (const d of days) {
    if (d.status === "NotEmployed") continue;
    if (d.dayKind === "W") {
      s.planDays++;
      s.planMinutes += d.planMinutes ?? 0;
    }
    if (d.status === "Present") s.presentDays++;
    if (d.status === "Absent") s.absentDays++;
    if (d.status === "OnLeave") s.leaveDays++;
    if (d.status === "Unknown") s.unknownDays++;
    if (d.late) (s.lateDays++, (s.lateMinutes += d.lateMinutes ?? 0));
    if (d.earlyLeave) (s.earlyDays++, (s.earlyMinutes += d.earlyLeaveMinutes ?? 0));
    if (d.noExit) s.noExitDays++;
    s.workedMinutes += d.workedMinutes ?? 0;
  }
  return s;
}

export function mockEmployee(kind: EmployeeDemo): HrEmployeeCard {
  let list = rows;
  if (kind === "dismissed") list = rows.filter((r) => r[0] <= "2026-10-24");
  let days = list.map(day);
  let events = list.flatMap((r, i) => marks(r, i * 20 + 1));
  for (const d of days) d.events = events.filter((e) => e.localTime.startsWith(d.date)).length;
  if (kind === "empty") {
    days = days.map((d) => ({ ...d, status: "NoSchedule", dayKind: null, planBegin: null, planEnd: null, planMinutes: null, firstIn: null, lastOut: null, events: 0, late: false, lateMinutes: null, earlyLeave: false, earlyLeaveMinutes: null, workedMinutes: null, workedOnDayOff: false, reason: "на этот день у сотрудника нет графика в Verifix" }));
    events = [];
  }
  const dismissed = kind === "dismissed";
  return {
    employee: {
      id: "demo",
      verifixId: 151368,
      fullName: "QAXRAMONOV SARDOR BAXTIYOR O'G'LI",
      phone: kind === "1" ? null : "998901234567",
      status: dismissed ? "D" : "W",
      hiringDate: "2024-09-09",
      dismissalDate: dismissed ? "2026-10-24" : null,
      divisionId: 1,
      division: "Бамбук",
      job: "Разнорабочий",
      schedule: "ПН–СБ 07:00–19:00, день и ночь",
      missingSince: null,
      lastEventAt: events.at(-1)?.localTime ?? null,
      today: dismissed ? "NotEmployed" : "Unknown",
      todayLate: false,
    },
    middleName: "Baxtiyor o'g'li",
    email: null,
    orgUnit: "Бамбук",
    note: kind === "1" ? null : "Работает по индивидуальному графику с 01.10",
    noteUpdatedAt: kind === "1" ? null : "2026-10-28T05:24:00Z",
    noteUpdatedBy: kind === "1" ? null : "Administrator",
    firstSyncedAt: "2026-09-28T10:00:00Z",
    lastSyncedAt: "2026-10-29T22:55:00Z",
    canEditNote: true,
    from: list.at(-1)![0],
    to: list[0][0],
    days,
    events,
    summary: summarize(days),
    verifixUrl: "https://app.verifix.com",
    sync: {
      configured: true,
      enabled: true,
      running: false,
      lastSuccessAt: kind === "stale" ? "2026-10-28T04:10:00Z" : "2026-10-29T22:55:00Z",
      lastRunAt: kind === "stale" ? "2026-10-29T22:50:00Z" : "2026-10-29T22:55:00Z",
      lastStatus: kind === "stale" ? "Failed" : "Succeeded",
      lastMessage: kind === "stale" ? "Verifix: 401 — неверный client_secret" : null,
      tracksThrough: "2026-10-30T03:50:00",
      timesheetFrom: "2026-10-01",
      timesheetTo: "2026-10-30",
    },
  };
}
