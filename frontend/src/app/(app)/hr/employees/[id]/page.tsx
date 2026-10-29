import { notFound } from "next/navigation";
import { EmployeeView } from "@/components/hr/employee/EmployeeView";
import { loadHr } from "@/components/hr/HrPage";
import layout from "@/components/hr/overview/overview.module.css";
import styles from "@/components/hr/timesheet/timesheet.module.css";
import { getT } from "@/i18n/server";
import { dateParam, param, qs, todayTashkent, type HrEmployeeCard } from "@/lib/hr";
import type { EmployeeDemo } from "@/lib/hr-employee.mock";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Сотрудник · HR · OneBase") };
}

type Props = { params: Promise<{ id: string }>; searchParams: Promise<Record<string, string | string[] | undefined>> };

const DEMOS: EmployeeDemo[] = ["1", "empty", "dismissed", "stale"];

/** Фильтры списка сотрудников для «← Сотрудники»: только известные параметры — не произвольный адрес. */
function backHref(back: string | undefined): string {
  if (!back) return "/hr/employees";
  const source = new URLSearchParams(back);
  const keep = new URLSearchParams();
  for (const key of ["search", "divisionId", "status", "sort", "page"]) {
    const value = source.get(key);
    if (value) keep.set(key, value);
  }
  const q = keep.toString();
  return q ? `/hr/employees?${q}` : "/hr/employees";
}

/**
 * Карточка сотрудника: как человек работает за период и есть ли что проверить. Поля Verifix (только чтение), заметка HR (локально), итоги,
 * подсказка, полоса дней и посещаемость по дням с отметками. Период — ?from=&to= (по умолчанию 2 недели, у уволенного — по день увольнения).
 * /hr/employees/demo?demo=1|empty|dismissed|stale в разработке — демо-карточка из макета.
 */
export default async function HrEmployeePage({ params, searchParams }: Props) {
  const { id } = await params;
  const sp = await searchParams;
  const path = `/hr/employees/${id}`;
  const ctx = await loadHr(path);
  const demoKind = DEMOS.find((d) => d === param(sp.demo));
  const demo = process.env.NODE_ENV !== "production" && id === "demo" && !!demoKind;

  let card: HrEmployeeCard | null = null;
  let today = todayTashkent();
  if (demo && demoKind) {
    const mock = await import("@/lib/hr-employee.mock");
    card = mock.mockEmployee(demoKind);
    today = mock.demoToday;
  } else if (/^[0-9a-f-]{36}$/i.test(id)) {
    card = await apiGetOrNull<HrEmployeeCard>(`/api/hr/employees/${id}${qs({ from: dateParam(sp.from), to: dateParam(sp.to) })}`, path);
  }
  if (!card) notFound();

  return (
    <div className={`${styles.root} ${layout.page} min-h-full`}>
      <EmployeeView key={`${card.employee.id}:${card.from}:${card.to}`} data={card} today={today} manages={ctx.managesIntegration} backHref={backHref(param(sp.back))} demo={demo} />
    </div>
  );
}
