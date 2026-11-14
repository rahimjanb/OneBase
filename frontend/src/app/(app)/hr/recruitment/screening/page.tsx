import { Suspense } from "react";
import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { ScreeningSkeleton, ScreeningView } from "@/components/recruitment/screening/ScreeningView";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("AI-скрининг · Подбор · OneBase") };
}

/** AI-скрининг: рейтинг кандидатов под вакансию (0–100 по шести критериям), карточки, сравнение, ручной импорт резюме. */
export default async function ScreeningPage() {
  const ctx = await loadRecruitment("/hr/recruitment/screening");
  return (
    <RecruitmentFrame title="AI-скрининг" subtitle="Рейтинг кандидатов под вакансию: оценка 0–100 с объяснением и доказательствами из резюме" ctx={ctx}>
      <Suspense fallback={<ScreeningSkeleton />}>
        <ScreeningView ctx={ctx} />
      </Suspense>
    </RecruitmentFrame>
  );
}
