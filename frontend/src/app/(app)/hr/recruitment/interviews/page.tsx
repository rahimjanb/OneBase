import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { InterviewsBoard } from "@/components/recruitment/Interviews";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Собеседования · Подбор · OneBase") };
}

export default async function Page() {
  const ctx = await loadRecruitment("/hr/recruitment/interviews");
  return (
    <RecruitmentFrame title="Собеседования" subtitle="Одна запись — и в подборе, и в календаре OneBase; кандидат выбирает и подтверждает время в Telegram" ctx={ctx}>
      <InterviewsBoard ctx={ctx} />
    </RecruitmentFrame>
  );
}
