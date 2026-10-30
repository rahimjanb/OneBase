import { TIME_ZONE } from "@/lib/format";
import type { HrOverview } from "@/lib/hr";
import { apiTry } from "@/lib/server-api";
import type { GroupView, OverviewView, Period, TargetLevel } from "@/lib/sales/types";
import type { WorkOverview } from "@/lib/tasks";

/**
 * Данные главной «Обзор» из разделов, где они уже есть: продажи (вторичка — план и факт месяца, по дням), HR (явка дня, отчёт за
 * период) и задачи. Каждый источник — по правам пользователя: нет доступа или ответа — null, блок на странице прячется или берёт
 * другой показатель. Быстрое (месяц продаж, явка, задачи) и медленное (12 недель: прошлые месяцы продаж сервер при первом запросе
 * считает секунды) — отдельно, чтобы динамика не держала остальную страницу.
 */

const DAY = 86_400_000;
const parse = (date: string) => new Date(`${date}T00:00:00Z`);
const iso = (d: Date) => d.toISOString().slice(0, 10);
export const addDays = (date: string, n: number) => iso(new Date(parse(date).getTime() + n * DAY));
const mondayOf = (date: string) => addDays(date, 1 - (parse(date).getUTCDay() || 7));

/** Номер недели ISO: «2026-10-30» → 44. */
export function isoWeek(date: string): number {
  const d = parse(date);
  d.setUTCDate(d.getUTCDate() + 4 - (d.getUTCDay() || 7));
  return Math.ceil(((d.getTime() - Date.UTC(d.getUTCFullYear(), 0, 1)) / DAY + 1) / 7);
}

/** Сегодня по времени компании (Ташкент), «2026-10-30». */
export const tashkentToday = () =>
  new Intl.DateTimeFormat("en-CA", { timeZone: TIME_ZONE, year: "numeric", month: "2-digit", day: "2-digit" }).format(new Date());

export type SalesBrief = {
  period: Period;
  /** Факт к плану месяца, 0–1; плана нет — null. */
  execution: number | null;
  forecastExecution: number | null;
  /** Цвет выполнения с сервера: от 90% — зелёный, от 60% — оранжевый, ниже — красный. */
  level: TargetLevel | null;
  revenue: number;
  /** Продажи (кг) к тем же дням прошлого месяца: +0,02 = +2 %. */
  vsPrevMonth: number | null;
};

export type HrBrief = {
  /** Явка сегодня: на месте от «по графику», %. */
  presentPct: number | null;
  /** Прошлый рабочий день в это время, %. */
  comparePct: number | null;
  /** Явка с начала месяца (доля дней с приходом среди решённых рабочих), %. */
  monthRate: number | null;
  /** Последняя успешная синхронизация с Verifix. */
  syncedAt: string | null;
};

export type HomeFast = { today: string; sales: SalesBrief | null; hr: HrBrief | null; tasks: WorkOverview | null };

export type HomeWeek = { start: string; end: string; week: number };

/** 12 недель по понедельникам, последняя — текущая (неполная). Ряд без данных за неделю — null. */
export type HomeTrends = {
  weeks: HomeWeek[];
  /** Сколько дней текущей недели прошло (пн = 1). */
  currentDays: number;
  /** Продажи к плану по темпу недели, %. */
  sales: (number | null)[] | null;
  /** Выручка за неделю, сум. */
  revenue: (number | null)[] | null;
  /** Явка за неделю, %. */
  attendance: (number | null)[] | null;
};

type HrReportBrief = { attendanceRate: number | null };

const sumOf = (values: number[]) => values.reduce((a, b) => a + b, 0);

function salesBrief(overview: OverviewView, republic: GroupView | null): SalesBrief {
  const before = sumOf(republic?.sameDays.map((r) => r.kgBefore) ?? []);
  const now = sumOf(republic?.sameDays.map((r) => r.kgNow) ?? []);
  return {
    period: overview.period,
    execution: overview.kpi.execution,
    forecastExecution: overview.kpi.forecastExecution,
    level: overview.kpi.executionLevel,
    revenue: overview.kpi.revenue,
    vsPrevMonth: before > 0 ? now / before - 1 : null,
  };
}

function hrBrief(o: HrOverview, month: HrReportBrief | null): HrBrief {
  return {
    presentPct: o.scheduled > 0 ? (100 * o.present) / o.scheduled : null,
    comparePct: o.compare?.presentPct ?? null,
    monthRate: month?.attendanceRate ?? null,
    syncedAt: o.sync.lastSuccessAt,
  };
}

/** Все запросы главной — сразу и параллельно; fast и trends — обещания для своих блоков. */
export function loadHome() {
  const today = tashkentToday();
  const overview = apiTry<OverviewView>("/api/sales/overview");
  // Месяц продаж по дням (кг) и «те же дни прошлого месяца» — тот же расчёт, что у раздела «Продажи», сервер держит его в кэше.
  const republic = apiTry<GroupView>("/api/sales/republic");
  const hrOverview = apiTry<HrOverview>("/api/hr/overview");
  const hrMonth = apiTry<HrReportBrief>(`/api/hr/reports?from=${today.slice(0, 8)}01&to=${today}`);
  const tasks = apiTry<WorkOverview>("/api/work/overview");

  const fast: Promise<HomeFast> = Promise.all([overview, republic, hrOverview, hrMonth, tasks]).then(([o, r, h, hm, w]) => ({
    today,
    sales: o ? salesBrief(o, r) : null,
    hr: h ? hrBrief(h, hm) : null,
    tasks: w,
  }));

  const trends: Promise<HomeTrends> = Promise.all([overview, republic]).then(async ([o, r]) => {
    const end = o?.period.today ?? today;
    const lastMonday = mondayOf(end);
    const weeks: HomeWeek[] = Array.from({ length: 12 }, (_, i) => {
      const start = addDays(lastMonday, -7 * (11 - i));
      return { start, end: addDays(start, 6), week: isoWeek(start) };
    });
    const [sales, attendance] = await Promise.all([o && r ? salesWeeks(weeks, o.period, r) : null, hrWeeks(weeks, end)]);
    return {
      weeks,
      currentDays: Math.round((parse(end).getTime() - parse(lastMonday).getTime()) / DAY) + 1,
      sales: sales?.execution ?? null,
      revenue: sales?.revenue ?? null,
      attendance,
    };
  });

  // Задачи — отдельно: блоку «Задачи» и «Фокусу дня» не нужно ждать продажи (их первый расчёт после обновления — секунды).
  return { fast, trends, tasks };
}

type MonthDays = { kg: GroupView; sum: GroupView | null };

/**
 * Продажи по неделям из дневных итогов месяцев (кг и выручка). План недели — по темпу, как у раздела «Продажи»: план месяца ÷ дней
 * в месяце за каждый день недели (неполный сегодняшний — его долей). Факт — в охвате плана: дневной кг × (факт с планом ÷ весь факт
 * месяца), чтобы недели сходились с выполнением месяца. Месяц без плана в неделю не входит.
 */
async function salesWeeks(weeks: HomeWeek[], current: Period, currentDays: GroupView) {
  const months: { year: number; month: number }[] = [];
  for (let y = current.year, m = current.month; ; m === 1 ? ((m = 12), y--) : m--) {
    months.unshift({ year: y, month: m });
    if (`${y}-${String(m).padStart(2, "0")}-01` <= weeks[0].start) break;
  }
  const loaded = await Promise.all(
    months.map(async ({ year, month }): Promise<MonthDays | null> => {
      const isCurrent = year === current.year && month === current.month;
      const [kg, sum] = await Promise.all([
        isCurrent ? currentDays : apiTry<GroupView>(`/api/sales/republic?year=${year}&month=${month}`),
        apiTry<GroupView>(`/api/sales/republic?year=${year}&month=${month}&metric=sum`),
      ]);
      return kg ? { kg, sum } : null;
    }),
  );
  const byMonth = new Map(loaded.filter((x): x is MonthDays => x !== null).map((x) => [`${x.kg.period.year}-${x.kg.period.month}`, x]));

  const execution: (number | null)[] = [];
  const revenue: (number | null)[] = [];
  for (const w of weeks) {
    let fact = 0;
    let plan = 0;
    let money = 0;
    let days = 0;
    for (let i = 0; i < 7; i++) {
      const date = addDays(w.start, i);
      const [y, m, d] = date.split("-").map(Number);
      const data = byMonth.get(`${y}-${m}`);
      if (!data || date > data.kg.period.dataThrough) continue;
      const kg = data.kg.calendar.totalDays[d - 1];
      if (kg == null) continue;
      days++;
      money += data.sum?.calendar.totalDays[d - 1] ?? 0;
      const { kpi, period } = data.kg;
      if (kpi.planKg == null || kpi.planKg <= 0) continue;
      // Сегодняшний неполный день — его долей, как темп у раздела «Продажи».
      const share = !period.closed && date === period.today ? Math.min(1, Math.max(0, (period.elapsedDays ?? period.workedDays) - (period.workedDays - 1))) : 1;
      const scope = kpi.planFactKg != null && kpi.factKg > 0 ? kpi.planFactKg / kpi.factKg : 1;
      fact += kg * scope;
      plan += (kpi.planKg / period.daysInMonth) * share;
    }
    execution.push(plan > 0 ? (100 * fact) / plan : null);
    revenue.push(days > 0 ? money : null);
  }
  return { execution, revenue };
}

/** Явка по неделям — отчёт HR за каждую неделю (до сегодня); недели до начала истории Verifix — null. Нет доступа к HR — null. */
async function hrWeeks(weeks: HomeWeek[], today: string): Promise<(number | null)[] | null> {
  const reports = await Promise.all(
    weeks.map((w) => apiTry<HrReportBrief>(`/api/hr/reports?from=${w.start}&to=${w.end < today ? w.end : today}`)),
  );
  return reports.every((r) => r === null) ? null : reports.map((r) => r?.attendanceRate ?? null);
}
