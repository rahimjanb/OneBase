import { redirect } from "next/navigation";
import { requireProduction } from "@/components/production/guard";
import { OrderForm } from "@/components/production/OrderForm";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ProductionOrderForm } from "@/lib/production";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Новое задание · OneBase") };
}

/** Новое задание на выпуск — черновик по действующей рецептуре (производство). */
export default async function NewOrderPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const path = "/production/orders/new";
  const nav = await requireProduction(path);
  if (!nav.produces) redirect("/production/orders");
  const t = await getT();
  const sp = await searchParams;
  const recipe = typeof sp.recipe === "string" ? sp.recipe : undefined;
  const form = await apiGet<ProductionOrderForm>("/api/production/orders/form", path);
  return (
    <>
      <PageHeader
        title={t("Новое задание на выпуск")}
        subtitle={t("Рецептура, план и склад — потребность в сырье считается сразу")}
        back="/production/orders"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Отделы"), href: "/departments" },
          { label: t("Производство"), href: "/production" },
          { label: t("Задания"), href: "/production/orders" },
          { label: t("Новое задание") },
        ]}
      />
      <PageBody>
        <OrderForm form={form} recipeId={recipe} />
      </PageBody>
    </>
  );
}
