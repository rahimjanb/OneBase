import { LoginForm } from "./LoginForm";

export const metadata = { title: "Вход · OneBase" };

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  const { next } = await searchParams;
  return (
    <main className="grid min-h-dvh place-items-center bg-page px-4">
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
