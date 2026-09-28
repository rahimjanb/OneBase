import { DepartmentStatusCard } from "@/components/departments";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { departments } from "@/lib/demo-data";

export default function DepartmentsPage() {
  return (
    <>
      <PageHeader title="Отделы" subtitle="Показатели, планы и состояние подразделений" />
      <PageBody>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {departments.map((d) => (
            <DepartmentStatusCard key={d.code} department={d} />
          ))}
        </div>
      </PageBody>
    </>
  );
}
