import { LoginForm } from "./LoginForm";

export const metadata = { title: "Вход · OneBase" };

export default async function LoginPage({ searchParams }: { searchParams: Promise<{ next?: string }> }) {
  const { next } = await searchParams;
  return (
    <main className="grid min-h-screen place-items-center bg-page px-4">
      <div className="w-full max-w-sm">
        <div className="mb-6 flex items-center justify-center gap-2.5 text-ink">
          <svg viewBox="0 0 24 24" className="size-7 text-accent-strong" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden>
            <path d="M12 2.5 21.5 12 12 21.5 2.5 12Z" strokeLinejoin="round" />
            <path d="M12 8.5 15.5 12 12 15.5 8.5 12Z" fill="currentColor" stroke="none" />
          </svg>
          <span className="text-2xl font-semibold tracking-tight">OneBase</span>
        </div>
        <LoginForm next={next ?? "/sales"} />
      </div>
    </main>
  );
}
