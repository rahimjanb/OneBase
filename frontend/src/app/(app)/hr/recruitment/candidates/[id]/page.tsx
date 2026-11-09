import { Suspense } from "react";
import { CandidateCardView } from "@/components/recruitment/Candidates";
import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Кандидат · Подбор · OneBase") };
}

export default async function CandidatePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const ctx = await loadRecruitment(`/hr/recruitment/candidates/${id}`);
  return (
    <RecruitmentFrame title="Кандидат" ctx={ctx}>
      <Suspense>
        <CandidateCardView ctx={ctx} id={id} />
      </Suspense>
    </RecruitmentFrame>
  );
}
