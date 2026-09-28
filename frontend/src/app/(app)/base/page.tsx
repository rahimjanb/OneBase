import { BaseOverview } from "@/components/files/BaseOverview";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { departments } from "@/lib/demo-data";

export default function BasePage() {
  return (
    <>
      <PageHeader
        title="Общая база"
        subtitle="Корпоративные документы и данные по отделам"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Общая база" }]}
      />
      <PageBody>
        <BaseOverview departments={departments} />
      </PageBody>
    </>
  );
}
