import Link from "next/link";
import { ArrowLeft } from "lucide-react";

export type Crumb = { label: string; href?: string };

/**
 * Заголовок страницы: «← Назад» и крошки слева, название, подзаголовок и действия справа.
 * Пользователь и тема — в общем Topbar.
 */
export function PageHeader({
  title,
  subtitle,
  breadcrumbs,
  actions,
  back,
}: {
  title: string;
  subtitle?: string;
  breadcrumbs?: Crumb[];
  actions?: React.ReactNode;
  /** Куда ведёт «← Назад» — кнопка стоит слева, перед крошками. */
  back?: string;
}) {
  return (
    <header className="border-b border-line">
      <div className="mx-auto flex max-w-[1800px] flex-wrap items-start justify-between gap-x-6 gap-y-3 px-4 pb-5 pt-5 max-lg:pb-4 max-lg:pt-3 sm:px-6">
        <div className="min-w-0">
          {(back || breadcrumbs) && (
            <div className="mb-2 flex flex-wrap items-center gap-x-3 gap-y-1.5">
              {back && (
                <Link
                  href={back}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-line bg-surface px-2.5 py-1 text-xs font-medium text-ink shadow-sm hover:bg-muted max-lg:h-10 max-lg:px-3 max-lg:text-sm"
                >
                  <ArrowLeft className="size-3.5" />
                  Назад
                </Link>
              )}
              {breadcrumbs && (
                <nav aria-label="Навигация" className="flex flex-wrap items-center gap-1.5 text-xs text-ink-3 max-lg:hidden">
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
            </div>
          )}
          <h1 className="text-[22px] font-semibold leading-tight tracking-tight text-ink sm:text-[26px] lg:text-[28px]">{title}</h1>
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
