import { notFound } from "next/navigation";
import { requireProduction } from "@/components/production/guard";
import { ImportScreen } from "@/components/production/import/ImportScreen";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ImportView } from "@/lib/recipe-import";
import { apiGetOrNull } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Загрузка техкарт · OneBase") };
}

/** Загрузка файла техкарт: сводка, рецептуры со статусами, построчная сверка «Excel → техкарта», решения технолога, создание черновиков. */
export default async function RecipeImportPage({ params, searchParams }: { params: Promise<{ id: string }>; searchParams: Promise<{ existing?: string }> }) {
  const { id } = await params;
  const { existing } = await searchParams;
  const path = `/production/recipes/import/${id}`;
  await requireProduction(path);
  const t = await getT();
  const view = await apiGetOrNull<ImportView>(`/api/production/recipe-imports/${id}`, path);
  if (!view) notFound();
  return (
    <>
      <PageHeader
        title={view.fileName}
        subtitle={t("Сверка Excel с техкартами OneBase: что готово, что нужно проверить и почему")}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Производство"), href: "/production" },
          { label: t("Техкарты"), href: "/production/recipes" },
          { label: t("Импорт из Excel"), href: "/production/recipes/import" },
          { label: view.fileName },
        ]}
      />
      <PageBody>
        <ImportScreen initial={view} existing={existing === "1"} />
      </PageBody>
    </>
  );
}
