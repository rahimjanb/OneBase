import Link from "next/link";
import { Plus } from "lucide-react";
import { requireProduction } from "@/components/production/guard";
import { OrdersView } from "@/components/production/OrdersView";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ProductionOrderList } from "@/lib/production";
import { apiGet } from "@/lib/server-api";
import { qs } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Задания на выпуск · OneBase") };
}

type SearchParams = Record<string, string | string[] | undefined>;
const one = (sp: SearchParams, key: string) => {
  const v = sp[key];
  return (Array.isArray(v) ? v[0] : v) || undefined;
};

/** «Производство → Задания»: задания на выпуск — черновики, выпущенные, в работе, закрытые. Создаёт и закрывает производство, сырьё выдаёт кладовщик. */
export default async function OrdersPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const path = "/production/orders";
  await requireProduction(path);
  const t = await getT();
  const sp = await searchParams;
  const status = one(sp, "status") ?? "open";
  const data = await apiGet<ProductionOrderList>(
    `/api/production/orders${qs({ q: one(sp, "q"), status: status === "all" ? undefined : status, page: one(sp, "page") })}`,
    path,
  );
  return (
    <>
      <PageHeader
        title={t("Задания на выпуск")}
        subtitle={t("Выпуск по действующей рецептуре: выдача сырья партиями, возврат остатка, закрытие с фактическим выходом и сверкой нормы")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Отделы"), href: "/departments" }, { label: t("Производство"), href: "/production" }, { label: t("Задания") }]}
        actions={
          data.canCreate ? (
            <Link href="/production/orders/new" className="inline-flex h-9 items-center gap-2 rounded-lg bg-accent px-3.5 text-sm font-semibold text-white hover:bg-accent-strong">
              <Plus className="size-4" aria-hidden /> {t("Новое задание")}
            </Link>
          ) : undefined
        }
      />
      <PageBody>
        <OrdersView data={data} />
      </PageBody>
    </>
  );
}
