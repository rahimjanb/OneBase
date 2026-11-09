import { requireProduction } from "@/components/production/guard";
import { ImportsHome } from "@/components/production/import/ImportsHome";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { ImportListRow } from "@/lib/recipe-import";
import { apiOutcome } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Импорт техкарт из Excel · OneBase") };
}

/** «Производство → Технолог → Техкарты → Импорт из Excel»: загрузка файла норм и прошлые загрузки. Импортирует технолог. */
export default async function RecipeImportsPage() {
  const path = "/production/recipes/import";
  await requireProduction(path);
  const t = await getT();
  const outcome = await apiOutcome<ImportListRow[]>("/api/production/recipe-imports");
  return (
    <>
      <PageHeader
        title={t("Импорт техкарт из Excel")}
        subtitle={t("Файл норм — продукты по столбцам, сырьё по строкам. Сначала проверка и сверка, затем черновики техкарт — и обычное утверждение.")}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Производство"), href: "/production" },
          { label: t("Технолог"), href: "/production/technologist" },
          { label: t("Техкарты"), href: "/production/recipes" },
          { label: t("Импорт из Excel") },
        ]}
      />
      <PageBody>
        {outcome.state === "ok" ? (
          <ImportsHome imports={outcome.data} />
        ) : (
          <p className="rounded-2xl border border-line bg-surface p-6 text-sm text-ink-2">
            {outcome.state === "forbidden"
              ? t("Импортировать техкарты может технолог — нужно право «Производство: рецептуры».")
              : t("Не удалось загрузить список импортов — обновите страницу.")}
          </p>
        )}
      </PageBody>
    </>
  );
}
