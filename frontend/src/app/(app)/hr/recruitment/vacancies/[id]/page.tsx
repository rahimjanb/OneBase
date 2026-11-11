import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { VacancyDetail } from "@/components/recruitment/Vacancies";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Вакансия · Подбор · OneBase") };
}

/** Вакансия; ?edit=1 — сразу форма правки (пункт «Редактировать» в меню списка вакансий). */
export default async function VacancyPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<{ edit?: string }> }) {
  const { id } = await params;
  const { edit } = await searchParams;
  const ctx = await loadRecruitment(`/hr/recruitment/vacancies/${id}`);
  return (
    <RecruitmentFrame title="Вакансия" ctx={ctx} parent={{ label: "Вакансии", href: "/hr/recruitment" }}>
      <VacancyDetail ctx={ctx} id={id} initialEdit={edit === "1" && ctx.canManage} />
    </RecruitmentFrame>
  );
}
