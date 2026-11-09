import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { RequestsView } from "@/components/recruitment/Requests";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Заявки на сотрудника · HR · OneBase") };
}

/** Заявки на сотрудника: руководители отделов подают, HR решает. */
export default async function HiringRequestsPage() {
  const ctx = await loadRecruitment("/hr/recruitment/requests", false);
  return (
    <RecruitmentFrame title="Заявки на сотрудника" subtitle="Руководитель отдела просит человека — HR одобряет и открывает вакансию" ctx={ctx}>
      <RequestsView ctx={ctx} />
    </RecruitmentFrame>
  );
}
