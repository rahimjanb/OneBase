import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { AnalyticsView } from "@/components/recruitment/Flow";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Аналитика подбора · Подбор · OneBase") };
}

export default async function Page() {
  const ctx = await loadRecruitment("/hr/recruitment/analytics");
  return (
    <RecruitmentFrame title="Аналитика подбора" ctx={ctx}>
      <AnalyticsView  />
    </RecruitmentFrame>
  );
}
