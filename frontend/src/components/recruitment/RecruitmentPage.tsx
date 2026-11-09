import { redirect } from "next/navigation";
import { HrHeader } from "@/components/hr/HrPage";
import { PageBody } from "@/components/shell/PageHeader";
import { RecruitmentNav } from "@/components/recruitment/ui";
import type { RecruitmentContext } from "@/lib/recruitment";
import { apiGet } from "@/lib/server-api";
import { getT } from "@/i18n/server";

/**
 * Доступ к подбору решает backend: нет доступа — «Нет доступа»; руководитель отдела без права «Подбор» видит только свои заявки на
 * сотрудника — с остальных страниц подбора его ведёт туда.
 */
export async function loadRecruitment(path: string, candidates = true): Promise<RecruitmentContext> {
  const ctx = await apiGet<RecruitmentContext>("/api/hr/recruitment/context", path);
  if (candidates && !ctx.canRead) redirect("/hr/recruitment/requests");
  return ctx;
}

export async function RecruitmentFrame({ title, subtitle, ctx, children }: { title: string; subtitle?: string; ctx: RecruitmentContext; children: React.ReactNode }) {
  const t = await getT();
  return (
    <>
      <HrHeader title={title} subtitle={subtitle} crumb={[{ label: t("Подбор"), href: ctx.canRead ? "/hr/recruitment" : undefined }, { label: t(title) }]} />
      <PageBody>
        {ctx.canRead && <RecruitmentNav canManage={ctx.canManage} />}
        {children}
      </PageBody>
    </>
  );
}
