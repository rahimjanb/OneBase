"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import {
  Bell,
  CalendarCheck,
  ChartColumn,
  ClipboardList,
  Footprints,
  Home,
  LogOut,
  Map as MapIcon,
  Menu,
  Route,
  Settings,
  Sparkles,
  Store,
  Users,
  UsersRound,
  WifiOff,
  type LucideIcon,
} from "lucide-react";
import { ThemeToggle } from "@/components/shell/ThemeToggle";
import { roleLabel } from "@/lib/field/labels";
import type { FieldMe } from "@/lib/field/types";
import { FIELD_LOGOUT_PATH } from "@/lib/field/host";
import { ProfileMenu } from "./ProfileMenu";
import { Sheet } from "./Sheet";

type Item = { href: string; label: string; short?: string; icon: LucideIcon; exact?: boolean };

/** Разделы по роли. Агенту — день и свои данные; супервайзеру и РМ — команда, AI, состав; РМ с правом управления — настройки. */
function navFor(me: FieldMe): { items: Item[]; bottom: string[] } {
  if (me.role === "Agent") {
    return {
      items: [
        { href: "/field", label: "Сегодня", short: "Главная", icon: Home, exact: true },
        { href: "/field/routes", label: "Маршрут", icon: Route },
        { href: "/field/map", label: "Карта", icon: MapIcon },
        { href: "/field/tasks", label: "Задачи", icon: ClipboardList },
        { href: "/field/customers", label: "Точки", icon: Store },
        { href: "/field/visits", label: "Визиты", icon: Footprints },
        { href: "/field/kpi", label: "KPI", icon: ChartColumn },
      ],
      bottom: ["/field", "/field/routes", "/field/map", "/field/tasks"],
    };
  }

  const items: Item[] = [
    { href: "/field", label: me.role === "Rm" ? "Дашборд" : "Моя команда", short: "Главная", icon: Home, exact: true },
    { href: "/field/agents", label: "Агенты", icon: Users },
    { href: "/field/routes", label: "Маршруты", icon: Route },
    { href: "/field/map", label: "Карта", icon: MapIcon },
    { href: "/field/tasks", label: "Задачи", icon: ClipboardList },
    { href: "/field/customers", label: "Точки", icon: Store },
    { href: "/field/visits", label: "Визиты", icon: Footprints },
    { href: "/field/kpi", label: "KPI", icon: ChartColumn },
    { href: "/field/ai", label: "AI-планирование", short: "AI", icon: Sparkles },
    { href: "/field/team", label: "Команды и доступы", short: "Команды", icon: UsersRound },
  ];
  if (me.canManageOrg) items.push({ href: "/field/settings", label: "Настройки", icon: Settings });
  return { items, bottom: ["/field", "/field/agents", "/field/map", "/field/tasks"] };
}

const isActive = (pathname: string, item: Item) => (item.exact ? pathname === item.href : pathname === item.href || pathname.startsWith(`${item.href}/`));

function Brand({ compact = false }: { compact?: boolean }) {
  return (
    <Link href="/field" className="flex items-center gap-2.5" aria-label="Sales Base — на главную">
      <img src="/icons/field-192.png" alt="" className={compact ? "size-8 rounded-lg" : "size-9 rounded-xl"} />
      <span className="leading-tight">
        <span className="block text-[15px] font-semibold text-white">Sales Base</span>
        {!compact && <span className="block text-[11px] text-sidebar-muted">Field Sales Management</span>}
      </span>
    </Link>
  );
}

function useOnline() {
  const [online, setOnline] = useState(true);
  useEffect(() => {
    const update = () => setOnline(navigator.onLine);
    update();
    window.addEventListener("online", update);
    window.addEventListener("offline", update);
    return () => {
      window.removeEventListener("online", update);
      window.removeEventListener("offline", update);
    };
  }, []);
  return online;
}

/** Оболочка Sales Base: панель слева на большом экране, шапка и нижняя навигация на телефоне, баннер идущего визита. */
export function FieldShell({ me, children }: { me: FieldMe; children: React.ReactNode }) {
  // На домене Sales Base адреса без /field («/», «/tasks»): приводим к виду меню, иначе пункт не подсвечивается.
  const rawPath = usePathname();
  const pathname = rawPath === "/field" || rawPath.startsWith("/field/") ? rawPath : rawPath === "/" ? "/field" : `/field${rawPath}`;
  const online = useOnline();
  const [more, setMore] = useState(false);
  const { items, bottom } = navFor(me);
  const bottomItems = bottom.map((href) => items.find((i) => i.href === href)!).filter(Boolean);
  const moreItems = items.filter((i) => !bottom.includes(i.href));
  const moreActive = moreItems.some((i) => isActive(pathname, i));

  useEffect(() => setMore(false), [pathname]);

  // Сменился пользователь на устройстве — service worker очищает кэш Sales Base предыдущего.
  useEffect(() => {
    if (!("serviceWorker" in navigator)) return;
    navigator.serviceWorker.ready.then((reg) => reg.active?.postMessage({ type: "sales-base-user", id: me.userId })).catch(() => {});
  }, [me.userId]);

  const visit = me.activeVisit;

  return (
    <div data-product="field" className="flex min-h-dvh bg-page">
      {/* Панель слева — большой экран */}
      <aside className="sticky top-0 hidden h-screen w-[244px] shrink-0 flex-col bg-sidebar px-3 py-5 lg:flex">
        <div className="px-2">
          <Brand />
        </div>
        <div className="mt-7 px-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-sidebar-muted">{roleLabel[me.role]}</div>
        <nav className="mt-2 space-y-0.5 overflow-y-auto">
          {items.map((item) => {
            const active = isActive(pathname, item);
            return (
              <Link
                key={item.href}
                href={item.href}
                aria-current={active ? "page" : undefined}
                className={`flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm transition-colors ${active ? "bg-sidebar-active font-semibold text-white" : "text-sidebar-text hover:bg-white/5 hover:text-white"}`}
              >
                <item.icon className="size-4 shrink-0" strokeWidth={1.75} />
                {item.label}
              </Link>
            );
          })}
        </nav>
        <div className="mt-auto border-t border-white/10 pt-3">
          <ProfileMenu me={me} variant="sidebar" />
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        {/* Шапка: на телефоне — тёмная с логотипом; на большом экране — светлая полоса с данными и уведомлениями */}
        <header className="sticky top-0 z-30 bg-sidebar pt-[env(safe-area-inset-top)] text-sidebar-text lg:bg-page/90 lg:pt-0 lg:text-ink-2 lg:backdrop-blur">
          <div className="flex h-14 items-center gap-2 px-4 sm:px-6 lg:h-14">
            <div className="lg:hidden">
              <Brand compact />
            </div>
            {me.dataAsOf && <span className="hidden text-xs text-ink-3 lg:inline">данные Linko на {new Date(me.dataAsOf).toLocaleDateString("ru-RU")}</span>}
            <div className="ml-auto flex items-center gap-1">
              <ThemeToggle />
              <Link href="/field/notifications" aria-label="Уведомления" className="relative grid size-10 place-items-center rounded-full hover:bg-white/10 lg:hover:bg-muted">
                <Bell className="size-[18px]" strokeWidth={1.75} />
                {me.unreadNotifications > 0 && (
                  <span className="absolute right-1 top-1 grid min-w-[18px] place-items-center rounded-full bg-bad px-1 text-[10px] font-semibold leading-[18px] text-white">
                    {me.unreadNotifications > 99 ? "99+" : me.unreadNotifications}
                  </span>
                )}
              </Link>
              <ProfileMenu me={me} variant="header" />
            </div>
          </div>
          {!online && (
            <div className="flex items-center gap-2 bg-warn px-4 py-1.5 text-xs font-medium text-white sm:px-6">
              <WifiOff className="size-3.5" /> Нет связи — показаны последние загруженные данные. Действия отправятся, когда связь появится, — повторите их.
            </div>
          )}
          {visit && pathname !== `/field/customers/${visit.marketId}` && (
            <Link href={`/field/customers/${visit.marketId}`} className="flex items-center gap-2 bg-accent px-4 py-2 text-sm font-medium text-white sm:px-6">
              <CalendarCheck className="size-4 shrink-0" />
              <span className="min-w-0 flex-1 truncate">Идёт визит: {visit.marketName}</span>
              <span className="shrink-0 rounded-full bg-white/20 px-2 py-0.5 text-xs">Открыть</span>
            </Link>
          )}
        </header>

        <main className="flex-1 pb-[calc(4rem+env(safe-area-inset-bottom))] lg:pb-0">{children}</main>
      </div>

      {/* Нижняя навигация — телефон */}
      <nav aria-label="Разделы Sales Base" className="fixed inset-x-0 bottom-0 z-30 border-t border-line bg-surface/95 pb-[env(safe-area-inset-bottom)] backdrop-blur lg:hidden">
        <ul className="flex">
          {bottomItems.map((item) => {
            const active = isActive(pathname, item);
            return (
              <li key={item.href} className="min-w-0 flex-1">
                <Link href={item.href} aria-current={active ? "page" : undefined} className={`flex h-16 flex-col items-center justify-center gap-1 text-[11px] font-medium active:bg-muted ${active ? "text-accent-strong" : "text-ink-3"}`}>
                  <item.icon className="size-[22px]" strokeWidth={active ? 2.25 : 1.75} />
                  <span className="max-w-full truncate px-1">{item.short ?? item.label}</span>
                </Link>
              </li>
            );
          })}
          <li className="min-w-0 flex-1">
            <button type="button" onClick={() => setMore(true)} className={`flex h-16 w-full flex-col items-center justify-center gap-1 text-[11px] font-medium active:bg-muted ${moreActive ? "text-accent-strong" : "text-ink-3"}`}>
              <Menu className="size-[22px]" strokeWidth={moreActive ? 2.25 : 1.75} />
              Ещё
            </button>
          </li>
        </ul>
      </nav>

      <Sheet open={more} onClose={() => setMore(false)} title="Разделы">
        <div className="mb-3 rounded-xl bg-muted px-3 py-2.5 text-sm">
          <div className="font-medium text-ink">{me.name}</div>
          <div className="text-xs text-ink-3">
            {roleLabel[me.role]}
            {me.teamName ? ` · ${me.teamName}` : ""}
            {me.supervisorName ? ` · супервайзер ${me.supervisorName}` : ""}
          </div>
        </div>
        <div className="grid grid-cols-3 gap-2">
          {[...moreItems, { href: "/field/notifications", label: "Уведомления", icon: Bell }].map((item) => (
            <Link
              key={item.href}
              href={item.href}
              className={`flex aspect-square flex-col items-center justify-center gap-2 rounded-xl border text-center text-xs font-medium ${isActive(pathname, item) ? "border-accent bg-accent-soft text-accent-strong" : "border-line text-ink-2 active:bg-muted"}`}
            >
              <item.icon className="size-6" strokeWidth={1.75} />
              <span className="px-1">{item.short ?? item.label}</span>
            </Link>
          ))}
          <a href={FIELD_LOGOUT_PATH} className="flex aspect-square flex-col items-center justify-center gap-2 rounded-xl border border-line text-xs font-medium text-bad active:bg-muted">
            <LogOut className="size-6" strokeWidth={1.75} />
            Выйти
          </a>
        </div>
      </Sheet>
    </div>
  );
}
