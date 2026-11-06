import { redirect } from "next/navigation";
import { QualityQueue } from "@/components/quality/QualityQueue";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { canOpen, homeFor } from "@/lib/navigation";
import type { Page, WarehousesView } from "@/lib/procurement";
import type { QualityCheckRow, QualityNav } from "@/lib/quality";
import { apiGet, apiSession, apiTry } from "@/lib/server-api";
import { qs } from "@/lib/tasks";
import type { Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Качество · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/**
 * «Производство → Качество»: партии сырья на контроле — сначала самые давние (шаг, позиция, поставщик, склад, сколько дней ждут) и архив
 * решений. Видят лаборатория, назначенные по качеству, кладовщик, снабжение и директор; вносит проверки лаборатория, решает назначенный.
 */
export default async function QualityPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const me = await apiSession<Me>("/api/auth/me");
  if (me && !canOpen("/production", me.permissions)) redirect(homeFor(me.permissions));
  const t = await getT();
  const sp = await searchParams;
  const path = "/production/quality";
  const nav = await apiGet<QualityNav>("/api/quality/nav", path);
  if (!nav.sees) redirect(me ? homeFor(me.permissions) : "/");
  const [data, stores] = await Promise.all([
    apiGet<Page<QualityCheckRow>>(
      `/api/quality/checks${qs({ status: one(sp, "status") ?? "open", q: one(sp, "q"), warehouse: one(sp, "warehouse"), page: one(sp, "page"), pageSize: 50 })}`,
      path,
    ),
    apiTry<WarehousesView>("/api/procurement/warehouses"),
  ]);
  return (
    <>
      <PageHeader
        title={t("Качество")}
        subtitle={t("Партии сырья на контроле: документы, лаборатория, решение — до решения партия не выдаётся")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Производство"), href: "/production" }, { label: t("Качество") }]}
      />
      <PageBody>
        <QualityQueue data={data} nav={nav} warehouses={stores?.warehouses ?? []} />
      </PageBody>
    </>
  );
}
