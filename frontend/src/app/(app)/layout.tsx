import { Suspense } from "react";
import { AppBridge } from "@/components/shell/AppBridge";
import { PullToRefresh, ScrollManager } from "@/components/shell/AppScroll";
import { MobileNav } from "@/components/shell/MobileNav";
import { NavProgress } from "@/components/shell/NavProgress";
import { Sidebar } from "@/components/shell/Sidebar";
import { Topbar } from "@/components/shell/Topbar";
import { TopbarFrame } from "@/components/shell/TopbarFrame";
import { apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  // Кто вошёл: имя и роль в шапке, «Настройки» в меню — только при правах на них.
  // Без сессии на вход ведёт proxy; недействительная сессия (401) — выход и вход заново.
  const me = await apiSession<Me>("/api/auth/me");
  const canOpenSettings = !!me?.canOpenSettings;
  // Разделы меню — по правам; если API недоступен (me = null), меню не прячем — страницы сами проверят доступ.
  // Руководителю отдела раздел HR открыт без права «HR» — признаком из /api/auth/me (данные backend отдаёт только по его подразделениям).
  const permissions = me ? (me.hrAccess ? [...me.permissions, "hr.department"] : me.permissions) : null;
  // Оболочка как у приложения: закреплена на весь экран, верхняя и нижняя панели не двигаются,
  // прокручивается только <main data-app-scroll>.
  return (
    <div data-app-shell className="fixed inset-0 flex overflow-hidden bg-page">
      <Sidebar canOpenSettings={canOpenSettings} permissions={permissions} />
      <div className="flex min-w-0 flex-1 flex-col">
        <TopbarFrame>
          <Topbar me={me} permissions={permissions} />
        </TopbarFrame>
        <div className="relative min-h-0 flex-1">
          <PullToRefresh />
          <main data-app-scroll className="h-full">
            {children}
          </main>
        </div>
        <MobileNav me={me} canOpenSettings={canOpenSettings} permissions={permissions} />
      </div>
      <ScrollManager />
      {/* Полоска загрузки при переходах; useSearchParams — внутри Suspense. */}
      <Suspense fallback={null}>
        <NavProgress />
      </Suspense>
      <AppBridge userId={me?.id ?? null} />
    </div>
  );
}
