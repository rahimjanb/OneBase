import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { PageBody, PageHeader, type Crumb } from "@/components/shell/PageHeader";
import { periodQuery, type SalesSearchParams } from "@/lib/sales/query";

/**
 * Заголовок и тело страницы раздела «Продажи»: крошки, название, «← Назад».
 * Разделы, период и «Обновить» — в постоянной панели раздела (sales/layout.tsx).
 */
export function SalesFrame({
  title,
  subtitle,
  crumbs = [],
  back,
  sp,
  children,
}: {
  title: string;
  subtitle?: string;
  crumbs?: Crumb[];
  back?: string;
  sp: SalesSearchParams;
  children: React.ReactNode;
}) {
  const query = periodQuery(sp);

  return (
    <>
      <PageHeader
        title={title}
        subtitle={subtitle}
        breadcrumbs={[{ label: "OneBase", href: "/" }, { label: "Продажи", href: query ? `/sales?${query}` : "/sales" }, ...crumbs]}
        actions={
          back && (
            <Link href={back} className="inline-flex items-center gap-1.5 rounded-lg border border-line bg-surface px-3 py-1.5 text-xs font-medium text-ink hover:bg-muted">
              <ArrowLeft className="size-3.5" />
              Назад
            </Link>
          )
        }
      />
      <PageBody>{children}</PageBody>
    </>
  );
}
