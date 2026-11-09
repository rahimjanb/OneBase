import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { ExceptionsView } from "@/components/recruitment/Flow";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Исключения · Подбор · OneBase") };
}

export default async function Page() {
  const ctx = await loadRecruitment("/hr/recruitment/exceptions");
  return (
    <RecruitmentFrame title="Исключения" subtitle="Где AI или служба ждёт человека" ctx={ctx}>
      <ExceptionsView ctx={ctx} />
    </RecruitmentFrame>
  );
}
