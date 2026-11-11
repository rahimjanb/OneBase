import { Suspense } from "react";
import { RecruitmentFrame, loadRecruitment } from "@/components/recruitment/RecruitmentPage";
import { NewVacancyButton, VacanciesView } from "@/components/recruitment/VacancyList";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Подбор · HR · OneBase") };
}

/**
 * HR → «Подбор» → «Вакансии» (макеты AItTc, xH9vJ): поиск, статус кнопками, отдел; таблица на компьютере, карточки на телефоне. Поиск,
 * статус и отдел — в адресе (?q=, ?status=, ?department=), ссылкой на отфильтрованный список можно поделиться.
 */
export default async function RecruitmentPage() {
  const ctx = await loadRecruitment("/hr/recruitment");
  return (
    <RecruitmentFrame title="Вакансии" ctx={ctx} mobileAction={ctx.canManage ? <NewVacancyButton round /> : undefined}>
      <Suspense>
        <VacanciesView ctx={ctx} />
      </Suspense>
    </RecruitmentFrame>
  );
}
