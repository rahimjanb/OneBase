import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { DepartmentIcon } from "@/components/departments";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { statusView, type DepartmentIntegrations } from "@/lib/integrations";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "Интеграции · Настройки" };

export default async function IntegrationsPage() {
  const departments = await apiGet<DepartmentIntegrations[]>("/api/integrations", "/settings/integrations");

  return (
    <>
      <PageHeader
        title="Интеграции"
        subtitle="Выберите отдел, чтобы настроить его подключения к внешним системам"
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Настройки", href: "/settings" }, { label: "Интеграции" }]}
      />
      <PageBody>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {departments.map((d) => (
            <Link key={d.code} href={`/settings/integrations/${d.code}`} className="group block">
              <div className="flex h-full flex-col rounded-xl border border-line bg-surface p-5 transition-colors group-hover:border-accent/40">
                <div className="flex items-center gap-3">
                  <DepartmentIcon code={d.code} />
                  <span className="flex-1 font-semibold text-ink">{d.name}</span>
                  <ArrowRight className="size-4 text-ink-3 transition-transform group-hover:translate-x-0.5 group-hover:text-accent-strong" />
                </div>
                <div className="mt-4 border-t border-line pt-3">
                  {d.integrations.length === 0 ? (
                    <span className="text-sm text-ink-3">Интеграций пока нет</span>
                  ) : (
                    <ul className="space-y-1.5">
                      {d.integrations.map((i) => (
                        <li key={i.code} className="flex items-center justify-between gap-3 text-sm">
                          <span className="text-ink">{i.name}</span>
                          <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${statusView[i.status].className}`}>{statusView[i.status].label}</span>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>
              </div>
            </Link>
          ))}
        </div>
      </PageBody>
    </>
  );
}
