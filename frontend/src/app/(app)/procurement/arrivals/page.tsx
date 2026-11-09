import { redirect } from "next/navigation";
import { ArrivalsView } from "@/components/procurement/ArrivalsView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ArrivalPage, ExpectedDelivery } from "@/lib/arrivals";
import type { RequestNav } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Поставки · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/**
 * «Снабжение → Поставки»: приезд машин по тендерам и покупкам «Куплено — привезёт поставщик» в порядке «качество → склад». Ожидаются — что и когда привезут; на приёмке — машины на
 * проверке качества и готовые к приёмке; принято и закрыто — история. Видят кладовщик, лаборатория, снабжение, аналитик и директор.
 */
export default async function ArrivalsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRequests();
  const t = await getT();
  const sp = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/arrivals");
  if (!nav.seesStock && !nav.readsAll && !nav.isDirector) redirect("/procurement");
  const tab = one(sp, "tab") === "open" ? "open" : one(sp, "tab") === "done" ? "done" : "expected";
  // Поставки одного тендера или покупки (из их карточек и «машин на приёмке»).
  const source = one(sp, "source");
  const [expected, open, done] = await Promise.all([
    apiGet<ExpectedDelivery[]>(`/api/procurement/arrivals/expected${qs({ take: "100", source })}`, "/procurement/arrivals"),
    apiGet<ArrivalPage>(`/api/procurement/arrivals${qs({ status: "open", pageSize: "100", source })}`, "/procurement/arrivals"),
    tab === "done" ? apiGet<ArrivalPage>(`/api/procurement/arrivals${qs({ status: "done", page: one(sp, "page"), source })}`, "/procurement/arrivals") : null,
  ]);
  // Открыли «Ожидаются», а ждать нечего, но машины на приёмке есть — сразу на них.
  const shown = tab === "expected" && !one(sp, "tab") && expected.length === 0 && open.total > 0 ? "open" : tab;
  return (
    <>
      <PageHeader
        title={t("Поставки")}
        subtitle={t("Приехала машина: лаборатория проверяет качество, кладовщик взвешивает и принимает на склад")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Поставки") }]}
      />
      <PageBody>
        <ArrivalsView tab={shown} expected={expected} open={open} done={done} source={source} seesTenders={nav.readsAll || nav.isDirector || nav.warehouse} />
      </PageBody>
    </>
  );
}
