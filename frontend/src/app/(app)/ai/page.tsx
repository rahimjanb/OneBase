import Link from "next/link";
import { ArrowRight, Sparkle } from "lucide-react";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { Card, SectionTitle } from "@/components/ui";
import type { AiAgentPublic } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "AI-сотрудники · OneBase" };

export default async function AiPage() {
  const agents = await apiGet<AiAgentPublic[]>("/api/ai/agents", "/ai");
  const consultant = agents.find((a) => a.isConsultant);
  const departments = agents.filter((a) => !a.isConsultant);
  return (
    <>
      <PageHeader title="AI-сотрудники" subtitle="Главный консультант и AI-сотрудники отделов" back="/consultant" />
      <PageBody>
        {consultant && (
          <div className="rounded-xl border border-accent/25 bg-accent-soft p-6">
            <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-accent-strong">
              <Sparkle className="size-3.5" />
              {consultant.name}
            </div>
            <p className="mt-2 text-sm text-ink-2">{consultant.description}</p>
            <Link href="/consultant" className="mt-4 inline-flex items-center gap-1.5 text-sm font-semibold text-accent-strong hover:underline">
              Задать вопрос <ArrowRight className="size-3.5" />
            </Link>
          </div>
        )}

        <div className="mt-8">
          <SectionTitle
            title="AI-сотрудники отделов"
            subtitle="Получают данные OneBase только через разрешённые инструменты и только в пределах ваших прав"
          />
        </div>
        <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {departments.map((agent) => (
            <Card key={agent.code} className="p-5">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="font-semibold">{agent.name}</div>
                  {agent.role && <div className="text-sm text-ink-3">{agent.role}</div>}
                </div>
                <span className={`rounded-full px-2 py-0.5 text-xs ${agent.available ? "bg-ok-soft text-ok" : "bg-muted text-ink-2"}`}>
                  {!agent.enabled ? "выключен" : agent.available ? "доступен" : "нет доступа"}
                </span>
              </div>
              <p className="mt-2 text-sm text-ink-2">{agent.description}</p>
            </Card>
          ))}
        </div>
      </PageBody>
    </>
  );
}
