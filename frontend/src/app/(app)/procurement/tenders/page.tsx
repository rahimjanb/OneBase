import Link from "next/link";
import { redirect } from "next/navigation";
import { Plus } from "lucide-react";
import { TendersView } from "@/components/procurement/TendersView";
import { requireRequests } from "@/components/requests/RequestListPage";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { RequestNav, TenderList } from "@/lib/procurement";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Тендеры · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** «Снабжение → Тендеры»: журнал тендерных закупок сырья и материалов для производства — записи снабжения. Видят снабжение и директор. */
export default async function TendersPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  await requireRequests();
  const t = await getT();
  const sp = await searchParams;
  const nav = await apiGet<RequestNav>("/api/procurement/nav", "/procurement/tenders");
  if (!nav.readsAll && !nav.isDirector) redirect("/procurement");
  const data = await apiGet<TenderList>(
    `/api/procurement/tenders${qs({ q: one(sp, "q"), status: one(sp, "status"), from: one(sp, "from"), to: one(sp, "to"), supplier: one(sp, "supplier"), page: one(sp, "page") })}`,
    "/procurement/tenders",
  );
  return (
    <>
      <PageHeader
        title={t("Тендеры")}
        subtitle={t("Журнал закупок сырья и материалов через тендер: победитель, цены, договор и поставка")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Снабжение"), href: "/procurement" }, { label: t("Тендеры") }]}
        actions={
          data.canCreate && (data.total > 0 || one(sp, "q") || one(sp, "status") || one(sp, "from") || one(sp, "to")) ? (
            <Link href="/procurement/tenders/new" className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong">
              <Plus className="size-4" aria-hidden /> {t("Добавить тендер")}
            </Link>
          ) : undefined
        }
      />
      <PageBody>
        <TendersView data={data} />
      </PageBody>
    </>
  );
}
