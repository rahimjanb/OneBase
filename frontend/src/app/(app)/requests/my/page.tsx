import { RequestListPage, type SearchParams } from "@/components/requests/RequestListPage";
import { getT } from "@/i18n/server";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Мои заявки · OneBase") };
}

/** «Мои заявки»: заявки, которые создал пользователь, — черновики, отправленные, на доработке и закрытые. */
export default async function MyRequestsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const t = await getT();
  return (
    <RequestListPage
      scope="my"
      title={t("Мои заявки")}
      subtitle={t("Ваши заявки на закупку: черновики, отправленные и закрытые")}
      sp={await searchParams}
      returnTo="/requests/my"
      empty="Заявок пока нет — создайте первую кнопкой «Новая заявка»."
    />
  );
}
