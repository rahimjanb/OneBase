"use client";

import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { useT } from "@/i18n/client";

export type Crumb = { label: string; href?: string };

/**
 * Заголовок страницы: «← Назад» и крошки слева, название, подзаголовок и действия справа.
 * Пользователь и тема — в общем Topbar. Заголовок, подзаголовок и крошки проходят через перевод: страница может передать уже
 * переведённый текст (тогда он не меняется) или русский — он переведётся по словарю; имена из данных остаются как есть.
 * Не текст (плашка-заготовка в макете страницы, RouteSkeleton) выводится как есть.
 * variant="display" — шапка страницы-сводки по макету («Продажи → Вторичка»): без линии снизу, крупное название 30/700, действия справа
 * прижаты к низу (к подзаголовку); отступ до содержимого задаёт PageBody с тем же variant.
 */
export function PageHeader({
  title,
  subtitle,
  breadcrumbs,
  actions,
  back,
  variant,
}: {
  title: React.ReactNode;
  subtitle?: React.ReactNode;
  breadcrumbs?: Crumb[];
  actions?: React.ReactNode;
  /** Куда ведёт «← Назад» — кнопка стоит слева, перед крошками. */
  back?: string;
  variant?: "display";
}) {
  const t = useT();
  const display = variant === "display";
  return (
    <header className={display ? undefined : "border-b border-line"}>
      <div
        className={`mx-auto flex max-w-[1800px] flex-wrap justify-between gap-x-6 gap-y-3 px-4 sm:px-6 ${display ? "items-end pt-6 max-lg:pt-3" : "items-start pb-5 pt-5 max-lg:pb-4 max-lg:pt-3"}`}
      >
        <div className="min-w-0">
          {(back || breadcrumbs) && (
            <div className={`flex flex-wrap items-center gap-x-3 gap-y-1.5 ${display ? "mb-1.5" : "mb-2"}`}>
              {back && (
                <Link
                  href={back}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-line bg-surface px-2.5 py-1 text-xs font-medium text-ink shadow-sm hover:bg-muted max-lg:h-10 max-lg:px-3 max-lg:text-sm"
                >
                  <ArrowLeft className="size-3.5" />
                  {t("Назад")}
                </Link>
              )}
              {breadcrumbs && (
                <nav aria-label={t("Навигация")} className={`flex flex-wrap items-center gap-1.5 text-xs text-ink-3 max-lg:hidden ${display ? "font-medium" : ""}`}>
                  {breadcrumbs.map((crumb, i) => (
                    <span key={`${crumb.label}-${i}`} className="flex items-center gap-1.5">
                      {i > 0 && <span aria-hidden>/</span>}
                      {crumb.href ? (
                        <Link href={crumb.href} className="hover:text-ink">
                          {t(crumb.label)}
                        </Link>
                      ) : (
                        <span className="text-ink-2">{t(crumb.label)}</span>
                      )}
                    </span>
                  ))}
                </nav>
              )}
            </div>
          )}
          <h1
            className={
              display
                ? "text-[24px] font-bold leading-tight tracking-[-0.5px] text-ink lg:text-[30px]"
                : "text-[1.375rem] font-semibold leading-tight tracking-tight text-ink sm:text-[1.625rem] lg:text-[1.75rem]"
            }
          >
            {typeof title === "string" ? t(title) : title}
          </h1>
          {subtitle && <p className={`mt-1.5 text-sm text-ink-2 ${display ? "leading-[18px]" : ""}`}>{typeof subtitle === "string" ? t(subtitle) : subtitle}</p>}
        </div>
        {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
      </div>
    </header>
  );
}

/** Контейнер содержимого страницы под шапкой; variant="display" — под шапкой без линии: 18px сверху, 40px снизу (макет «Вторички»). */
export function PageBody({ children, variant }: { children: React.ReactNode; variant?: "display" }) {
  return <div className={`mx-auto max-w-[1800px] px-4 sm:px-6 ${variant === "display" ? "pb-10 pt-[18px] max-md:pt-3.5" : "py-6"}`}>{children}</div>;
}
