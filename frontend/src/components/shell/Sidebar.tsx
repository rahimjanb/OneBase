"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Check,
  CircleDot,
  CircleQuestionMark,
  Columns2,
  Grid3x3,
  Rows3,
  Settings,
  Sparkle,
  type LucideIcon,
} from "lucide-react";

/** also — разделы, которые открываются из этого пункта и подсвечивают его (продажи — из «Отделов»). */
type NavItem = { href: string; label: string; icon: LucideIcon; also?: string[] };

const workspace: NavItem[] = [
  { href: "/", label: "Dashboard", icon: Columns2 },
  { href: "/departments", label: "Отделы", icon: Grid3x3, also: ["/sales"] },
  { href: "/tasks", label: "Задачи", icon: Check },
  { href: "/reports", label: "Отчёты", icon: Rows3 },
  { href: "/base", label: "Общая база", icon: CircleDot },
  { href: "/ai", label: "AI Consultants", icon: Sparkle },
];

const settings: NavItem = { href: "/settings", label: "Настройки", icon: Settings };

function isActive(pathname: string, item: NavItem) {
  const under = (href: string) => pathname === href || pathname.startsWith(`${href}/`);
  return item.href === "/" ? pathname === "/" : under(item.href) || (item.also ?? []).some(under);
}

export function Logo() {
  return (
    <Link href="/" className="flex items-center gap-2.5 text-white">
      <svg viewBox="0 0 24 24" className="size-6" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
        <path d="M12 2.5 21.5 12 12 21.5 2.5 12Z" strokeLinejoin="round" />
        <path d="M12 8.5 15.5 12 12 15.5 8.5 12Z" fill="currentColor" stroke="none" />
      </svg>
      <span className="text-xl font-semibold tracking-tight">OneBase</span>
    </Link>
  );
}

function NavLink({ item, active }: { item: NavItem; active: boolean }) {
  const Icon = item.icon;
  return (
    <Link
      href={item.href}
      aria-current={active ? "page" : undefined}
      className={
        active
          ? "flex items-center gap-3 rounded-lg bg-sidebar-active px-3 py-2.5 text-sm font-semibold text-white"
          : "flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm text-sidebar-text transition-colors hover:bg-white/5 hover:text-white"
      }
    >
      <Icon className="size-4 shrink-0" strokeWidth={1.75} />
      {item.label}
    </Link>
  );
}

export function Sidebar() {
  const pathname = usePathname();

  return (
    <>
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

        <nav className="mt-6">
          <NavLink item={settings} active={isActive(pathname, settings)} />
        </nav>

        <div className="mt-auto border-t border-white/10 px-3 pt-4">
          <a href="#" className="flex items-center gap-2 whitespace-nowrap text-sm text-sidebar-text hover:text-white">
            <CircleQuestionMark className="size-4" strokeWidth={1.75} />
            Помощь и поддержка
          </a>
        </div>
      </aside>

      {/* Мобильная навигация */}
      <div className="bg-sidebar px-4 pb-2 pt-4 lg:hidden">
        <Logo />
        <nav className="-mx-1 mt-3 flex gap-1 overflow-x-auto pb-1">
          {[...workspace, settings].map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className={
                isActive(pathname, item)
                  ? "shrink-0 rounded-lg bg-sidebar-active px-3 py-1.5 text-sm font-semibold text-white"
                  : "shrink-0 rounded-lg px-3 py-1.5 text-sm text-sidebar-text"
              }
            >
              {item.label}
            </Link>
          ))}
        </nav>
      </div>
    </>
  );
}
