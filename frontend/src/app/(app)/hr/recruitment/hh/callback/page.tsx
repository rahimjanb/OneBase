import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { HhCallback } from "@/components/recruitment/Flow";

/** Возврат с hh.ru после входа работодателя (redirect_uri приложения HH). */
export default async function HhCallbackPage() {
  const ctx = await loadRecruitment("/hr/recruitment/hh/callback");
  return (
    <RecruitmentFrame title="HH.uz" ctx={ctx}>
      <HhCallback />
    </RecruitmentFrame>
  );
}
