import Link from "next/link";
import { ArrowRight, Sparkle } from "lucide-react";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { Card, SectionTitle } from "@/components/ui";
import { AlertsDashboard } from "@/components/ai/AlertsDashboard";
import { getT } from "@/i18n/server";
import type { AiAgentPublic, AiAlertsView } from "@/lib/ai";
import { apiGet } from "@/lib/server-api";

export const metadata = { title: "AI Dashboard · OneBase" };

type Me = { permissions: string[] };

export default async function AiPage() {
  const t = await getT();
  const [agents, alerts, me] = await Promise.all([
    apiGet<AiAgentPublic[]>("/api/ai/agents", "/ai"),
    apiGet<AiAlertsView>("/api/ai/alerts", "/ai"),
    apiGet<Me>("/api/auth/me", "/ai"),
  ]);
  const consultant = agents.find((a) => a.isConsultant);
  const departments = agents.filter((a) => !a.isConsultant);
  return (
    <>
      <PageHeader title="AI Dashboard" subtitle={t("Что AI нашёл в данных компании и кто из AI-сотрудников работает")} back="/consultant" />
      <PageBody>
        <AlertsDashboard initial={alerts} canRun={me.permissions.includes("ai.settings.manage")} />

        <div className="mt-10" />
        {consultant && (
          <div className="rounded-xl border border-accent/25 bg-accent-soft p-6">
            <div className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wide text-accent-strong">
              <Sparkle className="size-3.5" />
              {t(consultant.name)}
            </div>
            {consultant.description && <p className="mt-2 text-sm text-ink-2">{t(consultant.description)}</p>}
            <Link href="/consultant" className="mt-4 inline-flex items-center gap-1.5 text-sm font-semibold text-accent-strong hover:underline">
              {t("Задать вопрос")} <ArrowRight className="size-3.5" />
            </Link>
          </div>
        )}

        <div className="mt-8">
          <SectionTitle
            title={t("AI-сотрудники отделов")}
            subtitle={t("Получают данные OneBase только через разрешённые инструменты и только в пределах ваших прав")}
          />
        </div>
        <div className="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {departments.map((agent) => (
            <Card key={agent.code} className="p-5">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <div className="font-semibold">{t(agent.name)}</div>
                  {agent.role && <div className="text-sm text-ink-3">{t(agent.role)}</div>}
                </div>
                <span className={`rounded-full px-2 py-0.5 text-xs ${agent.available ? "bg-ok-soft text-ok" : "bg-muted text-ink-2"}`}>
                  {!agent.enabled ? t("выключен") : agent.available ? t("доступен") : t("нет доступа")}
                </span>
              </div>
              {agent.description && <p className="mt-2 text-sm text-ink-2">{t(agent.description)}</p>}
            </Card>
          ))}
        </div>
      </PageBody>
    </>
  );
}
