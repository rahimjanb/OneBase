import { notFound } from "next/navigation";
import { VerifixTabs } from "@/components/hr/HrBits";
import { SyncJournal } from "@/components/hr/SyncJournal";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import type { HrSyncRun, PageResult, VerifixDetails } from "@/lib/hr";
import { apiGet } from "@/lib/server-api";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Журнал синхронизации · Verifix · OneBase") };
}

/** «Настройки → Интеграции → HR → Verifix → Журнал синхронизации»: запуски обмена с Verifix (право «Интеграции» — проверяет backend). */
export default async function VerifixJournalPage({ params }: { params: Promise<{ dept: string; code: string }> }) {
  const { dept, code } = await params;
  if (code !== "verifix") notFound();

  const path = `/settings/integrations/${dept}/${code}/journal`;
  const details = await apiGet<VerifixDetails>("/api/integrations/verifix", path);
  if (details.departmentCode !== dept) notFound();
  const runs = await apiGet<PageResult<HrSyncRun>>("/api/integrations/verifix/runs?page=1&pageSize=30", path);
  const t = await getT();

  return (
    <>
      <PageHeader
        title={details.name}
        subtitle={t(details.description)}
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Настройки"), href: "/settings" },
          { label: t("Интеграции"), href: "/settings/integrations" },
          { label: t(details.departmentName), href: `/settings/integrations/${dept}` },
          { label: details.name, href: `/settings/integrations/${dept}/${code}` },
          { label: t("Журнал синхронизации") },
        ]}
      />
      <PageBody>
        <VerifixTabs />
        <SyncJournal initial={runs} configured={details.status !== "not_configured" && details.enabled} />
        <p className="mt-3 text-xs text-ink-3">
          {t("Загружено — новые и изменённые строки, пропущено — пришедшие без изменений. Повторная загрузка не создаёт дубликатов: сотрудники, отметки и дни табеля сопоставляются по ID Verifix. Запуск, переставший отвечать дольше 15 минут, закрывается как прерванный.")}
        </p>
      </PageBody>
    </>
  );
}
