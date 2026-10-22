import { LanguageToggle } from "@/components/shell/LanguageToggle";
import { getT } from "@/i18n/server";
import { LoginForm } from "./LoginForm";

export async function generateMetadata() {
  const t = await getT();
  return { title: t("Вход · OneBase") };
}

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  const { next } = await searchParams;
  return (
    <main className="relative grid min-h-dvh place-items-center bg-page px-4">
      {/* Язык — до входа: форма и ошибки сразу на нём. */}
      <div className="absolute right-3 top-[max(env(safe-area-inset-top),0.75rem)] text-ink-2">
        <LanguageToggle />
      </div>
      <div className="w-full max-w-sm">
        {/* Иконка приложения: читается и на светлом, и на тёмном фоне. */}
        <div className="mb-6 flex justify-center">
          <img src="/icons/icon-192.png" alt="OneBase" width={64} height={64} className="size-16 rounded-2xl shadow-sm" />
        </div>
        <LoginForm next={next ?? "/sales"} />
      </div>
    </main>
  );
}
