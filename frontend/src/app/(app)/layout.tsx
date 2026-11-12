import { Suspense } from "react";
import { AppBridge } from "@/components/shell/AppBridge";
import { HomeKindProvider } from "@/components/shell/HomeKind";
import { PullToRefresh, ScrollManager } from "@/components/shell/AppScroll";
import { MobileNav } from "@/components/shell/MobileNav";
import { NavPending } from "@/components/shell/NavPending";
import { Sidebar } from "@/components/shell/Sidebar";
import { Topbar } from "@/components/shell/Topbar";
import { TopbarFrame } from "@/components/shell/TopbarFrame";
import { TaskToastHost } from "@/components/tasks/TaskToast";
import { WhatsNewHost } from "@/components/whats-new/WhatsNew";
import { screenPermissions } from "@/lib/navigation";
import { apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  // Кто вошёл: имя и роль в шапке, «Настройки» в меню — только при правах на них.
  // Без сессии на вход ведёт proxy; недействительная сессия (401) — выход и вход заново.
  const me = await apiSession<Me>("/api/auth/me");
  const canOpenSettings = !!me?.canOpenSettings;
  // Разделы меню — по правам; если API недоступен (me = null), меню не прячем — страницы сами проверят доступ.
  // Руководителю отдела раздел HR открыт без права «HR» — признаком из /api/auth/me (данные backend отдаёт только по его подразделениям).
  // Заявки на сотрудника — признак hr.requests (руководителю отдела вкладка «Заявки на сотрудника» открыта и без права «HR»).
  // «Отделы» — признак departments.open: виден, если открывается хотя бы один отдел.
  const permissions = me ? screenPermissions(me) : null;
  // Оболочка как у приложения: закреплена на весь экран, верхняя и нижняя панели не двигаются,
  // прокручивается только <main data-app-scroll>. Вид главной (HomeKind) — меню, шапке и макету перехода на «/».
  return (
    <HomeKindProvider home={me?.home ?? "overview"}>
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
        {/* Отклик на переходы без макета; useSearchParams — внутри Suspense. */}
        <Suspense fallback={null}>
          <NavPending />
        </Suspense>
        <AppBridge userId={me?.id ?? null} />
        {/* «Задача создана · Открыть» — после закрытия формы задачи. */}
        <TaskToastHost />
        {/* «Что нового» после обновления — один раз на версию, только карточки открытых пользователю разделов. */}
        <WhatsNewHost me={me} permissions={permissions} />
      </div>
    </HomeKindProvider>
  );
}
