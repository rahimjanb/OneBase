import { redirect } from "next/navigation";
import { DepartmentStatusCard } from "@/components/departments";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { departments } from "@/lib/demo-data";
import { canOpen, homeFor } from "@/lib/navigation";
import { apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";

export default async function DepartmentsPage() {
  const me = await apiSession<Me>("/api/auth/me");
  if (me && !canOpen("/departments", me.permissions)) redirect(homeFor(me.permissions));
  const t = await getT();
  return (
    <>
      <PageHeader title={t("Отделы")} subtitle={t("Показатели, планы и состояние подразделений")} />
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
