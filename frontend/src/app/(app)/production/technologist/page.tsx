import { requireProduction } from "@/components/production/guard";
import { TechnologistDashboard } from "@/components/production/TechnologistDashboard";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ProductionDashboard } from "@/lib/production";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Технолог · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** «Производство → Технолог»: панель — выпуск, план, брак, расход к норме, выпечка, задания в работе и что требует внимания (по умолчанию — этот месяц). */
export default async function TechnologistPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const path = "/production/technologist";
  await requireProduction(path);
  const t = await getT();
  const sp = await searchParams;
  const data = await apiGet<ProductionDashboard>(`/api/production/technologist${qs({ from: one(sp, "from"), to: one(sp, "to"), department: one(sp, "department") })}`, path);
  return (
    <>
      <PageHeader
        title={t("Технолог")}
        subtitle={t("Производство по техкартам: выпуск, брак по этапам, расход к норме и выход выпечки — по утверждённым сменам и закрытым заданиям")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Производство"), href: "/production" }, { label: t("Технолог") }]}
      />
      <PageBody>
        <TechnologistDashboard data={data} />
      </PageBody>
    </>
  );
}
