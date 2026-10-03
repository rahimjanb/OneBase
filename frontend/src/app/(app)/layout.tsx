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
  return (
    <div className="min-h-dvh lg:flex">
      <Sidebar canOpenSettings={canOpenSettings} />
      {/* На телефоне снизу — панель навигации: оставляем под неё место, с учётом safe area. */}
      <main className="min-w-0 flex-1 pb-[calc(3.5rem+env(safe-area-inset-bottom))] lg:pb-0">
        <Topbar me={me} />
        {children}
      </main>
      <MobileNav canOpenSettings={canOpenSettings} />
    </div>
  );
}
