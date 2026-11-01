import { RequestListPage, type SearchParams } from "@/components/requests/RequestListPage";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Заявки отдела · OneBase") };
}

/** «Заявки отдела»: отправленные заявки отделов, которыми руководит пользователь (и его собственные). */
export default async function DepartmentRequestsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const t = await getT();
  return (
    <RequestListPage
      scope="department"
      title={t("Заявки отдела")}
      subtitle={t("Заявки сотрудников отделов, которыми вы руководите")}
      sp={await searchParams}
      returnTo="/requests/department"
      empty="Заявок отдела по этому фильтру нет."
    />
  );
}
