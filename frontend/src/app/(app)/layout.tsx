import { Sidebar } from "@/components/shell/Sidebar";
import { Topbar } from "@/components/shell/Topbar";
import { apiSession } from "@/lib/server-api";
import type { Me } from "@/lib/users";

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  // Кто вошёл: имя и роль в шапке, «Настройки» в меню — только при правах на них.
  // Без сессии на вход ведёт proxy; недействительная сессия (401) — выход и вход заново.
  const me = await apiSession<Me>("/api/auth/me");
  return (
    <div className="min-h-screen lg:flex">
      <Sidebar canOpenSettings={!!me?.canOpenSettings} />
      <main className="min-w-0 flex-1">
        <Topbar me={me} />
        {children}
      </main>
    </div>
  );
}
