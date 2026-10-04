import { AppBridge } from "@/components/shell/AppBridge";
import { PullToRefresh, ScrollManager } from "@/components/shell/AppScroll";
import { MobileNav } from "@/components/shell/MobileNav";
import { Sidebar } from "@/components/shell/Sidebar";
import { Topbar } from "@/components/shell/Topbar";
import { apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  // Кто вошёл: имя и роль в шапке, «Настройки» в меню — только при правах на них.
  // Без сессии на вход ведёт proxy; недействительная сессия (401) — выход и вход заново.
  const me = await apiSession<Me>("/api/auth/me");
  const canOpenSettings = !!me?.canOpenSettings;
  // Оболочка как у приложения: закреплена на весь экран, верхняя и нижняя панели не двигаются,
  // прокручивается только <main data-app-scroll>.
  return (
    <div data-app-shell className="fixed inset-0 flex overflow-hidden bg-page">
      <Sidebar canOpenSettings={canOpenSettings} />
      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar me={me} />
        <div className="relative min-h-0 flex-1">
          <PullToRefresh />
          <main data-app-scroll className="h-full">
            {children}
          </main>
        </div>
        <MobileNav canOpenSettings={canOpenSettings} />
      </div>
      <ScrollManager />
      <AppBridge userId={me?.id ?? null} />
    </div>
  );
}
