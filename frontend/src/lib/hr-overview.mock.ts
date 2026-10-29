/**
 * Демо-набор «Обзора HR» из макета — только для проверки вёрстки в разработке (/hr?demo=1). Варианты состояний:
 * 1 — обычный день, error — ошибка синхронизации, off — Verifix не подключён, empty — подключено, сотрудников нет,
 * quiet — раннее утро, проходов ещё нет, past — прошлый день.
 */
import type { HrOverview, HrSyncBrief } from "@/lib/hr";

export type OverviewDemo = "1" | "error" | "off" | "empty" | "quiet" | "past";

export const DEMOS: OverviewDemo[] = ["1", "error", "off", "empty", "quiet", "past"];

const sync: HrSyncBrief = {
  configured: true,
  enabled: true,
  running: false,
  lastSuccessAt: "2026-10-30T05:22:00Z",
  lastRunAt: "2026-10-30T05:22:00Z",
  lastStatus: "Succeeded",
  lastMessage: null,
  tracksThrough: "2026-10-30T10:22:00",
  timesheetFrom: "2026-10-01",
  timesheetTo: "2026-10-30",
};

const base: HrOverview = {
  date: "2026-10-30",
  now: "2026-10-30T10:24:00",
  isToday: true,
  active: 168,
  activeDeltaMonth: 3,
  scheduled: 168,
  present: 132,
  late: 14,
  absent: 6,
  onLeave: 9,
  expected: 17,
  dayOff: 0,
  noSchedule: 0,
  unknown: 4,
  eventsToday: 1284,
  missingInVerifix: 0,
  avgArrival: "08:56:00",
  avgLateMinutes: 18,
  compare: { date: "2026-10-29", presentPct: 74.2, avgArrival: "09:00:00" },
  lateWeek: 41,
  lateLastWeek: 35,
  passesByHour: [0, 0, 0, 0, 0, 0, 18, 120, 486, 410, 250, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
  attention: [
    { employeeId: "d0000000-0000-0000-0000-000000000001", employee: "Рахимов Бахтиёр", division: "Склад и логистика", kind: "absent", planBegin: "2026-10-30T08:00:00", firstIn: null, lateMinutes: null, events: 0, firstEvent: null },
    { employeeId: "d0000000-0000-0000-0000-000000000002", employee: "Ниязов Олим", division: "Производство", kind: "absent", planBegin: "2026-10-30T09:00:00", firstIn: null, lateMinutes: null, events: 0, firstEvent: null },
    { employeeId: "d0000000-0000-0000-0000-000000000003", employee: "Азизов Тимур", division: "Продажи", kind: "late", planBegin: "2026-10-30T09:00:00", firstIn: "2026-10-30T09:47:00", lateMinutes: 47, events: 1, firstEvent: null },
    { employeeId: "d0000000-0000-0000-0000-000000000004", employee: "Каримова Дилноза", division: "Склад и логистика", kind: "late", planBegin: "2026-10-30T09:00:00", firstIn: "2026-10-30T09:21:00", lateMinutes: 21, events: 2, firstEvent: null },
    { employeeId: "d0000000-0000-0000-0000-000000000005", employee: "Юсупова Малика", division: "Производство", kind: "unknown", planBegin: "2026-10-30T08:00:00", firstIn: null, lateMinutes: null, events: 1, firstEvent: "2026-10-30T08:02:00" },
    { employeeId: "d0000000-0000-0000-0000-000000000006", employee: "Ташкентов Саид", division: "IT", kind: "late", planBegin: "2026-10-30T09:00:00", firstIn: "2026-10-30T09:16:00", lateMinutes: 16, events: 1, firstEvent: null },
    { employeeId: "d0000000-0000-0000-0000-000000000007", employee: "Хасанова Нигора", division: "Бухгалтерия", kind: "late", planBegin: "2026-10-30T09:00:00", firstIn: "2026-10-30T09:12:00", lateMinutes: 12, events: 1, firstEvent: null },
    { employeeId: "d0000000-0000-0000-0000-000000000008", employee: "Пулатов Жасур", division: "Продажи", kind: "unknown", planBegin: null, firstIn: null, lateMinutes: null, events: 0, firstEvent: null },
  ],
  attentionTotal: 24,
  divisions: [
    { id: 1, name: "Склад и логистика", active: 38, scheduled: 38, present: 33, late: 4, absent: 2, onLeave: 2, unknown: 1, events: 290 },
    { id: 2, name: "Продажи", active: 42, scheduled: 42, present: 36, late: 5, absent: 1, onLeave: 2, unknown: 1, events: 310 },
    { id: 3, name: "Бухгалтерия", active: 18, scheduled: 18, present: 14, late: 1, absent: 0, onLeave: 1, unknown: 0, events: 120 },
    { id: 4, name: "Производство", active: 34, scheduled: 34, present: 25, late: 3, absent: 1, onLeave: 2, unknown: 1, events: 330 },
    { id: 5, name: "IT", active: 22, scheduled: 22, present: 15, late: 1, absent: 1, onLeave: 1, unknown: 1, events: 140 },
    { id: 6, name: "Администрация", active: 14, scheduled: 14, present: 9, late: 0, absent: 1, onLeave: 1, unknown: 0, events: 94 },
  ],
  locations: [
    { id: 11, name: "Главный офис", employees: 74, events: 612 },
    { id: 12, name: "Склад Сергели", employees: 38, events: 384 },
    { id: 13, name: "Производство Чиланзар", employees: 25, events: 198 },
    { id: 14, name: "Филиал Самарканд", employees: 9, events: 90 },
  ],
  sync,
  seesAll: true,
};

/** Демо-ответ и «сейчас» (мс) для варианта. */
export function mockOverview(kind: OverviewDemo): { overview: HrOverview; now: number } {
  const now = Date.parse("2026-10-30T05:24:00Z");
  switch (kind) {
    case "error":
      return {
        overview: {
          ...base,
          sync: { ...sync, lastStatus: "Failed", lastRunAt: "2026-10-30T05:02:00Z", lastSuccessAt: "2026-10-30T04:30:00Z", lastMessage: "Verifix: 401 — неверный client_secret" },
        },
        now,
      };
    case "off":
      return { overview: { ...empty(), sync: { ...sync, configured: false, lastSuccessAt: null, lastRunAt: null, lastStatus: null, tracksThrough: null } }, now };
    case "empty":
      return { overview: { ...empty(), sync: { ...sync, lastSuccessAt: null, lastStatus: null, tracksThrough: null } }, now };
    case "quiet":
      return {
        overview: {
          ...base,
          now: "2026-10-30T06:40:00",
          present: 0,
          late: 0,
          absent: 0,
          unknown: 0,
          expected: 159,
          eventsToday: 0,
          avgArrival: null,
          avgLateMinutes: null,
          compare: { date: "2026-10-29", presentPct: 0, avgArrival: null },
          lateWeek: 27,
          passesByHour: Array(24).fill(0),
          attention: [],
          attentionTotal: 0,
          divisions: base.divisions.map((d) => ({ ...d, present: 0, late: 0, absent: 0, unknown: 0, events: 0 })),
          locations: [],
          sync: { ...sync, tracksThrough: "2026-10-30T06:38:00" },
        },
        now: Date.parse("2026-10-30T01:40:00Z"),
      };
    case "past":
      return {
        overview: {
          ...base,
          date: "2026-10-29",
          isToday: false,
          present: 148,
          late: 12,
          absent: 8,
          expected: 0,
          unknown: 3,
          compare: { date: "2026-10-28", presentPct: 86.9, avgArrival: "08:59:00" },
          avgArrival: "08:57:00",
          eventsToday: 1610,
          passesByHour: [0, 0, 0, 0, 0, 0, 20, 130, 470, 380, 120, 40, 60, 80, 50, 40, 30, 70, 120, 20, 0, 0, 0, 0],
          lateWeek: 27,
          lateLastWeek: 30,
        },
        now,
      };
    default:
      return { overview: base, now };
  }
}

function empty(): HrOverview {
  return {
    ...base,
    active: 0,
    activeDeltaMonth: null,
    scheduled: 0,
    present: 0,
    late: 0,
    absent: 0,
    onLeave: 0,
    expected: 0,
    unknown: 0,
    eventsToday: 0,
    avgArrival: null,
    avgLateMinutes: null,
    compare: null,
    lateWeek: 0,
    lateLastWeek: 0,
    passesByHour: Array(24).fill(0),
    attention: [],
    attentionTotal: 0,
    divisions: [],
    locations: [],
  };
}
