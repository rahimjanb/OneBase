import { loadHr } from "@/components/hr/HrPage";
import layout from "@/components/hr/overview/overview.module.css";
import { ReportView } from "@/components/hr/reports/ReportView";
import styles from "@/components/hr/timesheet/timesheet.module.css";
import { getT } from "@/i18n/server";
import { dateParam, param, todayTashkent, type HrReport } from "@/lib/hr";
import { parseDepts } from "@/lib/hr-report";
import { apiGet, ApiRequestError } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Отчёты · HR · OneBase") };
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
 * «Отчёт по посещаемости» за период (до квартала) по данным Verifix: где проблемы — какие отделы и какие люди; таблица и выгрузка в Excel
 * полные. Период и подразделения — параметры адреса (?from=&to=&dept=1,2), по умолчанию — текущий месяц по сегодня. Формулы явки,
 * опозданий и отсутствий — на сервере (HrService), страница их только показывает.
 */
export default async function HrReportsPage({ searchParams }: { searchParams: Search }) {
  const sp = await searchParams;
  const ctx = await loadHr("/hr/reports");
  const today = todayTashkent();
  const to = dateParam(sp.to) ?? today;
  const from = dateParam(sp.from) ?? `${to.slice(0, 8)}01`;
  const query = new URLSearchParams({ from, to });
  for (const id of parseDepts(param(sp.dept))) query.append("divisionId", String(id));

  let report: HrReport | null = null;
  let error: string | null = null;
  try {
    report = await apiGet<HrReport>(`/api/hr/reports?${query.toString()}`, "/hr/reports");
  } catch (e) {
    // Вход и «нет доступа» — переходы (их не перехватываем); ошибка API (например, слишком длинный период) — баннер с «Повторить».
    if (!(e instanceof ApiRequestError)) throw e;
    error = messageOf(e);
  }

  return (
    <div className={`${styles.root} ${layout.page} min-h-full`}>
      <ReportView report={report} error={error} divisions={ctx.divisions} today={today} now={Date.now()} manages={ctx.managesIntegration} />
    </div>
  );
}
