import { redirect } from "next/navigation";
import { RecruitmentShell } from "@/components/recruitment/RecruitmentShell";
import type { RecruitmentContext } from "@/lib/recruitment";
import { apiGet } from "@/lib/server-api";

/**
 * Доступ к подбору решает backend: нет доступа — «Нет доступа»; руководитель отдела без права «Подбор» видит только свои заявки на
 * сотрудника — с остальных страниц подбора его ведёт туда.
 */
export async function loadRecruitment(path: string, candidates = true): Promise<RecruitmentContext> {
  const ctx = await apiGet<RecruitmentContext>("/api/hr/recruitment/context", path);
  if (candidates && !ctx.canRead) redirect("/hr/recruitment/requests");
  return ctx;
}

/**
 * Рамка страниц подбора: шапка «Подбор» и кнопки разделов (RecruitmentShell). nav = false — без кнопок (заявки на сотрудника — своя
 * вкладка HR); кнопки видит только тот, кому открыт подбор.
 */
export function RecruitmentFrame({
  title,
  subtitle,
  ctx,
  nav = true,
  parent,
  mobileAction,
  children,
}: {
  title: string;
  subtitle?: string;
  ctx: RecruitmentContext;
  nav?: boolean;
  parent?: { label: string; href: string };
  mobileAction?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <RecruitmentShell title={title} subtitle={subtitle} canManage={ctx.canManage} nav={nav && ctx.canRead} parent={parent} mobileAction={mobileAction}>
      {children}
    </RecruitmentShell>
  );
}
