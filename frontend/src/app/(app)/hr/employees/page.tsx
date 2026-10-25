import Link from "next/link";
import { DayStatusBadge, EmployeeStatusBadge, HrFilters, Pager } from "@/components/hr/HrBits";
import { Empty, HrHeader, loadHr, SyncBanner } from "@/components/hr/HrPage";
import { PageBody } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { dayLabel, divisionOptions, type HrEmployeeRow, type PageResult, param, qs } from "@/lib/hr";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Сотрудники · HR · OneBase") };
}

type Search = Promise<Record<string, string | string[] | undefined>>;

/** «Сотрудники»: список из Verifix с подразделением, должностью, графиком, статусом и итогом сегодняшнего дня. */
export default async function HrEmployeesPage({ searchParams }: { searchParams: Search }) {
  const sp = await searchParams;
  const ctx = await loadHr("/hr/employees");
  const query = { search: param(sp.search), divisionId: param(sp.divisionId), status: param(sp.status), sort: param(sp.sort), page: param(sp.page) };
  const result = await apiGet<PageResult<HrEmployeeRow>>(`/api/hr/employees${qs(query)}`, "/hr/employees");
  const t = await getT();

  return (
    <>
      <HrHeader title="Сотрудники" subtitle="Справочник сотрудников из Verifix — источник истины по их данным" />
      <PageBody>
        <SyncBanner sync={ctx.sync} manages={ctx.managesIntegration} />
        <HrFilters
          fields={[
            { kind: "search", name: "search", label: "Поиск", placeholder: "ФИО, телефон или ID Verifix" },
            { kind: "select", name: "divisionId", label: "Подразделение", all: "Все подразделения", options: divisionOptions(ctx.divisions) },
            {
              kind: "select",
              name: "status",
              label: "Статус",
              options: [
                { value: "", label: "Работают" },
                { value: "dismissed", label: "Уволены" },
                { value: "not_hired", label: "Не приняты" },
                { value: "missing", label: "Нет в Verifix" },
                { value: "all", label: "Все" },
              ],
            },
            {
              kind: "select",
              name: "sort",
              label: "Порядок",
              options: [
                { value: "", label: "По ФИО" },
                { value: "division", label: "По подразделению" },
                { value: "hired", label: "Сначала новые" },
              ],
            },
          ]}
        />

        {result.items.length === 0 ? (
          <Empty text="Сотрудников по этому фильтру нет." />
        ) : (
          <div className="overflow-x-auto rounded-xl border border-line bg-surface">
            <table className="w-full min-w-[820px] text-sm">
              <thead>
                <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                  <th className="px-4 py-2.5 font-semibold">{t("Сотрудник")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Подразделение")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Должность")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("График")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Статус")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Сегодня")}</th>
                  <th className="px-3 py-2.5 font-semibold">{t("Последняя отметка")}</th>
                  <th className="px-4 py-2.5 text-right font-semibold">{t("ID Verifix")}</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-line">
                {result.items.map((e) => (
                  <tr key={e.id} className="hover:bg-muted/40">
                    <td className="px-4 py-2.5">
                      <Link href={`/hr/employees/${e.id}`} className="font-medium text-ink hover:text-accent-strong">
                        {e.fullName}
                      </Link>
                      {e.phone && <div className="text-xs text-ink-3">{e.phone}</div>}
                    </td>
                    <td className="px-3 py-2.5 text-ink-2">{e.division ?? "—"}</td>
                    <td className="px-3 py-2.5 text-ink-2">{e.job ?? "—"}</td>
                    <td className="px-3 py-2.5 text-ink-2">{e.schedule ?? "—"}</td>
                    <td className="px-3 py-2.5">
                      <EmployeeStatusBadge status={e.status} missing={!!e.missingSince} />
                      {e.status === "D" && e.dismissalDate && <div className="mt-0.5 text-xs text-ink-3">{t("с {date}", { date: dayLabel(e.dismissalDate, true) })}</div>}
                    </td>
                    <td className="px-3 py-2.5">{e.today && e.today !== "NotEmployed" ? <DayStatusBadge status={e.today} late={e.todayLate} /> : <span className="text-ink-3">—</span>}</td>
                    <td className="px-3 py-2.5 tabular-nums text-ink-2">{e.lastEventAt ? `${dayLabel(e.lastEventAt)} ${e.lastEventAt.slice(11, 16)}` : "—"}</td>
                    <td className="px-4 py-2.5 text-right tabular-nums text-ink-3">{e.verifixId}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <Pager page={result.page} pageSize={result.pageSize} total={result.total} />
      </PageBody>
    </>
  );
}
