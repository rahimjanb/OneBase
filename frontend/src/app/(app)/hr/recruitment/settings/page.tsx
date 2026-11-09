import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { AiSettingsCard } from "@/components/recruitment/Flow";
import { RecruitmentSettingsView } from "@/components/recruitment/Requests";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Настройки подбора · HR · OneBase") };
}

export default async function RecruitmentSettingsPage() {
  const ctx = await loadRecruitment("/hr/recruitment/settings");
  return (
    <RecruitmentFrame title="Настройки подбора" ctx={ctx}>
      <div className="space-y-4">
        <AiSettingsCard ctx={ctx} />
        <RecruitmentSettingsView ctx={ctx} />
      </div>
    </RecruitmentFrame>
  );
}
