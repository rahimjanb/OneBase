import { Sparkle } from "lucide-react";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { Card, SectionTitle } from "@/components/ui";
import { departmentAgents, director } from "@/lib/agents";
import { directorInsight } from "@/lib/demo-data";

export default function AiPage() {
  return (
    <>
      <PageHeader title="AI Consultants" subtitle="AI-сотрудники отделов под управлением AI Director" />
      <PageBody>
        <div className="rounded-xl border border-accent/25 bg-accent-soft p-6">
          <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-accent-strong">
            <Sparkle className="size-3.5" />
            {director.name}
          </div>
          <p className="mt-2 text-sm text-ink-2">{director.scope}</p>
          <p className="mt-4 text-sm font-medium text-ink">{directorInsight}</p>
        </div>

        <div className="mt-8">
          <SectionTitle
            title="AI-сотрудники отделов"
            subtitle="Работают только через разрешённые инструменты; критические действия подтверждает человек"
          />
        </div>
        <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {departmentAgents.map((agent) => (
            <Card key={agent.code} className="p-5">
              <div className="flex items-center justify-between">
                <span className="font-semibold">{agent.name}</span>
                <span className="rounded-full bg-muted px-2 py-0.5 text-xs text-ink-2">{agent.code}</span>
              </div>
              <p className="mt-2 text-sm text-ink-2">{agent.scope}</p>
            </Card>
          ))}
        </div>
      </PageBody>
    </>
  );
}
