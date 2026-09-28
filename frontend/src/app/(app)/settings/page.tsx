import Link from "next/link";
import { ArrowRight, Bot, Building2, Plug, Users } from "lucide-react";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";

export const metadata = { title: "Настройки · OneBase" };

const sections = [
  {
    href: "/settings/integrations",
    icon: Plug,
    title: "Интеграции",
    text: "Подключения отделов к внешним системам: Linko для продаж и другие.",
  },
  { href: null, icon: Users, title: "Пользователи и роли", text: "Доступы сотрудников и права ролей." },
  { href: null, icon: Building2, title: "Отделы", text: "Структура компании и руководители отделов." },
  { href: null, icon: Bot, title: "Доступ AI-агентов", text: "Какие инструменты разрешены каждому AI-сотруднику." },
];

export default function SettingsPage() {
  return (
    <>
      <PageHeader title="Настройки" subtitle="Компания, пользователи, доступы и интеграции" breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Настройки" }]} />
      <PageBody>
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {sections.map((s) => {
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
