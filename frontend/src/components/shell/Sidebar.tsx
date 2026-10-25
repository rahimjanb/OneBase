"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  ArrowRight,
  Building2,
  CalendarDays,
  ChartColumn,
  Database,
  Folder,
  LayoutGrid,
  LifeBuoy,
  ListChecks,
  Send,
  Settings2,
  Sparkles,
  type LucideIcon,
} from "lucide-react";
import { useT } from "@/i18n/client";
import { canOpen } from "@/lib/navigation";

/**
 * also — разделы, которые открываются из этого пункта и подсвечивают его (продажи — из «Отделов»).
 * soon — раздел ещё не сделан: пункт виден, но не кликается.
 * short — короткая подпись для нижней панели на телефоне.
 * badge — счётчик или пометка справа («6», «128», «скоро», «new»); badgeTone «new» — акцентная.
 */
export type NavItem = { href: string; label: string; icon: LucideIcon; also?: string[]; soon?: boolean; short?: string; badge?: string; badgeTone?: "default" | "new" };

export type NavGroup = { title: string; items: NavItem[] };

/** Группы меню (ТЗ dashboard): рабочее пространство, интеллект, коммуникации. Счётчики появятся вместе с данными разделов — выдуманных нет. */
export const navGroups: NavGroup[] = [
  {
    title: "Рабочее пространство",
    items: [
      { href: "/", label: "Обзор", icon: LayoutGrid },
      { href: "/departments", label: "Отделы", icon: Building2, also: ["/sales"] },
      { href: "/tasks", label: "Задачи", icon: ListChecks },
      { href: "/reports", label: "Отчёты", icon: ChartColumn, soon: true, badge: "скоро" },
      { href: "/base", label: "Файлы", icon: Folder, also: ["/files"] },
    ],
  },
  {
    title: "Интеллект",
    items: [
      { href: "/consultant", label: "AI-консультант", icon: Sparkles, also: ["/ai"], short: "AI", badge: "new", badgeTone: "new" },
      { href: "/data-manager", label: "AI Data Manager", icon: Database, short: "Данные" },
    ],
  },
  {
    title: "Коммуникации",
    items: [
      { href: "/campaigns", label: "Рассылки", icon: Send },
      // Календаря в OneBase пока нет: пункт виден по макету, но не кликается (без ссылки на несуществующий раздел).
      { href: "/calendar", label: "Календарь", icon: CalendarDays, soon: true },
    ],
  },
];

export const workspace: NavItem[] = navGroups.flatMap((g) => g.items);

export const settings: NavItem = { href: "/settings", label: "Настройки", icon: Settings2 };

/** Пункты меню, доступные пользователю (права — из /api/auth/me; без прав раздел скрыт, как и «Настройки»). */
export const visibleWorkspace = (permissions: readonly string[] | null) =>
  permissions === null ? workspace : workspace.filter((item) => canOpen(item.href, permissions));

export function isActive(pathname: string, item: NavItem) {
  const under = (href: string) => pathname === href || pathname.startsWith(`${href}/`);
  return item.href === "/" ? pathname === "/" : under(item.href) || (item.also ?? []).some(under);
}

/** Логотип «1Base»: квадрат с градиентом бренда и белой «1», рядом «Base». В сайдбаре и в шапке на телефоне. */
export function Logo() {
  const t = useT();
  return (
    <Link href="/" aria-label={t("OneBase — на главную")} className="flex items-center gap-2.5 rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent">
      <span className="grid size-[30px] place-items-center rounded-lg bg-gradient-to-br from-brand-from to-brand-to text-[17px] font-extrabold leading-none text-white">1</span>
      <span className="text-[21px] font-bold tracking-[-0.5px] text-sidebar-ink">Base</span>
    </Link>
  );
}

const itemBase =
  "flex h-[38px] items-center gap-3 rounded-[9px] px-2.5 text-[13.5px] transition-colors duration-150 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent";

function Badge({ text, tone }: { text: string; tone?: "default" | "new" }) {
  const t = useT();
  return (
    <span className={`ml-auto rounded-md px-[7px] py-0.5 text-[10.5px] font-semibold leading-4 ${tone === "new" ? "bg-accent text-white" : "bg-sidebar-hover text-sidebar-text"}`}>
      {t(text)}
    </span>
  );
}

function NavLink({ item, active }: { item: NavItem; active: boolean }) {
  const t = useT();
  const Icon = item.icon;
  if (item.soon) {
    return (
      <span aria-disabled="true" title={t("Раздел в разработке")} className={`${itemBase} cursor-default font-medium text-sidebar-muted`}>
        <Icon className="size-[17px] shrink-0" strokeWidth={1.75} />
        {t(item.label)}
        {item.badge && <Badge text={item.badge} />}
      </span>
    );
  }
  return (
    <Link
      href={item.href}
      aria-current={active ? "page" : undefined}
      className={`${itemBase} ${active ? "bg-sidebar-active font-semibold text-sidebar-ink ring-1 ring-inset ring-accent/25" : "font-medium text-sidebar-text hover:bg-sidebar-hover hover:text-sidebar-ink"}`}
    >
      <Icon className={`size-[17px] shrink-0 ${active ? "text-accent-strong" : "text-sidebar-text"}`} strokeWidth={1.75} />
      {t(item.label)}
      {item.badge && <Badge text={item.badge} tone={item.badgeTone} />}
    </Link>
  );
}

/**
 * Боковая навигация на больших экранах (248px, ТЗ dashboard): логотип, группы разделов, промо AI-отчёта, внизу — настройки и помощь. На телефоне вместо неё — шапка (Topbar) и нижняя панель (MobileNav).
 * canOpenSettings — есть права раздела «Настройки»; обычным сотрудникам пункт не показывается.
 * permissions — права пользователя: пункты, к которым нет доступа, скрыты (роль «Админ 2» видит только «Файлы» и «AI Data Manager»).
 */
export function Sidebar({ canOpenSettings, permissions }: { canOpenSettings: boolean; permissions: readonly string[] | null }) {
  const pathname = usePathname();
  const t = useT();
  const allowed = (item: NavItem) => permissions === null || canOpen(item.href, permissions);

  return (
    <aside className="hidden h-full w-[248px] shrink-0 flex-col gap-7 overflow-y-auto border-r border-line bg-sidebar/80 px-4 py-6 lg:flex">
      <div className="px-2">
        <Logo />
      </div>

      <div className="flex flex-col gap-6">
        {navGroups.map((group) => {
          const items = group.items.filter(allowed);
          if (items.length === 0) return null;
          return (
            <nav key={group.title} aria-label={t(group.title)}>
              <div className="px-2.5 pb-2 text-[10.5px] font-semibold uppercase tracking-[0.8px] text-sidebar-muted">{t(group.title)}</div>
              <div className="space-y-0.5">
                {items.map((item) => (
                  <NavLink key={item.href} item={item} active={isActive(pathname, item)} />
                ))}
              </div>
            </nav>
          );
        })}
      </div>

      <div className="mt-auto flex flex-col gap-5">
        {/* Промо еженедельного AI-отчёта: ведёт в обзор находок консультанта. */}
        <Link
          href="/ai"
          className="flex flex-col gap-2.5 rounded-xl border border-accent/20 bg-gradient-to-b from-ai-promo/40 to-transparent p-4 transition-colors duration-150 hover:border-accent/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
        >
          <Sparkles className="size-[18px] text-accent-strong" strokeWidth={1.75} />
          <span className="text-[13px] font-semibold leading-snug text-sidebar-ink">{t("Еженедельный AI-отчёт готов")}</span>
          <span className="text-xs leading-[1.45] text-sidebar-text">{t("3 риска и 5 рекомендаций по отделам за неделю.")}</span>
          <span className="inline-flex items-center gap-1 text-[12.5px] font-semibold text-accent-strong">
            {t("Открыть отчёт")} <ArrowRight className="size-3.5" />
          </span>
        </Link>

        <nav className="space-y-0.5" aria-label={t("Настройки")}>
          {canOpenSettings && <NavLink item={settings} active={isActive(pathname, settings)} />}
          <a href="#" className={`${itemBase} h-9 whitespace-nowrap font-medium text-sidebar-text hover:bg-sidebar-hover hover:text-sidebar-ink`}>
            <LifeBuoy className="size-[17px] shrink-0 text-sidebar-text" strokeWidth={1.75} />
            {t("Помощь и поддержка")}
          </a>
        </nav>
      </div>
    </aside>
  );
}
