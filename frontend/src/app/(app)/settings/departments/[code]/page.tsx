import { notFound, redirect } from "next/navigation";
import { DepartmentSettings } from "@/components/settings/DepartmentSettings";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { departmentAbout, type DepartmentPerson, type DepartmentsView, type SupplySettings } from "@/lib/departments";
import type { WarehousesView } from "@/lib/procurement";
import type { QualitySettings } from "@/lib/quality";
import { apiGet, apiTry } from "@/lib/server-api";
import { has, type Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Отделы · Настройки · OneBase") };
}

/** Отдел в «Настройки → Отделы»: руководители; у снабжения — маршрут согласования заявок, лимит без директора, правила, склады и кто решает по качеству. */
export default async function DepartmentSettingsPage({ params }: { params: Promise<{ code: string }> }) {
  const { code } = await params;
  const path = `/settings/departments/${code}`;
  const me = await apiGet<Me>("/api/auth/me", path);
  if (!has(me, "users.manage") && !has(me, "procurement.settings")) redirect("/no-access?from=%2Fsettings%2Fdepartments");
  const [view, users] = await Promise.all([apiGet<DepartmentsView>("/api/settings/departments", path), apiGet<DepartmentPerson[]>("/api/settings/departments/users", path)]);
  const department = view.departments.find((d) => d.code === code);
  if (!department) notFound();
  const supply = code === "supply" ? await apiGet<SupplySettings>("/api/procurement/settings", path) : null;
  // Склады видит тот, кто видит склад или настраивает снабжение; с одним правом «Пользователи» блока нет.
  const warehouses = code === "supply" ? await apiTry<WarehousesView>("/api/procurement/warehouses") : null;
  // Кто решает по качеству — видят те, кто настраивает снабжение, и те, кто видит контроль качества.
  const quality = code === "supply" ? await apiTry<QualitySettings>("/api/quality/settings") : null;
  const t = await getT();
  return (
    <>
      <PageHeader
        title={department.name}
        subtitle={t(departmentAbout[code] ?? "Руководители отдела.")}
        back="/settings/departments"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Настройки"), href: "/settings" },
          { label: t("Отделы"), href: "/settings/departments" },
          { label: department.name },
        ]}
      />
      <PageBody>
        <DepartmentSettings
          department={department}
          users={users}
          canEditHeads={view.canEditHeads}
          canEditSupply={view.canEditSupply}
          supply={supply}
          warehouses={warehouses}
          quality={quality}
        />

      </PageBody>
    </>
  );
}
