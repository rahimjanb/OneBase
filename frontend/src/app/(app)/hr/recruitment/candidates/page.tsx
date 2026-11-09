import { Suspense } from "react";
import { CandidatesView } from "@/components/recruitment/Candidates";
import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Кандидаты · Подбор · OneBase") };
}

/** Единая база кандидатов: из HH, Telegram и вручную — один человек одной карточкой. */
export default async function CandidatesPage() {
  const ctx = await loadRecruitment("/hr/recruitment/candidates");
  return (
    <RecruitmentFrame title="Кандидаты" subtitle="Единая база: HH, Telegram и вручную — один человек одной карточкой" ctx={ctx}>
      <Suspense>
        <CandidatesView ctx={ctx} />
      </Suspense>
    </RecruitmentFrame>
  );
}
