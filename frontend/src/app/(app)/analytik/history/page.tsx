import Link from "next/link";
import { FlaskConical, Gauge, Scale } from "lucide-react";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { requireAnalytik, one, type SearchParams } from "@/components/analytik/guard";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { HistoryView } from "@/components/analytik/DataMap";
import type { QueryLogRow } from "@/lib/analytik";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("История аналитики · OneBase") };
}

/** «Аналитик → История». */
export default async function Page({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const me = await requireAnalytik();
  const t = await getT();
  const sp = await searchParams;
  const all = one(sp, "all") === "1";
  const manager = !!me?.permissions.includes("analytik.manage");
  const [queries, chats] = await Promise.all([
    apiGet<QueryLogRow[]>(`/api/analytik/queries?take=100${all && manager ? "&all=true" : ""}`, "/analytik/history"),
    apiGet<{ id: string; title: string; lastMessageAt: string }[]>("/api/analytik/conversations", "/analytik/history"),
  ]);
  return (
    <>
      <PageHeader title={t("История")} subtitle={t("Ваши исследования и журнал запросов к базе: откуда взята каждая цифра")} breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Аналитик"), href: "/analytik" }, { label: t("История") }]} />
      <PageBody>
        <nav className="mb-4 grid gap-2 sm:grid-cols-3">
          {[
            { href: "/analytik/investigations", icon: FlaskConical, title: "Исследования", text: "План, шаги, гипотезы и проверка выводов сложных вопросов" },
            { href: "/analytik/decisions", icon: Scale, title: "Решения", text: "Что решили и что из этого вышло — проверка в срок" },
            ...(manager ? [{ href: "/analytik/quality", icon: Gauge, title: "Качество аналитика", text: "Решённые вопросы, ошибки, слабые места, набор сложных вопросов" }] : []),
          ].map((x) => (
            <Link key={x.href} href={x.href} className="flex items-start gap-3 rounded-2xl border border-line bg-surface p-3.5 hover:border-accent">
              <x.icon className="mt-0.5 size-5 shrink-0 text-accent-strong" />
              <span className="min-w-0">
                <span className="block text-sm font-semibold text-ink">{t(x.title)}</span>
                <span className="mt-0.5 block text-xs text-ink-3">{t(x.text)}</span>
              </span>
            </Link>
          ))}
        </nav>
        <HistoryView queries={queries} chats={chats} manager={manager} all={all && manager} />
      </PageBody>
    </>
  );
}
