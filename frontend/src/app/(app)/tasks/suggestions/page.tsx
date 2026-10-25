import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { SuggestionsView } from "@/components/tasks/SuggestionsView";
import { taskOptions } from "@/components/tasks/TaskListPage";
import { getT } from "@/i18n/server";
import { apiTry } from "@/lib/server-api";
import type { WorkSuggestion } from "@/lib/tasks";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Предложения AI · Задачи · OneBase") };
}

/** Предложения AI-диспетчера — руководителям отделов и руководству. Остальным — объяснение, кому они доступны. */
export default async function SuggestionsPage() {
  const t = await getT();
  const { heads } = await taskOptions("/tasks/suggestions");
  const items = heads ? ((await apiTry<WorkSuggestion[]>("/api/work/suggestions")) ?? []) : [];
  return (
    <>
      <PageHeader
        title={t("Предложения AI")}
        subtitle={t("AI-диспетчер предлагает задачи по данным OneBase — задача появляется только после вашего подтверждения")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Задачи"), href: "/tasks" }, { label: t("Предложения AI") }]}
      />
      <PageBody>
        {heads ? (
          <SuggestionsView initial={items} />
        ) : (
          <p className="rounded-xl border border-dashed border-line bg-surface px-4 py-10 text-center text-sm text-ink-3">
            {t("Предложения AI-диспетчера получают руководители отделов и руководство. Руководителей отделов назначают во вкладке «Проекты».")}
          </p>
        )}
      </PageBody>
    </>
  );
}
