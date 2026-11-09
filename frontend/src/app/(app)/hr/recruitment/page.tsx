import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { VacanciesView } from "@/components/recruitment/Vacancies";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Подбор · HR · OneBase") };
}

/** HR → «Подбор» → «Вакансии»: открытые сверху, новая вакансия, переход к воронке кандидатов. */
export default async function RecruitmentPage() {
  const ctx = await loadRecruitment("/hr/recruitment");
  return (
    <RecruitmentFrame title="Вакансии" subtitle="Подбор персонала: вакансии, кандидаты из HH и Telegram, собеседования" ctx={ctx}>
      <VacanciesView ctx={ctx} />
    </RecruitmentFrame>
  );
}
