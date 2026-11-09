import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { TelegramDialogsView } from "@/components/recruitment/TelegramHr";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Диалоги Telegram · Подбор · OneBase") };
}

export default async function Page() {
  const ctx = await loadRecruitment("/hr/recruitment/telegram");
  return (
    <RecruitmentFrame title="Диалоги Telegram" subtitle="Переписка с кандидатами с номера HR: AI или человек" ctx={ctx}>
      <TelegramDialogsView ctx={ctx} />
    </RecruitmentFrame>
  );
}
