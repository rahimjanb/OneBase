import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { VacancyDetail } from "@/components/recruitment/Vacancies";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Вакансия · Подбор · OneBase") };
}

export default async function VacancyPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const ctx = await loadRecruitment(`/hr/recruitment/vacancies/${id}`);
  return (
    <RecruitmentFrame title="Вакансия" ctx={ctx}>
      <VacancyDetail ctx={ctx} id={id} />
    </RecruitmentFrame>
  );
}
