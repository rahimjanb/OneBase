import { loadHr } from "@/components/hr/HrPage";
import { OverviewView } from "@/components/hr/overview/OverviewView";
import layout from "@/components/hr/overview/overview.module.css";
import styles from "@/components/hr/timesheet/timesheet.module.css";
import { getT } from "@/i18n/server";
import { dateParam, param, qs, todayTashkent, type HrOverview } from "@/lib/hr";
import { dayCounts, sumCounts } from "@/lib/hr-overview";
import type { OverviewDemo } from "@/lib/hr-overview.mock";
import { apiGet, ApiRequestError } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("HR · OneBase") };
}

type Search = Promise<Record<string, string | string[] | undefined>>;

const DEMOS: OverviewDemo[] = ["1", "error", "off", "empty", "quiet", "past"];

/** Текст ошибки API: { error } или как пришёл. */
function messageOf(error: ApiRequestError): string {
  try {
    const json = JSON.parse(error.message) as { error?: string; detail?: string; title?: string };
    return json.error ?? json.detail ?? json.title ?? error.message;
  } catch {
    return error.message;
  }
}

/**
 * «Обзор HR» за день (?date=, по умолчанию сегодня) по данным Verifix: что с явкой и на кого обратить внимание. Статусы считает backend
 * (HrAttendance): «отсутствует» — только рабочий день по графику без документа и при полностью загруженных отметках.
 * ?demo=1|error|off|empty|quiet|past в разработке — демо-набор из макета для проверки вёрстки и состояний.
 */
export default async function HrOverviewPage({ searchParams }: { searchParams: Search }) {
  const sp = await searchParams;
  const ctx = await loadHr("/hr");
  const demoKind = DEMOS.find((d) => d === param(sp.demo));
  const demo = process.env.NODE_ENV !== "production" && !!demoKind;

  let overview: HrOverview | null = null;
  let error: string | null = null;
  let today = todayTashkent();
  let now = Date.now();
  if (demo && demoKind) {
    const mock = (await import("@/lib/hr-overview.mock")).mockOverview(demoKind);
    overview = mock.overview;
    now = mock.now;
    today = "2026-10-30";
  } else {
    try {
      overview = await apiGet<HrOverview>(`/api/hr/overview${qs({ date: dateParam(sp.date) })}`, "/hr");
      today = overview.isToday ? overview.date : today;
    } catch (e) {
      // Вход и «нет доступа» — переходы (их не перехватываем); ошибка API — баннер с «Повторить».
      if (!(e instanceof ApiRequestError)) throw e;
      error = messageOf(e);
    }
  }

  // Шесть итогов дня должны давать «по графику»: расхождение — ошибка расчёта, её видно в логе сервера.
  if (overview && sumCounts(dayCounts(overview)) !== overview.scheduled) {
    console.warn(`HR overview ${overview.date}: сумма статусов ${sumCounts(dayCounts(overview))} ≠ по графику ${overview.scheduled}`);
  }

  return (
    <div className={`${styles.root} ${layout.page} min-h-full`}>
      <OverviewView o={overview} error={error} today={today} now={now} manages={ctx.managesIntegration} demo={demo} />
    </div>
  );
}
