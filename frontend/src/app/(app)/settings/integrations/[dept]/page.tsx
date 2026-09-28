import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowRight, Plug } from "lucide-react";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { statusView, type DepartmentIntegrations } from "@/lib/integrations";
import { dateTime } from "@/lib/sales/format";
import { apiGetOrNull } from "@/lib/server-api";

export default async function DepartmentIntegrationsPage({ params }: { params: Promise<{ dept: string }> }) {
  const { dept } = await params;
  const path = `/settings/integrations/${dept}`;
  const department = await apiGetOrNull<DepartmentIntegrations>(`/api/integrations/departments/${dept}`, path);
  if (!department) notFound();

  return (
    <>
      <PageHeader
        title={`Интеграции: ${department.name}`}
        subtitle="Внешние системы, из которых отдел получает данные"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: "Настройки", href: "/settings" },
          { label: "Интеграции", href: "/settings/integrations" },
          { label: department.name },
        ]}
      />
      <PageBody>
        {department.integrations.length === 0 ? (
          <div className="rounded-xl border border-dashed border-line bg-surface/60 px-6 py-12 text-center">
            <Plug className="mx-auto size-6 text-ink-3" />
            <p className="mt-3 text-sm text-ink-2">Для отдела «{department.name}» интеграций пока нет.</p>
            <p className="mt-1 text-xs text-ink-3">Сейчас доступна интеграция с Linko для отдела «Продажи».</p>
          </div>
        ) : (
          <div className="grid gap-4 md:grid-cols-2">
            {department.integrations.map((i) => (
              <Link key={i.code} href={`${path}/${i.code}`} className="group block">
                <div className="flex h-full flex-col rounded-xl border border-line bg-surface p-5 transition-colors group-hover:border-accent/40">
                  <div className="flex items-start gap-3">
                    <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-accent-soft text-sm font-bold text-accent-strong">
                      {i.name.slice(0, 2).toUpperCase()}
                    </span>
                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="font-semibold text-ink">{i.name}</span>
                        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${statusView[i.status].className}`}>{statusView[i.status].label}</span>
                      </div>
                      <p className="mt-1.5 text-sm text-ink-2">{i.description}</p>
                    </div>
                    <ArrowRight className="size-4 shrink-0 text-ink-3 group-hover:text-accent-strong" />
                  </div>
                  <div className="mt-4 border-t border-line pt-3 text-xs text-ink-3">
                    {i.dataAsOf ? `Данные по ${dateTime(i.dataAsOf)}` : "Данные ещё не загружались"}
                  </div>
                </div>
              </Link>
            ))}
          </div>
        )}
      </PageBody>
    </>
  );
}
