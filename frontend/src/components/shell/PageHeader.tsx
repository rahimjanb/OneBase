import Link from "next/link";

export type Crumb = { label: string; href?: string };

/** Заголовок страницы: крошки, название, подзаголовок и действия справа. Пользователь и тема — в общем Topbar. */
export function PageHeader({
  title,
  subtitle,
  breadcrumbs,
  actions,
}: {
  title: string;
  subtitle?: string;
  breadcrumbs?: Crumb[];
  actions?: React.ReactNode;
}) {
  return (
    <header className="border-b border-line">
      <div className="mx-auto flex max-w-[1800px] flex-wrap items-start justify-between gap-x-6 gap-y-3 px-4 pb-5 pt-5 sm:px-6">
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
        {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
      </div>
    </header>
  );
}

/** Контейнер содержимого страницы под шапкой. */
export function PageBody({ children }: { children: React.ReactNode }) {
  return <div className="mx-auto max-w-[1800px] px-4 py-6 sm:px-6">{children}</div>;
}
