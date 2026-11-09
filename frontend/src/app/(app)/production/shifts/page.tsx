import { requireProduction } from "@/components/production/guard";
import { ShiftsView } from "@/components/production/ShiftsView";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ShiftForm, ShiftList } from "@/lib/production";
import { apiGet, apiTry } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Смены · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** «Производство → Смены»: сменные отчёты по заданиям; новая смена — по выпущенному заданию (?order= — сразу с ним). */
export default async function ShiftsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const path = "/production/shifts";
  await requireProduction(path);
  const t = await getT();
  const sp = await searchParams;
  const [data, form] = await Promise.all([
    apiGet<ShiftList>(`/api/production/shifts${qs({ q: one(sp, "q"), status: one(sp, "status"), order: one(sp, "order"), page: one(sp, "page") })}`, path),
    apiTry<ShiftForm>("/api/production/shifts/form"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Смены")}
        subtitle={t("Сменные отчёты: замесы, выпечка, брак по этапам, упаковка — ввод оператора, закрытие и утверждение")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Производство"), href: "/production" }, { label: t("Смены") }]}
      />
      <PageBody>
        <ShiftsView data={data} form={form} order={one(sp, "order")} />
      </PageBody>
    </>
  );
}
