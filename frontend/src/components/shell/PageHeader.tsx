import Link from "next/link";
import { Bell, ChevronDown } from "lucide-react";
import { currentUser } from "@/lib/demo-data";

export type Crumb = { label: string; href?: string };

function UserBar() {
  return (
    <div className="flex shrink-0 items-center gap-4">
      <button
        type="button"
        aria-label="Уведомления"
        className="relative grid size-9 place-items-center rounded-full border border-line bg-white text-ink-2 hover:text-ink"
      >
        <Bell className="size-4" strokeWidth={1.75} />
        <span className="absolute right-1.5 top-1.5 size-2 rounded-full bg-accent ring-2 ring-white" />
      </button>
      <button type="button" className="flex items-center gap-3 text-left">
        <span className="grid size-10 place-items-center rounded-full bg-accent-soft text-sm font-semibold text-accent-strong">
          {currentUser.initials}
        </span>
        <span className="hidden sm:block">
          <span className="block text-sm font-semibold text-ink">{currentUser.login}</span>
          <span className="block text-xs text-ink-3">{currentUser.role}</span>
        </span>
        <ChevronDown className="ml-3 hidden size-4 text-ink-3 sm:block" />
      </button>
    </div>
  );
}

export function PageHeader({
  title,
  subtitle,
  breadcrumbs,
}: {
  title: string;
  subtitle?: string;
  breadcrumbs?: Crumb[];
}) {
  return (
    <header className="border-b border-line">
      <div className="mx-auto flex max-w-[1240px] items-start justify-between gap-6 px-4 pb-5 pt-6 sm:px-8">
        <div className="min-w-0">
          {breadcrumbs && (
            <nav aria-label="Навигация" className="mb-2 flex flex-wrap items-center gap-1.5 text-xs text-ink-3">
              {breadcrumbs.map((crumb, i) => (
                <span key={`${crumb.label}-${i}`} className="flex items-center gap-1.5">
                  {i > 0 && <span aria-hidden>/</span>}
                  {crumb.href ? (
                    <Link href={crumb.href} className="hover:text-ink">
                      {crumb.label}
                    </Link>
                  ) : (
                    <span className="text-ink-2">{crumb.label}</span>
                  )}
                </span>
              ))}
            </nav>
          )}
          <h1 className="text-[26px] font-semibold leading-tight tracking-tight text-ink sm:text-[28px]">{title}</h1>
          {subtitle && <p className="mt-1.5 text-sm text-ink-2">{subtitle}</p>}
        </div>
        <UserBar />
      </div>
    </header>
  );
}

/** Контейнер содержимого страницы под шапкой. */
export function PageBody({ children }: { children: React.ReactNode }) {
  return <div className="mx-auto max-w-[1240px] px-4 py-6 sm:px-8">{children}</div>;
}
