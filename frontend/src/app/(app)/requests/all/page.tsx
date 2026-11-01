import { RequestListPage, type SearchParams } from "@/components/requests/RequestListPage";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Все заявки · OneBase") };
}

/** «Все заявки»: отправленные заявки всех отделов (снабжение, директор) и свои черновики. */
export default async function AllRequestsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const t = await getT();
  return (
    <RequestListPage
      scope="all"
      title={t("Все заявки")}
      subtitle={t("Заявки на закупку всех отделов: поиск, фильтры и сортировка")}
      sp={await searchParams}
      returnTo="/requests/all"
      empty="Заявок по этому фильтру нет."
    />
  );
}
