"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  ChartNoAxesColumn,
  Check,
  CircleQuestionMark,
  Database,
  Grid3x3,
  LayoutGrid,
  SlidersHorizontal,
  Sparkle,
  type LucideIcon,
} from "lucide-react";

/**
 * also — разделы, которые открываются из этого пункта и подсвечивают его (продажи — из «Отделов»).
 * soon — раздел ещё не сделан: пункт виден, но не кликается.
 * short — короткая подпись для нижней панели на телефоне.
 */
export type NavItem = { href: string; label: string; icon: LucideIcon; also?: string[]; soon?: boolean; short?: string };

export const workspace: NavItem[] = [
  { href: "/", label: "Обзор", icon: LayoutGrid },
  { href: "/departments", label: "Отделы", icon: Grid3x3, also: ["/sales"] },
  { href: "/tasks", label: "Задачи", icon: Check, soon: true },
  { href: "/reports", label: "Отчёты", icon: ChartNoAxesColumn, soon: true },
  { href: "/base", label: "Общая база", icon: Database, short: "База" },
  { href: "/consultant", label: "AI-консультант", icon: Sparkle, also: ["/ai"], short: "AI" },
];

export const settings: NavItem = { href: "/settings", label: "Настройки", icon: SlidersHorizontal };

export function isActive(pathname: string, item: NavItem) {
  const under = (href: string) => pathname === href || pathname.startsWith(`${href}/`);
  return item.href === "/" ? pathname === "/" : under(item.href) || (item.also ?? []).some(under);
}

/** Логотип «1Base» — белая надпись с синей единицей, для тёмного сайдбара и шапки на телефоне. Файл — из public/brand (4× для чётких экранов). */
export function Logo() {
  return (
    <Link href="/" aria-label="OneBase — на главную" className="flex items-center">
      <img src="/brand/logo-light.png" alt="OneBase" className="h-7 w-auto" />
    </Link>
  );
}

const linkClass = "flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm text-sidebar-text transition-colors hover:bg-white/5 hover:text-white";

function NavLink({ item, active }: { item: NavItem; active: boolean }) {
  const Icon = item.icon;
  if (item.soon) {
    return (
      <span aria-disabled="true" title="Раздел в разработке" className="flex cursor-default items-center gap-3 rounded-lg px-3 py-2.5 text-sm text-sidebar-muted">
        <Icon className="size-4 shrink-0" strokeWidth={1.75} />
        {item.label}
        <span className="ml-auto rounded-full bg-white/5 px-1.5 py-0.5 text-[10px] text-sidebar-muted">скоро</span>
      </span>
    );
  }
  return (
    <Link
      href={item.href}
      aria-current={active ? "page" : undefined}
      className={active ? "flex items-center gap-3 rounded-lg bg-sidebar-active px-3 py-2.5 text-sm font-semibold text-white" : linkClass}
    >
      <Icon className="size-4 shrink-0" strokeWidth={1.75} />
      {item.label}
    </Link>
  );
}

/**
 * Боковая навигация на больших экранах. На телефоне вместо неё — шапка (Topbar) и нижняя панель (MobileNav).
 * canOpenSettings — есть права раздела «Настройки»; обычным сотрудникам пункт не показывается.
 */
export function Sidebar({ canOpenSettings }: { canOpenSettings: boolean }) {
  const pathname = usePathname();

  return (
    <aside className="sticky top-0 hidden h-screen w-[232px] shrink-0 flex-col bg-sidebar px-3 py-6 lg:flex">
      <div className="px-3">
        <Logo />
      </div>

      <div className="mt-9 px-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-sidebar-muted">
        Рабочее пространство
      </div>
      <nav className="mt-3 space-y-1">
        {workspace.map((item) => (
          <NavLink key={item.href} item={item} active={isActive(pathname, item)} />
        ))}
      </nav>

      {/* Внизу — настройки (по правам) и помощь, как в макете. */}
      <nav className="mt-auto space-y-1">
        {canOpenSettings && <NavLink item={settings} active={isActive(pathname, settings)} />}
        <a href="#" className={`${linkClass} whitespace-nowrap`}>
          <CircleQuestionMark className="size-4 shrink-0" strokeWidth={1.75} />
          Помощь и поддержка
        </a>
      </nav>
    </aside>
  );
}
