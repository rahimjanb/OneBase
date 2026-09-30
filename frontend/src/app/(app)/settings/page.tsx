import Link from "next/link";
import { ArrowRight, Bot, Building2, Plug, Sparkle, TrendingUp, Users } from "lucide-react";
import { redirect } from "next/navigation";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { apiGet } from "@/lib/server-api";
import { has, type Me } from "@/lib/users";

export const metadata = { title: "Настройки · OneBase" };

/** Карточка видна только с правом permission: директору — лишь «Пользователи и роли», сотрудникам раздел закрыт. */
const sections = [
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
    href: "/settings/ai",
    icon: Sparkle,
    title: "AI",
    text: "Провайдеры OpenAI и Anthropic, ключи API, модели и параметры консультанта.",
    permission: "ai.settings.manage",
  },
  {
    href: "/sales/setup",
    icon: TrendingUp,
    title: "Продажи: справочник и цели",
    text: "Синхронизация с Linko, справочник ТП и вакансий из Linko, цели. Планы и оргструктура — только в Linko.",
    permission: "sales.manage",
  },
  { href: null, icon: Building2, title: "Отделы", text: "Структура компании и руководители отделов.", permission: "ai.settings.manage" },
  { href: null, icon: Bot, title: "Доступ AI-агентов", text: "Какие инструменты разрешены каждому AI-сотруднику.", permission: "ai.settings.manage" },
];

export default async function SettingsPage() {
  const me = await apiGet<Me>("/api/auth/me", "/settings");
  if (!me.canOpenSettings) redirect("/no-access?from=%2Fsettings");
  const visible = sections.filter((s) => has(me, s.permission));
  return (
    <>
      <PageHeader title="Настройки" subtitle="Компания, пользователи, доступы и интеграции" breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Настройки" }]} />
      <PageBody>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {visible.map((s) => {
            const body = (
              <div className={`flex h-full flex-col rounded-xl border border-line bg-surface p-5 ${s.href ? "transition-colors group-hover:border-accent/40" : "opacity-70"}`}>
                <div className="flex items-center gap-3">
                  <span className="grid size-10 place-items-center rounded-full bg-accent-soft text-accent-strong">
                    <s.icon className="size-[18px]" />
                  </span>
                  <span className="flex-1 font-semibold text-ink">{s.title}</span>
                  {s.href ? (
                    <ArrowRight className="size-4 text-ink-3 group-hover:text-accent-strong" />
                  ) : (
                    <span className="rounded-full bg-muted px-2 py-0.5 text-xs text-ink-3">скоро</span>
                  )}
                </div>
                <p className="mt-3 text-sm text-ink-2">{s.text}</p>
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
