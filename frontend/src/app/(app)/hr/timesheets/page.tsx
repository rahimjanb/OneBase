import { loadHr } from "@/components/hr/HrPage";
import styles from "@/components/hr/timesheet/timesheet.module.css";
import { TimesheetView } from "@/components/hr/timesheet/TimesheetView";
import { getT } from "@/i18n/server";
import { dateParam, param, qs, todayTashkent, type HrTimesheet } from "@/lib/hr";
import type { TimesheetData } from "@/lib/hr-timesheet";
import { apiGet, ApiRequestError } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Рабочее время и табели · HR · OneBase") };
}

type Search = Promise<Record<string, string | string[] | undefined>>;

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
 * «Рабочее время и табели»: табель за период (до 62 дней) по графикам Verifix — буква каждого дня, явка, опоздания, отсутствия,
 * документы и отработанные часы. Период, подразделение и поиск — параметры запроса к API; остальное (нарушения, сортировка, страницы,
 * плотность) экран применяет сам. ?demo=1 в разработке — демо-набор из ТЗ редизайна для проверки вёрстки.
 */
export default async function HrTimesheetsPage({ searchParams }: { searchParams: Search }) {
  const sp = await searchParams;
  const ctx = await loadHr("/hr/timesheets");
  const demo = process.env.NODE_ENV !== "production" && param(sp.demo) === "1";

  let data: TimesheetData | null = null;
  let error: string | null = null;
  let today = todayTashkent();
  let now = Date.now();
  if (demo) {
    const mock = await import("@/lib/hr-timesheet.mock");
    data = mock.mockTimesheet;
    today = mock.mockToday;
    now = Date.parse("2026-10-26T16:30:00Z");
  } else {
    const to = dateParam(sp.to) ?? today;
    const from = dateParam(sp.from) ?? `${to.slice(0, 8)}01`;
    try {
      data = await apiGet<HrTimesheet>(`/api/hr/timesheet${qs({ from, to, divisionId: param(sp.divisionId), search: param(sp.search) })}`, "/hr/timesheets");
    } catch (e) {
      // Вход и «нет доступа» — переходы (их не перехватываем); ошибка API — баннер с «Повторить».
      if (!(e instanceof ApiRequestError)) throw e;
      error = messageOf(e);
    }
  }

  return (
    <div className={`${styles.root} min-h-full`}>
      <TimesheetView data={data} error={error} divisions={ctx.divisions} today={today} now={now} canManage={ctx.managesIntegration} demo={demo} />
    </div>
  );
}
