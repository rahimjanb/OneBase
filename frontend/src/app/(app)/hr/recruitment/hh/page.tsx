import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { HhView } from "@/components/recruitment/Flow";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("HH.uz · Подбор · OneBase") };
}

export default async function Page() {
  const ctx = await loadRecruitment("/hr/recruitment/hh");
  return (
    <RecruitmentFrame title="HH.uz" subtitle="Отклики и резюме из официального API HeadHunter" ctx={ctx}>
      <HhView ctx={ctx} />
    </RecruitmentFrame>
  );
}
