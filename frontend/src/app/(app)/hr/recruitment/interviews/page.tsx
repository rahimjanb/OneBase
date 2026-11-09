import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { InterviewsView } from "@/components/recruitment/Flow";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Собеседования · Подбор · OneBase") };
}

export default async function Page() {
  const ctx = await loadRecruitment("/hr/recruitment/interviews");
  return (
    <RecruitmentFrame title="Собеседования" subtitle="Время — из календаря OneBase, кандидат подтверждает в Telegram" ctx={ctx}>
      <InterviewsView ctx={ctx} />
    </RecruitmentFrame>
  );
}
