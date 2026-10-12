"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  ChartNoAxesColumn,
  Check,
  CircleQuestionMark,
  Database,
  DatabaseZap,
  Grid3x3,
  LayoutGrid,
  SlidersHorizontal,
  Sparkles,
  type LucideIcon,
} from "lucide-react";
import { canOpen } from "@/lib/navigation";

/**
 * also — разделы, которые открываются из этого пункта и подсвечивают его (продажи — из «Отделов»).
 * soon — раздел ещё не сделан: пункт виден, но не кликается.
 * short — короткая подпись для нижней панели на телефоне.
 */
export type NavItem = { href: string; label: string; icon: LucideIcon; also?: string[]; soon?: boolean; short?: string };

export const workspace: NavItem[] = [
  { href: "/", label: "Обзор", icon: LayoutGrid },
  { href: "/departments", label: "Отделы", icon: Grid3x3, also: ["/sales"] },
  { href: "/tasks", label: "Задачи", icon: Check },
  { href: "/reports", label: "Отчёты", icon: ChartNoAxesColumn, soon: true },
  { href: "/base", label: "Файлы", icon: Database, also: ["/files"] },
  { href: "/consultant", label: "AI-консультант", icon: Sparkles, also: ["/ai"], short: "AI" },
  { href: "/data-manager", label: "AI Data Manager", icon: DatabaseZap, short: "Данные" },
];

export const settings: NavItem = { href: "/settings", label: "Настройки", icon: SlidersHorizontal };

/** Пункты меню, доступные пользователю (права — из /api/auth/me; без прав раздел скрыт, как и «Настройки»). */
export const visibleWorkspace = (permissions: readonly string[] | null) =>
  permissions === null ? workspace : workspace.filter((item) => canOpen(item.href, permissions));

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

const linkClass = "flex items-center gap-3 rounded-xl px-3 py-2.5 text-[13px] text-sidebar-text transition-colors hover:bg-white/5 hover:text-white";

/** Активный пункт — «стеклянная» плашка: белый 10 %, обводка белый 15 %, размытие фона. */
const activeClass =
  "flex items-center gap-3 rounded-xl bg-sidebar-active px-3 py-2.5 text-[13px] font-semibold text-white ring-1 ring-inset ring-white/15 backdrop-blur-md";

function NavLink({ item, active }: { item: NavItem; active: boolean }) {
  const Icon = item.icon;
  if (item.soon) {
    return (
      <span aria-disabled="true" title="Раздел в разработке" className="flex cursor-default items-center gap-3 rounded-xl px-3 py-2.5 text-[13px] text-sidebar-muted">
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
      className={active ? activeClass : linkClass}
    >
      <Icon className="size-4 shrink-0" strokeWidth={1.75} />
      {item.label}
    </Link>
  );
}

/**
 * Боковая навигация на больших экранах. На телефоне вместо неё — шапка (Topbar) и нижняя панель (MobileNav).
 * canOpenSettings — есть права раздела «Настройки»; обычным сотрудникам пункт не показывается.
 * permissions — права пользователя: пункты, к которым нет доступа, скрыты (роль «Админ 2» видит только «Файлы» и «AI Data Manager»).
 */
export function Sidebar({ canOpenSettings, permissions }: { canOpenSettings: boolean; permissions: readonly string[] | null }) {
  const pathname = usePathname();

  return (
    <aside className="hidden h-full w-[232px] shrink-0 flex-col overflow-y-auto border-r border-white/[0.07] bg-sidebar px-3 pb-6 lg:flex">
      {/* Высота — как у верхней панели (72px): логотип и панель — одна строка. */}
      <div className="flex h-[72px] shrink-0 items-center px-3">
        <Logo />
      </div>

      <div className="mt-5 px-3 text-[10px] font-semibold uppercase tracking-[1px] text-sidebar-muted">
        Рабочее пространство
      </div>
      <nav className="mt-3 space-y-1">
        {visibleWorkspace(permissions).map((item) => (
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
