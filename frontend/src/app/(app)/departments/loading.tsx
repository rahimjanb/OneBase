import { DepartmentsSkeleton } from "@/components/department-board";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";

/** «Отделы» грузятся — шапка и скелетоны шести карточек (ТЗ: состояние загрузки). */
export default function Loading() {
  return (
    <>
      <PageHeader title="Отделы" subtitle="Показатели, планы и состояние подразделений" />
      <PageBody>
        <DepartmentsSkeleton />
      </PageBody>
    </>
  );
}
