import Link from "next/link";
import { ArrowRight, Bot, Building2, DatabaseZap, FileSpreadsheet, Plug, ScrollText, Send, Sparkle, TrendingUp, Users } from "lucide-react";
import { redirect } from "next/navigation";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { getT } from "@/i18n/server";
import { apiGet } from "@/lib/server-api";
import { has, type Me } from "@/lib/users";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Настройки · OneBase") };
}

/** Карточка видна только с правом permission (или с любым из списка): директору — лишь его разделы, сотрудникам раздел закрыт. */
const sections: { href: string | null; icon: typeof Users; title: string; text: string; permission: string | string[]; agents?: boolean }[] = [
  {
    href: "/settings/users",
    icon: Users,
    title: "Пользователи и роли",
    text: "Сотрудники: логин, имя, должность, контакты и роль. Роли отделов, директора и администратора.",
    permission: "users.manage",
  },
  {
    href: "/settings/integrations",
    icon: Plug,
    title: "Интеграции",
    text: "Подключения отделов к внешним системам: Linko для продаж и другие.",
    permission: "integrations.manage",
  },
  {
    href: "/settings/data-sources",
    icon: FileSpreadsheet,
    title: "Источники данных",
    text: "Linko и Google Sheets: какие поля разделов продаж (планы, дилеры, P&L) брать из таблиц и как часто их обновлять.",
    permission: "integrations.manage",
  },
  {
    href: "/settings/ai",
    icon: Sparkle,
    title: "AI",
    text: "Провайдеры OpenAI и Anthropic, ключи API, модели и параметры консультанта.",
    permission: "ai.settings.manage",
  },
  {
    href: "/settings/agents",
    icon: Bot,
    title: "ИИ-агенты",
    text: "Агенты отделов ведут переписку в Telegram как живые сотрудники: рекрутер HR, помощники отделов, агенты продаж. Как пишут, ограничения, готовые ответы, проверка черновиков.",
    permission: [],
    agents: true,
  },
  {
    href: "/settings/telegram",
    icon: Send,
    title: "Telegram",
    text: "Аккаунты Telegram, AI-агент для ответов в чатах: кому он отвечает, с какими правами, журнал ответов.",
    permission: "telegram.manage",
  },
  {
    href: "/data-manager",
    icon: DatabaseZap,
    title: "AI Data Manager",
    text: "Чат с Claude: загрузите отчёт Excel или CSV — он сверит его с данными OneBase и подготовит изменения планов и целей. Вы подтверждаете.",
    permission: "ai.data.manage",
  },
  {
    href: "/sales/setup",
    icon: TrendingUp,
    title: "Продажи: оргструктура и цели",
    text: "Синхронизация с Linko, направления (РМ, каналы), СВР и дилеры регионов, справочник ТП и вакансий из Linko, цели.",
    permission: "sales.manage",
  },
  {
    href: "/settings/logs",
    icon: ScrollText,
    title: "Журнал ошибок",
    text: "Ошибки интеграции Linko, синхронизации и сервера. Отмена идущей синхронизации.",
    permission: "system.logs.read",
  },
  {
    href: "/settings/departments",
    icon: Building2,
    title: "Отделы",
    text: "Шесть отделов компании: руководители и настройки каждого отдела — у снабжения маршрут согласования заявок и лимит без директора.",
    permission: ["users.manage", "procurement.settings"],
  },
  { href: null, icon: Bot, title: "Доступ AI-агентов", text: "Какие инструменты разрешены каждому AI-сотруднику.", permission: "ai.settings.manage" },
];

export default async function SettingsPage() {
  const me = await apiGet<Me>("/api/auth/me", "/settings");
  if (!me.canOpenSettings) redirect("/no-access?from=%2Fsettings");
  const visible = sections.filter(
    (s) => (s.agents && (me.agentDepartments?.length ?? 0) > 0) || (Array.isArray(s.permission) ? s.permission : [s.permission]).some((p) => has(me, p)),
  );
  const t = await getT();
  return (
    <>
      <PageHeader
        title={t("Настройки")}
        subtitle={t("Компания, пользователи, доступы и интеграции")}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: t("Настройки") }]}
      />
      <PageBody>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {visible.map((s) => {
            const body = (
              <div className={`flex h-full flex-col rounded-xl border border-line bg-surface p-5 ${s.href ? "transition-colors group-hover:border-accent/40" : "opacity-70"}`}>
                <div className="flex items-center gap-3">
                  <span className="grid size-10 place-items-center rounded-full bg-accent-soft text-accent-strong">
                    <s.icon className="size-[18px]" />
                  </span>
                  <span className="flex-1 font-semibold text-ink">{t(s.title)}</span>
                  {s.href ? (
                    <ArrowRight className="size-4 text-ink-3 group-hover:text-accent-strong" />
                  ) : (
                    <span className="rounded-full bg-muted px-2 py-0.5 text-xs text-ink-3">{t("скоро")}</span>
                  )}
                </div>
                <p className="mt-3 text-sm text-ink-2">{t(s.text)}</p>
              </div>
            );
            return s.href ? (
              <Link key={s.title} href={s.href} className="group block">
                {body}
              </Link>
            ) : (
              <div key={s.title}>{body}</div>
            );
          })}
        </div>
      </PageBody>
    </>
  );
}
