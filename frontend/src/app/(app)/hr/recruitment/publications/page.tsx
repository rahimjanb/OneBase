import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { PublicationsView } from "@/components/recruitment/TelegramHr";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Публикации · Подбор · OneBase") };
}

export default async function Page() {
  const ctx = await loadRecruitment("/hr/recruitment/publications");
  return (
    <RecruitmentFrame title="Публикации" subtitle="Вакансии в группы и каналы номера HR, источники и лиды" ctx={ctx}>
      <PublicationsView ctx={ctx} />
    </RecruitmentFrame>
  );
}
