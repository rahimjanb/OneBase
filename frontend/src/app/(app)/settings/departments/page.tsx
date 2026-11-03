import Link from "next/link";
import { redirect } from "next/navigation";
import { ArrowRight, Users } from "lucide-react";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { departmentAbout, departmentIcons, groupSum, type DepartmentsView, type SupplySettings } from "@/lib/departments";
import { apiGet, apiTry } from "@/lib/server-api";
import { has, type Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Отделы · Настройки · OneBase") };
}

/**
 * «Настройки → Отделы»: шесть отделов компании — руководители и что настраивается в каждом. Открывают администратор пользователей
 * (руководители) и тот, кто настраивает снабжение (маршрут заявок); остальным — нет доступа.
 */
export default async function DepartmentsSettingsPage() {
  const path = "/settings/departments";
  const me = await apiGet<Me>("/api/auth/me", path);
  if (!has(me, "users.manage") && !has(me, "procurement.settings")) redirect("/no-access?from=%2Fsettings%2Fdepartments");
  const [view, supply] = await Promise.all([apiGet<DepartmentsView>("/api/settings/departments", path), apiTry<SupplySettings>("/api/procurement/settings")]);
  const t = await getT();
  const route = (s: SupplySettings) =>
    t("Заявки: руководитель отдела → {finance} → {director} (больше {limit} сум) → снабжение", {
      finance: s.financeApproverName ?? t("финансист не выбран"),
      director: s.directorApproverName ?? t("директор не выбран"),
      limit: groupSum(s.directorLimit),
    });
  return (
    <>
      <PageHeader
        title={t("Отделы")}
        subtitle={t("Руководители и настройки каждого отдела")}
        back="/settings"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Настройки"), href: "/settings" }, { label: t("Отделы") }]}
      />
      <PageBody>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {view.departments.map((d) => {
            const Icon = departmentIcons[d.code] ?? Users;
            return (
              <Link
                key={d.code}
                href={`/settings/departments/${d.code}`}
                className="group flex h-full flex-col gap-3 rounded-xl border border-line bg-surface p-5 transition-colors hover:border-accent/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
              >
                <span className="flex items-center gap-3">
                  <span className="grid size-10 shrink-0 place-items-center rounded-full bg-accent-soft text-accent-strong">
                    <Icon className="size-[18px]" aria-hidden />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block font-semibold text-ink">{d.name}</span>
                    <span className="block text-xs text-ink-3">{t("Пользователей: {n}", { n: d.users })}</span>
                  </span>
                  <ArrowRight className="size-4 shrink-0 text-ink-3 group-hover:text-accent-strong" aria-hidden />
                </span>
                <span className="text-sm text-ink-2">
                  <span className="text-ink-3">{t("Руководитель:")} </span>
                  {d.heads.length > 0 ? d.heads.map((h) => h.name).join(", ") : <span className="text-warn">{t("не назначен")}</span>}
                </span>
                <span className="text-sm text-ink-3">{d.code === "supply" && supply ? route(supply) : t(departmentAbout[d.code] ?? "Руководители отдела.")}</span>
              </Link>
            );
          })}
        </div>
      </PageBody>
    </>
  );
}
