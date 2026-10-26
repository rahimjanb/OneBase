/**
 * Демо-набор «Рабочего времени и табелей» (ТЗ редизайна, 01–26 октября 2026; ФИО обезличены): только для проверки вёрстки в разработке — страница
 * берёт его при ?demo=1 и NODE_ENV !== "production". В рабочей сборке не используется: там только данные Verifix из API.
 */
import type { HrSummary, HrSyncBrief, HrTimesheetRow } from "@/lib/hr";
import type { TimesheetData } from "@/lib/hr-timesheet";

export type MockRowSource = {
  /** ФИО как в Verifix. */
  fullName: string;
  /** Буквы дней 01→26: через пробел, «-» — нет данных, «ОТС×6» — повтор. */
  codes: string;
  /** [явка, опоздания, отсутствия, документы, отработано ч, план ч]. */
  totals: [number, number, number, number, number, number];
};

const DIVISION = "3-этаж";

/** Строка с отдельными днями: «ОТС, 14: Я, 16: Я, остальное «-»». */
const sparse = (first: string[], at: Record<number, string> = {}) =>
  Array.from({ length: 26 }, (_, i) => at[i + 1] ?? first[i] ?? "-").join(" ");

export const mockRows: MockRowSource[] = [
  { fullName: "XODIMOV BIRINCHI OTA O'G'LI", codes: "ОТС ОТС ? В ? ОП ОП ? ОП РУ В ОТС ОТС РУ РУ ОП Я В ОП РУ Я ОП ОП ОП В ОП", totals: [15, 9, 4, 0, 119.2, 264] },
  { fullName: "XODIMOVA IKKINCHI OTA QIZI", codes: sparse(["ОТС"], { 14: "Я", 16: "Я" }), totals: [2, 0, 1, 0, 1.2, 12] },
  { fullName: "XODIMOV UCHINCHI OTA O'G'LI", codes: sparse(["ОТС"]), totals: [0, 0, 1, 0, 0, 12] },
  { fullName: "XODIMOV TO'RTINCHI OTA O'G'LI", codes: "ОП ОП ОТС В РУ РУ ОТС ? ОП РУ РВ РУ РУ РУ Я ОТС ? В ОТС ОТС ОТС ОТС ОТС ОТС В ОТС", totals: [11, 3, 10, 0, 129.1, 264] },
  { fullName: "XODIMOVA BESHINCHI OTAYEVNA", codes: sparse(["ОТС", "ОТС", "ОТС", "В", "ОП", "ОТС"]), totals: [1, 1, 4, 0, 11, 60] },
  { fullName: "XODIMOVA OLTINCHI OTA QIZI", codes: "ОП ОП ОТС В ОП ОП ОТС ОТС РУ ОП РВ ОП ? ОП РУ ОП ? РВ РУ РУ ОП ОП ОП ? В ОП", totals: [18, 12, 3, 0, 201.2, 264] },
  { fullName: "XODIMOV YETTINCHI OTAYEVICH", codes: "РУ РУ Я В РУ РУ Я РУ ОТС ОП РВ РУ РУ РУ РУ РУ РУ В РУ РУ РУ РУ РУ Я В Я", totals: [22, 1, 1, 0, 233.9, 264] },
  { fullName: "XODIMOV SAKKIZINCHI OTA O'G'LI", codes: "ОП ОП ОТС В РУ ОП ОТС ОП РУ ОП В РУ ОП РУ РУ ОП РУ В ОП ОП ОП ОП ОТС ОТС В ОТС", totals: [17, 11, 5, 0, 164.4, 264] },
  {
    fullName: "XODIMOVA TO'QQIZINCHI OTAYEVNA",
    codes: `${Array.from({ length: 13 }, () => "-").join(" ")} ? ОП ОП ОТС В ОП РУ ОП ОП ОП ОП В ОП`,
    totals: [9, 8, 1, 0, 84.1, 132],
  },
  { fullName: "XODIMOVA O'NINCHI OTAYEVNA", codes: "ОТС ОТС ОТС В ОТС ОТС ОТС ОТС ? Я РВ Я Я Я Я Я Я В Я Я Я Я Я Я В ОП", totals: [15, 1, 7, 0, 153.2, 220] },
  { fullName: "XODIMOV O'N BIRINCHI O'G'LI", codes: "ОТС ОТС ОТС В ОТС×6 В ОТС×6 В ОТС×6 В ОТС", totals: [0, 0, 22, 0, 0, 264] },
];

const expand = (codes: string) =>
  codes
    .split(/\s+/)
    .flatMap((token) => {
      const m = /^(.+)×(\d+)$/.exec(token);
      return m ? Array.from({ length: Number(m[2]) }, () => m[1]) : [token];
    })
    .map((c) => (c === "-" ? "" : c));

const summaryOf = (codes: string[], totals: MockRowSource["totals"]): HrSummary => ({
  planDays: codes.filter((c) => c !== "" && c !== "В" && c !== "РВ").length,
  presentDays: totals[0],
  lateDays: totals[1],
  lateMinutes: totals[1] * 18,
  earlyDays: codes.filter((c) => c === "РУ").length,
  earlyMinutes: codes.filter((c) => c === "РУ").length * 35,
  absentDays: totals[2],
  leaveDays: totals[3],
  unknownDays: codes.filter((c) => c === "?").length,
  noExitDays: 0,
  workedMinutes: Math.round(totals[4] * 60),
  planMinutes: Math.round(totals[5] * 60),
});

const dates = Array.from({ length: 26 }, (_, i) => `2026-10-${String(i + 1).padStart(2, "0")}`);

const rows: HrTimesheetRow[] = mockRows.map((r, i) => {
  const codes = expand(r.codes);
  return { employeeId: `00000000-0000-4000-8000-${String(i + 1).padStart(12, "0")}`, employee: r.fullName, division: DIVISION, job: null, summary: summaryOf(codes, r.totals), codes };
});

const sync: HrSyncBrief = {
  configured: true,
  enabled: true,
  running: false,
  lastSuccessAt: "2026-10-26T16:12:00Z",
  lastRunAt: "2026-10-26T16:11:40Z",
  lastStatus: "Succeeded",
  lastMessage: null,
  tracksThrough: "2026-10-26T21:12:00",
  timesheetFrom: "2026-10-01",
  timesheetTo: "2026-10-26",
};

/** Итоги сводки — как в макете (по всей организации, 276 сотрудников), строки — первые 11. */
export const mockTimesheet: TimesheetData = {
  from: "2026-10-01",
  to: "2026-10-26",
  dates,
  rows,
  total: {
    planDays: 5457,
    presentDays: 4622,
    lateDays: 2071,
    lateMinutes: 0,
    earlyDays: 0,
    earlyMinutes: 0,
    absentDays: 1017,
    leaveDays: 0,
    unknownDays: 0,
    noExitDays: 0,
    workedMinutes: Math.round(47120.9 * 60),
    planMinutes: 61402 * 60,
  },
  sync,
  employeesTotal: 276,
};

export const mockToday = "2026-10-26";
